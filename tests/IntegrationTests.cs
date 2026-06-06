using System.Text;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>
/// Whole-stack soak: a rich world (titles + soft-lock, holdings, spheres/coalition, a married couple
/// spanning generations) driven through <see cref="TurnController"/> for many turns against the real
/// content. Guards the <em>interaction</em> invariants the per-system unit tests can't: end-to-end
/// determinism, succession actually landing, crises de-escalating (not accumulating forever), and no
/// absurd state (dead protagonist with a live dynasty, negative resources, runaway accumulators).
/// </summary>
public class IntegrationTests
{
    private static World Rich(ulong seed)
    {
        var w = new World { ProtagonistId = "marcus", RngState = seed };
        void H(string id, string name, string title, int legit) =>
            w.Houses[id] = new House { Id = id, Name = name, Title = title, Legitimacy = legit, Treasury = new Resources { Credits = 10, Materials = 8, Manpower = 5 } };
        H("vega", "House Vega", "baron", 18);          // baron needs 25 → soft-lock instability
        H("corwin", "House Corwin", "count", 45);
        H("drake", "House Drake", "baron", 30);
        H("sato", "House Sato", "knight", 12);
        void C(string id, string house, int age, string sex, string track, int rank, string father = "")
        {
            w.Characters[id] = new Character { Id = id, Name = id, HouseId = house, Age = age, Alive = true, Sex = sex, CareerTrack = track, CareerRank = rank, FatherId = father };
            w.Houses[house].Members.Add(id);
            w.Pools[id] = id == "marcus" ? ActionPools.ForPlayer() : ActionPools.ForNpc();
        }
        C("marcus", "vega", 54, "male", "military", 3);
        C("junia", "vega", 30, "female", "", 0);
        C("vega_heir", "vega", 25, "male", "military", 1, "marcus");
        C("corwin_lord", "corwin", 40, "male", "military", 5);
        C("drake_lord", "drake", 38, "male", "stewardship", 3);
        C("sato_lord", "sato", 35, "male", "law", 2);
        w.Relationship("marcus", "junia").Bond = BondType.Marriage;
        w.Relationship("junia", "marcus").Bond = BondType.Marriage;
        w.Relationship("corwin_lord", "marcus").Disposition = -20;
        w.Holdings["vega_barony"] = new Holding { Id = "vega_barony", OwnerId = "vega", Name = "Tessaly", Specialization = "agri_world", Population = 20 };
        w.Holdings["corwin_forge"] = new Holding { Id = "corwin_forge", OwnerId = "corwin", Name = "Anvil", Specialization = "forge_world", Population = 14 };
        return w;
    }

    private sealed record Result(string Digest, bool SuccessionHappened, bool CrisesResolved, int MaxPressure);

    private static Result Run(ulong seed, int turns)
    {
        var content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
        var w = Rich(seed);
        var tc = new TurnController(w, content);

        bool succession = false, crisesResolved = false, sawCrisis = false;
        int maxPressure = 0;

        for (int t = 0; t < turns; t++)
        {
            var briefing = tc.BeginTurn();
            foreach (var item in briefing)
            {
                var choice = tc.Offer(item).FirstOrDefault(c => c.Available);
                if (choice != null) tc.Resolve(item, choice.Choice.Id);
            }
            tc.EndTurn();

            // ---- invariants every turn ----
            Assert.True(w.HasFlag("dynasty_dead") || w.Protagonist is { Alive: true },
                $"T{w.Turn}: protagonist is neither alive nor is the dynasty dead.");
            Assert.True(w.House("vega")!.Treasury.Manpower >= 0, $"T{w.Turn}: negative manpower.");
            Assert.True(w.House("vega")!.Treasury.Materials >= 0, $"T{w.Turn}: negative materials.");
            Assert.True(w.Counter("coalition_pressure") <= SphereSystem.MaxCoalitionPressure,
                $"T{w.Turn}: coalition pressure ran away ({w.Counter("coalition_pressure")}).");

            if (w.Counter("generation") >= 1) succession = true;
            if (w.Crises.Count > 0) sawCrisis = true;
            if (sawCrisis && w.Crises.Count == 0) crisesResolved = true;   // a crisis arose and later cleared
            maxPressure = Math.Max(maxPressure, w.Counter("coalition_pressure"));
        }

        var digest = new StringBuilder();
        foreach (var c in w.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            digest.Append(c.Id).Append(':').Append(c.Age).Append(c.Alive ? 'A' : 'D').Append(';');
        digest.Append("prot=").Append(w.ProtagonistId).Append("|rng=").Append(w.RngState);
        return new Result(digest.ToString(), succession, crisesResolved, maxPressure);
    }

    [Fact]
    public void FullStack_RunsManyTurns_Deterministically()
    {
        Assert.Equal(Run(2024, 40).Digest, Run(2024, 40).Digest);
    }

    [Fact]
    public void FullStack_ExercisesSuccession_AndCrisesDeEscalate()
    {
        var r = Run(2024, 40);
        Assert.True(r.SuccessionHappened, "Over 40 years the aged head should die and an heir succeed.");
        Assert.True(r.CrisesResolved, "Crises should de-escalate once their cause eases, not accumulate forever.");
        Assert.True(r.MaxPressure <= SphereSystem.MaxCoalitionPressure);
    }
}
