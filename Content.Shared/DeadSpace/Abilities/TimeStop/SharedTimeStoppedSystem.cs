// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Abilities.TimeStop.Components;
using Content.Shared.Emoting;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Speech;
using Content.Shared.Throwing;

namespace Content.Shared.DeadSpace.Abilities.TimeStop;

/// <summary>
/// Общая часть механики застывшей во времени сущности: блокирует движение, речь,
/// эмоуты и любые действия/взаимодействия, пока висит <see cref="TimeStoppedComponent"/>.
/// Заморозка физики выполняется сервером через паузу сущности (см. TimeStopSystem),
/// поэтому здесь только запреты действий (важно и для предсказания на клиенте).
/// </summary>
public sealed class SharedTimeStoppedSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimeStoppedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TimeStoppedComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<TimeStoppedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<TimeStoppedComponent, ChangeDirectionAttemptEvent>(OnAttempt);

        SubscribeLocalEvent<TimeStoppedComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, PullAttemptEvent>(OnPullAttempt);

        SubscribeLocalEvent<TimeStoppedComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        SubscribeLocalEvent<TimeStoppedComponent, InGameOocMessageAttemptEvent>(OnInGameOocMessageAttempt);
    }

    private void OnStartup(Entity<TimeStoppedComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<PullableComponent>(ent, out var pullable))
            _pulling.TryStopPull(ent, pullable);

        _blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<TimeStoppedComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnUpdateCanMove(Entity<TimeStoppedComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnAttempt(EntityUid uid, TimeStoppedComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnInteractAttempt(Entity<TimeStoppedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnPullAttempt(EntityUid uid, TimeStoppedComponent component, PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnSpeakAttempt(EntityUid uid, TimeStoppedComponent component, SpeakAttemptEvent args)
    {
        if (!component.Muted)
            return;

        args.Cancel();
    }

    private void OnEmoteAttempt(EntityUid uid, TimeStoppedComponent component, EmoteAttemptEvent args)
    {
        if (component.Muted)
            args.Cancel();
    }

    private void OnInGameOocMessageAttempt(Entity<TimeStoppedComponent> ent, ref InGameOocMessageAttemptEvent args)
    {
        if (!ent.Comp.Muted)
            return;

        args.Cancelled = true;
    }
}