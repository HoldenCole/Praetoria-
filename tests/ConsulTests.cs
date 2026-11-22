using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The consulship (docs/REPUBLIC.md stage 2): elected offices, the parties' nominees, the tally by weight, no two terms running, the veto.</summary>
public class ConsulTests
{
    private static World Rings(int unrest) { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { unrest })!; }
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static (World w, SenateSystem senate, CommandContext ctx) Chamber()
    {
        var w = Rings(0); w.Turn = 9; w.House("vega")!.Title = "baron"; w.Pools["self"].Influence = 6;
        foreach (var (id, name) in new[] { ("drake", "House Drake"), ("sato", "House Sato"), ("ferro", "House Ferro"), ("pike", "House Pike"), ("wren", "House Wren") })
        {
            w.Houses[id] = new House { Id = id, Name = name, Title = "baron", Legitimacy = 30, Treasury = new Resources { Credits = 30 } };
            w.Characters[id + "_head"] = new Character { Id = id + "_head", Name = name.Replace("House ", "") + " Head", HouseId = id, Age = 40, CareerTrack = "law", CareerRank = 2 }; w.Houses[id].Members.Add(id + "_head");
        }
        w.Char("aurelian_head")!.CareerTrack = "military"; w.Char("aurelian_head")!.CareerRank = 3; w.House("aurelian")!.SphereInfluence["senate"] = 6;
        w.Char("self")!.CareerTrack = "law"; w.Char("self")!.CareerRank = 2;
        var senate = new SenateSystem(Content.Motions, Content.Titles, null, Content.Offices) { Policies = Content.Policies };
        FactionSystem.Use(w, Content.Policies); FactionSystem.Seed(w, Content.Parties, id => senate.Weight(w, id));
        // the ladder (docs/LADDER.md): a consul is forty and has been praetor, as the people of this chamber are and have
        foreach (var c in w.Characters.Values.Where(c => c.CareerRank >= 2)) c.Age = Math.Max(c.Age, 40);
        LadderSystem.Seed(w);
        return (w, senate, new CommandContext(w, TestContent.Rng()));
    }

    [Fact]
    public void TheConsulOffices_AreElected_NotCommissioned_AndTakeAnyTrack()
    {
        var consuls = ElectionSystem.ConsulOffices(Content.Offices).ToList();
        Assert.Equal(2, consuls.Count); Assert.Equal("consul_senior", consuls[0].Id); Assert.True(consuls[0].Veto); Assert.Equal(10, consuls[0].TermTurns); Assert.Equal("", consuls[0].Track);
        var (w, senate, ctx) = Chamber();
        Assert.Equal("the consuls are elected, not commissioned", new ProposeMotionCommand("self", "commission", "vega", senate, "consul_senior").Refusal(ctx));
        Assert.NotNull(OfficeSystem.Candidate(w, consuls[0], "aurelian"));   // a military Commander qualifies for an office with no track
    }

    /* docs/PLAYTHROUGH.md, Medium, The Senate: "Ask your patron or your bench leader to move a motion for you, or for money, a
       post or backing" (P6, P17, P54). */
    [Fact]
    public void AHouseAsksItsPatronForCoin_ForItsBacking_OrToMoveAMotion_AndOwesAFavourForIt()
    {
        var (w, senate, ctx) = Chamber(); Func<string, int> wt = id => senate.Weight(w, id); var mine = w.House("vega")!; var exec = new CommandExecutor();
        var patron = w.House("aurelian")!; mine.PatronId = patron.Id; patron.Treasury.Credits = 120; var pHead = Economy.HeadOf(w, patron)!;
        Assert.Contains(AskAbove.Above(w, "vega"), x => x.house.Id == "aurelian");
        string stranger = w.Houses.Keys.Where(h => h != "vega" && !World.Outsider(h)).OrderBy(h => h, StringComparer.Ordinal).First(h => !AskAbove.Above(w, "vega").Any(x => x.house.Id == h));
        Assert.Equal("they are neither your patron nor the leader of your bench", new AskAboveCommand("self", stranger, AskAbove.Coin, Content.Offices, senate).Refusal(ctx));

        // not heard by one who thinks too little of you, and told by how much
        w.Relationship(pHead.Id, "self").Disposition = 4;
        string? no = new AskAboveCommand("self", "aurelian", AskAbove.Coin, Content.Offices, senate).Refusal(ctx);
        Assert.Contains("thinks +4 of you", no); Assert.Contains("(6 short)", no);
        w.Relationship(pHead.Id, "self").Disposition = 20;

        // coin: a sixth of its treasury, fifteen at the most; given, and a favour owed
        int coin = AskAbove.CoinOf(patron); Assert.Equal(AskAbove.CoinMost, coin); int had = mine.Treasury.Credits;
        var ask = new AskAboveCommand("self", "aurelian", AskAbove.Coin, Content.Offices, senate); Assert.Null(ask.Refusal(ctx)); Assert.Contains($"gives you {coin} credits", ask.Says(w));
        Assert.True(exec.TryExecute(ask, ctx));
        Assert.Equal(had + coin, mine.Treasury.Credits); Assert.Equal(120 - coin, patron.Treasury.Credits); Assert.True(WealthSystem.FavourOwed(w, "vega", "aurelian"));
        // one debt at a time: nothing more until the favour is settled
        Assert.Contains("you owe House Aurelian a favour already", new AskAboveCommand("self", "aurelian", AskAbove.Coin, Content.Offices, senate).Refusal(ctx));
        w.WorldCounters.Remove(WealthSystem.FavourKey("vega", "aurelian"));
        Assert.Contains("not again before year", new AskAboveCommand("self", "aurelian", AskAbove.Coin, Content.Offices, senate).Refusal(ctx));
        w.Turn += AskAbove.EveryYears; patron.Treasury.Credits = AskAbove.CoinKeeps; Assert.Contains("no coin to spare", new AskAboveCommand("self", "aurelian", AskAbove.Coin, Content.Offices, senate).Refusal(ctx));

        // a motion moved in the patron's name: it is on the docket as the patron's, and a favour is owed
        var kind = Content.Motions.Defs.Select(d => d.Id).FirstOrDefault(k => AskAbove.MoveRefusal(w, ctx, mine, patron, k, "", senate, "", "", "") == null);
        if (kind == null)
        {
            // no motion without a target is open to the patron in this chamber: one with a house named, then
            var named = Content.Motions.Defs.Select(d => d.Id).SelectMany(k => w.Houses.Keys.Where(h => h != "vega" && h != "aurelian" && !World.Outsider(h)).Select(h => (k, h))).FirstOrDefault(x => AskAbove.MoveRefusal(w, ctx, mine, patron, x.k, x.h, senate, "", "", "") == null);
            Assert.NotNull(named.k); int open0 = w.Motions.Count(m => m.Open);
            Assert.True(exec.TryExecute(new AskToMoveCommand("self", "aurelian", named.k, named.h, senate), ctx));
            Assert.Equal(open0 + 1, w.Motions.Count(m => m.Open)); Assert.Equal("aurelian", w.Motions.Last(m => m.Open).ProposerHouseId); Assert.True(WealthSystem.FavourOwed(w, "vega", "aurelian"));
        }
        else
        {
            int open = w.Motions.Count(m => m.Open);
            Assert.True(exec.TryExecute(new AskToMoveCommand("self", "aurelian", kind, "", senate), ctx));
            Assert.Equal(open + 1, w.Motions.Count(m => m.Open)); Assert.Equal("aurelian", w.Motions.Last(m => m.Open).ProposerHouseId); Assert.True(WealthSystem.FavourOwed(w, "vega", "aurelian"));
            Assert.Contains(w.History, h => h.Text.Contains("at the asking of House Vega"));
        }

        // its backing for consul: its seats are counted for you, once you have declared and it does not stand itself
        var (w2, senate2, ctx2) = Chamber(); Func<string, int> wt2 = id => senate2.Weight(w2, id); var mine2 = w2.House("vega")!;
        string backer = w2.Houses.Keys.Where(h => h != "vega" && !World.Outsider(h)).OrderBy(h => h, StringComparer.Ordinal).First(h => !ElectionSystem.Nominees(w2, Content.Offices, wt2).Any(n => n.houseId == h) && Economy.HeadOf(w2, w2.House(h)!) != null);
        mine2.PatronId = backer; w2.Relationship(Economy.HeadOf(w2, w2.House(backer)!)!.Id, "self").Disposition = 20;
        Assert.Contains("you are not standing for consul", new AskAboveCommand("self", backer, AskAbove.Backing, Content.Offices, senate2).Refusal(ctx2));
        w2.WorldFlags.Add(ElectionSystem.StandFlag("vega")); var noms = ElectionSystem.Nominees(w2, Content.Offices, wt2); Assert.Contains(noms, n => n.houseId == "vega");
        int before = ElectionSystem.Tally(w2, Content.Offices, wt2, noms)["vega"];
        Assert.True(new CommandExecutor().TryExecute(new AskAboveCommand("self", backer, AskAbove.Backing, Content.Offices, senate2), ctx2));
        Assert.Equal("vega", ElectionSystem.PledgedTo(w2, backer)); Assert.True(WealthSystem.FavourOwed(w2, "vega", backer));
        var noms2 = ElectionSystem.Nominees(w2, Content.Offices, wt2);
        Assert.True(ElectionSystem.Tally(w2, Content.Offices, wt2, noms2)["vega"] >= before);
    }

    /* docs/PLAYTHROUGH.md, Medium, The Senate: "Benches: ... lead by seats or by a contest" (P9, P99). */
    [Fact]
    public void AMemberAsksTheBenchToChoose_AndLeadsItIfMoreOfItsSeatsAreBehindThem()
    {
        var (w, senate, ctx) = Chamber(); Func<string, int> wt = id => senate.Weight(w, id); var mine = w.House("vega")!; var exec = new CommandExecutor();
        var bench = FactionSystem.Of(w, "vega"); if (bench == null) return;
        foreach (var f in w.Factions) { f.Members.Remove("drake"); f.Members.Remove("sato"); f.Members.Remove("ferro"); }
        bench.Members.AddRange(new[] { "drake", "sato", "ferro" }.Where(h => !bench.Members.Contains(h))); bench.LeaderHouseId = "drake";
        var leadHead = Economy.HeadOf(w, w.House("drake")!)!; var others = bench.Members.Where(h => h != "vega" && h != "drake").ToList();
        // the bench thinks better of its leader: asked, it keeps them, and the asking is remembered
        foreach (var m in others) { var head = Economy.HeadOf(w, w.House(m)!)!; w.Relationship(head.Id, "self").Disposition = -5; w.Relationship(head.Id, leadHead.Id).Disposition = 20; }
        var count = FactionSystem.LeadCount(w, wt, "vega")!.Value; Assert.Equal(wt("vega"), count.mine); Assert.Equal(wt("drake") + others.Sum(wt), count.leaders);
        var cmd = new ChallengeLeaderCommand("self", senate); Assert.Null(cmd.Refusal(ctx)); Assert.Contains("You would lose", cmd.Says(w));
        int regard = w.Relationship(leadHead.Id, "self").Disposition, influence = w.Pools["self"].Influence;
        Assert.True(exec.TryExecute(cmd, ctx));
        Assert.Equal("drake", bench.LeaderHouseId); Assert.Equal(regard - FactionSystem.LostRegard, w.Relationship(leadHead.Id, "self").Disposition); Assert.Equal(influence - FactionSystem.ChallengeInfluence, w.Pools["self"].Influence);
        Assert.Contains("not again before year", new ChallengeLeaderCommand("self", senate).Refusal(ctx));
        // years on, the bench thinks better of the house: asked, it chooses the house
        w.Turn += FactionSystem.ChallengeEvery; w.Pools["self"].Influence = 6;
        foreach (var m in others) { var head = Economy.HeadOf(w, w.House(m)!)!; w.Relationship(head.Id, "self").Disposition = 40; w.Relationship(head.Id, leadHead.Id).Disposition = 0; }
        var now = FactionSystem.LeadCount(w, wt, "vega")!.Value; Assert.Equal(wt("vega") + others.Sum(wt), now.mine);
        if (now.mine > now.leaders)
        {
            Assert.Contains("You would lead", new ChallengeLeaderCommand("self", senate).Says(w)); regard = w.Relationship(leadHead.Id, "self").Disposition;
            Assert.True(exec.TryExecute(new ChallengeLeaderCommand("self", senate), ctx));
            Assert.Equal("vega", bench.LeaderHouseId); Assert.Equal(regard - FactionSystem.WonRegard, w.Relationship(leadHead.Id, "self").Disposition); Assert.Contains(w.News, n => n.Text.Contains("has chosen you to lead it"));
            Assert.Equal("you lead it already", new ChallengeLeaderCommand("self", senate).Refusal(ctx));
        }
    }

    /* docs/PLAYTHROUGH.md, Medium, The Senate: "a nomination inside the bench" (P87). */
    [Fact]
    public void ABenchPutsForwardWhicheverOfItsMembersTheBenchIsBehind_NotThePlayerByRight()
    {
        var (w, senate, ctx) = Chamber(); Func<string, int> wt = id => senate.Weight(w, id);
        var bench = FactionSystem.Of(w, "vega"); if (bench == null) return;
        // put a weightier member and two more on the player's bench, and make another house its leader
        foreach (var f in w.Factions) { f.Members.Remove("drake"); f.Members.Remove("sato"); f.Members.Remove("ferro"); }
        bench.Members.AddRange(new[] { "drake", "sato", "ferro" }.Where(h => !bench.Members.Contains(h))); bench.LeaderHouseId = "drake";
        w.House("drake")!.SphereInfluence["senate"] = 8; w.WorldFlags.Add(ElectionSystem.StandFlag("vega"));
        Assert.True(ElectionSystem.Eligible(w, Content.Offices, "vega")); Assert.True(ElectionSystem.Eligible(w, Content.Offices, "drake"));
        string rival = bench.Members.Where(h => h != "vega" && ElectionSystem.Eligible(w, Content.Offices, h)).OrderByDescending(wt).ThenBy(h => h, StringComparer.Ordinal).First();
        string rivalHead = Economy.HeadOf(w, w.House(rival)!)!.Id; var others = bench.Members.Where(h => h != "vega" && h != rival).ToList(); Assert.NotEmpty(others);

        // the rest of the bench thinks better of the other: the bench puts them forward, and the player stands alone
        foreach (var m in others) { var head = Economy.HeadOf(w, w.House(m)!)!; w.Relationship(head.Id, "self").Disposition = -10; w.Relationship(head.Id, rivalHead).Disposition = 20; }
        var lost = ElectionSystem.BenchContest(w, Content.Offices, wt, bench)!.Value;
        Assert.False(lost.won); Assert.Equal(rival, lost.rivalId); Assert.Equal(wt("vega"), lost.mine); Assert.Equal(wt(rival) + others.Sum(wt), lost.rival); Assert.Empty(lost.forMine);
        var noms = ElectionSystem.Nominees(w, Content.Offices, wt);
        Assert.Contains(noms, n => n.houseId == rival && n.partyId == bench.Id); Assert.Contains(noms, n => n.houseId == "vega" && n.partyId == null);
        var tally = ElectionSystem.Tally(w, Content.Offices, wt, noms); Assert.True(tally["vega"] < tally[rival]);   // its own seats, and the bench behind the other

        // the rest of the bench thinks better of the player: the bench puts the player forward
        foreach (var m in others) { var head = Economy.HeadOf(w, w.House(m)!)!; w.Relationship(head.Id, "self").Disposition = 30; w.Relationship(head.Id, rivalHead).Disposition = 0; }
        var won = ElectionSystem.BenchContest(w, Content.Offices, wt, bench)!.Value;
        Assert.Equal(wt("vega") + others.Sum(wt), won.mine); Assert.Equal(won.mine >= won.rival, won.won);
        if (won.won) { var n2 = ElectionSystem.Nominees(w, Content.Offices, wt); Assert.Contains(n2, n => n.houseId == "vega" && n.partyId == bench.Id); Assert.DoesNotContain(n2, n => n.houseId == rival && n.partyId == bench.Id); }

        // a leader who stands is the bench's nominee, whatever the bench thinks
        foreach (var m in others) { var head = Economy.HeadOf(w, w.House(m)!)!; w.Relationship(head.Id, "self").Disposition = -50; w.Relationship(head.Id, rivalHead).Disposition = 50; }
        bench.LeaderHouseId = "vega"; Assert.True(ElectionSystem.BenchContest(w, Content.Offices, wt, bench)!.Value.won);
        Assert.Contains(ElectionSystem.Nominees(w, Content.Offices, wt), n => n.houseId == "vega" && n.partyId == bench.Id);
    }

    /* docs/PLAYTHROUGH.md, Medium, The Senate: "a pledge of your votes at the consular election" (P59). */
    [Fact]
    public void AHouseThatIsNotStanding_PledgesItsSeats_AndIsOwedAFavourIfItsNomineeIsElected()
    {
        var (w, senate, ctx) = Chamber(); Func<string, int> wt = id => senate.Weight(w, id); var mine = w.House("vega")!;
        var noms = ElectionSystem.Nominees(w, Content.Offices, wt); Assert.DoesNotContain(noms, n => n.houseId == "vega");
        string? own = ElectionSystem.BenchNominee(w, Content.Offices, wt, "vega");
        string to = noms.Select(n => n.houseId).First(h => h != own); var toHead = Economy.HeadOf(w, w.House(to)!)!;
        // nothing to take back yet; not to a house that is not standing
        Assert.Equal("you have pledged your seats to nobody", new PledgeVotesCommand("self", "", Content.Offices, senate).Refusal(ctx));
        string notStanding = w.Houses.Keys.First(h => h != "vega" && !World.Outsider(h) && !noms.Any(n => n.houseId == h));
        Assert.EndsWith("is not standing", new PledgeVotesCommand("self", notStanding, Content.Offices, senate).Refusal(ctx));

        // pledged: the seats are counted for them and not where they went before
        var before = ElectionSystem.Tally(w, Content.Offices, wt, noms); int seats = wt("vega"); Assert.True(seats > 0);
        int regard = w.Relationship(toHead.Id, "self").Disposition; int ownRegard = own != null ? w.Relationship(Economy.HeadOf(w, w.House(own)!)!.Id, "self").Disposition : 0;
        var cmd = new PledgeVotesCommand("self", to, Content.Offices, senate); Assert.Null(cmd.Refusal(ctx)); Assert.Contains($"{Praetoria.Core.Counted.Seats(seats)} {Praetoria.Core.Counted.Are(seats)} counted for", cmd.Says(w));
        Assert.True(new CommandExecutor().TryExecute(cmd, ctx)); Assert.Equal(to, ElectionSystem.PledgedTo(w, "vega"));
        var after = ElectionSystem.Tally(w, Content.Offices, wt, noms);
        Assert.Equal(before[to] + seats, after[to]); Assert.Equal(before.Values.Sum(), after.Values.Sum());
        Assert.Equal(regard + ElectionSystem.PledgeRegard, w.Relationship(toHead.Id, "self").Disposition);
        if (own != null) Assert.Equal(ownRegard - ElectionSystem.BenchSlight, w.Relationship(Economy.HeadOf(w, w.House(own)!)!.Id, "self").Disposition);
        Assert.Contains("pledged to them already", new PledgeVotesCommand("self", to, Content.Offices, senate).Refusal(ctx));

        // taken back, it costs; given again and kept to the count, a nominee who is elected owes a favour
        Assert.True(new CommandExecutor().TryExecute(new PledgeVotesCommand("self", "", Content.Offices, senate), ctx));
        Assert.Null(ElectionSystem.PledgedTo(w, "vega")); Assert.Equal(regard + ElectionSystem.PledgeRegard - ElectionSystem.UnpledgeRegard, w.Relationship(toHead.Id, "self").Disposition);
        Assert.Equal(before[to], ElectionSystem.Tally(w, Content.Offices, wt, noms)[to]);
        string winner = ElectionSystem.Tally(w, Content.Offices, wt, noms).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
        Assert.True(new CommandExecutor().TryExecute(new PledgeVotesCommand("self", winner, Content.Offices, senate), ctx));
        Assert.False(WealthSystem.FavourOwed(w, winner, "vega"));
        w.Turn = 10; ElectionSystem.Hold(w, Content.Offices, wt);
        Assert.True(WealthSystem.FavourOwed(w, winner, "vega")); Assert.Null(ElectionSystem.PledgedTo(w, "vega")); Assert.Contains(w.News, n => n.Text.Contains("owes your house a favour"));

        // a house that stands itself keeps its own seats
        var (w2, senate2, ctx2) = Chamber(); w2.WorldFlags.Add(ElectionSystem.StandFlag("vega"));
        var noms2 = ElectionSystem.Nominees(w2, Content.Offices, id => senate2.Weight(w2, id)); string other = noms2.Select(n => n.houseId).First(h => h != "vega");
        Assert.StartsWith("you are standing yourself", new PledgeVotesCommand("self", other, Content.Offices, senate2).Refusal(ctx2));
    }

    [Fact]
    public void TheElection_SeatsTheTwoHeaviestNominees_AndNeitherStandsAgainNextTerm()
    {
        var (w, senate, _) = Chamber(); Func<string, int> wt = id => senate.Weight(w, id);
        var noms = ElectionSystem.Nominees(w, Content.Offices, wt); Assert.True(noms.Count >= 2); Assert.All(noms, n => Assert.NotNull(n.partyId));
        w.Turn = 10; Assert.True(ElectionSystem.Due(w)); senate.Tick(w);
        Assert.True(w.HasFlag("election_held")); Assert.Equal(1, w.Counter("elections"));
        var senior = OfficeSystem.HolderHouse(w, "consul_senior"); var junior = OfficeSystem.HolderHouse(w, "consul_junior");
        Assert.NotNull(senior); Assert.NotNull(junior); Assert.NotEqual(senior, junior);
        Assert.Equal("aurelian", senior);                                                       // the duke's party weighs most
        Assert.Equal(10, w.Counter("consul_term:" + senior)); Assert.False(ElectionSystem.Eligible(w, Content.Offices, senior!));
        Assert.Contains(w.History, h => h.Text.StartsWith("The Senate elects its consuls"));
        // next election: the sitting consuls may not stand; the chairs change hands
        w.Turn = 20; senate.Tick(w);
        Assert.NotEqual(senior, OfficeSystem.HolderHouse(w, "consul_senior")); Assert.Equal(2, w.Counter("elections"));
    }

    [Fact]
    public void ThePlayer_Stands_AndIsNominatedInTheirPartysPlace()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor(); Func<string, int> wt = id => senate.Weight(w, id);
        var mine = FactionSystem.Of(w, "vega")!;
        Assert.Null(new StandForConsulCommand("self", Content.Offices).Refusal(ctx)); Assert.True(ex.TryExecute(new StandForConsulCommand("self", Content.Offices), ctx));
        Assert.Equal(4, w.Pools["self"].Influence); Assert.True(w.HasFlag(ElectionSystem.StandFlag("vega"))); Assert.Equal("you already stand", new StandForConsulCommand("self", Content.Offices).Refusal(ctx));
        var noms = ElectionSystem.Nominees(w, Content.Offices, wt); Assert.Contains(noms, n => n.houseId == "vega" && n.partyId == mine.Id);
        var tally = ElectionSystem.Tally(w, Content.Offices, wt, noms); Assert.True(tally["vega"] >= mine.Members.Sum(wt) - 0);   // the party's weight is behind us
        w.Turn = 10; senate.Tick(w);
        Assert.DoesNotContain(w.WorldFlags, f => f.StartsWith("stands_for_consul:"));
        if (OfficeSystem.HolderHouse(w, "consul_senior") == "vega" || OfficeSystem.HolderHouse(w, "consul_junior") == "vega") Assert.True(w.HasFlag("consul_elected"));
    }

    [Fact]
    public void AConsul_VetoesOnce_ATerm_AndRivalConsuls_StrikeWhatStrikesAtThem()
    {
        var (w, senate, ctx) = Chamber(); var ex = new CommandExecutor();
        OfficeSystem.Grant(w, Content.Offices.Def("consul_senior")!, "vega", w.Char("self")!); w.WorldCounters["consul_term:vega"] = w.Turn;
        var m = senate.Add(w, "corwin", "levy", "");
        Assert.Equal("not your own motion", new VetoCommand("self", senate.Add(w, "vega", "recognition", "vega").Id, Content.Offices, senate).Refusal(ctx));
        Assert.Null(new VetoCommand("self", m.Id, Content.Offices, senate).Refusal(ctx)); Assert.True(ex.TryExecute(new VetoCommand("self", m.Id, Content.Offices, senate), ctx));
        Assert.True(m.Resolved); Assert.False(m.Passed); Assert.True(m.Vetoed); Assert.Equal("vega", m.VetoedBy); Assert.Equal(1, w.Counter("vetoes"));
        Assert.Equal("the veto is spent this term", new VetoCommand("self", senate.Add(w, "sato", "levy", "").Id, Content.Offices, senate).Refusal(ctx));
        // a rival consul strikes a censure aimed at it before the vote
        var (w2, senate2, _) = Chamber(); OfficeSystem.Grant(w2, Content.Offices.Def("consul_junior")!, "corwin", w2.Char("corwin_head")!);
        var censure = senate2.Add(w2, "vega", "censure", "corwin"); w2.Turn++; senate2.Tick(w2);
        Assert.True(censure.Vetoed); Assert.Equal("corwin", censure.VetoedBy); Assert.True(w2.HasFlag("vetoed_mine")); Assert.Contains(w2.History, h => h.Text.Contains("vetoes:"));
    }
}
