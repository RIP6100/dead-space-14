// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Stack;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.Beekeeping;

public sealed class HiveFrameSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HiveFrameComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HiveFrameComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HiveFrameDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, HiveFrameComponent frame, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tagSystem.HasTag(args.Used, "Knife"))
            return;

        // Помечаем обработанным в любом случае, чтобы нож не делал ничего постороннего.
        args.Handled = true;

        if (frame.HoneycombAmount < 1f)
            return;

        var ev = new HiveFrameDoAfterEvent
        {
            FrameNetEntity = GetNetEntity(uid),
            UserNetEntity = GetNetEntity(args.User),
        };

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), ev, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            Broadcast = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(HiveFrameDoAfterEvent args)
    {
        if (args.DoAfter?.CancelledTime != null)
            return;

        var uid = GetEntity(args.FrameNetEntity);

        if (!TryComp<HiveFrameComponent>(uid, out var frame))
            return;

        // Перепроверяем, что соты всё ещё есть (рамку могли задеть между стартом и концом).
        if (frame.HoneycombAmount < 1f)
            return;

        var honeycomb = Spawn("MaterialHoneycomb", Transform(uid).Coordinates);
        if (HasComp<StackComponent>(honeycomb))
            _stack.SetCount(honeycomb, (int) MathF.Floor(frame.HoneycombAmount));

        var user = GetEntity(args.UserNetEntity);
        if (Exists(user))
            _hands.PickupOrDrop(user, honeycomb);

        QueueDel(uid);
    }

    private void OnExamined(EntityUid uid, HiveFrameComponent frame, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hive-frame-examine",
            ("honeycomb", (int) frame.HoneycombAmount),
            ("max", (int) frame.MaxCapacity)));
    }

    public void UpdateVisuals(EntityUid uid, HiveFrameComponent frame)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, HiveFrameVisuals.HasHoneycomb, frame.HoneycombAmount >= 1f, appearance);
    }
}