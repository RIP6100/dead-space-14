// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Abilities.TimeStop.Components;

/// <summary>
/// Стационарное поле остановленного времени. Живёт до EndTime и каждый тик замораживает
/// (ставит на паузу) всех живых существ, снаряды и брошенные предметы в радиусе Range.
/// Спавнится в точке каста. Кастер (Caster) не замораживается.
/// </summary>
[RegisterComponent]
public sealed partial class TimeStopFieldComponent : Component
{
    /// <summary>
    /// Радиус поля в тайлах.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = 10f;

    /// <summary>
    /// Момент серверного времени, когда поле исчезнет и всё разморозится.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan EndTime;

    /// <summary>
    /// Заглушать ли пойманных существ.
    /// </summary>
    [DataField]
    public bool Muted = true;

    /// <summary>
    /// Тот, кто применил способность. Не замораживается.
    /// </summary>
    [DataField]
    public EntityUid? Caster;

    /// <summary>
    /// Не замораживать союзников кастера (по фракции).
    /// </summary>
    [DataField]
    public bool IgnoreFriendly = false;
}