using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.DeadSpace.Beekeeping;

/// <summary>
/// Компонент для растений и цветков, которые могут быть опылены пчелами.
/// Добавляет бонус к скорости роста для PlantHolderComponent.
/// </summary>
[RegisterComponent]
public sealed partial class PollinationComponent : Component
{
    /// <summary>
    /// Активно ли цветение (можно ли собирать пыльцу).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsFlowering = true;

    /// <summary>
    /// Сколько пыльцы дает одно опыление.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PollenYield = 10f;

    /// <summary>
    /// На сколько процентов ускоряется рост при опылении (0.3 = +30%).
    /// Работает только для сущностей с PlantHolderComponent.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GrowthSpeedBonus = 0.3f;

    /// <summary>
    /// Длительность бонуса к скорости роста в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BoostDuration = 300f; // 5 минут по умолчанию

    /// <summary>
    /// Было ли растение опылено в текущем цикле.
    /// Предотвращает спам опыления одного растения.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool WasPollinated = false;

    /// <summary>
    /// Время, когда растение снова станет доступно для опыления.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextPollinationAvailable = TimeSpan.Zero;

    /// <summary>
    /// Текущий активный множитель скорости роста (1.0 = базовая скорость).
    /// Устанавливается PollinationSystem при опылении.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CurrentGrowthMultiplier = 1f;

    /// <summary>
    /// Время окончания действия текущего бонуса.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan BoostEndTime = TimeSpan.Zero;

    /// <summary>
    /// Минимальный интервал между опылениями одного растения (кулдаун).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PollinationCooldown = 60f; // 1 минута

    /// <summary>
    /// Сколько возраста добавляется мгновенно при опылении (дополнительный бонус).
    /// 0 = только ускорение CycleDelay.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int InstantAgeBonus = 0;

    /// <summary>
    /// Звук, проигрываемый при опылении.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string PollinationSound = "/Audio/Effects/Fluids/splat.ogg";
}
