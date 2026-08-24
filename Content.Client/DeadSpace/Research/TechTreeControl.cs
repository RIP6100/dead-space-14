// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Shared.Research.Prototypes;
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

public sealed class TechTreeControl : LayoutContainer
{
    public event Action<ProtoId<TechnologyPrototype>>? OnTechSelected;
    public int NodeCount => _nodes.Count;

    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprite;
    private readonly IGameTiming _timing;
    private readonly Texture? _spaceTex;

    private const float NodeW = 52f;
    private const float NodeH = 52f;
    private const float NodeGap = 64f;
    private const float IconSize = 24f;
    private const float LineWidth = 2.5f;
    private const float ClusterGap = 55f;
    private const float MinZoom = 0.82f;
    private const float DotThreshold = 0.72f;

    private static readonly Color BgColor = Color.FromHex("#07060e");
    private static readonly Color ConstLine = new(0.62f, 0.72f, 1f, 0.26f);
    private static readonly Color ForeignColor = Color.FromHex("#181a20");
    private static readonly Color UnlockedColor = Color.FromHex("#2f7d43");
    private static readonly Color AvailableColor = Color.FromHex("#3a72c4");
    private static readonly Color LockedColor = Color.FromHex("#20242c");
    private static readonly Color FallbackBorder = Color.FromHex("#11141a");
    private static readonly Color LockedMod = new(0.5f, 0.5f, 0.56f);
    private static readonly Color LineColor = Color.FromHex("#6f6580"); 
    private static readonly Color LineDoneColor = Color.FromHex("#57c46f");
    private static readonly Color SelectColor = Color.FromHex("#ffd27f");
    private static readonly Color UnlockLine = Color.FromHex("#4fd8e6");
    private static readonly Color SearchColor = Color.FromHex("#e6c8ff");
    private static readonly Color DimMod = new(0.32f, 0.32f, 0.38f);

    private static readonly Vector2 Half = new(NodeW * 0.5f, NodeH * 0.5f);

    private readonly Dictionary<string, Vector2> _centers = new();
    private readonly Dictionary<string, Color> _nodeColors = new();
    private readonly Dictionary<string, List<string>> _prereqs = new();
    private readonly Dictionary<string, string> _names = new();
    private readonly HashSet<string> _supportedDisc = new();
    private readonly HashSet<string> _searchMatch = new();
    private bool _searching;
    private readonly List<(Control Ctrl, string Id)> _nodes = new();
    private readonly List<(string Parent, string Child, Color Color, bool Cross)> _links = new();
    private readonly Vector2[] _quad = new Vector2[4];

    private Vector2 _pan;
    private float _zoom = 1f;
    private float _maxRadius;
    private bool _panning;
    private Vector2 _pressPos;
    private bool _pressMoved;
    private bool _centered;
    private string? _selectedId;
    private readonly HashSet<string> _pathIds = new();
    private readonly Dictionary<string, List<string>> _children = new();
    private readonly HashSet<string> _unlockIds = new();

    public TechTreeControl()
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _sprite = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
        try { _spaceTex = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>("/Textures/Parallaxes/layer1.png").Texture; }
        catch { _spaceTex = null; }
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Stop;
        MinSize = Vector2.Zero;
    }

    public void SetTechs(List<TechnologyPrototype> techs, HashSet<string> available, HashSet<string> unlocked, HashSet<string> supportedDisc)
    {
        RemoveAllChildren();
        _centers.Clear();
        _nodeColors.Clear();
        _prereqs.Clear();
        _names.Clear();
        _supportedDisc.Clear();
        _supportedDisc.UnionWith(supportedDisc);
        _searchMatch.Clear();
        _searching = false;
        _nodes.Clear();
        _links.Clear();
        _pathIds.Clear();
        _children.Clear();
        _unlockIds.Clear();
        _selectedId = null;
        _centered = false;

        var ids = new HashSet<string>(techs.Select(t => t.ID));
        foreach (var t in techs)
            _prereqs[t.ID] = t.TechnologyPrerequisites.Where(p => ids.Contains(p)).Select(p => p.Id).ToList();

        foreach (var t in techs)
            _children[t.ID] = new List<string>();
        foreach (var (id, prs) in _prereqs)
            foreach (var pre in prs)
                _children[pre].Add(id);
        var byDisc = techs.GroupBy(t => t.Discipline.Id).OrderBy(g => g.Key).ToList();
        var clusters = new List<(List<TechnologyPrototype> Techs, Dictionary<string, Vector2> Local, float Radius)>();
        foreach (var g in byDisc)
        {
            var dt = g.ToList();
            var local = ComputeClusterLayout(dt);
            var rad = local.Count > 0 ? local.Values.Select(p => p.Length()).Max() : 0f;
            clusters.Add((dt, local, rad));
        }

        var maxR = clusters.Count > 0 ? clusters.Max(c => c.Radius) : 0f;
        var cell = 2f * maxR + ClusterGap;
        var cols = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(clusters.Count)));
        var rows = (clusters.Count + cols - 1) / cols;
        var origin = new Vector2(-(cols - 1) * cell / 2f, -(rows - 1) * cell / 2f);

        for (var k = 0; k < clusters.Count; k++)
        {
            var (dt, local, _) = clusters[k];
            var cc = origin + new Vector2((k % cols) * cell, (k / cols) * cell);

            foreach (var t in dt)
            {
                if (!local.TryGetValue(t.ID, out var lp))
                    continue;
                var isUnlocked = unlocked.Contains(t.ID);
                var isAvailable = available.Contains(t.ID);

                Control node;
                try { node = MakeNode(t, isUnlocked, isAvailable); }
                catch { node = MakeFallbackNode(t); }

                _centers[t.ID] = cc + lp;
                _nodeColors[t.ID] = NodeStateColor(t, isUnlocked, isAvailable);
                _names[t.ID] = Normalize(Loc.GetString(t.Name) + " " + t.ID);
                AddChild(node);
                _nodes.Add((node, t.ID));
            }
        }

        var discById = techs.ToDictionary(t => t.ID, t => t.Discipline.Id);
        foreach (var t in techs)
        {
            if (!_centers.ContainsKey(t.ID))
                continue;
            foreach (var pre in _prereqs[t.ID])
            {
                if (!_centers.ContainsKey(pre))
                    continue;
                var cross = discById.TryGetValue(pre, out var dp) && dp != discById[t.ID];
                _links.Add((pre, t.ID, unlocked.Contains(pre) ? LineDoneColor : LineColor, cross));
            }
        }

        _maxRadius = _centers.Values.Select(c => c.Length()).DefaultIfEmpty(0f).Max();
        ApplyPan();
    }

    private Dictionary<string, Vector2> ComputeClusterLayout(List<TechnologyPrototype> discTechs)
    {
        var result = new Dictionary<string, Vector2>();
        if (discTechs.Count == 0)
            return result;

        var ids = new HashSet<string>(discTechs.Select(t => t.ID));
        var nodes = discTechs.Select(t => t.ID).ToList();
        if (nodes.Count == 1)
        {
            result[nodes[0]] = Vector2.Zero;
            return result;
        }
        var edges = new List<(string A, string B)>();
        foreach (var t in discTechs)
            foreach (var pre in t.TechnologyPrerequisites)
                if (ids.Contains(pre))
                    edges.Add((pre.Id, t.ID));
        var pos = new Dictionary<string, Vector2>();
        var spread = NodeGap * MathF.Sqrt(nodes.Count) * 0.7f;
        foreach (var id in nodes)
            pos[id] = new Vector2(Hash01(id, 1) - 0.5f, Hash01(id, 2) - 0.5f) * spread;

        const float k = NodeGap;
        var disp = new Dictionary<string, Vector2>();
        var temp = k * 3f;

        for (var iter = 0; iter < 180; iter++)
        {
            foreach (var id in nodes)
                disp[id] = Vector2.Zero;
            for (var i = 0; i < nodes.Count; i++)
            for (var j = i + 1; j < nodes.Count; j++)
            {
                var delta = pos[nodes[i]] - pos[nodes[j]];
                var d = MathF.Max(delta.Length(), 0.01f);
                var f = delta / d * (k * k / d);
                disp[nodes[i]] += f;
                disp[nodes[j]] -= f;
            }
            foreach (var (a, b) in edges)
            {
                var delta = pos[a] - pos[b];
                var d = MathF.Max(delta.Length(), 0.01f);
                var f = delta / d * (d * d / k);
                disp[a] -= f;
                disp[b] += f;
            }
            foreach (var id in nodes)
                disp[id] -= pos[id] * 0.1f;
            foreach (var id in nodes)
            {
                var dp = disp[id];
                var dl = dp.Length();
                if (dl > 0.01f)
                    pos[id] += dp / dl * MathF.Min(dl, temp);
            }
            temp = MathF.Max(k * 0.1f, temp * 0.96f);
        }
        var minX = pos.Values.Min(p => p.X);
        var maxX = pos.Values.Max(p => p.X);
        var minY = pos.Values.Min(p => p.Y);
        var maxY = pos.Values.Max(p => p.Y);
        var ctr = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        foreach (var id in nodes)
            result[id] = pos[id] - ctr;

        return result;
    }
    private static float Hash01(string s, int salt)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in s)
                h = (h ^ c) * 16777619u;
            h = (h ^ (uint)salt) * 16777619u;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
    public void SetSelected(string? id)
    {
        _selectedId = id != null && _centers.ContainsKey(id) ? id : null;
        _pathIds.Clear();
        _unlockIds.Clear();
        if (_selectedId == null)
            return;

        var up = new Stack<string>();
        up.Push(_selectedId);
        while (up.Count > 0)
        {
            var cur = up.Pop();
            if (!_pathIds.Add(cur) || !_prereqs.TryGetValue(cur, out var pr))
                continue;
            foreach (var p in pr)
                up.Push(p);
        }
        _unlockIds.Add(_selectedId);
        if (_children.TryGetValue(_selectedId, out var direct))
            foreach (var c in direct)
                _unlockIds.Add(c);
    }
    public void SetSearch(string? query)
    {
        var q = Normalize(query ?? string.Empty);
        _searchMatch.Clear();
        _searching = q.Length > 0;
        if (_searching)
        {
            foreach (var (id, key) in _names)
                if (key.Contains(q))
                    _searchMatch.Add(id);
        }

        foreach (var (ctrl, id) in _nodes)
            ctrl.Modulate = !_searching || _searchMatch.Contains(id) ? Color.White : DimMod;

        if (_searching && _searchMatch.Count > 0 && Size.X > 1f)
        {
            var focus = _searchMatch.OrderBy(i => _centers[i].Length()).First();
            _pan = Size / 2f - _centers[focus] * _zoom;
            ApplyPan();
        }
    }

    private void ApplyPan()
    {
        var showNodes = _zoom >= DotThreshold;
        foreach (var (ctrl, id) in _nodes)
        {
            ctrl.Visible = showNodes;
            LayoutContainer.SetPosition(ctrl, _centers[id] * _zoom + _pan - Half);
        }
    }
    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    private Color DisciplineColor(TechnologyPrototype tech)
    {
        if (!string.IsNullOrEmpty(tech.Discipline.Id) && _proto.TryIndex(tech.Discipline, out var d))
            return d.Color;
        return FallbackBorder;
    }

    private Color NodeStateColor(TechnologyPrototype tech, bool isUnlocked, bool isAvailable)
    {
        if (_supportedDisc.Count > 0 && !_supportedDisc.Contains(tech.Discipline.Id))
            return ForeignColor;
        return isUnlocked ? UnlockedColor : isAvailable ? AvailableColor : LockedColor;
    }

    private Control MakeNode(TechnologyPrototype tech, bool isUnlocked, bool isAvailable)
    {
        var foreign = _supportedDisc.Count > 0 && !_supportedDisc.Contains(tech.Discipline.Id);
        var state = NodeStateColor(tech, isUnlocked, isAvailable);
        var disc = DisciplineColor(tech);
        var name = Loc.GetString(tech.Name);

        var box = new StyleBoxFlat
        {
            BackgroundColor = state,
            BorderColor = foreign ? FallbackBorder : isAvailable ? Color.InterpolateBetween(disc, Color.White, 0.35f) : disc,
            BorderThickness = new Thickness(!foreign && isAvailable ? 3 : 1),
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
        Texture? tex = null;
        try { tex = _sprite.Frame0(tech.Icon); }
        catch { /* я крутой */ }

        var icon = new TextureRect
        {
            Texture = tex,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MinSize = new Vector2(IconSize, IconSize),
            MaxSize = new Vector2(IconSize, IconSize),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };
        if (foreign || (!isUnlocked && !isAvailable))
            icon.ModulateSelfOverride = LockedMod;

        var label = new Label
        {
            Text = name,
            ClipText = true,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Bottom,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
        };

        var btn = new ContainerButton
        {
            StyleBoxOverride = box,
            ToolTip = name,
            MinSize = new Vector2(NodeW, NodeH),
            MaxSize = new Vector2(NodeW, NodeH),
            RectClipContent = true,
        };
        btn.AddChild(icon);
        btn.AddChild(label);
        var id = new ProtoId<TechnologyPrototype>(tech.ID);
        btn.OnPressed += _ => OnTechSelected?.Invoke(id);
        return btn;
    }

    private Control MakeFallbackNode(TechnologyPrototype tech)
    {
        var name = Loc.GetString(tech.Name);
        var box = new StyleBoxFlat
        {
            BackgroundColor = LockedColor,
            BorderColor = FallbackBorder,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
        };
        var label = new Label
        {
            Text = name,
            ClipText = true,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
        };
        var btn = new ContainerButton
        {
            StyleBoxOverride = box,
            ToolTip = name,
            MinSize = new Vector2(NodeW, NodeH),
            MaxSize = new Vector2(NodeW, NodeH),
            RectClipContent = true,
        };
        btn.AddChild(label);
        var id = new ProtoId<TechnologyPrototype>(tech.ID);
        btn.OnPressed += _ => OnTechSelected?.Invoke(id);
        return btn;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawSpaceBackground(handle, UIScale);

        if (!_centered && Size.X > 1f)
        {
            var fit = (MathF.Min(Size.X, Size.Y) * 0.5f - 48f) / MathF.Max(_maxRadius, 1f);
            _zoom = Math.Clamp(fit, MinZoom, 1.1f);
            _pan = Size / 2f;
            ApplyPan();
            _centered = true;
        }

        var s = UIScale;
        var dotMode = _zoom < DotThreshold;
        foreach (var (parent, child, color, cross) in _links)
        {
            var pc = _centers[parent] * _zoom + _pan;
            var cc = _centers[child] * _zoom + _pan;
            var dir = cc - pc;
            var len = dir.Length();
            if (len < 0.01f)
                continue;
            var u = dir / len;
            var e = dotMode ? 0f : EdgeDist(u);
            if (!dotMode && len <= e * 2f)
                continue;
            var from = (pc + u * e) * s;
            var to = (cc - u * e) * s;
            var up = _pathIds.Contains(parent) && _pathIds.Contains(child);
            var down = parent == _selectedId;

            if (up)
                DrawThickLine(handle, from, to, (LineWidth + 1.5f) * s, SelectColor);
            else if (down)
                DrawThickLine(handle, from, to, (LineWidth + 1.5f) * s, UnlockLine);
            else if (dotMode)
            {
                if (cross)
                    DrawDashedLine(handle, from, to, 0.7f * s, ConstLine.WithAlpha(0.06f), 6f * s, 11f * s);
                else
                    DrawThickLine(handle, from, to, 0.9f * s, ConstLine);
            }
            else if (cross)
                DrawDashedLine(handle, from, to, LineWidth * 0.7f * s, color.WithAlpha(0.14f), 6f * s, 11f * s);
            else
                DrawThickLine(handle, from, to, LineWidth * s, color);
        }

        if (dotMode)
        {
            DrawDots(handle, s);
            base.Draw(handle);
            return;
        }

        foreach (var id in _unlockIds)
            if (id != _selectedId && _centers.TryGetValue(id, out var um))
                DrawRectOutline(handle, um, Half + new Vector2(2, 2), 1.5f, s, UnlockLine);

        if (_selectedId != null && _centers.TryGetValue(_selectedId, out var sel))
            DrawRectOutline(handle, sel, Half + new Vector2(3, 3), 2f, s, SelectColor);

        if (_searching)
            foreach (var id in _searchMatch)
                if (_centers.TryGetValue(id, out var m))
                    DrawRectOutline(handle, m, Half + new Vector2(2, 2), 2f, s, SearchColor);

        base.Draw(handle);
    }

    private void DrawDots(DrawingHandleScreen handle, float s)
    {
        var time = (float)_timing.RealTime.TotalSeconds;
        var dotR = MathF.Max(2f, NodeW * 0.4f * _zoom);
        foreach (var (id, color) in _nodeColors)
        {
            if (!_centers.TryGetValue(id, out var m))
                continue;
            var pos = (m * _zoom + _pan) * s;
            var dim = _searching && !_searchMatch.Contains(id) ? 0.3f : 1f;
            var twinkle = 0.7f + 0.3f * MathF.Sin(time * 2.2f + Hash01(id, 8) * MathF.Tau);
            var b = dim * twinkle;
            var core = Color.InterpolateBetween(color, Color.White, 0.65f);
            handle.DrawCircle(pos, dotR * 2.0f * s, color.WithAlpha(0.14f * b), true);
            handle.DrawCircle(pos, dotR * 0.62f * s, core.WithAlpha(b), true);
        }
        if (_searching)
            foreach (var id in _searchMatch)
                if (_centers.TryGetValue(id, out var m))
                    handle.DrawCircle((m * _zoom + _pan) * s, (dotR + 3f) * s, SearchColor, false);
        if (_selectedId != null && _centers.TryGetValue(_selectedId, out var sel))
            handle.DrawCircle((sel * _zoom + _pan) * s, (dotR + 3f) * s, SelectColor, false);
    }

    private void DrawSpaceBackground(DrawingHandleScreen handle, float s)
    {
        float w = PixelSize.X, h = PixelSize.Y;
        handle.DrawRect(new UIBox2(0, 0, w, h), BgColor);
        if (_spaceTex == null)
            return;
        float tw = _spaceTex.Width, th = _spaceTex.Height;
        var par = _pan * (0.15f * s);
        var ox = par.X % tw;
        if (ox > 0) ox -= tw;
        var oy = par.Y % th;
        if (oy > 0) oy -= th;

        for (var y = oy; y < h; y += th)
        for (var x = ox; x < w; x += tw)
            handle.DrawTextureRect(_spaceTex, UIBox2.FromDimensions(new Vector2(x, y), new Vector2(tw, th)));
    }

    private void DrawRectOutline(DrawingHandleScreen handle, Vector2 centerBase, Vector2 half, float width, float s, Color color)
    {
        var c = centerBase * _zoom + _pan;
        var tl = c - half;
        var br = c + half;
        var tr = new Vector2(br.X, tl.Y);
        var bl = new Vector2(tl.X, br.Y);
        DrawThickLine(handle, tl * s, tr * s, width * s, color);
        DrawThickLine(handle, tr * s, br * s, width * s, color);
        DrawThickLine(handle, br * s, bl * s, width * s, color);
        DrawThickLine(handle, bl * s, tl * s, width * s, color);
    }

    private static float EdgeDist(Vector2 u)
    {
        var k = MathF.Max(MathF.Abs(u.X) / (NodeW * 0.5f), MathF.Abs(u.Y) / (NodeH * 0.5f));
        return k < 1e-4f ? NodeW * 0.5f : 1f / k;
    }

    private void DrawThickLine(DrawingHandleScreen handle, Vector2 from, Vector2 to, float width, Color color)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 0.01f)
            return;
        var perp = new Vector2(-dir.Y, dir.X) / len * (width * 0.5f);
        _quad[0] = from + perp;
        _quad[1] = to + perp;
        _quad[2] = to - perp;
        _quad[3] = from - perp;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _quad, color);
    }
    private void DrawDashedLine(DrawingHandleScreen handle, Vector2 from, Vector2 to, float width, Color color, float dashLen, float gapLen)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 0.01f)
            return;
        var u = dir / len;
        var step = MathF.Max(1f, dashLen + gapLen);
        for (var pos = 0f; pos < len; pos += step)
            DrawThickLine(handle, from + u * pos, from + u * MathF.Min(pos + dashLen, len), width, color);
    }


    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        _panning = true;
        _pressPos = args.RelativePosition;
        _pressMoved = false;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        _panning = false;
        if (!_pressMoved && _zoom < DotThreshold)
            SelectNearestAt(args.RelativePosition);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_panning)
            return;
        _pan += args.Relative;
        if ((args.RelativePosition - _pressPos).Length() > 5f)
            _pressMoved = true;
        ApplyPan();
    }
    private void SelectNearestAt(Vector2 pos)
    {
        string? best = null;
        var bestD = float.MaxValue;
        foreach (var (id, center) in _centers)
        {
            var d = (center * _zoom + _pan - pos).LengthSquared();
            if (d < bestD)
            {
                bestD = d;
                best = id;
            }
        }
        var tol = NodeGap * _zoom * 0.75f;
        if (best != null && bestD <= tol * tol)
            OnTechSelected?.Invoke(new ProtoId<TechnologyPrototype>(best));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        var old = _zoom;
        var factor = args.Delta.Y > 0 ? 1.15f : args.Delta.Y < 0 ? 1f / 1.15f : 1f;
        _zoom = Math.Clamp(_zoom * factor, 0.15f, 2.5f);
        if (MathF.Abs(_zoom - old) < 1e-4f)
            return;
        var cur = args.RelativePosition;
        var w = (cur - _pan) / old;
        _pan = cur - w * _zoom;
        ApplyPan();
        args.Handle();
    }
}
