// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Abilities.TimeStop.Components;

/// <summary>
/// Висит на сущности, застывшей во времени. Пока висит - сущность спаузена
/// (физика заморожена с сохранением скорости), не может двигаться, взаимодействовать
/// и, если Muted, говорить. Снимается сервером по достижении EndTime.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TimeStoppedComponent : Component
{
    /// <summary>
    /// Момент серверного времени, когда заморозка спадёт.
    /// Поле НЕ приостанавливается паузой - сравнивается с реальным CurTime.
    /// </summary>
    [DataField]
    public TimeSpan EndTime;

    /// <summary>
    /// Немота: нельзя говорить/эмоутить.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Muted = true;
}