using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Local law: a law for one world (docs/POLITICS.md, "Local law: a law for one world"). Each local law falls on the world
/// that keeps it and on no other world of its house; a world carries only so many; only the head of the house that holds the world
/// may decree, at a cost in influence, and a decree is settled for some years; the estates feel it by the share of the house's
/// worlds; a world that changes hands keeps its laws; the laws are saved; and the other houses decree too.</summary>
public class LocalLawTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static PolicyCatalog Cat => Content.Policies;
    private static CommandContext Ctx(World w) => new(w, TestContent.Rng());
    private static Economy Eco() => new(TestContent.EconomyCatalog(), BusinessCatalog.Empty, Cat);
    private static BusinessCatalog Ventures() => new(new[]
    {
        new BusinessDef { Id = "trading_company", Name = "Trading Company", Cost = new Resources { Credits = 4 }, Yield = new Resources { Credits = 4 }, MaxLevel = 3, Charter = 1, RequiresPopulated = true }
    });

    /// <summary>House Vega (the player's) holds two peopled worlds in the rim, the Barony and Outer; House Corwin holds Ember Hold in the core.</summary>
    private static World Reach()
    {
        var w = TestContent.EconomyWorld(credits: 30, materials: 10);
        w.Houses["corwin"] = new House { Id = "corwin", Name = "House Corwin", Treasury = new Resources { Credits = 30, Materials = 5 } };
        w.Characters["corwin_head"] = new Character { Id = "corwin_head", Name = "Lucan", HouseId = "corwin", Age = 40, Alive = true };
        w.Houses["corwin"].Members.Add("corwin_head"); w.Pools["corwin_head"] = ActionPools.ForNpc();
        var g = new Galaxy { Id = "g", Name = "G" };
        g.Regions.Add(new Region { Id = "rim", Name = "The Rim", Ring = Ring.Rim }); g.Regions.Add(new Region { Id = "core", Name = "The Core", Ring = Ring.Core });
        g.Systems["tessaly"] = new StarSystem { Id = "tessaly", Name = "Tessaly", RegionId = "rim", PlanetClass = "terran" };
        g.Systems["outer"] = new StarSystem { Id = "outer", Name = "Outer", RegionId = "rim", PlanetClass = "terran" };
        g.Systems["ember"] = new StarSystem { Id = "ember", Name = "Ember", RegionId = "core", PlanetClass = "terran" };
        g.Lanes.Add(new Hyperlane { A = "tessaly", B = "outer" }); g.Lanes.Add(new Hyperlane { A = "outer", B = "ember" });
        w.Galaxy = g;
        var barony = w.Holding("barony")!; barony.SystemId = "tessaly"; barony.GovernorId = "self";
        w.Holdings["outer"] = new Holding { Id = "outer", OwnerId = "vega", Name = "Outer", Specialization = "agri", SystemId = "outer", Population = 20, GovernorId = "self" };
        w.Holdings["corwin_world"] = new Holding { Id = "corwin_world", OwnerId = "corwin", Name = "Ember Hold", Specialization = "agri", SystemId = "ember", Population = 20, GovernorId = "corwin_head" };
        LocalLawSystem.Use(w, Cat);
        return w;
    }
    private static Resources Line(Economy eco, World w, string houseId, string world) => eco.Compute(w, houseId).Lines.Single(l => l.Source == world).Delta;
    private static string Same(Resources r) => string.Join(",", Resource.All.Select(k => r.Get(k)));

    public static IEnumerable<object[]> Options() => Cat.Local.Laws.SelectMany(l => l.Options.Where(o => !o.Default).Select(o => new object[] { l.Id, o.Id }));

    [Fact]
    public void There_are_eight_local_laws_each_a_real_choice_drawn_from_real_law()
    {
        Assert.Equal(8, Cat.Local.Laws.Count);
        foreach (var law in Cat.Local.Laws)
        {
            Assert.Single(law.Options, o => o.Default); Assert.InRange(law.Options.Count, 2, 3);
            Assert.False(string.IsNullOrWhiteSpace(law.Model), law.Id + " names no real law"); Assert.False(string.IsNullOrWhiteSpace(law.Description));
            Assert.True(law.Cost >= 1 && law.Cooldown >= 1);
            foreach (var text in new[] { law.Name, law.Description, law.Model }.Concat(law.Options.SelectMany(o => new[] { o.Name, o.Short })))
            { Assert.DoesNotContain("—", text); Assert.DoesNotContain("!", text); Assert.DoesNotContain("(s)", text); }
            foreach (var o in law.Options.Where(o => !o.Default))
            {
                Assert.True(o.Opinions.Values.Any(v => v < 0), $"{law.Id}:{o.Id}: nobody minds it");
                Assert.True(o.Effects.Values.Any(v => v != 0), $"{law.Id}:{o.Id}: it does nothing");
                // it gives something and costs something on the world (the closed gates cost a venture's founding, the rest a number)
                var e = o.Effects;
                Assert.True(e.Any(kv => kv.Value != 0 && (kv.Key == "unrest_drift" || kv.Key == "lawlessness_drift" ? kv.Value < 0 : kv.Value > 0)), $"{law.Id}:{o.Id} gives nothing");
                Assert.True(e.Any(kv => kv.Value != 0 && (kv.Key == "unrest_drift" || kv.Key == "lawlessness_drift" ? kv.Value > 0 : kv.Key == LocalLawSystem.ClosedKey ? true : kv.Value < 0)), $"{law.Id}:{o.Id} costs nothing");
            }
        }
    }

    [Fact]
    public void Every_option_off_the_custom_has_an_effect_that_a_rule_reads()
    {
        foreach (var law in Cat.Local.Laws) foreach (var o in law.Options.Where(o => !o.Default))
        {
            Assert.Contains(o.Effects.Keys, k => LocalLawSystem.Read.Contains(k));
            foreach (var k in o.Effects.Keys) Assert.True(LocalLawSystem.Read.Contains(k), $"{law.Id}:{o.Id} names {k}, which no rule reads for one world");
            foreach (var k in o.Opinions.Keys) Assert.NotNull(Cat.Estate(k));
        }
        // every key the rules read is named by some option, and each is shown moving a number below
        foreach (var k in LocalLawSystem.Read) Assert.Contains(Cat.Local.Laws.SelectMany(l => l.Options), o => o.Effects.ContainsKey(k));
    }

    [Theory, MemberData(nameof(Options))]
    public void A_local_law_falls_on_its_own_world_and_on_no_other_world_of_the_house(string lawId, string optionId)
    {
        var o = Cat.Local.Law(lawId)!.Option(optionId)!; var eco = Eco(); var with = Reach(); var without = Reach();
        LocalLawSystem.Set(with, Cat, with.Holding("barony")!, lawId, optionId);
        int E(string key) => o.Effects.GetValueOrDefault(key);

        // the ledger: one added term on the world's own line
        var a = Line(eco, with, "vega", "Barony"); var b = Line(eco, without, "vega", "Barony");
        Assert.Equal(b.Credits + E("credits_per_world"), a.Credits); Assert.Equal(b.Materials + E("materials_per_world"), a.Materials); Assert.Equal(b.Manpower + E("manpower_per_world"), a.Manpower);
        Assert.Equal(Same(Line(eco, without, "vega", "Outer")), Same(Line(eco, with, "vega", "Outer")));
        Assert.Equal(Same(Line(eco, without, "corwin", "Ember Hold")), Same(Line(eco, with, "corwin", "Ember Hold")));

        // the lists a player reads name the law, in words, on its world alone
        var barony = with.Holding("barony")!; var outer = with.Holding("outer")!;
        if (E("unrest_drift") != 0) Assert.Contains(("local law: " + o.Short, E("unrest_drift")), eco.UnrestWhy(with, barony));
        if (E("prosperity_drift") != 0) Assert.Contains(("local law: " + o.Short, 2 * E("prosperity_drift")), eco.ProsperityWhy(with, barony));
        Assert.DoesNotContain(eco.UnrestWhy(with, outer), x => x.what.StartsWith("local law"));
        Assert.DoesNotContain(eco.ProsperityWhy(with, outer), x => x.what.StartsWith("local law"));
        Assert.Equal(eco.ProsperityTarget(without, without.Holding("barony")!) + 2 * E("prosperity_drift"), eco.ProsperityTarget(with, barony));

        // a year's drift: unrest, the people and prosperity move on the one world
        foreach (var w in new[] { with, without }) { w.Holding("barony")!.Unrest = 30; w.Holding("outer")!.Unrest = 30; }
        eco.Accrue(with); eco.Accrue(without);
        var wb = without.Holding("barony")!; var wo = without.Holding("outer")!;
        Assert.Equal(wb.Unrest + E("unrest_drift"), barony.Unrest); Assert.Equal(wo.Unrest, outer.Unrest);
        Assert.Equal(wb.Population + E("pop_growth"), barony.Population); Assert.Equal(wo.Population, outer.Population);
        // prosperity moves at most five a year toward where it settles: over ten years, its unrest held where it was, the world
        // settles where its law puts it (a law that calms a restless world also lifts its prosperity by the calm, which is held out here)
        for (int y = 0; y < 9; y++) { barony.Unrest = wb.Unrest = 30; eco.Accrue(with); eco.Accrue(without); }
        if (E("prosperity_drift") != 0) Assert.Equal(Math.Sign(E("prosperity_drift")), Math.Sign(barony.Prosperity - wb.Prosperity));
        Assert.Equal(wo.Prosperity, outer.Prosperity); Assert.Equal(wo.Unrest, outer.Unrest);

        // the lawlessness of the world's own region, and of no other
        var w2 = Reach(); var w3 = Reach(); LocalLawSystem.Set(w2, Cat, w2.Holding("barony")!, lawId, optionId);
        var rs = new RegionSystem { Policies = Cat }; w2.Turn++; w3.Turn++; rs.Tick(w2); rs.Tick(w3);
        Assert.Equal(RegionSystem.StateOf(w3, "rim").Lawlessness + E("lawlessness_drift"), RegionSystem.StateOf(w2, "rim").Lawlessness);
        Assert.Equal(RegionSystem.StateOf(w3, "core").Lawlessness, RegionSystem.StateOf(w2, "core").Lawlessness);

        // a closed world admits no other house's venture; an open one does
        var found = new FoundBusinessCommand("corwin_head", "trading_company", "tessaly", Ventures(), TestContent.EconomyCatalog());
        if (E(LocalLawSystem.ClosedKey) > 0) Assert.Equal("Barony is a closed world: its holder admits no venture but its own", found.Refusal(Ctx(w2)));
        else Assert.Null(found.Refusal(Ctx(w2)));
        Assert.Null(new FoundBusinessCommand("corwin_head", "trading_company", "outer", Ventures(), TestContent.EconomyCatalog()).Refusal(Ctx(w2)));
    }

    [Fact]
    public void The_command_costs_influence_and_settles_the_law_on_that_world_for_its_years()
    {
        var w = Reach(); w.Turn = 5; var ex = new CommandExecutor(); w.Pools["self"].Influence = 4;
        var dole = Cat.Local.Law("dole")!;
        Assert.True(ex.TryExecute(new SetLocalLawCommand("self", "barony", "dole", "corn_dole", Cat), Ctx(w)));
        Assert.Equal("corn_dole", w.Holding("barony")!.LocalLaws["dole"]); Assert.Equal(4 - dole.Cost, w.Pools["self"].Influence);
        Assert.Empty(w.Holding("outer")!.LocalLaws); Assert.Equal(1, w.Counter(LocalLawSystem.SetCounter));
        Assert.Contains(w.History, e => e.Text.Contains("House Vega decrees for Barony: a corn dole."));
        // settled on that world for the law's years, and only on that world
        Assert.Equal($"decreed too lately on Barony: settled for {dole.Cooldown} more years", new SetLocalLawCommand("self", "barony", "dole", "dole_and_games", Cat).Refusal(Ctx(w)));
        Assert.Null(new SetLocalLawCommand("self", "outer", "dole", "corn_dole", Cat).Refusal(Ctx(w)));
        Assert.Null(new SetLocalLawCommand("self", "barony", "garrison", "quartered", Cat).Refusal(Ctx(w)));
        w.Turn += dole.Cooldown - 1; Assert.StartsWith("decreed too lately on Barony: settled for 1 more year", new SetLocalLawCommand("self", "barony", "dole", "none", Cat).Refusal(Ctx(w)));
        w.Turn++;
        // lifting it puts the world back as it was: the default is not kept
        Assert.True(ex.TryExecute(new SetLocalLawCommand("self", "barony", "dole", "none", Cat), Ctx(w)));
        Assert.Empty(w.Holding("barony")!.LocalLaws);
        Assert.Contains(w.History, e => e.Text.Contains("House Vega lifts a corn dole on Barony: no dole again."));
    }

    [Fact]
    public void Refusals_say_what_is_missing()
    {
        var w = Reach(); var ctx = Ctx(w);
        w.Characters["kin"] = new Character { Id = "kin", Name = "Tiro", HouseId = "vega", Age = 25, Alive = true }; w.Houses["vega"].Members.Add("kin"); w.Pools["kin"] = ActionPools.ForPlayer();
        w.Holdings["post"] = new Holding { Id = "post", OwnerId = "vega", Name = "Tessaly Post", Kind = Holding.Station, Specialization = "agri", SystemId = "tessaly" };
        string? No(string actor, string holding, string law, string option) => new SetLocalLawCommand(actor, holding, law, option, Cat).Refusal(ctx);
        Assert.Equal("only the head of House Vega makes law for its worlds", No("kin", "barony", "dole", "corn_dole"));
        Assert.Equal("Ember Hold is not held by House Vega", No("self", "corwin_world", "dole", "corn_dole"));
        Assert.Equal("a local law is made for a world, and Tessaly Post is not one", No("self", "post", "dole", "corn_dole"));
        Assert.Equal("no such world", No("self", "nowhere", "dole", "corn_dole"));
        Assert.Equal("no such local law", No("self", "barony", "tithe", "harsh"));
        Assert.Equal("no such option", No("self", "barony", "dole", "free_bread"));
        Assert.Equal("already the law on Barony", No("self", "barony", "dole", "none"));
        Assert.Equal("no house", No("nobody", "barony", "dole", "corn_dole"));
        w.Pools["self"].Influence = 1; Assert.Equal("needs 2 influence this year (1 left)", No("self", "barony", "gates", "closed"));
        w.Pools["self"].Influence = 0; Assert.Equal("needs 1 influence this year (0 left)", No("self", "barony", "dole", "corn_dole"));
        Assert.False(new SetLocalLawCommand("self", "barony", "dole", "corn_dole", Cat).CanExecute(ctx));
        Assert.Contains("barony", new SetLocalLawCommand("self", "barony", "dole", "corn_dole", Cat).Describe());
    }

    [Fact]
    public void A_world_carries_two_laws_off_the_custom_and_the_seat_or_a_developed_world_three()
    {
        var w = Reach(); var h = w.Holding("barony")!; var ctx = Ctx(w);
        Assert.Equal(2, LocalLawSystem.Limit(w, h));
        LocalLawSystem.Set(w, Cat, h, "dole", "corn_dole"); LocalLawSystem.Set(w, Cat, h, "garrison", "quartered");
        Assert.Equal(2, LocalLawSystem.Count(Cat, h)); Assert.Equal("a garrison, a corn dole", LocalLawSystem.Summary(Cat, h));
        Assert.Equal("Barony can carry 2 local laws off the custom and has 2: lift one first", new SetLocalLawCommand("self", "barony", "charter", "chartered", Cat).Refusal(ctx));
        // a law already off the custom may be changed to its other option, and any may be lifted; the next world has room of its own
        w.Turn = 10;
        Assert.Null(new SetLocalLawCommand("self", "barony", "garrison", "martial_rule", Cat).Refusal(ctx));
        Assert.Null(new SetLocalLawCommand("self", "barony", "garrison", "none", Cat).Refusal(ctx));
        Assert.Null(new SetLocalLawCommand("self", "outer", "charter", "chartered", Cat).Refusal(ctx));
        // the seat of the house carries three
        w.House("vega")!.SeatGrade = 1; w.House("vega")!.SeatHoldingId = "barony";
        Assert.Equal(3, LocalLawSystem.Limit(w, h)); Assert.Null(new SetLocalLawCommand("self", "barony", "charter", "chartered", Cat).Refusal(ctx));
        // and so does a world of high development
        Assert.Equal(2, LocalLawSystem.Limit(w, w.Holding("outer")!)); w.Development["outer"] = 10 * LocalLawSystem.HighDevelopment;
        Assert.Equal(3, LocalLawSystem.Limit(w, w.Holding("outer")!));
    }

    [Fact]
    public void The_estates_feel_a_local_law_by_the_share_of_the_house_s_worlds()
    {
        var w = Reach(); var h = w.Holding("barony")!;   // Vega holds two worlds
        int commons = PolicySystem.LawOpinion(w, Cat, "vega", "commons"), magistrates = PolicySystem.LawOpinion(w, Cat, "vega", "magistrates"), old = PolicySystem.LawOpinion(w, Cat, "vega", "old_blood");
        LocalLawSystem.Set(w, Cat, h, "dole", "dole_and_games");   // commons +15, magistrates −10, old blood −5, on one world of two
        Assert.Equal(8, LocalLawSystem.Opinion(w, Cat, "vega", "commons"));        // 7.5, a half away from nought
        Assert.Equal(-5, LocalLawSystem.Opinion(w, Cat, "vega", "magistrates"));
        Assert.Equal(-3, LocalLawSystem.Opinion(w, Cat, "vega", "old_blood"));     // −2.5
        Assert.Equal(commons + 8, PolicySystem.LawOpinion(w, Cat, "vega", "commons")); Assert.Equal(magistrates - 5, PolicySystem.LawOpinion(w, Cat, "vega", "magistrates")); Assert.Equal(old - 3, PolicySystem.LawOpinion(w, Cat, "vega", "old_blood"));
        Assert.Equal(0, LocalLawSystem.Opinion(w, Cat, "corwin", "commons"));
        // the same law on both worlds is felt in full; a house with one world feels it in full
        LocalLawSystem.Set(w, Cat, w.Holding("outer")!, "dole", "dole_and_games"); Assert.Equal(15, LocalLawSystem.Opinion(w, Cat, "vega", "commons"));
        LocalLawSystem.Set(w, Cat, w.Holding("corwin_world")!, "garrison", "martial_rule"); Assert.Equal(-20, LocalLawSystem.Opinion(w, Cat, "corwin", "commons"));
        Assert.Equal(1, LocalLawSystem.Scaled(1, 2)); Assert.Equal(-1, LocalLawSystem.Scaled(-1, 2)); Assert.Equal(0, LocalLawSystem.Scaled(1, 3)); Assert.Equal(2, LocalLawSystem.Scaled(5, 3)); Assert.Equal(0, LocalLawSystem.Scaled(5, 0));
        Assert.Contains(LocalLawSystem.Says(w, Cat, "vega", "commons"), x => x == "local law on 2 of 2 worlds +15");
        // and the estates' regard drifts toward it, the player's house and another's alike
        var twin = Reach(); LocalLawSystem.Set(twin, Cat, twin.Holding("outer")!, "dole", "none");
        w.Turn = twin.Turn = 1; PolicySystem.Tick(w, Cat); PolicySystem.Tick(twin, Cat);
        Assert.Equal(PolicySystem.Approval(twin, "vega", "commons") + 3, PolicySystem.Approval(w, "vega", "commons"));
        Assert.Equal(PolicySystem.Approval(twin, "corwin", "commons") - 3, PolicySystem.Approval(w, "corwin", "commons"));
    }

    [Fact]
    public void A_world_that_changes_hands_keeps_its_laws_and_its_new_holder_may_change_them_at_once()
    {
        var w = Reach(); w.Turn = 3; var ex = new CommandExecutor(); var eco = Eco(); var h = w.Holding("barony")!;
        Assert.True(ex.TryExecute(new SetLocalLawCommand("self", "barony", "local_tithe", "surcharge", Cat), Ctx(w)));
        int before = Line(eco, w, "vega", "Barony").Credits;
        h.OwnerId = "corwin"; h.FormerOwnerId = "vega"; h.GovernorId = "corwin_head";
        // the law stays with the world: it is in the new holder's ledger, and its estates feel it
        Assert.Equal("surcharge", LocalLawSystem.OptionId(Cat, h, "local_tithe"));
        Assert.Equal(before, Line(eco, w, "corwin", "Barony").Credits);
        Assert.Contains(("local law: a surcharge on the tithe", 5), eco.UnrestWhy(w, h));
        Assert.Equal(-8, LocalLawSystem.Opinion(w, Cat, "corwin", "commons"));   // −15 over its two worlds
        Assert.Equal(0, LocalLawSystem.Opinion(w, Cat, "vega", "commons"));
        // the old holder can no longer decree there; the new one is not bound by the years the old one's decree was settled for
        Assert.Equal("Barony is not held by House Vega", new SetLocalLawCommand("self", "barony", "local_tithe", "customary", Cat).Refusal(Ctx(w)));
        Assert.Equal(0, LocalLawSystem.CooldownLeft(w, h, "local_tithe"));
        Assert.True(ex.TryExecute(new SetLocalLawCommand("corwin_head", "barony", "local_tithe", "customary", Cat), Ctx(w)));
        Assert.Empty(h.LocalLaws);
    }

    [Fact]
    public void The_local_laws_and_their_settled_years_survive_a_save()
    {
        var w = Reach(); w.Turn = 2;
        Assert.True(new CommandExecutor().TryExecute(new SetLocalLawCommand("self", "barony", "settlers", "veterans", Cat), Ctx(w)));
        LocalLawSystem.Set(w, Cat, w.Holding("outer")!, "port", "free_port");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); LocalLawSystem.Use(back, Cat);
        Assert.Equal("veterans", back.Holding("barony")!.LocalLaws["settlers"]); Assert.Equal("free_port", back.Holding("outer")!.LocalLaws["port"]);
        Assert.Equal(LocalLawSystem.CooldownLeft(w, w.Holding("barony")!, "settlers"), LocalLawSystem.CooldownLeft(back, back.Holding("barony")!, "settlers"));
        Assert.True(LocalLawSystem.CooldownLeft(back, back.Holding("barony")!, "settlers") > 0);
        Assert.Equal(Same(Line(Eco(), w, "vega", "Barony")), Same(Line(Eco(), back, "vega", "Barony")));
    }

    [Fact]
    public void Another_house_decrees_where_there_is_a_plain_reason_and_lifts_it_when_the_reason_has_passed()
    {
        var holdings = Content.Holdings;
        World Corwin(int credits, int unrest, int prosperity = 50, int population = 20)
        {
            var w = Reach(); var h = w.Holding("corwin_world")!; w.House("corwin")!.Treasury.Credits = credits;
            h.Unrest = unrest; h.Prosperity = prosperity; h.Population = population; h.Specialization = "agri_world"; return w;
        }
        (string, string)? Want(World w) => LocalLawSystem.Wanted(w, Cat, w.House("corwin")!, w.Holding("corwin_world")!, holdings);
        // a rich and quiet world, a treasury that is low: a surcharge on the tithe
        Assert.Equal(("local_tithe", "surcharge"), Want(Corwin(credits: 2, unrest: 0, prosperity: 60, population: 50)));
        // a stirring world: bread where there is coin for it, soldiers where there is not
        Assert.Equal(("dole", "corn_dole"), Want(Corwin(credits: 20, unrest: 20, population: 50)));
        Assert.Equal(("garrison", "quartered"), Want(Corwin(credits: 2, unrest: 20, population: 50)));
        // a world in revolt, or one taken from a house that still stands and not yet quiet: martial rule
        var revolt = Corwin(credits: 20, unrest: 10, population: 50); revolt.Holding("corwin_world")!.Rebellion = 1; Assert.Equal(("garrison", "martial_rule"), Want(revolt));
        var taken = Corwin(credits: 20, unrest: 20, population: 50); taken.Holding("corwin_world")!.FormerOwnerId = "vega"; Assert.Equal(("garrison", "martial_rule"), Want(taken));
        // a thinly peopled world and a full chest: settlers' grants
        Assert.Equal(("settlers", "land_grants"), Want(Corwin(credits: 20, unrest: 0, population: 10)));
        // a quiet, full, solvent world: nothing
        Assert.Null(Want(Corwin(credits: 12, unrest: 0, prosperity: 50, population: 55)));
        // each is lifted when its reason has passed, once its years are out
        var calm = Corwin(credits: 20, unrest: 0, population: 55); var cw = calm.Holding("corwin_world")!;
        foreach (var (law, option, back) in new[] { ("garrison", "martial_rule", "none"), ("garrison", "quartered", "none"), ("dole", "corn_dole", "none"), ("local_tithe", "surcharge", "customary"), ("settlers", "land_grants", "none") })
        {
            cw.LocalLaws.Clear(); cw.LocalLaws[law] = option; Assert.Equal((law, back), Want(calm));
        }
        cw.LocalLaws.Clear(); LocalLawSystem.Set(calm, Cat, cw, "dole", "corn_dole"); Assert.Null(Want(calm));   // not while it is settled
    }

    [Fact]
    public void A_world_taken_at_its_limit_of_laws_has_what_stirs_it_lifted_first_and_then_martial_rule()
    {
        // the player's world, with a surcharge and a labour levy (its two laws off the custom), taken by House Corwin and restless
        var w = Reach(); var ex = new CommandExecutor(); var barony = w.Holding("barony")!;
        LocalLawSystem.Set(w, Cat, barony, "local_tithe", "surcharge"); LocalLawSystem.Set(w, Cat, barony, "labour_levy", "corvee");
        w.Holdings.Remove("corwin_world");
        barony.OwnerId = "corwin"; barony.FormerOwnerId = "vega"; barony.GovernorId = "corwin_head"; barony.Unrest = 35; barony.Population = 50;
        var corwin = w.House("corwin")!; corwin.Treasury.Credits = 2;
        Assert.Equal(LocalLawSystem.Limit(w, barony), LocalLawSystem.Count(Cat, barony));
        Assert.Equal(("garrison", "martial_rule"), LocalLawSystem.Wanted(w, Cat, corwin, barony, Content.Holdings));   // what it wants first; the world has no room for it
        Assert.NotNull(new SetLocalLawCommand("corwin_head", "barony", "garrison", "martial_rule", Cat).Refusal(Ctx(w)));
        for (int t = 1; t <= 6 && !(barony.LocalLaws.GetValueOrDefault("garrison") == "martial_rule"); t++) { w.Turn = t; w.Pools["corwin_head"].Influence = 2; barony.Unrest = 35; LocalLawSystem.Tick(w, Cat, ex, Ctx(w), Content.Holdings); }
        Assert.Equal("martial_rule", barony.LocalLaws.GetValueOrDefault("garrison"));
        Assert.Equal("customary", LocalLawSystem.OptionId(Cat, barony, "local_tithe"));   // the surcharge went first, to make room
        Assert.Contains(w.History, e => e.Text.Contains("House Corwin decrees for Barony"));
    }

    [Fact]
    public void Another_house_s_head_makes_the_decree_at_the_year_s_turn_and_the_player_s_house_is_left_alone()
    {
        var w = Reach(); var ex = new CommandExecutor();
        w.House("corwin")!.Treasury.Credits = 2; var cw = w.Holding("corwin_world")!; cw.Prosperity = 60; cw.Specialization = "agri_world"; cw.Population = 50;
        w.House("vega")!.Treasury.Credits = 2; w.Holding("barony")!.Prosperity = 60;   // the same plain reason on the player's world
        for (int t = 1; t <= 4 && cw.LocalLaws.Count == 0; t++) { w.Turn = t; w.Pools["corwin_head"].Influence = 2; LocalLawSystem.Tick(w, Cat, ex, Ctx(w), Content.Holdings); }
        Assert.Equal("surcharge", cw.LocalLaws["local_tithe"]);
        Assert.Empty(w.Holding("barony")!.LocalLaws); Assert.Empty(w.Holding("outer")!.LocalLaws);
        Assert.Contains(w.History, e => e.Text.Contains("House Corwin decrees for Ember Hold: a surcharge on the tithe."));
        // a house does not decree what would turn one of its estates against it
        var near = Reach(); near.House("corwin")!.Treasury.Credits = 2; var nw = near.Holding("corwin_world")!; nw.Prosperity = 60; nw.Specialization = "agri_world"; nw.Population = 50;
        near.Laws["corwin"] = new Dictionary<string, string> { ["tithe"] = "harsh" };   // the commons already at −25
        for (int t = 1; t <= 4; t++) { near.Turn = t; near.Pools["corwin_head"].Influence = 2; LocalLawSystem.Tick(near, Cat, ex, Ctx(near), Content.Holdings); }
        Assert.Empty(nw.LocalLaws);
    }

    [Fact]
    public void The_other_houses_make_local_law_in_a_real_game()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 1); var tc = new TurnController(w, Content);
        string mine = w.Protagonist!.HouseId; var seen = new HashSet<string>();
        for (int y = 0; y < 12; y++)
        {
            foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
            tc.EndTurn();
            foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet && h.OwnerId != mine)) foreach (var (law, option) in LocalLawSystem.InForce(Cat, h)) seen.Add(law.Id + ":" + option.Id);
        }
        Assert.True(w.Counter(LocalLawSystem.SetCounter) >= 3, $"only {w.Counter(LocalLawSystem.SetCounter)} decrees in twelve years");
        Assert.NotEmpty(seen);
        Assert.All(w.Holdings.Values.Where(h => h.OwnerId == mine), h => Assert.Empty(h.LocalLaws));   // nobody decrees for the player
        Assert.Contains(w.History, e => e.Text.Contains(" decrees for "));
    }

    [Fact]
    public void What_an_option_does_is_said_in_words_and_in_shown_money()
    {
        var o = Cat.Local.Law("dole")!.Option("corn_dole")!;
        Assert.Equal("costs 200 credits a year; unrest eases by 3 a year", LocalLawSystem.Does(o));
        Assert.Equal("the custom: nothing is added and nothing taken", LocalLawSystem.Does(Cat.Local.Law("dole")!.DefaultOption));
        Assert.Contains("no other house may found a venture here", LocalLawSystem.Does(Cat.Local.Law("gates")!.Option("closed")!));
        Assert.Equal("Commons +10, Magistrates −5", LocalLawSystem.Minds(Cat, o));
        foreach (var law in Cat.Local.Laws) foreach (var opt in law.Options) Assert.DoesNotContain("_", LocalLawSystem.Does(opt));
    }
}
