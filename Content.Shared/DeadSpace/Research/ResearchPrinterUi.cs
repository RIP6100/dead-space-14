// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Research;

[Serializable, NetSerializable]
public enum ResearchPrinterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PrintResearchMessage : BoundUserInterfaceMessage
{
    public ProtoId<TechnologyPrototype> Tech;

    public PrintResearchMessage(ProtoId<TechnologyPrototype> tech)
    {
        Tech = tech;
    }
}

[Serializable, NetSerializable]
public sealed class ResearchPrinterState : BoundUserInterfaceState
{
    public List<ProtoId<TechnologyPrototype>> Available;

    public List<ProtoId<TechnologyPrototype>> Unlocked;

    public List<ProtoId<TechDisciplinePrototype>> SupportedDisciplines;

    public ResearchPrinterState(
        List<ProtoId<TechnologyPrototype>> available,
        List<ProtoId<TechnologyPrototype>> unlocked,
        List<ProtoId<TechDisciplinePrototype>> supportedDisciplines)
    {
        Available = available;
        Unlocked = unlocked;
        SupportedDisciplines = supportedDisciplines;
    }
}
