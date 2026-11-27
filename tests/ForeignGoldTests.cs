using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Foreign gold in the Senate (docs/BEYOND.md, "Foreign gold in the Senate"; GAPS N11): the Free Cities' purse, the gold they send
/// senators before a war on them or on the Hollow Crown is voted, the three answers, a war bought off before the vote, what the gold does
/// to the count, and the record a rival can charge before a praetor.</summary>
public class ForeignGoldTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private sealed record Game(TurnController Tc, World W, House Mine, Character Me, CommandContext Ctx)
    {
        public SenateSystem S => Tc.Senate;
        public Nation Cities => W.Nation("thessar")!;
        public House H(string id) => W.House(id)!;
        public Character HeadOf(House h) => Economy.HeadOf(W, h)!;
        public int Seats(House h) => S.Weight(W, h.Id);
        public void Gold() => ForeignGoldSystem.Tick(W, Content.Nations, S, Content.Offices);
        public string? No(string offerId, string answer, string? who = null) => new ForeignGoldCommand(who ?? Me.Id, offerId, answer).Refusal(Ctx);
        public void Answer(string offerId, string answer) { var c = new ForeignGoldCommand(Me.Id, offerId, answer); Assert.True(Tc.Executor.TryExecute(c, Ctx), c.Refusal(Ctx)); }
        /// <summary>Lean a house on the motion by exactly this much, without declaring its vote.</summary>
        public void SetLean(Motion m, House h, int to) { m.Lobby.Remove(h.Id); int now = S.Lean(W, m, h.Id); m.Lobby[h.Id] = to - now; Assert.Equal(to, S.Lean(W, m, h.Id)); }
        /// <summary>Several at once: a house's lean moves with its bench leader's lobbying, so they are set until they all hold.</summary>
        public void SetLeans(Motion m, params (House h, int to)[] leans)
        {
            for (int i = 0; i < 6 && leans.Any(x => S.Lean(W, m, x.h.Id) != x.to); i++) foreach (var (h, to) in leans) m.Lobby[h.Id] = m.Lobby.GetValueOrDefault(h.Id) + to - S.Lean(W, m, h.Id);
            foreach (var (h, to) in leans) Assert.Equal(to, S.Lean(W, m, h.Id));
        }
        /// <summary>The Senate counts the motion now.</summary>
        public void Count(Motion m) { m.ProposedTurn = W.Turn - 1; m.HeldOver = true; Tc.Senate.Tick(W); Assert.True(m.Resolved && !m.Vetoed, "the motion was not counted"); }
        public bool Told(string part) => W.News.Any(n => n.Text.Contains(part));
    }
    private static Game Start(ulong seed = 5, string scenario = "the_old_lion")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), seed); var tc = new TurnController(w, Content);
        var me = w.Protagonist!; w.Turn = 3;
        var g = new Game(tc, w, w.House(me.HouseId)!, me, new CommandContext(w, WorldBuilder.RngFor(w)));
        var n = g.Cities; n.PurseOpened = true; n.Purse = ForeignGoldSystem.Cap(Content.Nations.Def("thessar")!); n.BoughtOffTurn = w.Turn;   // a full purse, and no war bought off: that is tested on its own
        return g;
    }
    private static void Nature(Character c, params string[] traits) { c.NatureTraits.Clear(); c.AptitudeTraits.Clear(); c.Ambition = ""; foreach (var t in traits) c.NatureTraits.Add(t); }
    /// <summary>The other houses of the Reach, heaviest first, leaving out the player's.</summary>
    private static List<House> Others(Game g) => g.W.Houses.Values.Where(h => g.W.OfTheReach(h.Id) && h.Id != g.Mine.Id && Economy.HeadOf(g.W, h) is { Alive: true }).OrderByDescending(h => g.Seats(h)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
    /// <summary>A war on the Cities moved by a light house; every house declared to stay away but those named, which are left to lean as set.</summary>
    private static Motion War(Game g, House mover, params House[] open)
    {
        var m = g.S.Add(g.W, mover.Id, "foreign_war", "thessar");
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mover.Id && h.Id != g.Mine.Id && !open.Contains(h))) m.Votes[h.Id] = 0;
        return m;
    }

    [Fact]
    public void The_Cities_keep_a_purse_for_the_Senate_and_it_fills_from_their_wealth_to_a_limit()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 5); var tc = new TurnController(w, Content);
        var d = Content.Nations.Def("thessar")!; Assert.Equal(12, d.SenateGold); Assert.Equal(new[] { "hollow_crown" }, d.Favours); Assert.Equal(108, ForeignGoldSystem.Cap(d));
        Assert.All(Content.Nations.Defs.Where(x => x.Id != "thessar"), x => Assert.Equal(0, x.SenateGold));   // only the Cities buy votes
        var n = w.Nation("thessar")!; Assert.False(n.PurseOpened);
        ForeignGoldSystem.Tick(w, Content.Nations, tc.Senate, Content.Offices); Assert.True(n.PurseOpened); Assert.Equal(d.Wealth * ForeignGoldSystem.StartYears, n.Purse);
        ForeignGoldSystem.Tick(w, Content.Nations, tc.Senate, Content.Offices); Assert.Equal(d.Wealth * (ForeignGoldSystem.StartYears + 1), n.Purse);
        n.AtWar = true; ForeignGoldSystem.Tick(w, Content.Nations, tc.Senate, Content.Offices); Assert.Equal(d.Wealth * (ForeignGoldSystem.StartYears + 1), n.Purse);   // at war the money goes on hulls
        n.AtWar = false; for (int i = 0; i < 20; i++) ForeignGoldSystem.Tick(w, Content.Nations, tc.Senate, Content.Offices); Assert.Equal(ForeignGoldSystem.Cap(d), n.Purse);
        // who pays: the Cities for a war on themselves or on the Crown; nobody for the Khanates; nobody once the Cities are fallen or a client
        Assert.Equal("thessar", ForeignGoldSystem.Payer(w, Content.Nations, "thessar")?.Id); Assert.Equal("thessar", ForeignGoldSystem.Payer(w, Content.Nations, "hollow_crown")?.Id);
        Assert.Null(ForeignGoldSystem.Payer(w, Content.Nations, "vashti"));
        n.Vassal = true; Assert.Null(ForeignGoldSystem.Payer(w, Content.Nations, "thessar")); n.Vassal = false; n.Fallen = true; Assert.Null(ForeignGoldSystem.Payer(w, Content.Nations, "hollow_crown"));
    }

    [Fact]
    public void Other_houses_answer_by_nature_and_by_money()
    {
        var g = Start(); var h = Others(g)[0]; var head = g.HeadOf(h); h.Treasury.Credits = 50;
        Nature(head, "Honorable"); Assert.Equal(ForeignGoldSystem.Expose, ForeignGoldSystem.ByNature(head, h));
        Nature(head, "Just", "Ruthless"); Assert.Equal(ForeignGoldSystem.Expose, ForeignGoldSystem.ByNature(head, h));   // the upright first
        Nature(head, "Ruthless"); Assert.Equal(ForeignGoldSystem.Take, ForeignGoldSystem.ByNature(head, h));
        Nature(head, "Cruel"); Assert.Equal(ForeignGoldSystem.Take, ForeignGoldSystem.ByNature(head, h));
        Nature(head); head.AptitudeTraits.Add("Greedy"); Assert.Equal(ForeignGoldSystem.Take, ForeignGoldSystem.ByNature(head, h));
        Nature(head); head.Ambition = "amass_a_fortune"; Assert.Equal(ForeignGoldSystem.Take, ForeignGoldSystem.ByNature(head, h));
        Nature(head, "Loyal"); Assert.Equal(ForeignGoldSystem.Refuse, ForeignGoldSystem.ByNature(head, h));
        h.Treasury.Credits = DealingsSystem.PinchedAt - 1; Assert.Equal(ForeignGoldSystem.Take, ForeignGoldSystem.ByNature(head, h));   // in want
    }

    [Fact]
    public void Before_a_close_war_vote_the_Cities_write_to_the_senators_who_could_turn_it_and_each_answers_by_nature()
    {
        var g = Start(); var o = Others(g); var c1 = o[0]; var c2 = o[1]; var c3 = o[2];
        var mover = o.Skip(3).Where(h => g.S.MayPropose(g.W, h.Id)).OrderBy(h => Math.Abs(g.Seats(h) - g.Seats(c2))).ThenBy(h => h.Id, StringComparer.Ordinal).First();   // heavy enough that all three are needed to turn it
        Nature(g.HeadOf(c1), "Ruthless"); Nature(g.HeadOf(c2), "Honorable"); Nature(g.HeadOf(c3), "Loyal"); c3.Treasury.Credits = 100;
        int trust = LadderSystem.Trust(g.W), legit = c2.Legitimacy, regard = g.Cities.Disposition, purse = g.Cities.Purse, takerPurse = g.HeadOf(c1).Purse;
        var m = War(g, mover, c1, c2, c3);
        g.SetLeans(m, (c1, 5), (c2, 1), (c3, 1), (g.Mine, 5));   // the player, too, wants the war: asked only to stay away
        g.Gold();
        var sent = g.W.GoldOffers.Where(x => x.MotionId == m.Id).ToList();
        var to1 = sent.Single(x => x.HouseId == c1.Id); var to2 = sent.Single(x => x.HouseId == c2.Id); var to3 = sent.Single(x => x.HouseId == c3.Id); var mine = sent.Single(x => x.HouseId == g.Mine.Id);
        Assert.Equal(4, sent.Count); Assert.DoesNotContain(sent, x => x.HouseId == mover.Id);
        // one who wants the war badly is asked only to stay away; the rest to vote against it; the sum by the seats
        Assert.Equal(ForeignGoldSystem.Away, to1.Ask); Assert.Equal(ForeignGoldSystem.AwayBase + ForeignGoldSystem.AwayPerSeat * g.Seats(c1), to1.Sum);
        Assert.Equal(ForeignGoldSystem.Against, to2.Ask); Assert.Equal(ForeignGoldSystem.AgainstBase + ForeignGoldSystem.AgainstPerSeat * g.Seats(c2), to2.Sum);
        Assert.Equal(1, to1.Before); Assert.Equal(g.Seats(c1), to1.Seats); Assert.Equal("the Free Cities of Thessar", to1.NationName);
        // the one for sale took it; the upright told the chamber; the other refused; the player is asked
        Assert.Equal(ForeignGoldSystem.Taken, to1.Answer); Assert.Equal(ForeignGoldSystem.Exposed, to2.Answer); Assert.Equal(ForeignGoldSystem.Refused, to3.Answer); Assert.Equal("", mine.Answer);
        Assert.Equal(takerPurse + to1.Sum, g.HeadOf(c1).Purse); Assert.Equal(purse - to1.Sum - mine.Sum, g.Cities.Purse);   // what was refused or told went home; the player's is set aside
        Assert.True(m.Bought.ContainsKey(c1.Id));   // a bench does not follow a leader the Cities paid
        Assert.Contains(g.W.OfficeRecord, d => d.What == ForeignGoldSystem.Deed && d.HouseId == c1.Id && d.CharacterId == g.HeadOf(c1).Id);
        Assert.Equal(trust + ForeignGoldSystem.ExposeTrust, LadderSystem.Trust(g.W)); Assert.Equal(legit + ForeignGoldSystem.ExposeLegitimacy, c2.Legitimacy); Assert.Equal(regard - ForeignGoldSystem.ExposeRegard, g.Cities.Disposition);
        Assert.True(g.W.HasFlag(ForeignGoldSystem.OfferedFlag)); Assert.True(g.Told("has called with")); Assert.True(g.Told("has told the Senate of the gold"));
        // what the player can know: their own, the one told to the chamber, and the one taken (the roll call gives it as the reason, and the courts list it); not the one refused quietly
        var known = ForeignGoldSystem.Known(g.W, m.Id, g.Mine.Id); Assert.Equal(new[] { g.Mine.Id }.Concat(new[] { c1.Id, c2.Id }.OrderBy(x => x, StringComparer.Ordinal)), known.Select(x => x.HouseId));
        Assert.Contains(LadderSystem.Chargeable(g.W, g.Mine.Id), d => d.HouseId == c1.Id && d.What == ForeignGoldSystem.Deed);
        string says = ForeignGoldSystem.KnownSays(g.W, known, "the Free Cities of Thessar", g.Mine.Id);
        Assert.Contains($"{g.H(c1.Id).Name}, which took it", says); Assert.Contains($"{g.H(c2.Id).Name}, which told the chamber", says); Assert.DoesNotContain(g.H(c3.Id).Name, says);
        g.W.Relationship(g.HeadOf(c3).Id, g.Me.Id).Disposition = ForeignGoldSystem.FriendTells; Assert.Contains(ForeignGoldSystem.Known(g.W, m.Id, g.Mine.Id), x => x.HouseId == c3.Id);   // a friend tells you
        // the gold on the count: the one who took it stays away; the chamber, told of it, leans a point more for the war
        Assert.Equal(0, g.S.Lean(g.W, m, c1.Id)); Assert.Contains(g.S.Why(g.W, m, c1.Id), x => x.why.Contains("taken gold to stay away"));
        Assert.Contains(g.S.Why(g.W, m, c3.Id), x => x.why.Contains("shown to the chamber") && x.by == ForeignGoldSystem.ExposeAnger);
        g.Count(m); Assert.Equal(0, m.Votes[c1.Id]);
        g.Gold();   // the count is settled: what waited on the player lapses and goes home
        Assert.Equal(ForeignGoldSystem.Lapsed, mine.Answer); Assert.True(sent.All(x => x.Settled)); Assert.False(g.W.HasFlag(ForeignGoldSystem.OfferedFlag));
        Assert.DoesNotContain(g.W.WorldFlags, x => x.StartsWith("foreign_gold", StringComparison.Ordinal));   // no flag of the gold outlives the year it was raised
        Assert.Equal(1, g.W.Counter(ForeignGoldSystem.TakenKey)); Assert.Equal(1, g.W.Counter(ForeignGoldSystem.ExposedKey)); Assert.Equal(1, g.W.Counter(ForeignGoldSystem.RefusedKey)); Assert.Equal(1, g.W.Counter(ForeignGoldSystem.LapsedKey));
    }

    [Fact]
    public void A_war_that_fails_anyway_or_that_gold_could_not_turn_is_not_written_about()
    {
        var g = Start(); var o = Others(g); var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last();
        var m = War(g, mover); foreach (var h in o.Where(h => h.Id != mover.Id).Take(3)) m.Votes[h.Id] = -1;   // it fails, and by a long way
        g.Gold(); Assert.DoesNotContain(g.W.GoldOffers, x => x.MotionId == m.Id);
        var m2 = g.S.Add(g.W, o[1].Id, "foreign_war", "hollow_crown"); g.W.Motions.Remove(m);
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != o[1].Id)) m2.Votes[h.Id] = 1;   // it carries by everyone, and every house has given its word
        g.Gold(); Assert.DoesNotContain(g.W.GoldOffers, x => x.MotionId == m2.Id);
        Assert.Equal(0, g.W.Counter(ForeignGoldSystem.MotionsKey));
    }

    [Fact]
    public void The_Cities_buy_off_a_war_before_it_is_voted_but_sparingly()
    {
        var g = Start(); g.Cities.BoughtOffTurn = 0; var o = Others(g); var mover = o.First(h => g.S.MayPropose(g.W, h.Id)); Nature(g.HeadOf(mover), "Ruthless");
        string key = "senate_standing:" + mover.Id; g.W.WorldCounters[key] = 3; int moverPurse = g.HeadOf(mover).Purse;
        var m = War(g, mover);   // only the mover is for it: it carries
        g.Gold();
        var bought = g.W.GoldOffers.Single(x => x.MotionId == m.Id);
        Assert.Equal(ForeignGoldSystem.Withdraw, bought.Ask); Assert.Equal(ForeignGoldSystem.WithdrawBase + ForeignGoldSystem.WithdrawPerSeat * g.Seats(mover), bought.Sum); Assert.Equal(ForeignGoldSystem.Taken, bought.Answer);
        Assert.DoesNotContain(g.W.Motions, x => x.Id == m.Id); Assert.True(bought.Settled && bought.Turned); Assert.Equal(g.W.Turn, g.Cities.BoughtOffTurn);
        Assert.Equal(2, g.W.Counter(key)); Assert.Equal(moverPurse + bought.Sum, g.HeadOf(mover).Purse); Assert.Equal(1, g.W.Counter(ForeignGoldSystem.BoughtOffKey));
        Assert.Contains("to withdraw the motion of war on the Free Cities of Thessar", LadderSystem.DeedSays(g.W, Content.Offices, g.W.OfficeRecord.Single(d => d.What == ForeignGoldSystem.Deed)));
        // the next war within the years is not bought off: the Cities write to the senators instead, if anyone
        g.W.Turn += 1; var m2 = g.S.Add(g.W, o.First(h => h.Id != mover.Id && g.S.MayPropose(g.W, h.Id)).Id, "foreign_war", "thessar");
        g.Gold(); Assert.DoesNotContain(g.W.GoldOffers, x => x.MotionId == m2.Id && x.Ask == ForeignGoldSystem.Withdraw);
        // an upright mover tells the chamber, and the Cities write to nobody else on that motion
        g.W.Motions.Remove(m2); g.W.Turn += ForeignGoldSystem.BuyOffEvery; var honest = o.First(h => h.Id != mover.Id && g.S.MayPropose(g.W, h.Id)); Nature(g.HeadOf(honest), "Just");
        var m3 = War(g, honest, o.Where(h => h.Id != honest.Id).Take(2).ToArray()); g.Gold();
        Assert.Equal(ForeignGoldSystem.Exposed, g.W.GoldOffers.Single(x => x.MotionId == m3.Id).Answer); Assert.Contains(g.W.Motions, x => x.Id == m3.Id && x.Open);
        Assert.Contains(ForeignGoldSystem.Known(g.W, m3.Id, g.Mine.Id), x => x.HouseId == honest.Id && x.Ask == ForeignGoldSystem.Withdraw);   // told the chamber, so known
    }

    [Fact]
    public void The_player_who_moved_the_war_may_take_a_sum_to_withdraw_it()
    {
        var g = Start(); g.Cities.BoughtOffTurn = 0; var o = Others(g); int purse = g.Me.Purse;
        var m = g.S.Add(g.W, g.Mine.Id, "foreign_war", "thessar"); m.Votes[g.Mine.Id] = 1;
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != g.Mine.Id)) m.Votes[h.Id] = 0;
        g.Gold();
        var offer = Assert.Single(g.W.GoldOffers.Where(x => x.MotionId == m.Id)); Assert.Equal(ForeignGoldSystem.Withdraw, offer.Ask); Assert.Equal("", offer.Answer);
        Assert.True(g.Told("to withdraw the motion of war"));
        Assert.Equal(new[] { offer }, ForeignGoldSystem.Known(g.W, m.Id, g.Mine.Id));   // your own is known to you
        Assert.Contains("your own house, to withdraw it.", ForeignGoldSystem.KnownSays(g.W, ForeignGoldSystem.Known(g.W, m.Id, g.Mine.Id), offer.NationName, g.Mine.Id));
        g.Answer(offer.Id, ForeignGoldSystem.Take);
        Assert.Equal(purse + offer.Sum, g.Me.Purse); Assert.DoesNotContain(g.W.Motions, x => x.Id == m.Id); Assert.Equal(ForeignGoldSystem.Taken, offer.Answer);
        Assert.Equal("it has been answered", g.No(offer.Id, ForeignGoldSystem.Refuse));
        var deed = g.W.OfficeRecord.Single(d => d.What == ForeignGoldSystem.Deed); Assert.Equal(g.Me.Id, deed.CharacterId);
        Assert.Contains(deed, LadderSystem.Chargeable(g.W, o[0].Id));   // any rival may charge it at once
        Assert.DoesNotContain(deed, LadderSystem.Chargeable(g.W, g.Mine.Id));
    }

    [Fact]
    public void Gold_that_waits_for_you_goes_to_whoever_heads_the_house_when_it_is_answered()
    {
        foreach (bool died in new[] { true, false })
        {
            var g = Start(); g.Cities.BoughtOffTurn = 0; var o = Others(g);
            var m = g.S.Add(g.W, g.Mine.Id, "foreign_war", "thessar"); m.Votes[g.Mine.Id] = 1;
            foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != g.Mine.Id)) m.Votes[h.Id] = 0;
            g.Gold();
            var offer = Assert.Single(g.W.GoldOffers.Where(x => x.MotionId == m.Id)); Assert.Equal(g.Me.Id, offer.CharacterId);
            // the head dies, or yields the seat, before the offer is answered: the heir answers it, and it is the heir's
            var heir = DynastySystem.FindHeir(g.W, g.Me)!;
            if (died) DynastySystem.Die(g.W, g.Me.Id); else DynastySystem.HandOver(g.W, heir);
            Assert.Equal(heir.Id, g.W.Protagonist!.Id);
            int heirPurse = heir.Purse, oldPurse = g.Me.Purse;
            var take = new ForeignGoldCommand(heir.Id, offer.Id, ForeignGoldSystem.Take); Assert.Null(take.Refusal(g.Ctx)); Assert.True(g.Tc.Executor.TryExecute(take, g.Ctx));
            Assert.Equal(heirPurse + offer.Sum, heir.Purse); Assert.Equal(oldPurse, g.Me.Purse); Assert.Equal(heir.Id, offer.CharacterId);
            var deed = g.W.OfficeRecord.Single(d => d.What == ForeignGoldSystem.Deed); Assert.Equal(heir.Id, deed.CharacterId);
            Assert.Contains(deed, LadderSystem.Chargeable(g.W, o[0].Id));   // on the record against the one who took it
            Assert.Contains(heir.Life!.Deeds, n => n.Text.Contains("Took " + Money.Credits(offer.Sum)));
        }
    }

    [Fact]
    public void Under_a_throne_the_Cities_send_nothing_for_the_record_is_not_kept()
    {
        var g = Start(scenario: "the_imperial_court"); Assert.True(g.W.IsImperium); Assert.False(LadderSystem.Active(g.W, Content.Offices));
        var o = Others(g); var c1 = o[0]; var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last(); Nature(g.HeadOf(c1), "Ruthless"); g.Cities.BoughtOffTurn = 0;
        var m = War(g, mover, c1); g.SetLeans(m, (c1, 1), (g.Mine, 1)); g.Gold();
        Assert.DoesNotContain(g.W.GoldOffers, x => x.MotionId == m.Id);
        // a throne rises over the Republic while an offer waits on you: it cannot be taken, and at the year's end it goes home
        var r = Start(); var ro = Others(r); var rc = ro[0]; var rm = War(r, ro.Where(h => r.S.MayPropose(r.W, h.Id)).Last(), rc); Nature(r.HeadOf(rc), "Loyal"); rc.Treasury.Credits = 100;
        r.SetLeans(rm, (rc, 1), (r.Mine, 1)); r.Gold();
        var waiting = ForeignGoldSystem.WaitingOn(r.W, rm.Id, r.Mine.Id)!; int purse = r.Cities.Purse;
        r.W.Era = "imperium";
        Assert.Equal("a throne stands now, and the Cities treat with it, not with the benches", r.No(waiting.Id, ForeignGoldSystem.Take));
        r.Gold(); Assert.Equal(ForeignGoldSystem.Lapsed, waiting.Answer); Assert.True(waiting.Settled); Assert.Equal(purse + waiting.Sum + Content.Nations.Def("thessar")!.Wealth, r.Cities.Purse);
    }

    [Fact]
    public void When_you_moved_the_war_the_Cities_write_to_the_senators_in_the_same_year_and_do_not_wait_for_your_answer()
    {
        var g = Start(); g.Cities.BoughtOffTurn = 0; var o = Others(g);
        int left = g.Cities.Purse - ForeignGoldSystem.SumFor(ForeignGoldSystem.Withdraw, g.Seats(g.Mine));   // what the purse holds once your sum is set aside
        var open = new[] { o.Where(h => g.Seats(h) > g.Seats(g.Mine) && ForeignGoldSystem.SumFor(ForeignGoldSystem.Against, g.Seats(h)) <= left).OrderBy(h => g.Seats(h)).ThenBy(h => h.Id, StringComparer.Ordinal).First() };
        foreach (var h in open) Nature(g.HeadOf(h), "Ruthless");
        var m = g.S.Add(g.W, g.Mine.Id, "foreign_war", "thessar"); m.Votes[g.Mine.Id] = 1;
        foreach (var h in g.W.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != g.Mine.Id && !open.Contains(h))) m.Votes[h.Id] = 0;
        g.SetLeans(m, open.Select(h => (h, 1)).ToArray());
        g.Gold();
        var withdraw = ForeignGoldSystem.WaitingOn(g.W, m.Id, g.Mine.Id)!; Assert.Equal(ForeignGoldSystem.Withdraw, withdraw.Ask);
        Assert.All(open, h => Assert.Contains(g.W.GoldOffers, x => x.MotionId == m.Id && x.HouseId == h.Id && x.Answer == ForeignGoldSystem.Taken));
        g.Answer(withdraw.Id, ForeignGoldSystem.Expose);   // told to the chamber: what the senators took, they took
        Assert.All(open, h => Assert.True(g.S.Lean(g.W, m, h.Id) < 0));
    }

    [Fact]
    public void Every_answer_the_player_can_give_and_what_each_does()
    {
        foreach (var answer in ForeignGoldSystem.Answers)
        {
            var g = Start(); var o = Others(g); var c1 = o[0]; var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last(); Nature(g.HeadOf(c1), "Loyal"); c1.Treasury.Credits = 100;
            var m = War(g, mover, c1); g.SetLeans(m, (c1, 1), (g.Mine, 1));
            g.Gold();
            var offer = ForeignGoldSystem.WaitingOn(g.W, m.Id, g.Mine.Id); Assert.NotNull(offer); Assert.Equal(ForeignGoldSystem.Against, offer!.Ask);
            int purse = g.Me.Purse, cities = g.Cities.Purse, trust = LadderSystem.Trust(g.W), legit = g.Mine.Legitimacy, regard = g.Cities.Disposition;
            g.Answer(offer.Id, answer);
            Assert.Null(ForeignGoldSystem.WaitingOn(g.W, m.Id, g.Mine.Id)); Assert.False(g.W.HasFlag(ForeignGoldSystem.OfferedFlag));
            if (answer == ForeignGoldSystem.Take)
            {
                Assert.Equal(ForeignGoldSystem.Taken, offer.Answer); Assert.Equal(purse + offer.Sum, g.Me.Purse); Assert.Equal(cities, g.Cities.Purse); Assert.Equal(-1, m.Votes[g.Mine.Id]);
                var deed = g.W.OfficeRecord.Single(d => d.What == ForeignGoldSystem.Deed && d.HouseId == g.Mine.Id);
                Assert.True(LadderSystem.ChargedAtOnce(deed));
                Assert.Equal($"took {Money.Credits(offer.Sum)} from the Free Cities of Thessar to vote against the war on the Free Cities of Thessar in year {g.W.Turn}", LadderSystem.DeedSays(g.W, Content.Offices, deed));
            }
            else if (answer == ForeignGoldSystem.Refuse)
            {
                Assert.Equal(ForeignGoldSystem.Refused, offer.Answer); Assert.Equal(purse, g.Me.Purse); Assert.Equal(cities + offer.Sum, g.Cities.Purse);
                Assert.Equal(trust, LadderSystem.Trust(g.W)); Assert.Equal(regard, g.Cities.Disposition); Assert.DoesNotContain(g.W.OfficeRecord, d => d.What == ForeignGoldSystem.Deed && d.HouseId == g.Mine.Id);
            }
            else
            {
                Assert.Equal(ForeignGoldSystem.Exposed, offer.Answer); Assert.Equal(purse, g.Me.Purse); Assert.Equal(cities + offer.Sum, g.Cities.Purse);
                Assert.Equal(trust + ForeignGoldSystem.ExposeTrust, LadderSystem.Trust(g.W)); Assert.Equal(legit + ForeignGoldSystem.ExposeLegitimacy, g.Mine.Legitimacy); Assert.Equal(regard - ForeignGoldSystem.ExposeRegard, g.Cities.Disposition);
                Assert.Contains(g.S.Why(g.W, m, c1.Id), x => x.why.Contains("shown to the chamber"));
            }
        }
    }

    [Fact]
    public void An_answer_is_refused_with_its_reason()
    {
        var g = Start(); var o = Others(g); var c1 = o[0]; var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last(); Nature(g.HeadOf(c1), "Loyal"); c1.Treasury.Credits = 100;
        var m = War(g, mover, c1); g.SetLeans(m, (c1, 1), (g.Mine, 1)); g.Gold();
        var offer = ForeignGoldSystem.WaitingOn(g.W, m.Id, g.Mine.Id)!; var theirs = g.W.GoldOffers.Single(x => x.HouseId == c1.Id);
        var kin = g.W.LivingCharacters().First(c => c.HouseId == g.Mine.Id && c.Id != g.Me.Id);
        Assert.Equal("only the head of the house answers for it", g.No(offer.Id, ForeignGoldSystem.Take, kin.Id));
        Assert.Equal("nothing was sent", g.No("gold_nothing", ForeignGoldSystem.Take));
        Assert.Equal("it was not sent to you", g.No(theirs.Id, ForeignGoldSystem.Take));
        Assert.Equal("take it, refuse it, or tell the Senate", g.No(offer.Id, "haggle"));
        Assert.False(g.Tc.Executor.TryExecute(new ForeignGoldCommand(g.Me.Id, offer.Id, "haggle"), g.Ctx));
        Assert.Null(g.No(offer.Id, ForeignGoldSystem.Take));
        m.ProposedTurn = g.W.Turn - 1; m.HeldOver = true; g.S.Tick(g.W);   // the vote is over
        Assert.Equal("the vote it was sent for is over", g.No(offer.Id, ForeignGoldSystem.Take));
        g.Gold(); Assert.Equal("it has been answered", g.No(offer.Id, ForeignGoldSystem.Take));
    }

    [Fact]
    public void Gold_taken_can_turn_a_war_vote_and_the_turn_is_marked()
    {
        var g = Start(); var o = Others(g); var c1 = o[0];
        var rest = o.Skip(1).Where(h => g.S.MayPropose(g.W, h.Id)).ToList();
        // a mover and a house against with near the same seats, so the heaviest house decides it
        var pair = rest.SelectMany(a => rest.Where(b => b.Id != a.Id).Select(b => (a, b))).First(p => Math.Abs(g.Seats(p.a) - g.Seats(p.b)) < g.Seats(c1));
        var (mover, against) = pair; Nature(g.HeadOf(c1), "Ruthless");
        var m = War(g, mover, c1); m.Votes[against.Id] = -1; m.Votes[g.Mine.Id] = 0; g.SetLean(m, c1, 1);
        g.Gold();
        var bought = g.W.GoldOffers.Single(x => x.MotionId == m.Id && x.HouseId == c1.Id); Assert.Equal(ForeignGoldSystem.Taken, bought.Answer); Assert.Equal(ForeignGoldSystem.Against, bought.Ask);
        Assert.Equal(-1, Math.Sign(g.S.Lean(g.W, m, c1.Id))); Assert.Contains(g.S.Why(g.W, m, c1.Id), x => x.why.Contains("taken gold to vote against"));
        g.Count(m); Assert.False(m.Passed); Assert.Equal(-1, m.Votes[c1.Id]);
        g.Gold(); Assert.True(bought.Turned); Assert.False(bought.Broken); Assert.Equal(1, g.W.Counter(ForeignGoldSystem.TurnedKey));
    }

    [Fact]
    public void One_who_takes_the_gold_and_votes_for_the_war_is_written_to_no_more()
    {
        var g = Start(); var o = Others(g); var c1 = o[0]; var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last(); Nature(g.HeadOf(c1), "Loyal"); c1.Treasury.Credits = 100;
        var m = War(g, mover, c1); g.SetLeans(m, (c1, 1), (g.Mine, 1)); g.Gold();
        var offer = ForeignGoldSystem.WaitingOn(g.W, m.Id, g.Mine.Id)!; g.Answer(offer.Id, ForeignGoldSystem.Take);
        int regard = g.Cities.Disposition;
        Assert.True(g.Tc.Executor.TryExecute(new VoteCommand(g.Me.Id, m.Id, 1), g.Ctx));
        g.Count(m); g.Gold();
        Assert.True(offer.Broken); Assert.True(ForeignGoldSystem.Shut(g.W, g.Mine.Id)); Assert.Equal(regard - ForeignGoldSystem.BrokenRegard - (m.Passed ? 20 : 0), g.Cities.Disposition);   // (a war voted on them costs twenty more)
        Assert.Equal(1, g.W.Counter(ForeignGoldSystem.BrokenKey));
        Assert.True(g.Told("voted for the war all the same"));
        Assert.Contains(g.W.OfficeRecord, d => d.What == ForeignGoldSystem.Deed && d.HouseId == g.Mine.Id);   // and it is still on the record
        // the next war: the Cities write to the player's house no more
        g.W.Turn += 1; var m2 = War(g, mover, c1); g.SetLeans(m2, (c1, 1), (g.Mine, 1)); g.Gold();
        Assert.Null(ForeignGoldSystem.WaitingOn(g.W, m2.Id, g.Mine.Id));
    }

    [Fact]
    public void Gold_taken_is_charged_before_a_praetor_with_the_facts_of_the_record()
    {
        var g = Start(); var o = Others(g); var c1 = o[0]; var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last(); Nature(g.HeadOf(c1), "Ruthless");
        var m = War(g, mover, c1); g.SetLeans(m, (c1, 1), (g.Mine, -1)); g.Gold();
        var offer = g.W.GoldOffers.Single(x => x.HouseId == c1.Id); var deed = g.W.OfficeRecord.Single(d => d.What == ForeignGoldSystem.Deed);
        Assert.Equal("deed_" + offer.Id, deed.Id); Assert.Contains(deed, LadderSystem.Chargeable(g.W, g.Mine.Id));
        Assert.Contains($"took {Money.Credits(offer.Sum)} from the Free Cities of Thessar", LadderSystem.DeedSays(g.W, Content.Offices, deed));
        var accuser = o.First(h => h.Id != c1.Id && LadderSystem.Court(g.W, deed, h.Id) != null);
        var judge = LadderSystem.Court(g.W, deed, accuser.Id)!; Nature(judge, "Just");   // an upright praetor goes by the record
        bool guilty = LadderSystem.Prosecute(g.W, Content.Offices, g.HeadOf(accuser), deed, judge);
        Assert.True(guilty); Assert.True(deed.Answered); Assert.True(g.W.Counter(LadderSystem.BarredKey(g.HeadOf(c1).Id)) > g.W.Turn);
    }

    [Fact]
    public void The_offers_and_the_purse_are_saved()
    {
        var g = Start(); var o = Others(g); var c1 = o[0]; var mover = o.Where(h => g.S.MayPropose(g.W, h.Id)).Last(); Nature(g.HeadOf(c1), "Ruthless");
        var m = War(g, mover, c1); g.SetLeans(m, (c1, 1), (g.Mine, 1)); g.Gold(); g.Cities.BoughtOffTurn = 7;
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(g.W));
        Assert.Equal(g.W.GoldOffers.Count, back.GoldOffers.Count); Assert.True(back.GoldOffers.Count >= 2);
        foreach (var (a, b) in g.W.GoldOffers.Zip(back.GoldOffers))
        {
            Assert.Equal(a.Id, b.Id); Assert.Equal(a.NationId, b.NationId); Assert.Equal(a.WarOnId, b.WarOnId); Assert.Equal(a.NationName, b.NationName); Assert.Equal(a.WarOnName, b.WarOnName); Assert.Equal(a.MotionId, b.MotionId);
            Assert.Equal(a.HouseId, b.HouseId); Assert.Equal(a.CharacterId, b.CharacterId); Assert.Equal(a.Ask, b.Ask); Assert.Equal(a.Sum, b.Sum); Assert.Equal(a.Turn, b.Turn); Assert.Equal(a.Answer, b.Answer);
            Assert.Equal(a.Before, b.Before); Assert.Equal(a.Seats, b.Seats); Assert.Equal(a.Settled, b.Settled); Assert.Equal(a.Turned, b.Turned); Assert.Equal(a.Broken, b.Broken);
        }
        var n = back.Nation("thessar")!; Assert.Equal(g.Cities.Purse, n.Purse); Assert.True(n.PurseOpened); Assert.Equal(7, n.BoughtOffTurn);
        Assert.True(back.Motions.Single(x => x.Id == m.Id).Bought.ContainsKey(c1.Id));
    }

    [Fact]
    public void Left_to_itself_a_game_runs_with_the_gold_in_it()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 3); var tc = new TurnController(w, Content);
        for (int t = 0; t < 12; t++)
        {
            if (t == 4) { var mover = w.Houses.Values.Where(h => h.Id != w.Protagonist!.HouseId && !World.Outsider(h.Id) && tc.Senate.MayPropose(w, h.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id)).OrderByDescending(h => tc.Senate.Weight(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).First(); tc.Senate.Add(w, mover.Id, "foreign_war", "thessar"); }
            foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
            tc.EndTurn();
        }
        Assert.True(w.Nation("thessar")!.PurseOpened); Assert.InRange(w.Nation("thessar")!.Purse, 0, ForeignGoldSystem.Cap(Content.Nations.Def("thessar")!));
        Assert.All(w.GoldOffers, x => Assert.True(x.Settled || w.Motions.Any(m => m.Id == x.MotionId && m.Open)));
    }

    [Fact]
    public void Taking_a_sum_to_withdraw_a_motion_says_nothing_of_a_vote_there_will_not_be()
    {
        var withdraw = new GoldOffer { Id = "o1", Ask = ForeignGoldSystem.Withdraw, Sum = 10 }; var against = new GoldOffer { Id = "o2", Ask = ForeignGoldSystem.Against, Sum = 10 };
        Assert.Contains("Your motion is withdrawn at once.", ForeignGoldSystem.TakeSays(withdraw)); Assert.DoesNotContain("Vote for the war after all", ForeignGoldSystem.TakeSays(withdraw));
        Assert.Contains($"Vote for the war after all and they send your house nothing for {Counted.Years(ForeignGoldSystem.BrokenYears)}.", ForeignGoldSystem.TakeSays(against));
        string razor = File.ReadAllText(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Web", "Components", "ForeignGoldBlock.razor"));
        Assert.Contains("ForeignGoldSystem.Take => ForeignGoldSystem.TakeSays(o)", razor);
    }
}
