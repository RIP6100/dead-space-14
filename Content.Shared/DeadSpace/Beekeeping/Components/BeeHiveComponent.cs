// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.Beekeeping;

[RegisterComponent]
public sealed partial class BeeHiveComponent : Component
{
    // Сколько пыльцы накоплено (только от пчёл)
    [DataField]
    public float PollenStored = 0f;

    // Максимум пыльцы в улье
    [DataField]
    public float MaxPollen = 100f;

    // Сколько пыльцы стоит 1 единица мёда
    [DataField]
    public float PollenPerHoney = 10f;

    // Соотношение воска к мёду
    [DataField]
    public float WaxPerHoney = 0.5f;

    // Максимум рамок
    [DataField]
    public int MaxFrames = 6;

    // Интервал производства мёда
    [DataField]
    public float ProductionInterval = 10f;

    [DataField]
    public float ProductionTimer = 0f;

    // Активен ли улей
    [DataField]
    public bool Active = true;

    // === ПЧЁЛЫ ===
    [DataField]
    public int BeeCount = 0;

    [DataField]
    public int MaxBees = 5;

    [DataField]
    public float SpawnInterval = 30f;

    [DataField]
    public float SpawnTimer = 0f;
}