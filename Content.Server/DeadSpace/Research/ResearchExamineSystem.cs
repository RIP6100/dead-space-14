// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Research.Components;
using Content.Shared.DeadSpace.Research.Components;
using Content.Shared.DeadSpace.Research.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Research;

public sealed class ResearchExamineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ResearchDataSystem _data = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchDataStorageComponent, ExaminedEvent>(OnServerExamined);
        SubscribeLocalEvent<ResearchDataDiskComponent, ExaminedEvent>(OnDiskExamined);
        SubscribeLocalEvent<ResearchPrintoutComponent, ExaminedEvent>(OnPrintoutExamined);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, ExaminedEvent>(OnConsoleExamined);
    }

    private void OnServerExamined(EntityUid uid, ResearchDataStorageComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        AppendDataSummary(args, _data.GetAvailable(uid), "research-examine-server-header", "research-examine-server-empty");
    }

    private void OnDiskExamined(EntityUid uid, ResearchDataDiskComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        AppendDataSummary(args, comp.Data, "research-examine-disk-header", "research-examine-disk-empty");
    }

    private void OnPrintoutExamined(EntityUid uid, ResearchPrintoutComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!string.IsNullOrEmpty(comp.Tech.Id) && _proto.TryIndex(comp.Tech, out var tech))
            args.PushMarkup(Loc.GetString("research-examine-printout", ("tech", Loc.GetString(tech.Name))));
        else
            args.PushMarkup(Loc.GetString("research-examine-printout-empty"));
    }

    private void OnConsoleExamined(EntityUid uid, ResearchMosaicConsoleComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("research-examine-console"));
    }

    private void AppendDataSummary(
        ExaminedEvent args,
        Dictionary<ProtoId<ResearchFieldPrototype>, int> data,
        string headerLoc,
        string emptyLoc)
    {
        if (data.Count == 0)
        {
            args.PushMarkup(Loc.GetString(emptyLoc));
            return;
        }

        args.PushMarkup(Loc.GetString(headerLoc), 1);
        foreach (var (field, amount) in data)
        {
            var proto = _proto.Index(field);
            var name = $"[color={proto.Color.ToHex()}]{Loc.GetString(proto.Name)}[/color]";
            args.PushMarkup(Loc.GetString("research-examine-data-line", ("field", name), ("amount", amount)));
        }
    }
}
