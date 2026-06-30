using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.Beekeeping;

/// <summary>
/// Событие, вызываемое когда пчела опыляет растение.
/// Отправляется на сущность с PlantHolderComponent + PollinationComponent.
/// </summary>
[ByRefEvent]
public readonly record struct PlantPollinatedEvent
{
    /// <summary>
    /// UID пчелы, которая опылила растение.
    /// </summary>
    public readonly EntityUid BeeUid;

    /// <summary>
    /// Количество собранной пыльцы.
    /// </summary>
    public readonly float PollenCollected;

    public PlantPollinatedEvent(EntityUid beeUid, float pollenCollected)
    {
        BeeUid = beeUid;
        PollenCollected = pollenCollected;
    }
}

/// <summary>
/// Событие-запрос: может ли пчела опылить данное растение?
/// Возвращает CanPollinate = false если растение в кулдауне или не цветет.
/// </summary>
[ByRefEvent]
public struct CanPollinateEvent
{
    public bool CanPollinate;
    public string? Reason;

    public CanPollinateEvent()
    {
        CanPollinate = true;
        Reason = null;
    }
}
