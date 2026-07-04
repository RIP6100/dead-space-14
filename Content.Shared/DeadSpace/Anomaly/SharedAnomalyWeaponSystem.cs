// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Anomaly.Components;
using Content.Shared.Anomaly.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Weapons.Anomaly.Systems;

public sealed class SharedAnomalyWeaponSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyCoreAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<AnomalyCoreAmmoProviderComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<AnomalyCoreAmmoProviderComponent, ExaminedEvent>(OnExamined);
    }

    private void OnTakeAmmo(Entity<AnomalyCoreAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        var (uid, comp) = ent;

        if (!TryGetLoadedCore(uid, comp, out var coreUid, out var core))
        {
            PlayDenialFeedback(uid, comp, args.User, comp.NoCoreMessage);
            return;
        }

        if (!TryResolveEntry(comp, coreUid, out var entry))
        {
            PlayDenialFeedback(uid, comp, args.User, comp.UnsupportedCoreMessage);
            return;
        }

        for (var i = 0; i < args.Shots; i++)
        {
            if (!TryConsumeCharge(coreUid, core, entry.EnergyCost))
            {
                PlayDenialFeedback(uid, comp, args.User, comp.CoreDepletedMessage);
                break;
            }

            var projectile = Spawn(entry.Proto, args.Coordinates);
            args.Ammo.Add((projectile, EnsureShootable(projectile)));
        }
    }

    private void OnGetAmmoCount(Entity<AnomalyCoreAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        var (uid, comp) = ent;

        if (!TryGetLoadedCore(uid, comp, out var _, out var core))
        {
            args.Count = 0;
            args.Capacity = 0;
            return;
        }

        if (core.IsDecayed)
        {
            args.Count = core.Charge;
            args.Capacity = core.Charge;
        }
        else
        {
            args.Count = 1;
            args.Capacity = 1;
        }
    }

    private void OnExamined(Entity<AnomalyCoreAmmoProviderComponent> ent, ref ExaminedEvent args)
    {
        var (uid, comp) = ent;

        if (!args.IsInDetailsRange)
            return;

        if (!TryGetLoadedCore(uid, comp, out var coreUid, out var core))
        {
            args.PushMarkup(Loc.GetString("anomaly-core-weapon-examine-empty"));
            return;
        }

        var coreName = Name(coreUid);

        args.PushMarkup(core.IsDecayed
            ? Loc.GetString("anomaly-core-weapon-examine-charges", ("core", coreName), ("charges", core.Charge))
            : Loc.GetString("anomaly-core-weapon-examine-stable", ("core", coreName)));
    }

    private bool TryResolveEntry(AnomalyCoreAmmoProviderComponent comp, EntityUid coreUid, out AnomalyCoreAmmoEntry entry)
    {
        entry = default!;

        if (!TryPrototype(coreUid, out var coreProto))
            return false;

        if (comp.CoreToProjectile.TryGetValue(coreProto.ID, out var mapped))
        {
            entry = mapped;
            return true;
        }

        if (comp.FallbackEntry is { } fallback)
        {
            entry = fallback;
            return true;
        }

        return false;
    }

    private bool TryGetLoadedCore(
        EntityUid uid,
        AnomalyCoreAmmoProviderComponent comp,
        out EntityUid coreUid,
        out AnomalyCoreComponent core)
    {
        coreUid = default;
        core = default!;

        var slotItem = _itemSlots.GetItemOrNull(uid, comp.CoreSlotId);
        if (slotItem is not { } itemUid)
            return false;

        if (!TryComp(itemUid, out AnomalyCoreComponent? coreComp))
            return false;

        coreUid = itemUid;
        core = coreComp;
        return true;
    }

    private bool TryConsumeCharge(EntityUid coreUid, AnomalyCoreComponent core, int cost)
    {
        if (!core.IsDecayed)
            return true;

        if (core.Charge < cost)
            return false;

        core.Charge -= cost;
        Dirty(coreUid, core);
        return true;
    }

    private void PlayDenialFeedback(EntityUid uid, AnomalyCoreAmmoProviderComponent comp, EntityUid? user, LocId message)
    {
        _audio.PlayPredicted(comp.NoCoreSound, uid, user);
        _popup.PopupClient(Loc.GetString(message), uid, user);
    }

    private IShootable EnsureShootable(EntityUid uid)
    {
        return EnsureComp<AmmoComponent>(uid);
    }
}
