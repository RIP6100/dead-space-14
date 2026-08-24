// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.Research;
using Content.Shared.DeadSpace.Research.Prototypes;
using Content.Shared.Research.Prototypes;
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
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Research;
public sealed class MosaicTestWindow : FancyWindow
{
    public event Action? OnSubmit;
    public event Action<Vector2i, ProtoId<ResearchFieldPrototype>>? OnPlaceRequest;
    public event Action<Vector2i>? OnRemoveRequest;
    public event Action<ProtoId<ResearchFieldPrototype>, ProtoId<ResearchFieldPrototype>>? OnCombine;

    private static readonly Color Accent = Color.FromHex("#c68cfa");
    private static readonly Color StatusOk = Color.FromHex("#7cc47f");
    private static readonly Color StatusErr = Color.FromHex("#e0a34f");

    private readonly IPrototypeManager _proto;
    private readonly SharedResearchMosaicSystem _mosaic;

    private readonly Label _techLabel;
    private readonly Label _targetLabel;
    private readonly HexGridControl _hex;
    private readonly RichTextLabel _statusLabel;
    private readonly Button _submitButton;
    private readonly Button _clearButton;
    private readonly BoxContainer _brushStrip;
    private readonly List<Button> _brushButtons = new();
    private readonly Dictionary<ProtoId<ResearchFieldPrototype>, (Button Button, Label Count)> _brushInfo = new();
    private readonly CombinerHexControl _combiner;
    private readonly Button _combineButton;
    private Dictionary<ProtoId<ResearchFieldPrototype>, int> _lastData = new();

    private ProtoId<TechnologyPrototype>? _selectedTech;
    private ProtoId<ResearchFieldPrototype>? _selectedBrush;
    private bool _fixed;
    private bool _damaged;

    public MosaicTestWindow()
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _mosaic = IoCManager.Resolve<IEntityManager>().System<SharedResearchMosaicSystem>();

        Title = Loc.GetString("research-mosaic-window-title");
        MinSize = new Vector2(940, 540);
        SetSize = new Vector2(1080, 680);

        _hex = new HexGridControl { HorizontalExpand = true, VerticalExpand = true };
        _hex.OnPlaceRequest += (cell, field) => OnPlaceRequest?.Invoke(cell, field);
        _hex.OnRemoveRequest += cell => OnRemoveRequest?.Invoke(cell);
        _hex.OnChanged += () => { UpdateSubmitState(); UpdateBrushAvailability(); };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            MinWidth = 300,
        };

        var techBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        _techLabel = new Label { Text = Loc.GetString("research-mosaic-no-printout"), StyleClasses = { "LabelBig" } };
        _targetLabel = new Label { StyleClasses = { "LabelSubText" }, Visible = false };
        techBox.AddChild(_techLabel);
        techBox.AddChild(_targetLabel);
        left.AddChild(MakeSection(Loc.GetString("research-mosaic-tech-header"), techBox));

        _brushStrip = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        left.AddChild(MakeSection(Loc.GetString("research-mosaic-field-header"), _brushStrip));
        _combiner = new CombinerHexControl(_proto) { GetBrush = () => _selectedBrush };
        _combiner.OnChanged += RefreshCombine;

        _combineButton = new Button { Text = Loc.GetString("research-mosaic-draft-button"), HorizontalExpand = true, Disabled = true };
        _combineButton.OnPressed += _ =>
        {
            if (_combiner.A is { } a && _combiner.B is { } b)
                OnCombine?.Invoke(a, b);
        };

        var combineBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        combineBox.AddChild(_combiner);
        combineBox.AddChild(_combineButton);
        left.AddChild(MakeSection(Loc.GetString("research-mosaic-draft-header"), combineBox));

        _clearButton = new Button
        {
            Text = Loc.GetString("research-mosaic-clear"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Disabled = true,
        };
        _clearButton.OnPressed += _ =>
        {
            foreach (var cell in _hex.PlayerCells)
                OnRemoveRequest?.Invoke(cell);
            _hex.RemoveAllPlayer();
        };
        left.AddChild(_clearButton);

        _submitButton = new Button
        {
            Text = Loc.GetString("research-mosaic-submit"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Disabled = true,
        };
        _submitButton.OnPressed += _ => OnSubmit?.Invoke();
        left.AddChild(_submitButton);
        var leftScroll = new ScrollContainer { HScrollEnabled = false, MinWidth = 470, HorizontalExpand = false };
        leftScroll.AddChild(left);
        _statusLabel = new RichTextLabel { HorizontalExpand = true, MinSize = new Vector2(0, 22) };
        var boardBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        boardBox.AddChild(_hex);
        boardBox.AddChild(_statusLabel);
        var hexSection = MakeSection(Loc.GetString("research-mosaic-board-header"), boardBox, expand: true);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(leftScroll);
        root.AddChild(hexSection);

        ContentsContainer.AddChild(root);
    }
    private void RebuildPalette(Dictionary<ProtoId<ResearchFieldPrototype>, int> data)
    {
        _brushStrip.RemoveAllChildren();
        _brushButtons.Clear();
        _brushInfo.Clear();

        void AddGroup(string headerLoc, int complexity)
        {
            var fields = data.Keys
                .Select(k => _proto.Index(k))
                .Where(f => FieldComplexity(f) == complexity)
                .OrderBy(f => Loc.GetString(f.Name))
                .ToList();
            if (fields.Count == 0)
                return;

            _brushStrip.AddChild(new Label
            {
                Text = Loc.GetString(headerLoc),
                StyleClasses = { "LabelSubText" },
                FontColorOverride = Accent,
            });
            var cols = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var colL = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, HorizontalExpand = true };
            var colR = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, HorizontalExpand = true };
            cols.AddChild(colL);
            cols.AddChild(colR);
            for (var i = 0; i < fields.Count; i++)
                (i % 2 == 0 ? colL : colR).AddChild(MakeBrushButton(fields[i]));
            _brushStrip.AddChild(cols);
        }

        AddGroup("research-mosaic-group-fundamental", 0);
        AddGroup("research-mosaic-group-compound", 1);
        AddGroup("research-mosaic-group-complex", 2);
        if (_selectedBrush is { } sel && _brushInfo.TryGetValue(sel, out var info))
        {
            info.Button.Pressed = true;
            _hex.SetBrush(sel);
        }
        else
        {
            _selectedBrush = null;
        }
    }
    private Button MakeBrushButton(ResearchFieldPrototype field)
    {
        var id = new ProtoId<ResearchFieldPrototype>(field.ID);

        var swatch = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = field.Color },
            MinSize = new Vector2(14, 14),
            VerticalAlignment = Control.VAlignment.Center,
        };
        var name = new RichTextLabel { MaxWidth = 150, VerticalAlignment = Control.VAlignment.Center };
        name.SetMessage(FieldName(id));
        var count = new Label
        {
            Text = "0",
            StyleClasses = { "Monospace" },
            HorizontalExpand = true,
            HorizontalAlignment = Control.HAlignment.Right,
            VerticalAlignment = Control.VAlignment.Center,
        };

        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        content.AddChild(swatch);
        content.AddChild(name);
        content.AddChild(count);

        var btn = new Button { ToolTip = FieldDescription(id), ToggleMode = true, HorizontalExpand = true, MinWidth = 120 };
        btn.AddChild(content);
        btn.OnToggled += _ =>
        {
            foreach (var b in _brushButtons)
                b.Pressed = false;
            btn.Pressed = true;
            _hex.SetBrush(id);
            _selectedBrush = id;
        };
        _brushButtons.Add(btn);
        _brushInfo[id] = (btn, count);
        return btn;
    }
    private void UpdateBrushAvailability()
    {
        foreach (var (field, (btn, count)) in _brushInfo)
        {
            var remaining = _hex.RemainingData(field);
            count.Text = remaining.ToString();
            var empty = remaining <= 0;
            btn.Disabled = empty;
            btn.Modulate = empty ? Color.White.WithAlpha(0.4f) : Color.White;
            if (empty)
                btn.Pressed = false;
        }
    }
    private void RefreshCombine()
    {
        if (_combiner.A is not { } a || _combiner.B is not { } b || a == b)
        {
            _combineButton.Disabled = true;
            _combineButton.ToolTip = Loc.GetString("research-mosaic-draft-hint");
            return;
        }

        var enough = _lastData.GetValueOrDefault(a) >= 1 && _lastData.GetValueOrDefault(b) >= 1;
        _combineButton.Disabled = !enough;
        _combineButton.ToolTip = enough ? null : Loc.GetString("research-mosaic-draft-nodata");
    }
    private Control MakeSection(string header, Control content, bool expand = false)
    {
        var inner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10),
            VerticalExpand = expand,
        };
        inner.AddChild(new Label { Text = header, StyleClasses = { "LabelHeading" }, FontColorOverride = Accent });
        inner.AddChild(new PanelContainer { StyleClasses = { "LowDivider" } });
        inner.AddChild(content);

        var panel = new PanelContainer
        {
            StyleClasses = { "PanelDark" },
            HorizontalExpand = expand,
            VerticalExpand = expand,
        };
        panel.AddChild(inner);
        return panel;
    }
    public void Update(ResearchMosaicBuiState state)
    {
        _hex.SetData(state.Data);
        RebuildPalette(state.Data);
        _lastData = state.Data;
        RefreshCombine();

        _statusLabel.SetMessage(state.Status ?? "", state.StatusIsError ? StatusErr : StatusOk);

        if (state.CurrentTech is { } techId)
        {
            _selectedTech = techId;
            _fixed = state.Fixed;

            var tech = _proto.Index(techId);
            _techLabel.Text = Loc.GetString(tech.Name);

            if (tech.MosaicBoard is { } board)
            {
                _damaged = false;
                _hex.SetBoard(board);
                if (state.Placement is { } saved)
                    _hex.LoadPlacement(saved);
                _hex.ReadOnly = state.Fixed;

                var ends = string.Join(" <-> ", board.Endpoints.Select(e => Loc.GetString(_proto.Index(e.Field).Name)));
                _targetLabel.Text = Loc.GetString("research-mosaic-target", ("ends", ends));
                _targetLabel.Visible = true;
            }
            else
            {
                _damaged = true;
                _hex.SetDamaged();
                _hex.ReadOnly = true;
                _targetLabel.Visible = false;
            }
        }
        else
        {
            _selectedTech = null;
            _fixed = false;
            _damaged = false;
            _hex.Clear();
            _hex.ReadOnly = false;
            _techLabel.Text = Loc.GetString("research-mosaic-no-printout");
            _targetLabel.Visible = false;
        }
        _clearButton.Disabled = _selectedTech is null || _fixed || !_hex.PlayerCells.Any();

        UpdateBrushAvailability();
        UpdateSubmitState();
    }
    private void UpdateSubmitState()
    {
        if (_selectedTech is null || _fixed)
        {
            _submitButton.Disabled = true;
            _submitButton.ToolTip = null;
            return;
        }

        if (_damaged)
        {
            _submitButton.Disabled = true;
            _submitButton.ToolTip = Loc.GetString("research-mosaic-damaged");
            return;
        }

        if (!_hex.IsSolved())
        {
            _submitButton.Disabled = true;
            _submitButton.ToolTip = Loc.GetString("research-mosaic-hint-connect");
        }
        else
        {
            _submitButton.Disabled = false;
            _submitButton.ToolTip = null;
        }
    }
    private int FieldComplexity(ResearchFieldPrototype field)
    {
        if (field.IsBase)
            return 0;
        return field.Components.All(c => _proto.Index(c).IsBase) ? 1 : 2;
    }

    private string FieldName(ProtoId<ResearchFieldPrototype> field)
    {
        return Loc.GetString(_proto.Index(field).Name);
    }

    private string? FieldDescription(ProtoId<ResearchFieldPrototype> field)
    {
        var desc = _proto.Index(field).Description;
        return string.IsNullOrEmpty(desc.Id) ? null : Loc.GetString(desc);
    }
    private sealed class CombinerHexControl : Control
    {
        private const float HexRadius = 30f;
        private static readonly Color OutlineColor = Color.FromHex("#6a5f7a");
        private static readonly Color FillColor = Color.FromHex("#0b0810");
        private static readonly Color PlusColor = Color.FromHex("#c68cfa");

        private readonly IPrototypeManager _proto;
        private readonly Font _font;
        private readonly Vector2[] _corners = new Vector2[6];

        public ProtoId<ResearchFieldPrototype>? A;
        public ProtoId<ResearchFieldPrototype>? B;
        public Func<ProtoId<ResearchFieldPrototype>?>? GetBrush;
        public event Action? OnChanged;

        public CombinerHexControl(IPrototypeManager proto)
        {
            _proto = proto;
            _font = new VectorFont(
                IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 18);
            MinSize = new Vector2(0, 84);
            MouseFilter = MouseFilterMode.Stop;
        }

        private float R => HexRadius * UIScale;
        private Vector2 LeftCenter => new(PixelSize.X / 2f - R * 1.35f, PixelSize.Y / 2f);
        private Vector2 RightCenter => new(PixelSize.X / 2f + R * 1.35f, PixelSize.Y / 2f);

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            DrawSlot(handle, LeftCenter, A);
            DrawSlot(handle, RightCenter, B);
            handle.DrawString(_font, new Vector2(PixelSize.X / 2f - 5 * UIScale, PixelSize.Y / 2f - 11 * UIScale), "+", PlusColor);
        }

        private void DrawSlot(DrawingHandleScreen handle, Vector2 center, ProtoId<ResearchFieldPrototype>? field)
        {
            for (var i = 0; i < 6; i++)
            {
                var angle = MathF.PI / 180f * (60f * i - 30f);
                _corners[i] = center + new Vector2(R * MathF.Cos(angle), R * MathF.Sin(angle));
            }
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _corners, FillColor);
            for (var i = 0; i < 6; i++)
                handle.DrawLine(_corners[i], _corners[(i + 1) % 6], OutlineColor);

            if (field is { } f)
                handle.DrawCircle(center, R * 0.55f, _proto.Index(f).Color);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick)
                return;
            if (GetBrush?.Invoke() is not { } brush)
                return;

            var p = args.RelativePixelPosition;
            if ((p - LeftCenter).Length() <= R)
            {
                A = brush;
                OnChanged?.Invoke();
                args.Handle();
            }
            else if ((p - RightCenter).Length() <= R)
            {
                B = brush;
                OnChanged?.Invoke();
                args.Handle();
            }
        }
    }
}
