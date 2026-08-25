using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Reactions;
using Content.Shared.Atmos;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Atmos;

/// <summary>
/// DS14: smoke tests for the data-driven gas reaction engine (GasRecipeReaction: generic blocks + specialEffect).
/// Each reaction's real YAML-loaded effect is run against a triggering gas mixture and checked for the correct
/// directional transformation. This guards against gross regressions (wrong dispatch, wrong products, no reaction).
/// </summary>
[TestFixture]
public sealed class GasReactionSmokeTest
{
    [Test]
    public async Task ReactionsTransformGasesCorrectly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protoMan = server.ProtoMan;
            var atmos = server.EntMan.System<AtmosphereSystem>();

            IGasReactionEffect Effect(string id) => protoMan.Index<GasReactionPrototype>(id).Effects[0];

            // --- PlasmaFire (specialEffect): plasma + oxygen burn, produce tritium/CO2, release heat ---
            {
                var mix = new GasMixture(Atmospherics.CellVolume)
                {
                    Temperature = Atmospherics.PlasmaMinimumBurnTemperature + 100f,
                };
                mix.AdjustMoles(Gas.Plasma, 20f);
                mix.AdjustMoles(Gas.Oxygen, 100f);
                var startTemp = mix.Temperature;

                Effect("PlasmaFire").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(Gas.Plasma), Is.LessThan(20f), "PlasmaFire: plasma should be consumed");
                    Assert.That(mix.GetMoles(Gas.Oxygen), Is.LessThan(100f), "PlasmaFire: oxygen should be consumed");
                    Assert.That(mix.GetMoles(Gas.Tritium) + mix.GetMoles(Gas.CarbonDioxide), Is.GreaterThan(0f),
                        "PlasmaFire: should produce tritium and/or CO2");
                    Assert.That(mix.Temperature, Is.GreaterThan(startTemp), "PlasmaFire: should release heat");
                });
            }

            // --- TritiumFire (specialEffect): tritium + oxygen burn, produce water vapor, release heat ---
            {
                var mix = new GasMixture(Atmospherics.CellVolume)
                {
                    Temperature = Atmospherics.FireMinimumTemperatureToExist + 100f,
                };
                mix.AdjustMoles(Gas.Tritium, 20f);
                mix.AdjustMoles(Gas.Oxygen, 100f);
                var startTemp = mix.Temperature;

                Effect("TritiumFire").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(Gas.Tritium), Is.LessThan(20f), "TritiumFire: tritium should be consumed");
                    Assert.That(mix.GetMoles(Gas.WaterVapor), Is.GreaterThan(0f), "TritiumFire: should produce water vapor");
                    Assert.That(mix.Temperature, Is.GreaterThan(startTemp), "TritiumFire: should release heat");
                });
            }

            // --- FrezonProduction (specialEffect): cold oxygen + tritium + nitrogen catalyst -> frezon ---
            {
                var mix = new GasMixture(Atmospherics.CellVolume)
                {
                    Temperature = Atmospherics.FrezonProductionMaxEfficiencyTemperature * 0.8f,
                };
                mix.AdjustMoles(Gas.Oxygen, 50f);
                mix.AdjustMoles(Gas.Tritium, 50f);
                mix.AdjustMoles(Gas.Nitrogen, 10f);

                Effect("FrezonProduction").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(Gas.Frezon), Is.GreaterThan(0f), "FrezonProduction: should produce frezon");
                    Assert.That(mix.GetMoles(Gas.Tritium), Is.LessThan(50f), "FrezonProduction: tritium should be consumed");
                });
            }

            // --- FrezonCoolant (generic blocks + temperatureScale): frezon + nitrogen -> N2O, absorb heat ---
            {
                var mix = new GasMixture(Atmospherics.CellVolume)
                {
                    Temperature = Atmospherics.T20C + 50f,
                };
                mix.AdjustMoles(Gas.Frezon, 30f);
                mix.AdjustMoles(Gas.Nitrogen, 100f);
                var startTemp = mix.Temperature;

                Effect("FrezonCoolant").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(Gas.Frezon), Is.LessThan(30f), "FrezonCoolant: frezon should be consumed");
                    Assert.That(mix.GetMoles(Gas.NitrousOxide), Is.GreaterThan(0f), "FrezonCoolant: should produce N2O");
                    Assert.That(mix.Temperature, Is.LessThan(startTemp), "FrezonCoolant: should absorb heat (cool)");
                });
            }

            // --- N2ODecomposition (generic blocks): hot N2O -> nitrogen + oxygen ---
            {
                var mix = new GasMixture(Atmospherics.CellVolume)
                {
                    Temperature = 900f,
                };
                mix.AdjustMoles(Gas.NitrousOxide, 100f);

                Effect("N2ODecomposition").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(Gas.NitrousOxide), Is.LessThan(100f), "N2ODecomposition: N2O should be consumed");
                    Assert.That(mix.GetMoles(Gas.Nitrogen), Is.GreaterThan(0f), "N2ODecomposition: should produce nitrogen");
                    Assert.That(mix.GetMoles(Gas.Oxygen), Is.GreaterThan(0f), "N2ODecomposition: should produce oxygen");
                });
            }

            // --- AmmoniaOxygenReaction (generic blocks + concentration): ammonia + oxygen -> N2O + water vapor ---
            {
                var mix = new GasMixture(Atmospherics.CellVolume)
                {
                    Temperature = Atmospherics.T20C + 100f,
                };
                mix.AdjustMoles(Gas.Ammonia, 40f);
                mix.AdjustMoles(Gas.Oxygen, 40f);

                Effect("AmmoniaOxygenReaction").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(Gas.Ammonia), Is.LessThan(40f), "AmmoniaOxygen: ammonia should be consumed");
                    Assert.That(mix.GetMoles(Gas.NitrousOxide), Is.GreaterThan(0f), "AmmoniaOxygen: should produce N2O");
                    Assert.That(mix.GetMoles(Gas.WaterVapor), Is.GreaterThan(0f), "AmmoniaOxygen: should produce water vapor");
                });
            }

            // --- FusiumCombustion (DS14 test gas): fully YAML-authored reaction with a no-enum reactant (Fusium),
            //     a no-enum product (Voidgas), plus the temperatureScale and productSplit generic blocks. ---
            {
                var fusiumId = atmos.GetGasId("Fusium");
                var voidId = atmos.GetGasId("Voidgas");
                var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = 500f };
                mix.AdjustMoles(fusiumId, 20f);
                mix.AdjustMoles(Gas.Oxygen, 20f);

                Effect("FusiumCombustion").React(mix, null, atmos, 1f);

                Assert.Multiple(() =>
                {
                    Assert.That(mix.GetMoles(fusiumId), Is.LessThan(20f), "FusiumCombustion: no-enum reactant Fusium should be consumed");
                    Assert.That(mix.GetMoles(voidId) + mix.GetMoles(Gas.CarbonDioxide), Is.GreaterThan(0f),
                        "FusiumCombustion: productSplit should produce Voidgas and/or CO2");
                });
            }
        });

        await pair.CleanReturnAsync();
    }
}
