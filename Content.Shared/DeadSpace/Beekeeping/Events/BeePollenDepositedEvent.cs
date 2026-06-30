// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.Beekeeping;

[ByRefEvent]
public readonly record struct BeePollenDepositedEvent(EntityUid Bee, float Amount);