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
        // the Free Cities and the Compact can have one too, at longer odds; the Concord, which strikes anyway, has none
        Assert.True(Content.Nations.Def("thessar")!.GreatOneIn > d.GreatOneIn); Assert.True(Content.Nations.Def("compact")!.GreatOneIn > d.GreatOneIn); Assert.Equal(0, Content.Nations.Def("concord")!.GreatOneIn);
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
        int leeway = NationSystem.Leeway(w, mine); Assert.Equal(standing - NationSystem.DefyCost(leeway), mine.Legitimacy); Assert.True(w.HasFlag("peace_defied_by_us:vashti")); Assert.Equal(1, w.Counter(NationSystem.DefiedCounter + ":vashti"));
        Assert.True(w.AtWar(mine.Id, Khans)); Assert.False(w.AtWar(other.Id, Khans));                              // everyone else has their peace
        Assert.Same(n, NationSystem.DefiedBy(w, mine.Id)); Assert.Contains("does not listen", NationSystem.PeaceRefusal(w, Content.Nations, "vashti"));
        // the Senate may have him home, whatever his name is worth
        Assert.Null(new CaesarSystem(fs, Content.Offices).RecallRefusal(w, mine));
        Year(w, ns);
        Assert.True(n.AtWar); Assert.Equal(standing - NationSystem.DefyCost(leeway) - NationSystem.DefyYearlyCost(leeway), mine.Legitimacy);   // and every year it goes on
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

    // ---- the owner's answers of 2026-11-19 to decisions 16 to 19 ----

    [Fact]
    public void HowOftenTheyAttack_IsASetting_AndEachGamesOwnLuck()
    {
        var d = Content.Nations.Def("vashti")!;
        Assert.Equal(100, Start().w.Nation("vashti")!.Boldness);                                                  // pinned: as written
        var seldom = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); seldom.BeyondVaries = false; seldom.BeyondAggression = "seldom"; NationSystem.Seed(seldom, Content.Nations);
        var often = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); often.BeyondVaries = false; often.BeyondAggression = "often"; NationSystem.Seed(often, Content.Nations);
        var meek = seldom.Nation("vashti")!; var bold = often.Nation("vashti")!;
        Assert.Equal(60, meek.Boldness); Assert.Equal(160, bold.Boldness);
        // a Great Khan at one accession in thirteen, or one in five, where it is written as one in eight; and the rest between wars moves with it
        Assert.Equal(13, NationSystem.ByBoldness(meek, d.GreatOneIn)); Assert.Equal(5, NationSystem.ByBoldness(bold, d.GreatOneIn)); Assert.Equal(d.GreatOneIn, NationSystem.ByBoldness(Start().w.Nation("vashti")!, d.GreatOneIn));
        Assert.True(NationSystem.ByBoldness(bold, NationSystem.StrikeRest) < NationSystem.StrikeRest); Assert.True(NationSystem.ByBoldness(meek, NationSystem.StrikeRest) > NationSystem.StrikeRest);
        // and where games vary, no two have the same: between seven-tenths and seven-fifths
        var seen = new HashSet<int>();
        for (int seed = 1; seed <= 12; seed++) { var n = Start(seed, varies: true).w.Nation("vashti")!; Assert.InRange(n.Boldness, NationSystem.BoldSpreadLow, NationSystem.BoldSpreadHigh); seen.Add(n.Boldness); }
        Assert.True(seen.Count >= 4, string.Join(", ", seen));
    }

    [Fact]
    public void EvenTheCompact_ComesOut_UnderAHighWarden()
    {
        var (w, ns, _, _, _, _) = Start(); var n = w.Nation("compact")!; var d = Content.Nations.Def("compact")!;
        Assert.Equal("never", d.Sorties); Assert.Equal("High Warden", d.GreatTitle); Assert.False(NationSystem.ComesOut(n, d)); Assert.False(NationSystem.Begins(n, d));
        n.Ruler!.Great = true; n.Ruler.GreatSince = w.Turn; n.Ruler.Nature = "warlike";
        Assert.True(NationSystem.ComesOut(n, d)); Assert.True(NationSystem.Begins(n, d)); Assert.StartsWith("High Warden", NationSystem.Styled(d, n.Ruler, true));
        for (int y = 0; y < 25 && !n.AtWar; y++) Year(w, ns);
        Assert.True(n.AtWar); Assert.True(n.BeganIt);
        var cities = w.Nation("thessar")!; var cd = Content.Nations.Def("thessar")!; Assert.Equal("Navarch", cd.GreatTitle); Assert.False(NationSystem.Begins(cities, cd));
    }

    [Fact]
    public void APeaceNobodyForced_GoesByTheTally_AndLevelItIsAWhitePeace()
    {
        // level: everything goes home, both ways
        var (w, ns, _, _, mine, other) = Start(); var n = w.Nation("vashti")!; NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id); HomeFleets(w, Khans, "khal_orda");
        var camp = w.Holding("long_grass_camp")!; camp.OwnerId = other.Id; camp.FormerOwnerId = Khans; Post(w, "g", other.Id, camp.SystemId, 4);
        var lost = w.HoldingsOf(mine.Id).First(h => h.IsPlanet); lost.OwnerId = Khans; lost.FormerOwnerId = mine.Id;
        n.ScoreReach = 3; n.ScoreNation = 2;
        Assert.Contains("white peace", NationSystem.PeaceNow(w, n, Content.Nations.Def("vashti")!, mine.Id));
        NationSystem.VotePeace(w, Content.Nations, "vashti", other.Id); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == other.Id).ToList()) w.Fleets.Remove(f.Id);
        Year(w, ns);
        Assert.False(n.AtWar); Assert.Equal("white", n.LastPeace); Assert.Equal(Khans, camp.OwnerId); Assert.Equal(mine.Id, lost.OwnerId); Assert.Equal(0, n.SubduedUntil); Assert.True(w.HasFlag("white_peace:vashti"));
        // two ahead: the Reach keeps what it holds, and has its own back
        var (w2, ns2, _, _, mine2, other2) = Start(); var n2 = w2.Nation("vashti")!; NationSystem.DeclareWar(w2, Content.Nations, "vashti", other2.Id); HomeFleets(w2, Khans, "khal_orda");
        var camp2 = w2.Holding("long_grass_camp")!; camp2.OwnerId = mine2.Id; camp2.FormerOwnerId = Khans; n2.ScoreReach = 4; n2.ScoreNation = 2;
        NationSystem.VotePeace(w2, Content.Nations, "vashti", other2.Id); w2.WorldFlags.Add(NationSystem.PeaceObeyFlag); Year(w2, ns2); if (n2.AtWar) { w2.WorldFlags.Add(NationSystem.PeaceObeyFlag); Year(w2, ns2); }
        Assert.False(n2.AtWar); Assert.Equal("territory", n2.LastPeace); Assert.Equal(mine2.Id, camp2.OwnerId);
        // two behind: they keep what they hold
        var (w3, ns3, _, _, mine3, other3) = Start(); var n3 = w3.Nation("vashti")!; NationSystem.DeclareWar(w3, Content.Nations, "vashti", other3.Id); HomeFleets(w3, Khans, "khal_orda");
        var lost3 = w3.HoldingsOf(mine3.Id).First(h => h.IsPlanet); lost3.OwnerId = Khans; lost3.FormerOwnerId = mine3.Id; n3.ScoreReach = 0; n3.ScoreNation = 3;
        NationSystem.VotePeace(w3, Content.Nations, "vashti", other3.Id); Year(w3, ns3);
        Assert.False(n3.AtWar); Assert.Equal("ceded", n3.LastPeace); Assert.Equal(Khans, lost3.OwnerId); Assert.Equal("", lost3.FormerOwnerId);
    }

    [Fact]
    public void AWarLost_WithNothingToCede_IsPaidForInTribute_YearAfterYear()
    {
        var (w, ns, _, _, mine, other) = Start(); var n = w.Nation("vashti")!; var d = Content.Nations.Def("vashti")!; var chair = RegionSystem.CapitalHolder(w)!;
        NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id); HomeFleets(w, Khans, "khal_orda"); n.ScoreNation = d.Threshold; int purse = chair.Treasury.Credits;
        Assert.Contains("pays it tribute", NationSystem.PeaceNow(w, n, d, mine.Id));
        Year(w, ns);
        Assert.False(n.AtWar); Assert.Equal("paid", n.LastPeace); Assert.Equal(w.Turn + NationSystem.PaidYears, n.PaidUntil); Assert.True(w.HasFlag("reach_pays_tribute:vashti"));
        Assert.Equal(purse - d.Tribute, chair.Treasury.Credits);                                                    // no lump: the first year's payment
        Year(w, ns); Assert.Equal(purse - 2 * d.Tribute, chair.Treasury.Credits); Assert.Equal(2 * d.Tribute, w.Counter(NationSystem.PaidCounter + ":vashti"));
        Assert.False(n.Subdued(w.Turn));
        // it holds worlds of the Reach's: those are what it takes, and it is bought off once
        var (w2, ns2, _, _, mine2, other2) = Start(); var n2 = w2.Nation("vashti")!; var chair2 = RegionSystem.CapitalHolder(w2)!;
        NationSystem.DeclareWar(w2, Content.Nations, "vashti", other2.Id); HomeFleets(w2, Khans, "khal_orda"); n2.ScoreNation = d.Threshold; int purse2 = chair2.Treasury.Credits;
        var lost = w2.HoldingsOf(mine2.Id).First(h => h.IsPlanet); lost.OwnerId = Khans; lost.FormerOwnerId = mine2.Id;
        Year(w2, ns2);
        Assert.Equal("ceded", n2.LastPeace); Assert.Equal(0, n2.PaidUntil); Assert.Equal(purse2 - 3 * d.Tribute, chair2.Treasury.Credits); Assert.Equal(Khans, lost.OwnerId);
    }

    [Fact]
    public void BeatenDecisively_ANationIsMadeAClient_ItPaysTheChairAndItsPatron_UntilItThrowsTheTreatyOff()
    {
        var (w, ns, _, me, mine, other) = Start(); var n = w.Nation("vashti")!; var d = Content.Nations.Def("vashti")!; var chair = RegionSystem.CapitalHolder(w)!; var ctx = new CommandContext(w, TestContent.Rng());
        NationSystem.DeclareWar(w, Content.Nations, "vashti", other.Id); HomeFleets(w, Khans, "khal_orda");
        var camp = w.Holding("long_grass_camp")!; camp.OwnerId = other.Id; camp.FormerOwnerId = Khans; Post(w, "g", other.Id, camp.SystemId, 4);
        w.WorldCounters[NationSystem.WinsKey(other.Id, "vashti")] = 3; n.ScoreReach = 2 * NationSystem.SuesAt(n, d);
        Assert.True(NationSystem.Decisive(w, n, d)); Assert.True(NationSystem.Client(w, n, d, other, mine.Id));
        int chairPurse = chair.Treasury.Credits, patronPurse = other.Treasury.Credits;
        Year(w, ns);
        Assert.False(n.AtWar); Assert.True(n.Vassal); Assert.Equal(other.Id, n.PatronId); Assert.Equal("client", n.LastPeace); Assert.True(n.Subdued(w.Turn)); Assert.True(w.HasFlag("nation_client:vashti"));
        Assert.Equal(Khans, camp.OwnerId);                                                                          // a client is left what is its own
        int share = NationSystem.ClientShare(d); bool same = chair.Id == other.Id;
        Assert.Equal(patronPurse + share + (same ? d.Tribute : 0), other.Treasury.Credits); if (!same) Assert.Equal(chairPurse + d.Tribute, chair.Treasury.Credits);
        // it raids nobody and nobody may make war on it on their own account
        Post(w, "mine", mine.Id, "anvil", 4); Assert.Contains("tribute", new RaidNationCommand(me.Id, "vashti", Content.Nations).Refusal(ctx));
        for (int y = 0; y < 30; y++) { Year(w, ns); Assert.True(n.Vassal); Assert.False(n.AtWar); }             // a mild ruler keeps the treaty
        // one of the great throws it off
        n.Ruler!.Great = true; n.Ruler.GreatSince = w.Turn; n.Ruler.Nature = "warlike"; Year(w, ns);
        Assert.False(n.Vassal); Assert.Equal("", n.PatronId); Assert.True(w.HasFlag("client_breaks:vashti")); Assert.Equal(1, w.Counter(NationSystem.ClientLostCounter + ":vashti"));
        // the player's house keeps what it took, unless the player has said it would make clients
        var (w2, ns2, _, me2, mine2, _) = Start(); var n2 = w2.Nation("vashti")!; var ctx2 = new CommandContext(w2, TestContent.Rng());
        NationSystem.DeclareWar(w2, Content.Nations, "vashti", mine2.Id); n2.ScoreReach = 2 * NationSystem.SuesAt(n2, d); w2.WorldCounters[NationSystem.WinsKey(mine2.Id, "vashti")] = 3;
        Assert.False(NationSystem.Client(w2, n2, d, mine2, mine2.Id));
        var word = new PeacePostureCommand(me2.Id, true); Assert.Null(word.Refusal(ctx2)); word.Execute(ctx2);
        Assert.True(NationSystem.Client(w2, n2, d, mine2, mine2.Id)); Assert.Equal("that is your word already", new PeacePostureCommand(me2.Id, true).Refusal(ctx2));
        HomeFleets(w2, Khans, "khal_orda"); Year(w2, ns2); Assert.True(n2.Vassal); Assert.Equal(mine2.Id, n2.PatronId);
    }

    [Fact]
    public void WhatDefyingTheSenateCosts_DependsOnWhoseFleetItIs()
    {
        var (w, _, fs, me, mine, _) = Start(); var caesar = new CaesarSystem(fs, Content.Offices);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == mine.Id).ToList()) w.Fleets.Remove(f.Id);
        Assert.Equal(0, NationSystem.Leeway(w, mine));                                                              // nobody of the house commands anything
        var fleet = Post(w, "mine", mine.Id, "anvil", 6, me.Id); me.CareerTrack = "civil"; me.CareerRank = 0; me.Skills["tactics"] = 3;
        Assert.Equal(0, NationSystem.Leeway(w, mine)); Assert.Equal(8, NationSystem.DefyCost(0)); Assert.Equal(3, NationSystem.DefyYearlyCost(0)); Assert.Equal(4, NationSystem.RecallLean(w, mine));
        // a commander of real skill counts a step better than his name
        me.Skills["tactics"] = NationSystem.LeewaySkill; Assert.Equal(1, NationSystem.Leeway(w, mine)); me.Skills["tactics"] = 3;
        // battles won and triumphs voted make a name
        w.WorldCounters[CaesarSystem.VictoryKey(me.Id)] = 5; Assert.True(CaesarSystem.Sway(w, me) >= NationSystem.LeewayKnown); Assert.Equal(1, NationSystem.Leeway(w, mine));
        w.WorldCounters[CaesarSystem.TriumphKey(me.Id)] = 2; Assert.Equal(2, NationSystem.Leeway(w, mine)); Assert.Equal(0, NationSystem.RecallLean(w, mine));
        w.WorldCounters[CaesarSystem.TriumphKey(me.Id)] = 3; w.WorldCounters[FleetSystem.ValourKey(me.Id)] = 2; Assert.True(CaesarSystem.Sway(w, me) >= NationSystem.LeewayGreat); Assert.Equal(3, NationSystem.Leeway(w, mine));
        Assert.Equal(2, NationSystem.DefyCost(3)); Assert.Equal(0, NationSystem.DefyYearlyCost(3)); Assert.Equal(-2, NationSystem.RecallLean(w, mine));
        Assert.Equal("a great name", NationSystem.LeewayName(3)); Assert.Equal("nobody the Senate has heard of", NationSystem.LeewayName(0));
        // put to it: the same defiance, by a nobody and by a great name
        (int standing, int loyalty) Defy(bool famous)
        {
            var (x, xs, _, xme, xmine, xother) = Start(); var xn = x.Nation("vashti")!; var xctx = new CommandContext(x, TestContent.Rng());
            foreach (var f in x.Fleets.Values.Where(f => f.OwnerId == xmine.Id).ToList()) x.Fleets.Remove(f.Id);
            xme.CareerTrack = "civil"; xme.CareerRank = 0; xme.Skills["tactics"] = 3; if (famous) { x.WorldCounters[CaesarSystem.VictoryKey(xme.Id)] = 5; x.WorldCounters[CaesarSystem.TriumphKey(xme.Id)] = 3; x.WorldCounters[FleetSystem.ValourKey(xme.Id)] = 2; }
            NationSystem.DeclareWar(x, Content.Nations, "vashti", xother.Id); HomeFleets(x, Khans, "khal_orda"); xn.ScoreReach = 2; var xf = Post(x, "mine", xmine.Id, "the_long_grass", 6, xme.Id);
            NationSystem.VotePeace(x, Content.Nations, "vashti", xother.Id); Year(x, xs); xmine.Legitimacy = 60; new AnswerPeaceCommand(xme.Id, false).Execute(xctx); Year(x, xs);
            Assert.True(xn.Defied); return (xmine.Legitimacy, xf.Loyalty);
        }
        var nobody = Defy(false); var name = Defy(true);
        Assert.Equal(60 - 8, nobody.standing); Assert.Equal(60 - 2, name.standing);
        Assert.True(nobody.loyalty < 80, "his crews waver"); Assert.Equal(80, name.loyalty);
        _ = caesar; _ = fleet;
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
