using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The three orders of the Republic (docs/REPUBLIC.md stage 3): standing measured from what a house holds, a calling, the
/// four doors to a seat, patrons and clients, sway over votes, canvassing.</summary>
public class OrderTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, House mine) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content);
        return (tc, w, w.House(w.Protagonist!.HouseId)!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static List<House> Others(World w, House mine) => w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    [Fact]
    public void Every_house_is_measured_in_three_orders_and_has_a_calling_and_an_origin()
    {
        var (tc, w, mine) = Start();
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId))
        {
            Assert.Equal(3, h.Orders.Count); Assert.Contains(h.Origin, new[] { "old", "new" });
            foreach (var o in OrderSystem.All) { var (score, parts) = tc.Orders.Measure(w, h, o); Assert.Equal(h.Order(o), score); Assert.Equal(Math.Min(100, parts.Sum(p => p.points)), score); Assert.InRange(score, 0, 100); }
            if (h.Orders.Values.Any(v => v > 0)) Assert.Contains(h.Calling, OrderSystem.All);
        }
        Assert.Contains(w.Houses.Values, h => h.Origin == "old"); Assert.Contains(w.Houses.Values, h => h.Origin == "new");
    }

    [Fact]
    public void Standing_follows_what_the_house_holds_and_the_calling_is_slow_to_change()
    {
        var (tc, w, mine) = Start(); int arms = mine.Order(OrderSystem.Arms), commerce = mine.Order(OrderSystem.Commerce);
        mine.Glory = 5; var fleet = w.Fleets.Values.FirstOrDefault(f => f.OwnerId == mine.Id);
        if (fleet != null) { fleet.Ships.Clear(); fleet.Strength = 8; }
        tc.Orders.Tick(w);
        Assert.True(mine.Order(OrderSystem.Arms) > arms);
        Assert.Contains(tc.Orders.Measure(w, mine, OrderSystem.Arms).parts, p => p.what.Contains("victories"));
        string calling = mine.Calling; mine.Orders[OrderSystem.Commerce] = mine.Order(calling) + OrderSystem.CallingMargin - 1;   // not far enough ahead to rename the family
        Assert.Equal(calling, mine.Calling);
        Assert.Equal(2, OrderSystem.Tier(OrderSystem.GreatAt)); Assert.Equal(1, OrderSystem.Tier(OrderSystem.NotableAt)); Assert.Equal(3, OrderSystem.Tier(OrderSystem.DominantAt)); Assert.Equal(0, OrderSystem.Tier(OrderSystem.NotableAt - 1));
    }

    [Fact]
    public void A_great_general_or_banker_carries_weight_in_the_chamber_whatever_their_rank()
    {
        var (tc, w, mine) = Start(); int plain = tc.Senate.Weight(w, mine.Id);
        int tiers = OrderSystem.Tier(mine, OrderSystem.Arms) + OrderSystem.Tier(mine, OrderSystem.Commerce);
        mine.Orders[OrderSystem.Arms] = OrderSystem.DominantAt; mine.Orders[OrderSystem.Commerce] = OrderSystem.GreatAt;
        Assert.Equal(plain - tiers + 5, tc.Senate.Weight(w, mine.Id));
    }

    [Fact]
    public void There_are_four_doors_to_a_seat_and_the_purse_is_one_of_them()
    {
        var (tc, w, mine) = Start(); var os = tc.Orders;
        var low = Others(w, mine).First(h => Content.Titles.RankOf(h.Title) < SenateSystem.ProposerTitleRank && !(Economy.HeadOf(w, h) is { CareerTrack: "law" } hd && hd.CareerRank >= SenateSystem.ProposerLawRank));
        low.Orders[OrderSystem.Arms] = 0; low.Orders[OrderSystem.Commerce] = 0; low.Enrolled = false;
        Assert.Null(tc.Senate.Door(w, low)); Assert.False(tc.Senate.MayPropose(w, low.Id));
        Assert.Contains("commerce", os.EnrollRefusal(w, low, false));
        low.Orders[OrderSystem.Commerce] = OrderSystem.NotableAt; low.Treasury.Credits = OrderSystem.CensusFee - 1;
        Assert.Contains("credits", os.EnrollRefusal(w, low, false));
        low.Treasury.Credits = OrderSystem.CensusFee + 5; var head = Economy.HeadOf(w, low)!;
        Assert.True(Do(w, new EnrollCommand(head.Id, os, Content.Titles)));
        Assert.True(low.Enrolled); Assert.Equal(5, low.Treasury.Credits); Assert.Equal("the purse", tc.Senate.Door(w, low)); Assert.True(tc.Senate.MayPropose(w, low.Id));
        Assert.False(Do(w, new EnrollCommand(head.Id, os, Content.Titles)));
        low.Enrolled = false; low.Orders[OrderSystem.Arms] = OrderSystem.GreatAt;
        Assert.Equal("the sword", tc.Senate.Door(w, low));
        Assert.Equal("land", tc.Senate.Door(w, Others(w, mine).First(h => Content.Titles.RankOf(h.Title) >= SenateSystem.ProposerTitleRank)));
    }

    [Fact]
    public void A_patron_pays_a_clients_debts_and_the_client_votes_as_the_patron_wishes()
    {
        var (tc, w, mine) = Start(); var os = tc.Orders; var others = Others(w, mine);
        var client = others[0]; var lender = others[1];
        mine.Treasury.Credits = 500; mine.Wealth = 800; client.Wealth = 30; client.Treasury.Credits = 5; client.Debt = 12; client.LenderId = lender.Id; client.PatronId = ""; mine.PatronId = "";
        foreach (var c in OrderSystem.ClientsOf(w, client.Id).ToList()) c.PatronId = "";
        int lent = lender.Treasury.Credits;
        Assert.Null(os.TakeClientRefusal(w, mine, client)); Assert.Equal(12 + OrderSystem.ClientGift, OrderSystem.ClientCost(client));
        Assert.True(Do(w, new ClientCommand(w.ProtagonistId, ClientCommand.Take, client.Id, os)));
        Assert.Equal(mine.Id, client.PatronId); Assert.Equal(0, client.Debt); Assert.Equal(lent + 12, lender.Treasury.Credits); Assert.Equal(500 - 22, mine.Treasury.Credits); Assert.Equal(5 + OrderSystem.ClientGift, client.Treasury.Credits);
        Assert.Contains(OrderSystem.ClientTakenFlag, w.WorldFlags);
        var (lean, why) = OrderSystem.Sway(w, client, mine); Assert.Equal(3, lean); Assert.Contains("its client", why);
        Assert.Contains(tc.Economy!.Compute(w, mine.Id, tc.Fleets).Lines, l => l.Kind == "clients" && l.Delta.Credits == -OrderSystem.ClientStipend);
        Assert.Contains(tc.Economy.Compute(w, client.Id, tc.Fleets).Lines, l => l.Kind == "clients" && l.Delta.Credits == OrderSystem.ClientStipend);
        // at an election the client's weight goes to the patron, whatever its party
        var nominees = new List<(string houseId, string? partyId)> { (mine.Id, null), (lender.Id, FactionSystem.Of(w, client.Id)?.Id ?? "x") };
        var tally = ElectionSystem.Tally(w, Content.Offices, id => id == client.Id ? 7 : 0, nominees);
        Assert.Equal(7, tally[mine.Id]);
        // a client that outgrows its patron is a client no longer
        client.Wealth = mine.Wealth + 1; os.Tick(w);
        Assert.Equal("", client.PatronId); Assert.Contains(OrderSystem.ClientLeftFlag, w.WorldFlags);
    }

    [Fact]
    public void A_house_may_seek_a_patron_who_thinks_well_of_it_and_leaving_is_not_forgiven()
    {
        var (tc, w, mine) = Start(); var os = tc.Orders; var patron = Others(w, mine)[0];
        mine.Wealth = 40; patron.Wealth = 500; mine.PatronId = ""; patron.PatronId = ""; foreach (var c in OrderSystem.ClientsOf(w, mine.Id).ToList()) c.PatronId = ""; foreach (var c in OrderSystem.ClientsOf(w, patron.Id).ToList()) c.PatronId = "";
        var ph = Economy.HeadOf(w, patron)!; w.Relationship(ph.Id, w.ProtagonistId).Disposition = 0;
        Assert.Contains("think well", os.SeekPatronRefusal(w, mine, patron));
        w.Relationship(ph.Id, w.ProtagonistId).Disposition = 20;
        Assert.True(Do(w, new ClientCommand(w.ProtagonistId, ClientCommand.Seek, patron.Id, os)));
        Assert.Equal(patron.Id, mine.PatronId); Assert.Contains(OrderSystem.BecameClientFlag, w.WorldFlags);
        Assert.Equal(2, OrderSystem.Sway(w, patron, mine).lean);   // a patron backs the client's cause
        int regard = w.Relationship(ph.Id, w.ProtagonistId).Disposition;
        Assert.True(Do(w, new ClientCommand(w.ProtagonistId, ClientCommand.Leave, "", os)));
        Assert.Equal("", mine.PatronId); Assert.Equal(regard - 30, w.Relationship(ph.Id, w.ProtagonistId).Disposition);
    }

    [Fact]
    public void A_debtor_and_a_fleet_that_burns_your_fuel_lean_your_way()
    {
        var (tc, w, mine) = Start(); var others = Others(w, mine); var debtor = others[0];
        debtor.PatronId = ""; mine.PatronId = ""; debtor.Debt = 0; mine.Corners.Clear();
        Assert.Equal(0, OrderSystem.Sway(w, debtor, mine).lean - (w.Companies.Any(c => c.ControllerId == mine.Id && c.HeldBy(debtor.Id) * 10 >= c.Shares) ? 1 : 0));
        debtor.Debt = 10; debtor.LenderId = mine.Id;
        Assert.Contains("in its debt", OrderSystem.Sway(w, debtor, mine).why);
        var sailor = others.First(h => w.Fleets.Values.Any(f => f.OwnerId == h.Id)); sailor.PatronId = ""; sailor.Debt = 0;
        mine.Corners.Add(Resource.Helium3);
        Assert.Contains(OrderSystem.Sway(w, sailor, mine).why, x => x.Contains("fuel"));
        Assert.Contains(OrderSystem.Swayed(w, mine), x => x.house.Id == debtor.Id);
        Assert.True(OrderSystem.Sway(w, debtor, mine).lean <= OrderSystem.SwayCap);
    }

    [Fact]
    public void Coin_spent_on_the_voters_counts_at_the_election_and_is_swept_after_it()
    {
        var (tc, w, mine) = Start(); var os = tc.Orders; mine.Treasury.Credits = 200; var other = Others(w, mine)[0];
        var nominees = new List<(string houseId, string? partyId)> { (other.Id, null) };
        int before = ElectionSystem.Tally(w, Content.Offices, _ => 0, nominees)[other.Id];
        Assert.False(Do(w, new CanvassCommand(w.ProtagonistId, mine.Id, os, _ => new[] { other.Id })));   // not a nominee
        for (int i = 0; i < OrderSystem.CanvassCap; i++) Assert.True(Do(w, new CanvassCommand(w.ProtagonistId, other.Id, os, _ => new[] { other.Id })));
        Assert.False(Do(w, new CanvassCommand(w.ProtagonistId, other.Id, os, _ => new[] { other.Id })));
        Assert.Equal(200 - OrderSystem.CanvassCap * OrderSystem.CanvassCredits, mine.Treasury.Credits);
        Assert.Equal(before + OrderSystem.CanvassCap, ElectionSystem.Tally(w, Content.Offices, _ => 0, nominees)[other.Id]);
        OrderSystem.SweepCanvass(w);
        Assert.Equal(before, ElectionSystem.Tally(w, Content.Offices, _ => 0, nominees)[other.Id]);
    }

    [Fact]
    public void A_victory_is_glory_and_glory_fades()
    {
        var (tc, w, mine) = Start(); mine.Glory = 3; int arms = tc.Orders.Measure(w, mine, OrderSystem.Arms).score;
        mine.Glory = 0; Assert.True(tc.Orders.Measure(w, mine, OrderSystem.Arms).score < arms);
        mine.Glory = 2; w.Turn = OrderSystem.GloryFadeEvery; tc.Orders.Tick(w); Assert.Equal(1, mine.Glory);
    }

    [Fact]
    public void Under_the_imperium_no_seat_is_bought_and_no_client_taken()
    {
        var (tc, w, mine) = Start("the_imperial_court"); var os = tc.Orders; var other = Others(w, mine)[0];
        Assert.True(w.IsImperium); mine.Orders[OrderSystem.Commerce] = 50; mine.Treasury.Credits = 500;
        Assert.Contains("throne", os.EnrollRefusal(w, mine, false));
        Assert.False(Do(w, new ClientCommand(w.ProtagonistId, ClientCommand.Take, other.Id, os)));
        Assert.All(w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId), h => Assert.Equal(3, h.Orders.Count));   // the orders are still measured
    }

    [Fact]
    public void Houses_that_depend_on_a_candidate_lean_toward_his_commission_and_against_his_censure()
    {
        var (tc, w, mine) = Start(); var voter = Others(w, mine)[0]; voter.PatronId = ""; mine.PatronId = ""; voter.Debt = 0; mine.Corners.Clear();
        var commission = tc.Senate.Add(w, mine.Id, "commission", mine.Id, "admiral"); var censure = tc.Senate.Add(w, Others(w, mine)[1].Id, "censure", mine.Id);
        int forBefore = tc.Senate.Lean(w, commission, voter.Id), againstBefore = tc.Senate.Lean(w, censure, voter.Id);
        voter.Debt = 10; voter.LenderId = mine.Id;
        Assert.True(tc.Senate.Lean(w, commission, voter.Id) >= forBefore + 3);    // for what the creditor wants, twice over: he moved it and it is his
        Assert.True(tc.Senate.Lean(w, censure, voter.Id) <= againstBefore - 2);   // and against what is aimed at him
    }
}
