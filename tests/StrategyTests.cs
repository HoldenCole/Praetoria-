using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

public class StrategyTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario = "the_circling_heir", ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }

    [Fact]
    public void Exchange_LoadsRates_AndNeedsAMarket()
    {
        var (content, w) = Fresh();                                    // Serval's Highmarch has a market
        Assert.Equal(6, content.Holdings.Rates.Count);
        var rate = content.Holdings.Rates.First(r => r.Give == "credits" && r.Get == "materials");
        var ctx = new CommandContext(w, TestContent.Rng());
        var house = w.House("serval")!;
        int credits = house.Treasury.Credits, materials = house.Treasury.Materials;
        Assert.True(new CommandExecutor().TryExecute(new ExchangeCommand(w.ProtagonistId, rate, content.Holdings, times: 2), ctx));
        Assert.Equal(credits - 6, house.Treasury.Credits);
        Assert.Equal(materials + 4, house.Treasury.Materials);

        var (_, w2) = Fresh("the_risen_officer");                     // Vane: a bare fortress billet, no market
        Assert.False(new ExchangeCommand(w2.ProtagonistId, rate, content.Holdings).CanExecute(new CommandContext(w2, TestContent.Rng())));
    }

    [Fact]
    public void TaxPolicy_TradesCreditsForUnrest()
    {
        var (content, w) = Fresh("academy_crucible");
        var house = w.House("vega")!; var barony = w.Holding("vega_barony")!;
        var eco = new Economy(content.Holdings);
        var baseline = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        eco.Accrue(baseline); int normalCredits = baseline.House("vega")!.Treasury.Credits;

        new CommandExecutor().TryExecute(new SetTaxPolicyCommand(w.ProtagonistId, "harsh", content.Holdings), new CommandContext(w, TestContent.Rng()));
        eco.Accrue(w);
        Assert.Equal(normalCredits + 2, house.Treasury.Credits);     // +2 per holding
        Assert.Equal(2, barony.Unrest);                               // drift +5 against the solvent decay of −3
    }

    [Fact]
    public void Territory_ContiguityRewardsACompactRealm()
    {
        var (content, w) = Fresh("the_circling_heir");                // Highmarch ↔ The Spire are adjacent, both Serval
        var eco = new Economy(content.Holdings);
        int before = w.House("serval")!.Treasury.Credits;
        eco.Accrue(w);
        int withBonus = w.House("serval")!.Treasury.Credits - before;

        var (_, w2) = Fresh("the_circling_heir");
        w2.Holding("serval_seat_minor")!.SystemId = "aurelia_prime"; // same holdings, scattered across the map
        int before2 = w2.House("serval")!.Treasury.Credits;
        eco.Accrue(w2);
        Assert.Equal(withBonus - 2, w2.House("serval")!.Treasury.Credits - before2);   // one bonus per holding lost
    }

    [Fact]
    public void Megastructures_AreGatedByTitleRank_AndAnnounced()
    {
        var (content, w) = Fresh("the_imperial_court");               // Thorne is a knight
        var house = w.House("thorne")!; house.Treasury.Materials = 100; house.Treasury.Credits = 100; house.Treasury.Exotics = 10;
        var ctx = new CommandContext(w, TestContent.Rng());
        var ring = new BuildCommand(w.ProtagonistId, "thorne_fief", "orbital_ring", content.Holdings, content.Titles);
        Assert.False(ring.CanExecute(ctx));
        house.Title = "count";
        Assert.True(new CommandExecutor().TryExecute(ring, ctx));
        Assert.True(w.HasFlag("megastructure_raised"));
        Assert.Equal(1, w.Counter("megastructures"));
        Assert.True(content.Holdings.Building("orbital_ring")!.Megastructure);
    }

    [Fact]
    public void Difficulty_ScalesRivalAppetite_AndRoundTrips()
    {
        var (content, w) = Fresh("academy_crucible");
        w.Difficulty = 2.0;
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(2.0, loaded.Difficulty);
        // harsh: a rival at −35 (not −60) with more hulls goes to war
        w.Relationship("corwin_lucan", "vega_marcus").Disposition = -35;
        var exec = new CommandExecutor();
        new NpcAi().Act(exec, new CommandContext(w, TestContent.Rng()), null, null, content.Fleets);
        Assert.True(w.AtWar("corwin", "vega"));
        var (_, w3) = Fresh("academy_crucible");
        w3.Relationship("corwin_lucan", "vega_marcus").Disposition = -35;      // default difficulty: not yet
        new NpcAi().Act(new CommandExecutor(), new CommandContext(w3, TestContent.Rng()), null, null, content.Fleets);
        Assert.False(w3.AtWar("corwin", "vega"));
    }
}
