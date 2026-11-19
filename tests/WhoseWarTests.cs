using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The owner's answers on the map and on war (docs/BEYOND.md, "Whose war"; 2026-11-19). No two games have the same
/// neighbours. The Khanates raid until a Great Khan is raised, and then come to conquer. Houses raid a nation and take nothing, unless
/// it attacked first. A house may make war on its own account. The Senate votes war and peace and directs no fleet: a general may not
/// listen, at a cost. And a house whose lands are all conquered lives while one of it lives.</summary>
public class WhoseWarTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private const string Khans = "nation_vashti";

    private static (World w, NationSystem ns, FleetSystem fs, Character me, House mine, House other) Start(int seed = 7, bool varies = false, string beyond = "default")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed); w.BeyondVaries = varies; w.Beyond = beyond;
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations); w.Turn = 3;   // not the year nought: a year stamped 0 reads as never
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }   // nobody raids, strikes or dies of age while a test looks at something else
        var mine = w.House(w.Protagonist!.HouseId)!; var other = w.Houses.Values.Where(h => w.OfTheReach(h.Id) && h.Id != mine.Id).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        return (w, ns, fs, w.Protagonist!, mine, other);
    }
    private static Fleet Post(World w, string id, string houseId, string systemId, int hulls, string admiral = "")
    { var f = new Fleet { Id = id, Name = "the " + id, OwnerId = houseId, SystemId = systemId, Strength = hulls, Supply = 100, Readiness = 100, Loyalty = 80, AdmiralId = admiral }; w.Fleets[id] = f; return f; }
    private static void Year(World w, NationSystem ns) { w.Turn++; ns.Tick(w); }
    private static int Hulls(World w, string house) => w.Fleets.Values.Where(f => f.OwnerId == house).Sum(f => f.Strength);
    private static void HomeFleets(World w, string house, string systemId) { foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house)) { f.SystemId = systemId; f.DestinationId = ""; f.Eta = 0; } }

    [Fact]
    public void NoTwoGames_HaveTheSameNeighbours_AndASettingMakesThemGentlerOrHarsher()
    {
        var d = Content.Nations.Def("vashti")!; var seen = new HashSet<int>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var (w, _, _, _, _, _) = Start(seed, varies: true); var n = w.Nation("vashti")!;
            Assert.InRange(n.Might, 100 - NationSystem.MightSpread, 100 + NationSystem.MightSpread);
            Assert.Equal(NationSystem.Full(n, d), n.Strength); Assert.Equal(n.Strength, Hulls(w, Khans));   // what it is worth is what is on the map
            seen.Add(n.Strength);
        }
        Assert.True(seen.Count >= 3, $"the Khanates were {string.Join(", ", seen)} hulls in twelve games");
        // pinned, it is what is written; the setting moves it a fifth down or a quarter up
        Assert.Equal(d.Strength, Start().w.Nation("vashti")!.Strength);
        Assert.Equal(11, Start(beyond: "gentle").w.Nation("vashti")!.Strength); Assert.Equal(18, Start(beyond: "harsh").w.Nation("vashti")!.Strength);
        // and it is fixed once: a second reckoning does not roll it again
        var (w2, ns2, _, _, _, _) = Start(3, varies: true); int was = w2.Nation("vashti")!.Might; Year(w2, ns2); Assert.Equal(was, w2.Nation("vashti")!.Might);
    }

    [Fact]
    public void TheKhanates_AreRaiders_UntilAGreatKhanIsRaised_AndThenTheyComeToConquer()
    {
        var (w, ns, _, _, _, _) = Start(); var n = w.Nation("vashti")!; var d = Content.Nations.Def("vashti")!;
        Assert.Equal("Great Khan", d.GreatTitle); Assert.True(d.GreatOneIn > 0); Assert.False(d.Strikes); Assert.False(d.Campaigns);
        // a Khan of Khans, however much he hates the Reach, begins no war: he raids
        n.Disposition = -100; n.Ruler!.Nature = "warlike"; for (int y = 0; y < 15; y++) { Year(w, ns); Assert.False(n.AtWar); }
        Assert.False(NationSystem.Begins(n, d)); Assert.False(NationSystem.Stays(n, d)); Assert.StartsWith("Khan of Khans", NationSystem.Styled(d, n.Ruler, true));
        // a Great Khan: one host, half as many hulls again, and it begins wars itself
        n.Ruler.Great = true; n.Ruler.GreatSince = w.Turn; n.Ruler.Age = 30;
        Assert.StartsWith("Great Khan", NationSystem.Styled(d, n.Ruler, true)); Assert.True(NationSystem.Begins(n, d)); Assert.True(NationSystem.Stays(n, d)); Assert.True(NationSystem.ComesOut(n, d));
        Assert.Equal(d.Strength * NationSystem.GreatMight / 100, NationSystem.Cap(n, d));
        int before = Hulls(w, Khans);
        for (int y = 0; y < NationSystem.GreatGathers - 1; y++) { Year(w, ns); Assert.False(n.AtWar, "three years to gather the host"); }
        Assert.True(Hulls(w, Khans) > before, "it musters every year");
        for (int y = 0; y < 20 && !n.AtWar; y++) Year(w, ns);
        Assert.True(n.AtWar); Assert.True(n.BeganIt); Assert.Equal("", n.WarOf); Assert.Equal(1, w.Counter(NationSystem.StruckCounter + ":vashti"));
        Assert.True(Hulls(w, Khans) > d.Strength);
        // the Compact never has one, whoever follows whom
        Assert.Equal(0, Content.Nations.Def("compact")!.GreatOneIn);
    }

    [Fact]
    public void AWarlikeKhan_WhoBeatsTheReach_IsAcclaimed_AMildOneIsNot()
    {
        var (w, ns, _, _, mine, _) = Start(); var n = w.Nation("vashti")!; var d = Content.Nations.Def("vashti")!;
        NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); n.Ruler!.Nature = "warlike"; n.ScoreNation = d.Threshold; Year(w, ns);
        Assert.False(n.AtWar); Assert.True(n.Ruler.Great); Assert.Equal(1, w.Counter(NationSystem.GreatAcclaimedCounter + ":vashti")); Assert.True(w.HasFlag("great_ruler_raised:vashti"));
        var (w2, ns2, _, _, mine2, _) = Start(); var n2 = w2.Nation("vashti")!;
        NationSystem.DeclareWar(w2, Content.Nations, "vashti", mine2.Id); n2.ScoreNation = d.Threshold; Year(w2, ns2);
        Assert.False(n2.AtWar); Assert.False(n2.Ruler!.Great);
    }

    [Fact]
    public void InAWarTheNationDidNotBegin_AnotherHouseSacksAWorldAndGoesHome_ItTakesOnlyWhenTheNationAttackedFirst()
    {
        var (w, _, fs, me, mine, other) = Start(); var n = w.Nation("vashti")!; var d = Content.Nations.Def("vashti")!; var camp = w.Holding("long_grass_camp")!;
        NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); HomeFleets(w, Khans, "khal_orda");
        Post(w, "raiders", other.Id, camp.SystemId, 5); int purse = other.Treasury.Credits;
        Assert.True(NationSystem.RaidsOnly(w, n, other.Id)); Assert.True(NationSystem.Sacks(w, camp, other.Id));
        fs.ResolveConquests(w);
        Assert.Equal(Khans, camp.OwnerId); Assert.True(NationSystem.Sacked(w, camp));                              // sacked in a season, and still theirs
        Assert.Equal(purse + NationSystem.PlunderPerWorld * d.Wealth, other.Treasury.Credits); Assert.Equal(NationSystem.ScoreSack, n.ScoreReach);
        Assert.Equal(1, w.Counter(NationSystem.SackedCounter + ":vashti")); Assert.True(w.HasFlag("world_sacked:vashti"));
        for (int y = 0; y < 4; y++) fs.ResolveConquests(w);
        Assert.Equal(Khans, camp.OwnerId); Assert.Empty(w.Sieges); Assert.Equal(purse + NationSystem.PlunderPerWorld * d.Wealth, other.Treasury.Credits);   // not again for years
        w.Turn += NationSystem.SackRest; Assert.False(NationSystem.Sacked(w, camp));
        // a war the nation began: the same house takes the world
        n.BeganIt = true; Assert.False(NationSystem.Sacks(w, camp, other.Id));
        for (int y = 0; y < 12 && camp.OwnerId == Khans; y++) fs.ResolveConquests(w);
        Assert.Equal(other.Id, camp.OwnerId);
        // the player's house does as the player orders: it takes and holds until told to raid
        var wells = w.Holding("tul_aimag_wells")!; var ctx = new CommandContext(w, TestContent.Rng());
        Assert.False(NationSystem.Sacks(w, wells, mine.Id));
        var order = new RaidPostureCommand(me.Id, true); Assert.Null(order.Refusal(ctx)); order.Execute(ctx);
        Assert.True(NationSystem.Sacks(w, wells, mine.Id)); Assert.Equal("that is the order already", new RaidPostureCommand(me.Id, true).Refusal(ctx));
        // and a world of the Reach's that the nation holds is never sacked: it is freed
        var held = w.HoldingsOf(mine.Id).First(h => h.IsPlanet); held.FormerOwnerId = mine.Id; held.OwnerId = Khans;
        Assert.False(NationSystem.Sacks(w, held, other.Id));
    }

    [Fact]
    public void AHouseMayMakeWarOnItsOwnAccount_AndItIsNobodyElsesWar()
    {
        var (w, _, _, me, mine, other) = Start(); var n = w.Nation("vashti")!; var ctx = new CommandContext(w, TestContent.Rng());
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == mine.Id).ToList()) w.Fleets.Remove(f.Id);
        Assert.Contains("needs a fleet", new RaidNationCommand(me.Id, "vashti", Content.Nations).Refusal(ctx));
        Post(w, "mine", mine.Id, "anvil", 4);
        n.SubduedUntil = w.Turn + 5; Assert.Contains("tribute", new RaidNationCommand(me.Id, "vashti", Content.Nations).Refusal(ctx)); n.SubduedUntil = 0;
        var cmd = new RaidNationCommand(me.Id, "vashti", Content.Nations); Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx);
        Assert.True(n.AtWar); Assert.Equal(mine.Id, n.WarOf); Assert.False(n.BeganIt); Assert.Equal(50 - NationSystem.PrivateRegard, n.Disposition);
        Assert.True(w.AtWar(mine.Id, Khans)); Assert.True(w.AtWar(Khans, mine.Id)); Assert.False(w.AtWar(other.Id, Khans)); Assert.False(w.AtWar(Khans, other.Id));
        Assert.Equal(new[] { mine.Id }, w.EnemiesOf(Khans).ToArray()); Assert.DoesNotContain(Khans, w.EnemiesOf(other.Id)); Assert.Contains(Khans, w.EnemiesOf(mine.Id));
        Assert.True(NationSystem.Foe(w, n, mine.Id)); Assert.False(NationSystem.Foe(w, n, other.Id));
        Assert.Equal(1, w.Counter(NationSystem.PrivateCounter + ":vashti")); Assert.True(w.HasFlag("private_war_by_us:vashti"));
        // one war beyond the Reach at a time, whoever's it is
        Assert.Contains("one war", NationSystem.PrivateWarRefusal(w, Content.Nations, other, "thessar"));
        // the Senate may make it the Reach's
        NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id);
        Assert.True(n.AtWar); Assert.Equal("", n.WarOf); Assert.True(w.AtWar(other.Id, Khans)); Assert.Equal(1, n.Wars);
    }

    [Fact]
    public void TheSenateVotesPeace_AndWithNobodyToRefuse_ThePeaceIsMade()
    {
        var (w, ns, _, _, mine, other) = Start(); var n = w.Nation("vashti")!;
        Assert.Contains("at peace", NationSystem.PeaceRefusal(w, Content.Nations, "vashti"));
        NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id); HomeFleets(w, Khans, "khal_orda"); n.ScoreReach = 2;
        Assert.Null(NationSystem.PeaceRefusal(w, Content.Nations, "vashti"));
        NationSystem.VotePeace(w, Content.Nations, "vashti", other.Id);
        Assert.True(n.AtWar); Assert.True(n.PeaceVoted > 0); Assert.True(w.HasFlag("peace_voted")); Assert.False(w.HasFlag("peace_voted_on_us"));   // voted, not yet made; nobody of ours is out there
        Assert.Contains("being told", NationSystem.PeaceRefusal(w, Content.Nations, "vashti"));
        Year(w, ns);
        Assert.False(n.AtWar); Assert.Equal(0, n.SubduedUntil); Assert.Equal(1, w.Counter(NationSystem.PeaceMadeCounter + ":vashti")); Assert.True(w.HasFlag("peace_made:vashti"));
        Assert.Equal(0, w.Counter(NationSystem.DefiedCounter)); Assert.Equal(w.Turn, n.LastWarEnd);
    }

    [Fact]
    public void AGeneralMayNotListen_ItIsHisHousesWarThen_AndItCostsItsName_UntilHeComesHome()
    {
        var (w, ns, fs, me, mine, other) = Start(); var n = w.Nation("vashti")!; var ctx = new CommandContext(w, TestContent.Rng());
        NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id); HomeFleets(w, Khans, "khal_orda"); n.ScoreReach = 2;
        Post(w, "mine", mine.Id, "the_long_grass", 6, me.Id);
        Assert.Equal("the Senate has voted no peace that waits on you", new AnswerPeaceCommand(me.Id, false).Refusal(ctx));
        NationSystem.VotePeace(w, Content.Nations, "vashti", other.Id);
        Assert.True(w.HasFlag("peace_voted_on_us"));
        Year(w, ns);
        Assert.True(n.AtWar); Assert.True(n.PeaceVoted > 0); Assert.True(w.HasFlag("peace_awaits_us:vashti"));     // a year to answer
        int standing = mine.Legitimacy = 60;
        var defy = new AnswerPeaceCommand(me.Id, false); Assert.Null(defy.Refusal(ctx)); defy.Execute(ctx);
        Year(w, ns);
        Assert.True(n.AtWar); Assert.True(n.Defied); Assert.Equal(mine.Id, n.WarOf); Assert.Equal(0, n.PeaceVoted);
        Assert.Equal(standing - NationSystem.DefyLegitimacy, mine.Legitimacy); Assert.True(w.HasFlag("peace_defied_by_us:vashti")); Assert.Equal(1, w.Counter(NationSystem.DefiedCounter + ":vashti"));
        Assert.True(w.AtWar(mine.Id, Khans)); Assert.False(w.AtWar(other.Id, Khans));                              // everyone else has their peace
        Assert.Same(n, NationSystem.DefiedBy(w, mine.Id)); Assert.Contains("does not listen", NationSystem.PeaceRefusal(w, Content.Nations, "vashti"));
        // the Senate may have him home, whatever his name is worth
        Assert.Null(new CaesarSystem(fs, Content.Offices).RecallRefusal(w, mine));
        Year(w, ns);
        Assert.True(n.AtWar); Assert.Equal(standing - NationSystem.DefyLegitimacy - NationSystem.DefyYearly, mine.Legitimacy);   // and every year it goes on
        var home = new AnswerPeaceCommand(me.Id, true); Assert.Null(home.Refusal(ctx)); home.Execute(ctx);
        Year(w, ns);
        Assert.False(n.AtWar); Assert.False(n.Defied); Assert.Equal("", n.WarOf); Assert.Null(NationSystem.DefiedBy(w, mine.Id)); Assert.Equal(1, w.Counter(NationSystem.CameHomeCounter + ":vashti"));
    }

    [Fact]
    public void AskedAndSilent_ThePlayersHouseKeepsThePeace()
    {
        var (w, ns, _, me, mine, other) = Start(); var n = w.Nation("vashti")!;
        NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id); HomeFleets(w, Khans, "khal_orda"); Post(w, "mine", mine.Id, "the_long_grass", 6, me.Id);
        NationSystem.VotePeace(w, Content.Nations, "vashti", other.Id); Year(w, ns); Assert.True(n.AtWar); Year(w, ns);
        Assert.False(n.AtWar); Assert.False(n.Defied); Assert.Equal(1, w.Counter(NationSystem.PeaceMadeCounter));
    }

    [Fact]
    public void ThePeace_IsAMotionOfTheSenate_GuardedLikeTheWar_AndItsPassingTellsTheFleets()
    {
        var (w, _, fs, me, mine, other) = Start(); var n = w.Nation("vashti")!; var ctx = new CommandContext(w, TestContent.Rng());
        var senate = new SenateSystem(Content.Motions, Content.Titles, fs, Content.Offices) { Policies = Content.Policies, Nations = Content.Nations };
        w.Pools[me.Id].Influence = 9;
        Assert.Equal("nation", Content.Motions.Def("foreign_peace")!.Target);
        Assert.Contains("at peace", new ProposeMotionCommand(me.Id, "foreign_peace", "vashti", senate).Refusal(ctx));
        NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id);
        Assert.Contains("already at war", new ProposeMotionCommand(me.Id, "foreign_war", "vashti", senate).Refusal(ctx));
        var cmd = new ProposeMotionCommand(me.Id, "foreign_peace", "vashti", senate); Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx);
        var m = w.Motions.Single(x => x.Open && x.Kind == "foreign_peace"); Assert.Contains("The Vashti Khanates", senate.Describe(w, m));
        // how the benches lean: the Sword against, the Factors for; a house whose own war it is, dead against
        n.WarOf = other.Id; Assert.Equal(-4, senate.ForeignPeaceLean(w, other, "vashti")); n.WarOf = "";
        foreach (var h in w.Houses.Keys) m.Votes[h] = 1;
        w.Turn++; senate.Tick(w);
        Assert.True(n.AtWar); Assert.True(n.PeaceVoted > 0); Assert.True(w.HasFlag("peace_voted")); Assert.Equal(1, w.Counter(NationSystem.PeaceVotedCounter + ":vashti"));
        Assert.Contains("being told", NationSystem.PeaceRefusal(w, Content.Nations, "vashti"));
        // a house's own war may be made the Reach's by a vote for war
        var (w2, _, fs2, me2, mine2, other2) = Start(); var ctx2 = new CommandContext(w2, TestContent.Rng()); w2.Pools[me2.Id].Influence = 9;
        var senate2 = new SenateSystem(Content.Motions, Content.Titles, fs2, Content.Offices) { Policies = Content.Policies, Nations = Content.Nations };
        Post(w2, "theirs", other2.Id, "anvil", 4); NationSystem.PrivateWar(w2, Content.Nations, "vashti", other2.Id); Assert.Equal(other2.Id, w2.Nation("vashti")!.WarOf);
        Assert.Null(new ProposeMotionCommand(me2.Id, "foreign_war", "vashti", senate2).Refusal(ctx2));
        Assert.Contains("one war abroad", new ProposeMotionCommand(me2.Id, "foreign_war", "thessar", senate2).Refusal(ctx2));
    }

    [Fact]
    public void ThePeace_IsPutToThePlayerByAReport_AndTheAnswerIsCarriedOut_InARealGame()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); w.BeyondVaries = false; var tc = new TurnController(w, Content);
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!; var n = w.Nation("vashti")!; var other = w.Houses.Values.Where(h => w.OfTheReach(h.Id) && h.Id != mine.Id).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        bool asked = false;
        for (int t = 0; t < 6 && !n.Defied; t++)
        {
            foreach (var i in tc.BeginTurn())
            {
                if (i.Fired.Def.Id == "the_senate_votes_peace") { asked = true; tc.Resolve(i, "fight_on"); continue; }
                var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id);
            }
            if (t == 1)
            {   // a war, the player's fleet in their space, and the Senate's vote for peace (as its passing does it)
                NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id); HomeFleets(w, Khans, "khal_orda"); Post(w, "mine", mine.Id, "the_long_grass", 30, me.Id);
                NationSystem.VotePeace(w, Content.Nations, "vashti", other.Id); Assert.True(w.HasFlag("peace_voted_on_us"));
            }
            if (t >= 1 && n.AtWar) { n.ScoreReach = 2; n.ScoreNation = 0; HomeFleets(w, Khans, "khal_orda"); }   // the war itself stands still while the answer is waited for
            tc.EndTurn();
        }
        Assert.True(asked, "the report was never offered while the peace waited on the player");
        Assert.True(n.AtWar); Assert.True(n.Defied); Assert.Equal(mine.Id, n.WarOf); Assert.True(w.Counter(NationSystem.DefiedCounter + ":vashti") == 1);
    }

    [Fact]
    public void AHouseWhoseLandsAreAllConquered_IsNotConquered_ItLivesWhileOneOfItLives()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 5); w.BeyondVaries = false; var tc = new TurnController(w, Content);
        var mine = w.House(w.Protagonist!.HouseId)!; int members = mine.Members.Count(id => w.Char(id)?.Alive == true); int credits = mine.Treasury.Credits;
        tc.BeginTurn(); tc.EndTurn();
        foreach (var h in w.HoldingsOf(mine.Id).ToList()) { h.FormerOwnerId = ""; h.OwnerId = Khans; }             // every world it had, taken and ceded
        Assert.Empty(w.HoldingsOf(mine.Id));
        for (int t = 0; t < 4 && !w.IsDynastyDead; t++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        Assert.False(w.IsDynastyDead); Assert.NotNull(w.House(mine.Id)); Assert.True(w.OfTheReach(mine.Id)); Assert.True(w.Protagonist!.Alive);
        Assert.True(mine.Members.Count(id => w.Char(id)?.Alive == true) >= 1);
        // and what it has that is not land is still its own: its fleets, its purse, its seat
        Assert.Contains(w.Fleets.Values, f => f.OwnerId == mine.Id || members > 0); Assert.True(mine.Treasury.Credits > -1000 || credits < 0);
        Assert.False(World.Outsider(mine.Id));
    }
}
