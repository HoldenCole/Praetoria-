using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>What five playthroughs found (docs/PLAYTHROUGH.md, "the fifteen to fix first"), held shut.</summary>
public class PlaythroughFixTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (World w, TurnController tc, House house, Character me, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!;
        return (w, tc, w.House(me.HouseId)!, me, new CommandContext(w, TestContent.Rng()));
    }
    private static void Fill(CommandContext ctx, Character me) { var p = ctx.PoolsOf(me.Id); p.Influence = 6; p.Treasury = 6; p.Agents = 6; }
    private static void Hulls(World w, string houseId, int n)
    {
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId).ToList()) w.Fleets.Remove(f.Id);
        if (n > 0) w.Fleets["t_" + houseId] = new Fleet { Id = "t_" + houseId, OwnerId = houseId, Name = "T", SystemId = w.Holdings.Values.First(h => h.IsPlanet).SystemId, Strength = n };
    }

    [Fact]
    public void NoRivalIsGrantedAWorldOfThePlayers_OverThePlayersHead()
    {
        // 1: a landless rival petitioned "the house with most worlds" and the player's colony, and another player's home world, went
        var (w, _, house, _, _) = Start(); foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet).Take(8)) h.OwnerId = house.Id; w.HoldingsChanged();
        int mine = w.HoldingsOf(house.Id).Count(h => h.IsPlanet); Assert.True(w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).All(h => w.HoldingsOf(h.Id).Count(x => x.IsPlanet) <= mine));
        foreach (var rival in w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id))) Assert.NotEqual(house.Id, PetitionFiefCommand.Grantor(w, rival.Id)?.Id);
        // the player may still be granted one by another house
        foreach (var h in w.HoldingsOf(house.Id).ToList()) h.OwnerId = w.Houses.Values.First(x => x.Id != house.Id && !World.Outsider(x.Id)).Id; w.HoldingsChanged();
        Assert.NotNull(PetitionFiefCommand.Grantor(w, house.Id)); Assert.NotEqual(house.Id, PetitionFiefCommand.Grantor(w, house.Id)!.Id);
    }

    [Fact]
    public void ATitle_IsNotClimbedByClicking()
    {
        // 3: "Seize it by force" with no ships on turn 1; three petitions in one year; First Citizen with no world
        var (w, tc, house, me, ctx) = Start(); var prog = tc.Progression!; me.CareerRank = 3; house.Title = Title.Landless; house.Legitimacy = 100;
        ctx.PoolsOf(me.Id); foreach (var h in w.HoldingsOf(house.Id).ToList()) h.OwnerId = "nobody"; w.HoldingsChanged();
        // to seize a rank takes ships of the house's own: two for each rung of the rank
        Hulls(w, house.Id, 0); Assert.Contains("hulls of the house's own", prog.SeizeRefusal(w, me.Id)); Assert.False(prog.CanSeize(w, me.Id));
        Hulls(w, house.Id, ProgressionSystem.SeizeHullsPerRank); Assert.Null(prog.SeizeRefusal(w, me.Id)); prog.Seize(w, me.Id); Assert.Equal(Title.Knight, house.Title);
        // one rung a year, by any road
        Hulls(w, house.Id, 20); Assert.Contains("one a year", prog.SeizeRefusal(w, me.Id)); Assert.Contains("one a year", prog.PetitionRefusal(w, me.Id)); Assert.False(prog.CanPetition(w, me.Id));
        w.Turn++; Assert.Null(prog.PetitionRefusal(w, me.Id)); prog.Petition(w, me.Id); Assert.Equal(Title.Baron, house.Title);
        // from Legate up the rank wants land under it, however well the house is thought of
        w.Turn++; Assert.Contains("holds a world", prog.PetitionRefusal(w, me.Id)); house.Claims.Add(Title.Count); Assert.Contains("holds a world", prog.ClaimRefusal(w, me.Id));
        w.Holdings.Values.First(h => h.IsPlanet).OwnerId = house.Id; w.HoldingsChanged(); Assert.Null(prog.PetitionRefusal(w, me.Id)); prog.Petition(w, me.Id); Assert.Equal(Title.Count, house.Title);
        w.Turn++; Assert.Contains("2 worlds", prog.PetitionRefusal(w, me.Id));
        // legitimacy is still asked for, and said
        house.Title = Title.Landless; house.Legitimacy = 3; w.Turn++; Assert.StartsWith("needs legitimacy 10", prog.PetitionRefusal(w, me.Id));
        // the screen is given the cost, the reason and the question to ask first
        var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content); Assert.All(model.Standing!.Paths, p => { Assert.NotEmpty(p.Cost); Assert.NotEmpty(p.Confirm); if (!p.Available) Assert.False(string.IsNullOrEmpty(p.Refusal)); });
    }

    [Fact]
    public void AVentureJustFounded_SellsForWhatItCost_NotForWhatItWillEarn()
    {
        // 4: a skimmer fleet that cost 12 credits and 6 materials sold for 105 the year it was finished
        var (w, tc, _, _, _) = Start(); var vs = tc.Ventures!; var b = w.Businesses.Values.OrderByDescending(x => vs.Value(w, x)).First(x => vs.Value(w, x) > (Content.Businesses.Def(x.Kind)?.Value(x.Level) ?? 0));
        int value = vs.Value(w, b), book = Content.Businesses.Def(b.Kind)!.Value(b.Level); w.Turn = 10;
        b.Founded = 10; Assert.Equal(book, vs.Trading(w, b)); Assert.True(vs.MarketPrice(w, b) <= book);
        b.Founded = 8; Assert.InRange(vs.Trading(w, b), book + 1, value - 1);
        b.Founded = 10 - VentureSystem.Seasoning; Assert.Equal(value, vs.Trading(w, b)); Assert.Equal(Math.Max(vs.Salvage(b), value * VentureSystem.MarketPct / 100), vs.MarketPrice(w, b));
        b.Founded = 0; Assert.Equal(value, vs.Trading(w, b));   // one that was always there
    }

    [Fact]
    public void ThereIsOneHeir_AndEveryScreenReadsTheSameLine()
    {
        // 5: the header said the son, the will said the rival, and after the succession the heir was the mother who had stepped down
        var (w, _, house, me, _) = Start(); var kin = w.Characters.Values.Where(c => c.Alive && c.HouseId == house.Id && c.Id != me.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToList(); Assert.True(kin.Count >= 2);
        house.NamedHeirId = ""; var line = DynastySystem.Line(w, me); Assert.Equal(line.FirstOrDefault()?.Id, DynastySystem.FindHeir(w, me)?.Id); Assert.Equal(kin.Count, line.Count); Assert.NotEmpty(DynastySystem.HeirBy(w, me));
        foreach (var law in new[] { "primogeniture", "ultimogeniture", "seniority", "merit" }) { house.SuccessionLaw = law; Assert.Equal(DynastySystem.Line(w, me).First().Id, DynastySystem.FindHeir(w, me)!.Id); }
        house.SuccessionLaw = "seniority"; var eldest = DynastySystem.FindHeir(w, me)!;
        // someone who married in is of the house but not of its blood, and stands behind everyone who is
        eldest.BornHouseId = "another_house"; Assert.NotEqual(eldest.Id, DynastySystem.FindHeir(w, me)!.Id); Assert.Equal(eldest.Id, DynastySystem.Line(w, me).Last().Id); eldest.BornHouseId = "";
        // so does a head who has already laid the house down
        eldest.Flags.Add(LifeSystem.RetiredChar); Assert.NotEqual(eldest.Id, DynastySystem.FindHeir(w, me)!.Id); eldest.Flags.Remove(LifeSystem.RetiredChar);
        // but if nobody of the blood is left, they are better than nobody
        foreach (var k in kin) k.BornHouseId = "another_house"; Assert.NotNull(DynastySystem.FindHeir(w, me)); foreach (var k in kin) k.BornHouseId = "";
        // someone the head named stands first, whatever the law
        var last = DynastySystem.Line(w, me).Last(); house.NamedHeirId = last.Id; Assert.Equal(last.Id, DynastySystem.FindHeir(w, me)!.Id); Assert.Equal("named by you", DynastySystem.HeirBy(w, me));
    }

    [Fact]
    public void TheForecast_CountsWhatThePlayerSpends()
    {
        // 6: instalments, tutors, allowances and the household's keep were in neither the top bar nor the ledger
        var (w, _, house, me, _) = Start(); var cat = Content.Holdings; house.Treasury.Credits = 10;
        var before = HouseAccounts.Total(HouseAccounts.Outgoings(w, cat, house)).Credits;
        var child = w.LivingCharacters().FirstOrDefault(c => c.HouseId == house.Id && c.Id != me.Id);
        if (child != null) { child.Tutor = 1; Assert.Contains(HouseAccounts.Outgoings(w, cat, house), l => l.what == "tutors"); Assert.True(HouseAccounts.Total(HouseAccounts.Outgoings(w, cat, house)).Credits > before); }
        var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        w.Projects.Add(new Project { Id = "t_p", OwnerId = house.Id, Kind = Project.Building, DefId = "x", TargetId = seat.Id, Name = "a test works", TurnsTotal = 4, TurnsLeft = 4, Cost = new Resources { Credits = 40, Materials = 20 } });
        var lines = HouseAccounts.Outgoings(w, cat, house); var build = lines.FirstOrDefault(l => l.what.Contains("a test works"));
        Assert.NotNull(build.cost); Assert.True(build.cost.Credits > 0, "a building's next instalment is an outgoing");
        // the year's end is what is in hand, what the ledger brings, less all of it
        var net = new Resources { Credits = 3 }; int owed = HouseAccounts.Total(lines).Credits;
        Assert.Equal(10 + 3 - owed, HouseAccounts.YearEnd(w, cat, house, net, Resource.Credits));
        // another house's forecast has its buildings but not this household
        var rival = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id)); Assert.DoesNotContain(HouseAccounts.Outgoings(w, cat, rival), l => l.what == "tutors" || l.what.Contains("household"));
    }

    [Fact]
    public void AReportsChoice_SaysWhatItCostsAndWhatItLacks()
    {
        // 8: no cost or effect before choosing, coin taken that was not listed, "requirements not met" with no requirement
        var (w, _, house, me, _) = Start(); house.Treasury.Credits = 4; house.Treasury.Materials = 0;
        var ectx = new Praetoria.Core.Events.EvalContext(w, new Praetoria.Core.Events.Binding(me.Id), TestContent.Rng(), Content);
        var pay = new Praetoria.Core.Events.Choice { Id = "pay", Effects = new Praetoria.Core.Events.IEffect[] { new Praetoria.Core.Events.AdjustResourceEffect("self", Resource.Credits, -9), new Praetoria.Core.Events.AdjustLegitimacyEffect("self", 2) } };
        string stakes = Praetoria.Core.Presentation.ChoiceStakes.Of(pay, ectx, me);
        Assert.Contains("−9 credits", stakes); Assert.Contains("legitimacy +2", stakes);
        Assert.Contains("below nought", Praetoria.Core.Presentation.ChoiceStakes.Overdraws(pay, ectx, me));
        house.Treasury.Credits = 50; Assert.Null(Praetoria.Core.Presentation.ChoiceStakes.Overdraws(pay, ectx, me));
        var hard = new Praetoria.Core.Events.Choice { Id = "hard", Requirements = new Praetoria.Core.Events.ICondition[] {
            new Praetoria.Core.Events.SkillCondition("self", "tactics", Praetoria.Core.Events.CompareOp.Gte, 99), new Praetoria.Core.Events.ResourceCondition("self", Resource.Materials, Praetoria.Core.Events.CompareOp.Gte, 30) } };
        string lacks = Praetoria.Core.Presentation.ChoiceStakes.Unmet(hard, ectx);
        Assert.Contains("tactics at least 99", lacks); Assert.Contains("30 materials", lacks); Assert.DoesNotContain("requirements not met", lacks);
        // and across a real game no greyed choice is left without its reason
        var (w2, tc, _, _, _) = Start("the_old_lion", 11); tc.BeginTurn();
        for (int y = 0; y < 6; y++)
        {
            var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content);
            foreach (var card in model.Briefing) foreach (var ch in card.Choices.Where(c => !c.Available)) { Assert.False(string.IsNullOrWhiteSpace(ch.Reason)); Assert.NotEqual("requirements not met", ch.Reason); }
            tc.EndTurn(); if (w2.IsDynastyDead) break; tc.BeginTurn();
        }
    }

    [Fact]
    public void PeaceWithAHouse_FollowsTheWar()
    {
        // 9: at five to nought it paid a credit or nothing, and a world could not be asked for
        var (w, tc, house, me, ctx) = Start(); var rules = Content.Fleets; var fleets = tc.Fleets;
        var foe = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).OrderByDescending(h => w.HoldingsOf(h.Id).Count(x => x.IsPlanet)).First();
        foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet && h.OwnerId != house.Id && h.OwnerId != foe.Id).Take(2)) h.OwnerId = foe.Id; w.HoldingsChanged();
        Assert.True(HousePeace.Cedable(w, foe.Id).Count > 0);
        Hulls(w, house.Id, 10); Hulls(w, foe.Id, 4);
        var war = new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn }; w.Wars.Add(war); war.AddScore(house.Id, 4);
        foe.Treasury.Credits = 1; foe.Treasury.Materials = 30;
        var terms = HousePeace.Terms(w, fleets, rules, house.Id, foe.Id);
        Assert.Contains(terms, t => t.Kind == HousePeace.White && t.Accepted);
        var tribute = terms.First(t => t.Kind == HousePeace.Tribute); Assert.True(tribute.Accepted); Assert.Contains("materials", tribute.What);   // an empty treasury pays in goods
        Assert.All(terms.Where(t => t.Kind == HousePeace.Cede), t => { Assert.False(t.Accepted); Assert.Contains("from 5 ahead", t.Why); });
        war.AddScore(house.Id, 2);   // six to nought
        terms = HousePeace.Terms(w, fleets, rules, house.Id, foe.Id); var world = terms.First(t => t.Kind == HousePeace.Cede && t.HoldingId.Length > 0);
        Assert.True(world.Accepted, world.Why);
        // a house whose fleet is the stronger will not give a world at six
        Hulls(w, foe.Id, 12); Assert.False(HousePeace.Terms(w, fleets, rules, house.Id, foe.Id).First(t => t.Kind == HousePeace.Cede).Accepted); Hulls(w, foe.Id, 4);
        Fill(ctx, me);
        var cmd = new SueForPeaceCommand(me.Id, foe.Id, rules, HousePeace.Cede, world.HoldingId); Assert.True(cmd.CanExecute(ctx)); cmd.Execute(ctx);
        Assert.False(w.AtWar(house.Id, foe.Id)); Assert.Equal(house.Id, w.Holding(world.HoldingId)!.OwnerId);
        // tribute: what is owed is the lead times the rate, and goods stand in for coin that is not there
        var war2 = new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn }; w.Wars.Add(war2); war2.AddScore(house.Id, 4); int mats = house.Treasury.Materials, cr = house.Treasury.Credits;
        Fill(ctx, me); new SueForPeaceCommand(me.Id, foe.Id, rules, HousePeace.Tribute).Execute(ctx);
        Assert.Equal(4 * rules.TributePerScore, (house.Treasury.Credits - cr) + (house.Treasury.Materials - mats));
        // behind, a white peace is refused and said why; giving a world always ends it
        var war3 = new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn }; w.Wars.Add(war3); war3.AddScore(foe.Id, 6); Hulls(w, foe.Id, 40);
        terms = HousePeace.Terms(w, fleets, rules, house.Id, foe.Id);
        Assert.False(terms.First(t => t.Kind == HousePeace.White).Accepted); Assert.All(terms.Where(t => t.Kind == HousePeace.Cede), t => Assert.True(t.Accepted));
        // the screen is given every term
        var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content); Assert.NotEmpty(model.Wars.First(x => x.EnemyHouseId == foe.Id).Terms);
    }

    [Fact]
    public void ABattle_IsReportedWithItsNumbers_AndTheOddsAreKnownBefore()
    {
        // 10: no place, no enemy, no numbers in the report; no way to see the odds before sailing
        var (w, tc, house, me, ctx) = Start(); var fleets = tc.Fleets;
        var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet));
        var theirs = w.HoldingsOf(foe.Id).First(h => h.IsPlanet).SystemId; var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id || f.OwnerId == foe.Id).ToList()) w.Fleets.Remove(f.Id);
        var mine = new Fleet { Id = "t_mine", OwnerId = house.Id, Name = "Mine", SystemId = home, Strength = 8, Readiness = 100 }; w.Fleets[mine.Id] = mine;
        w.Fleets["t_foe"] = new Fleet { Id = "t_foe", OwnerId = foe.Id, Name = "Theirs", SystemId = theirs, Strength = 4, Readiness = 100 };
        Assert.Null(fleets.OddsAt(w, house.Id, new[] { mine }, theirs));   // no war: nobody to fight
        w.Wars.Add(new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn }); w.Intel["t_foe"] = new FleetIntel { FleetId = "t_foe", SystemId = theirs, Strength = 4, Confidence = 80, TurnObserved = w.Turn };
        var odds = fleets.OddsAt(w, house.Id, new[] { mine }, theirs);
        Assert.NotNull(odds); Assert.Equal(8, odds!.MyHulls); Assert.Equal(4, odds.TheirHulls); Assert.True(odds.TheyHoldTheGround); Assert.InRange(odds.Chance, 1, 99);
        Assert.Contains(foe.Name, FleetSystem.Say(odds)); Assert.Contains("in 100", FleetSystem.Say(odds));
        // the battle itself is kept, with the place, the sides, the hulls and what told
        mine.SystemId = theirs; fleets.ResolveBattles(w, TestContent.Rng());
        var b = Assert.Single(w.Battles); Assert.Equal(theirs, b.SystemId); Assert.NotEmpty(b.Place); Assert.Equal(house.Id, b.AttackerId); Assert.Equal(foe.Id, b.DefenderId);
        Assert.Equal(8, b.AttackerHulls); Assert.Equal(4, b.DefenderHulls); Assert.True(b.AttackerLost + b.DefenderLost > 0); Assert.Equal(odds.Chance, b.AttackerChance());
        Assert.Contains(b.Notes, n => n.Contains("own ground")); Assert.Contains(b.Notes, n => n.Contains("readiness"));
        // and it survives a save
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Single(again.Battles); Assert.Equal(b.Place, again.Battles[0].Place);
    }

    [Fact]
    public void AFleetsOrder_CanBeTakenBack()
    {
        // 11: one squadron left on a six-year voyage that could not be recalled
        var (w, _, house, me, ctx) = Start(); var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId;
        var far = w.Galaxy!.Systems.Keys.Where(s => w.Galaxy.Hops(home, s) >= 3).OrderBy(s => w.Galaxy.Hops(home, s)).First();
        var f = new Fleet { Id = "t_mine", OwnerId = house.Id, Name = "Mine", SystemId = home, Strength = 3, Readiness = 100, Loyalty = 100 }; w.Fleets[f.Id] = f;
        Assert.Equal("it is not under way", new RecallFleetCommand(me.Id, f.Id).Refusal(ctx));
        Fill(ctx, me); var move = new MoveFleetCommand(me.Id, f.Id, far, Content.Fleets); Assert.True(move.CanExecute(ctx), move.Refusal(ctx)); move.Execute(ctx);
        int voyage = f.Eta; Assert.Equal(voyage, f.Voyage);
        // struck out the same year: it never left
        new RecallFleetCommand(me.Id, f.Id).Execute(ctx); Assert.False(f.Moving); Assert.Equal(home, f.SystemId);
        // two years out, it takes two to come home
        move.Execute(ctx); f.Eta -= 2; Assert.Equal(2, RecallFleetCommand.YearsBack(f)); new RecallFleetCommand(me.Id, f.Id).Execute(ctx);
        Assert.True(f.Moving); Assert.Equal(home, f.DestinationId); Assert.Equal(2, f.Eta); Assert.NotNull(new RecallFleetCommand(me.Id, f.Id).Refusal(ctx));
    }

    [Fact]
    public void ASuitAgainstTheHouse_IsSaid_AndCanBeAnswered()
    {
        // 12: the courts fined the player every year in silence: no notice, no sum, and nothing to do about it
        bool settled = false, heard = false;
        for (int seed = 1; seed <= 8 && !(settled && heard); seed++)
        {
            var (w, tc, house, me, _) = Start("the_old_lion", seed); tc.BeginTurn();
            for (int y = 0; y < 14 && !w.IsDynastyDead && !(settled && heard); y++)
            {
                me = w.Protagonist!; house = w.House(me.HouseId)!; var ctx = new CommandContext(w, TestContent.Rng());
                foreach (var suit in LadderSystem.SuitsNaming(w, house.Id))
                {
                    // it is on the docket for the whole year, with who, what and how much
                    var other = w.House(LadderSystem.OtherIn(suit, house.Id)); Assert.NotNull(other); Assert.True(LadderSystem.StakeOf(suit) > 0); Assert.NotEmpty(LadderSystem.MatterOf(suit)); Assert.NotEmpty(LadderSystem.LawFor(w, suit, house.Id));
                    Assert.Contains(w.History, h => h.Text.Contains("brings a suit against " + house.Name));
                    if (!settled && suit.RightId != house.Id && house.Treasury.Credits >= LadderSystem.StakeOf(suit) / 2 && Economy.HeadOf(w, house)?.Id == me.Id)
                    {
                        int had = house.Treasury.Credits, theirs = other!.Treasury.Credits, half = LadderSystem.StakeOf(suit) / 2;
                        var cmd = new SuitCommand(me.Id, SuitCommand.Settle, suit.Id); Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx);
                        Assert.Equal(had - half, house.Treasury.Credits); Assert.Equal(theirs + half, other.Treasury.Credits); Assert.Empty(LadderSystem.SuitsNaming(w, house.Id).Where(x => x.Id == suit.Id));
                        Assert.Contains(w.Judgments, j => j.LoserId == house.Id && j.Sum == half && j.How.Contains("settled")); settled = true;
                    }
                    else if (!heard && Economy.HeadOf(w, house)?.Id == me.Id)
                    {
                        // someone of the house can be sent to plead, for an influence
                        Fill(ctx, me); var plead = new SuitCommand(me.Id, SuitCommand.Plead, suit.Id, me.Id); Assert.Null(plead.Refusal(ctx)); plead.Execute(ctx); Assert.Equal(me.Id, suit.AdvocateId);
                        int rulings = w.Judgments.Count; string id = suit.Id; tc.EndTurn(); if (w.IsDynastyDead) break; tc.BeginTurn();
                        var after = w.OfficeWork.FirstOrDefault(x => x.Id == id);
                        // a year on it has been heard (or the praetor is gone), and a ruling that moved coin is written down
                        Assert.True(after == null || after.Choice.Length > 0, "a held suit is heard the year after it is brought");
                        if (after is { Choice: not LadderSystem.Lapsed } && w.Judgments.Count > rulings) { var j = w.Judgments[^1]; Assert.True(j.WinnerId == w.Protagonist!.HouseId || j.LoserId == w.Protagonist.HouseId); heard = true; }
                        goto next;
                    }
                }
                tc.EndTurn(); if (w.IsDynastyDead) break; tc.BeginTurn();
                next: ;
            }
        }
        Assert.True(settled || heard, "in eight sagas of fourteen years no suit named the player's house");
    }

    [Fact]
    public void AVoteIsNotBoughtForFiveCredits_AndABoughtLeaderDoesNotCarryTheBench()
    {
        // 13: a five-credit bribe to a bench leader turned the whole bench, and a count forecast at 94 to 62 came in at 138 to nought
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var senate = tc.Senate; Motion? m = null; Faction? bench = null;
        for (int y = 0; y < 12 && m == null; y++)
        {
            foreach (var open in w.Motions.Where(x => x.Open))
            {
                // the forecast follows the leader as the count will: the leader's own lean, not a part of it
                foreach (var f in w.Factions.Where(f => f.Members.Count > 1 && !open.Votes.ContainsKey(f.LeaderHouseId))) Assert.Equal(Math.Sign(senate.Lean(w, open, f.LeaderHouseId)), senate.LeaderStance(w, open, f));
                bench ??= w.Factions.FirstOrDefault(f => f.Members.Count > 2 && f.LeaderHouseId != house.Id && !f.Members.Contains(house.Id) && open.TargetId != f.LeaderHouseId && open.ProposerHouseId != f.LeaderHouseId
                    && SenateSystem.BribeRefused(w, w.House(f.LeaderHouseId)!, me) == null);
                if (bench != null) { m = open; break; }
            }
            if (m == null) { tc.EndTurn(); tc.BeginTurn(); me = w.Protagonist!; house = w.House(me.HouseId)!; }
        }
        Assert.NotNull(m); Assert.NotNull(bench); var leader = w.House(bench!.LeaderHouseId)!; ctx = new CommandContext(w, TestContent.Rng());
        int price = SenateSystem.BribeCost(w, leader, me); Assert.True(price >= SenateSystem.BribeCredits + SenateSystem.BenchLeaderBribe, "a bench's leader costs more than five credits");
        house.Treasury.Credits = price - 1; Assert.Contains("needs " + price, new LobbyCommand(me.Id, m!.Id, leader.Id, 1, bribe: true).Refusal(ctx));
        house.Treasury.Credits = price + 10; var taker = Economy.HeadOf(w, leader)!; int theirs = taker.Purse;
        var members = bench.Members.Where(id => id != leader.Id && id != m.TargetId && id != m.ProposerHouseId).ToList(); var before = members.ToDictionary(id => id, id => senate.Lean(w, m, id)); int leaderBefore = senate.Lean(w, m, leader.Id);
        int side = leaderBefore > 0 ? -1 : 1; var bribe = new LobbyCommand(me.Id, m.Id, leader.Id, side, bribe: true); Assert.Null(bribe.Refusal(ctx)); bribe.Execute(ctx);
        Assert.Equal(10, house.Treasury.Credits); Assert.Equal(theirs + price, taker.Purse);
        Assert.Equal(leaderBefore + side * SenateSystem.BribeWeight, senate.Lean(w, m, leader.Id));   // the purse moves the one who took it
        foreach (var id in members) Assert.Equal(before[id], senate.Lean(w, m, id));                    // and nobody who did not
        // the upright will not take it, and the screen is told so before the button is pressed
        var upright = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null); Economy.HeadOf(w, upright)!.NatureTraits.Add("Honorable");
        Assert.Contains("upright", new LobbyCommand(me.Id, m.Id, upright.Id, 1, bribe: true).Refusal(ctx));
    }

    [Fact]
    public void TheHouseIsTold_WhatHappensToIt()
    {
        // 14: births, the death of the steward, two declarations of war, tutors leaving: all in the Chronicle and nowhere else
        var (w, tc, house, me, _) = Start(); tc.BeginTurn();
        var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id));
        tc.Fleets.DeclareWar(w, foe.Id, house.Id);
        var war = Assert.Single(w.NewsNow().Where(n => n.Text.Contains("declared war on your house"))); Assert.Equal("military", war.Target); Assert.Contains("Why:", war.Text); Assert.Contains("hull(s)", war.Text);
        var kin = w.LivingCharacters().First(c => c.HouseId == house.Id && c.Id != me.Id); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); seat.GovernorId = kin.Id;
        DynastySystem.Die(w, kin.Id);
        var death = Assert.Single(w.NewsNow().Where(n => n.Text.Contains(kin.Name + " of your house has died"))); Assert.Contains("the governing of " + seat.Name, death.Text); Assert.Equal(kin.Id, death.SubjectId);
        // nothing is told of another house's dead
        int told = w.News.Count; DynastySystem.Die(w, w.LivingCharacters().First(c => c.HouseId == foe.Id).Id); Assert.Equal(told, w.News.Count);
        // this year's news is this year's: after the year turns, last year's is no longer new
        tc.EndTurn(); tc.BeginTurn(); Assert.DoesNotContain(w.NewsNow(), n => n.Seq == war.Seq || n.Seq == death.Seq);
        // and it survives a save
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(w.News.Count, again.News.Count); Assert.Equal(w.NewsFrom, again.NewsFrom);
    }

    [Fact]
    public void AGranary_DoesWhatItSays()
    {
        // the Easy list: the Public Granary said only what it cost, and did nothing
        int Unrest(bool granary)
        {
            var (w, tc, house, _, _) = Start("the_old_lion", 5); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); seat.Unrest = 60; house.Treasury.Credits = 500;
            if (granary) seat.Buildings.Add(Economy.GranaryId);
            tc.BeginTurn(); tc.EndTurn(); return w.Holding(seat.Id)!.Unrest;
        }
        Assert.Equal(Unrest(false) - Economy.GranaryCalm, Unrest(true));
    }

    [Fact]
    public void AStalledSite_CanBePaidAtOnce()
    {
        var (w, _, house, me, ctx) = Start(); var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        var p = new Project { Id = "t_p", OwnerId = house.Id, Kind = Project.Building, DefId = "x", TargetId = seat.Id, Name = "a test works", TurnsTotal = 4, TurnsLeft = 3, Cost = new Resources { Credits = 40 }, Stalled = true }; w.Projects.Add(p);
        house.Treasury.Credits = 4; Assert.Equal("6 credits short", new PayInstalmentCommand(me.Id, p.Id).Refusal(ctx));
        house.Treasury.Credits = 25; var cmd = new PayInstalmentCommand(me.Id, p.Id); Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx);
        Assert.False(p.Stalled); Assert.Equal(15, house.Treasury.Credits); Assert.Equal(2, p.TurnsLeft); Assert.Equal(10, p.Paid.Credits);
        Assert.Equal("it has not stalled", new PayInstalmentCommand(me.Id, p.Id).Refusal(ctx));
    }

    [Fact]
    public void OneBarredFromOffice_IsNotGivenOne()
    {
        // the Easy list: "a convicted man is elected consul; a banned man is commissioned"
        var (w, _, house, me, _) = Start(); var def = Content.Offices.Defs.First(d => OfficeSystem.Candidate(w, d, house.Id) != null);
        var who = OfficeSystem.Candidate(w, def, house.Id)!; w.WorldCounters[LadderSystem.BarredKey(who.Id)] = w.Turn + 5;
        Assert.NotEqual(who.Id, OfficeSystem.Candidate(w, def, house.Id)?.Id);
        w.Turn += 5; Assert.Equal(who.Id, OfficeSystem.Candidate(w, def, house.Id)?.Id);
    }

    [Fact]
    public void EveryoneOfTheBlood_HasAWordForWhoTheyAre()
    {
        // the family: rows and profiles did not say whose child each was, and a niece was taken for a daughter
        var w = new World(); Character P(string id, string sex, string mother = "", string father = "") { var c = new Character { Id = id, Name = id, Sex = sex, HouseId = "h", MotherId = mother, FatherId = father, Alive = true }; w.Characters[id] = c; return c; }
        var gm = P("gm", "female"); var me = P("me", "male", "gm"); var sis = P("sis", "female", "gm"); var son = P("son", "male", "wife", "me"); var wife = P("wife", "female");
        var niece = P("niece", "female", "sis"); var grandson = P("grandson", "male", "x", "son"); var stranger = P("stranger", "male");
        w.Marriages.Add(new Marriage { A = "me", B = "wife" });
        Assert.Equal("your mother", Praetoria.Core.Presentation.Kinship.Of(w, me, gm)); Assert.Equal("your sister", Praetoria.Core.Presentation.Kinship.Of(w, me, sis));
        Assert.Equal("your son", Praetoria.Core.Presentation.Kinship.Of(w, me, son)); Assert.Equal("your wife", Praetoria.Core.Presentation.Kinship.Of(w, me, wife));
        Assert.Equal("your niece", Praetoria.Core.Presentation.Kinship.Of(w, me, niece)); Assert.Equal("your grandson", Praetoria.Core.Presentation.Kinship.Of(w, me, grandson));
        Assert.Equal("your uncle", Praetoria.Core.Presentation.Kinship.Of(w, niece, me)); Assert.Equal("your grandmother", Praetoria.Core.Presentation.Kinship.Of(w, niece, gm));
        Assert.Equal("", Praetoria.Core.Presentation.Kinship.Of(w, me, stranger)); Assert.Equal("son of me and wife", Praetoria.Core.Presentation.Kinship.ChildOfSays(w, son));
    }

    [Fact]
    public void NobodyIsTheRivalInTheirOwnSuit()
    {
        var (w, _, house, me, _) = Start(); var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id));
        var mine = w.LivingCharacters().First(c => c.HouseId == house.Id && c.Id != me.Id); var theirs = w.LivingCharacters().First(c => c.HouseId == foe.Id);
        var suit = new Courtship { Id = "t_c", SuitorId = mine.Id, TargetId = theirs.Id, Since = w.Turn, Consent = 40 }; w.Courtships.Add(suit);
        Assert.True(CourtshipSystem.Intercept(w, theirs, mine));   // their steward proposes the same match
        Assert.Equal("", suit.RivalId); Assert.Equal(50, suit.Consent);
    }

    [Fact]
    public void AFleet_CanBeGivenAName()
    {
        var (w, _, house, me, ctx) = Start(); var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet).SystemId;
        var a = new Fleet { Id = "t_a", OwnerId = house.Id, Name = "House Squadron", SystemId = home, Strength = 2 }; var b = new Fleet { Id = "t_b", OwnerId = house.Id, Name = "House Squadron", SystemId = home, Strength = 2 }; w.Fleets[a.Id] = a; w.Fleets[b.Id] = b;
        Assert.Equal("another of your fleets has that name", new RenameFleetCommand(me.Id, b.Id, "house squadron").Refusal(ctx));
        Assert.NotNull(new RenameFleetCommand(me.Id, b.Id, "x").Refusal(ctx));
        var cmd = new RenameFleetCommand(me.Id, b.Id, "  The Second  "); Assert.Null(cmd.Refusal(ctx)); cmd.Execute(ctx); Assert.Equal("The Second", b.Name);
        b.LevyOf = "someone"; Assert.Contains("levy", new RenameFleetCommand(me.Id, b.Id, "Mine Now").Refusal(ctx));
    }

    [Fact]
    public void TheElectionYear_SaysItIsTheElectionYear()
    {
        // the Easy list: "in 10 years" in the election year, and the alert a year early
        var (w, _, _, _, _) = Start(); int every = Content.Offices.LadderEvery;
        w.Turn = every; Assert.True(LadderSystem.Due(w, Content.Offices)); Assert.Equal(0, LadderSystem.CountIn(w, Content.Offices));
        w.Turn = every - 1; Assert.Equal(1, LadderSystem.CountIn(w, Content.Offices)); w.Turn = every + 1; Assert.Equal(every - 1, LadderSystem.CountIn(w, Content.Offices));
        w.Turn = ElectionSystem.Every; Assert.Equal(0, ElectionSystem.CountIn(w)); w.Turn = ElectionSystem.Every - 2; Assert.Equal(2, ElectionSystem.CountIn(w));
    }
}
