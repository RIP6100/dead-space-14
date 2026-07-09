// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.DeadSpace.Beekeeping;

public enum BeeState
{
    Idle,
    SearchingFlower,
    MovingToFlower,
    Pollinating,
    ReturningToHive,
    DepositingPollen
}

[RegisterComponent]
public sealed partial class BeeComponent : Component
{
    /// <summary>
    /// Улей, которому принадлежит пчела. Задаётся при спавне из BeeHiveSystem.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? HiveOwner;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public BeeState State = BeeState.Idle;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? TargetFlower;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PollenCarried = 0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxPollenCarry = 20f;

    /// <summary>
    /// Сколько секунд пчела ждёт в Idle перед новым вылетом.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float IdleCooldown = 5f;

    /// <summary>
    /// Радиус поиска цветков вокруг пчелы.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SearchRadius = 15f;

    /// <summary>
    /// Расстояние, на котором пчела считается "прибывшей" к цели.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ArrivalThreshold = 0.5f;

    /// <summary>
    /// Максимальное время (в секундах) на достижение цели.
    /// Если пчела не добралась за это время — считаем, что застряла, и ищем другую цель.
    /// Скорость самого движения задаётся через MovementSpeedModifierComponent
    /// (baseSprintSpeed) в прототипе, а не здесь — им управляет NPCSteeringSystem.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxMovingTime = 10f;

    /// <summary>
    /// Длительность опыления одного цветка (сек).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PollinatingDuration = 2f;

    /// <summary>
    /// Длительность сдачи пыльцы в улей (сек).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DepositingDuration = 1.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StateTimer = 0f;

    /// <summary>
    /// Расстояние до цели на прошлой проверке прогресса. Служебное — для антизалипания.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LastDistanceToTarget = float.MaxValue;

    /// <summary>
    /// Сколько секунд пчела не приближается к цели. Если превышает StuckTimeout —
    /// считаем, что застряла (steering перестал двигать), и принудительно
    /// перестраиваем путь. Служебное поле, управляется BeeAISystem.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float NoProgressTime = 0f;

    /// <summary>
    /// Через сколько секунд "без приближения к цели" считать пчелу застрявшей
    /// и принудительно перестроить путь в NPCSteeringSystem.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StuckTimeout = 3.5f;

    /// <summary>
    /// Минимальное уменьшение расстояния (юниты) за тик, считающееся прогрессом.
    /// Меньше этого — считаем, что пчела топчется на месте.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ProgressEpsilon = 0.05f;

    /// <summary>
    /// Пчела занята DoAfter процессом (опыление или сдача пыльцы).
    /// Блокирует движение и обновление состояния.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsBusy = false;
}