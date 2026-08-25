using System.Collections.Generic;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;

namespace Content.IntegrationTests.Tests.Atmos;

[TestOf(typeof(Atmospherics))]
public sealed class ConstantsTest
{
    [Test]
    public async Task TotalGasesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var protoManager = server.ProtoMan;

        await server.WaitPost(() =>
        {
            var atmosSystem = entityManager.System<AtmosphereSystem>();

            Assert.Multiple(() =>
            {
                // DS14: gases are data-driven. The gas prototypes (not the Gas enum) are the source of truth for
                // the gas count and indices, so a new gas can be added purely in YAML. These invariants check the
                // runtime registry is consistent; the Gas enum is now only an optional convenience for C# code.
                var gasProtos = protoManager.EnumeratePrototypes<GasPrototype>().ToList();

                // The runtime gas count matches the number of gas prototypes.
                Assert.That(atmosSystem.GasCount, Is.EqualTo(gasProtos.Count),
                    "AtmosphereSystem.GasCount does not match the number of GasPrototypes.");
                Assert.That(Atmospherics.TotalNumberOfGases, Is.EqualTo(gasProtos.Count),
                    "TotalNumberOfGases does not match the number of GasPrototypes.");
                // Number of gas prototypes registered in the atmos system.
                Assert.That(atmosSystem.Gases.Count(), Is.EqualTo(gasProtos.Count),
                    "AtmosSystem.Gases is not equal to the number of GasPrototypes.");
                // The mole-array length must stay a multiple of 4 (SIMD) and be able to hold every gas.
                Assert.That(Atmospherics.AdjustedNumberOfGases % 4, Is.EqualTo(0),
                    "AdjustedNumberOfGases must be a multiple of 4.");
                Assert.That(Atmospherics.AdjustedNumberOfGases, Is.GreaterThanOrEqualTo(Atmospherics.TotalNumberOfGases),
                    "AdjustedNumberOfGases must be able to hold every gas.");

                // Every gas prototype must resolve to a unique index within range.
                var seenIndices = new HashSet<int>();
                foreach (var gas in gasProtos)
                {
                    Assert.That(atmosSystem.TryGetGasId(gas.ID, out var id), Is.True,
                        $"GasPrototype {gas.ID} did not register an index.");
                    Assert.That(id, Is.InRange(0, Atmospherics.TotalNumberOfGases - 1),
                        $"GasPrototype {gas.ID} has an out-of-range index {id}.");
                    Assert.That(seenIndices.Add(id), Is.True,
                        $"GasPrototype {gas.ID} reuses index {id}.");
                }

                // Every legacy Gas enum value must still have a matching prototype (enum is a subset of prototypes).
                foreach (var gas in Enum.GetValues<Gas>())
                {
                    Assert.That(protoManager.HasIndex<GasPrototype>(gas.ToString()), Is.True,
                        $"Gas enum value {gas} has no matching GasPrototype.");
                }
            });
        });
        await pair.CleanReturnAsync();
    }
}

