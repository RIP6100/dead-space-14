// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Research;

public sealed class SharedResearchMosaicSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public bool AreFieldsRelated(ProtoId<ResearchFieldPrototype> a, ProtoId<ResearchFieldPrototype> b)
    {
        if (a == b)
            return false;

        var pa = _proto.Index(a);
        var pb = _proto.Index(b);
        return pa.Components.Contains(b) || pb.Components.Contains(a);
    }

    public bool IsAvailable(TechnologyDatabaseComponent db, TechnologyPrototype tech)
    {
        if (tech.Hidden)
            return false;

        var supported = db.SupportedDisciplines;
        var unlocked = db.UnlockedTechnologies;

        if (!supported.Contains(tech.Discipline))
            return false;

        if (unlocked.Contains(tech.ID))
            return false;

        foreach (var prereq in tech.TechnologyPrerequisites)
        {
            if (!unlocked.Contains(prereq))
                return false;
        }

        return true;
    }

    public bool TryGetCombination(ProtoId<ResearchFieldPrototype> a, ProtoId<ResearchFieldPrototype> b, out ProtoId<ResearchFieldPrototype> result)
    {
        result = default;
        if (a == b)
            return false;

        foreach (var field in _proto.EnumeratePrototypes<ResearchFieldPrototype>())
        {
            var comps = field.Components;
            if (comps.Count != 2)
                continue;

            if ((comps[0] == a && comps[1] == b) || (comps[0] == b && comps[1] == a))
            {
                result = new ProtoId<ResearchFieldPrototype>(field.ID);
                return true;
            }
        }

        return false;
    }
}
