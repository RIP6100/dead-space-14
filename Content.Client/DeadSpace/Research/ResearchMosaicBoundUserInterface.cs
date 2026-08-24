// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Content.Shared.DeadSpace.Research;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Research;

public sealed class ResearchMosaicBoundUserInterface : BoundUserInterface
{
    private MosaicTestWindow? _window;

    public ResearchMosaicBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MosaicTestWindow>();
        _window.OnSubmit += () => SendMessage(new MosaicSubmitMessage());
        _window.OnPlaceRequest += (cell, field) => SendMessage(new MosaicPlaceMessage(cell, field));
        _window.OnRemoveRequest += cell => SendMessage(new MosaicRemoveMessage(cell));
        _window.OnCombine += (a, b) => SendMessage(new MosaicCombineMessage(a, b));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ResearchMosaicBuiState mosaicState)
            _window?.Update(mosaicState);
    }
}
