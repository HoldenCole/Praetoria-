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
}
