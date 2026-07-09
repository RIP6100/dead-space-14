// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Abilities.TimeStop;
using Content.Shared.DeadSpace.Abilities.TimeStop.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Abilities.TimeStop;

public sealed class TimeStopSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimeStopComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<TimeStopComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<TimeStopComponent, TimeStopActionEvent>(OnTimeStopAction);

        SubscribeLocalEvent<TimeStopOnUseComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // 1) Активные поля морозят цели каждый тик; истёкшие удаляются.
        var fields = EntityQueryEnumerator<TimeStopFieldComponent, TransformComponent>();
        while (fields.MoveNext(out var fieldUid, out var field, out var fieldXform))
        {
            if (curTime >= field.EndTime)
            {
                QueueDel(fieldUid);
                continue;
            }

            FreezeInRange(field, fieldXform);
        }

        // 2) Разморозка тех, у кого истёк срок (поле кончилось).
        var frozen = EntityQueryEnumerator<TimeStoppedComponent>();
        while (frozen.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.EndTime)
                Unfreeze(uid);
        }
    }

    #region Activation

    private void OnComponentInit(EntityUid uid, TimeStopComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionTimeStopEntity, component.ActionTimeStop, uid);
    }

    private void OnComponentShutdown(EntityUid uid, TimeStopComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionTimeStopEntity);
    }

    private void OnTimeStopAction(EntityUid uid, TimeStopComponent component, TimeStopActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        StartField(uid, component.EffectPrototype, component.Range, component.Duration,
            component.MuteFrozen, component.IgnoreFriendly, component.TimeStopSound);
    }

    private void OnUseInHand(EntityUid uid, TimeStopOnUseComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<UseDelayComponent>(uid, out var delay) && _useDelay.IsDelayed((uid, delay)))
            return;

        StartField(args.User, component.EffectPrototype, component.Range, component.Duration,
            component.MuteFrozen, false, component.TimeStopSound);

        args.Handled = true;

        if (delay != null)
            _useDelay.TryResetDelay((uid, delay));
    }

    /// <summary>
    /// Спавнит поле остановленного времени в точке кастера.
    /// </summary>
    public EntityUid? StartField(
        EntityUid caster,
        string fieldPrototype,
        float range,
        float durationSeconds,
        bool mute,
        bool ignoreFriendly,
        SoundSpecifier? sound)
    {
        if (string.IsNullOrEmpty(fieldPrototype))
            fieldPrototype = "TimeStopField";

        var coords = Transform(caster).Coordinates;
        var field = Spawn(fieldPrototype, coords);

        var comp = EnsureComp<TimeStopFieldComponent>(field);
        comp.Range = range;
        comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(durationSeconds);
        comp.Muted = mute;
        comp.Caster = caster;
        comp.IgnoreFriendly = ignoreFriendly;

        if (sound != null)
            _audio.PlayPvs(sound, caster, AudioParams.Default.WithVolume(2).WithMaxDistance(range * 2));

        return field;
    }

    #endregion

    #region Freeze logic

    private void FreezeInRange(TimeStopFieldComponent field, TransformComponent fieldXform)
    {
        var mapId = fieldXform.MapID;
        var epicenter = _transform.GetWorldPosition(fieldXform);
        var rangeSquared = field.Range * field.Range;
        var endTime = field.EndTime;

        // Живые существа (игроки и NPC).
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out _, out var xform))
            TryFreeze(uid, xform, field, mapId, epicenter, rangeSquared, endTime);

        // Снаряды (пули, стрелы, магия) — фикстуры нежёсткие, обычный лукап их пропускает.
        var projectiles = EntityQueryEnumerator<ProjectileComponent, TransformComponent>();
        while (projectiles.MoveNext(out var uid, out _, out var xform))
            TryFreeze(uid, xform, field, mapId, epicenter, rangeSquared, endTime);

        // Брошенные предметы (ножи и т.п.).
        var thrown = EntityQueryEnumerator<ThrownItemComponent, TransformComponent>();
        while (thrown.MoveNext(out var uid, out _, out var xform))
            TryFreeze(uid, xform, field, mapId, epicenter, rangeSquared, endTime);
    }

    private void TryFreeze(
        EntityUid uid,
        TransformComponent xform,
        TimeStopFieldComponent field,
        MapId mapId,
        Vector2 epicenter,
        float rangeSquared,
        TimeSpan endTime)
    {
        if (uid == field.Caster)
            return;

        if (xform.MapID != mapId)
            return;

        var delta = _transform.GetWorldPosition(xform) - epicenter;
        if (delta.LengthSquared() > rangeSquared)
            return;

        if (field.IgnoreFriendly && field.Caster != null
            && _npcFaction.IsEntityFriendly(field.Caster.Value, uid))
            return;

        Freeze(uid, endTime, field.Muted);
    }

    /// <summary>
    /// Замораживает сущность: пауза (стоп физики с сохранением скорости) + запрет действий.
    /// Если уже заморожена — просто продлевает срок.
    /// </summary>
    public void Freeze(EntityUid uid, TimeSpan endTime, bool mute)
    {
        if (TryComp<TimeStoppedComponent>(uid, out var existing))
        {
            existing.EndTime = endTime;
            return;
        }

        var comp = AddComp<TimeStoppedComponent>(uid);
        comp.EndTime = endTime;
        comp.Muted = mute;
        Dirty(uid, comp);

        _meta.SetEntityPaused(uid, true);
    }

    /// <summary>
    /// Снимает заморозку: снимает паузу (физика и полёт продолжаются) и убирает компонент.
    /// </summary>
    public void Unfreeze(EntityUid uid)
    {
        _meta.SetEntityPaused(uid, false);
        RemCompDeferred<TimeStoppedComponent>(uid);
    }

    #endregion
}