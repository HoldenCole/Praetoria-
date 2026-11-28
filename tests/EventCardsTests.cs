using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Cards that come back, and cards that come once (docs/EVENTS.md, "Waiting times", "Versions", "Two answers open", "Moments in
/// the year"): a repeatable card waits its years before it comes again to the same head, longer each time, and a new head may meet it at
/// once; a role marked once binds each person once; the record is in the world, so a save keeps it and an older save loads without it; a
/// card is drawn only when two of its answers are open; a text may carry versions of its words, one drawn each time by the world's dice and
/// never the one it last came in; a wedding made in the year is answered in the year.</summary>
public class EventCardsTests
{
    private static EventDef Card(string id, bool repeatable = true, int cooldown = -1, params Choice[] choices) => new()
    {
        Id = id, Repeatable = repeatable, Cooldown = cooldown,
        Choices = choices.Length > 0 ? choices : new[] { new Choice { Id = "a" }, new Choice { Id = "b" } }
    };
    private static World World() { var w = TestContent.ThreeCadetWorld(); w.Pools["self"] = ActionPools.ForPlayer(); return w; }
    private static void Fire(EventEngine engine, World w)
    {
        var f = engine.SelectBriefing(w, 1).Single();
        engine.Resolve(f, w, f.Def.Choices[0].Id);
    }

    // ---------------- waiting times ----------------

    [Fact]
    public void ARepeatableCard_WaitsItsYears_BeforeItComesAgainToTheSameHead()
    {
        var w = World(); var def = Card("rep"); var engine = TestContent.Engine(w, TestContent.Db(new[] { def }));
        Assert.Equal(EventDef.DefaultCooldown, def.WaitYears);
        Fire(engine, w);
        for (int y = 1; y < EventDef.DefaultCooldown; y++) { w.Turn = y; Assert.Empty(engine.GatherEligible(w)); }
        w.Turn = EventDef.DefaultCooldown;
        Assert.Single(engine.GatherEligible(w));
        var seen = w.Cards[CardSeen.Key("rep", "self")];
        Assert.Equal(1, seen.Times); Assert.Equal(0, seen.Turn);
    }

    [Fact]
    public void EachTimeACardComesBack_ItWaitsLongerBeforeTheNext()
    {
        var w = World(); var def = Card("rep", cooldown: 3); var engine = TestContent.Engine(w, TestContent.Db(new[] { def }));
        Fire(engine, w);                       // the first time, at year 0: it waits 3
        w.Turn = 3; Fire(engine, w);           // the second, at 3: it waits 6
        w.Turn = 8; Assert.Empty(engine.GatherEligible(w));
        w.Turn = 9; Assert.Single(engine.GatherEligible(w)); Fire(engine, w);   // the third, at 9: it waits 9
        w.Turn = 17; Assert.Empty(engine.GatherEligible(w));
        w.Turn = 18; Assert.Single(engine.GatherEligible(w));
        Assert.Equal(new[] { 3, 6, 9 }, new[] { 1, 2, 3 }.Select(n => EventEngine.WaitAfter(def, n)));
    }

    [Fact]
    public void ANewHead_CanMeetACardTheOldHeadHadLastYear()
    {
        var w = World(); var engine = TestContent.Engine(w, TestContent.Db(new[] { Card("rep") }));
        Fire(engine, w);
        w.Turn = 1; Assert.Empty(engine.GatherEligible(w));
        w.ProtagonistId = "rivalA"; w.Pools["rivalA"] = ActionPools.ForPlayer();   // the seat has passed
        Assert.Single(engine.GatherEligible(w));
        w.ProtagonistId = "self"; Assert.Empty(engine.GatherEligible(w));            // and the old head's own wait still holds
    }

    [Fact]
    public void AYearsReports_AreOfCardsTheHouseHasNotHad_WhileAnyCanCome()
    {
        var w = World(); var a = Card("old_a"); var b = Card("old_b");
        var engine = TestContent.Engine(w, TestContent.Db(new[] { a, b }));
        var first = engine.SelectBriefing(w, 3);
        Assert.Equal(2, first.Count);                       // two cards, neither had: both come
        w.Turn = EventDef.DefaultCooldown;
        Assert.Single(engine.SelectBriefing(w, 3));        // their wait is out, but both had and nothing new: a quieter year of one
        var c = Card("new_c");
        var engine2 = TestContent.Engine(w, TestContent.Db(new[] { a, b, c }));
        w.Turn = 3 * EventDef.DefaultCooldown;
        Assert.Equal(new[] { "new_c" }, engine2.SelectBriefing(w, 3).Select(f => f.Def.Id));   // a new card comes, and no old one beside it
        // a card whose author named its own wait (a campaign, a censure) is meant to come back, and is not held behind the new
        var w2 = World(); var hook = Card("hook", cooldown: 1); var e3 = TestContent.Engine(w2, TestContent.Db(new[] { hook }));
        e3.SelectBriefing(w2, 3); w2.Turn = 1;
        var e4 = TestContent.Engine(w2, TestContent.Db(new[] { hook, Card("fresh") }));
        Assert.Equal(new[] { "fresh", "hook" }, e4.SelectBriefing(w2, 3).Select(f => f.Def.Id).OrderBy(x => x));
    }

    [Fact]
    public void ACooldownOfNothing_LetsACardComeEveryYear_AndOneThatComesOnce_NeverWaits()
    {
        var w = World(); var every = Card("every", cooldown: 0); var engine = TestContent.Engine(w, TestContent.Db(new[] { every }));
        Fire(engine, w); w.Turn = 1;
        Assert.Single(engine.GatherEligible(w));
        Assert.Equal(0, Card("once", repeatable: false).WaitYears);
        Assert.Equal(0, Card("once", repeatable: false, cooldown: 5).WaitYears);
    }

    [Fact]
    public void TheLoader_ReadsAWaitingTime_AndRefusesOneThatIsNotWholeYears()
    {
        string dir = Path.Combine(Path.GetTempPath(), "praetoria-cards-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string good = Path.Combine(dir, "good.json"), bad = Path.Combine(dir, "bad.json");
            File.WriteAllText(good, """{ "events": [ { "id": "x", "repeatable": true, "cooldown": 4, "moment": true, "roles": [ { "name": "kid", "once": true } ], "choices": [ { "id": "a" }, { "id": "b" } ] } ] }""");
            File.WriteAllText(bad, """{ "events": [ { "id": "y", "repeatable": true, "cooldown": -2, "choices": [ { "id": "a" }, { "id": "b" } ] } ] }""");
            var x = ContentLoader.LoadEventFile(good).Single();
            Assert.Equal(4, x.Cooldown); Assert.Equal(4, x.WaitYears); Assert.True(x.Moment); Assert.True(x.Roles.Single().Once);
            Assert.Throws<ContentException>(() => ContentLoader.LoadEventFile(bad));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---------------- once for each person ----------------

    [Fact]
    public void ARoleMarkedOnce_BindsEachPersonOnce_ForThatEvent()
    {
        var w = World();
        var def = new EventDef
        {
            Id = "milestone", Repeatable = true, Cooldown = 0,
            Roles = new[] { new RoleDef { Name = "kid", Once = true } },
            Choices = new[] { new Choice { Id = "a" }, new Choice { Id = "b" } }
        };
        var engine = TestContent.Engine(w, TestContent.Db(new[] { def }));
        var met = new HashSet<string>();
        for (int y = 0; y < 3; y++)
        {
            w.Turn = y; var all = engine.GatherEligible(w);
            if (y < 2) { var f = engine.SelectBriefing(w, 1).Single(); Assert.True(met.Add(f.Binding.Resolve("kid"))); engine.Resolve(f, w, "a"); }
            else Assert.Empty(all);   // both of the others have had it: nobody is left to bind
        }
        Assert.Equal(2, w.CardsMet.Count);
        Assert.All(w.CardsMet, kv => Assert.StartsWith("milestone|kid|", kv.Key));
    }

    // ---------------- the record is in the world ----------------

    [Fact]
    public void TheRecordOfCards_IsKeptByASave_AndASaveWithoutItLoads()
    {
        var w = World(); var engine = TestContent.Engine(w, TestContent.Db(new[] { Card("rep") }));
        Fire(engine, w); w.CardsMet["x|kid|rivalA"] = 0;
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(1, back.Cards[CardSeen.Key("rep", "self")].Times);
        Assert.Equal(0, back.CardsMet["x|kid|rivalA"]);

        // a save from before the record was kept: no "Cards" and no "CardsMet" in it
        var node = System.Text.Json.Nodes.JsonNode.Parse(SaveSystem.Serialize(w))!;
        node["World"]!.AsObject().Remove("Cards"); node["World"]!.AsObject().Remove("CardsMet");
        var old = SaveSystem.Deserialize(node.ToJsonString());
        Assert.Empty(old.Cards); Assert.Empty(old.CardsMet);
        Assert.Single(TestContent.Engine(old, TestContent.Db(new[] { Card("rep") })).GatherEligible(old));   // as if it had never come
    }

    // ---------------- two answers open ----------------

    [Fact]
    public void ACard_IsDrawnOnlyWhenTwoOfItsAnswersAreOpen()
    {
        var w = World();
        var gated = new Choice { Id = "brave", Requirements = new ICondition[] { new WorldFlagCondition("brave", true) } };
        var def = Card("choice", true, 0, new Choice { Id = "plain" }, gated);
        var held = new List<string>();
        var engine = TestContent.Engine(w, TestContent.Db(new[] { def })); engine.HeldForOneAnswer = (d, _) => held.Add(d.Id);
        Assert.Empty(engine.GatherEligible(w));
        Assert.Equal(new[] { "choice" }, held);
        w.WorldFlags.Add("brave");
        Assert.Single(engine.GatherEligible(w));
    }

    [Fact]
    public void AnAnswerThatCostsMoreThanTheYearsPools_DoesNotCountAsOpen()
    {
        var w = World(); w.Pools["self"].Influence = 1;
        var dear = new Choice { Id = "dear", Cost = new Dictionary<string, int> { [Pool.Influence] = 5 } };
        var engine = TestContent.Engine(w, TestContent.Db(new[] { Card("pricey", true, 0, new Choice { Id = "plain" }, dear) }));
        Assert.Empty(engine.GatherEligible(w));
        w.Pools["self"].Influence = 5;
        Assert.Single(engine.GatherEligible(w));
    }

    [Fact]
    public void TheValidator_RefusesAnEventWithOneAnswer()
    {
        var one = Card("lone", true, 0, new Choice { Id = "a" });
        var text = new EventText { Id = "lone", Title = "t", Body = "b", Choices = new Dictionary<string, string> { ["a"] = "A" } };
        var issues = ContentValidator.Validate(TestContent.Db(new[] { one }, new[] { text }));
        Assert.Contains(issues, i => i.Severity == "error" && i.Where == "lone" && i.Message.Contains("one answer"));
    }

    // ---------------- versions of the words ----------------

    private static EventText Versioned(string id) => new()
    {
        Id = id, Title = "One", Body = "First words.", Choices = new Dictionary<string, string> { ["a"] = "A", ["b"] = "B" },
        Versions = new[]
        {
            new TextVersion { Body = "Second words.", Choices = new Dictionary<string, string> { ["a"] = "A again" } },
            new TextVersion { Title = "Three", Body = "Third words." }
        }
    };

    [Fact]
    public void AVersionReadsItsOwnWords_AndTheTextsOwnWhereItHasNone()
    {
        var t = Versioned("v");
        Assert.Equal(3, t.VersionCount);
        Assert.Equal("First words.", t.BodyOf(0)); Assert.Equal("Second words.", t.BodyOf(1)); Assert.Equal("Third words.", t.BodyOf(2));
        Assert.Equal("A again", t.ChoiceOf("a", 1)); Assert.Equal("B", t.ChoiceOf("b", 1)); Assert.Equal("A", t.ChoiceOf("a", 2));
        Assert.Equal("One", t.TitleOf(1)); Assert.Equal("Three", t.TitleOf(2)); Assert.Equal("First words.", t.BodyOf(7));
    }

    [Fact]
    public void ACardThatComesBack_IsDrawnInOtherWordsThanLastTime_ByTheWorldsDice()
    {
        var def = Card("v", cooldown: 0);
        var db = TestContent.Db(new[] { def }, new[] { Versioned("v") });
        var w = World(); var engine = TestContent.Engine(w, db);
        int last = -1; var drawn = new List<int>();
        for (int y = 0; y < 12; y++)
        {
            w.Turn = y; var f = engine.SelectBriefing(w, 1).Single();
            Assert.NotEqual(last, f.Version); Assert.InRange(f.Version, 0, 2);
            Assert.Equal(f.Version, w.Cards[CardSeen.Key("v", "self")].Version);
            last = f.Version; drawn.Add(f.Version); engine.Resolve(f, w, "a");
        }
        Assert.Equal(3, drawn.Distinct().Count());
        // the same world and the same dice draw the same words
        var w2 = World(); var e2 = TestContent.Engine(w2, db); var again = new List<int>();
        for (int y = 0; y < 12; y++) { w2.Turn = y; var f = e2.SelectBriefing(w2, 1).Single(); again.Add(f.Version); e2.Resolve(f, w2, "a"); }
        Assert.Equal(drawn, again);
    }

    [Fact]
    public void TheValidator_ChecksEveryVersion()
    {
        var def = Card("v", cooldown: 0);
        var bad = new EventText
        {
            Id = "v", Title = "t", Body = "b", Choices = new Dictionary<string, string> { ["a"] = "A", ["b"] = "B" },
            Versions = new[] { new TextVersion { Body = "{nobody.name} came.", Choices = new Dictionary<string, string> { ["c"] = "C" } }, new TextVersion { Body = "" } }
        };
        var issues = ContentValidator.Validate(TestContent.Db(new[] { def }, new[] { bad }));
        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("undeclared role 'nobody'"));
        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("names a choice the event does not have"));
        Assert.Contains(issues, i => i.Severity == "error" && i.Message.Contains("a version has no body"));
    }

    [Fact]
    public void AReportSavedInTheMiddleOfTheYear_KeepsTheVersionItWasDrawnIn()
    {
        var w = World(); var def = Card("v", cooldown: 0);
        var db = TestContent.Db(new[] { def }, new[] { Versioned("v") });
        var tc = new TurnController(w, db, briefingBudget: 1);
        tc.BeginTurn(); int version = tc.Briefing.Single().Fired.Version;
        var loaded = SaveSystem.Deserialize(tc.SaveMidYear());
        var tc2 = new TurnController(loaded, db, briefingBudget: 1);
        Assert.Equal(version, tc2.Resume().Single().Fired.Version);
    }

    // ---------------- the office a role is named for ----------------

    [Fact]
    public void TheSovereign_IsTheHeadOfTheHouseOnTheThrone_AndTheHeadOfAHouseIsItsHead()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_imperial_court"), 3);
        var throne = ThroneSystem.Emperor(w)!; var emperor = Economy.HeadOf(w, throne)!;
        Assert.True(OfficeCondition.Holds(w, emperor, "sovereign"));
        Assert.True(OfficeCondition.Holds(w, emperor, "head"));
        var kin = w.LivingCharacters().FirstOrDefault(c => c.HouseId == throne.Id && c.Id != emperor.Id && c.Age >= 16);
        if (kin != null) { Assert.False(OfficeCondition.Holds(w, kin, "sovereign")); Assert.False(OfficeCondition.Holds(w, kin, "head")); }
        Assert.False(OfficeCondition.Holds(w, w.Protagonist!, "sovereign"));
    }

    // ---------------- the ambition someone has outgrown ----------------

    [Fact]
    public void AnAdmiral_NoLongerWantsToEarnACommand_AndAnotherHousesCommanderHasTheOneTheyWanted()
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_risen_officer"), 2);
        var me = w.Protagonist!; me.Ambition = "earn_a_command"; me.CareerTrack = "military"; me.CareerRank = 6;
        var other = w.LivingCharacters().First(c => c.HouseId != me.HouseId && c.Age >= 20);
        other.Ambition = "earn_a_command";
        var fleet = w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal).First(f => f.OwnerId != me.HouseId); fleet.AdmiralId = other.Id;
        var junior = w.LivingCharacters().First(c => c.HouseId != me.HouseId && c.Id != other.Id && c.Age >= 20 && w.Fleets.Values.All(f => f.AdmiralId != c.Id));
        junior.Ambition = "earn_a_command"; junior.CareerTrack = "military"; junior.CareerRank = 2;
        AmbitionSystem.Outgrow(w, content.Careers);
        Assert.Equal("command_a_fleet", me.Ambition);
        Assert.Contains(w.NewsNow(), n => n.Text.Contains("fleet of your own"));
        Assert.Equal("outshine_all_rivals", other.Ambition);
        Assert.Equal("earn_a_command", junior.Ambition);   // a Lieutenant with no command still wants one
    }
}
