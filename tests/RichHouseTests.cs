using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Things worth a rich house's money (docs/WEALTH.md, the section of that name): land settled on one of the house, the
/// seat of the house, a house at the capital, and coin behind a canvass; and that other houses do the same.</summary>
public class RichHouseTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Protagonist!.Age = Math.Min(w.Protagonist.Age, 45);
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static bool Do(CommandContext ctx, ICommand cmd) => new CommandExecutor().TryExecute(cmd, ctx);
    /// <summary>One of the house: grown, in a service, steady (so the year's spending does not move their purse about).</summary>
    private static Character Kin(World w, House h, string id, int age = 30)
    {
        var head = w.Protagonist!;
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "male", CareerTrack = age >= 16 ? "law" : "", CareerRank = age >= 16 ? 2 : 0, FatherId = head.Sex == "female" ? "" : head.Id, MotherId = head.Sex == "female" ? head.Id : "" };
        c.NatureTraits.Add("Honorable"); c.Skills["oratory"] = 9; c.Skills["charisma"] = 9; c.Skills["discipline"] = 3; w.Characters[id] = c; h.Members.Add(id); w.Relationship(id, head.Id).Disposition = 30; return c;
    }
    private static int Regard(World w, Character of, Character toward) => w.GetRelationship(of.Id, toward.Id)?.Disposition ?? 0;
    private static House Other(World w, House mine) => w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).First();

    // ---------------- land settled on one of the house ----------------

    [Fact]
    public void The_head_settles_land_on_one_of_the_house_and_it_is_theirs_for_life()
    {
        var (_, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "kin"); var wife = Kin(w, mine, "wife"); wife.Sex = "female"; w.Relationship(k.Id, wife.Id).Bond = BondType.Marriage;
        mine.Treasury.Credits = 200; int regard = Regard(w, k, me), hers = Regard(w, wife, me);
        // who may, and on whom
        Assert.Equal("only the head of the house settles its land", new EndowCommand(k.Id, wife.Id).Refusal(ctx));
        Assert.Equal("not of your house", new EndowCommand(me.Id, Economy.HeadOf(w, Other(w, mine))!.Id).Refusal(ctx));
        Assert.Equal("an estate is settled on one of the house, not on its head", new EndowCommand(me.Id, me.Id).Refusal(ctx));
        // the first costs fifteen years of what it pays, and returns no more than land is meant to
        Assert.Equal(WealthSystem.EstatePrice, WealthSystem.EstateCost(w, k)); Assert.Equal(15, WealthSystem.EstatePrice / WealthSystem.EstatePays);
        Assert.InRange(WealthSystem.EstatePays / (double)WealthSystem.EstatePrice, 0.06, 0.07);
        Assert.True(Do(ctx, new EndowCommand(me.Id, k.Id)));
        Assert.Equal(200 - WealthSystem.EstatePrice, mine.Treasury.Credits);
        var land = Assert.Single(WealthSystem.Settled(w, mine.Id)); Assert.Equal(k.Id, land.HolderId); Assert.Equal(WealthSystem.EstateKind, land.Kind); Assert.Equal(mine.Id, land.OfHouseId);
        Assert.Equal(WealthSystem.EstatePrice * WealthSystem.MadeWorthPct / 100, land.Value);   // sold again, it loses money
        Assert.Equal(regard + WealthSystem.EstateRegard, Regard(w, k, me)); Assert.Equal(hers + WealthSystem.EstateSpouseRegard, Regard(w, wife, me));
        Assert.Contains(w.History, h => h.Text.Contains("settles it on " + k.Name) && h.Text.Contains("3,000 credits"));
        Assert.Contains("200 credits a year", WealthSystem.Does(w, land));
        // each further one is dearer, and three is the most on one person
        Assert.Equal(WealthSystem.EstatePrice + WealthSystem.EstateDearer, WealthSystem.EstateCost(w, k));
        Assert.True(Do(ctx, new EndowCommand(me.Id, k.Id))); Assert.Equal(WealthSystem.EstatePrice + 2 * WealthSystem.EstateDearer, WealthSystem.EstateCost(w, k));
        mine.Treasury.Credits = 10; Assert.Equal("it costs 4,200 credits and the treasury holds 1,000", new EndowCommand(me.Id, k.Id).Refusal(ctx));
        mine.Treasury.Credits = 100; Assert.True(Do(ctx, new EndowCommand(me.Id, k.Id)));
        Assert.Equal("they hold 3 estates already, and no more is settled on one person", new EndowCommand(me.Id, k.Id).Refusal(ctx));
        Assert.Equal(3, WealthSystem.EstatesHeld(w, k.Id)); Assert.Equal(3 * WealthSystem.EstatePays, WealthSystem.EstateIncome(w, k.Id));
        // it pays into their own purse each year
        k.Purse = 0; WealthSystem.Tick(w);
        Assert.Equal(WealthSystem.Pay(k) + WealthSystem.AllowanceOf(mine.Allowance) + 3 * WealthSystem.EstatePays, k.Purse);
        // and when they die it goes as their own things go: to the head
        k.Alive = false; WealthSystem.Tick(w); Assert.All(w.Heirlooms.Where(h => h.Id.StartsWith(WealthSystem.SettledPrefix)), h => Assert.Equal(me.Id, h.HolderId));
    }

    [Fact]
    public void Land_settled_on_a_child_is_kept_for_them_and_counts_with_a_family_asked_for_a_match()
    {
        var (_, w, me, mine, ctx) = Start(); var child = Kin(w, mine, "child", 9); mine.Treasury.Credits = 100;
        Assert.True(Do(ctx, new EndowCommand(me.Id, child.Id))); Assert.Contains(w.History, h => h.Text.Contains("kept for them until they are of age"));
        Assert.Equal(0, child.Purse); WealthSystem.Tick(w); WealthSystem.Tick(w);
        Assert.Equal(2 * WealthSystem.EstatePays, child.Purse); Assert.Equal("Kept for them until they are of age.", child.LastSpend);
        Assert.DoesNotContain(child, WealthSystem.Members(w, mine));   // no allowance and no purse to give to: only what their land brings
        // a grown son's land is weighed by the family of the one he is to marry
        var son = Kin(w, mine, "son", 24); var bride = w.LivingCharacters().Where(c => c.HouseId != mine.Id && !World.Outsider(c.HouseId) && c.Sex == "female" && c.Age is >= 18 and <= 40 && DynastySystem.Spouse(w, c) == null).OrderBy(c => c.Id, StringComparer.Ordinal).First();
        int before = CourtshipSystem.TreatyAnswer(w, son, bride).score; Assert.Equal(0, WealthSystem.EstateMatchOf(w, son));
        Assert.True(Do(ctx, new EndowCommand(me.Id, son.Id))); Assert.Equal(before + WealthSystem.EstateMatch, CourtshipSystem.TreatyAnswer(w, son, bride).score);
        mine.Treasury.Credits = 200; Assert.True(Do(ctx, new EndowCommand(me.Id, son.Id))); Assert.True(Do(ctx, new EndowCommand(me.Id, son.Id)));
        Assert.Equal(WealthSystem.EstateMatchMost, WealthSystem.EstateMatchOf(w, son)); Assert.Equal(before + WealthSystem.EstateMatchMost, CourtshipSystem.TreatyAnswer(w, son, bride).score);
    }

    [Fact]
    public void What_land_costs_follows_how_easy_money_is_and_a_seat_does_not()
    {
        var (_, w, _, mine, _) = Start(); var k = Kin(w, mine, "kin");
        w.MoneyEase = MoneySettings.Generous; Assert.Equal(20, WealthSystem.EstateCost(w, k));
        w.MoneyEase = MoneySettings.Lean; Assert.Equal(43, WealthSystem.EstateCost(w, k));
        w.MoneyEase = MoneySettings.Lavish; Assert.Equal(12, WealthSystem.EstateCost(w, k)); Assert.Equal(60, WealthSystem.SeatCost(1));
    }

    // ---------------- the seat ----------------

    [Fact]
    public void A_seat_is_built_in_three_grades_on_a_world_of_the_houses_own()
    {
        var (_, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "kin"); var world = WealthSystem.SeatSite(w, mine)!; var theirs = w.HoldingsOf(Other(w, mine).Id).First(h => h.IsPlanet);
        Assert.Equal(new[] { 60, 150, 400 }, new[] { 1, 2, 3 }.Select(WealthSystem.SeatCost)); Assert.Equal(new[] { 1, 3, 6 }, new[] { 1, 2, 3 }.Select(WealthSystem.SeatKeep));
        Assert.Equal(new[] { "manor", "great house", "palace" }, new[] { 1, 2, 3 }.Select(WealthSystem.SeatName)); Assert.All(new[] { 1, 2, 3 }, g => Assert.True(WealthSystem.SeatGives(g).Length > 30));
        mine.Treasury.Credits = 59;
        Assert.Equal("only the head of the house builds its seat", new BuildSeatCommand(k.Id, world.Id).Refusal(ctx));
        Assert.Equal("the seat stands on a world of the house's own", new BuildSeatCommand(me.Id, theirs.Id).Refusal(ctx));
        Assert.Equal("a manor costs 6,000 credits and the treasury holds 5,900", new BuildSeatCommand(me.Id, world.Id).Refusal(ctx));
        mine.Treasury.Credits = 1000; int room = HouseholdSystem.Room(mine), old = PolicySystem.Approval(w, mine.Id, "old_blood"), regard = Regard(w, k, me);
        Assert.True(Do(ctx, new BuildSeatCommand(me.Id, world.Id)));
        Assert.Equal(1, mine.SeatGrade); Assert.Equal(world.Id, mine.SeatHoldingId); Assert.Equal(940, mine.Treasury.Credits); Assert.Equal(world.Id, WealthSystem.SeatWorld(w, mine)!.Id);
        Assert.Equal(room + 1, HouseholdSystem.Room(mine)); Assert.Equal(old + WealthSystem.SeatSway, PolicySystem.Approval(w, mine.Id, "old_blood")); Assert.Equal(regard + WealthSystem.SeatRegard, Regard(w, k, me));
        Assert.Contains(w.History, h => h.Text.Contains("builds a manor on " + world.Name));
        Assert.Contains(HouseAccounts.Outgoings(w, Content.Holdings, mine), l => l.what == "the keep of the manor" && l.cost.Credits == 1);
        // the next two stand where the first does, whatever world is named
        Assert.True(Do(ctx, new BuildSeatCommand(me.Id, ""))); Assert.Equal(2, mine.SeatGrade); Assert.Equal(790, mine.Treasury.Credits);
        Assert.True(Do(ctx, new BuildSeatCommand(me.Id, theirs.Id))); Assert.Equal(3, mine.SeatGrade); Assert.Equal(world.Id, mine.SeatHoldingId); Assert.Equal(390, mine.Treasury.Credits);
        Assert.Equal(room + 3, HouseholdSystem.Room(mine));
        Assert.Equal("the house has its palace: there is nothing grander to build", new BuildSeatCommand(me.Id, world.Id).Refusal(ctx));
        // a house with no world has nowhere to build
        var (_, w2, me2, mine2, ctx2) = Start(); foreach (var h in w2.HoldingsOf(mine2.Id).ToList()) h.OwnerId = Other(w2, mine2).Id; w2.HoldingsChanged(); mine2.Treasury.Credits = 1000;
        Assert.Equal("the house holds no world to build a seat on", new BuildSeatCommand(me2.Id, "").Refusal(ctx2));
    }

    [Fact]
    public void A_seat_costs_its_keep_mends_the_houses_standing_receives_guests_and_is_lost_with_its_world()
    {
        // the keep: two houses the same but for a manor are a manor's price and a year's keep apart after a year
        int After(bool seat)
        {
            var (_, wk, mek, minek, ctxk) = Start(); minek.Treasury.Credits = 500; wk.Heirlooms.Clear();
            if (seat) Assert.True(Do(ctxk, new BuildSeatCommand(mek.Id, WealthSystem.SeatSite(wk, minek)!.Id)));
            WealthSystem.Tick(wk); return minek.Treasury.Credits;
        }
        Assert.Equal(After(false) - WealthSystem.SeatCost(1) - WealthSystem.SeatKeep(1), After(true));

        var (_, w, me, mine, ctx) = Start(); w.Heirlooms.Clear(); mine.Treasury.Credits = 2000; var world = WealthSystem.SeatSite(w, mine)!;
        Assert.True(Do(ctx, new BuildSeatCommand(me.Id, world.Id))); mine.Legitimacy = 30; WealthSystem.Tick(w); Assert.Equal(30, mine.Legitimacy);   // a manor mends nothing
        Assert.True(Do(ctx, new BuildSeatCommand(me.Id, world.Id))); WealthSystem.Tick(w); Assert.Equal(31, mine.Legitimacy);
        mine.Legitimacy = WealthSystem.SeatMendsBelow; WealthSystem.Tick(w); Assert.Equal(WealthSystem.SeatMendsBelow, mine.Legitimacy);
        // a palace mends further, and receives the head of the greatest house that is not yet a friend
        Assert.True(Do(ctx, new BuildSeatCommand(me.Id, world.Id))); WealthSystem.Tick(w); Assert.Equal(WealthSystem.SeatMendsBelow + 1, mine.Legitimacy);
        var guest = WealthSystem.Guest(w, mine, me)!; Assert.NotEqual(mine.Id, guest.HouseId); int theirs = Regard(w, guest, me), ours = Regard(w, me, guest);
        WealthSystem.Tick(w); Assert.True(Regard(w, guest, me) >= theirs + WealthSystem.GuestRegard); Assert.True(Regard(w, me, guest) >= ours + WealthSystem.GuestRegard);
        Assert.Contains(w.History, h => h.Text.Contains(guest.Name) && h.Text.Contains("is received at the palace on " + world.Name));
        w.Relationship(guest.Id, me.Id).Disposition = WealthSystem.GuestCeiling; Assert.NotEqual(guest.Id, WealthSystem.Guest(w, mine, me)?.Id);   // a friend already: the next is asked
        // unpaid, it is shut up and gives nothing; paid again, it opens
        mine.Treasury.Credits = 0; mine.Legitimacy = 20; int kept = mine.Legitimacy; WealthSystem.Tick(w);
        Assert.True(WealthSystem.SeatShut(w, mine)); Assert.Equal(kept, mine.Legitimacy); Assert.Equal(3, mine.SeatGrade); Assert.Contains(w.History, h => h.Text.Contains("The house is shut up"));
        mine.Treasury.Credits = 100; WealthSystem.Tick(w); Assert.False(WealthSystem.SeatShut(w, mine)); Assert.Equal(kept + 1, mine.Legitimacy);
        // and it goes with the world it stands on
        world.OwnerId = Other(w, mine).Id; w.HoldingsChanged(); mine.Legitimacy = 50; WealthSystem.Tick(w);
        Assert.Equal(0, mine.SeatGrade); Assert.Equal("", mine.SeatHoldingId); Assert.Equal(50 - 3 * WealthSystem.SeatLostLegitimacy, mine.Legitimacy);
        Assert.Contains(w.News, n => n.Text.Contains("is lost with it") && n.Tone == "warn");
    }

    // ---------------- a house at the capital ----------------

    [Fact]
    public void A_house_at_the_capital_costs_five_years_of_its_holders_income_and_is_theirs()
    {
        var (_, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "kin"); var k2 = Kin(w, mine, "kin2"); var idle = Kin(w, mine, "idle"); idle.CareerTrack = ""; idle.CareerRank = 0; var child = Kin(w, mine, "child", 10);
        // what a person takes in: their service, the allowance, their land, a post of the council
        Assert.Equal(WealthSystem.Pay(k) + WealthSystem.AllowanceOf(mine.Allowance), WealthSystem.YearlyIncome(w, k)); Assert.Equal(WealthSystem.Pay(me) + WealthSystem.EstateIncome(w, me.Id), WealthSystem.YearlyIncome(w, me));
        int cost = WealthSystem.TownHouseCost(w, k); Assert.Equal(WealthSystem.TownHouseYears * WealthSystem.YearlyIncome(w, k), cost); Assert.InRange(WealthSystem.TownHouseYears, 4, 6);
        Assert.Equal(WealthSystem.TownHouseLeast, WealthSystem.TownHouseCost(w, idle));   // never less than the least such a house sells for
        mine.Treasury.Credits = 100; Assert.True(Do(ctx, new EndowCommand(me.Id, k2.Id))); Assert.Equal(cost + WealthSystem.TownHouseYears * WealthSystem.EstatePays, WealthSystem.TownHouseCost(w, k2));   // the richer live dearer
        // refusals
        mine.Treasury.Credits = cost - 1; me.Purse = cost - 1;
        Assert.Equal("only the head of the house buys for its people", new TownHouseCommand(k.Id, k2.Id, false).Refusal(ctx));
        Assert.Equal("they are a child: a house at the capital is for one who is grown", new TownHouseCommand(me.Id, child.Id, false).Refusal(ctx));
        Assert.Equal($"it costs {Money.Credits(cost)} and the treasury holds {Money.Show(cost - 1)}", new TownHouseCommand(me.Id, k.Id, false).Refusal(ctx));
        Assert.Equal($"it costs {Money.Credits(cost)} and your purse holds {Money.Show(cost - 1)}", new TownHouseCommand(me.Id, k.Id, true).Refusal(ctx));
        // from the treasury
        mine.Treasury.Credits = 100; int regard = Regard(w, k, me); var rung = Offices.Rung("quaestor")!; var one = new List<Character> { k };
        int votes = LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id];
        Assert.True(Do(ctx, new TownHouseCommand(me.Id, k.Id, false))); Assert.Equal(100 - cost, mine.Treasury.Credits);
        var house = WealthSystem.TownHouseOf(w, k.Id)!; Assert.Equal(WealthSystem.TownHouse, house.Kind); Assert.Equal(cost * WealthSystem.MadeWorthPct / 100, house.Value); Assert.True(WealthSystem.AtTheCapital(w, k.Id));
        Assert.Equal(regard + WealthSystem.TownHouseRegard, Regard(w, k, me)); Assert.Contains("at the capital", WealthSystem.Does(w, house));
        Assert.Equal(votes + WealthSystem.TownHouseVotes, LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id]);   // their canvass is worth more
        Assert.Equal("they have a house at the capital already", new TownHouseCommand(me.Id, k.Id, false).Refusal(ctx));
        // from the head's own purse, and it is remembered whose money it was
        me.Purse = 200; int was = Regard(w, k2, me), k2Cost = WealthSystem.TownHouseCost(w, k2); Assert.True(Do(ctx, new TownHouseCommand(me.Id, k2.Id, true)));
        Assert.Equal(200 - k2Cost, me.Purse); Assert.Equal(100 - cost, mine.Treasury.Credits); Assert.Equal(was + WealthSystem.TownHousePurseRegard, Regard(w, k2, me));
        // the head may have one too
        int myCost = WealthSystem.TownHouseCost(w, me); mine.Treasury.Credits = myCost; Assert.True(Do(ctx, new TownHouseCommand(me.Id, me.Id, false))); Assert.Equal(0, mine.Treasury.Credits);
        Assert.Equal("you have a house at the capital already", new TownHouseCommand(me.Id, me.Id, true).Refusal(ctx));
        // it is theirs: given away, whoever has it is the one at the capital
        Assert.Null(WealthSystem.GiveRefusal(w, k, house, idle)); WealthSystem.Give(w, k, house, idle); Assert.False(WealthSystem.AtTheCapital(w, k.Id)); Assert.True(WealthSystem.AtTheCapital(w, idle.Id));
        Assert.Equal(votes, LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id]);
    }

    [Fact]
    public void One_of_the_house_who_is_standing_buys_their_own_house_at_the_capital_when_they_can()
    {
        var (_, w, _, mine, _) = Start(); var k = Kin(w, mine, "kin"); var quiet = Kin(w, mine, "quiet"); int cost = WealthSystem.TownHouseCost(w, k);
        w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = k.Id }); k.Purse = cost + WealthSystem.KinHouseKeepsBack; quiet.Purse = k.Purse; mine.Treasury.Credits = 50;
        WealthSystem.Tick(w);
        Assert.True(WealthSystem.AtTheCapital(w, k.Id)); Assert.Contains("Bought a house at", k.LastSpend); Assert.Contains(w.History, h => h.Text.Contains(k.Name + " buys a house at"));
        Assert.False(WealthSystem.AtTheCapital(w, quiet.Id));   // one with no business there keeps their money
        Assert.Equal(WealthSystem.KinHouseKeepsBack + WealthSystem.YearlyIncome(w, k), k.Purse);   // out of their own purse, after the year's income came in
    }

    // ---------------- coin behind a canvass ----------------

    [Fact]
    public void The_head_pays_for_a_canvass_in_three_sizes_with_falling_returns()
    {
        var (_, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "kin"); var other = Kin(w, mine, "other"); mine.Treasury.Credits = 0;
        Assert.Equal(new[] { 5, 15, 40 }, LadderSystem.CampaignSizes.Select(LadderSystem.CampaignCost)); Assert.Equal(new[] { 3, 6, 10 }, LadderSystem.CampaignSizes.Select(LadderSystem.CampaignVotes));
        var perCoin = LadderSystem.CampaignSizes.Select(s => LadderSystem.CampaignVotes(s) / (double)LadderSystem.CampaignCost(s)).ToList(); Assert.True(perCoin[0] > perCoin[1] && perCoin[1] > perCoin[2]);
        Assert.Equal("they are not standing", new FundCampaignCommand(me.Id, k.Id, LadderSystem.Modest, Offices).Refusal(ctx));
        Assert.Equal("you are not standing", new FundCampaignCommand(me.Id, me.Id, LadderSystem.Modest, Offices).Refusal(ctx));
        w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = k.Id });
        Assert.Equal("only the head of the house spends its treasury on an election", new FundCampaignCommand(other.Id, k.Id, LadderSystem.Modest, Offices).Refusal(ctx));
        Assert.Equal("no such canvass", new FundCampaignCommand(me.Id, k.Id, "grand", Offices).Refusal(ctx));
        Assert.Equal("it costs 500 credits and the treasury holds 0", new FundCampaignCommand(me.Id, k.Id, LadderSystem.Modest, Offices).Refusal(ctx));
        mine.Treasury.Credits = 100; var rung = Offices.Rung("quaestor")!; var one = new List<Character> { k }; int votes = LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id], regard = Regard(w, k, me);
        // modest
        Assert.True(Do(ctx, new FundCampaignCommand(me.Id, k.Id, LadderSystem.Modest, Offices)));
        var fund = LadderSystem.CampaignOf(w, k.Id)!; Assert.Equal(95, mine.Treasury.Credits); Assert.Equal((LadderSystem.Modest, 5, 3, "quaestor", mine.Id), (fund.Size, fund.Spent, fund.Votes, fund.RungId, fund.HouseId));
        Assert.Equal(votes + 3, LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id]); Assert.Equal(regard + LadderSystem.CampaignRegard, Regard(w, k, me)); Assert.Empty(w.OfficeRecord.Where(d => d.What == LadderSystem.BoughtElection));
        Assert.Equal("a modest canvass is paid for already: only a larger one adds to it", new FundCampaignCommand(me.Id, k.Id, LadderSystem.Modest, Offices).Refusal(ctx));
        // raised to handsome for the difference; it is in addition to speaking for them
        Assert.True(Do(ctx, new FundCampaignCommand(me.Id, k.Id, LadderSystem.Handsome, Offices))); Assert.Equal(85, mine.Treasury.Credits); Assert.Single(w.Campaigns);
        w.PoolsFor(me.Id).Influence = 9; Assert.True(Do(ctx, new LadderCommand(me.Id, LadderCommand.Back, k.Id, "", Offices)));
        Assert.Equal(votes + 6 + LadderSystem.BackVotes, LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id]);
        // lavish: the most coin can buy, and it goes on the record against the candidate, to be charged at once
        Assert.True(Do(ctx, new FundCampaignCommand(me.Id, k.Id, LadderSystem.Lavish, Offices))); Assert.Equal(60, mine.Treasury.Credits);
        Assert.Equal(votes + 10 + LadderSystem.BackVotes, LadderSystem.Tally(w, Offices, rung, one, _ => 0)[k.Id]); Assert.Equal(regard + 3 * LadderSystem.CampaignRegard + 6, Regard(w, k, me));
        var deed = Assert.Single(w.OfficeRecord.Where(d => d.What == LadderSystem.BoughtElection)); Assert.Equal((k.Id, mine.Id, "quaestor"), (deed.CharacterId, deed.HouseId, deed.RungId)); Assert.True(LadderSystem.ChargedAtOnce(deed));
        Assert.Contains("bought votes to be made quaestor", LadderSystem.DeedSays(w, Offices, deed)); Assert.Contains(deed, LadderSystem.Chargeable(w, Other(w, mine).Id));
        Assert.Equal("a lavish canvass is paid for: there is nothing more that coin can buy", new FundCampaignCommand(me.Id, k.Id, LadderSystem.Lavish, Offices).Refusal(ctx));
        Assert.Contains(w.History, h => h.Text.Contains("a lavish canvass, and one that will be talked of"));
        // the head's own canvass can be paid for too
        w.Candidacies.Add(new Candidacy { RungId = "praetor", CharacterId = me.Id }); int mineWas = Regard(w, me, me); Assert.True(Do(ctx, new FundCampaignCommand(me.Id, me.Id, LadderSystem.Modest, Offices)));
        Assert.Equal(mineWas, Regard(w, me, me)); Assert.Equal("praetor", LadderSystem.CampaignOf(w, me.Id)!.RungId);
        // under a throne there are no elections, and coin not yet counted goes with them
        w.Era = World.EraImperium; Assert.Equal("there are no elections under a throne", new FundCampaignCommand(me.Id, k.Id, LadderSystem.Modest, Offices).Refusal(ctx));
        LadderSystem.Tick(w, Offices); Assert.Empty(w.Campaigns);
        Assert.Equal(55, mine.Treasury.Credits);
    }

    [Fact]
    public void A_lavish_canvass_can_be_charged_before_a_praetor_and_the_candidate_answers_for_it()
    {
        var (_, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "kin"); mine.Treasury.Credits = 100; mine.Legitimacy = 50; w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = k.Id });
        Assert.True(Do(ctx, new FundCampaignCommand(me.Id, k.Id, LadderSystem.Lavish, Offices))); var deed = w.OfficeRecord.Single(d => d.What == LadderSystem.BoughtElection);
        var accuser = w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null && LadderSystem.Court(w, deed, h.Id) != null).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var judge = LadderSystem.Court(w, deed, accuser.Id)!; judge.NatureTraits.Add("Honorable");   // an upright praetor goes by the record
        Assert.Null(LadderSystem.ProsecuteRefusal(w, Economy.HeadOf(w, accuser), deed));
        Assert.True(LadderSystem.Prosecute(w, Offices, Economy.HeadOf(w, accuser)!, deed));
        Assert.True(deed.Answered); Assert.Equal(50 - LadderSystem.ConvictLegitimacy, mine.Legitimacy); Assert.Equal(60 - LadderSystem.FineCredits, mine.Treasury.Credits);
        Assert.Contains("convicted of buying an election", LadderSystem.Bar(w, Offices, k, Offices.Rung("quaestor"))); Assert.True(LadderSystem.Tainted(w, mine.Id));
        Assert.Contains(w.News, n => n.Text.Contains(k.Name + " is convicted of buying the election")); Assert.False(w.HasFlag(LadderSystem.KinExposedFlag));
        // their name comes down: one barred does not stand
        LadderSystem.Tick(w, Offices); Assert.DoesNotContain(w.Candidacies, c => c.CharacterId == k.Id);
    }

    [Fact]
    public void After_the_count_what_was_paid_for_is_kept_as_a_record_and_the_one_it_was_for_remembers()
    {
        var (tc, w, me, mine, ctx) = Start(); var strong = Kin(w, mine, "strong"); var weak = Kin(w, mine, "zz_weak", 38); /* an id that sorts last: level with another on everything else, this is the one not chosen */ weak.Skills.Clear(); weak.Cursus.Add("quaestor"); mine.Treasury.Credits = 100;
        w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = strong.Id }); w.Candidacies.Add(new Candidacy { RungId = "praetor", CharacterId = weak.Id }); Assert.Null(LadderSystem.Bar(w, Offices, weak, Offices.Rung("praetor")));
        Assert.True(Do(ctx, new FundCampaignCommand(me.Id, strong.Id, LadderSystem.Handsome, Offices))); Assert.True(Do(ctx, new FundCampaignCommand(me.Id, weak.Id, LadderSystem.Modest, Offices)));
        int owed = Regard(w, strong, me), sore = Regard(w, weak, me);
        w.Turn = 5; LadderSystem.Hold(w, Offices, id => id == mine.Id ? 0 : tc.Senate.Weight(w, id) + 20);
        Assert.True(w.Counts.Single(x => x.CharacterId == strong.Id).Won, "the strong candidate should have been chosen"); Assert.False(w.Counts.Single(x => x.CharacterId == weak.Id).Won);
        // both are kept, marked as they went; neither stands behind a canvass any more
        Assert.Equal(2, w.Campaigns.Count); Assert.All(w.Campaigns, f => Assert.True(f.Counted)); Assert.True(w.Campaigns.Single(f => f.CharacterId == strong.Id).Won); Assert.False(w.Campaigns.Single(f => f.CharacterId == weak.Id).Won);
        Assert.Null(LadderSystem.CampaignOf(w, strong.Id));
        // the one who won knows what they owe; the one who lost with the house's coin behind them does not blame the head for silence
        Assert.Equal(owed + LadderSystem.CampaignWonRegard, Regard(w, strong, me)); Assert.Equal(sore, Regard(w, weak, me)); Assert.DoesNotContain(LadderSystem.BlamesChar, weak.Flags);
        // one who stood with nothing at all behind them and lost does blame the head: that is as it was
        var (tc2, w2, me2, mine2, _) = Start(); var alone = Kin(w2, mine2, "zz_alone", 38); alone.Skills.Clear(); alone.Cursus.Add("quaestor"); w2.Candidacies.Add(new Candidacy { RungId = "praetor", CharacterId = alone.Id });
        w2.Turn = 5; LadderSystem.Hold(w2, Offices, id => id == mine2.Id ? 0 : tc2.Senate.Weight(w2, id) + 20); Assert.Contains(LadderSystem.BlamesChar, alone.Flags); _ = me2;
        // a record is not kept for ever
        w.Turn = 5 + LadderSystem.CampaignKept + 5; LadderSystem.Hold(w, Offices, id => tc.Senate.Weight(w, id)); Assert.Empty(w.Campaigns);
    }

    // ---------------- other houses ----------------

    [Fact]
    public void Other_houses_pay_for_their_own_candidates_the_year_before_the_count_and_it_is_told()
    {
        var (tc, w, me, mine, _) = Start(); var k = Kin(w, mine, "kin"); foreach (var r in Offices.Rungs) w.Candidacies.Add(new Candidacy { RungId = r.Id, CharacterId = k.Id });
        var others = w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        foreach (var h in others) { h.Treasury.Credits = 300; h.Debt = 0; }
        var poor = others[0]; poor.Treasury.Credits = LadderSystem.CampaignCost(LadderSystem.Modest) * LadderSystem.OthersCampaignAfford - 1;
        // not in any other year
        w.Turn = 3; LadderSystem.Tick(w, Offices); Assert.Empty(w.Campaigns);
        foreach (var h in others.Skip(1)) h.Treasury.Credits = 300;
        var had = others.ToDictionary(h => h.Id, h => h.Treasury.Credits);
        w.Turn = 4; Assert.Equal(1, LadderSystem.NextIn(w, Offices)); int news = w.News.Count; LadderSystem.Tick(w, Offices);
        var funds = w.Campaigns.ToList(); Assert.True(funds.Count >= 5, $"{funds.Count} houses paid for a canvass");
        Assert.DoesNotContain(funds, f => f.HouseId == poor.Id || f.HouseId == mine.Id); Assert.Equal(funds.Count, funds.Select(f => f.HouseId).Distinct().Count());   // one canvass to a house
        foreach (var f in funds)
        {
            var h = w.House(f.HouseId)!; var head = Economy.HeadOf(w, h)!; bool upright = head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just");
            Assert.Equal(LadderSystem.CampaignCost(f.Size), f.Spent); Assert.Equal(LadderSystem.CampaignVotes(f.Size), f.Votes); Assert.True(had[h.Id] - h.Treasury.Credits >= f.Spent, h.Name);
            Assert.Equal(upright ? LadderSystem.Handsome : LadderSystem.Lavish, f.Size);   // with coin to spare the upright stop short of buying it
            Assert.Equal(f.Size == LadderSystem.Lavish, w.OfficeRecord.Any(d => d.What == LadderSystem.BoughtElection && d.HouseId == h.Id));
        }
        Assert.Contains(funds, f => f.Size == LadderSystem.Lavish); Assert.Contains(w.History, x => x.Text.StartsWith("Coin is spent on the coming election"));
        Assert.True(w.News.Count > news && w.News.Skip(news).Any(n => n.Text.Contains("has put") && n.Text.Contains("against your house") && n.Target == "senate:ladder"));
        // what a house bought of another's candidate can be charged by the player's house
        Assert.Contains(LadderSystem.Chargeable(w, mine.Id), d => d.What == LadderSystem.BoughtElection);
        // at the count the coin is worth its votes to whoever of that house stands for the rung
        w.Turn = 5; var fund = funds.First(); var rung = Offices.Rung(fund.RungId)!; var standing = LadderSystem.Candidates(w, Offices, rung); var theirs = standing.FirstOrDefault(c => c.HouseId == fund.HouseId);
        Assert.NotNull(theirs); Assert.Equal(fund.Votes, LadderSystem.CampaignVotesFor(w, theirs!, rung.Id)); Assert.Equal(0, LadderSystem.CampaignVotesFor(w, k, rung.Id));
        int with = LadderSystem.Tally(w, Offices, rung, standing, _ => 0)[theirs!.Id]; fund.Votes = 0; Assert.Equal(with - LadderSystem.CampaignVotes(fund.Size), LadderSystem.Tally(w, Offices, rung, standing, _ => 0)[theirs.Id]); fund.Votes = LadderSystem.CampaignVotes(fund.Size);
        Assert.Contains(LadderSystem.RivalCampaigns(w, rung.Id), f => f.HouseId == fund.HouseId);
        var stands = LadderSystem.CountAsItStands(w, Offices, rung, id => tc.Senate.Weight(w, id)); Assert.Contains(stands, x => x.who.Id == theirs.Id); Assert.True(stands.Zip(stands.Skip(1)).All(p => p.First.votes >= p.Second.votes));
        // and after it other houses' are struck out
        LadderSystem.Hold(w, Offices, id => tc.Senate.Weight(w, id)); Assert.DoesNotContain(w.Campaigns, f => f.HouseId != mine.Id); _ = me;
    }

    [Fact]
    public void Another_house_that_is_rich_and_idle_builds_a_seat_settles_land_and_takes_a_house_at_the_capital()
    {
        var (_, w, _, mine, _) = Start(); w.Wars.Clear(); var rich = Other(w, mine); var owing = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != rich.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        foreach (var h in w.Houses.Values) h.Treasury.Credits = Math.Min(h.Treasury.Credits, 20);   // nobody else has it to spend
        var head = Economy.HeadOf(w, rich)!; var heir = new Character { Id = "their_heir", Name = "Their Heir", HouseId = rich.Id, Age = 12, Sex = "female", FatherId = head.Id }; w.Characters[heir.Id] = heir; rich.Members.Add(heir.Id);
        Assert.True(WealthSystem.Idle(w, rich)); owing.Debt = 5; Assert.False(WealthSystem.Idle(w, owing));
        int spent = 0;
        for (int y = 0; y < 24; y++)
        {
            w.Turn++; rich.Treasury.Credits = 2000; rich.Debt = 0; owing.Treasury.Credits = 2000; owing.Debt = 5; WealthSystem.Tick(w); spent += 2000 - rich.Treasury.Credits;
        }
        Assert.True(rich.SeatGrade >= 1, "a rich and idle house built no seat in twenty-four years"); Assert.Equal(WealthSystem.SeatSite(w, rich)!.Id, rich.SeatHoldingId);
        Assert.True(WealthSystem.EstatesHeld(w, heir.Id) >= 1, "it settled no land on its head's child"); Assert.True(WealthSystem.AtTheCapital(w, head.Id), "its head took no house at the capital");
        Assert.True(spent >= WealthSystem.SeatCost(1) + WealthSystem.EstatePrice + WealthSystem.TownHouseLeast);
        // one that owes money does none of it, however much it has in hand
        Assert.Equal(0, owing.SeatGrade); Assert.DoesNotContain(w.Heirlooms, h => h.OfHouseId == owing.Id && (h.Kind == WealthSystem.TownHouse || h.Id.StartsWith(WealthSystem.SettledPrefix)));
        // and it never spends what it could not pay for three times over
        var (_, w2, _, mine2, _) = Start(); w2.Wars.Clear(); var modest = Other(w2, mine2);
        for (int y = 0; y < 24; y++) { w2.Turn++; foreach (var h in w2.Houses.Values) { h.Treasury.Credits = WealthSystem.TownHouseLeast * WealthSystem.OthersAfford - 1; h.Debt = 0; } WealthSystem.Tick(w2); }
        Assert.Equal(0, modest.SeatGrade); Assert.Equal(0, w2.Counter(WealthSystem.SeatsKey) + w2.Counter(WealthSystem.EstatesKey) + w2.Counter(WealthSystem.TownHousesKey));
    }

    // ---------------- kept by a save, and the same every time ----------------

    [Fact]
    public void All_of_it_is_kept_by_a_save()
    {
        var (_, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "kin"); var child = Kin(w, mine, "child", 7); mine.Treasury.Credits = 1000; var world = WealthSystem.SeatSite(w, mine)!;
        w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = k.Id });
        Assert.True(Do(ctx, new EndowCommand(me.Id, child.Id))); Assert.True(Do(ctx, new BuildSeatCommand(me.Id, world.Id))); Assert.True(Do(ctx, new BuildSeatCommand(me.Id, world.Id)));
        Assert.True(Do(ctx, new TownHouseCommand(me.Id, k.Id, false))); Assert.True(Do(ctx, new FundCampaignCommand(me.Id, k.Id, LadderSystem.Lavish, Offices)));
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var house = back.House(mine.Id)!;
        Assert.Equal((2, world.Id, w.Turn), (house.SeatGrade, house.SeatHoldingId, house.SeatSince)); Assert.Equal(mine.Treasury.Credits, house.Treasury.Credits);
        var land = Assert.Single(WealthSystem.Settled(back, mine.Id)); Assert.Equal(child.Id, land.HolderId); Assert.Equal(WealthSystem.EstatePays, WealthSystem.EstateIncome(back, child.Id));
        Assert.True(WealthSystem.AtTheCapital(back, k.Id)); Assert.Equal(WealthSystem.TownHouseOf(w, k.Id)!.Value, WealthSystem.TownHouseOf(back, k.Id)!.Value);
        var fund = Assert.Single(back.Campaigns); Assert.Equal((mine.Id, k.Id, "quaestor", LadderSystem.Lavish, 40, 10, w.Turn, false, false), (fund.HouseId, fund.CharacterId, fund.RungId, fund.Size, fund.Spent, fund.Votes, fund.Turn, fund.Counted, fund.Won));
        Assert.Contains(back.OfficeRecord, d => d.What == LadderSystem.BoughtElection && d.CharacterId == k.Id);
        Assert.Equal(SaveSystem.Serialize(w), SaveSystem.Serialize(back));
    }

    [Fact]
    public void Twelve_years_with_every_house_spending_play_the_same_twice()
    {
        string Run()
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 11); var tc = new TurnController(w, Content);
            foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id))) h.Treasury.Credits += 400;   // rich enough that all of it happens
            for (int t = 0; t < 12; t++)
            {
                foreach (var item in tc.BeginTurn()) { var choice = tc.Offer(item).FirstOrDefault(c => c.Available); if (choice != null) tc.Resolve(item, choice.Choice.Id); }
                tc.EndTurn();
            }
            Assert.True(w.Counter(WealthSystem.SeatsKey) > 0 && w.Counter(WealthSystem.EstatesKey) > 0 && w.Counter(WealthSystem.TownHousesKey) > 0 && w.Counter(LadderSystem.CampaignsKey) > 0,
                $"seats {w.Counter(WealthSystem.SeatsKey)}, estates {w.Counter(WealthSystem.EstatesKey)}, town houses {w.Counter(WealthSystem.TownHousesKey)}, canvasses {w.Counter(LadderSystem.CampaignsKey)}");
            return SaveSystem.Serialize(w);
        }
        Assert.Equal(Run(), Run());
    }
}
