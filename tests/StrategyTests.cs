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
        Assert.Equal(11, content.Holdings.Rates.Count);   // five classic, five strategic (isotopes sell only)
        var rate = content.Holdings.Rates.First(r => r.Give == "credits" && r.Get == "materials");
        var ctx = new CommandContext(w, TestContent.Rng());
        var house = w.House("serval")!;
        int credits = house.Treasury.Credits, materials = house.Treasury.Materials;
        Assert.True(new CommandExecutor().TryExecute(new ExchangeCommand(w.ProtagonistId, rate, content.Holdings, times: 2), ctx));
        Assert.Equal(credits - 6, house.Treasury.Credits);
        Assert.Equal(materials + 4, house.Treasury.Materials);

        var (_, w2) = Fresh("the_risen_officer");                     // Vane: a bare fortress billet, no market
        var vane = w2.House(w2.Protagonist!.HouseId)!;                 // ...so the caravaneers take half (GDD §17: never locked out)
        Assert.False(ExchangeCommand.HasExchange(w2, vane.Id, content.Holdings));
        Assert.Equal(1, ExchangeCommand.Received(w2, vane.Id, rate, content.Holdings, 1));
        Assert.Equal(4, ExchangeCommand.Received(w, "serval", rate, content.Holdings, 2));
        Assert.Equal(vane.Treasury.Credits >= 3, new ExchangeCommand(w2.ProtagonistId, rate, content.Holdings).CanExecute(new CommandContext(w2, TestContent.Rng())));
    }

    [Fact]
    public void UnpaidFleets_LoseReadiness_ThenDesert_UntilTheDebtIsPayable()
    {
        var (content, w) = Fresh("academy_crucible");
        var corwin = w.House("corwin")!;
        corwin.Treasury.Credits = -FleetSystem.DesertionDebt - 5;
        var fleet = w.Fleets.Values.First(f => f.OwnerId == "corwin");
        fleet.Ships["frigate"] = 3; fleet.Recount();
        int before = fleet.Strength, readiness = fleet.Readiness;
        var fleets = new FleetSystem(content.Fleets, content.Holdings, content.ShipClasses, content.Friction);
        fleets.Tick(w);
        Assert.Equal(before - 1, fleet.Strength);
        Assert.True(fleet.Readiness < readiness);
        Assert.Equal(1, w.Counter(FleetSystem.DesertionsCounter));
        Assert.Contains(w.History, h => h.Text.Contains("desert"));

        corwin.Treasury.Credits = 50;                                   // paid again: nobody walks
        fleets.Tick(w);
        Assert.Equal(before - 1, fleet.Strength);
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
        Assert.Equal(3, barony.Unrest);                               // drift +5 against the solvent decay of −3, +1 for a world on the rim (GDD §19)
    }

    [Fact]
    public void Territory_ContiguityRewardsACompactRealm()
    {
        var (content, w) = Fresh("the_circling_heir");                // Highmarch ↔ The Spire are adjacent, both Serval
        var eco = new Economy(content.Holdings);
        int Worlds(World x) => eco.Compute(x, "serval").Lines.Where(l => l.Kind == "world").Sum(l => l.Delta.Credits);   // the holdings' own lines, not tolls or the court
        int withBonus = Worlds(w);

        var (_, w2) = Fresh("the_circling_heir");
        w2.Holding("serval_seat_minor")!.SystemId = "sato_holdfast"; // same holdings, scattered across the map (another middle-ring world, so no ring bonus changes)
        Assert.Equal(withBonus - 2, Worlds(w2));   // one bonus per holding lost
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
