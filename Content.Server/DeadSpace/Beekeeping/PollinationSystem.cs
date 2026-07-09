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
        // Broadcast-подписка (не directed). Причина: посадку семени обрабатывает штатный
        // PlantHolderSystem через directed InteractUsing на лотке и помечает событие Handled,
        // из-за чего AfterInteract на семени не поднимается. А вторую directed-подписку на
        // ту же пару (PlantHolderComponent + InteractUsing) движок не разрешает. Поэтому
        // ловим InteractUsing широковещательно и отсеиваем чужое ранними проверками -
        // самый дешёвый отсев (наличие SeedPollinationComponent на Used) стоит первым.
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
                ResetGrowthBoost(uid, pollination, plantHolder);

            if (pollination.WasPollinated && curTime >= pollination.NextPollinationAvailable)
                pollination.WasPollinated = false;
        }
    }

    /// <summary>
    /// Когда игрок тыкает семенем (с SeedPollinationComponent) в лоток с растением -
    /// копируем параметры опыления с семени на лоток как PollinationComponent.
    /// Broadcast: срабатывает на любое InteractUsing, поэтому сразу отсеиваем чужое.
    /// </summary>
    private void OnInteractUsing(InteractUsingEvent args)
    {
        // Самый дешёвый отсев первым: наш ли это предмет-семя.
        if (!TryComp<SeedPollinationComponent>(args.Used, out var seedPoll))
            return;

        // Цель должна быть лотком с растением.
        if (!HasComp<PlantHolderComponent>(args.Target))
            return;

        ApplySeedToPlant(args.Target, seedPoll);
    }

    /// <summary>
    /// Переносит параметры опыления с семени на лоток и сбрасывает служебное состояние.
    /// </summary>
    private void ApplySeedToPlant(EntityUid plant, SeedPollinationComponent seedPoll)
    {
        var pollination = EnsureComp<PollinationComponent>(plant);
        pollination.IsFlowering = seedPoll.IsFlowering;
        pollination.PollenYield = seedPoll.PollenYield;
        pollination.GrowthSpeedBonus = seedPoll.GrowthSpeedBonus;
        pollination.BoostDuration = seedPoll.BoostDuration;
        pollination.PollinationCooldown = seedPoll.PollinationCooldown;
        pollination.InstantAgeBonus = seedPoll.InstantAgeBonus;
        pollination.PollinationSound = seedPoll.PollinationSound;
        pollination.PotencyBonus = seedPoll.PotencyBonus;

        pollination.WasPollinated = false;
        pollination.NextPollinationAvailable = TimeSpan.Zero;
        pollination.CurrentGrowthMultiplier = 1f;
        pollination.BoostEndTime = TimeSpan.Zero;
        pollination.AppliedPotencyDelta = 0f;
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

        // Повышаем потенцию плодов. Если буст перезаписывается повторным опылением,
        // прошлая прибавка сначала снимается внутри ApplyPotencyBonus (чтобы не копилась).
        ApplyPotencyBonus(uid, pollination, plantHolder);

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

    /// <summary>
    /// Повышает потенцию текущего растения на PotencyBonus (в долях от текущей).
    /// Сохраняет применённую дельту в AppliedPotencyDelta, чтобы позже откатить ровно её.
    /// Перед применением снимает предыдущую прибавку (на случай повторного опыления).
    /// </summary>
    private void ApplyPotencyBonus(EntityUid uid, PollinationComponent pollination, PlantHolderComponent plantHolder)
    {
        if (plantHolder.Seed == null)
            return;

        // Снимаем прошлую прибавку, если была (перезапись буста), чтобы не накапливать.
        RevertPotencyBonus(uid, pollination, plantHolder);

        if (pollination.PotencyBonus <= 0f)
            return;

        // КРИТИЧЕСКИ ВАЖНО: делаем Seed уникальным перед мутацией. Иначе plantHolder.Seed
        // может быть ОБЩЕЙ ссылкой на SeedData прототипа, и изменение Potency растеклось бы
        // на ВСЕ растения этого вида. EnsureUniqueSeed заменяет разделяемый seed на клон.
        _plantHolder.EnsureUniqueSeed(uid, plantHolder);
        if (plantHolder.Seed == null)
            return;

        // Прибавка = процент от текущей потенции, округлённый (потенция отображается целой).
        // Итог ограничиваем сверху 100 (потолок потенции), в дельту кладём реально
        // применённое приращение для точного отката.
        var delta = MathF.Round(plantHolder.Seed.Potency * pollination.PotencyBonus);

        var newPotency = MathF.Min(100f, MathF.Round(plantHolder.Seed.Potency) + delta);
        pollination.AppliedPotencyDelta = newPotency - plantHolder.Seed.Potency;
        plantHolder.Seed.Potency = newPotency;
    }

    /// <summary>
    /// Снимает ранее применённую прибавку потенции (вычитает сохранённую дельту).
    /// Вычитаем именно дельту, а не восстанавливаем старое абсолютное значение - так
    /// естественные изменения потенции за время буста (мутации/удобрения) сохраняются.
    /// </summary>
    private void RevertPotencyBonus(EntityUid uid, PollinationComponent pollination, PlantHolderComponent plantHolder)
    {
        if (pollination.AppliedPotencyDelta == 0f || plantHolder.Seed == null)
            return;

        // Seed уже уникален (был уникализирован при применении), просто вычитаем дельту.
        plantHolder.Seed.Potency = MathF.Max(0f, plantHolder.Seed.Potency - pollination.AppliedPotencyDelta);
        pollination.AppliedPotencyDelta = 0f;
    }

    private void ResetGrowthBoost(EntityUid uid, PollinationComponent pollination, PlantHolderComponent plantHolder)
    {
        pollination.CurrentGrowthMultiplier = 1f;
        pollination.BoostEndTime = TimeSpan.Zero;

        // Возвращаем потенцию к прежнему уровню, снимая нашу прибавку.
        RevertPotencyBonus(uid, pollination, plantHolder);
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