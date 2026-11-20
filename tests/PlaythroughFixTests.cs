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
        for (int seed = 1; seed <= 16 && !(settled && heard); seed++)
        {
            var (w, tc, house, me, _) = Start("the_old_lion", seed); tc.BeginTurn();
            for (int y = 0; y < 16 && !w.IsDynastyDead && !(settled && heard); y++)
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
        Assert.True(settled || heard, "in sixteen sagas of sixteen years no suit named the player's house");
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

    [Fact]
    public void AMotionForm_SaysWhatTheMotionDoes()
    {
        var defs = Content.Motions.Defs; Assert.NotEmpty(defs);
        Assert.Contains("legitimacy", Praetoria.Core.Presentation.CourtProjection.MotionDoes(defs.First(d => d.LegitimacyTarget < 0)));
        Assert.Contains("exchanges are closed", Praetoria.Core.Presentation.CourtProjection.MotionDoes(defs.First(d => d.EmbargoTurns > 0)));
        Assert.All(defs, d => { var says = Praetoria.Core.Presentation.CourtProjection.MotionDoes(d); Assert.True(says.Length == 0 || says.StartsWith("If it carries: ")); });
    }

    [Fact]
    public void AnAnsweredReport_SaysHowItWasAnswered()
    {
        var (w, tc, _, _, _) = Start(); tc.BeginTurn();
        for (int y = 0; y < 8; y++)
        {
            var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content);
            var card = model.Briefing.FirstOrDefault(b => !b.Resolved && b.Choices.Any(c => c.Available));
            if (card != null)
            {
                var pick = card.Choices.First(c => c.Available); Assert.Equal("", card.Answer); Assert.True(tc.Resolve(card.Item, pick.Id));
                var after = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content).Briefing.First(b => ReferenceEquals(b.Item, card.Item));
                Assert.True(after.Resolved); Assert.Equal(pick.Label, after.Answer); return;
            }
            tc.EndTurn(); if (w.IsDynastyDead) break; tc.BeginTurn();
        }
        Assert.Fail("no report with an open choice in eight years");
    }

    [Fact]
    public void ACampaign_EndsWithItsWar()
    {
        var (w, tc, house, _, _) = Start(); tc.BeginTurn(); var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id));
        var target = w.HoldingsOf(foe.Id).First(h => h.IsPlanet).SystemId;
        foreach (var x in w.Wars.Where(x => x.Involves(house.Id)).ToList()) w.Wars.Remove(x); foreach (var n in w.Nations) n.AtWar = false;
        w.Wars.Add(new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn });
        w.WorldCounters[FleetSystem.CampaignUntilKey(house.Id)] = w.Turn + 9; w.WorldFlags.Add(FleetSystem.CampaignPrefix(house.Id) + target);
        tc.Fleets.Tick(w); Assert.Equal(target, FleetSystem.CampaignTarget(w, house.Id));   // at war: the campaign stands
        w.Wars.RemoveAll(x => x.Between(house.Id, foe.Id)); Assert.Empty(w.EnemiesOf(house.Id));
        tc.Fleets.Tick(w); Assert.Null(FleetSystem.CampaignTarget(w, house.Id)); Assert.Contains(w.History, h => h.Text.Contains("The war is over, and with it the campaign"));
    }

    [Fact]
    public void ACensure_NamesItsGround()
    {
        var (w, tc, house, me, _) = Start(); tc.BeginTurn(); var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null);
        var m = new Motion { Id = "t_m", Kind = "censure", ProposerHouseId = foe.Id, TargetId = house.Id, ProposedTurn = w.Turn }; w.Motions.Add(m);
        w.Relationship(Economy.HeadOf(w, foe)!.Id, me.Id).Disposition = -45;
        Assert.Contains("thinks ill of", tc.Senate.Ground(w, m)); Assert.Contains("-45", tc.Senate.Ground(w, m));
        w.Wars.Add(new War { HouseA = foe.Id, HouseB = house.Id, Since = w.Turn }); Assert.Contains("at war", tc.Senate.Ground(w, m));
        Assert.Equal("", tc.Senate.Ground(w, new Motion { Id = "t_l", Kind = "levy", ProposerHouseId = foe.Id }));
    }

    [Fact]
    public void AReport_NamesItsSubject()
    {
        // reports: "The Court Finds Against You" named neither who sued, nor over what, nor for how much
        var (w, _, house, _, _) = Start(); var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id));
        var lost = Content.Events.First(e => e.Id == "the_court_finds_against_you"); var line = Content.Events.First(e => e.Id == "the_line_breaks");
        Assert.Equal("", Praetoria.Core.Presentation.ReportFacts.For(w, lost, Content));
        w.Judgments.Add(new JudgmentRecord { Turn = 3, Judge = "Eamon Thalen", WinnerId = foe.Id, LoserId = house.Id, Sum = 15, Matter = "a debt of long standing", How = "ruled where the law was doubtful" });
        string facts = Praetoria.Core.Presentation.ReportFacts.For(w, lost, Content);
        Assert.Contains(foe.Name, facts); Assert.Contains("a debt of long standing", facts); Assert.Contains("15 credits", facts); Assert.Contains("Eamon Thalen", facts);
        w.Battles.Add(new BattleRecord { Turn = 4, Place = "Cinder Gate", AttackerId = foe.Id, DefenderId = house.Id, WinnerId = foe.Id, AttackerHulls = 9, DefenderHulls = 4, AttackerLost = 1, DefenderLost = 3 });
        string fight = Praetoria.Core.Presentation.ReportFacts.For(w, line, Content);
        Assert.Contains("Cinder Gate", fight); Assert.Contains("your 4 hull(s) against 9", fight); Assert.Contains("You lost 3", fight);
    }
    [Fact]
    public void An_article_fits_the_good_and_every_skill_says_what_it_is_for()
    {
        Assert.Equal("an isotopes", Resource.A(Resource.Isotopes));
        Assert.StartsWith("a ", Resource.A(Resource.RareEarths));
        foreach (var s in EducationSystem.All) Assert.False(string.IsNullOrEmpty(Praetoria.Core.Presentation.SkillWords.For(s)), s);
        Assert.Contains("legitimacy", Praetoria.Core.Presentation.Marks.Tip("parvenu"));
        Assert.Equal("debt to crown", Praetoria.Core.Presentation.Marks.Label("debt_to_crown"));
    }

    [Fact]
    public void A_committees_chair_weighs_in_its_estate_every_year_it_is_held()
    {
        var (w, tc, house, me, ctx) = Start();
        house.Committee = LadderSystem.FleetCommittee; Assert.Equal("navy", LadderSystem.CommitteeSphere(LadderSystem.FleetCommittee));
        w.WorldCounters[LadderSystem.StandingKey(house.Id)] = 0;
        tc.BeginTurn(); int before = house.SphereInfluence.GetValueOrDefault("navy");
        w.WorldCounters[LadderSystem.StandingKey(house.Id)] = LadderSystem.StandingMax;
        Assert.Equal(house.Id, LadderSystem.ChairOf(w, LadderSystem.FleetCommittee)?.Id);
        tc.EndTurn(); tc.BeginTurn();
        Assert.True(house.SphereInfluence.GetValueOrDefault("navy") >= before + SphereSystem.ChairWeight, $"{before} -> {house.SphereInfluence.GetValueOrDefault("navy")}");
        Assert.False(house.SphereInfluence.ContainsKey("military"));
    }
    [Fact]
    public void A_system_is_the_space_of_whoever_holds_its_world()
    {
        var (w, tc, house, me, ctx) = Start();
        var world = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var other = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id));
        for (int i = 0; i < 3; i++) w.Holdings["zz_claim_" + i] = new Holding { Id = "zz_claim_" + i, OwnerId = other.Id, Name = "claim", SystemId = world.SystemId, Kind = Holding.Claim, Specialization = "belt_claim" };
        Assert.Equal(house.Id, FleetSystem.TerritoryOwner(w, world.SystemId));
    }
    [Fact]
    public void One_left_unanswered_at_the_door_goes_and_does_not_come_back_for_years()
    {
        var (w, tc, house, me, ctx) = Start();
        var asker = w.LivingCharacters().First(c => c.HouseId != house.Id && !World.Outsider(c.HouseId) && c.Age >= 18);
        house.Petitions.Add(new Petition { PersonId = asker.Id, As = HouseholdSystem.Companion, Since = w.Turn - HouseholdSystem.PetitionYears });
        w.NewsFrom = w.NewsSeq; tc.EndTurn();
        Assert.DoesNotContain(house.Petitions, p => p.PersonId == asker.Id);
        Assert.True(w.WorldCounters.ContainsKey(HouseholdSystem.AskedKey(asker.Id)));
        Assert.Contains(w.News, n => n.Text.Contains(asker.Name) && n.Text.Contains("gone to look elsewhere"));
        for (int i = 0; i < HouseholdSystem.AskAgainYears - 2; i++) { tc.BeginTurn(); tc.EndTurn(); Assert.DoesNotContain(house.Petitions, p => p.PersonId == asker.Id); if (w.IsDynastyDead) break; }
    }

    [Fact]
    public void The_census_refusal_gives_the_number_and_a_house_without_a_voice_is_told_so()
    {
        var (w, tc, house, me, ctx) = Start("the_new_man");
        house.Orders[OrderSystem.Commerce] = 3; house.Enrolled = false;
        string? no = tc.Orders.EnrollRefusal(w, house, false);
        if (no != null && no.Contains("notable in commerce")) { Assert.Contains(OrderSystem.NotableAt.ToString(), no); Assert.Contains("the house has 3", no); }
        else Assert.Fail("expected the census to refuse a house of no account: " + no);
    }
    [Fact]
    public void A_dowry_agreed_and_a_dowry_paid_are_both_kept_and_a_second_yes_marries_nobody()
    {
        var (w, tc, house, me, ctx) = Start();
        var bride = w.LivingCharacters().First(c => c.Sex == "female" && c.Age is >= 18 and <= 35 && c.HouseId != house.Id && !World.Outsider(c.HouseId) && DynastySystem.Spouse(w, c) == null);
        var groom = w.LivingCharacters().First(c => c.Sex == "male" && c.Age >= 18 && c.HouseId == house.Id && DynastySystem.Spouse(w, c) == null);
        var from = w.House(bride.HouseId)!; int due = CourtshipSystem.DowryAmount(w, from.Id, CourtshipSystem.Customary); Assert.True(due > 3);
        from.Treasury.Credits = 3; bride.Purse = 0;
        var m = CourtshipSystem.Wed(w, bride, groom, CourtshipSystem.Arranged, CourtshipSystem.Customary);
        Assert.Equal(due, m.DowryAgreed); Assert.Equal(3, m.Dowry);
        Assert.Contains(w.News, n => n.Text.Contains($"agreed a dowry of {due}") && n.Text.Contains("only 3"));
        // a second yes, from a report answered the same year, marries nobody
        var other = w.LivingCharacters().First(c => c.Sex == "female" && c.Age >= 18 && c.HouseId != house.Id && c.Id != bride.Id && DynastySystem.Spouse(w, c) == null);
        new Praetoria.Core.Events.AddBondEffect("self", "match", BondType.Marriage, 60).Apply(new Praetoria.Core.Events.EvalContext(w, new Praetoria.Core.Events.Binding(groom.Id).With("match", other.Id), TestContent.Rng()));
        Assert.NotEqual(BondType.Marriage, w.GetRelationship(groom.Id, other.Id)?.Bond ?? BondType.None);
        Assert.Equal(bride.Id, DynastySystem.Spouse(w, groom)?.Id);
    }

    [Fact]
    public void Nobody_of_the_house_is_cast_as_the_outside_buyer()
    {
        var (w, tc, house, me, ctx) = Start();
        var def = Content.Events.First(e => e.Id == "the_fuel_contract");
        for (int i = 0; i < 40; i++)
        {
            var b = Praetoria.Core.Events.Binder.TryBind(def, w, new Praetoria.Core.Rng.SplitMix64Rng((ulong)(i + 1)));
            if (b != null && b.TryGet("buyer", out var who)) Assert.NotEqual(house.Id, w.Char(who)!.HouseId);
        }
    }
    [Fact]
    public void A_report_sells_at_the_markets_price_and_only_when_what_it_says_of_the_price_is_true()
    {
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var g = w.Market!.Good(Resource.Helium3)!; house.Treasury.Helium3 = 10;
        var glut = Content.Events.First(e => e.Id == "the_helium_glut");
        var price = glut.Conditions.OfType<Praetoria.Core.Events.PriceCondition>().Single();
        var ev = new Praetoria.Core.Events.EvalContext(w, new Praetoria.Core.Events.Binding(me.Id), TestContent.Rng(), Content);
        g.History.Clear(); g.History.Add(g.Price * 2); g.History.Add(g.Price); Assert.True(price.Evaluate(ev));      // half what it was a year ago
        g.History.Clear(); g.History.Add(g.Price / 2); g.History.Add(g.Price); Assert.False(price.Evaluate(ev));     // twice what it was: no glut
        var sell = glut.Choices.First(c => c.Id == "sell_cheap").Effects.OfType<Praetoria.Core.Events.SellAtMarketEffect>().Single();
        int quoted = MarketSystem.QuoteSell(w, Content.Trade, house.Id, Resource.Helium3, 4).Credits, had = house.Treasury.Credits;
        Assert.Equal(quoted, sell.Proceeds(ev)); sell.Apply(ev);
        Assert.Equal(had + quoted, house.Treasury.Credits); Assert.Equal(6, house.Treasury.Helium3);
    }

    [Fact]
    public void A_director_may_pay_the_houses_coin_into_the_company_and_a_private_sale_is_remembered()
    {
        var (w, tc, house, me, ctx) = Start();
        var other = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null);
        var co = new Company { Id = "co_test", Name = "The Test Company", ControllerId = house.Id, Shares = 100, Cash = 0 }; w.Companies.Add(co);
        house.Treasury.Credits = 30;
        var payIn = new BoardCommand(me.Id, co.Id, BoardCommand.PayIn, 12, tc.Companies!); Assert.Null(payIn.Refusal(ctx)); payIn.Execute(ctx);
        Assert.Equal(12, co.Cash); Assert.Equal(18, house.Treasury.Credits);
        Assert.Contains("treasury holds 18", new BoardCommand(me.Id, co.Id, BoardCommand.PayIn, 50, tc.Companies!).Refusal(ctx));
        co.ControllerId = other.Id; Assert.Equal("you do not direct it", new BoardCommand(me.Id, co.Id, BoardCommand.PayIn, 5, tc.Companies!).Refusal(ctx));
    }
    [Fact]
    public void A_will_may_leave_smaller_legacies_to_several_and_never_more_than_the_purse()
    {
        var (w, tc, house, me, ctx) = Start();
        var heir = DynastySystem.FindHeir(w, me)!; var kin = w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != me.Id && c.Id != heir.Id).OrderBy(c => c.Id, StringComparer.Ordinal).Take(3).ToList();
        Assert.True(kin.Count >= 2, "the old lion has kin enough");
        Assert.True(tc.Executor.TryExecute(new WealthCommand(me.Id, WealthCommand.Legacy, kin[0].Id, "25"), ctx));
        Assert.True(tc.Executor.TryExecute(new WealthCommand(me.Id, WealthCommand.Legacy, kin[1].Id, "10"), ctx));
        Assert.Equal("a tenth or a quarter of it", new WealthCommand(me.Id, WealthCommand.Legacy, kin[1].Id, "60").Refusal(ctx));
        // with all of it willed to one, nothing is left for a smaller legacy
        Assert.True(tc.Executor.TryExecute(new WealthCommand(me.Id, WealthCommand.Will, kin[0].Id, "50"), ctx)); Assert.False(me.Will!.Also.ContainsKey(kin[0].Id));
        me.Purse = 200; int a = kin[0].Purse, b = kin[1].Purse, h = heir.Purse;
        WealthSystem.Inherit(w, me, heir);
        Assert.Equal(a + 100, kin[0].Purse); Assert.Equal(b + 20, kin[1].Purse); Assert.Equal(h + 80, heir.Purse);
    }
    [Fact]
    public void The_year_in_brief_gathers_what_the_year_did_to_the_house()
    {
        // docs/PLAYTHROUGH.md, Medium: "the year in brief at the start of each year"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id));
        house.Treasury.Credits += 40;                       // spent or earned in the year: the brief says where the treasury went
        w.Battles.Add(new BattleRecord { Turn = w.Turn, Place = "Cinder Gate", AttackerId = foe.Id, DefenderId = house.Id, WinnerId = house.Id, AttackerHulls = 5, DefenderHulls = 6, AttackerLost = 3, DefenderLost = 1 });
        w.Judgments.Add(new JudgmentRecord { Turn = w.Turn, Judge = "Flavian Marrow", WinnerId = foe.Id, LoserId = house.Id, Sum = 7, Matter = "a boundary" });
        tc.EndTurn(); if (!w.IsDynastyDead) tc.BeginTurn();
        var brief = Praetoria.Core.Presentation.YearInBrief.For(w, Content);
        Assert.Contains(brief, l => l.Kind == Praetoria.Core.Presentation.YearInBrief.War && l.Text.Contains("Cinder Gate") && l.Text.Contains("won against") && l.Target == "military:battles");
        Assert.Contains(brief, l => l.Kind == Praetoria.Core.Presentation.YearInBrief.Courts && l.Text.Contains("against you") && l.Text.Contains("7 credits left"));
        Assert.Contains(brief, l => l.Kind == Praetoria.Core.Presentation.YearInBrief.Money && l.Text.Contains("credits") && l.Target == "house:ledger");
        // the lines come grouped in the order the brief names
        var kinds = brief.Select(l => Array.IndexOf(Praetoria.Core.Presentation.YearInBrief.Order, l.Kind)).ToList(); Assert.Equal(kinds.OrderBy(k => k).ToList(), kinds);
    }
    [Fact]
    public void Prosperity_and_unrest_are_each_one_reckoning_shown_cause_by_cause()
    {
        // docs/PLAYTHROUGH.md, Medium: "why loyalty and prosperity are what they are, cause by cause"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var world = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var eco = tc.Economy!;
        var why = eco.ProsperityWhy(w, world);
        Assert.Equal(Math.Clamp(why.Sum(x => x.by), 0, 100), eco.ProsperityTarget(w, world));
        Assert.Contains(why, x => x.what == "where any world begins" && x.by == 50);
        // a building standing is a cause, and taking it away moves the target by exactly that
        int before = eco.ProsperityTarget(w, world), per = why.FirstOrDefault(x => x.what.Contains("building(s) standing")).by;
        if (world.Buildings.Count > 0 && before is > 0 and < 100) { var keep = world.Buildings.ToList(); world.Buildings.Clear(); Assert.Equal(Math.Clamp(before - per, 0, 100), eco.ProsperityTarget(w, world)); world.Buildings.AddRange(keep); }
        // unrest: the year applies the sum of the causes shown
        house.Treasury.Credits = 500; world.Unrest = 40; int drift = eco.UnrestWhy(w, world).Sum(x => x.by);
        Assert.Contains(eco.UnrestWhy(w, world), x => x.what.StartsWith("a solvent house"));
        house.Treasury.Credits = -5; Assert.Contains(eco.UnrestWhy(w, world), x => x.what == "the house is insolvent" && x.by > 0);
    }
    [Fact]
    public void A_war_declared_on_the_house_is_a_report_that_says_who_why_and_what_would_end_it()
    {
        // docs/PLAYTHROUGH.md, Medium: "a declaration of war on you is a report: who, why, what they want"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var foe = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).First();
        tc.Fleets.DeclareWar(w, foe.Id, house.Id);
        Assert.True(w.HasFlag(FleetSystem.DeclaredOnMeFlag)); Assert.False(string.IsNullOrEmpty(w.Wars[^1].Why));
        var card = Content.Events.First(e => e.Id == "war_is_declared_on_you");
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        Assert.Contains(engine.GatherEligible(w), e => e.def.Id == card.Id);
        string facts = Praetoria.Core.Presentation.ReportFacts.For(w, card, Content);
        Assert.Contains(foe.Name + " declared war on your house", facts); Assert.Contains("Why: " + w.Wars[^1].Why, facts); Assert.Contains("peace", facts);
        // it is news for the year it happens and the next, and then the flag goes
        tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn(); Assert.True(w.HasFlag(FleetSystem.DeclaredOnMeFlag));
        tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn(); tc.EndTurn();
        Assert.False(w.HasFlag(FleetSystem.DeclaredOnMeFlag));
    }
    [Fact]
    public void A_death_in_the_house_is_a_report_of_what_it_left_empty_and_what_passed()
    {
        // docs/PLAYTHROUGH.md, Medium: "a death report: who, how, what passes to whom, which posts are now empty"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var kin = w.LivingCharacters().First(c => c.HouseId == house.Id && c.Id != me.Id && c.Age >= 18);
        var world = w.HoldingsOf(house.Id).First(h => h.IsPlanet); world.GovernorId = kin.Id; kin.Purse = 14;
        DynastySystem.Die(w, kin.Id, "in battle");
        var rec = w.Deaths.Single(d => d.PersonId == kin.Id);
        Assert.Equal("in battle", rec.How);
        Assert.Contains(rec.Posts, p => p.What == "the governing of " + world.Name && p.Target == "galaxy:" + world.SystemId);
        Assert.Contains(rec.Passed, l => l.Contains("purse of 14 credits") && l.Contains(me.Name));
        tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn();
        var brief = Praetoria.Core.Presentation.YearInBrief.For(w, Content);
        Assert.Contains(brief, l => l.Text.StartsWith(kin.Name + " died at") && l.Text.Contains("in battle"));
        Assert.Contains(brief, l => l.Text.Contains("the governing of " + world.Name) && l.Target == "galaxy:" + world.SystemId);
        Assert.DoesNotContain(brief, l => l.Text.Contains("of your house has died"));   // said once, as the report
    }
}
