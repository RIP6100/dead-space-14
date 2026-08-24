// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Research.Components;
using System;
using Content.Server.GameTicking;
using Content.Server.Research.Systems;
using Content.Shared.DeadSpace.Research;
using Content.Shared.DeadSpace.Research.Components;
using Content.Shared.Paper;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Research;

public sealed class ResearchPrinterSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedResearchMosaicSystem _mosaic = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchPrinterComponent, PrintResearchMessage>(OnPrint);
        SubscribeLocalEvent<ResearchPrinterComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
    }

    private void OnPrint(EntityUid uid, ResearchPrinterComponent component, PrintResearchMessage args)
    {
        if (string.IsNullOrEmpty(args.Tech.Id) || !_proto.TryIndex(args.Tech, out var tech) || tech.Hidden)
            return;

        var printout = Spawn(component.PrintoutProto, _xform.GetMapCoordinates(uid));
        var comp = EnsureComp<ResearchPrintoutComponent>(printout);
        comp.Tech = args.Tech;
        Dirty(printout, comp);

        var time = _ticker.RoundDuration().ToString(@"hh\:mm\:ss");
        var date = DateTime.UtcNow.AddHours(3).ToString("dd.MM") + ".2710";
        _paper.SetContent(printout, Loc.GetString("research-printout-content",
            ("tech", Loc.GetString(tech.Name)),
            ("time", time),
            ("date", date)));

        _audio.PlayPvs(component.PrintSound, uid);

        UpdateUiState(uid);
    }

    private void OnBeforeUiOpen(EntityUid uid, ResearchPrinterComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUiState(uid);
    }

    private void UpdateUiState(EntityUid printer)
    {
        if (!_ui.HasUi(printer, ResearchPrinterUiKey.Key))
            return;

        var available = new List<ProtoId<TechnologyPrototype>>();
        var unlocked = new List<ProtoId<TechnologyPrototype>>();
        var supported = new List<ProtoId<TechDisciplinePrototype>>();
        if (_research.TryGetClientServer(printer, out var server, out _)
            && TryComp<TechnologyDatabaseComponent>(server, out var db))
        {
            var unlockedSet = db.UnlockedTechnologies;
            supported = new List<ProtoId<TechDisciplinePrototype>>(db.SupportedDisciplines);
            foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
            {
                if (tech.Hidden)
                    continue;

                if (unlockedSet.Contains(tech.ID))
                    unlocked.Add(tech.ID);
                else if (_mosaic.IsAvailable(db, tech))
                    available.Add(tech.ID);
            }
        }

        _ui.SetUiState(printer, ResearchPrinterUiKey.Key, new ResearchPrinterState(available, unlocked, supported));
    }
}
