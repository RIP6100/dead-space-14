// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.Research;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Research;

public sealed class ResearchPrinterWindow : FancyWindow
{
    public event Action<ProtoId<TechnologyPrototype>>? OnPrint;

    private static readonly Color Accent = Color.FromHex("#c68cfa");
    private static readonly Color UnlockedColor = Color.FromHex("#2f7d43");
    private static readonly Color AvailableColor = Color.FromHex("#3a72c4");
    private static readonly Color LockedColor = Color.FromHex("#3a414d");

    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprite;
    private readonly SharedLatheSystem _lathe;

    private readonly TechTreeControl _tree;

    private HashSet<string> _available = new();
    private HashSet<string> _unlocked = new();
    private HashSet<string> _supported = new();
    private ProtoId<TechnologyPrototype>? _selected;

    private readonly BoxContainer _discLegend;
    private readonly Label _countLabel;
    private readonly TextureRect _detailIcon;
    private readonly RichTextLabel _detailName;
    private readonly PanelContainer _stateChip;
    private readonly Label _stateLabel;
    private readonly RichTextLabel _detailDesc;
    private readonly RichTextLabel _detailInfo;
    private readonly Button _printButton;

    public ResearchPrinterWindow()
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
        var entity = IoCManager.Resolve<IEntityManager>();
        _sprite = entity.System<SpriteSystem>();
        _lathe = entity.System<SharedLatheSystem>();

        Title = Loc.GetString("research-printer-window-title");
        MinSize = new Vector2(960, 640);
        SetSize = new Vector2(1140, 730);
        var topBar = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        topBar.AddChild(new Label
        {
            Text = Loc.GetString("research-printer-subtitle"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = Accent,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });
        var legend = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12, VerticalAlignment = VAlignment.Center };
        legend.AddChild(LegendItem(UnlockedColor, Loc.GetString("research-printer-legend-unlocked")));
        legend.AddChild(LegendItem(AvailableColor, Loc.GetString("research-printer-legend-available")));
        legend.AddChild(LegendItem(LockedColor, Loc.GetString("research-printer-legend-locked")));
        topBar.AddChild(legend);
        _tree = new TechTreeControl { HorizontalExpand = true, VerticalExpand = true };
        _tree.OnTechSelected += OnTechSelected;

        var treeBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, VerticalExpand = true };
        var search = new LineEdit { PlaceHolder = Loc.GetString("research-printer-search"), HorizontalExpand = true };
        search.OnTextChanged += args => _tree.SetSearch(args.Text);
        treeBox.AddChild(search);
        treeBox.AddChild(_tree);
        _discLegend = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 14, HorizontalAlignment = HAlignment.Center };
        var discLegendScroll = new ScrollContainer { VScrollEnabled = false, HorizontalExpand = true };
        discLegendScroll.AddChild(_discLegend);
        treeBox.AddChild(discLegendScroll);
        _countLabel = new Label { StyleClasses = { "LabelSubText" }, HorizontalExpand = true, HorizontalAlignment = HAlignment.Right };
        treeBox.AddChild(_countLabel);
        var leftSection = MakeSection(Loc.GetString("research-printer-tree-header"), treeBox, expand: true);
        var detail = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10, VerticalExpand = true };

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        _detailIcon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MinSize = new Vector2(44, 44),
            VerticalAlignment = VAlignment.Center,
        };
        var iconFrame = new PanelContainer { StyleClasses = { "PanelDark" }, VerticalAlignment = VAlignment.Center };
        iconFrame.AddChild(_detailIcon);
        header.AddChild(iconFrame);
        _detailName = new RichTextLabel { VerticalAlignment = VAlignment.Center, HorizontalExpand = true };
        header.AddChild(_detailName);
        detail.AddChild(header);

        _stateLabel = new Label { StyleClasses = { "LabelSubText" } };
        _stateChip = new PanelContainer { HorizontalAlignment = HAlignment.Left };
        var chipInner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(8, 2) };
        chipInner.AddChild(_stateLabel);
        _stateChip.AddChild(chipInner);
        detail.AddChild(_stateChip);

        _detailDesc = new RichTextLabel { HorizontalExpand = true };
        detail.AddChild(_detailDesc);

        detail.AddChild(new PanelContainer { StyleClasses = { "LowDivider" } });

        var infoScroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        _detailInfo = new RichTextLabel { HorizontalExpand = true };
        infoScroll.AddChild(_detailInfo);
        detail.AddChild(infoScroll);

        _printButton = new Button { Text = Loc.GetString("research-printer-print"), Disabled = true, HorizontalExpand = true };
        _printButton.OnPressed += _ =>
        {
            if (_selected is { } t)
                OnPrint?.Invoke(t);
        };
        detail.AddChild(_printButton);

        var rightSection = MakeSection(Loc.GetString("research-printer-detail-header"), detail);
        rightSection.MinWidth = 350;
        var main = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        main.AddChild(leftSection);
        main.AddChild(rightSection);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(topBar);
        root.AddChild(main);

        ContentsContainer.AddChild(root);
        ShowDetail(null);
    }

    public void Update(ResearchPrinterState state)
    {
        _available = new HashSet<string>(state.Available.Select(x => x.Id));
        _unlocked = new HashSet<string>(state.Unlocked.Select(x => x.Id));
        _supported = new HashSet<string>(state.SupportedDisciplines.Select(x => x.Id));
        var techs = _proto.EnumeratePrototypes<TechnologyPrototype>()
            .Where(t => !t.Hidden
                        && (_supported.Count == 0
                            || _supported.Contains(t.Discipline.Id)
                            || _unlocked.Contains(t.ID)))
            .ToList();
        _tree.SetTechs(techs, _available, _unlocked, _supported);
        _countLabel.Text = Loc.GetString("research-printer-count", ("n", _tree.NodeCount));
        _discLegend.RemoveAllChildren();
        foreach (var did in techs.Select(t => t.Discipline.Id).Distinct().OrderBy(x => x))
        {
            if (_proto.TryIndex<TechDisciplinePrototype>(did, out var d))
                _discLegend.AddChild(LegendItem(d.Color, Loc.GetString(d.Name)));
        }

        ShowDetail(_selected);
    }

    private void OnTechSelected(ProtoId<TechnologyPrototype> tech)
    {
        _selected = tech;
        _tree.SetSelected(tech.Id);
        ShowDetail(tech);
    }

    private void ShowDetail(ProtoId<TechnologyPrototype>? sel)
    {
        if (sel is not { } id || string.IsNullOrEmpty(id.Id) || !_proto.TryIndex(id, out var tech))
        {
            _detailIcon.Texture = null;
            _detailName.SetMessage(Loc.GetString("research-printer-detail-none"));
            _stateChip.Visible = false;
            _detailDesc.Visible = false;
            _detailInfo.SetMessage(new FormattedMessage());
            _printButton.Disabled = true;
            return;
        }

        _detailIcon.Texture = _sprite.Frame0(tech.Icon);

        var nameMsg = new FormattedMessage();
        nameMsg.AddMarkupOrThrow($"[bold]{Loc.GetString(tech.Name)}[/bold]");
        _detailName.SetMessage(nameMsg);

        var isUnlocked = _unlocked.Contains(tech.ID);
        var isAvailable = _available.Contains(tech.ID);
        var stateKey = isUnlocked ? "research-printer-state-unlocked"
            : isAvailable ? "research-printer-state-available"
            : "research-printer-state-locked";
        var stateColor = isUnlocked ? UnlockedColor : isAvailable ? AvailableColor : LockedColor;
        _stateChip.Visible = true;
        _stateChip.PanelOverride = new StyleBoxFlat { BackgroundColor = stateColor.WithAlpha(0.28f) };
        _stateLabel.Text = Loc.GetString(stateKey);
        _stateLabel.FontColorOverride = Lighten(stateColor);
        if (tech.Description is { } descLoc)
        {
            var dm = new FormattedMessage();
            dm.AddMarkupOrThrow($"[color=#b8bec9]{Loc.GetString(descLoc)}[/color]");
            _detailDesc.SetMessage(dm);
            _detailDesc.Visible = true;
        }
        else
        {
            _detailDesc.Visible = false;
        }

        _detailInfo.SetMessage(BuildInfo(tech));
        _printButton.Disabled = !isAvailable;
    }

    private FormattedMessage BuildInfo(TechnologyPrototype tech)
    {
        var desc = new FormattedMessage();
        if (tech.TechnologyPrerequisites.Count > 0)
        {
            desc.AddMarkupOrThrow(Loc.GetString("research-console-prereqs-list-start"));
            foreach (var pre in tech.TechnologyPrerequisites)
            {
                desc.PushNewline();
                var pname = _proto.TryIndex(pre, out var pt) ? Loc.GetString(pt.Name) : pre.Id;
                desc.AddMarkupOrThrow(Loc.GetString("research-console-prereqs-list-entry", ("text", pname)));
            }
            desc.PushNewline();
        }

        desc.AddMarkupOrThrow(Loc.GetString("research-console-unlocks-list-start"));
        foreach (var recipe in tech.RecipeUnlocks)
        {
            if (!_proto.TryIndex(recipe, out var rp))
                continue;
            desc.PushNewline();
            desc.AddMarkupOrThrow(Loc.GetString("research-console-unlocks-list-entry", ("name", _lathe.GetRecipeName(rp))));
        }
        foreach (var generic in tech.GenericUnlocks)
        {
            desc.PushNewline();
            desc.AddMarkupOrThrow(Loc.GetString("research-console-unlocks-list-entry-generic", ("text", Loc.GetString(generic.UnlockDescription))));
        }
        return desc;
    }

    private static Color Lighten(Color c) => Color.InterpolateBetween(c, Color.White, 0.55f);

    private static Control LegendItem(Color color, string text)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6, VerticalAlignment = VAlignment.Center };
        box.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
            MinSize = new Vector2(13, 13),
            VerticalAlignment = VAlignment.Center,
        });
        box.AddChild(new Label { Text = text, StyleClasses = { "LabelSubText" }, VerticalAlignment = VAlignment.Center });
        return box;
    }

    private static Control MakeSection(string header, Control content, bool expand = false)
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
}
