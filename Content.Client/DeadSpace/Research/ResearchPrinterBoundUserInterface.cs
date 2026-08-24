// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Content.Shared.DeadSpace.Research;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Research;

public sealed class ResearchPrinterBoundUserInterface : BoundUserInterface
{
    private ResearchPrinterWindow? _window;

    public ResearchPrinterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchPrinterWindow>();
        _window.OnPrint += tech => SendMessage(new PrintResearchMessage(tech));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchPrinterState s)
            _window?.Update(s);
    }
}
