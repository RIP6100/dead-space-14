// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Popups;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Beekeeping;

public sealed class PollinationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollinationComponent, PlantPollinatedEvent>(OnPlantPollinated);
        SubscribeLocalEvent<PollinationComponent, CanPollinateEvent>(OnCanPollinate);
        SubscribeLocalEvent<PollinationComponent, ExaminedEvent>(OnExamined);
        // Широковещательная подписка (не привязанная к компоненту): штатный
        // PlantHolderSystem уже держит directed-подписку на InteractUsing у лотка,
        // а движок не допускает двух directed-подписок на одну пару компонент+событие.
        // Broadcast регистрируется в отдельной таблице и не конфликтует. Ранний return
        // делает её дешёвой даже при частых взаимодействиях на станции.
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<PollinationComponent>();
        while (query.MoveNext(out var uid, out var pollination))
        {
            // Если растение погибло/выкопано — снимаем компонент.
            if (!TryComp<PlantHolderComponent>(uid, out var plantHolder) ||
                plantHolder.Seed == null ||
                plantHolder.Dead)
            {
                RemCompDeferred<PollinationComponent>(uid);
                continue;
            }

            if (!pollination.IsFlowering)
                continue;

            if (pollination.CurrentGrowthMultiplier > 1f && curTime >= pollination.BoostEndTime)
                ResetGrowthBoost(pollination);

            if (pollination.WasPollinated && curTime >= pollination.NextPollinationAvailable)
                pollination.WasPollinated = false;
        }
    }

    /// <summary>
    /// При посадке семени с SeedPollinationComponent в лоток — копируем параметры опыления.
    /// Широковещательный обработчик: сначала отсеиваем всё, что не относится к нашему семени/лотку.
    /// </summary>
    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (!HasComp<PlantHolderComponent>(args.Target))
            return;

        if (!TryComp<SeedPollinationComponent>(args.Used, out var seedPoll))
            return;

        var pollination = EnsureComp<PollinationComponent>(args.Target);
        pollination.IsFlowering = seedPoll.IsFlowering;
        pollination.PollenYield = seedPoll.PollenYield;
        pollination.GrowthSpeedBonus = seedPoll.GrowthSpeedBonus;
        pollination.BoostDuration = seedPoll.BoostDuration;
        pollination.PollinationCooldown = seedPoll.PollinationCooldown;
        pollination.InstantAgeBonus = seedPoll.InstantAgeBonus;
        pollination.PollinationSound = seedPoll.PollinationSound;

        pollination.WasPollinated = false;
        pollination.NextPollinationAvailable = TimeSpan.Zero;
        pollination.CurrentGrowthMultiplier = 1f;
        pollination.BoostEndTime = TimeSpan.Zero;
    }

    private void OnPlantPollinated(EntityUid uid, PollinationComponent pollination, ref PlantPollinatedEvent args)
    {
        if (!pollination.IsFlowering)
            return;

        if (_gameTiming.CurTime < pollination.NextPollinationAvailable)
            return;

        pollination.WasPollinated = true;
        pollination.NextPollinationAvailable = _gameTiming.CurTime + TimeSpan.FromSeconds(pollination.PollinationCooldown);

        if (TryComp<PlantHolderComponent>(uid, out var plantHolder) &&
            plantHolder.Seed != null &&
            !plantHolder.Dead)
        {
            ApplyGrowthBoost(uid, pollination, plantHolder);
        }

        _audio.PlayPvs(pollination.PollinationSound, uid);

        _popup.PopupEntity(
            Loc.GetString("pollination-success-popup"),
            uid,
            Filter.Pvs(uid),
            true);
    }

    private void ApplyGrowthBoost(EntityUid uid, PollinationComponent pollination, PlantHolderComponent plantHolder)
    {
        pollination.CurrentGrowthMultiplier = 1f + pollination.GrowthSpeedBonus;
        pollination.BoostEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(pollination.BoostDuration);

        if (pollination.InstantAgeBonus > 0)
            _plantHolder.AffectGrowth(uid, pollination.InstantAgeBonus, plantHolder);

        var newCycleDelay = TimeSpan.FromSeconds(
            plantHolder.CycleDelay.TotalSeconds / pollination.CurrentGrowthMultiplier);

        var timeReduction = plantHolder.CycleDelay - newCycleDelay;
        if (plantHolder.LastCycle + timeReduction < _gameTiming.CurTime)
            plantHolder.LastCycle = _gameTiming.CurTime - newCycleDelay;
        else
            plantHolder.LastCycle += timeReduction;
    }

    private static void ResetGrowthBoost(PollinationComponent pollination)
    {
        pollination.CurrentGrowthMultiplier = 1f;
        pollination.BoostEndTime = TimeSpan.Zero;
    }

    private void OnCanPollinate(EntityUid uid, PollinationComponent pollination, ref CanPollinateEvent args)
    {
        if (!pollination.IsFlowering)
        {
            args.CanPollinate = false;
            args.Reason = "not-flowering";
            return;
        }

        if (pollination.WasPollinated && _gameTiming.CurTime < pollination.NextPollinationAvailable)
        {
            args.CanPollinate = false;
            args.Reason = "pollination-cooldown";
        }
    }

    private void OnExamined(EntityUid uid, PollinationComponent pollination, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!pollination.IsFlowering)
        {
            args.PushMarkup(Loc.GetString("pollination-examine-not-flowering"));
            return;
        }

        if (pollination.WasPollinated && pollination.CurrentGrowthMultiplier > 1f)
        {
            var remaining = (pollination.BoostEndTime - _gameTiming.CurTime).TotalSeconds;
            if (remaining > 0)
            {
                args.PushMarkup(Loc.GetString("pollination-examine-boosted",
                    ("multiplier", (int) (pollination.GrowthSpeedBonus * 100)),
                    ("time", (int) remaining)));
            }
            else
            {
                args.PushMarkup(Loc.GetString("pollination-examine-pollinated"));
            }
        }
        else
        {
            args.PushMarkup(Loc.GetString("pollination-examine-flowering"));
        }
    }

    /// <summary>
    /// Публичный API: опылить растение от имени пчелы.
    /// </summary>
    public void PollinatePlant(EntityUid uid, EntityUid beeUid, float pollenAmount)
    {
        if (!HasComp<PollinationComponent>(uid))
            return;

        var ev = new PlantPollinatedEvent(beeUid, pollenAmount);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    /// Публичный API: можно ли опылить данное растение.
    /// </summary>
    public bool CanPollinate(EntityUid uid)
    {
        if (!HasComp<PollinationComponent>(uid))
            return false;

        var ev = new CanPollinateEvent();
        RaiseLocalEvent(uid, ref ev);
        return ev.CanPollinate;
    }
}