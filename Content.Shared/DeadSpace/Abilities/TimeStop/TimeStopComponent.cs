// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.DeadSpace.Abilities.TimeStop.Components;

/// <summary>
/// Даёт носителю действие "Остановка времени". При активации замораживает
/// все сущности и снаряды в радиусе <see cref="Range"/> на <see cref="Duration"/> секунд.
/// Вешается на моба-мага. Для предмета используйте TimeStopOnUseComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TimeStopComponent : Component
{
    /// <summary>
    /// Прототип действия, которое выдаётся носителю.
    /// </summary>
    [DataField]
    public EntProtoId ActionTimeStop = "ActionTimeStop";

    [DataField]
    public EntityUid? ActionTimeStopEntity;

    /// <summary>
    /// Радиус остановки времени в тайлах.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = 10f;

    /// <summary>
    /// Сколько секунд длится остановка времени.
    /// </summary>
    [DataField, AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Duration = 8f;

    /// <summary>
    /// Немота застывших: не могут говорить и эмоутить.
    /// </summary>
    [DataField]
    public bool MuteFrozen = true;

    /// <summary>
    /// Не замораживать союзников кастера (по фракции).
    /// </summary>
    [DataField]
    public bool IgnoreFriendly = false;

    /// <summary>
    /// Прототип поля остановленного времени (спавнится в точке каста).
    /// </summary>
    [DataField]
    public string EffectPrototype = "TimeStopField";

    /// <summary>
    /// Звук активации.
    /// </summary>
    [DataField]
    public SoundSpecifier? TimeStopSound = default;
}