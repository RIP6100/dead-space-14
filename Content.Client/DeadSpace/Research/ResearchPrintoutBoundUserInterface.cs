// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Content.Client.Paper.UI;
using Content.Shared.DeadSpace.Research;
using Content.Shared.DeadSpace.Research.Components;
using Content.Shared.Paper;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.DeadSpace.Research;

public sealed class ResearchPrintoutBoundUserInterface : BoundUserInterface
{
    private PaperWindow? _window;

    public ResearchPrintoutBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaperWindow>();
        _window.OnSaved += OnTextEntered;

        if (EntMan.TryGetComponent<PaperComponent>(Owner, out var paper))
            _window.MaxInputLength = paper.ContentSize;
        if (EntMan.TryGetComponent<PaperVisualsComponent>(Owner, out var visuals))
            _window.InitVisuals(Owner, visuals);

        InjectHex(_window);
    }
    private void InjectHex(PaperWindow window)
    {
        var inputContainer = window.Input.Parent?.Parent;
        var contentBox = inputContainer?.Parent;
        if (inputContainer == null || contentBox == null)
            return;

        if (!EntMan.TryGetComponent<ResearchPrintoutComponent>(Owner, out var comp) || string.IsNullOrEmpty(comp.Tech.Id))
            return;

        var proto = IoCManager.Resolve<IPrototypeManager>();
        if (!proto.TryIndex(comp.Tech, out var tech))
            return;

        var hex = new HexGridControl
        {
            ReadOnly = true,
            PaperMode = true,
            HorizontalAlignment = Control.HAlignment.Center,
        };
        if (tech.MosaicBoard is { } board)
        {
            hex.SetBoard(board);
            hex.LoadPlacement(comp.Placement);
        }
        else
        {
            hex.SetDamaged();
        }

        contentBox.AddChild(hex);
        var idx = inputContainer.GetPositionInParent();
        for (var i = 0; i < contentBox.ChildCount; i++)
        {
            if (contentBox.GetChild(i) is RichTextLabel)
            {
                idx = i;
                break;
            }
        }
        hex.SetPositionInParent(idx);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window?.Populate((PaperBoundUserInterfaceState) state);
    }

    private void OnTextEntered(string text)
    {
        SendMessage(new PaperInputTextMessage(text));

        if (_window != null)
        {
            _window.Input.TextRope = Rope.Leaf.Empty;
            _window.Input.CursorPosition = new TextEdit.CursorPos(0, TextEdit.LineBreakBias.Top);
        }
    }
}
