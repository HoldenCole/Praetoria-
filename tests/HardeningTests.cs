using System.Text;
using System.Text.Json;
using Praetoria.Core.Crises;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>
/// Second hardening pass. Covers the seams the gap analysis found: career events had no way to gate
/// on the track they belong to; a marriage authored from one spouse was invisible to the birth system;
/// crises could never be one-time and could not un-arm what they armed; and there was no save/load.
/// </summary>
public class HardeningTests
{
    private static (ContentDatabase content, string root) Load()
    {
        var root = ContentLocator.FindContentDir();
        return (ContentLoader.LoadFromDirectory(root), root);
    }

    // ---- `track` condition (GDD §13: three tracks are three institutions) ----

    [Fact]
    public void TrackCondition_HoldsOnlyForTheNamedTrack()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Char("self")!.CareerTrack = "law";
        var ctx = new EvalContext(w, new Binding("self"), TestContent.Rng());

        Assert.True(new TrackCondition("self", "law", present: true).Evaluate(ctx));
        Assert.False(new TrackCondition("self", "military", present: true).Evaluate(ctx));
        Assert.True(new TrackCondition("self", "military", present: false).Evaluate(ctx));
    }

    [Fact]
    public void TrackCondition_ParsesFromJson_WithSelfAsDefaultRole()
    {
        using var doc = JsonDocument.Parse("""{ "type": "track", "track": "stewardship" }""");
        var cond = Assert.IsType<TrackCondition>(ConditionParser.Parse(doc.RootElement));
        Assert.Equal("self", cond.Role);
        Assert.Equal("stewardship", cond.Track);
        Assert.True(cond.Present);
    }

    /// <summary>The content-level consequence: a magistrate never receives a fleet's first command.</summary>
    [Fact]
    public void AuthoredCareerEvents_DoNotFireForTheWrongTrack()
    {
        var (content, root) = Load();
        var world = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_circling_heir"), 1);
        var lucia = world.Protagonist!;
        Assert.Equal("law", lucia.CareerTrack);
        lucia.Skills["tactics"] = 2;            // satisfy first_command's skill gate on purpose
        lucia.Skills["administration"] = 2;     // ...and the governor's seat's
        world.Turn = 5;

        var engine = new EventEngine(content, WorldBuilder.RngFor(world));
        var eligible = engine.GatherEligible(world).Select(e => e.def.Id).ToHashSet();

        Assert.DoesNotContain("first_command", eligible);        // military arc
        Assert.DoesNotContain("the_governors_seat", eligible);   // stewardship arc

        lucia.CareerTrack = "military";
        eligible = engine.GatherEligible(world).Select(e => e.def.Id).ToHashSet();
        Assert.Contains("first_command", eligible);              // same world, right track → fires
    }

    // ---- marriage is mutual; births need a mother and a father ----

    [Fact]
    public void AddBond_Marriage_IsWrittenOnBothEdges_ButSwornStaysDirected()
    {
        var w = TestContent.ThreeCadetWorld();
        var ctx = new EvalContext(w, new Binding("self").With("other", "rivalA"), TestContent.Rng());

        new AddBondEffect("self", "other", BondType.Marriage, 60).Apply(ctx);
        Assert.Equal(BondType.Marriage, w.GetRelationship("self", "rivalA")!.Bond);
        Assert.Equal(BondType.Marriage, w.GetRelationship("rivalA", "self")!.Bond);

        var w2 = TestContent.ThreeCadetWorld();
        var ctx2 = new EvalContext(w2, new Binding("self").With("other", "rivalA"), TestContent.Rng());
        new AddBondEffect("self", "other", BondType.Sworn, 60).Apply(ctx2);
        Assert.Equal(BondType.Sworn, w2.GetRelationship("self", "rivalA")!.Bond);
        Assert.Null(w2.GetRelationship("rivalA", "self"));
    }

    [Fact]
    public void Births_HappenWhenTheMarriageIsRecordedOnlyOnTheHusbandsEdge()
    {
        // The authored match event writes self→bride; before this pass the birth roll only looked at
        // edges leaving the mother, so such a couple could never have children.
        var w = new World { ProtagonistId = "husband" };
        w.Houses["h"] = new House { Id = "h", Name = "h" };
        w.Characters["husband"] = new Character { Id = "husband", Name = "husband", HouseId = "h", Age = 28, Sex = "male" };
        w.Characters["wife"] = new Character { Id = "wife", Name = "wife", HouseId = "h", Age = 24, Sex = "female" };
        w.Houses["h"].Members.AddRange(new[] { "husband", "wife" });
        w.Relationship("husband", "wife").Bond = BondType.Marriage;   // one direction only

        var sys = new DynastySystem();
        var rng = new SplitMix64Rng(1);
        for (int t = 0; t < 20; t++) sys.Tick(w, rng);

        Assert.Contains(w.Characters.Values, c => c.Id.StartsWith("scion_"));
    }

    [Fact]
    public void Births_RequireAFather_SoASameSexMarriageDrawsNoRandomness()
    {
        var w = new World { ProtagonistId = "a" };
        w.Houses["h"] = new House { Id = "h", Name = "h" };
        w.Characters["a"] = new Character { Id = "a", Name = "a", HouseId = "h", Age = 28, Sex = "female" };
        w.Characters["b"] = new Character { Id = "b", Name = "b", HouseId = "h", Age = 24, Sex = "female" };
        w.Relationship("a", "b").Bond = BondType.Marriage;
        w.Relationship("b", "a").Bond = BondType.Marriage;

        var rng = new SplitMix64Rng(7);
        ulong before = rng.State;
        new DynastySystem().Tick(w, rng);

        Assert.Equal(before, rng.State);
        Assert.DoesNotContain(w.Characters.Values, c => c.Id.StartsWith("scion_"));
    }

    // ---- crises: `once` + `onResolve` ----

    private static CrisisDef Crisis(string id, bool once, IEffect[]? onResolve = null) => new()
    {
        Id = id, Name = id, Severity = 1, Once = once,
        Gates = new ICondition[] { new ConstCondition(true) },
        OnTrigger = new IEffect[] { new SetWorldFlagEffect("armed", true) },
        OnResolve = onResolve ?? Array.Empty<IEffect>()
    };

    [Fact]
    public void OnceCrisis_CannotRecur_AfterItResolves_ButARecurringOneCan()
    {
        var w = TestContent.ThreeCadetWorld();
        var rng = TestContent.Rng();
        var once = Crisis("one_time", once: true);
        var again = Crisis("recurring", once: false);
        var engine = new CrisisEngine(new[] { once, again });

        foreach (var def in new[] { once, again })
        {
            Assert.True(engine.IsCausable(def, w, rng));
            engine.Trigger(def, w, rng);
            Assert.False(engine.IsCausable(def, w, rng));          // active, not repeatable
            engine.Resolve(def, w, rng);
        }

        Assert.False(engine.IsCausable(once, w, rng));             // spent for the run
        Assert.True(w.HasFlag(World.CrisisSpentFlag("one_time")));
        Assert.True(engine.IsCausable(again, w, rng));             // free to recur
    }

    [Fact]
    public void OnResolve_UnArmsWhatOnsetArmed()
    {
        var w = TestContent.ThreeCadetWorld();
        var rng = TestContent.Rng();
        var def = Crisis("storm", once: false, new IEffect[] { new SetWorldFlagEffect("armed", false) });
        var engine = new CrisisEngine(new[] { def });

        engine.Trigger(def, w, rng);
        Assert.True(w.HasFlag("armed"));

        engine.ApplyDamper(def, new DamperDef { Id = "d", Relief = 5 }, w, rng);   // drives severity ≤ 0
        Assert.False(w.IsCrisisActive("storm"));
        Assert.False(w.HasFlag("armed"));
    }

    [Fact]
    public void AuthoredCivilWar_CoolsTheRealm_WhenItSubsides()
    {
        var (content, _) = Load();
        var engine = new CrisisEngine(content.Crises);
        var w = TestContent.EconomyWorld();
        var rng = TestContent.Rng();
        var cw = engine.Def("civil_war")!;

        engine.Trigger(cw, w, rng);
        Assert.True(w.HasFlag("civil_war_active"));

        // Its gates don't hold in this world, so natural de-escalation winds it down and resolves it.
        for (int i = 0; i < 10 && w.IsCrisisActive("civil_war"); i++) engine.Decay(w, rng);
        Assert.False(w.IsCrisisActive("civil_war"));
        Assert.False(w.HasFlag("civil_war_active"));
        Assert.False(w.HasFlag("realm_unstable"));
    }

    // ---- save / load ----

    private static string Digest(World w)
    {
        var sb = new StringBuilder();
        foreach (var c in w.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            sb.Append(c.Id).Append(':').Append(c.Age).Append(c.Alive ? 'A' : 'D').Append(':').Append(c.CareerRank)
              .Append(':').Append(string.Join('/', c.Flags.OrderBy(f => f, StringComparer.Ordinal))).Append(';');
        foreach (var kv in w.WorldCounters.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
        sb.Append(string.Join(',', w.WorldFlags.OrderBy(f => f, StringComparer.Ordinal)));
        foreach (var h in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
            sb.Append(h.Id).Append(':').Append(h.Title).Append(':').Append(h.Legitimacy).Append(':').Append(h.Treasury.Credits).Append(';');
        sb.Append("|prot=").Append(w.ProtagonistId).Append("|turn=").Append(w.Turn)
          .Append("|rng=").Append(w.RngState).Append("|hist=").Append(w.History.Count);
        return sb.ToString();
    }

    private static void Auto(TurnController tc, int turns)
    {
        for (int t = 0; t < turns; t++)
        {
            foreach (var item in tc.BeginTurn())
            {
                var choice = tc.Offer(item).FirstOrDefault(c => c.Available);
                if (choice != null) tc.Resolve(item, choice.Choice.Id);
            }
            tc.EndTurn();
        }
    }

    [Fact]
    public void Save_RoundTrips_TheWholeWorld_AndTheRunContinuesIdentically()
    {
        var (content, root) = Load();
        var scenario = ScenarioLoader.LoadFromContent(root, "academy_crucible");

        // Reference run: 10 straight turns.
        var reference = WorldBuilder.FromScenario(scenario, 99);
        Auto(new TurnController(reference, content), 10);

        // Saved run: 5 turns, serialise, deserialise, 5 more on the loaded World.
        var live = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "academy_crucible"), 99);
        var tc = new TurnController(live, content);
        Auto(tc, 5);
        tc.SyncRng();
        string json = SaveSystem.Serialize(live);

        Assert.DoesNotContain("\"Protagonist\"", json);          // computed lookups aren't state
        Assert.Contains("\"RngState\"", json);                     // the RNG position is

        var loaded = SaveSystem.Deserialize(json);
        Assert.Equal(Digest(live), Digest(loaded));                // lossless
        Assert.Equal(BondType.Marriage.ToString(), "Marriage");    // enum names (not ints) in the file

        Auto(new TurnController(loaded, content), 5);
        Assert.Equal(Digest(reference), Digest(loaded));           // resumed run == uninterrupted run
    }

    [Fact]
    public void Save_RejectsForeignOrFutureFiles()
    {
        Assert.Throws<ContentException>(() => SaveSystem.Deserialize("{ \"Game\": \"other\", \"Version\": 1, \"World\": {} }"));
        Assert.Throws<ContentException>(() => SaveSystem.Deserialize("{ \"Game\": \"praetoria\", \"Version\": 999, \"World\": {} }"));
        Assert.Throws<ContentException>(() => SaveSystem.Deserialize("not json"));
    }

    [Fact]
    public void DynastyDeath_IsReadableOffTheWorld()
    {
        var w = new World { ProtagonistId = "last" };
        w.Houses["h"] = new House { Id = "h", Name = "h" };
        w.Characters["last"] = new Character { Id = "last", Name = "last", HouseId = "h", Age = 40 };
        Assert.False(w.IsDynastyDead);
        DynastySystem.Die(w, "last");
        Assert.True(w.IsDynastyDead);
    }
}

public class CareerLadderTests
{
    private static (ContentDatabase content, string root) Load()
    {
        var root = ContentLocator.FindContentDir();
        return (ContentLoader.LoadFromDirectory(root), root);
    }

    [Fact]
    public void CareerCatalog_LoadsTheThreeLockedLadders()
    {
        var (content, _) = Load();
        Assert.Equal(3, content.Careers.Defs.Count);
        Assert.Equal(6, content.Careers.MaxRank("military"));        // Cadet … Grand Admiral
        Assert.Equal(4, content.Careers.MaxRank("stewardship"));
        Assert.Equal(4, content.Careers.MaxRank("law"));
        Assert.Equal("Lieutenant", content.Careers.RankName("military", 2));
        Assert.Null(content.Careers.MaxRank("spymaster"));           // not a V1 track → uncapped
    }

    [Fact]
    public void AdvanceCareer_StopsAtTheTopOfTheLadder_AndIsUncappedWithoutACatalog()
    {
        var (content, _) = Load();
        var w = TestContent.ThreeCadetWorld();
        var self = w.Char("self")!;
        self.CareerTrack = "stewardship";
        self.CareerRank = 4;                                          // Imperial Treasurer — the top

        new AdvanceCareerEffect("self").Apply(new EvalContext(w, new Binding("self"), TestContent.Rng(), content));
        Assert.Equal(4, self.CareerRank);

        new AdvanceCareerEffect("self").Apply(new EvalContext(w, new Binding("self"), TestContent.Rng()));
        Assert.Equal(5, self.CareerRank);                             // no catalog → the old bare-int behaviour
    }

    [Fact]
    public void RankNameToken_RendersTheRung_OrFallsBackToTheNumber()
    {
        var (content, _) = Load();
        var w = TestContent.ThreeCadetWorld();
        w.Char("self")!.CareerTrack = "law";
        w.Char("self")!.CareerRank = 3;

        var withCatalog = new EvalContext(w, new Binding("self"), TestContent.Rng(), content);
        Assert.Equal("Praetor (3)", TextRenderer.Substitute("{self.rankName} ({self.rank})", withCatalog));

        var bare = new EvalContext(w, new Binding("self"), TestContent.Rng());
        Assert.Equal("3", TextRenderer.Substitute("{self.rankName}", bare));
    }

    [Fact]
    public void Validator_RejectsAnEventWhoseEveryChoiceIsGated()
    {
        var gated = new EventDef
        {
            Id = "locked", Choices = new[]
            {
                new Choice { Id = "a", Requirements = new ICondition[] { new ConstCondition(false) } },
                new Choice { Id = "b", Requirements = new ICondition[] { new ConstCondition(false) } }
            }
        };
        var text = new EventText { Id = "locked", Title = "t", Body = "b", Choices = new Dictionary<string, string> { ["a"] = "A", ["b"] = "B" } };
        var issues = ContentValidator.Validate(TestContent.Db(new[] { gated }, new[] { text }));
        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("every choice is gated"));
    }
}

public class SuccessionEffectTests
{
    [Fact]
    public void RevokeClaim_RemovesTheHousesClaim()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Houses["h"].Claims.Add("count");
        new RevokeClaimEffect("self", "count").Apply(new EvalContext(w, new Binding("self"), TestContent.Rng()));
        Assert.DoesNotContain("count", w.Houses["h"].Claims);
    }

    [Fact]
    public void Succeed_HandsTheSeatToALivingHeir_WithoutADeath()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Pools["self"] = ActionPools.ForPlayer();
        w.Pools["rivalA"] = ActionPools.ForNpc();
        var ctx = new EvalContext(w, new Binding("self").With("heir", "rivalA"), TestContent.Rng());

        new SucceedEffect("heir").Apply(ctx);

        Assert.Equal("rivalA", w.ProtagonistId);
        Assert.True(w.Char("self")!.Alive);                           // an abdication, not a funeral
        Assert.Equal(ActionPools.ForPlayer().InfluenceRegen, w.Pools["rivalA"].InfluenceRegen);
        Assert.Equal(1, w.Counter("generation"));
        Assert.Contains(w.History, h => h.Text.Contains("yields the seat"));
    }

    [Fact]
    public void Succeed_IgnoresTheDeadAndTheCurrentHead()
    {
        var w = TestContent.ThreeCadetWorld();
        w.Char("rivalA")!.Alive = false;
        var ctx = new EvalContext(w, new Binding("self").With("heir", "rivalA"), TestContent.Rng());
        new SucceedEffect("heir").Apply(ctx);
        Assert.Equal("self", w.ProtagonistId);
        new SucceedEffect("self").Apply(ctx);
        Assert.Equal(0, w.Counter("generation"));
    }
}
