using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The ranged returns as the game's own prices, how easy money is, and the boosts a game may be begun with
/// (docs/MONEY.md, "Settings"; docs/ECONOMY_REVIEW.md, "Ranged returns").</summary>
public class MoneySettingsTests
{
    // its own copy of the content: these tests price it at settings other than the standard one
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Start() => WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 5);

    [Fact]
    public void Nothing_that_is_built_returns_more_than_its_kind_is_meant_to()
    {
        var fresh = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir()); var all = ReturnTargets.All(fresh).ToList(); Assert.True(all.Count > 100);
        var over = all.Where(x => x.meant > 0 && x.returns > x.meant + 0.005).Select(x => $"{x.kind} {x.id} returns {100 * x.returns:F0}%, meant {100 * x.meant:F0}%").ToList();
        Assert.True(over.Count == 0, string.Join("; ", over));
        // the kinds are a range, as the owner asked: the dear goods above the common ones, and those above a market or a port
        double Meant(string id) => all.First(x => x.id == id).meant; double Pays(string id) => all.First(x => x.id == id).returns;
        Assert.True(Meant("rare_earth_refinery") > Meant("mine") && Meant("mine") > Meant("market"));
        Assert.InRange(Pays("mine"), 0.08, 0.105); Assert.InRange(Pays("rare_earth_refinery"), 0.12, 0.155); Assert.InRange(Pays("salvage_guild"), 0.16, 0.205);
        // and none of it is a way to double one's money in a year any more
        Assert.All(all, x => Assert.True(x.returns < 0.21, $"{x.id} {x.returns:F2}"));
    }

    [Fact]
    public void How_easy_money_is_scales_what_things_cost_for_a_game_and_is_put_back_for_the_next()
    {
        var mine = Content.Holdings.Building("mine")!; int written = mine.Cost.Credits; var bank = Content.Businesses.Def("merchant_bank")!; int bankWas = bank.Cost.Credits, expandWas = bank.ExpandCost.Credits;
        Assert.Equal(MoneySettings.Standard, Start().MoneyEase); Assert.Equal(new[] { 70, 100, 150, 250 }, MoneySettings.Levels.Select(MoneySettings.ReturnPct));
        foreach (var level in MoneySettings.Levels) { Assert.NotEqual(level, MoneySettings.Name(level)); Assert.True(MoneySettings.Does(level).Length > 40); }
        var w = Start(); w.MoneyEase = MoneySettings.Generous; _ = new TurnController(w, Content);
        Assert.Equal((int)Math.Ceiling(written * 100.0 / 150), mine.Cost.Credits); Assert.True(bank.Cost.Credits < bankWas && bank.ExpandCost.Credits < expandWas);
        Assert.InRange(ReturnTargets.Returns(mine.Cost, mine.Yield, new Resources(), mine.Upkeep), 0.135, 0.16);   // half as much again as the 10% it is meant to
        var lean = Start(); lean.MoneyEase = MoneySettings.Lean; _ = new TurnController(lean, Content); Assert.Equal((int)Math.Ceiling(written * 100.0 / 70), mine.Cost.Credits);
        var lavish = Start(); lavish.MoneyEase = MoneySettings.Lavish; _ = new TurnController(lavish, Content); Assert.Equal((int)Math.Ceiling(written * 100.0 / 250), mine.Cost.Credits);
        // a game at the standard setting after it finds the prices as they are written
        _ = new TurnController(Start(), Content); Assert.Equal(written, mine.Cost.Credits); Assert.Equal(bankWas, bank.Cost.Credits); Assert.Equal(expandWas, bank.ExpandCost.Credits);
        // nothing that costs something becomes free, and hulls are not scaled
        var cheap = Start(); cheap.MoneyEase = MoneySettings.Lavish; int hull = Content.ShipClasses.Defs[0].Cost.Materials; _ = new TurnController(cheap, Content);
        Assert.All(Content.Holdings.Works, x => Assert.All(Resource.All, r => Assert.True(x.Cost.Get(r) >= 0))); Assert.Equal(hull, Content.ShipClasses.Defs[0].Cost.Materials);
        _ = new TurnController(Start(), Content);
        // and the setting is kept by a save
        var kept = Start(); kept.MoneyEase = MoneySettings.Lean; kept.Boosts.Add(MoneySettings.DeepPurse); var back = SaveSystem.Deserialize(SaveSystem.Serialize(kept));
        Assert.Equal(MoneySettings.Lean, back.MoneyEase); Assert.Contains(MoneySettings.DeepPurse, back.Boosts);
        // a save made in the day they were called cheats still carries them, and what is written now does not use the word
        string saved = SaveSystem.Serialize(kept); Assert.Contains("\"Boosts\"", saved); Assert.DoesNotContain("Cheats", saved);
        Assert.Contains(MoneySettings.DeepPurse, SaveSystem.Deserialize(saved.Replace("\"Boosts\"", "\"Cheats\"")).Boosts);
    }

    [Fact]
    public void A_game_begun_with_boosts_gives_the_players_house_what_they_give_once_and_says_so()
    {
        var w = Start(); var p = w.Protagonist!; var house = w.House(p.HouseId)!; var other = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id)); int otherHad = other.Treasury.Credits;
        Assert.Equal("", MoneySettings.Says(w)); MoneySettings.Begin(w); Assert.DoesNotContain(w.History, h => h.Text.Contains("Begun with"));
        int credits = house.Treasury.Credits, materials = house.Treasury.Materials, rare = house.Treasury.RareEarths, purse = p.Purse;
        foreach (var c in MoneySettings.Boosts) { w.Boosts.Add(c); Assert.NotEqual(c, MoneySettings.BoostName(c)); Assert.True(MoneySettings.BoostDoes(c).Length > 20); }
        MoneySettings.Begin(w); MoneySettings.Begin(w);   // asked twice, given once
        Assert.Equal(credits + MoneySettings.TreasuryGift, house.Treasury.Credits); Assert.Equal(materials + MoneySettings.StoresMaterials, house.Treasury.Materials);
        Assert.Equal(rare + MoneySettings.StoresDear, house.Treasury.RareEarths); Assert.Equal(purse + MoneySettings.PurseGift, p.Purse); Assert.Equal(otherHad, other.Treasury.Credits);
        Assert.Contains("A full treasury", MoneySettings.Says(w)); Assert.Contains(w.History, h => h.Text.Contains("Begun with"));
        Assert.Contains("50,000 credits", MoneySettings.BoostDoes(MoneySettings.FullTreasury));
        // the second income is a line of the player's ledger and nobody else's
        var tc = new TurnController(w, ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir())); var led = tc.Economy.Compute(w, house.Id, tc.Fleets);
        int earned = led.Lines.Where(l => l.Kind != "boost" && l.Delta.Credits > 0).Sum(l => l.Delta.Credits); Assert.Equal(earned, led.Lines.Single(l => l.Kind == "boost").Delta.Credits);
        Assert.DoesNotContain(tc.Economy.Compute(w, other.Id, tc.Fleets).Lines, l => l.Kind == "boost");
    }
}
