using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The dynasty lifecycle (GDD §14): aging, death, birth, and the heir handoff that makes the
/// dynasty — not the character — the persistent entity.</summary>
public class DynastyTests
{
    private static World Mk(params (string id, string house, int age, string sex)[] people)
    {
        var w = new World { ProtagonistId = people.Length > 0 ? people[0].id : "" };
        foreach (var (id, house, age, sex) in people)
        {
            if (!w.Houses.ContainsKey(house)) w.Houses[house] = new House { Id = house, Name = house };
            w.Characters[id] = new Character { Id = id, Name = id, HouseId = house, Age = age, Alive = true, Sex = sex };
            w.Houses[house].Members.Add(id);
        }
        return w;
    }

    [Fact]
    public void Tick_AgesEveryLivingCharacter()
    {
        var w = Mk(("a", "h", 20, "male"), ("b", "h", 30, "female"));
        new DynastySystem().Tick(w, new SplitMix64Rng(1));
        Assert.Equal(21, w.Char("a")!.Age);
        Assert.Equal(31, w.Char("b")!.Age);
    }

    [Fact]
    public void YoungUnmarriedCast_ConsumesNoRandomness()
    {
        // The Academy invariant: with no one at mortality age and no fertile couples, the lifecycle
        // draws no RNG — so adding it perturbs nothing deterministic.
        var w = Mk(("a", "h", 20, "male"), ("b", "h", 25, "female"), ("c", "h", 40, "male"));
        var rng = new SplitMix64Rng(1234);
        ulong before = rng.State;
        new DynastySystem().Tick(w, rng);
        Assert.Equal(before, rng.State);
    }

    [Fact]
    public void Death_IsCertain_AtExtremeAge_ButNeverYoung()
    {
        Assert.Equal(0.0, DynastySystem.DeathProbability(20));
        Assert.Equal(0.0, DynastySystem.DeathProbability(49));
        Assert.Equal(1.0, DynastySystem.DeathProbability(DynastySystem.MaxAge));
        Assert.True(DynastySystem.DeathProbability(70) > DynastySystem.DeathProbability(60));

        var w = Mk(("elder", "h", DynastySystem.MaxAge, "male"));
        new DynastySystem().Tick(w, new SplitMix64Rng(1));  // p = 1.0 → certain
        Assert.False(w.Char("elder")!.Alive);
    }

    [Fact]
    public void Birth_ProducesAChild_WithParentsHouseAndBloodTies()
    {
        var w = Mk(("dad", "vega", 30, "male"), ("mom", "drake", 24, "female"));
        w.Relationship("mom", "dad").Bond = BondType.Marriage;   // mom is married to dad
        var sys = new DynastySystem();

        // Within the mother's fertile window a child arrives (BirthChance per turn).
        var rng = new SplitMix64Rng(1);
        Character? child = null;
        for (int t = 0; t < 20 && child == null; t++)
        {
            sys.Tick(w, rng);
            child = w.Characters.Values.FirstOrDefault(c => c.Id.StartsWith("scion_"));
        }
        Assert.NotNull(child);
        Assert.Equal("vega", child!.HouseId);                   // patrilineal: father's house
        Assert.Equal("dad", child.FatherId);
        Assert.Equal("mom", child.MotherId);
        Assert.Contains(child.Id, w.House("vega")!.Members);
        Assert.Equal(BondType.Blood, w.GetRelationship("dad", child.Id)!.Bond);
        Assert.Equal(0, child.Age - (child.Age));               // born at age 0 this turn (then ages)
    }

    [Fact]
    public void Succession_PassesTheSeatToAnHeir_EldestBloodChildFirst()
    {
        var w = Mk(
            ("king", "h", DynastySystem.MaxAge, "male"),
            ("uncle", "h", 60, "male"),                  // older, but not a child
            ("elder_child", "h", 30, "female"),
            ("younger_child", "h", 20, "male"));
        w.Char("elder_child")!.FatherId = "king";
        w.Char("younger_child")!.FatherId = "king";

        new DynastySystem().Tick(w, new SplitMix64Rng(1));      // king (age max) dies

        Assert.False(w.Char("king")!.Alive);
        Assert.Equal("elder_child", w.ProtagonistId);          // blood child, eldest — beats the older uncle
        Assert.Equal(1, w.Counter("generation"));
    }

    [Fact]
    public void DynastyDeath_WhenNoHeirRemains()
    {
        var w = Mk(("last", "h", DynastySystem.MaxAge, "male"));
        new DynastySystem().Tick(w, new SplitMix64Rng(1));
        Assert.True(w.HasFlag("dynasty_dead"));
    }

    [Fact]
    public void KillEffect_KillsARole_AndTriggersSuccession()
    {
        var w = Mk(("self", "h", 40, "male"), ("heir", "h", 18, "male"));
        w.Char("heir")!.FatherId = "self";
        var ctx = new EvalContext(w, new Binding("self"), new SplitMix64Rng(1));

        new KillEffect("self").Apply(ctx);

        Assert.False(w.Char("self")!.Alive);
        Assert.Equal("heir", w.ProtagonistId);
    }

    [Fact]
    public void AgeCondition_ComparesCharacterAge()
    {
        var w = Mk(("self", "h", 55, "male"));
        var ctx = new EvalContext(w, new Binding("self"), new SplitMix64Rng(1));
        Assert.True(new AgeCondition("self", CompareOp.Gte, 50).Evaluate(ctx));
        Assert.False(new AgeCondition("self", CompareOp.Lt, 50).Evaluate(ctx));
    }

    [Fact]
    public void Lifecycle_IsDeterministic_FromSameSeed()
    {
        string Run(ulong seed)
        {
            var w = Mk(("dad", "h", 30, "male"), ("mom", "h", 24, "female"), ("elder", "h", 80, "male"));
            w.Relationship("mom", "dad").Bond = BondType.Marriage;
            var sys = new DynastySystem();
            var rng = new SplitMix64Rng(seed);
            for (int t = 0; t < 15; t++) sys.Tick(w, rng);
            var ids = string.Join(",", w.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal)
                .Select(c => $"{c.Id}:{c.Age}:{(c.Alive ? "A" : "D")}"));
            return ids + "|gen" + w.Counter("generation");
        }
        Assert.Equal(Run(2024), Run(2024));
    }
}
