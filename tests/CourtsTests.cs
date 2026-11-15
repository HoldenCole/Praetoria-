using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The courts (docs/LADDER.md; docs/GAPS.md L2): what a magistrate did is on a record that outlasts the year; a former
/// magistrate can be prosecuted before a sitting praetor, whose verdict it is; other houses prosecute too; and a praetor can be
/// bought, or refuse and say so.</summary>
public class CourtsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static List<House> Others(World w, House mine) =>
        w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    private static Character Person(World w, House h, string id, params string[] natures)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = 40, Sex = "male", CareerTrack = "law", CareerRank = 2 }; foreach (var n in natures) c.NatureTraits.Add(n);
        w.Characters[id] = c; h.Members.Add(id); return c;
    }
    private static void Sit(World w, Character c, string rung) { w.Magistrates.RemoveAll(m => m.CharacterId == c.Id); w.Magistrates.Add(new OfficeHolding { OfficeId = rung, HouseId = c.HouseId, CharacterId = c.Id, SinceTurn = w.Turn }); }
    private static OfficeDeed Deed(World w, Character who, string what = LadderSystem.TookAccounts, string rung = "quaestor", string against = "")
    {
        var d = new OfficeDeed { Id = "deed_" + who.Id + "_" + w.OfficeRecord.Count, CharacterId = who.Id, HouseId = who.HouseId, RungId = rung, Turn = w.Turn, What = what, AgainstId = against }; w.OfficeRecord.Add(d); return d;
    }
    private static void Fires(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = 100; w.PoolsFor(me.Id).Influence = 9;
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId);
        Assert.True(found.def != null, $"{eventId} is not eligible");
        foreach (var c in found.def!.Choices)
        {
            var again = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); if (again.def == null) break;
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(again.def, again.binding), w, c.Id);
        }
    }

    [Fact]
    public void What_a_magistrate_took_is_on_a_record_that_outlasts_the_year_and_cannot_be_charged_while_they_sit()
    {
        var (_, w, me, mine, _) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeWork.Clear(); w.OfficeRecord.Clear();
        var thief = Person(w, others[0], "thief", "Ruthless"); Sit(w, thief, "quaestor");
        var biz = new OfficeBusiness { Id = "biz_take", RungId = "quaestor", HolderId = thief.Id, Kind = LadderSystem.Accounts, Turn = w.Turn }; w.OfficeWork.Add(biz);
        LadderSystem.Decide(w, Offices, thief, LadderSystem.Favour);
        var deed = Assert.Single(w.OfficeRecord); Assert.Equal(thief.Id, deed.CharacterId); Assert.Equal(LadderSystem.TookAccounts, deed.What); Assert.Contains("accounts", LadderSystem.DeedSays(w, Offices, deed));
        if (deed.Answered) { Assert.Null(LadderSystem.Seat(w, thief.Id)); return; }   // it came out at once: they were removed for it, and are not tried twice
        Assert.Empty(LadderSystem.Chargeable(w, mine.Id));                              // they still sit
        w.Turn += 3; Assert.Single(w.OfficeRecord);                                     // the year's business is gone; the record is not
        w.Magistrates.Clear(); Assert.Single(LadderSystem.Chargeable(w, mine.Id)); Assert.Empty(LadderSystem.Chargeable(w, others[0].Id));   // nobody charges their own
        w.Turn += LadderSystem.RecordYears; LadderSystem.Tick(w, Offices, Content.Policies); Assert.Empty(w.OfficeRecord);                    // and in the end it is forgotten
        // an honest year leaves nothing on it
        var straight = Person(w, others[1], "straight", "Just"); Sit(w, straight, "quaestor"); w.OfficeWork.Add(new OfficeBusiness { Id = "biz_ok", RungId = "quaestor", HolderId = straight.Id, Kind = LadderSystem.Accounts, Turn = w.Turn });
        LadderSystem.Decide(w, Offices, straight, LadderSystem.Straight); Assert.DoesNotContain(w.OfficeRecord, d => d.CharacterId == straight.Id);
    }

    [Fact]
    public void An_upright_praetor_convicts_on_the_record_and_the_house_answers_for_it()
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeRecord.Clear();
        var thief = Person(w, others[0], "thief"); var judge = Person(w, others[1], "judge", "Just"); Sit(w, judge, LadderSystem.Praetor); var wronged = others[2];
        var deed = Deed(w, thief, LadderSystem.TookGift, LadderSystem.Praetor, wronged.Id);
        others[0].Treasury.Credits = 50; int legit = others[0].Legitimacy, mineLegit = mine.Legitimacy, got = wronged.Treasury.Credits, trust = LadderSystem.Trust(w); w.PoolsFor(me.Id).Influence = 5;
        var (guilty, why) = LadderSystem.Verdict(w, deed, mine.Id, judge); Assert.True(guilty); Assert.Contains("upright", why);
        Assert.True(tc.Executor.TryExecute(new ProsecutionCommand(me.Id, ProsecutionCommand.Prosecute, deed.Id, Offices), ctx));
        Assert.Equal(5 - LadderSystem.ProsecuteInfluence, w.PoolsFor(me.Id).Influence); Assert.True(deed.Answered);
        Assert.Equal(w.Turn + LadderSystem.BarYears, w.Counter(LadderSystem.BarredKey(thief.Id))); Assert.True(LadderSystem.Tainted(w, others[0].Id));
        Assert.Equal(legit - LadderSystem.ConvictLegitimacy, others[0].Legitimacy); Assert.Equal(50 - LadderSystem.FineCredits, others[0].Treasury.Credits); Assert.Equal(got + LadderSystem.FineCredits, wronged.Treasury.Credits);
        Assert.Equal(Math.Min(100, mineLegit + 2), mine.Legitimacy); Assert.True(LadderSystem.Trust(w) >= trust); Assert.True(w.HasFlag(LadderSystem.ProsecutionWonFlag));
        // not heard twice, and one charge a year
        Assert.False(tc.Executor.TryExecute(new ProsecutionCommand(me.Id, ProsecutionCommand.Prosecute, deed.Id, Offices), ctx));
        var second = Deed(w, Person(w, others[3], "other_thief")); Assert.Contains("this year", LadderSystem.ProsecuteRefusal(w, me, second));
        Fires(w, "the_charge_is_proved");
    }

    [Fact]
    public void A_praetor_who_is_a_friend_of_the_accused_lets_them_go_and_one_for_sale_finds_for_the_richer_house()
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeRecord.Clear();
        var thief = Person(w, others[0], "thief"); var judge = Person(w, others[1], "judge", "Loyal"); Sit(w, judge, LadderSystem.Praetor);
        w.Relationship(Economy.HeadOf(w, others[1])!.Id, Economy.HeadOf(w, others[0])!.Id).Disposition = 40;
        var deed = Deed(w, thief); Assert.False(LadderSystem.Verdict(w, deed, mine.Id, judge).guilty);
        int legit = mine.Legitimacy, trust = LadderSystem.Trust(w); w.PoolsFor(me.Id).Influence = 5;
        Assert.True(tc.Executor.TryExecute(new ProsecutionCommand(me.Id, ProsecutionCommand.Prosecute, deed.Id, Offices), ctx));
        Assert.False(w.WorldCounters.ContainsKey(LadderSystem.BarredKey(thief.Id))); Assert.Equal(legit - 1, mine.Legitimacy); Assert.Equal(trust - 1, LadderSystem.Trust(w));
        Assert.True(w.HasFlag(LadderSystem.ProsecutionLostFlag)); Fires(w, "the_court_lets_them_go");
        // one known to be for sale: the richer house has the verdict
        w.Relationship(Economy.HeadOf(w, others[1])!.Id, Economy.HeadOf(w, others[0])!.Id).Disposition = 0; judge.NatureTraits.Clear(); judge.NatureTraits.Add("Ruthless");
        var d2 = Deed(w, thief); others[0].Treasury.Credits = 500; mine.Treasury.Credits = 10; Assert.False(LadderSystem.Verdict(w, d2, mine.Id, judge).guilty);
        mine.Treasury.Credits = 900; Assert.True(LadderSystem.Verdict(w, d2, mine.Id, judge).guilty);
        // no praetor of a third house: nobody can hear it
        w.Turn++; w.Magistrates.Clear(); Sit(w, Person(w, others[0], "their_own_praetor"), LadderSystem.Praetor); Assert.Contains("no praetor", LadderSystem.ProsecuteRefusal(w, me, d2));
    }

    [Fact]
    public void Other_houses_prosecute_too_and_the_players_own_people_are_not_excepted()
    {
        var (_, w, me, mine, _) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeRecord.Clear(); w.OfficeWork.Clear(); w.Candidacies.Clear();
        var kin = Person(w, mine, "kin"); var judge = Person(w, others[1], "judge", "Just"); Sit(w, judge, LadderSystem.Praetor);
        var deed = Deed(w, kin); int legit = mine.Legitimacy;
        // nobody hates the house: nobody brings it
        foreach (var h in others) w.Relationship(Economy.HeadOf(w, h)!.Id, me.Id).Disposition = 10;
        LadderSystem.Tick(w, Offices, Content.Policies); Assert.False(deed.Answered);
        w.Relationship(Economy.HeadOf(w, others[0])!.Id, me.Id).Disposition = -50; Sit(w, judge, LadderSystem.Praetor);
        LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.True(deed.Answered); Assert.True(w.Counter(LadderSystem.BarredKey(kin.Id)) > w.Turn); Assert.True(mine.Legitimacy < legit); Assert.True(w.HasFlag(LadderSystem.KinExposedFlag)); Assert.Contains(LadderSystem.ExposedChar, kin.Flags);
        Assert.Equal(1, w.Counter("convictions"));
    }

    [Fact]
    public void A_praetor_can_be_bought_unless_they_are_upright_and_a_bought_ruling_can_come_out()
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeRecord.Clear(); w.OfficeWork.Clear();
        // the upright refuse, and say so in open court
        var upright = Person(w, others[0], "upright", "Just"); Sit(w, upright, LadderSystem.Praetor); me.Purse = 40; int legit = mine.Legitimacy;
        Assert.False(LadderSystem.WouldTake(w, me, upright));
        Assert.True(tc.Executor.TryExecute(new ProsecutionCommand(me.Id, ProsecutionCommand.Buy, upright.Id, Offices), ctx));
        Assert.Equal(40, me.Purse); Assert.Null(LadderSystem.BoughtBy(w, upright)); Assert.True(mine.Legitimacy < legit); Assert.True(LadderSystem.Tainted(w, mine.Id)); Assert.True(w.HasFlag(LadderSystem.BribeRefusedFlag));
        Fires(w, "the_praetor_is_not_for_sale");
        // one who is for sale takes it, and finds for the buyer whenever that house is before them
        var bent = Person(w, others[1], "bent", "Ruthless"); Sit(w, bent, LadderSystem.Praetor); me.Purse = 40; int theirs = others[1].Treasury.Credits;
        Assert.True(LadderSystem.WouldTake(w, me, bent)); Assert.True(tc.Executor.TryExecute(new ProsecutionCommand(me.Id, ProsecutionCommand.Buy, bent.Id, Offices), ctx));
        Assert.Equal(40 - LadderSystem.BribeCredits, me.Purse); Assert.Equal(theirs + LadderSystem.BribeCredits, others[1].Treasury.Credits); Assert.Equal(mine.Id, LadderSystem.BoughtBy(w, bent));
        Assert.Contains(w.OfficeRecord, d => d.What == LadderSystem.Bribed && d.HouseId == mine.Id);
        Assert.Contains("spoken for", LadderSystem.BuyRefusal(w, me, bent));
        bool cameOut = false; int won = 0;
        for (int i = 0; i < 12 && !cameOut; i++)
        {
            var biz = new OfficeBusiness { Id = "case_" + i, RungId = LadderSystem.Praetor, HolderId = bent.Id, Kind = LadderSystem.Case, Turn = w.Turn, HouseA = mine.Id, HouseB = others[2].Id, RightId = others[2].Id }; w.OfficeWork.Add(biz);
            Assert.Equal(LadderSystem.Gift, LadderSystem.Inclination(w, bent, biz)); LadderSystem.Decide(w, Offices, bent, LadderSystem.Gift);
            Assert.Equal(mine.Id, biz.WinnerId); won++; cameOut = w.HasFlag(LadderSystem.BribeOutFlag);
            if (!cameOut) Sit(w, bent, LadderSystem.Praetor);
        }
        Assert.True(won >= 1); Assert.True(cameOut, "twelve bought rulings and nobody ever asked whose money it was");
        Assert.Null(LadderSystem.Seat(w, bent.Id)); Assert.True(LadderSystem.Tainted(w, mine.Id));
        // a praetor who no longer sits is nobody's
        LadderSystem.Tick(w, Offices, Content.Policies); Assert.Null(LadderSystem.BoughtBy(w, bent));
    }

    // ---------------- L5: the facts of the year's business ----------------

    [Fact]
    public void A_case_has_a_matter_a_stake_and_a_law_that_is_plain_arguable_or_doubtful()
    {
        var (_, w, me, mine, _) = Start(); var seen = new HashSet<int>(); var stakes = new HashSet<int>(); var matters = new HashSet<string>(); int cases = 0;
        var benchHouses = Others(w, mine); var j1 = Person(w, benchHouses[0], "bench_one", "Just"); var j2 = Person(w, benchHouses[1], "bench_two", "Just");
        for (int year = 0; year < 40; year++)
        {
            w.Turn++; Sit(w, j1, LadderSystem.Praetor); Sit(w, j2, LadderSystem.Praetor); LadderSystem.Tick(w, Offices, Content.Policies);   // no election is held here, so the bench is kept filled by hand
            foreach (var b in w.OfficeWork.Where(b => b.Turn == w.Turn && b.Kind == LadderSystem.Case && b.HouseA.Length > 0))
            {
                Assert.InRange(b.Weight, LadderSystem.Doubtful, LadderSystem.Plain); Assert.Contains(b.Stake, new[] { LadderSystem.Damages, LadderSystem.Damages * 2, LadderSystem.Damages * 3 }); Assert.NotEqual("", b.Matter);
                seen.Add(b.Weight); stakes.Add(b.Stake); matters.Add(b.Matter); cases++;
                string says = LadderSystem.Says(w, Offices, b); Assert.Contains(b.Stake + " credits turn on it", says); Assert.Contains(" over ", says);
                Assert.Contains(b.Weight == LadderSystem.Plain ? "read plainly" : b.Weight == LadderSystem.Arguable ? "arguable" : "doubtful", says);
            }
            foreach (var b in w.OfficeWork.Where(b => b.Turn == w.Turn && b.Kind == LadderSystem.Accounts)) { Assert.InRange(b.Stake, 0, 12); if (b.Stake > 0) Assert.Contains(b.Stake + " credits over", LadderSystem.Says(w, Offices, b)); }
        }
        Assert.True(seen.Count == 3, $"weights seen: {string.Join(",", seen)} over {cases} cases, {w.Magistrates.Count(m => m.OfficeId == LadderSystem.Praetor)} praetors sitting"); Assert.True(stakes.Count >= 2); Assert.True(matters.Count >= 3);
        _ = me; _ = mine;
    }

    private static OfficeBusiness Case(World w, Character judge, House a, House b, House right, int weight, int stake = 10)
    {
        var biz = new OfficeBusiness { Id = $"case_{w.OfficeWork.Count}_{weight}", RungId = LadderSystem.Praetor, HolderId = judge.Id, Kind = LadderSystem.Case, Turn = w.Turn, HouseA = a.Id, HouseB = b.Id, RightId = right.Id, Weight = weight, Stake = stake, Matter = "debt" };
        w.OfficeWork.RemoveAll(x => x.HolderId == judge.Id && x.Choice.Length == 0); w.OfficeWork.Add(biz); return biz;
    }

    [Fact]
    public void The_plainer_the_law_the_more_a_bent_ruling_costs_and_a_doubtful_one_is_not_held_against_the_judge()
    {
        var (_, w, me, mine, _) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeWork.Clear(); w.OfficeRecord.Clear();
        var judge = Person(w, others[0], "judge"); var a = others[1]; var b = others[2];
        int Cost(int weight)
        {
            w.WorldCounters[LadderSystem.TrustKey] = 60; Sit(w, judge, LadderSystem.Praetor); a.Treasury.Credits = 100; b.Treasury.Credits = 100;
            w.Relationship(Economy.HeadOf(w, b)!.Id, Economy.HeadOf(w, others[0])!.Id).Disposition = 80; w.Relationship(Economy.HeadOf(w, a)!.Id, Economy.HeadOf(w, others[0])!.Id).Disposition = -50;   // the friend is the house the law is against
            var biz = Case(w, judge, a, b, right: a, weight); int before = b.Treasury.Credits; LadderSystem.Decide(w, Offices, judge, LadderSystem.Friend);
            Assert.Equal(b.Id, biz.WinnerId); Assert.Equal(before + 10, b.Treasury.Credits);   // the stake, not a fixed sum
            return 60 - LadderSystem.Trust(w);
        }
        int plain = Cost(LadderSystem.Plain), arguable = Cost(LadderSystem.Arguable); int onRecord = w.OfficeRecord.Count; int doubtful = Cost(LadderSystem.Doubtful);
        Assert.True(plain > arguable && arguable > doubtful, $"{plain} {arguable} {doubtful}"); Assert.Equal(2, onRecord); Assert.Equal(2, w.OfficeRecord.Count);   // the doubtful ruling is not on the record
        // and it is not appealed the year after
        w.Turn++; LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.DoesNotContain(w.OfficeWork, x => x.Kind == LadderSystem.Appeal && x.Weight == LadderSystem.Doubtful);
    }

    [Fact]
    public void A_praetor_can_bring_two_houses_to_terms_and_where_the_law_is_doubtful_that_is_the_honest_ruling()
    {
        var (_, w, me, mine, _) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeWork.Clear();
        var judge = Person(w, others[0], "judge", "Just"); Sit(w, judge, LadderSystem.Praetor); var a = others[1]; var b = others[2]; a.Treasury.Credits = 100; b.Treasury.Credits = 100;
        var doubtful = Case(w, judge, a, b, right: a, LadderSystem.Doubtful); Assert.Equal(LadderSystem.Settle, LadderSystem.Inclination(w, judge, doubtful));
        var ha = Economy.HeadOf(w, a)!; var hb = Economy.HeadOf(w, b)!; var hj = Economy.HeadOf(w, others[0])!; int ra = w.Relationship(ha.Id, hj.Id).Disposition, rb = w.Relationship(hb.Id, hj.Id).Disposition;
        w.WorldCounters[LadderSystem.TrustKey] = 50; LadderSystem.Decide(w, Offices, judge, LadderSystem.Settle);
        Assert.Equal(105, a.Treasury.Credits); Assert.Equal(95, b.Treasury.Credits);                                   // half the stake
        Assert.True(w.Relationship(ha.Id, hj.Id).Disposition > ra); Assert.True(w.Relationship(hb.Id, hj.Id).Disposition > rb); Assert.Equal(51, LadderSystem.Trust(w));
        // where the law is plain, the house that had it is short-changed, and knows it; and an upright judge rules on the law
        Sit(w, judge, LadderSystem.Praetor); var plain = Case(w, judge, a, b, right: a, LadderSystem.Plain); Assert.Equal(LadderSystem.Law, LadderSystem.Inclination(w, judge, plain));
        ra = w.Relationship(ha.Id, hj.Id).Disposition; LadderSystem.Decide(w, Offices, judge, LadderSystem.Settle); Assert.True(w.Relationship(ha.Id, hj.Id).Disposition < ra);
        Assert.Contains("had the law", LadderSystem.ChoiceSays(w, plain, LadderSystem.Settle));
        // nobody before the court: nothing to settle
        Sit(w, judge, LadderSystem.Praetor); w.OfficeWork.Add(new OfficeBusiness { Id = "empty", RungId = LadderSystem.Praetor, HolderId = judge.Id, Kind = LadderSystem.Case, Turn = w.Turn });
        Assert.Contains("nobody before you", LadderSystem.DecideRefusal(w, judge, LadderSystem.Settle));
    }

    [Fact]
    public void The_accounts_show_what_is_over_and_that_is_what_can_be_sent_or_taken_and_an_aedile_can_give_both()
    {
        var (_, w, me, mine, _) = Start(); var others = Others(w, mine); w.Magistrates.Clear(); w.OfficeWork.Clear(); w.OfficeRecord.Clear();
        var chair = RegionSystem.CapitalHolder(w)!; var q = Person(w, others.First(h => h.Id != chair.Id), "quaestor_man"); Sit(w, q, "quaestor"); var house = w.House(q.HouseId)!;
        string region = w.Regions.Keys.OrderBy(k => k, StringComparer.Ordinal).First(); RegionSystem.StateOf(w, region).Lawlessness = 60;
        // a large surplus sent to a region does more than a small one
        int Sent(int surplus)
        {
            Sit(w, q, "quaestor"); chair.Treasury.Credits = 100; RegionSystem.StateOf(w, region).Lawlessness = 60;
            w.OfficeWork.RemoveAll(x => x.HolderId == q.Id); w.OfficeWork.Add(new OfficeBusiness { Id = "acc_" + surplus, RungId = "quaestor", HolderId = q.Id, Kind = LadderSystem.Accounts, Turn = w.Turn, RegionId = region, Stake = surplus, Matter = "dues" });
            LadderSystem.Decide(w, Offices, q, LadderSystem.Relief); Assert.Equal(100 - surplus, chair.Treasury.Credits); return 60 - RegionSystem.StateOf(w, region).Lawlessness;
        }
        Assert.Equal(2, Sent(4)); Assert.Equal(6, Sent(12));
        // and what is taken is what was over, out of the capital's purse
        Sit(w, q, "quaestor"); chair.Treasury.Credits = 100; int had = house.Treasury.Credits; w.OfficeWork.RemoveAll(x => x.HolderId == q.Id);
        w.OfficeWork.Add(new OfficeBusiness { Id = "acc_take", RungId = "quaestor", HolderId = q.Id, Kind = LadderSystem.Accounts, Turn = w.Turn, Stake = 11, Matter = "fines" });
        LadderSystem.Decide(w, Offices, q, LadderSystem.Favour); Assert.Equal(had + 11, house.Treasury.Credits); Assert.Equal(89, chair.Treasury.Credits);
        // the aedile: lanes and grain both, for twice the sum
        var ae = Person(w, others.First(h => h.Id != chair.Id && h.Id != house.Id), "aedile_man"); Sit(w, ae, "aedile"); var aeHouse = w.House(ae.HouseId)!; aeHouse.Treasury.Credits = 5;
        w.OfficeWork.Add(new OfficeBusiness { Id = "works_both", RungId = "aedile", HolderId = ae.Id, Kind = LadderSystem.Works, Turn = w.Turn, RegionId = region });
        Assert.Contains((LadderSystem.WorksCredits * 2).ToString(), LadderSystem.DecideRefusal(w, ae, LadderSystem.Both));
        aeHouse.Treasury.Credits = 40; int legit = aeHouse.Legitimacy; RegionSystem.StateOf(w, region).Lawlessness = 60;
        LadderSystem.Decide(w, Offices, ae, LadderSystem.Both);
        Assert.Equal(40 - LadderSystem.WorksCredits * 2, aeHouse.Treasury.Credits); Assert.Equal(60 - LadderSystem.ReliefBy, RegionSystem.StateOf(w, region).Lawlessness); Assert.Equal(Math.Min(100, legit + 3), aeHouse.Legitimacy);
    }
}
