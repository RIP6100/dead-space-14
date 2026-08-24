using Content.Server.Research.Systems;
using Content.Server.Xenoarchaeology.Artifact;
using Content.Shared.Popups;
using Content.Shared.Xenoarchaeology.Equipment;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Shared.Audio.Systems;
using Content.Server.DeadSpace.Research; // DS14
using Content.Shared.DeadSpace.Research.Components; // DS14

namespace Content.Server.Xenoarchaeology.Equipment;

/// <inheritdoc />
public sealed class ArtifactAnalyzerSystem : SharedArtifactAnalyzerSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly XenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly ResearchDataSystem _data = default!; // DS14

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnalysisConsoleComponent, AnalysisConsoleExtractButtonPressedMessage>(OnExtractButtonPressed);
    }

    private void OnExtractButtonPressed(Entity<AnalysisConsoleComponent> ent, ref AnalysisConsoleExtractButtonPressedMessage args)
    {
        if (!TryGetArtifactFromConsole(ent, out var artifact))
            return;
        // DS14-start
        if (!_research.TryGetClientServer(ent, out var server, out _))
            return;

        var extractedAny = false;
        foreach (var node in _xenoArtifact.GetAllNodes(artifact.Value))
        {
            var research = _xenoArtifact.GetResearchValue(node);
            if (research <= 0)
                continue;

            _xenoArtifact.SetConsumedResearchValue(node, node.Comp.ConsumedResearchValue + research);

            if (!TryComp<ResearchFieldYieldComponent>(node, out var yield))
                continue;

            var ratio = research / node.Comp.BasePointValue;
            foreach (var (field, baseAmount) in yield.Fields)
            {
                var amount = (int) MathF.Round(baseAmount * ratio);
                if (amount > 0 && _data.AddData(server.Value, field, amount))
                    extractedAny = true;
            }
        }

        if (!extractedAny)
            return;

        // DS14-end
        _audio.PlayPvs(ent.Comp.ExtractSound, artifact.Value);
        _popup.PopupEntity(Loc.GetString("analyzer-artifact-extract-popup"), artifact.Value, PopupType.Large);
    }
}

