using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The docket (docs/LADDER.md, "The docket"; docs/PLAYTHROUGH.md, Hard): a suit has a plaintiff and a defendant; the head
/// may bring one on a ground the world records, and a house that is sued may counter-sue; a charge against one of the player's
/// house waits a year, and may be pleaded or admitted; and all of it stands on one list.</summary>
public class DocketTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;

    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!;
        w.Magistrates.Clear(); w.OfficeWork.Clear(); w.OfficeRecord.Clear(); w.Candidacies.Clear(); w.Indictments.Clear(); w.Judgments.Clear();
        mine.Treasury.Credits = 100; mine.Treasury.Influence = 5; w.PoolsFor(me.Id).Influence = 5;
        return (tc, w, me, mine, new CommandContext(w, TestContent.Rng()));
    }
    private static List<House> Others(World w, House mine) =>
        w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null && w.Wars.All(x => !x.Between(mine.Id, h.Id))).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    private static Character Person(World w, House h, string id, params string[] natures)
    {
        if (h.HeadId.Length == 0 && Economy.HeadOf(w, h) is { } leads) h.HeadId = leads.Id;   // whoever led the house still leads it: a person added for a test is not its new eldest
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = 40, Sex = "male", CareerTrack = "law", CareerRank = 2 }; foreach (var n in natures) c.NatureTraits.Add(n);
        w.Characters[id] = c; h.Members.Add(id); return c;
    }
    private static void Sit(World w, Character c, string rung = LadderSystem.Praetor) { w.Magistrates.RemoveAll(m => m.CharacterId == c.Id); w.Magistrates.Add(new OfficeHolding { OfficeId = rung, HouseId = c.HouseId, CharacterId = c.Id, SinceTurn = w.Turn }); }
    private static OfficeDeed Deed(World w, Character who, string what = LadderSystem.TookAccounts, string rung = "quaestor", string against = "")
    {
        var d = new OfficeDeed { Id = "deed_" + who.Id + "_" + w.OfficeRecord.Count, CharacterId = who.Id, HouseId = who.HouseId, RungId = rung, Turn = w.Turn, What = what, AgainstId = against }; w.OfficeRecord.Add(d); return d;
    }
    /// <summary>Every head thinks well enough of every other: nobody has a quarrel with anybody.</summary>
    private static void Calm(World w)
    {
        var heads = w.Houses.Values.Select(h => Economy.HeadOf(w, h)).Where(c => c != null).Select(c => c!).ToList();
        foreach (var a in heads) foreach (var b in heads) if (a.Id != b.Id) w.Relationship(a.Id, b.Id).Disposition = 20;
    }
    private static void Hate(World w, House of, House toward, int by = -50) => w.Relationship(Economy.HeadOf(w, of)!.Id, Economy.HeadOf(w, toward)!.Id).Disposition = by;
    private static int Regard(World w, House of, House toward) => w.GetRelationship(Economy.HeadOf(w, of)!.Id, Economy.HeadOf(w, toward)!.Id)?.Disposition ?? 0;
    private static void Year(World w) { LadderSystem.Tick(w, Offices, Content.Policies); w.Turn++; }
    private static void Unskilled(World w, House h) { foreach (var c in w.LivingCharacters().Where(c => c.HouseId == h.Id)) { c.Skills["law"] = 0; c.Skills["oratory"] = 0; } }
    private static bool Eligible(World w, string eventId) => new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == eventId);

    /// <summary>A world with one quarrel in it (the player's with the first other house) and one upright praetor of a third house.</summary>
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx, House them, Character judge) Quarrel(string nature = "Just")
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); Calm(w); Hate(w, others[0], mine);
        var judge = Person(w, others[2], "judge", nature); Sit(w, judge);
        others[0].Treasury.Credits = 100; others[0].Treasury.Influence = 5;
        return (tc, w, me, mine, ctx, others[0], judge);
    }
    private static OfficeBusiness Against(World w, House mine, House them, Character judge, int weight = LadderSystem.Arguable, bool rightIsMine = true, int stake = 10)
    {
        var b = new OfficeBusiness { Id = "case_against_" + w.OfficeWork.Count, RungId = LadderSystem.Praetor, HolderId = judge.Id, Kind = LadderSystem.Case, Turn = w.Turn - 1, HouseA = them.Id, HouseB = mine.Id,
            RightId = rightIsMine ? mine.Id : them.Id, Matter = "debt", Weight = weight, Stake = stake, Held = true, PlaintiffId = them.Id };
        w.OfficeWork.Add(b); return b;
    }

    [Fact]
    public void A_suit_the_bench_draws_up_has_a_plaintiff_and_one_that_names_the_house_is_the_other_houses()
    {
        var (_, w, _, mine, _, them, judge) = Quarrel(); int had = them.Treasury.Credits;
        LadderSystem.Tick(w, Offices, Content.Policies);
        var suit = Assert.Single(LadderSystem.SuitsNaming(w, mine.Id)); Assert.True(suit.Held); Assert.Equal(judge.Id, suit.HolderId);
        Assert.Equal(them.Id, suit.PlaintiffId); Assert.Equal(them.Id, LadderSystem.PlaintiffOf(w, suit));
        // the house that brings it pays the same fee the player's house would
        int fee = LadderSystem.FeeOf(LadderSystem.StakeOf(suit)); Assert.True(fee >= 1);
        if (RegionSystem.CapitalHolder(w)?.Id != them.Id) Assert.Equal(had - fee, them.Treasury.Credits);
        var line = Assert.Single(Docket.Of(w, Offices, mine.Id)); Assert.Equal(Docket.Against, line.Kind); Assert.Contains(them.Name + "'s suit against you", line.Title);
        // between two other houses it is whoever thinks worse of the other
        var (_, w2, _, mine2, _) = Start(); var o = Others(w2, mine2); Calm(w2); Hate(w2, o[1], o[0], -60); Sit(w2, Person(w2, o[3], "judge", "Just"));
        LadderSystem.Tick(w2, Offices, Content.Policies);
        var theirs = Assert.Single(w2.OfficeWork, b => b.Kind == LadderSystem.Case && b.HouseA.Length > 0); Assert.Equal(o[1].Id, theirs.PlaintiffId); Assert.False(theirs.Held); Assert.NotEqual("", theirs.Choice);
        // a case from before suits had a plaintiff: where it names the house, the other house brought it
        var old = Against(w, mine, them, judge); old.PlaintiffId = ""; Assert.Equal(them.Id, LadderSystem.PlaintiffOf(w, old));
    }

    [Fact]
    public void A_house_with_no_quarrel_cannot_be_sued_and_a_quarrel_gives_grounds_from_the_record()
    {
        var (_, w, me, mine, ctx, them, _) = Quarrel(); var friend = Others(w, mine)[1];
        Assert.Empty(LadderSystem.Grounds(w, mine, friend));
        string no = new BringSuitCommand(me.Id, friend.Id, "debt", Offices).Refusal(ctx)!; Assert.Contains("no quarrel with " + friend.Name, no); Assert.Contains("-30", no); Assert.Contains("40", no);
        // the quarrel itself is always a ground, and it says what it is over, for how much and how the law leans
        var grounds = LadderSystem.Grounds(w, mine, them); Assert.NotEmpty(grounds); Assert.Contains(grounds, g => g.Record.Contains("standing quarrel"));
        Assert.Equal(grounds.Count, grounds.Select(g => g.Matter).Distinct().Count());
        foreach (var g in grounds)
        {
            Assert.NotEqual("", g.Says); Assert.NotEqual("", g.Record); Assert.InRange(g.Weight, LadderSystem.Doubtful, LadderSystem.Plain); Assert.Contains(g.Stake, new[] { 5, 10, 15 });
            Assert.Contains(g.RightId, new[] { mine.Id, them.Id }); Assert.Contains(g.RightId == mine.Id ? "with you" : "against you", LadderSystem.LawOn(g, mine.Id));
        }
        // what the world records between the two adds grounds: a war (a wreck to salvage), a debt, one of the blood in the other house
        w.Wars.Add(new War { HouseA = mine.Id, HouseB = them.Id, Since = w.Turn });
        Assert.Contains(LadderSystem.Grounds(w, mine, them), g => g.Matter == "salvage" && g.Record.Contains("at war")); w.Wars.RemoveAll(x => x.Between(mine.Id, them.Id));
        them.Debt = 12; them.LenderId = mine.Id; Assert.Contains(LadderSystem.Grounds(w, mine, them), g => g.Matter == "debt" && g.Record.Contains(them.Name + " owes")); them.Debt = 0; them.LenderId = "";
        var cousin = Person(w, them, "cousin"); cousin.BornHouseId = mine.Id; Assert.Contains(LadderSystem.Grounds(w, mine, them), g => g.Matter == "inheritance" && g.Record.Contains(cousin.Name));
        if (w.HoldingsOf(mine.Id).FirstOrDefault(h => h.IsPlanet) is { } world && w.Galaxy?.System(world.SystemId)?.RegionId is { Length: > 0 })
        {
            var theirs = w.Holdings.Values.First(h => h.IsPlanet && h.Id != world.Id); theirs.OwnerId = them.Id; theirs.SystemId = world.SystemId;
            Assert.Contains(LadderSystem.Grounds(w, mine, them), g => g.Matter == "boundary" && g.Record.Contains("both houses hold worlds"));
        }
    }

    [Fact]
    public void Bringing_a_suit_is_refused_for_what_is_missing_and_costs_a_fee_clout_and_the_other_heads_regard()
    {
        var (tc, w, me, mine, ctx, them, judge) = Quarrel(); var g = LadderSystem.Grounds(w, mine, them)[0]; int fee = LadderSystem.FeeOf(g.Stake);
        BringSuitCommand Cmd(string actor, string house, string matter) => new(actor, house, matter, Offices);
        var kin = Person(w, mine, "kin");
        Assert.Contains("only the head", Cmd(kin.Id, them.Id, g.Matter).Refusal(ctx));
        Assert.Contains("no such house", Cmd(me.Id, "nowhere", g.Matter).Refusal(ctx)); Assert.Contains("no such house", Cmd(me.Id, mine.Id, g.Matter).Refusal(ctx));
        Assert.Contains("no such ground", Cmd(me.Id, them.Id, "a_matter_nobody_has").Refusal(ctx));
        w.Magistrates.Clear(); Assert.Equal("no praetor of a third house sits to hear it", Cmd(me.Id, them.Id, g.Matter).Refusal(ctx));
        Sit(w, Person(w, them, "their_own_praetor")); Assert.Contains("no praetor of a third house", Cmd(me.Id, them.Id, g.Matter).Refusal(ctx)); Sit(w, judge);
        mine.Treasury.Credits = 0; Assert.Equal($"the filing fee is {Money.Credits(fee)} and the house has {Money.Held(mine.Treasury)}", Cmd(me.Id, them.Id, g.Matter).Refusal(ctx)); mine.Treasury.Credits = 100;
        mine.Treasury.Influence = 1; Assert.Equal("needs 2 clout and the house has 1", Cmd(me.Id, them.Id, g.Matter).Refusal(ctx)); mine.Treasury.Influence = 5;

        var chair = RegionSystem.CapitalHolder(w); int chairHad = chair?.Treasury.Credits ?? 0, regard = Regard(w, them, mine);
        Assert.True(tc.Executor.TryExecute(Cmd(me.Id, them.Id, g.Matter), ctx), Cmd(me.Id, them.Id, g.Matter).Refusal(ctx));
        Assert.Equal(100 - fee, mine.Treasury.Credits); Assert.Equal(5 - LadderSystem.BringClout, mine.Treasury.Influence);
        if (chair != null && chair.Id != mine.Id) Assert.Equal(chairHad + fee, chair.Treasury.Credits);
        Assert.Equal(regard - LadderSystem.BringRegard, Regard(w, them, mine));
        // it is the same record a suit the bench draws up is, with the facts the screen showed before it was brought
        var suit = Assert.Single(LadderSystem.SuitsNaming(w, mine.Id)); Assert.True(suit.Held); Assert.Equal(mine.Id, suit.PlaintiffId); Assert.Equal(judge.Id, suit.HolderId); Assert.Equal(w.Turn, suit.Turn);
        Assert.Equal(g.Matter, suit.Matter); Assert.Equal(g.Stake, LadderSystem.StakeOf(suit)); Assert.Equal(g.Weight, LadderSystem.WeightOf(suit)); Assert.Equal(g.RightId, suit.RightId);
        Assert.Contains(w.History, h => h.Text.Contains($"{mine.Name} brings a suit against {them.Name}") && h.Text.Contains(Money.Credits(g.Stake)));
        var line = Assert.Single(Docket.Of(w, Offices, mine.Id)); Assert.Equal(Docket.Brought, line.Kind); Assert.Equal("Your suit against " + them.Name, line.Title); Assert.Equal("at the end of next year", line.Heard);
        Assert.Equal(judge.Name, line.Judge); Assert.Equal("upright", line.JudgeIs); Assert.Equal(g.Stake, line.Sum); Assert.Equal(g.Says, line.Over); Assert.Equal(LadderSystem.LawOn(g, mine.Id), line.Law);
        // once a year, and not twice against the same house while the first waits
        var second = Others(w, mine)[1]; Hate(w, second, mine);
        Assert.Equal("your house has brought a suit this year", Cmd(me.Id, second.Id, LadderSystem.Grounds(w, mine, second)[0].Matter).Refusal(ctx));
        w.Turn++; Assert.Contains("already waiting to be heard", Cmd(me.Id, them.Id, LadderSystem.Grounds(w, mine, them)[0].Matter).Refusal(ctx));
        Assert.Null(Cmd(me.Id, second.Id, LadderSystem.Grounds(w, mine, second)[0].Matter).Refusal(ctx));
        // there are no courts under a throne
        w.Era = World.EraImperium; Assert.Equal("there are no courts of the Republic", Cmd(me.Id, second.Id, "debt").Refusal(ctx));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_suit_the_house_brought_is_heard_at_the_end_of_the_next_year_and_the_loser_pays_the_stake(bool lawWithUs)
    {
        var (tc, w, me, mine, ctx, them, judge) = Quarrel(); var g = LadderSystem.Grounds(w, mine, them)[0];
        Assert.True(tc.Executor.TryExecute(new BringSuitCommand(me.Id, them.Id, g.Matter, Offices), ctx));
        var suit = LadderSystem.SuitsNaming(w, mine.Id).Single(); suit.Weight = LadderSystem.Doubtful + 1; suit.RightId = lawWithUs ? mine.Id : them.Id; them.Treasury.Influence = 0;   // arguable; they have no clout to counter with
        int stake = LadderSystem.StakeOf(suit); mine.Treasury.Credits = 100; them.Treasury.Credits = 100;
        // the year it was brought turns: it is not heard
        Year(w); Assert.Equal("", suit.Choice); Assert.False(suit.Countered); Assert.Equal(100, mine.Treasury.Credits);
        Assert.Equal("at the end of this year", Docket.Of(w, Offices, mine.Id).First(e => e.Id == suit.Id).Heard);
        // the next year turns: an upright praetor rules on the law
        int ours = mine.Treasury.Credits;
        LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.Equal(LadderSystem.Law, suit.Choice); Assert.Equal(lawWithUs ? mine.Id : them.Id, suit.WinnerId);
        Assert.DoesNotContain(Docket.Of(w, Offices, mine.Id), e => e.Id == suit.Id);
        var ruling = Assert.Single(w.Judgments, j => j.Matter == LadderSystem.MatterOf(suit)); Assert.Equal(stake, ruling.Sum); Assert.Equal(lawWithUs ? them.Id : mine.Id, ruling.LoserId); Assert.Equal(judge.Name, ruling.Judge);
        Assert.Equal(lawWithUs, w.HasFlag(LadderSystem.SuitWonFlag)); Assert.Equal(!lawWithUs, w.HasFlag(LadderSystem.SuitLostFlag));
        Assert.False(w.HasFlag("case_won")); Assert.False(w.HasFlag("case_lost"));   // the report of a suit the house brought is its own
        Assert.Contains(LadderSystem.SuedChar, Economy.HeadOf(w, them)!.Flags);
        Assert.True(Eligible(w, lawWithUs ? "your_suit_is_upheld" : "your_suit_is_thrown_out"));
        // the money: the stake from the loser to the winner (the bench's own case of the year may also have named either house)
        var other = w.Judgments.Where(j => j != ruling).ToList();
        int net = other.Sum(j => j.WinnerId == mine.Id ? j.Sum : -j.Sum);
        Assert.Equal(ours + (lawWithUs ? stake : -stake) + net, mine.Treasury.Credits);
        Assert.Contains(Money.Credits(stake), ReportFacts.For(w, Content.Events.First(e => e.Id == (lawWithUs ? "your_suit_is_upheld" : "your_suit_is_thrown_out")), Content));
    }

    [Fact]
    public void A_house_that_is_sued_where_the_law_leans_its_way_answers_with_a_counter_suit_and_the_player_is_told()
    {
        var (tc, w, me, mine, ctx, them, _) = Quarrel(); var g = LadderSystem.Grounds(w, mine, them)[0];
        Assert.True(tc.Executor.TryExecute(new BringSuitCommand(me.Id, them.Id, g.Matter, Offices), ctx));
        var suit = LadderSystem.SuitsNaming(w, mine.Id).Single(); suit.Weight = LadderSystem.Plain; suit.RightId = them.Id; int stake = LadderSystem.StakeOf(suit), fee = LadderSystem.FeeOf(stake);
        int had = them.Treasury.Credits, clout = them.Treasury.Influence, regard = Regard(w, mine, them);
        LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.True(suit.Countered); Assert.Equal(stake * 2, LadderSystem.StakeOf(suit)); Assert.Equal(clout - LadderSystem.CounterClout, them.Treasury.Influence); Assert.Equal(mine.Id, suit.PlaintiffId);
        if (RegionSystem.CapitalHolder(w)?.Id != them.Id) Assert.True(them.Treasury.Credits <= had - fee);
        Assert.Equal(regard - LadderSystem.CounterRegard, Regard(w, mine, them));
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Text.Contains(them.Name + " answers your suit") && n.Text.Contains(Money.Credits(stake * 2)));
        Assert.Contains("and their counter-suit", Docket.Of(w, Offices, mine.Id).First(e => e.Id == suit.Id).Title);
        // terms out of court are now half of the doubled sum
        w.Turn++; mine.Treasury.Credits = 100; int theirs = them.Treasury.Credits;
        Assert.True(tc.Executor.TryExecute(new SuitCommand(me.Id, SuitCommand.Settle, suit.Id), ctx));
        Assert.Equal(100 - stake, mine.Treasury.Credits); Assert.Equal(theirs + stake, them.Treasury.Credits); Assert.Equal(LadderSystem.SettledOut, suit.Choice);
        // where the law is doubtful, or leans to the house that sued, they do not
        var (tc2, w2, me2, mine2, ctx2, them2, _) = Quarrel(); var g2 = LadderSystem.Grounds(w2, mine2, them2)[0];
        Assert.True(tc2.Executor.TryExecute(new BringSuitCommand(me2.Id, them2.Id, g2.Matter, Offices), ctx2));
        var s2 = LadderSystem.SuitsNaming(w2, mine2.Id).Single(); s2.Weight = LadderSystem.Doubtful; s2.RightId = them2.Id;
        LadderSystem.Tick(w2, Offices, Content.Policies); Assert.False(s2.Countered);
    }

    [Fact]
    public void A_house_that_is_sued_may_counter_sue_once_and_then_whoever_loses_pays_twice_the_stake()
    {
        var (tc, w, me, mine, ctx, them, judge) = Quarrel(); var kin = Person(w, mine, "kin");
        var suit = Against(w, mine, them, judge, LadderSystem.Plain, rightIsMine: false);
        SuitCommand Cmd(string actor, string id) => new(actor, SuitCommand.Counter, id);
        Assert.Contains("only the head", Cmd(kin.Id, suit.Id).Refusal(ctx)); Assert.Equal("no such suit is waiting", Cmd(me.Id, "no_such_suit").Refusal(ctx));
        Assert.Contains("read plainly, is against you", Cmd(me.Id, suit.Id).Refusal(ctx));
        suit.Weight = LadderSystem.Arguable; suit.RightId = mine.Id; int fee = LadderSystem.FeeOf(10);
        mine.Treasury.Credits = 1; Assert.Equal($"the filing fee is {Money.Credits(fee)} and the house has {Money.Held(mine.Treasury)}", Cmd(me.Id, suit.Id).Refusal(ctx)); mine.Treasury.Credits = 100;
        mine.Treasury.Influence = 0; Assert.Equal("needs 1 clout and the house has 0", Cmd(me.Id, suit.Id).Refusal(ctx)); mine.Treasury.Influence = 5;
        int regard = Regard(w, them, mine);
        Assert.True(tc.Executor.TryExecute(Cmd(me.Id, suit.Id), ctx), Cmd(me.Id, suit.Id).Refusal(ctx));
        Assert.True(suit.Countered); Assert.Equal(20, LadderSystem.StakeOf(suit)); Assert.Equal(100 - fee, mine.Treasury.Credits); Assert.Equal(5 - LadderSystem.CounterClout, mine.Treasury.Influence);
        Assert.Equal(regard - LadderSystem.CounterRegard, Regard(w, them, mine));
        Assert.Equal("a counter-suit has been brought already", Cmd(me.Id, suit.Id).Refusal(ctx));
        Assert.Contains("and your counter-suit", Docket.Of(w, Offices, mine.Id).Single().Title);
        // its own suit is not countered
        var own = Against(w, mine, them, judge); own.PlaintiffId = mine.Id; Assert.Contains("your own suit", Cmd(me.Id, own.Id).Refusal(ctx)); w.OfficeWork.Remove(own);
        // heard: an upright praetor rules on the law, and the house that sued pays twice what it asked
        mine.Treasury.Credits = 100; them.Treasury.Credits = 100; LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.Equal(LadderSystem.Law, suit.Choice); Assert.Equal(mine.Id, suit.WinnerId); Assert.Contains(w.Judgments, j => j.WinnerId == mine.Id && j.LoserId == them.Id && j.Sum == 20);
        Assert.True(w.HasFlag("case_won")); Assert.False(w.HasFlag(LadderSystem.SuitWonFlag));
    }

    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx, House accuser, Character judge, Character kin, OfficeDeed deed, Indictment charge) Charged(string nature = "Just", params string[] kinNature)
    {
        var (tc, w, me, mine, ctx) = Start(); var others = Others(w, mine); Calm(w); Hate(w, others[0], mine);
        var judge = Person(w, others[2], "judge", nature); Sit(w, judge); var kin = Person(w, mine, "kin", kinNature); kin.Ambition = "";
        var deed = Deed(w, kin, LadderSystem.TookGift, LadderSystem.Praetor, others[3].Id);
        LadderSystem.Tick(w, Offices, Content.Policies); w.Turn++; Sit(w, judge);
        w.OfficeWork.Clear(); w.Judgments.Clear();   // the bench's own business of that year is not what these are about
        return (tc, w, me, mine, ctx, others[0], judge, kin, deed, w.Indictments.Single());
    }

    [Fact]
    public void A_charge_against_one_of_the_house_is_not_heard_the_year_it_is_brought_and_the_house_is_told()
    {
        var (_, w, _, mine, _, accuser, judge, kin, deed, charge) = Charged();
        Assert.False(deed.Answered); Assert.Equal("", charge.Outcome); Assert.Equal(accuser.Id, charge.AccuserId); Assert.Equal(kin.Id, charge.AccusedId); Assert.Equal(judge.Id, charge.JudgeId);
        Assert.False(w.WorldCounters.ContainsKey(LadderSystem.BarredKey(kin.Id))); Assert.Equal(0, w.Counter("convictions"));
        // who charges whom, with what, before which praetor, and what conviction would cost
        var told = Assert.Single(w.News, n => n.Tone == "warn" && n.Text.Contains("charges " + kin.Name));
        Assert.Contains(accuser.Name, told.Text); Assert.Contains("took a gift to rule as praetor", told.Text); Assert.Contains(judge.Name + ", praetor", told.Text); Assert.Contains("upright", told.Text);
        Assert.Contains(Money.Credits(LadderSystem.FineCredits), told.Text); Assert.Contains($"may not stand for {LadderSystem.BarYears} years", told.Text); Assert.Equal("senate:ladder", told.Target);
        // nobody else may bring it while it waits, and it stands on the docket
        Assert.True(LadderSystem.UnderCharge(w, deed.Id)); Assert.DoesNotContain(deed, LadderSystem.Chargeable(w, Others(w, mine)[4].Id));
        var line = Assert.Single(Docket.Of(w, Offices, mine.Id)); Assert.Equal(Docket.Charge, line.Kind); Assert.Equal($"{accuser.Name} charges {kin.Name}", line.Title); Assert.Contains("took a gift", line.Over);
        Assert.Equal(LadderSystem.FineCredits, line.Sum); Assert.Equal(judge.Name, line.Judge); Assert.Contains("upright", line.JudgeIs); Assert.Equal("at the end of this year", line.Heard); Assert.Equal("", line.Pleader);
        Assert.Contains("1 charge", AdvisorReports.Build(w, Content).First(s => s.Role == HouseholdSystem.Advocate).Line);
        // nothing is done: it is heard, and before an upright praetor the record is enough
        int legit = mine.Legitimacy; LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.Equal(LadderSystem.Proved, charge.Outcome); Assert.True(deed.Answered); Assert.True(w.Counter(LadderSystem.BarredKey(kin.Id)) > w.Turn); Assert.Equal(legit - LadderSystem.ConvictLegitimacy, mine.Legitimacy);
        Assert.True(w.HasFlag(LadderSystem.KinExposedFlag)); Assert.Empty(Docket.Of(w, Offices, mine.Id));
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Text.Contains("finds the charge against " + kin.Name + " proved"));
        Assert.Contains(w.Judgments, j => j.Kind == "charge" && j.LoserId == mine.Id && j.How.Contains("found proved"));
    }

    [Fact]
    public void Charges_between_two_other_houses_are_still_heard_the_year_they_are_brought()
    {
        var (_, w, _, mine, _) = Start(); var others = Others(w, mine); Calm(w); Hate(w, others[0], others[1]);
        Sit(w, Person(w, others[2], "judge", "Just")); var thief = Person(w, others[1], "thief"); var deed = Deed(w, thief);
        LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.True(deed.Answered); Assert.Empty(w.Indictments); Assert.True(w.Counter(LadderSystem.BarredKey(thief.Id)) > w.Turn);
    }

    [Fact]
    public void A_pleader_may_be_sent_to_answer_a_charge_and_before_a_praetor_for_sale_a_good_one_keeps_the_verdict_from_being_sold()
    {
        var (tc, w, me, mine, ctx, accuser, judge, kin, deed, charge) = Charged("Patient"); Unskilled(w, accuser); accuser.Treasury.Credits = 500; mine.Treasury.Credits = 20;
        var pleader = Person(w, mine, "pleader"); pleader.Skills["law"] = 10; pleader.Skills["oratory"] = 10; var weak = Person(w, mine, "weak"); var child = Person(w, mine, "child"); child.Age = 9;
        ChargeCommand Cmd(string actor, string id, string who) => new(actor, ChargeCommand.Plead, id, Offices, who);
        Assert.Contains("only the head", Cmd(kin.Id, charge.Id, pleader.Id).Refusal(ctx)); Assert.Equal("no such charge is waiting", Cmd(me.Id, "no_such_charge", pleader.Id).Refusal(ctx));
        Assert.Contains("no one of age", Cmd(me.Id, charge.Id, child.Id).Refusal(ctx)); Assert.Contains("no one of age", Cmd(me.Id, charge.Id, judge.Id).Refusal(ctx));
        w.PoolsFor(me.Id).Influence = 0; Assert.Contains("needs 1 influence", Cmd(me.Id, charge.Id, pleader.Id).Refusal(ctx)); w.PoolsFor(me.Id).Influence = 5;
        // before a praetor who goes by the pleading, what sending each would do is said before they are sent
        Assert.Contains("in a hundred that it is found proved", LadderSystem.ChargePleadSays(w, charge, pleader)); Assert.Contains("40 with nobody to answer", LadderSystem.ChargePleadSays(w, charge, weak));
        Assert.Equal(0, LadderSystem.ProvedChance(w, accuser.Id, LadderSystem.DefenceOf(w, pleader))); Assert.Equal(40, LadderSystem.ProvedChance(w, accuser.Id, 0));
        // before one who is for sale the richer house has the verdict, unless whoever answers is good enough that it cannot be sold in open court
        judge.NatureTraits.Add("Ruthless");
        Assert.Contains("is for sale", LadderSystem.ChargePleadSays(w, charge, weak)); Assert.Contains($"it would take {LadderSystem.AdvocateHolds}", LadderSystem.ChargePleadSays(w, charge, weak));
        Assert.Contains("in a hundred that it is found proved", LadderSystem.ChargePleadSays(w, charge, pleader));
        var sold = LadderSystem.Verdict(w, deed, accuser.Id, judge, 0); Assert.True(sold.guilty); Assert.Contains("for sale", sold.why);
        var kept = LadderSystem.Verdict(w, deed, accuser.Id, judge, LadderSystem.DefenceOf(w, pleader)); Assert.False(kept.guilty); Assert.StartsWith("neither", kept.why);
        accuser.Treasury.Credits = 0; Assert.False(LadderSystem.Verdict(w, deed, accuser.Id, judge, 0).guilty); accuser.Treasury.Credits = 500;   // and the richer house may be the accused's
        judge.NatureTraits.Remove("Ruthless");
        // and before an upright one pleading moves nothing, which the docket says
        judge.NatureTraits.Add("Just"); Assert.Contains("pleading will not move it", LadderSystem.ChargePleadSays(w, charge, pleader)); judge.NatureTraits.Remove("Just");

        Assert.True(tc.Executor.TryExecute(Cmd(me.Id, charge.Id, pleader.Id), ctx), Cmd(me.Id, charge.Id, pleader.Id).Refusal(ctx));
        Assert.Equal(pleader.Id, charge.PleaderId); Assert.Equal(5 - LadderSystem.PleadInfluence, w.PoolsFor(me.Id).Influence);
        Assert.Equal("they are already to plead it", Cmd(me.Id, charge.Id, pleader.Id).Refusal(ctx)); Assert.Equal(pleader.Name, Docket.Of(w, Offices, mine.Id).Single().Pleader);
        int legit = mine.Legitimacy; LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.Equal(LadderSystem.LetGo, charge.Outcome); Assert.True(deed.Answered); Assert.False(w.WorldCounters.ContainsKey(LadderSystem.BarredKey(kin.Id))); Assert.Equal(legit, mine.Legitimacy);
        Assert.True(w.HasFlag(LadderSystem.KinAcquittedFlag)); Assert.Contains(w.News, n => n.Tone == "good" && n.Text.Contains("lets " + kin.Name + " go"));
        Assert.Contains(kin.Life!.Deeds, e => e.Text.Contains("let go")); Assert.Contains(pleader.Life!.Deeds, e => e.Text.Contains("Answered the charge against " + kin.Name));
        Assert.Contains(w.History, h => h.Text.Contains(pleader.Name + " answers it"));

        // the same charge with nobody sent, against a house whose pleader is good: it is found proved
        var (_, w2, _, _, _, accuser2, _, kin2, deed2, charge2) = Charged("Patient");
        var theirs = Person(w2, accuser2, "their_pleader"); theirs.Skills["law"] = 10; theirs.Skills["oratory"] = 10; Assert.Equal(100, LadderSystem.ProvedChance(w2, accuser2.Id, 0));
        LadderSystem.Tick(w2, Offices, Content.Policies);
        Assert.Equal(LadderSystem.Proved, charge2.Outcome); Assert.True(deed2.Answered); Assert.True(w2.Counter(LadderSystem.BarredKey(kin2.Id)) > w2.Turn);
        Assert.Contains(w2.History, h => h.Text.Contains("nobody answers it"));
    }

    [Fact]
    public void A_charge_may_be_admitted_and_the_fine_paid_for_a_smaller_loss_than_a_conviction()
    {
        var (tc, w, me, mine, ctx, accuser, _, kin, deed, charge) = Charged(); var wronged = w.House(deed.AgainstId)!;
        ChargeCommand Cmd(string actor, string id) => new(actor, ChargeCommand.Admit, id, Offices);
        Assert.Contains("only the head", Cmd(kin.Id, charge.Id).Refusal(ctx)); Assert.Equal("no such charge is waiting", Cmd(me.Id, "no_such_charge").Refusal(ctx));
        mine.Treasury.Credits = 4; Assert.Equal($"the fine is {Money.Credits(LadderSystem.FineCredits)} and the house has {Money.Held(mine.Treasury)}", Cmd(me.Id, charge.Id).Refusal(ctx)); mine.Treasury.Credits = 50;
        int legit = mine.Legitimacy, theirs = wronged.Treasury.Credits, accuserLegit = accuser.Legitimacy;
        Assert.True(tc.Executor.TryExecute(Cmd(me.Id, charge.Id), ctx), Cmd(me.Id, charge.Id).Refusal(ctx));
        Assert.Equal(50 - LadderSystem.FineCredits, mine.Treasury.Credits); Assert.Equal(theirs + LadderSystem.FineCredits, wronged.Treasury.Credits);
        Assert.Equal(legit - LadderSystem.AdmitLegitimacy, mine.Legitimacy); Assert.True(LadderSystem.AdmitLegitimacy < LadderSystem.ConvictLegitimacy); Assert.Equal(accuserLegit, accuser.Legitimacy);
        Assert.True(deed.Answered); Assert.Equal(LadderSystem.Admitted, charge.Outcome); Assert.Empty(Docket.Of(w, Offices, mine.Id));
        // no trial: no bar from office, no scandal on the house's candidates
        Assert.False(w.WorldCounters.ContainsKey(LadderSystem.BarredKey(kin.Id))); Assert.False(LadderSystem.Tainted(w, mine.Id)); Assert.DoesNotContain(LadderSystem.ExposedChar, kin.Flags);
        // the lasting record: the ruling, and the life
        var ruling = Assert.Single(w.Judgments); Assert.Equal("charge", ruling.Kind); Assert.Equal(mine.Id, ruling.LoserId); Assert.Equal(accuser.Id, ruling.WinnerId); Assert.Equal(LadderSystem.FineCredits, ruling.Sum); Assert.Contains("admitted before it was heard", ruling.How);
        Assert.Contains(kin.Life!.Deeds, e => e.Text.Contains("Admitted what they did in office"));
        Assert.True(w.HasFlag(LadderSystem.ChargeAdmittedFlag)); Assert.Contains(LadderSystem.AdmittedChar, kin.Flags); Assert.True(Eligible(w, "a_charge_admitted"));
        Assert.Equal("no such charge is waiting", Cmd(me.Id, charge.Id).Refusal(ctx));
        // it is not heard afterwards
        LadderSystem.Tick(w, Offices, Content.Policies); Assert.Equal(LadderSystem.Admitted, charge.Outcome); Assert.Equal(0, w.Counter("convictions")); Assert.Equal(0, w.Counter("prosecutions"));

        // one of the house with a will of their own will not have it admitted for them
        var (_, w2, me2, _, ctx2, _, _, kin2, _, charge2) = Charged("Just", "Proud");
        Assert.Equal($"{kin2.Name} will not admit it: it is their name, and they mean to be heard", new ChargeCommand(me2.Id, ChargeCommand.Admit, charge2.Id, Offices).Refusal(ctx2));
    }

    [Fact]
    public void The_houses_advocate_answers_a_charge_without_being_asked()
    {
        var (tc, w, me, mine, ctx, _, _, _, _, charge) = Charged("Patient");
        var adv = new Character { Id = "kin_advocate", Name = "Test advocate", HouseId = mine.Id, Age = 30, Sex = "female", CareerTrack = "law" }; adv.Skills["law"] = 8; adv.Skills["oratory"] = 8;
        w.Characters[adv.Id] = adv; mine.Members.Add(adv.Id); w.Pools[adv.Id] = ActionPools.ForNpc(); w.Relationship(adv.Id, me.Id).Disposition = 40;
        Assert.True(tc.Executor.TryExecute(new DelegateCommand(me.Id, HouseholdSystem.Advocate, adv.Id, 0), ctx), new DelegateCommand(me.Id, HouseholdSystem.Advocate, adv.Id, 0).Refusal(ctx));
        int influence = w.PoolsFor(me.Id).Influence;
        tc.Household.Tick(w, tc.Executor, ctx);
        Assert.Equal(adv.Id, charge.PleaderId); Assert.Equal(influence, w.PoolsFor(me.Id).Influence); Assert.Contains("is to answer 1 charge", mine.Duty(HouseholdSystem.Advocate)!.LastReport, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(adv.Name, Docket.Of(w, Offices, mine.Id).Single().Pleader);
        LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.NotEqual("", charge.Outcome); Assert.True(w.HasFlag(charge.Outcome == LadderSystem.LetGo ? HouseholdSystem.AdvocateWonFlag : HouseholdSystem.AdvocateLostFlag));
    }

    [Fact]
    public void A_charge_lapses_when_no_praetor_sits_who_could_hear_it_and_stays_on_the_record()
    {
        var (_, w, _, mine, _, _, _, kin, deed, charge) = Charged(); w.Magistrates.Clear();
        LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.Equal(LadderSystem.Lapsed, charge.Outcome); Assert.False(deed.Answered); Assert.False(LadderSystem.UnderCharge(w, deed.Id));
        Assert.Contains(w.News, n => n.Text.Contains("The charge against " + kin.Name + " has lapsed")); Assert.Empty(Docket.Of(w, Offices, mine.Id));
    }

    [Fact]
    public void The_docket_lists_what_the_house_brought_then_what_is_brought_against_it_then_the_charges_and_survives_a_save()
    {
        var (tc, w, me, mine, ctx, accuser, judge, kin, _, charge) = Charged(); var others = Others(w, mine); var them = others[1]; Hate(w, them, mine);
        var pleader = Person(w, mine, "pleader"); charge.PleaderId = pleader.Id;
        var against = Against(w, mine, accuser, judge); Assert.True(tc.Executor.TryExecute(new SuitCommand(me.Id, SuitCommand.Counter, against.Id), ctx));
        var g = LadderSystem.Grounds(w, mine, them)[0]; Assert.True(tc.Executor.TryExecute(new BringSuitCommand(me.Id, them.Id, g.Matter, Offices), ctx), new BringSuitCommand(me.Id, them.Id, g.Matter, Offices).Refusal(ctx));
        var lines = Docket.Of(w, Offices, mine.Id); Assert.Equal(new[] { Docket.Brought, Docket.Against, Docket.Charge }, lines.Select(l => l.Kind));
        foreach (var l in lines) { Assert.NotEqual("", l.Title); Assert.NotEqual("", l.Over); Assert.True(l.Sum > 0); Assert.NotEqual("", l.Judge); Assert.NotEqual("", l.Law); Assert.NotEqual("", l.Heard); Assert.DoesNotContain("(s)", l.Title + l.Over + l.Law); }
        Assert.Equal(them.Id, lines[0].OtherId); Assert.Equal(accuser.Id, lines[1].OtherId); Assert.Equal(accuser.Id, lines[2].OtherId); Assert.Equal(pleader.Name, lines[2].Pleader);

        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        var suit = back.OfficeWork.Single(b => b.PlaintiffId == mine.Id); Assert.True(suit.Held); Assert.Equal(g.Matter, suit.Matter); Assert.Equal(g.Stake, suit.Stake); Assert.False(suit.Countered);
        var countered = back.OfficeWork.Single(b => b.Id == against.Id); Assert.True(countered.Countered); Assert.Equal(accuser.Id, countered.PlaintiffId); Assert.Equal(20, countered.Stake);
        var c = Assert.Single(back.Indictments); Assert.Equal(charge.Id, c.Id); Assert.Equal(charge.DeedId, c.DeedId); Assert.Equal(accuser.Id, c.AccuserId); Assert.Equal(kin.Id, c.AccusedId); Assert.Equal(mine.Id, c.HouseId);
        Assert.Equal(judge.Id, c.JudgeId); Assert.Equal(charge.Turn, c.Turn); Assert.Equal(pleader.Id, c.PleaderId); Assert.Equal("", c.Outcome);
        Assert.Equal(lines.Select(l => l.Kind + l.Id + l.Title + l.Sum + l.Heard), Docket.Of(back, Offices, mine.Id).Select(l => l.Kind + l.Id + l.Title + l.Sum + l.Heard));
        // and under a throne there is no docket
        w.Era = World.EraImperium; Assert.Empty(Docket.Of(w, Offices, mine.Id));
        LadderSystem.Tick(w, Offices, Content.Policies); Assert.Empty(w.Indictments); Assert.Empty(w.OfficeWork);
    }

    [Theory]
    [InlineData("your_suit_is_upheld", LadderSystem.SuitWonFlag, LadderSystem.SuedChar, false)]
    [InlineData("your_suit_is_thrown_out", LadderSystem.SuitLostFlag, LadderSystem.SuedChar, false)]
    [InlineData("a_charge_admitted", LadderSystem.ChargeAdmittedFlag, LadderSystem.AdmittedChar, true)]
    public void Each_scene_of_the_docket_can_be_reached_and_every_answer_to_it_taken(string eventId, string flag, string mark, bool ofTheHouse)
    {
        var answers = Content.Events.First(e => e.Id == eventId).Choices.Select(c => c.Id).ToList(); Assert.InRange(answers.Count, 2, 3);
        foreach (string answer in answers)
        {
            var (_, w, _, mine, _) = Start(); var them = Others(w, mine)[0];
            var who = ofTheHouse ? Person(w, mine, "kin") : Economy.HeadOf(w, them)!; who.Flags.Add(mark); w.WorldFlags.Add(flag);
            var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
            var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); Assert.True(found.def != null, eventId + " is not eligible");
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(found.def!, found.binding), w, answer);
            Assert.False(w.HasFlag(flag), eventId + "/" + answer + " leaves its flag up");
            Assert.Contains(w.History, h => h.Text.Contains(who.Name));
        }
        // the words for it are written, and carry no jargon marks
        string text = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentLocator.FindContentDir(), "text", "docket.json"));
        Assert.Contains("\"" + eventId + "\"", text); Assert.DoesNotContain("!", text); Assert.DoesNotContain("—", text); Assert.DoesNotContain("(s)", text);
    }

    [Fact]
    public void A_saga_played_with_the_docket_open_brings_suits_answers_them_and_never_leaves_one_unheard()
    {
        int brought = 0, heard = 0;
        for (int seed = 1; seed <= 3; seed++)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed); var tc = new TurnController(w, Content);
            var pending = new Dictionary<string, int>();
            for (int y = 0; y < 14 && !w.IsDynastyDead; y++)
            {
                foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
                var me = w.Protagonist!; var mine = w.House(me.HouseId)!; var ctx = new CommandContext(w, TestContent.Rng());
                mine.Treasury.Credits = Math.Max(mine.Treasury.Credits, 40); mine.Treasury.Influence = Math.Max(mine.Treasury.Influence, 4); w.PoolsFor(me.Id).Influence = 6;
                // a suit brought waits at most two turnings of the year
                foreach (var kv in pending.ToList())
                {
                    var b = w.OfficeWork.FirstOrDefault(x => x.Id == kv.Key);
                    if (b == null || b.Choice.Length > 0) { pending.Remove(kv.Key); heard++; } else Assert.True(w.Turn - kv.Value <= 1, $"seed {seed}: {kv.Key} brought in year {kv.Value} is still unheard in year {w.Turn}");
                }
                foreach (var line in Docket.Of(w, Offices, mine.Id))
                {
                    if (line.Kind == Docket.Against) tc.Executor.TryExecute(new SuitCommand(me.Id, SuitCommand.Counter, line.Id), ctx);
                    if (line.Kind == Docket.Charge && !tc.Executor.TryExecute(new ChargeCommand(me.Id, ChargeCommand.Admit, line.Id, Offices), ctx)) tc.Executor.TryExecute(new ChargeCommand(me.Id, ChargeCommand.Plead, line.Id, Offices, me.Id), ctx);
                }
                foreach (var h in w.Houses.Values.Where(h => h.Id != mine.Id).OrderByDescending(h => LadderSystem.Heat(w, mine, h)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(4))
                    foreach (var g in LadderSystem.Grounds(w, mine, h))
                        if (tc.Executor.TryExecute(new BringSuitCommand(me.Id, h.Id, g.Matter, Offices), ctx)) { brought++; pending[LadderSystem.SuitsNaming(w, mine.Id).First(b => b.PlaintiffId == mine.Id && b.Turn == w.Turn).Id] = w.Turn; }
                tc.EndTurn();
            }
        }
        Assert.True(brought > 0, "in three sagas of fourteen years the house never had a quarrel it could take to law");
        Assert.True(heard > 0, "no suit the house brought was ever heard, settled or lapsed");
    }
}
