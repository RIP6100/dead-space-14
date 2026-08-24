// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Research;

[ByRefEvent]
public readonly record struct ResearchServerGetDataPerSecondEvent(
    EntityUid Server,
    Dictionary<ProtoId<ResearchFieldPrototype>, int> Data);
