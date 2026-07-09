// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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

    /// <summary>
    /// На сколько повышается потенция растения (Potency) на время действия опыления.
    /// 0.2 = +20% к текущей потенции в момент опыления. Плоды, снятые с опылённого
    /// растения, получают увеличенную потенцию. По окончании буста прибавка снимается.
    /// 0 = не влиять на потенцию.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PotencyBonus = 0.2f;

    /// <summary>
    /// Служебное: величина потенции, реально добавленная текущим бустом.
    /// Храним именно ДЕЛЬТУ (а не старое абсолютное значение), чтобы при откате
    /// вычесть ровно свою прибавку и не затереть естественные изменения потенции
    /// за время буста (мутации, удобрения и т.п.).
    /// 0 = прибавка сейчас не применена. Управляется PollinationSystem, вручную не трогать.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AppliedPotencyDelta = 0f;
}