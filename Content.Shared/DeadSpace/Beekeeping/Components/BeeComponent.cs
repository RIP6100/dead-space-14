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
 
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float IdleCooldown = 5f;
 
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SearchRadius = 15f;
 
    /// <summary>
    /// Расстояние, на котором пчела считается "прибывшей" к цели.
    /// Увеличено с 0.1f до 0.5f для надёжной работы с физическим движком.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ArrivalThreshold = 0.5f;
 
    /// <summary>
    /// Скорость движения пчелы (единиц в секунду).
    /// Передаётся в SetLinearVelocity вместо хардкоженного значения.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoveSpeed = 3f;
 
    /// <summary>
    /// Максимальное время (в секундах) для достижения цели.
    /// Если пчела не добралась за это время — считаем, что застряла.
    /// Должно быть > SearchRadius / MoveSpeed с запасом.
    /// По умолчанию: 15 / 3 * 2 = 10 секунд (2x запас).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxMovingTime = 10f;
 
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StateTimer = 0f;
 
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DepositingDuration = 1.5f;
 
    /// <summary>
    /// Пчела занята DoAfter процессом (опыление или сдача пыльцы).
    /// Блокирует движение и обновление состояния.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsBusy = false;
}