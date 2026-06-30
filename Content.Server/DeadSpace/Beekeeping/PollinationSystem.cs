
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Popups;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
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
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing); // ← глобальная подписка
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var plantQuery = EntityQueryEnumerator<PlantHolderComponent>();
        while (plantQuery.MoveNext(out var uid, out var plantHolder))
        {
            if (plantHolder.Seed == null || plantHolder.Dead)
            {
                if (HasComp<PollinationComponent>(uid))
                    RemComp<PollinationComponent>(uid);
                continue;
            }
        }

        var query = EntityQueryEnumerator<PollinationComponent>();
        while (query.MoveNext(out var uid, out var pollination))
        {
            if (!pollination.IsFlowering)
                continue;

            if (pollination.CurrentGrowthMultiplier > 1f &&
                _gameTiming.CurTime >= pollination.BoostEndTime)
            {
                ResetGrowthBoost(uid, pollination);
            }

            if (pollination.WasPollinated &&
                _gameTiming.CurTime >= pollination.NextPollinationAvailable)
            {
                pollination.WasPollinated = false;
            }
        }
    }

    /// <summary>
    /// Глобальный обработчик: когда игрок использует предмет на сущности.
    /// Если это семя с SeedPollinationComponent на лоток — копируем параметры.
    /// </summary>
    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (!TryComp<PlantHolderComponent>(args.Target, out var plantHolder)) return;
        if (!TryComp<SeedPollinationComponent>(args.Used, out var seedPoll)) return;

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
            Robust.Shared.Player.Filter.Pvs(uid),
            true);
    }

    private void ApplyGrowthBoost(EntityUid uid, PollinationComponent pollination, PlantHolderComponent plantHolder)
    {
        pollination.CurrentGrowthMultiplier = 1f + pollination.GrowthSpeedBonus;
        pollination.BoostEndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(pollination.BoostDuration);

        if (pollination.InstantAgeBonus > 0)
        {
            _plantHolder.AffectGrowth(uid, pollination.InstantAgeBonus, plantHolder);
        }

        var newCycleDelay = TimeSpan.FromSeconds(
            plantHolder.CycleDelay.TotalSeconds / pollination.CurrentGrowthMultiplier);

        var timeReduction = plantHolder.CycleDelay - newCycleDelay;
        if (plantHolder.LastCycle + timeReduction < _gameTiming.CurTime)
        {
            plantHolder.LastCycle = _gameTiming.CurTime - newCycleDelay;
        }
        else
        {
            plantHolder.LastCycle += timeReduction;
        }
    }

    private void ResetGrowthBoost(EntityUid uid, PollinationComponent pollination)
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
            return;
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
                    ("multiplier", (int)(pollination.GrowthSpeedBonus * 100)),
                    ("time", (int)remaining)));
            }
            else
            {
                args.PushMarkup(Loc.GetString("pollination-examine-pollinated"));
            }
        }
        else if (pollination.IsFlowering)
        {
            args.PushMarkup(Loc.GetString("pollination-examine-flowering"));
        }
    }

    public void PollinatePlant(EntityUid uid, EntityUid beeUid, float pollenAmount)
    {
        if (!TryComp<PollinationComponent>(uid, out var pollination))
            return;

        var ev = new PlantPollinatedEvent(beeUid, pollenAmount);
        RaiseLocalEvent(uid, ref ev);
    }

    public bool CanPollinate(EntityUid uid)
    {
        if (!TryComp<PollinationComponent>(uid, out var pollination))
            return false;

        var ev = new CanPollinateEvent();
        RaiseLocalEvent(uid, ref ev);
        return ev.CanPollinate;
    }
}