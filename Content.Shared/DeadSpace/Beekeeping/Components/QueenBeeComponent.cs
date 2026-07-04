// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.Beekeeping;

/// <summary>
/// Маркер предмета-матки. Вставленная в слот улья матка активирует его
/// (по типу батарейки): пока матка на месте - улей спавнит пчёл и делает мёд.
/// </summary>
[RegisterComponent]
public sealed partial class QueenBeeComponent : Component
{
}