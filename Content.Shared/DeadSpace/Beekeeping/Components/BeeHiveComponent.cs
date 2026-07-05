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

    // Интервал производства мёда
    [DataField]
    public float ProductionInterval = 10f;

    [DataField]
    public float ProductionTimer = 0f;

    /// <summary>
    /// ID слота матки. Сам слот объявлен через компонент ItemSlots в прототипе улья.
    /// Улей активен (спавнит пчёл и производит мёд) только когда в этом слоте
    /// находится матка - она работает как "батарейка".
    /// </summary>
    [DataField]
    public string QueenSlotId = "queen_slot";

    // Текущее число живых пчёл НЕ хранится здесь - оно вычисляется в BeeHiveSystem
    // подсчётом сущностей с BeeComponent, у которых HiveOwner == этот улей.
    // Так счётчик не может разойтись с реальностью (гибель, удаление и т.п.).

    [DataField]
    public int MaxBees = 5;

    [DataField]
    public float SpawnInterval = 30f;

    [DataField]
    public float SpawnTimer = 0f;
}