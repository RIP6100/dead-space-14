// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.Research;
using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Research;

public sealed class HexGridControl : Control
{
    private const float HexRadius = 32f;

    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprites;
    private readonly SharedResearchMosaicSystem _mosaic;
    private readonly IGameTiming _timing;

    private static readonly Color CellColor = Color.FromHex("#6a5f7a");
    private static readonly Color ValidColor = Color.FromHex("#4caf50");
    private static readonly Color InvalidColor = Color.FromHex("#e04040");
    private static readonly Color BgColor = Color.FromHex("#1c1528");
    private static readonly Color CellFill = Color.FromHex("#0b0810");
    private static readonly Color EndpointColor = Color.FromHex("#ffd27f");
    private static readonly Color HintColor = Color.FromHex("#79d1ff");
    private static readonly Color SolvedColor = Color.FromHex("#7cff9a");
    private static readonly Color PaperInk = Color.FromHex("#2b2521"); // «чернила» режима распечатки
    private readonly List<Vector2i> _cells = new();
    private readonly Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>> _placed = new();
    private readonly HashSet<Vector2i> _endpoints = new();
    private ProtoId<ResearchFieldPrototype>? _brush;
    private IReadOnlyDictionary<ProtoId<ResearchFieldPrototype>, int>? _data;
    private readonly Dictionary<ProtoId<ResearchFieldPrototype>, int> _localData = new();
    public bool ReadOnly;
    public bool PaperMode;
    public event Action<Vector2i, ProtoId<ResearchFieldPrototype>>? OnPlaceRequest;
    public event Action<Vector2i>? OnRemoveRequest;
    public event Action? OnChanged;
    private readonly Dictionary<ProtoId<ResearchFieldPrototype>, (Texture? Tex, Color Color)> _visuals = new();
    private readonly Font _font;
    private Vector2i? _hoverCell;
    private readonly Vector2[] _quad = new Vector2[4];
    private readonly Label _damagedLabel;

    public HexGridControl()
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
        var entMan = IoCManager.Resolve<IEntityManager>();
        _sprites = entMan.System<SpriteSystem>();
        _mosaic = entMan.System<SharedResearchMosaicSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
        MinSize = new Vector2(380, 380);
        MouseFilter = MouseFilterMode.Stop;

        _damagedLabel = new Label
        {
            Text = Loc.GetString("research-mosaic-damaged"),
            Align = Label.AlignMode.Center,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = InvalidColor,
            StyleClasses = { "LabelBig" },
            Visible = false,
        };
        AddChild(_damagedLabel);
    }
    public void SetBoard(MosaicBoard board)
    {
        _cells.Clear();
        _placed.Clear();
        _endpoints.Clear();
        _damagedLabel.Visible = false;

        var holes = new HashSet<Vector2i>(board.Holes);
        var radius = board.Radius;
        for (var q = -radius; q <= radius; q++)
        {
            for (var r = -radius; r <= radius; r++)
            {
                var s = -q - r;
                if (Math.Abs(q) <= radius && Math.Abs(r) <= radius && Math.Abs(s) <= radius)
                {
                    var cell = new Vector2i(q, r);
                    if (!holes.Contains(cell))
                        _cells.Add(cell);
                }
            }
        }

        foreach (var endpoint in board.Endpoints)
        {
            _placed[endpoint.Pos] = endpoint.Field;
            _endpoints.Add(endpoint.Pos);
        }

        UpdateSize();
    }
    public void Clear()
    {
        _cells.Clear();
        _placed.Clear();
        _endpoints.Clear();
        _brush = null;
        _damagedLabel.Visible = false;
        UpdateSize();
    }
    public void SetDamaged()
    {
        _cells.Clear();
        _placed.Clear();
        _endpoints.Clear();
        _brush = null;
        _damagedLabel.Visible = true;
        UpdateSize();
    }

    private void UpdateSize()
    {
        if (_cells.Count == 0)
        {
            MinSize = PaperMode ? new Vector2(240, 96) : new Vector2(380, 380);
            return;
        }

        var extX = 0f;
        var extY = 0f;
        foreach (var cell in _cells)
        {
            var p = AxialToPixel(cell);
            extX = MathF.Max(extX, MathF.Abs(p.X));
            extY = MathF.Max(extY, MathF.Abs(p.Y));
        }

        var margin = HexRadius + 6f;
        var size = new Vector2((extX + margin) * 2f, (extY + margin) * 2f);
        MinSize = PaperMode ? size : new Vector2(MathF.Max(380, size.X), MathF.Max(380, size.Y));
    }
    public void SetData(IReadOnlyDictionary<ProtoId<ResearchFieldPrototype>, int> data)
    {
        _data = data;
        _localData.Clear();
        foreach (var (field, amount) in data)
            _localData[field] = amount;
    }
    private int Remaining(ProtoId<ResearchFieldPrototype> field)
    {
        if (_data is null)
            return int.MaxValue;
        return _localData.TryGetValue(field, out var amount) ? amount : 0;
    }
    public bool CanAfford(ProtoId<ResearchFieldPrototype> field) => Remaining(field) > 0;
    public int RemainingData(ProtoId<ResearchFieldPrototype> field) => Remaining(field);
    private (Texture? Tex, Color Color) GetVisual(ProtoId<ResearchFieldPrototype> field)
    {
        if (!_visuals.TryGetValue(field, out var visual))
        {
            var proto = _proto.Index(field);
            var tex = proto.HexSprite is { } spec ? _sprites.Frame0(spec) : null;
            visual = (tex, proto.Color);
            _visuals[field] = visual;
        }
        return visual;
    }
    public Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>> GetPlacement()
    {
        return new Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>>(_placed);
    }
    public IEnumerable<Vector2i> PlayerCells => _placed.Keys.Where(c => !_endpoints.Contains(c)).ToList();
    public void RemoveAllPlayer()
    {
        foreach (var cell in PlayerCells)
            _placed.Remove(cell);
        OnChanged?.Invoke();
    }
    public void LoadPlacement(Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>> placement)
    {
        foreach (var (cell, field) in placement)
        {
            if (_cells.Contains(cell))
                _placed[cell] = field;
        }
    }
    public void SetBrush(ProtoId<ResearchFieldPrototype> field)
    {
        _brush = field;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var paper = PaperMode;
        if (!paper)
            handle.DrawRect(new UIBox2(0, 0, PixelSize.X, PixelSize.Y), BgColor);

        var center = new Vector2(PixelSize.X / 2f, PixelSize.Y / 2f);
        var solved = SolvedComponent();
        var pulse = paper ? 1f : 0.5f + 0.5f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 3f);

        foreach (var cell in _cells)
        {
            Color outline;
            if (paper)
                outline = PaperInk.WithAlpha(0.55f);
            else
                outline = !ReadOnly && IsValidTarget(cell) ? HintColor : CellColor;
            DrawHex(handle, center + AxialToPixel(cell), HexRadius, outline, paper ? null : CellFill, paper ? 2f : 1f);
        }

        foreach (var (cell, field) in _placed)
        {
            var c0 = center + AxialToPixel(cell);
            foreach (var n in Neighbors(cell))
            {
                if (!_placed.TryGetValue(n, out var nField))
                    continue;
                if (cell.X > n.X || (cell.X == n.X && cell.Y > n.Y))
                    continue;

                var related = _mosaic.AreFieldsRelated(field, nField);
                var c1 = center + AxialToPixel(n);
                if (paper)
                    DrawThickLine(handle, c0, c1, related ? PaperInk : PaperInk.WithAlpha(0.18f), related ? 2f : 1.5f);
                else if (related && solved.Contains(cell) && solved.Contains(n))
                    DrawThickLine(handle, c0, c1, SolvedColor.WithAlpha(0.55f + 0.45f * pulse), 4f);
                else
                    DrawThickLine(handle, c0, c1, related ? ValidColor : InvalidColor, 2f);
            }
        }

        foreach (var (cell, field) in _placed)
        {
            var hexCenter = center + AxialToPixel(cell);

            if (_endpoints.Contains(cell) && !paper)
            {
                var ringAlpha = solved.Count > 0 ? 0.5f + 0.5f * pulse : 0.9f;
                ResearchDrawing.DrawRing(handle, hexCenter, HexRadius * 0.82f, EndpointColor.WithAlpha(ringAlpha), 2.5f, UIScale);
            }

            var (tex, color) = GetVisual(field);
            if (tex != null)
            {
                var rect = UIBox2.FromDimensions(hexCenter - new Vector2(16, 16), new Vector2(32, 32));
                handle.DrawTextureRect(tex, rect);
            }
            else
            {
                handle.DrawCircle(hexCenter, HexRadius * 0.6f, color);
                if (paper)
                    ResearchDrawing.DrawRing(handle, hexCenter, HexRadius * 0.6f, PaperInk, 2f, UIScale);
            }
        }

        if (!paper && !ReadOnly && _hoverCell is { } hover && !_placed.ContainsKey(hover) && _brush is { } brush && CanAfford(brush))
        {
            var hc = center + AxialToPixel(hover);
            var relates = RelatesToNeighbor(hover, brush);
            DrawHex(handle, hc, HexRadius, relates ? ValidColor : InvalidColor, null);

            var (tex, col) = GetVisual(brush);
            if (tex != null)
                handle.DrawTextureRect(tex, UIBox2.FromDimensions(hc - new Vector2(16, 16), new Vector2(32, 32)), col.WithAlpha(0.4f));
            else
                handle.DrawCircle(hc, HexRadius * 0.6f, col.WithAlpha(0.4f));
        }

        if (_hoverCell is { } hoveredCell && _placed.TryGetValue(hoveredCell, out var hoveredField))
        {
            var name = Loc.GetString(_proto.Index(hoveredField).Name);
            var labelPos = center + AxialToPixel(hoveredCell) + new Vector2(-HexRadius * 0.8f, HexRadius + 2);
            var labelColor = paper ? PaperInk : (_endpoints.Contains(hoveredCell) ? EndpointColor : Color.White);
            handle.DrawString(_font, labelPos, name, labelColor);
        }
    }
    private bool IsValidTarget(Vector2i cell)
    {
        if (_brush is not { } brush || !CanAfford(brush))
            return false;
        if (_placed.ContainsKey(cell) || !_cells.Contains(cell))
            return false;
        return RelatesToNeighbor(cell, brush);
    }

    private bool RelatesToNeighbor(Vector2i cell, ProtoId<ResearchFieldPrototype> field)
    {
        foreach (var n in Neighbors(cell))
        {
            if (_placed.TryGetValue(n, out var nField) && _mosaic.AreFieldsRelated(field, nField))
                return true;
        }
        return false;
    }

    private static Vector2i[] Neighbors(Vector2i c)
    {
        return new[]
        {
            new Vector2i(c.X + 1, c.Y), new Vector2i(c.X - 1, c.Y),
            new Vector2i(c.X, c.Y + 1), new Vector2i(c.X, c.Y - 1),
            new Vector2i(c.X + 1, c.Y - 1), new Vector2i(c.X - 1, c.Y + 1),
        };
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var cell = PixelToCell(args.RelativePixelPosition);
        _hoverCell = _cells.Contains(cell) ? cell : null;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hoverCell = null;
    }

    public bool IsSolved() => SolvedComponent().Count > 0;
    private HashSet<Vector2i> SolvedComponent()
    {
        var empty = new HashSet<Vector2i>();
        if (_endpoints.Count == 0)
            return empty;

        var start = default(Vector2i);
        foreach (var e in _endpoints)
        {
            start = e;
            break;
        }

        var visited = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!_placed.TryGetValue(cur, out var curField))
                continue;

            foreach (var n in Neighbors(cur))
            {
                if (visited.Contains(n) || !_placed.TryGetValue(n, out var nField))
                    continue;
                if (!_mosaic.AreFieldsRelated(curField, nField))
                    continue;

                visited.Add(n);
                queue.Enqueue(n);
            }
        }

        foreach (var e in _endpoints)
        {
            if (!visited.Contains(e))
                return empty;
        }
        return visited;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (ReadOnly)
            return;

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var cell = PixelToCell(args.RelativePixelPosition);
        if (!_cells.Contains(cell))
            return;
        if (_endpoints.Contains(cell))
            return;

        if (_placed.ContainsKey(cell))
        {
            _placed.Remove(cell);
            OnRemoveRequest?.Invoke(cell);
            OnChanged?.Invoke();
        }
        else if (_brush is { } field && CanAfford(field))
        {
            _placed[cell] = field;
            if (_localData.ContainsKey(field))
                _localData[field]--;
            OnPlaceRequest?.Invoke(cell, field);
            OnChanged?.Invoke();
        }

        args.Handle();
    }

    private static Vector2 AxialToPixel(Vector2i c)
    {
        var x = HexRadius * (MathF.Sqrt(3f) * c.X + MathF.Sqrt(3f) / 2f * c.Y);
        var y = HexRadius * (3f / 2f * c.Y);
        return new Vector2(x, y);
    }

    private void DrawHex(DrawingHandleScreen handle, Vector2 center, float radius, Color outline, Color? fill = null, float width = 1f)
    {
        var corners = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 180f * (60f * i - 30f); // -30° = pointy-top
            corners[i] = center + new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
        }

        if (fill is { } f)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, corners, f);

        for (var i = 0; i < 6; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 6];
            if (width > 1f)
                DrawThickLine(handle, a, b, outline, width);
            else
                handle.DrawLine(a, b, outline);
        }
    }
    private void DrawThickLine(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color, float width)
    {
        var dir = b - a;
        var len = dir.Length();
        if (len < 0.001f)
            return;
        dir /= len;
        var perp = new Vector2(-dir.Y, dir.X) * (width / 2f);
        _quad[0] = a + perp;
        _quad[1] = a - perp;
        _quad[2] = b - perp;
        _quad[3] = b + perp;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _quad, color);
    }

    private Vector2i PixelToCell(Vector2 pixel)
    {
        var center = new Vector2(PixelSize.X / 2f, PixelSize.Y / 2f);
        var p = pixel - center;

        var q = (MathF.Sqrt(3f) / 3f * p.X - 1f / 3f * p.Y) / HexRadius;
        var r = (2f / 3f * p.Y) / HexRadius;

        return AxialRound(q, r);
    }

    private static Vector2i AxialRound(float q, float r)
    {
        var s = -q - r;
        var rq = MathF.Round(q);
        var rr = MathF.Round(r);
        var rs = MathF.Round(s);

        var dq = MathF.Abs(rq - q);
        var dr = MathF.Abs(rr - r);
        var ds = MathF.Abs(rs - s);

        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;

        return new Vector2i((int) rq, (int) rr);
    }
}
