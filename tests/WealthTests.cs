using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Personal wealth (docs/WEALTH.md): purses apart from the treasury, the allowance, what people do with their own money,
/// debts, heirlooms, the will, and the estate kept whole or shared out.</summary>
public class WealthTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static Character Kin(World w, House h, Character head, string id, params string[] natures)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = 30, Sex = "male", MotherId = head.Sex == "female" ? head.Id : "", FatherId = head.Sex == "female" ? "" : head.Id };
        foreach (var n in natures) c.NatureTraits.Add(n); c.Skills["discipline"] = 3; w.Characters[id] = c; h.Members.Add(id); w.Relationship(id, head.Id).Disposition = 40; return c;
    }

    [Fact]
    public void A_purse_is_apart_from_the_treasury_and_the_head_moves_money_between_them()
    {
        var (_, w, me, mine) = Start(); mine.Treasury.Credits = 100; me.Purse = 0;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Move, "", "30"))); Assert.Equal(30, me.Purse); Assert.Equal(70, mine.Treasury.Credits);
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Move, "", "-10"))); Assert.Equal(20, me.Purse); Assert.Equal(80, mine.Treasury.Credits);
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Move, "", "500"))); Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Move, "", "-500")));
        var k = Kin(w, mine, me, "kin"); Assert.Equal("only the head of the house has its purse", WealthSystem.MoveRefusal(w, k, 5));
    }

    [Fact]
    public void The_allowance_is_paid_from_the_treasury_and_the_family_judges_the_head_by_it()
    {
        int After(string policy, out int paid)
        {
            var (tc, w, me, mine) = Start(); foreach (var c in WealthSystem.Members(w, mine).Where(c => c.Id != me.Id).ToList()) c.Alive = false;
            var k = Kin(w, mine, me, "kin", "Honorable"); mine.Treasury.Credits = 100; int before = mine.Treasury.Credits;
            Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Allowance, "", policy))); Assert.Equal(WealthSystem.AllowanceOf(policy), WealthSystem.AllowanceBill(w, mine));
            int purse = k.Purse; WealthSystem.Tick(w); paid = k.Purse - purse - WealthSystem.Pay(k); _ = before; _ = tc;
            return w.Relationship(k.Id, me.Id).Disposition;
        }
        int mean = After(WealthSystem.Mean, out int paidMean), usual = After(WealthSystem.Customary, out int paidUsual), generous = After(WealthSystem.Generous, out int paidGenerous);
        Assert.Equal(0, paidMean); Assert.Equal(1, paidUsual); Assert.Equal(3, paidGenerous);
        Assert.True(mean < usual && usual < generous, $"{mean} {usual} {generous}");
    }

    [Fact]
    public void What_people_do_with_their_own_money_is_their_own_business()
    {
        var (_, w, me, mine) = Start(); foreach (var c in WealthSystem.Members(w, mine).Where(c => c.Id != me.Id).ToList()) c.Alive = false;
        var loyal = Kin(w, mine, me, "loyal", "Loyal"); var proud = Kin(w, mine, me, "proud", "Proud"); var plain = Kin(w, mine, me, "plain", "Honorable");
        loyal.Purse = 40; proud.Purse = 40; plain.Purse = 90; mine.Treasury.Credits = -30;
        WealthSystem.Tick(w);
        Assert.Contains("of their own into the house", loyal.LastSpend); Assert.True(mine.Treasury.Credits > -30); Assert.True(w.HasFlag(WealthSystem.KinGaveFlag)); Assert.Contains(WealthSystem.GaveChar, loyal.Flags);
        mine.Treasury.Credits = 100; proud.Purse = 40; plain.Purse = 90; WealthSystem.Tick(w);
        Assert.Contains("being seen", proud.LastSpend); Assert.True(proud.Purse < 40 + WealthSystem.Pay(proud) + 1);
        Assert.Equal("Lived within their means.", plain.LastSpend); Assert.Equal(WealthSystem.ComfortablePurse, plain.Purse);
    }

    [Fact]
    public void A_relative_in_debt_can_be_paid_for_or_left_until_the_creditors_come_to_the_house()
    {
        var (_, w, me, mine) = Start(); var k = Kin(w, mine, me, "debtor", "Honorable"); k.Purse = -12; mine.Treasury.Credits = 100; me.Purse = 50;
        WealthSystem.Tick(w); Assert.Contains(WealthSystem.InDebtChar, k.Flags); Assert.True(w.HasFlag(WealthSystem.DebtFlag), "flag"); Assert.True(k.Purse <= -12, $"a debt grows: {k.Purse}");
        int owed = -k.Purse; int regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Settle, k.Id, "purse"))); Assert.Equal(0, k.Purse); Assert.Equal(50 - owed + WealthSystem.Pay(me) + WealthSystem.EstateIncome(w, me.Id), me.Purse);
        Assert.Equal(regard + 15, w.Relationship(k.Id, me.Id).Disposition); Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Settle, k.Id, "treasury")));
        // left alone and grown large, it becomes the house's scandal
        k.Purse = -30; int legit = mine.Legitimacy; int treasury = mine.Treasury.Credits; WealthSystem.Tick(w);
        Assert.True(w.HasFlag(WealthSystem.DebtScandalFlag)); Assert.Equal(Math.Max(0, legit - 2), mine.Legitimacy); Assert.True(mine.Treasury.Credits < treasury);
    }

    [Fact]
    public void Old_names_come_with_an_heirloom_which_can_be_given_or_sold_at_a_price()
    {
        var (_, w, me, mine) = Start(); Assert.NotEmpty(w.Heirlooms); Assert.All(w.Heirlooms, h => { Assert.NotNull(w.Char(h.HolderId)); Assert.InRange(h.Value, 15, 40); });
        var item = WealthSystem.HeldBy(w, me.Id).FirstOrDefault() ?? new Heirloom { Id = "test_item", Name = "the test signet", Kind = "signet", HolderId = me.Id, OfHouseId = mine.Id, Value = 20 };
        if (!w.Heirlooms.Contains(item)) w.Heirlooms.Add(item);
        var k = Kin(w, mine, me, "kin"); int regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Give, item.Id, k.Id))); Assert.Equal(k.Id, item.HolderId); Assert.Equal(regard + 15, w.Relationship(k.Id, me.Id).Disposition);
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Sell, item.Id, "")));   // no longer yours
        item.HolderId = me.Id; int legit = mine.Legitimacy; int purse = me.Purse;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Sell, item.Id, ""))); Assert.DoesNotContain(item, w.Heirlooms); Assert.Equal(purse + item.Value, me.Purse);
        Assert.Equal(Math.Max(0, legit - 3), mine.Legitimacy); Assert.True(w.HasFlag(WealthSystem.HeirloomSoldFlag));
        // a new name has nothing handed down
        var (_, w2, me2, _) = Start("the_new_man"); Assert.Empty(WealthSystem.HeldBy(w2, me2.Id));
    }

    [Fact]
    public void The_house_goes_by_the_law_and_the_purse_and_heirlooms_go_by_the_will()
    {
        var (_, w, me, mine) = Start(); foreach (var c in WealthSystem.Members(w, mine).Where(c => c.Id != me.Id).ToList()) c.Alive = false;
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id).ToList()) c.Alive = false;
        var elder = Kin(w, mine, me, "elder"); elder.Age = 40; var younger = Kin(w, mine, me, "younger"); younger.Age = 25;
        var item = new Heirloom { Id = "test_item", Name = "the test sword", Kind = "sword", HolderId = me.Id, OfHouseId = mine.Id, Value = 20 }; w.Heirlooms.RemoveAll(h => h.HolderId == me.Id); w.Heirlooms.Add(item);
        me.Purse = 100; mine.Treasury.Credits = 200;
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Will, younger.Id, "50"))); Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Bequeath, item.Id, younger.Id)));
        Assert.False(Do(w, new WealthCommand(me.Id, WealthCommand.Will, younger.Id, "33")));
        DynastySystem.Die(w, me.Id);
        Assert.Equal(elder.Id, w.ProtagonistId);   // the house, by primogeniture
        Assert.Equal(50, younger.Purse); Assert.Equal(50, elder.Purse); Assert.Equal(younger.Id, item.HolderId); Assert.Equal(200, mine.Treasury.Credits);
        Assert.True(w.HasFlag(WealthSystem.WillReadFlag)); Assert.True(w.HasFlag(WealthSystem.WillContestedFlag)); Assert.Contains(WealthSystem.LegateeChar, younger.Flags);
    }

    [Fact]
    public void An_estate_is_kept_whole_or_shared_out_and_a_dead_heads_debts_are_the_houses()
    {
        var (_, w, me, mine) = Start(); foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id).ToList()) c.Alive = false;
        var elder = Kin(w, mine, me, "elder"); elder.Age = 40; var a = Kin(w, mine, me, "a"); var b = Kin(w, mine, me, "b"); w.Heirlooms.Clear();
        Assert.True(Do(w, new WealthCommand(me.Id, WealthCommand.Inheritance, "", WealthSystem.Portions)));
        mine.Treasury.Credits = 200; me.Purse = -40;
        DynastySystem.Die(w, me.Id);
        Assert.Equal(elder.Id, w.ProtagonistId); Assert.Equal(16, a.Purse); Assert.Equal(16, b.Purse);   // a tenth each of the 160 left once the debt was paid
        Assert.Equal(200 - 40 - 32, mine.Treasury.Credits); Assert.Equal(0, elder.Purse);
    }

    [Fact]
    public void A_house_with_purses_saves_loads_and_replays_the_same()
    {
        string Play(bool reload)
        {
            var (tc, w, me, mine) = Start("the_old_lion", 5); Do(w, new WealthCommand(me.Id, WealthCommand.Allowance, "", WealthSystem.Generous)); Do(w, new WealthCommand(me.Id, WealthCommand.Move, "", "5"));
            for (int i = 0; i < 10; i++)
            {
                tc.EndTurn();
                if (reload && i == 4) { w = SaveSystem.Deserialize(SaveSystem.Serialize(w)); tc = new TurnController(w, Content); }
                if (w.IsDynastyDead) break; tc.BeginTurn();
            }
            var h = w.House(w.Protagonist!.HouseId)!;
            return $"{w.RngState}|{h.Treasury.Credits}|{string.Join(",", WealthSystem.Members(w, h).Select(c => c.Id + ":" + c.Purse))}|{string.Join(",", w.Heirlooms.Select(x => x.Id + ">" + x.HolderId))}|{h.Allowance}";
        }
        Assert.Equal(Play(false), Play(true));
    }
}
