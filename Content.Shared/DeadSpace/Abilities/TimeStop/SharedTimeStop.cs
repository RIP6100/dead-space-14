// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.DeadSpace.Abilities.TimeStop;

/// <summary>
/// Событие мгновенного действия "Остановка времени".
/// </summary>
public sealed partial class TimeStopActionEvent : InstantActionEvent
{
}

/// <summary>
/// Вешается на предмет (свиток, песочные часы, посох). При использовании в руке
/// (Use in hand) запускает остановку времени вокруг пользователя.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TimeStopOnUseComponent : Component
{
    /// <summary>
    /// Радиус остановки времени в тайлах.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = 10f;

    /// <summary>
    /// Длительность заморозки в секундах.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Duration = 8f;

    [DataField]
    public bool MuteFrozen = true;

    [DataField]
    public string EffectPrototype = "TimeStopField";

    [DataField]
    public SoundSpecifier? TimeStopSound = default;

    /// <summary>
    /// Кулдаун между применениями (сек). Использует UseDelay.
    /// </summary>
    [DataField]
    public float UseDelay = 30f;
}