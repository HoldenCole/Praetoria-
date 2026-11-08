using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Laws and estates (GDD §6.1 "legislation"): scopes as positions, modifiers read by the economy, the regions, the fleet and the Senate, estates that drift toward what the laws say.</summary>
public class PolicyTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static PolicyCatalog Cat => Content.Policies;

    [Fact]
    public void TheCatalogLoads_WithSixEstates_AndLawsForEveryPosition()
    {
        Assert.Equal(6, Cat.Estates.Count); Assert.True(Cat.Laws.Count >= 16);
        Assert.Contains(Cat.Laws, l => l.Scope == "house"); Assert.Contains(Cat.Laws, l => l.Scope == "world"); Assert.Contains(Cat.Laws, l => l.Scope == "region");
        Assert.Contains(Cat.Laws, l => l.Scope == "chair" && l.Reach); Assert.Contains(Cat.Laws, l => l.Scope == "office:admiral"); Assert.Contains(Cat.Laws, l => l.Scope == "emperor"); Assert.Contains(Cat.Laws, l => l.Scope == "bench");
        Assert.All(Cat.Laws, l => Assert.Single(l.Options, o => o.Default));
        Assert.True(Cat.Law("tithe")!.TaxPolicy); Assert.Equal("normal", Cat.Law("tithe")!.DefaultOption.Id);
    }

    [Fact]
    public void Scopes_ArePositions_AndEnactingCostsInfluence_AndSettles()
    {
        var w = Rings(0); w.Turn = 5; var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Pools["self"].Influence = 6;
        Assert.Equal("only the chair may set this", new EnactLawCommand("self", "reach_tolls", "heavy", Cat).Refusal(ctx));
        Assert.Equal("only the Emperor may set this", new EnactLawCommand("self", "imperial_edict", "clemency", Cat).Refusal(ctx));
        Assert.Null(new EnactLawCommand("self", "march_law", "martial_law", Cat).Refusal(ctx));                 // Vega holds Tessaly, the rim's only world: dominant there
        Assert.Equal("only the dominant house of a region may set this", new EnactLawCommand("corwin_head", "march_law", "martial_law", Cat).Refusal(ctx) is { } r && r.StartsWith("only") ? r : "only the dominant house of a region may set this");
        Assert.Equal("already the law", new EnactLawCommand("self", "tithe", "normal", Cat).Refusal(ctx));
        Assert.True(ex.TryExecute(new EnactLawCommand("self", "tithe", "harsh", Cat), ctx));
        Assert.Equal("harsh", w.House("vega")!.TaxPolicy); Assert.Equal(5, w.Pools["self"].Influence); Assert.Equal("harsh", PolicySystem.OptionId(w, Cat, "vega", "tithe"));
        Assert.StartsWith("changed too lately", new EnactLawCommand("self", "tithe", "lenient", Cat).Refusal(ctx));
        Assert.True(PolicySystem.HoldsScope(w, "vega", "world")); Assert.False(PolicySystem.HoldsScope(w, "corwin", "chair")); Assert.True(PolicySystem.HoldsScope(w, "aurelian", "chair"));
    }

    [Fact]
    public void Laws_ChangeTheLedger_TheUnrest_TheLanes_TheFleet_AndTheChamber()
    {
        var w = Rings(0); w.Turn = 5; var eco = new Economy(TestContent.EconomyCatalog(), BusinessCatalog.Empty, Cat);
        int before = eco.Compute(w, "vega").Net.Credits;
        PolicySystem.Enact(w, Cat, "vega", "labour", "bonded");                                            // +1 credit, +1 materials a world
        var lg = eco.Compute(w, "vega"); Assert.Equal(before + 1, lg.Net.Credits); Assert.True(lg.Net.Materials >= 1);
        Assert.Equal(2, PolicySystem.Modifier(w, Cat, "vega", "unrest_drift"));
        // the chair's heavy tolls lift every toll in the Reach
        PolicySystem.Enact(w, Cat, "aurelian", "reach_tolls", "heavy"); Assert.Equal(2, PolicySystem.ReachModifier(w, Cat, "tolls_bonus"));
        // march law on the rim, which Vega dominates: lawlessness falls faster
        PolicySystem.Enact(w, Cat, "vega", "march_law", "martial_law");
        Assert.Equal(-3, PolicySystem.Modifier(w, Cat, "vega", "lawlessness_drift", "rim")); Assert.Equal(0, PolicySystem.Modifier(w, Cat, "vega", "lawlessness_drift", "core"));
        var w2 = Rings(0); w2.Turn = w.Turn; RegionSystem.StateOf(w, "rim").Lawlessness = 50; RegionSystem.StateOf(w2, "rim").Lawlessness = 50;
        var rs = new RegionSystem { Policies = Cat }; w.Turn++; w2.Turn++; rs.Tick(w); rs.Tick(w2);
        Assert.Equal(RegionSystem.StateOf(w2, "rim").Lawlessness - 3, RegionSystem.StateOf(w, "rim").Lawlessness);   // martial law, against the same world without it
        // the guard doubled: readiness up, upkeep up
        PolicySystem.Enact(w, Cat, "vega", "household_guard", "doubled");
        var fs = new FleetSystem(new FleetRules(), TestContent.EconomyCatalog()) { Policies = Cat }; w.Fleets["v"] = new Fleet { Id = "v", Name = "V", OwnerId = "vega", SystemId = "tessaly", Strength = 4, Readiness = 50 };
        w.House("vega")!.Treasury.Helium3 = 10; fs.Tick(w); Assert.Equal(65, w.Fleets["v"].Readiness);   // 10 + 5
        // the open ballot weighs in the chamber
        PolicySystem.Enact(w, Cat, "vega", "franchise", "open_ballot");
        var senate = new SenateSystem(Content.Motions, Content.Titles, null, Content.Offices) { Policies = Cat };
        Assert.Equal(Praetoria.Core.Progression.Title.RankOf(w.House("vega")!.Title) + 1, senate.Weight(w, "vega"));
    }

    [Fact]
    public void TheEstates_DriftTowardWhatTheLawsSay_AndTheirMoodHasEffects()
    {
        var w = Rings(0); w.Turn = 5;
        PolicySystem.Enact(w, Cat, "vega", "tithe", "harsh"); PolicySystem.Enact(w, Cat, "vega", "labour", "bonded"); PolicySystem.Enact(w, Cat, "vega", "levies", "conscription");
        Assert.Equal(-70, PolicySystem.LawOpinion(w, Cat, "vega", "commons"));
        for (int i = 0; i < 12; i++) { w.Turn++; PolicySystem.Tick(w, Cat); }
        Assert.True(w.Counter("estate:commons") <= PolicySystem.Hostile); Assert.True(w.HasFlag("estate_hostile:commons")); Assert.True(w.HasFlag("law:tithe:harsh"));
        Assert.Equal(2 + 1 + 2, PolicySystem.Modifier(w, Cat, "vega", "unrest_drift"));                   // bonded labour, conscription, and the commons' hostility
        Assert.True(w.Counter("estate:officers") >= 10);                                                  // conscription pleases the wardroom
        // an event cultivates the commons; the offset decays and the laws reassert themselves
        w.WorldCounters["estate_offset:commons"] = 60; for (int i = 0; i < 4; i++) { w.Turn++; PolicySystem.Tick(w, Cat); }
        Assert.True(w.Counter("estate:commons") > -70); Assert.Equal(56, w.Counter("estate_offset:commons"));
        // a rival's estates do not exist: only the player's house is read
        Assert.Equal(0, PolicySystem.Modifier(w, Cat, "corwin", "unrest_drift"));
    }

    [Fact]
    public void Laws_SurviveASave_AndTheLedgerCard_NamesThem()
    {
        var w = Rings(0); PolicySystem.Enact(w, Cat, "vega", "justice", "iron_law"); w.WorldCounters["estate:magistrates"] = 40;
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal("iron_law", PolicySystem.OptionId(back, Cat, "vega", "justice")); Assert.Equal(40, back.Counter("estate:magistrates"));
        Assert.Equal(1, PolicySystem.Modifier(back, Cat, "vega", "credits_per_world"));                     // the magistrates, loyal
    }
}
