using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.Spheres;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The power-balance system (GDD §7): sphere influence derived from careers, the threat
/// score it produces, and the coalition pressure an over-mighty house draws — which arms the §16
/// coalition crisis.</summary>
public class SphereTests
{
    private static SphereCatalog Cat() => new(new[]
    {
        new SphereDef { Id = "navy", Name = "Navy", CareerTrack = "military" },
        new SphereDef { Id = "treasury", Name = "Treasury", CareerTrack = "stewardship" },
        new SphereDef { Id = "senate", Name = "Senate", CareerTrack = "law" }
    });

    /// <summary>Four houses; House Vega's heir is a rank-5 admiral — over-mighty in the Navy.</summary>
    private static World PowerWorld()
    {
        var w = new World { ProtagonistId = "marcus" };
        foreach (var id in new[] { "vega", "corwin", "drake", "sato" })
            w.Houses[id] = new House { Id = id, Name = id };

        void Add(string id, string house, string track, int rank)
        {
            w.Characters[id] = new Character { Id = id, Name = id, HouseId = house, Alive = true, CareerTrack = track, CareerRank = rank };
            w.Houses[house].Members.Add(id);
        }
        Add("marcus", "vega", "military", 5);      // dominant Navy
        Add("lucan", "corwin", "military", 1);     // minor Navy
        Add("sela", "drake", "law", 2);            // Senate
        Add("ren", "sato", "stewardship", 2);      // Treasury
        return w;
    }

    [Fact]
    public void Recompute_DerivesSphereInfluence_FromMemberCareers()
    {
        var w = PowerWorld();
        new SphereSystem(Cat()).Recompute(w);

        Assert.Equal(5, w.House("vega")!.SphereInfluence["navy"]);
        Assert.Equal(0, w.House("vega")!.SphereInfluence["treasury"]);
        Assert.Equal(1, w.House("corwin")!.SphereInfluence["navy"]);
        Assert.Equal(2, w.House("drake")!.SphereInfluence["senate"]);
        Assert.Equal(2, w.House("sato")!.SphereInfluence["treasury"]);
    }

    [Fact]
    public void Threat_IsHigh_ForAnOverMightyHouse_AndLowerForRivals()
    {
        var w = PowerWorld();
        var s = new SphereSystem(Cat());
        s.Recompute(w);

        Assert.True(s.Threat(w, "vega") >= SphereSystem.CoalitionThreshold, "An 83%-Navy house should look threatening.");
        Assert.True(s.Threat(w, "corwin") < s.Threat(w, "vega"));
    }

    [Fact]
    public void BalancedPower_ProducesLowThreat_AndNoCoalition()
    {
        var w = new World { ProtagonistId = "a1" };
        foreach (var id in new[] { "a", "b", "c", "d" })
            w.Houses[id] = new House { Id = id, Name = id };
        // One rank-2 admiral per house: every house holds an equal 25% Navy share.
        foreach (var (cid, hid) in new[] { ("a1", "a"), ("b1", "b"), ("c1", "c"), ("d1", "d") })
        {
            w.Characters[cid] = new Character { Id = cid, Name = cid, HouseId = hid, Alive = true, CareerTrack = "military", CareerRank = 2 };
            w.Houses[hid].Members.Add(cid);
        }
        var s = new SphereSystem(Cat());
        s.Recompute(w);

        Assert.Equal(0, s.Threat(w, "a"));
        Assert.False(w.HasFlag("coalition_forming"));
        Assert.Equal(0, w.Counter("coalition_pressure"));
    }

    [Fact]
    public void CoalitionPressure_Accumulates_AndArmsTheCoalitionCrisis()
    {
        var w = PowerWorld();
        var s = new SphereSystem(Cat());
        for (int t = 0; t < 3; t++) s.Recompute(w);   // three over-mighty turns

        Assert.True(w.Counter("coalition_pressure") >= 3, "Sustained dominance should accrue coalition pressure.");
        Assert.True(w.HasFlag("coalition_forming"));

        // The authored coalition crisis gates on exactly that pressure — spheres now drive §16.
        var content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
        var crises = new CrisisEngine(content.Crises);
        Assert.True(crises.IsCausable(crises.Def("coalition_war")!, w, new SplitMix64Rng(1)));
    }

    [Fact]
    public void CoalitionPressure_Eases_WhenThreatFalls()
    {
        var w = PowerWorld();
        var s = new SphereSystem(Cat());
        s.Recompute(w); s.Recompute(w);
        int peak = w.Counter("coalition_pressure");
        Assert.True(peak > 0);

        // The admiral dies; House Vega's Navy dominance collapses.
        w.Char("marcus")!.Alive = false;
        s.Recompute(w);
        Assert.True(w.Counter("coalition_pressure") < peak, "Pressure should ease once the house is no longer over-mighty.");
    }

    [Fact]
    public void Bridge_FeedsCareerInfluence_IntoPlayerCounters_WithoutClobberingCultivation()
    {
        var w = PowerWorld();                       // Marcus (Vega) is a rank-5 admiral → structural navy 5
        var s = new SphereSystem(Cat());
        s.Recompute(w);
        Assert.Equal(5, w.Counter("navy_influence"));   // career-derived structural mirrored to the event counter

        // The player cultivates +2 navy through coalition events.
        w.WorldCounters["navy_influence"] += 2;
        s.Recompute(w);                                 // structural unchanged → no delta
        Assert.Equal(7, w.Counter("navy_influence"));   // cultivation preserved, not clobbered

        // The admiral is promoted: structural 5 → 6 applies a +1 delta on top of the cultivation.
        w.Char("marcus")!.CareerRank = 6;
        s.Recompute(w);
        Assert.Equal(8, w.Counter("navy_influence"));
    }

    [Fact]
    public void SphereCondition_ReadsHouseInfluence_AndParsesFromJson()
    {
        var w = PowerWorld();
        new SphereSystem(Cat()).Recompute(w);
        var ctx = new EvalContext(w, new Binding("marcus"), new SplitMix64Rng(1));

        Assert.True(new SphereCondition("self", "navy", CompareOp.Gte, 5).Evaluate(ctx));
        Assert.False(new SphereCondition("self", "treasury", CompareOp.Gt, 0).Evaluate(ctx));

        var parsed = ConditionParser.Parse(System.Text.Json.JsonDocument.Parse(
            """{ "type": "sphere", "sphere": "navy", "op": "gte", "value": 5 }""").RootElement);
        Assert.True(parsed.Evaluate(ctx));
    }
}
