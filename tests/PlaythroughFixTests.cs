using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
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

    /* docs/PLAYTHROUGH.md, Medium, The Senate: "Titles: ... an alert when you qualify". (The strength test for seizing, the
       land a rank wants and one rise a year were done with the fifteen: 3.) */
    /* docs/PLAYTHROUGH.md, Medium, War and peace: "a forecast of fuel against what the house brings up" (S72, S42, S68: the
       tile divided the tanks by the burn alone, so a house that skimmed all it burned was told it had a few years left). */
    [Fact]
    public void TheFuelForecast_SetsWhatComesUpAgainstWhatIsBurned()
    {
        // nothing burning: no end, and it says so
        var idle = FleetSystem.Outlook(12, 0, 3); Assert.Equal(-1, idle.Years); Assert.Contains("nothing is burning it", idle.Says); Assert.Equal(3, idle.BroughtUp);
        // the house brings up all it burns: the tanks hold
        var level = FleetSystem.Outlook(12, 4, 0); Assert.Equal(-1, level.Years); Assert.Equal(4, level.BroughtUp); Assert.Contains("hold level", level.Says);
        var filling = FleetSystem.Outlook(12, 4, 2); Assert.Equal(-1, filling.Years); Assert.Equal(6, filling.BroughtUp); Assert.Contains("fill by 2", filling.Says);
        // it brings up some: the years are by the shortfall, not by the burn
        var some = FleetSystem.Outlook(12, 4, -1); Assert.Equal(12, some.Years); Assert.Equal(3, some.BroughtUp); Assert.Contains("1 short each year", some.Says); Assert.Contains("dry in 12 years", some.Says);
        // it brings up none: the years are the tanks over the burn, as before
        var none = FleetSystem.Outlook(12, 4, -4); Assert.Equal(3, none.Years); Assert.Equal(0, none.BroughtUp); Assert.Contains("none comes up", none.Says);
        Assert.Equal(0, FleetSystem.Outlook(3, 4, -4).Years); Assert.Contains("dry within the year", FleetSystem.Outlook(3, 4, -4).Says);
        Assert.Contains("dry in a year", FleetSystem.Outlook(4, 4, -4).Says);

        // on a real house the figures are the house's own: the burn is the fleet's bill, and the net is the year's forecast
        var (w, tc, house, me, _) = Start(); tc.BeginTurn(); Hulls(w, house.Id, 9);
        int burn = FleetSystem.FuelBillFor(w, house.Id, Content.Fleets.FuelHullsPerUnit); Assert.True(burn > 0);
        int net = tc.Economy!.Forecast(w, house.Id, tc.Fleets).Helium3;
        var real = FleetSystem.Outlook(house.Treasury.Helium3, burn, net);
        Assert.Equal(Math.Max(0, burn + net), real.BroughtUp);
        if (net < 0) Assert.Equal(house.Treasury.Helium3 / -net, real.Years); else Assert.Equal(-1, real.Years);
    }

    /* docs/PLAYTHROUGH.md, Medium, War and peace: "lay up or pay off hulls to cut the cost" (S50, S74: the only way to cut a
       fleet's cost was to break its hulls up for a quarter of their materials). */
    [Fact]
    public void AFleetLaidUp_KeepsItsHulls_CostsAPartOfItsKeep_BurnsNothing_AndCannotSailUntilRecommissioned()
    {
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); Fill(ctx, me); Hulls(w, house.Id, 9);
        var f = w.Fleets["t_" + house.Id]; f.HomeSystemId = f.SystemId; var fs = tc.Fleets;
        Assert.Contains(w.Holdings.Values, h => h.OwnerId == house.Id && h.SystemId == f.SystemId);
        var full = fs.Upkeep(f); int burn = FleetSystem.FuelBillFor(w, house.Id, Content.Fleets.FuelHullsPerUnit); int strength = FleetSystem.StrengthOf(w, house.Id);
        Assert.True(full.Credits > 0); Assert.True(burn > 0); Assert.Equal(9, strength);

        var lay = new LayUpFleetCommand(me.Id, f.Id, true, fs);
        Assert.Null(lay.Refusal(ctx));
        var (now, laid) = lay.Keep(w); Assert.Equal(full.Credits, now.Credits); Assert.False(f.LaidUp);   // asking the price changes nothing
        int treasuryActions = ctx.PoolsOf(me.Id).Treasury;
        Assert.True(tc.Executor.TryExecute(lay, ctx));
        Assert.True(f.LaidUp); Assert.Equal(9, f.Strength); Assert.Equal(0, f.Readiness); Assert.Equal(treasuryActions - 1, ctx.PoolsOf(me.Id).Treasury);
        // a part of its keep, rounded up, and never nothing; no fuel; not counted as strength
        Assert.Equal((full.Credits * Content.Fleets.LaidUpUpkeepPct + 99) / 100, fs.Upkeep(f).Credits); Assert.Equal(laid.Credits, fs.Upkeep(f).Credits);
        Assert.InRange(fs.Upkeep(f).Credits, 1, full.Credits - 1);
        Assert.Equal(0, FleetSystem.FuelBillFor(w, house.Id, Content.Fleets.FuelHullsPerUnit));
        Assert.Equal(0, FleetSystem.StrengthOf(w, house.Id));
        // it takes no order that needs crews, and each says why
        string other = w.Galaxy!.Lanes.Where(l => l.A == f.SystemId || l.B == f.SystemId).Select(l => l.A == f.SystemId ? l.B : l.A).First();
        var move = new MoveFleetCommand(me.Id, f.Id, other, Content.Fleets); Assert.False(move.CanExecute(ctx)); Assert.Contains("laid up", move.Refusal(ctx));
        Assert.Contains("laid up", new DrillFleetCommand(me.Id, f.Id, true, fs).Refusal(ctx));
        Assert.Contains("laid up", new DetachHullsCommand(me.Id, f.Id, 2, Content.Fleets, Content.ShipClasses).Refusal(ctx));
        Assert.False(new RefitFleetCommand(me.Id, f.Id).CanExecute(ctx));
        Assert.Contains("already", new LayUpFleetCommand(me.Id, f.Id, true, fs).Refusal(ctx));
        // a year laid up: it gains no readiness, and the year's forecast counts the lesser keep and no fuel for it
        tc.EndTurn(); tc.BeginTurn(); Fill(ctx, me);
        Assert.True(f.LaidUp); Assert.Equal(0, f.Readiness); Assert.Equal(9, f.Strength);
        // back in commission: it may sail again, starts with no readiness, and gains it by the year
        var back = new LayUpFleetCommand(me.Id, f.Id, false, fs); Assert.Null(back.Refusal(ctx)); Assert.True(tc.Executor.TryExecute(back, ctx));
        Assert.False(f.LaidUp); Assert.Equal(0, f.Readiness); Assert.Equal(9, FleetSystem.StrengthOf(w, house.Id)); Assert.Equal(full.Credits, fs.Upkeep(f).Credits);
        Assert.Equal(burn, FleetSystem.FuelBillFor(w, house.Id, Content.Fleets.FuelHullsPerUnit));
        Assert.Contains("in commission", new LayUpFleetCommand(me.Id, f.Id, false, fs).Refusal(ctx));
        tc.EndTurn(); tc.BeginTurn();
        Assert.True(f.Readiness > 0);
        // not away from home, and not another's
        Fill(ctx, me); f.SystemId = w.Galaxy.Systems.Keys.First(sid => !w.Holdings.Values.Any(h => h.OwnerId == house.Id && h.SystemId == sid));
        Assert.Contains("only at a world or station of your own", new LayUpFleetCommand(me.Id, f.Id, true, fs).Refusal(ctx));
        var theirs = w.Fleets.Values.First(x => x.OwnerId != house.Id && !x.Office); Assert.Equal("not your fleet", new LayUpFleetCommand(me.Id, theirs.Id, true, fs).Refusal(ctx));
    }

    /* docs/PLAYTHROUGH.md, Medium, A first game: "a Save that saves where you are" (N62: Save in the middle of a year stored
       nothing new, and a reload put the answered report back unanswered and the credits back where they were). */
    [Fact]
    public void ASaveInTheMiddleOfAYear_KeepsWhatWasDone_AndTheYearsAfterAreTheSameAsIfItHadNeverBeenClosed()
    {
        var (w, tc, house, me, _) = Start();
        tc.BeginTurn(); tc.EndTurn(); tc.BeginTurn();   // into the second year, so the save is not of a fresh world
        Assert.NotEmpty(tc.Briefing);
        // the head answers a report and spends from the treasury
        var first = tc.Briefing[0]; var choice = tc.Offer(first).First(c => c.Available).Choice.Id;
        Assert.True(tc.Resolve(first, choice));
        house.Treasury.Credits += 7;
        int credits = house.Treasury.Credits; int reports = tc.Briefing.Count;

        string json = tc.SaveMidYear();
        Assert.Null(w.MidYear);   // the world being played carries no mark of it

        var w2 = SaveSystem.Deserialize(json); var tc2 = new TurnController(w2, Content);
        Assert.NotNull(w2.MidYear);
        var again = tc2.Resume();
        Assert.Null(w2.MidYear);
        // the same year, not the next; the same reports, the answered one still answered; the same treasury
        Assert.Equal(w.Turn, w2.Turn);
        Assert.Equal(TurnPhase.Action, tc2.Phase);
        Assert.Equal(reports, again.Count);
        Assert.Equal(tc.Briefing.Select(b => (b.Fired.Def.Id, b.Resolved, b.ChosenId, string.Join(",", b.Fired.Binding.All.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value)))),
                     again.Select(b => (b.Fired.Def.Id, b.Resolved, b.ChosenId, string.Join(",", b.Fired.Binding.All.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value)))));
        Assert.True(again[0].Resolved);
        Assert.Equal(credits, w2.House(house.Id)!.Treasury.Credits);
        // an unanswered report can still be answered in the loaded year
        if (again.FirstOrDefault(b => !b.Resolved) is { } open && tc2.Offer(open).FirstOrDefault(c => c.Available) is { } oc)
        {
            var same = tc.Briefing.First(b => b.Fired.Def.Id == open.Fired.Def.Id && !b.Resolved);
            Assert.True(tc2.Resolve(open, oc.Choice.Id)); Assert.True(tc.Resolve(same, oc.Choice.Id));
        }
        // and the years that follow are the same in both, to the last character of the save
        for (int i = 0; i < 3; i++)
        {
            tc.EndTurn(); tc2.EndTurn();
            tc.SyncRng(); tc2.SyncRng();
            Assert.Equal(SaveSystem.Serialize(w), SaveSystem.Serialize(w2));
            tc.BeginTurn(); tc2.BeginTurn();
            Assert.Equal(tc.Briefing.Select(b => b.Fired.Def.Id), tc2.Briefing.Select(b => b.Fired.Def.Id));
        }

        // a save made between years is the plain save, and resuming it begins the next year
        var (w3, tc3, _, _, _) = Start();
        string idle = tc3.SaveMidYear();
        Assert.Equal(SaveSystem.Serialize(w3), idle);
        var w4 = SaveSystem.Deserialize(idle); var tc4 = new TurnController(w4, Content);
        Assert.Null(w4.MidYear); tc4.Resume(); Assert.Equal(1, w4.Turn);
    }

    /* docs/PLAYTHROUGH.md, Medium, A first game: "undo the last action of the year" (N25). The client takes an act back by loading the
       year as it opened and doing the earlier acts again; this holds the rule that makes that sound: the same acts on the same
       world come out the same, so the world after "do A, do B, take B back" is the world after "do A". */
    [Fact]
    public void TakingTheLastActBack_IsTheYearReopenedWithTheEarlierActsDoneAgain()
    {
        var (w, tc, house, me, _) = Start();
        tc.BeginTurn();
        string opened = tc.SaveMidYear();
        bool Do(World world, TurnController turn, ICommand c) { bool ok = turn.Executor.TryExecute(c, new CommandContext(world, WorldBuilder.RngFor(world))); turn.SyncRng(); return ok; }
        string skill = new[] { "tactics", "leadership", "discipline", "administration", "oratory" }.First(s => new TrainCommand(me.Id, s).CanExecute(new CommandContext(w, WorldBuilder.RngFor(w))));
        string law = Content.Policies.Laws.First().Id; string option = Content.Policies.Laws.First().Options.Select(o => o.Id).First(o => new EnactLawCommand(me.Id, law, o, Content.Policies).CanExecute(new CommandContext(w, WorldBuilder.RngFor(w))));

        // the played game: answer a report, study, then set a law
        var first = tc.Briefing[0]; string choice = tc.Offer(first).First(c => c.Available).Choice.Id;
        Assert.True(tc.Resolve(first, choice));
        Assert.True(Do(w, tc, new TrainCommand(me.Id, skill)));
        string beforeTheLast = tc.SaveMidYear();
        Assert.True(Do(w, tc, new EnactLawCommand(me.Id, law, option, Content.Policies)));
        Assert.NotEqual(beforeTheLast, tc.SaveMidYear());

        // taking the law back: the year as it opened, the report answered the same way, the study done again
        var w2 = SaveSystem.Deserialize(opened); var tc2 = new TurnController(w2, Content); tc2.Resume();
        Assert.True(tc2.Resolve(tc2.Briefing[0], choice));
        Assert.True(Do(w2, tc2, new TrainCommand(me.Id, skill)));
        Assert.Equal(beforeTheLast, tc2.SaveMidYear());
    }

    /* docs/PLAYTHROUGH.md, Medium, A first game: "Aims: the ambition's conditions and progress ... a next step". */
    [Fact]
    public void YourOwnAmbition_SaysHowNearItIs_WhereItStands_AndANextStep()
    {
        var content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7UL); var tc = new TurnController(w, content); tc.BeginTurn();
        var me = w.Protagonist!; var house = w.House(me.HouseId)!;
        // every ambition there is has an account, with a figure, where it stands, and a step while it is not had
        foreach (var a in content.Traits.Ambitions)
        {
            me.Ambition = a.Id; var aim = OwnAim.For(w, me);
            Assert.NotNull(aim); Assert.InRange(aim!.Progress, 0, 100); Assert.True(aim.Stands.Length > 10, a.Id); if (aim.Had) Assert.Equal(100, aim.Progress);
            if (!aim.Had) Assert.True(aim.Next.Length > 10 && aim.Target.Length > 0, a.Id);
        }
        me.Ambition = ""; Assert.Null(OwnAim.For(w, me));
        // a fortune is your own purse, not the treasury
        me.Ambition = "amass_a_fortune"; me.Purse = 0; house.Treasury.Credits = 900; Assert.Equal(0, OwnAim.For(w, me)!.Progress);
        me.Purse = AmbitionSystem.FortuneAt / 2; Assert.Equal(50, OwnAim.For(w, me)!.Progress);
        me.Purse = AmbitionSystem.FortuneAt; var rich = OwnAim.For(w, me)!; Assert.Equal(100, rich.Progress); Assert.True(rich.Had); Assert.Equal("", rich.Next);
        // a command is a command: having one is the whole of it
        me.Ambition = "command_a_fleet"; foreach (var f in w.Fleets.Values) if (f.AdmiralId == me.Id) f.AdmiralId = "";
        var none = OwnAim.For(w, me)!; Assert.True(none.Progress <= 60); Assert.False(none.Had);
        var fleet = w.Fleets.Values.First(f => f.OwnerId == house.Id); fleet.AdmiralId = me.Id; var has = OwnAim.For(w, me)!; Assert.Equal(100, has.Progress); Assert.Contains(fleet.Name, has.Stands);
        // the house rich again is the house's worth against the figure, and a debt is named as the first thing to clear
        me.Ambition = "restore_house_fortunes"; house.Wealth = AmbitionSystem.RestoredAt / 3; house.Debt = 10;
        var poor = OwnAim.For(w, me)!; Assert.Equal(33, poor.Progress); Assert.Contains("debt", poor.Next);
    }

    [Fact]
    public void TheRoadsToTheNextRank_AreListedOnlyWhenTheyAreOpen()
    {
        var content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7UL); var tc = new TurnController(w, content); tc.BeginTurn();
        var prog = tc.Progression!; var me = w.Protagonist!; var house = w.House(me.HouseId)!; var next = prog.NextTitle(w, house.Id);
        if (next == null) { Assert.Empty(prog.OpenRoads(w, me.Id)); return; }
        // each road is listed exactly when its own rule allows it
        var roads = prog.OpenRoads(w, me.Id);
        Assert.Equal(prog.PetitionRefusal(w, me.Id) == null, roads.Contains("petition")); Assert.Equal(prog.ClaimRefusal(w, me.Id) == null, roads.Contains("claim")); Assert.Equal(prog.SeizeRefusal(w, me.Id) == null, roads.Contains("seize"));
        // standing enough, and land enough: the petition opens
        house.Legitimacy = 100; foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet).Take(4)) h.OwnerId = house.Id; w.HoldingsChanged();
        if (prog.PetitionRefusal(w, me.Id) == null) Assert.Contains("petition", prog.OpenRoads(w, me.Id));
        // no standing, no claim, no ships: nothing is open, and nothing is said
        house.Legitimacy = 0; house.Claims.Clear(); foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id).ToList()) w.Fleets.Remove(f.Id);
        Assert.Empty(prog.OpenRoads(w, me.Id));
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
        // (a venture priced to return what its kind is meant to is worth about what it cost, so the case is made: one that cost little)
        var (w, tc, _, _, _) = Start(); var vs = tc.Ventures!; var b = w.Businesses.Values.OrderByDescending(x => vs.Value(w, x)).First(); var kind = Content.Businesses.Def(b.Kind)!; var cost = kind.Cost.Clone(); var more = kind.ExpandCost.Clone();
        foreach (var r in Resource.All) { kind.Cost.Set(r, 0); kind.ExpandCost.Set(r, 0); } kind.Cost.Credits = 4; kind.ExpandCost.Credits = 1;
        try { Assert.True(vs.Value(w, b) > kind.Value(b.Level), $"{b.Kind} worth {vs.Value(w, b)} against {kind.Value(b.Level)}"); AVentureJustFoundedAt(w, vs, b); }
        finally { foreach (var r in Resource.All) { kind.Cost.Set(r, cost.Get(r)); kind.ExpandCost.Set(r, more.Get(r)); } }
    }
    private static void AVentureJustFoundedAt(World w, VentureSystem vs, Business b)
    {
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
        Assert.Contains("−900 credits", stakes); Assert.Contains("legitimacy +2", stakes);
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
        house.Treasury.Credits = price - 1; Assert.Contains("needs " + Money.Show(price), new LobbyCommand(me.Id, m!.Id, leader.Id, 1, bribe: true).Refusal(ctx));
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
        var war = Assert.Single(w.NewsNow().Where(n => n.Text.Contains("declared war on your house"))); Assert.Equal("military", war.Target); Assert.Contains("Why:", war.Text); Assert.Contains("hulls", war.Text);
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
        house.Treasury.Credits = 4; Assert.Equal("600 credits short", new PayInstalmentCommand(me.Id, p.Id).Refusal(ctx));
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
        Assert.Contains(foe.Name, facts); Assert.Contains("a debt of long standing", facts); Assert.Contains("1,500 credits", facts); Assert.Contains("Eamon Thalen", facts);
        w.Battles.Add(new BattleRecord { Turn = 4, Place = "Cinder Gate", AttackerId = foe.Id, DefenderId = house.Id, WinnerId = foe.Id, AttackerHulls = 9, DefenderHulls = 4, AttackerLost = 1, DefenderLost = 3 });
        string fight = Praetoria.Core.Presentation.ReportFacts.For(w, line, Content);
        Assert.Contains("Cinder Gate", fight); Assert.Contains("your 4 hulls against 9", fight); Assert.Contains("You lost 3", fight);
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
        Assert.Contains(w.News, n => n.Text.Contains($"agreed a dowry of {Money.Show(due)}") && n.Text.Contains("only 300"));
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
        Assert.Contains("treasury holds 1,800", new BoardCommand(me.Id, co.Id, BoardCommand.PayIn, 50, tc.Companies!).Refusal(ctx));
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
        Assert.Contains(brief, l => l.Kind == Praetoria.Core.Presentation.YearInBrief.Courts && l.Text.Contains("against you") && l.Text.Contains("700 credits left"));
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
        int before = eco.ProsperityTarget(w, world), per = why.FirstOrDefault(x => x.what.Contains("standing") && x.what.Contains("building")).by;
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
        Assert.Contains(rec.Passed, l => l.Contains("purse of 1,400 credits") && l.Contains(me.Name));
        tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn();
        var brief = Praetoria.Core.Presentation.YearInBrief.For(w, Content);
        Assert.Contains(brief, l => l.Text.StartsWith(kin.Name + " died at") && l.Text.Contains("in battle"));
        Assert.Contains(brief, l => l.Text.Contains("the governing of " + world.Name) && l.Target == "galaxy:" + world.SystemId);
        Assert.DoesNotContain(brief, l => l.Text.Contains("of your house has died"));   // said once, as the report
    }
    [Fact]
    public void Last_years_accounts_add_up_to_the_change_in_the_treasury()
    {
        // docs/PLAYTHROUGH.md, Medium: "last year's accounts in the ledger, one-off lines included, so that the change in the treasury adds up"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        Assert.Null(Praetoria.Core.Presentation.YearInBrief.Accounts(w, Resource.Credits));   // no whole year has been played yet
        int opened = house.Treasury.Credits;
        house.Treasury.Credits -= 7;                                                          // the head spends in the year
        tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn();
        var acc = Praetoria.Core.Presentation.YearInBrief.Accounts(w, Resource.Credits)!.Value;
        Assert.Equal(opened, acc.open); Assert.Equal(house.Treasury.Credits, acc.close);
        Assert.Equal(acc.close - acc.open, acc.lines.Sum(l => l.By));
        Assert.Equal(-7, acc.lines[0].By); Assert.Equal(w.Counter(Praetoria.Core.Presentation.YearInBrief.LedgerKey(Resource.Credits)), acc.lines[1].By);
    }
    [Fact]
    public void Reports_fit_the_house_they_come_to()
    {
        // docs/PLAYTHROUGH.md, Medium: "cards that do not fit the house"
        var (w, tc, house, me, ctx) = Start("the_new_man"); tc.BeginTurn();
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        house.Treasury.Materials = 5;
        // a house with no forge-world hears nothing from "the forge-world"
        foreach (var h in w.HoldingsOf(house.Id).ToList()) h.Specialization = "trade_hub";
        Assert.DoesNotContain(engine.GatherEligible(w), e => e.def.Id == "the_failing_reactor");
        if (w.HoldingsOf(house.Id).FirstOrDefault(h => h.IsPlanet) is { } world)
        {
            world.Specialization = "forge_world"; Assert.Contains(engine.GatherEligible(w), e => e.def.Id == "the_failing_reactor");
        }
        // the steward of a report is the house's steward, or there is no such report
        w.Turn = 5; house.Duties.Clear();
        Assert.DoesNotContain(engine.GatherEligible(w), e => e.def.Id == "the_skimming_steward");
        // a relative's wedding does not raise the head's own wedding card
        var (w2, tc2, house2, me2, _) = Start(); tc2.BeginTurn();
        var bride = w2.LivingCharacters().First(c => c.Sex == "female" && c.Age is >= 18 and <= 35 && c.HouseId != house2.Id && !World.Outsider(c.HouseId) && DynastySystem.Spouse(w2, c) == null);
        var groom = w2.LivingCharacters().First(c => c.Sex == "male" && c.Age >= 18 && c.HouseId == house2.Id && c.Id != me2.Id && DynastySystem.Spouse(w2, c) == null);
        w2.WorldFlags.Remove(CourtshipSystem.JustWedFlag); CourtshipSystem.Wed(w2, bride, groom, CourtshipSystem.Arranged);
        Assert.False(w2.HasFlag(CourtshipSystem.JustWedFlag));
        Assert.Contains(w2.News, n => n.Text.Contains(groom.Name + " of your house has married " + bride.Name));
    }
    [Fact]
    public void TheFirstCase_ComesToANewJudge_NotInTheThirteenthYear()
    {
        // docs/PLAYTHROUGH.md, Medium: "the robe is new" in the thirteenth year
        var (w, tc, house, me, ctx) = Start("the_new_man"); tc.BeginTurn();
        var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
        me.CareerTrack = "law"; me.CareerRank = 1; me.Skills["law"] = 3; DynastySystem.MarkRanks(w);
        Assert.Equal(0, DynastySystem.YearsInRank(w, me));
        Assert.Contains(engine.GatherEligible(w), e => e.def.Id == "first_case");
        w.Turn += 13; Assert.Equal(13, DynastySystem.YearsInRank(w, me));
        Assert.DoesNotContain(engine.GatherEligible(w), e => e.def.Id == "first_case");
        // a step up is a new rank; someone met already grown and past the first rank has held it for years
        me.CareerRank = 2; DynastySystem.MarkRanks(w); Assert.Equal(0, DynastySystem.YearsInRank(w, me));
        var old = new Character { Id = "t_old", Name = "Old", HouseId = house.Id, Age = 50, Sex = "male", CareerTrack = "law", CareerRank = 3 }; w.Characters[old.Id] = old;
        DynastySystem.MarkRanks(w); Assert.Equal(DynastySystem.SettledYears, DynastySystem.YearsInRank(w, old));
    }

    [Fact]
    public void WhenTheHeadMayStand_ItIsTheHeadWhoIsConsul()
    {
        // docs/PLAYTHROUGH.md, Medium: the house's consul was the head's sister, when the head stood
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var consul = ElectionSystem.ConsulOffices(Content.Offices).First();
        var sister = new Character { Id = "t_sister", Name = "Sister", HouseId = house.Id, Age = 50, Sex = "female", CareerTrack = consul.Track.Length > 0 ? consul.Track : me.CareerTrack, CareerRank = 9 };
        if (consul.Requires.Length > 0) sister.Cursus.Add(consul.Requires); w.Characters[sister.Id] = sister;
        me.Age = Math.Max(me.Age, consul.MinAge); me.CareerRank = Math.Max(me.CareerRank, consul.MinRank); if (consul.Track.Length > 0) me.CareerTrack = consul.Track;
        if (consul.Requires.Length > 0 && !me.Cursus.Contains(consul.Requires)) me.Cursus.Add(consul.Requires);
        Assert.Equal(me.Id, OfficeSystem.Candidate(w, consul, house.Id)!.Id);
        // and when the head may not, it is the relative who can, and the declaration says so
        if (consul.Requires.Length > 0)
        {
            me.Cursus.Remove(consul.Requires); Assert.Equal(sister.Id, OfficeSystem.Candidate(w, consul, house.Id)!.Id);
            Fill(ctx, me); w.WorldFlags.Remove(ElectionSystem.StandFlag(house.Id));
            var stand = new StandForConsulCommand(me.Id, Content.Offices);
            if (stand.Refusal(ctx) == null) { Assert.True(tc.Executor.TryExecute(stand, ctx)); Assert.Contains(w.History, l => l.Text.Contains("in the person of Sister")); }
        }
    }

    [Fact]
    public void ASiege_StaysWithTheHouseThatBeganIt_AndItsYearsAreNotLost()
    {
        // docs/PLAYTHROUGH.md, Medium: a siege you press is taken over by an ally's fleet, your years are lost, and the ally takes the world (S70)
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var fleets = tc.Fleets;
        var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet));
        var ally = w.Houses.Values.First(h => h.Id != house.Id && h.Id != foe.Id && !World.Outsider(h.Id) && string.CompareOrdinal(h.Id, house.Id) != 0);
        var target = w.HoldingsOf(foe.Id).First(x => x.IsPlanet);
        foreach (var f in w.Fleets.Values.ToList()) w.Fleets.Remove(f.Id);
        w.Wars.Clear(); fleets.DeclareWar(w, house.Id, foe.Id); fleets.DeclareWar(w, ally.Id, foe.Id);
        w.Fleets["mine"] = new Fleet { Id = "mine", Name = "Mine", OwnerId = house.Id, SystemId = target.SystemId, Strength = 6, ArrivedTurn = w.Turn };
        // an enemy's world says how it is taken, in the years this one would take
        string how = fleets.HowToTake(w, target, house.Id); Assert.Contains(Praetoria.Core.Counted.Years(fleets.SiegeRequired(w, target)), how); Assert.Contains("Your fleet is over it", how);
        Assert.Equal("", fleets.HowToTake(w, w.HoldingsOf(house.Id).First(), house.Id));
        // and a fleet far from home says what its supply will do there
        string fed = fleets.SupplySays(w, w.Fleets["mine"], w.HoldingsOf(house.Id).First(x => x.IsPlanet).SystemId); Assert.Contains("in supply", fed);
        var far = w.Galaxy!.Systems.Keys.OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault(x => !fleets.InSupply(w, house.Id, x));
        if (far != null) { w.Fleets["mine"].Supply = 100; string hungry = fleets.SupplySays(w, w.Fleets["mine"], far); Assert.Contains("out of supply", hungry); Assert.Contains("a hull deserts each year", hungry); }
        fleets.ResolveConquests(w); Assert.Equal(house.Id, w.Sieges[target.Id].BesiegerId); int spent = w.Sieges[target.Id].Turns;
        // the ally comes later, with more hulls: the siege is still the house's that began it, and it goes on
        w.Fleets["theirs"] = new Fleet { Id = "theirs", Name = "Theirs", OwnerId = ally.Id, SystemId = target.SystemId, Strength = 30, ArrivedTurn = w.Turn + 1 };
        Assert.Equal(house.Id, fleets.Besieger(w, target));
        if (w.Sieges[target.Id].Required > spent + 1) { fleets.ResolveConquests(w); Assert.Equal(house.Id, w.Sieges[target.Id].BesiegerId); Assert.Equal(spent + 1, w.Sieges[target.Id].Turns); spent++; }
        // the house sails away: the ally takes the siege up where it stood, not from nought
        w.Fleets.Remove("mine");
        if (w.Sieges[target.Id].Required > spent + 1) { fleets.ResolveConquests(w); Assert.Equal(ally.Id, w.Sieges[target.Id].BesiegerId); Assert.Equal(spent + 1, w.Sieges[target.Id].Turns); }
        // two who come to an unbesieged world: the one who came first lays the siege
        w.Sieges.Clear(); w.Fleets["mine"] = new Fleet { Id = "mine", Name = "Mine", OwnerId = house.Id, SystemId = target.SystemId, Strength = 2, ArrivedTurn = w.Turn - 1 };
        w.Fleets["theirs"].ArrivedTurn = w.Turn; Assert.Equal(house.Id, fleets.Besieger(w, target));
    }
    [Fact]
    public void AnEnvoysPeace_IsReckonedByTheWar_AndTheCardSaysHowItStands()
    {
        // docs/PLAYTHROUGH.md, Medium, War and peace: an envoy whose terms can be read and accepted
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var fleets = tc.Fleets; var rules = Content.Fleets;
        var foe = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Count(x => x.IsPlanet) >= 2);
        w.Wars.Clear(); fleets.DeclareWar(w, foe.Id, house.Id); var war = w.Wars.First(x => x.Between(house.Id, foe.Id));
        var envoy = Economy.HeadOf(w, foe)!; envoy.Flags.Add(FleetSystem.SuingForPeaceFlag);
        var def = Content.Events.First(e => e.Id == Praetoria.Core.Presentation.ReportFacts.EnvoyReport);
        var binding = Praetoria.Core.Events.Binder.TryBind(def, w, TestContent.Rng()); Assert.NotNull(binding);
        var ec = new Praetoria.Core.Events.EvalContext(w, binding!, TestContent.Rng(), Content);
        Praetoria.Core.Events.Choice Ch(string id) => def.Choices.First(c => c.Id == id);
        bool Open(string id) => Ch(id).Requirements.All(r => r.Evaluate(ec));
        // level: nothing may be asked, and the card says so
        Assert.False(Open("demand_tribute")); Assert.False(Open("demand_world"));
        Assert.Contains("neither side is ahead", Praetoria.Core.Presentation.ReportFacts.For(w, def, Content));
        // three ahead: tribute by the score, which is what the choice pays; no world yet
        war.AddScore(house.Id, 3); foe.Treasury.Credits = 500; int owed = HousePeace.TributeOwed(w, rules, house.Id, foe.Id); Assert.True(owed > 0);
        Assert.True(Open("demand_tribute")); Assert.False(Open("demand_world"));
        string facts = Praetoria.Core.Presentation.ReportFacts.For(w, def, Content); Assert.Contains("you are 3 ahead", facts); Assert.Contains(owed.ToString(), facts);
        Assert.Contains("+" + Money.Credits(owed), Praetoria.Core.Presentation.ChoiceStakes.Of(Ch("demand_tribute"), ec, me));
        int before = house.Treasury.Credits; foreach (var e in Ch("demand_tribute").Effects) e.Apply(ec);
        Assert.Equal(before + owed, house.Treasury.Credits); Assert.False(w.AtWar(house.Id, foe.Id));
        // far ahead with the stronger fleet: a world may be named, and it changes hands
        fleets.DeclareWar(w, foe.Id, house.Id); war = w.Wars.First(x => x.Between(house.Id, foe.Id)); war.AddScore(house.Id, HousePeace.WorldLead); Hulls(w, foe.Id, 0); Hulls(w, house.Id, 9);
        envoy.Flags.Add(FleetSystem.SuingForPeaceFlag); Assert.True(Open("demand_world"));
        var world = HousePeace.Cedable(w, foe.Id).First(); foreach (var e in Ch("demand_world").Effects) e.Apply(ec);
        Assert.Equal(house.Id, world.OwnerId); Assert.False(w.AtWar(house.Id, foe.Id));
    }
    [Fact]
    public void HullsAreDetached_PaidOff_AndAnAdmiralShiftsFlag()
    {
        // docs/PLAYTHROUGH.md, Medium, War and peace: detach hulls to leave a guard; pay off hulls to cut the cost; move your flag
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); Fill(ctx, me); var rules = Content.Fleets; var classes = Content.ShipClasses;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id).ToList()) w.Fleets.Remove(f.Id);
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var heavy = classes.Defs.OrderByDescending(c => c.Cost.Materials + c.Cost.Credits).First(); var light = classes.Defs.OrderBy(c => c.Cost.Materials + c.Cost.Credits).First();
        var fleet = new Fleet { Id = "main", Name = "Main", OwnerId = house.Id, SystemId = home.SystemId, Strength = 0, Readiness = 80, AdmiralId = me.Id };
        fleet.Ships[heavy.Id] = 2; fleet.Ships[light.Id] = 3; fleet.Recount(); w.Fleets[fleet.Id] = fleet;
        // a guard is left: the lightest hulls, where the fleet lies, and the admiral stays with the fleet
        Assert.NotNull(new DetachHullsCommand(me.Id, fleet.Id, 5, rules, classes).Refusal(ctx));
        Assert.True(tc.Executor.TryExecute(new DetachHullsCommand(me.Id, fleet.Id, 2, rules, classes), ctx));
        var guard = w.Fleets.Values.Single(f => f.OwnerId == house.Id && f.Id != fleet.Id);
        Assert.Equal(2, guard.Strength); Assert.Equal(2, guard.Ships[light.Id]); Assert.Equal(3, fleet.Strength); Assert.Equal(home.SystemId, guard.SystemId); Assert.Equal("", guard.AdmiralId); Assert.Equal(me.Id, fleet.AdmiralId);
        // the flag is shifted: one command to a person, and the fleet left behind has no admiral
        Assert.True(tc.Executor.TryExecute(new AssignAdmiralCommand(me.Id, guard.Id, me.Id), ctx)); Assert.Equal(me.Id, guard.AdmiralId); Assert.Equal("", fleet.AdmiralId);
        // hulls are paid off at a world of the house's own: the cost goes, and a quarter of the materials comes back
        var pay = new PayOffHullsCommand(me.Id, fleet.Id, 1, rules, classes); int scrap = pay.Scrap(w); int mat = house.Treasury.Materials; var before = tc.Fleets.Upkeep(fleet).Credits;
        Assert.Equal(light.Cost.Materials * PayOffHullsCommand.ScrapPct / 100, scrap);
        Assert.True(tc.Executor.TryExecute(pay, ctx)); Assert.Equal(2, fleet.Strength); Assert.Equal(mat + scrap, house.Treasury.Materials); Assert.True(tc.Fleets.Upkeep(fleet).Credits <= before);
        // not in another house's space, and the last hulls take the fleet with them
        var elsewhere = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id && h.SystemId != home.SystemId); fleet.SystemId = elsewhere.SystemId;
        Assert.Contains("of your own", new PayOffHullsCommand(me.Id, fleet.Id, 1, rules, classes).Refusal(ctx));
        fleet.SystemId = home.SystemId; Assert.True(tc.Executor.TryExecute(new PayOffHullsCommand(me.Id, fleet.Id, 2, rules, classes), ctx)); Assert.False(w.Fleets.ContainsKey(fleet.Id));
    }
    [Fact]
    public void AFleetInYourSpace_SaysWhatItIsFor_AndCanBeAskedToGo()
    {
        // docs/PLAYTHROUGH.md, Medium, War and peace: neutral fleets over your world: what they are doing, and a way to ask them to go (S47, S80)
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); Fill(ctx, me);
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        var other = w.Houses.Values.First(h => h.Id != house.Id && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && x.SystemId != home.SystemId && w.Galaxy!.Hops(home.SystemId, x.SystemId) > 0));
        w.Wars.Clear(); foreach (var f in w.Fleets.Values.ToList()) w.Fleets.Remove(f.Id);
        var theirs = new Fleet { Id = "theirs", Name = "Theirs", OwnerId = other.Id, SystemId = home.SystemId, Strength = 6, ArrivedTurn = w.Turn - 2, Errand = "to stand over your seat as a threat: its head thinks ill of you" };
        w.Fleets[theirs.Id] = theirs; var head = Economy.HeadOf(w, other)!;
        string doing = FleetSystem.Doing(w, theirs, house.Id);
        Assert.Contains("Not at war with you", doing); Assert.Contains("to stand over your seat", doing); Assert.Contains("2 years", doing);
        // a head who thinks ill of you, with more hulls there than you, says no, and the refusal gives both figures
        w.Relationship(head.Id, me.Id).Disposition = -60; var ask = new AskFleetToLeaveCommand(me.Id, theirs.Id);
        string? no = ask.Refusal(ctx); Assert.NotNull(no); Assert.Contains("-60", no); Assert.Contains("0 hulls here to its 6", no);
        // as many hulls there as it has, and it goes; or a head who thinks no worse of you than the mark
        w.Fleets["mine"] = new Fleet { Id = "mine", Name = "Mine", OwnerId = house.Id, SystemId = home.SystemId, Strength = 6 }; Assert.Null(ask.Refusal(ctx));
        w.Fleets.Remove("mine"); w.Relationship(head.Id, me.Id).Disposition = FleetSystem.ObligesAt; Assert.Null(ask.Refusal(ctx));
        int inf = ctx.PoolsOf(me.Id).Influence; Assert.True(tc.Executor.TryExecute(ask, ctx)); Assert.Equal(inf - 1, ctx.PoolsOf(me.Id).Influence);
        Assert.True(theirs.Moving); Assert.Equal(other.Id, w.PlanetHolder(theirs.DestinationId) ?? w.Holdings.Values.First(h => h.SystemId == theirs.DestinationId).OwnerId);
        Assert.True(w.Counter(FleetSystem.AskedOffKey(other.Id, house.Id)) > w.Turn);
        // at war it is not asked, and it is not asked out of space that is not yours
        theirs.DestinationId = ""; theirs.SystemId = home.SystemId; tc.Fleets.DeclareWar(w, other.Id, house.Id); Assert.Contains("at war", new AskFleetToLeaveCommand(me.Id, theirs.Id).Refusal(ctx));
        Assert.StartsWith("At war with you", FleetSystem.Doing(w, theirs, house.Id));
        // a fleet held by office says whose it is and what ends it
        var office = new Fleet { Id = "office", Name = "The Reach Squadron", OwnerId = house.Id, SystemId = home.SystemId, Strength = 4, Office = true }; w.Fleets[office.Id] = office;
        var admiral = Content.Offices.Defs.First(d => d.CommandsFleets); OfficeSystem.Grant(w, admiral, house.Id, me);
        string terms = OfficeSystem.FleetTerms(w, Content.Offices, office); Assert.Contains(admiral.Name, terms); Assert.Contains("There is no term", terms); Assert.Contains("not yours to pay off", terms);
        Assert.NotNull(new PayOffHullsCommand(me.Id, office.Id, 1, Content.Fleets, Content.ShipClasses).Refusal(ctx));
    }
    [Fact]
    public void ARollCallGivesReasons_AndAMotionIsForecastBeforeItIsMoved()
    {
        // docs/PLAYTHROUGH.md, Medium, the Senate: a roll call with each house's reason; how a motion would fare before paying to move it
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var senate = tc.Senate;
        for (int y = 0; y < 3; y++) { tc.EndTurn(); tc.BeginTurn(); }
        var target = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        foreach (var kind in new[] { "censure", "levy" })
        {
            if (senate.Motions.Def(kind) is not { } def) continue; string t = def.Target == "house" ? target.Id : "";
            // the forecast is the count the motion has the moment it is on the docket
            var (ff, fa) = senate.Forecast(w, house.Id, kind, t);
            var m = senate.Add(w, house.Id, kind, t); var (tf, ta) = senate.Tally(w, m);
            Assert.Equal((tf, ta), (ff, fa));
            // each house's reasons add up to its lean, and the mover's own is that it moved it
            foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id)))
            {
                var why = senate.Why(w, m, h.Id); Assert.Equal(senate.Lean(w, m, h.Id), why.Sum(x => x.by));
                Assert.All(why, x => { Assert.NotEqual(0, x.by); Assert.False(string.IsNullOrWhiteSpace(x.why)); });
            }
            Assert.Contains(senate.Why(w, m, house.Id), x => x.why == "it moved the motion");
            // a whip is worth what the screen says it is
            int before = senate.Lean(w, m, target.Id); m.Lobby[target.Id] = SenateSystem.LobbyWeight;
            Assert.Equal(before + SenateSystem.LobbyWeight, senate.Why(w, m, target.Id).Sum(x => x.by));
        }
    }
    [Fact]
    public void ACrisis_IsExplainedFromItsOwnRules()
    {
        // docs/PLAYTHROUGH.md, Medium, the Senate: crises explained: what it is, who is behind it, what it does each year, what each answer costs
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var engine = tc.Crises!;
        var revolt = engine.Def("local_revolt")!; w.WorldCounters["unrest"] = 6; engine.Trigger(revolt, w, TestContent.Rng());
        var lines = Praetoria.Core.Presentation.CrisisFacts.For(w, engine, revolt, Content.Recovery);
        string all = string.Join("\n", lines.Select(l => l.Label + ": " + l.Text));
        Assert.Contains("unrest at or above 5 (it is " + w.Counter("unrest") + ")", all);
        Assert.Contains("Nobody: it came of how things stood", all);
        Assert.Contains("its severity does not grow with time", all);
        Assert.Contains("Civil War waits on", all);                       // the mark it leaves is another crisis's gate
        Assert.Contains("its cause still holds", all);
        foreach (var d in revolt.Dampers) Assert.Contains(lines, l => l.Label == "Answer: " + d.Name && l.Text.Contains("Takes " + d.Relief));
        // and it is true: a year passing takes nothing from it while its cause holds, and one severity once it has passed
        int sev = w.Crises[revolt.Id].Severity; engine.Decay(w, TestContent.Rng()); Assert.Equal(sev, w.Crises[revolt.Id].Severity);
        w.WorldCounters["unrest"] = 0; engine.Decay(w, TestContent.Rng()); Assert.Equal(sev - 1, w.Crises.TryGetValue(revolt.Id, out var left) ? left.Severity : 0);
    }
    [Fact]
    public void AHouseCrossesTheFloorInOneStep_AtAPriceItIsTold()
    {
        // docs/PLAYTHROUGH.md, Medium, the Senate: benches: cross in one step with the cost shown
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); Fill(ctx, me);
        var benches = w.Factions.Where(f => f.Members.Count > 0).ToList(); Assert.True(benches.Count >= 2);
        var from = benches.First(f => f.LeaderHouseId != house.Id); var to = benches.First(f => f.Id != from.Id);
        foreach (var f in w.Factions) f.Members.Remove(house.Id); from.Members.Add(house.Id);
        var oldLeader = Economy.HeadOf(w, w.House(from.LeaderHouseId)!)!; var newLeader = Economy.HeadOf(w, w.House(to.LeaderHouseId)!)!;
        w.Relationship(newLeader.Id, me.Id).Disposition = 0; int regard = w.Relationship(oldLeader.Id, me.Id).Disposition, inf = ctx.PoolsOf(me.Id).Influence;
        Assert.Contains(FactionSystem.BreakWhipDisposition + " the worse of you", FactionSystem.WalkSays(w, from));
        Assert.True(tc.Executor.TryExecute(new JoinFactionCommand(me.Id, to.Id), ctx));
        Assert.Equal(to.Id, FactionSystem.Of(w, house.Id)!.Id); Assert.DoesNotContain(house.Id, from.Members);
        Assert.Equal(inf - FactionSystem.JoinInfluence, ctx.PoolsOf(me.Id).Influence); Assert.Equal(regard - FactionSystem.BreakWhipDisposition, w.Relationship(oldLeader.Id, me.Id).Disposition);
        Assert.Contains(w.News, n => n.Text.StartsWith("You have left " + from.Name));
        Assert.Equal("you sit with them already", new JoinFactionCommand(me.Id, to.Id).Refusal(ctx));
    }
    [Fact]
    public void Manpower_StopsAtWhatTheWorldsCanMuster()
    {
        // the owner, 2026-11-21: "there should 100% be a cap ... you're not gonna be in charge of one world that has like 100 million troops"
        Assert.Equal(30, Economy.MusterSettle(20, 10, 100));           // under the cap, the year's gain is kept
        Assert.Equal(100, Economy.MusterSettle(95, 10, 100));          // it stops at the cap
        Assert.Equal(100, Economy.MusterSettle(100, 12, 100));         // and stays there
        Assert.Equal(175, Economy.MusterSettle(200, 10, 100));         // an excess already held grows no more, and a quarter of it stands down
        Assert.Equal(100, Economy.MusterSettle(101, 0, 100));
        Assert.Equal(90, Economy.MusterSettle(120, -30, 100));         // spending is spending
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        int cap = Economy.MusterCap(w, house.Id); Assert.Equal(w.HoldingsOf(house.Id).Where(h => h.IsPlanet).Sum(h => h.Population), cap);
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == house.Id && h.IsPlanet).ToList()) Assert.True(h.Population > 0);
        var landless = w.Houses.Values.First(h => h.Id != house.Id); Assert.True(Economy.MusterCap(w, landless.Id) >= Economy.MusterFloor);
        // a year turns on a house a little under its cap: it ends at the cap of the worlds as they then are, and the player is told once
        house.Treasury.Manpower = Economy.MusterCap(w, house.Id, Content.Holdings) - 1; house.Treasury.Credits = 500; tc.EndTurn(); tc.BeginTurn();
        Assert.True(house.Treasury.Manpower <= Economy.MusterCap(w, house.Id, Content.Holdings));
        Assert.Contains(w.News, n => n.Text.StartsWith("Your worlds can muster no more"));
        // no house in the Reach ends a long run with a host its worlds could not keep
        for (int y = 0; y < 12; y++) { tc.EndTurn(); tc.BeginTurn(); }
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id))) Assert.True(h.Treasury.Manpower <= Math.Max(Economy.MusterCap(w, h.Id, Content.Holdings), 60), h.Name + " holds " + h.Treasury.Manpower + " of " + Economy.MusterCap(w, h.Id, Content.Holdings));
    }

    [Fact]
    public void AWorkGang_HastensAWork_ForManpowerEachYear()
    {
        // the owner, 2026-11-21: "you can definitely spend manpower to hasten buildings, but it would be more of a cost ... over [time]"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var cat = Content.Holdings; int gm = cat.Rules.GangManpower;
        var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        var p = new Project { Id = "t_gang", OwnerId = house.Id, Kind = Project.Building, DefId = "x", TargetId = seat.Id, Name = "a test works", TurnsTotal = 6, TurnsLeft = 6, Cost = new Resources { Credits = 60 } }; w.Projects.Add(p);
        house.Treasury.Credits = 500; house.Treasury.Manpower = 0;
        Assert.Contains(gm + " manpower a year", new WorkGangCommand(me.Id, p.Id, true, cat).Refusal(ctx));
        house.Treasury.Manpower = 3 * gm; Assert.True(tc.Executor.TryExecute(new WorkGangCommand(me.Id, p.Id, true, cat), ctx)); Assert.True(p.Gang);
        Assert.Equal("a gang is on it", new WorkGangCommand(me.Id, p.Id, true, cat).Refusal(ctx));
        Assert.NotNull(new HastenWorkCommand(me.Id, p.Id, true, cat).Refusal(ctx));
        // the year's outgoings count the gang, and the work goes two years in one for it
        var outs = HouseAccounts.Outgoings(w, cat, house, tc.Economy); Assert.Contains(outs, o => o.what.StartsWith("a gang hastening") && o.cost.Manpower == gm);
        int mp = house.Treasury.Manpower, paid = p.Paid.Credits; ProjectSystem.Tick(w, cat, Content.Businesses);
        Assert.Equal(4, p.TurnsLeft); Assert.Equal(mp - gm, house.Treasury.Manpower); Assert.True(p.Paid.Credits > paid);
        // a year the men cannot be found it goes at its own pace, and the house is told
        house.Treasury.Manpower = gm - 1; ProjectSystem.Tick(w, cat, Content.Businesses); Assert.Equal(3, p.TurnsLeft);
        Assert.Contains(w.News, n => n.Text.Contains("was not worked by its gang this year"));
        // called off, it goes on as any work does
        Assert.True(tc.Executor.TryExecute(new WorkGangCommand(me.Id, p.Id, false, cat), ctx)); house.Treasury.Manpower = 50; ProjectSystem.Tick(w, cat, Content.Businesses); Assert.Equal(2, p.TurnsLeft); Assert.Equal(50, house.Treasury.Manpower);
        // a company's work takes no gang
        p.CompanyId = "co"; Assert.Contains("company", new WorkGangCommand(me.Id, p.Id, true, cat).Refusal(ctx));
    }
    [Fact]
    public void AFleetIsHiredOutAsMercenaries_ForPay_AndAtACostToTheHousesName()
    {
        // the owner, 2026-11-21: "you can have forces act as mercenaries in war, but I think that should come with a [hurt] to reputation"; manpower is never sold
        Assert.False(Content.Market.Good(Resource.Manpower)?.Sell ?? false);
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); Fill(ctx, me);
        w.Wars.Clear(); foreach (var f in w.Fleets.Values.ToList()) w.Fleets.Remove(f.Id);
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        var others = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var employer = others[0]; var foe = others[1]; var eHead = Economy.HeadOf(w, employer)!; var foeHead = Economy.HeadOf(w, foe)!;
        var fleet = new Fleet { Id = "co", Name = "The Company", OwnerId = house.Id, SystemId = home.SystemId, Strength = 4, AdmiralId = me.Id }; w.Fleets[fleet.Id] = fleet;
        // nobody is hiring in peace
        Assert.Empty(MercenarySystem.Employers(w, house.Id)); Assert.Contains("at war with no one", new HireOutFleetCommand(me.Id, fleet.Id, employer.Id).Refusal(ctx));
        tc.Fleets.DeclareWar(w, employer.Id, foe.Id); employer.Treasury.Credits = 100; w.Relationship(eHead.Id, me.Id).Disposition = 0;
        Assert.Contains(employer, MercenarySystem.Employers(w, house.Id));
        int legit = house.Legitimacy, credits = house.Treasury.Credits, foeRegard = w.Relationship(foeHead.Id, me.Id).Disposition, pay = MercenarySystem.PayOf(fleet);
        Assert.Contains("legitimacy −" + MercenarySystem.Legitimacy, MercenarySystem.Says(w, fleet, employer));
        Assert.True(tc.Executor.TryExecute(new HireOutFleetCommand(me.Id, fleet.Id, employer.Id), ctx));
        // it sails under the employer, keeps its own admiral, and is kept whole as a levy is
        Assert.Equal(employer.Id, fleet.OwnerId); Assert.Equal(house.Id, fleet.HiredFrom); Assert.Equal(me.Id, fleet.AdmiralId);
        Assert.NotNull(new DetachHullsCommand(eHead.Id, fleet.Id, 1, Content.Fleets, Content.ShipClasses).Refusal(new CommandContext(w, TestContent.Rng())));
        // the first year is paid, the house is thought the less of, and the house it is turned on thinks the worse of its head
        Assert.Equal(credits + pay, house.Treasury.Credits); Assert.Equal(legit - MercenarySystem.Legitimacy, house.Legitimacy);
        Assert.Equal(foeRegard - MercenarySystem.FoeRegard, w.Relationship(foeHead.Id, me.Id).Disposition);
        Assert.Contains(fleet, MercenarySystem.HiredOutBy(w, house.Id)); Assert.Contains("a company hired from " + house.Name, FleetSystem.Doing(w, fleet, foe.Id));
        // each year after, the pay comes; when the pay is not found it comes home
        w.Turn += 1; credits = house.Treasury.Credits; MercenarySystem.Tick(w); Assert.Equal(credits + pay, house.Treasury.Credits); Assert.Equal(employer.Id, fleet.OwnerId);
        w.Turn += 1; employer.Treasury.Credits = 0; MercenarySystem.Tick(w); Assert.Equal(house.Id, fleet.OwnerId); Assert.Equal("", fleet.HiredFrom); Assert.Equal("", fleet.LevyOf);
        Assert.Contains(w.News, n => n.Text.Contains("has left " + employer.Name + "'s service"));
        // hired again and called back: the contract is broken, and the employer's head remembers
        employer.Treasury.Credits = 100; fleet.DestinationId = ""; fleet.SystemId = home.SystemId; Assert.True(tc.Executor.TryExecute(new HireOutFleetCommand(me.Id, fleet.Id, employer.Id), ctx));
        int eRegard = w.Relationship(eHead.Id, me.Id).Disposition; Assert.True(tc.Executor.TryExecute(new RecallHiredFleetCommand(me.Id, fleet.Id), ctx));
        Assert.Equal(house.Id, fleet.OwnerId); Assert.Equal(eRegard - MercenarySystem.BreakRegard, w.Relationship(eHead.Id, me.Id).Disposition);
        // when the employer's wars end, a company comes home; one destroyed in service is told of
        Assert.True(tc.Executor.TryExecute(new HireOutFleetCommand(me.Id, fleet.Id, employer.Id), ctx)); w.Wars.Clear(); w.Turn += 1; MercenarySystem.Tick(w); Assert.Equal(house.Id, fleet.OwnerId);
        tc.Fleets.DeclareWar(w, employer.Id, foe.Id); fleet.DestinationId = ""; fleet.SystemId = home.SystemId; Assert.True(tc.Executor.TryExecute(new HireOutFleetCommand(me.Id, fleet.Id, employer.Id), ctx));
        w.Fleets.Remove(fleet.Id); MercenarySystem.Tick(w); Assert.Contains(w.News, n => n.Text.Contains("The Company, which you hired out, has been destroyed"));
    }
    [Fact]
    public void AFleetAtDrill_IsWorthMore_ForManpowerEachYear()
    {
        // the owner, 2026-11-21: "you can have maybe smaller forces but ... you train them better"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var fleets = tc.Fleets; var rules = Content.Fleets;
        w.Wars.Clear(); foreach (var f in w.Fleets.Values.ToList()) w.Fleets.Remove(f.Id);
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        var fleet = new Fleet { Id = "d", Name = "The Drilled", OwnerId = house.Id, SystemId = home.SystemId, Strength = 5, Readiness = 100 }; w.Fleets[fleet.Id] = fleet;
        var raw = new Fleet { Id = "r", Name = "The Raw", OwnerId = house.Id, SystemId = home.SystemId, Strength = 5, Readiness = 100 }; w.Fleets[raw.Id] = raw;
        int cost = fleets.DrillCost(fleet); Assert.Equal(3, cost);           // one for every two hulls, rounded up
        Assert.True(tc.Executor.TryExecute(new DrillFleetCommand(me.Id, fleet.Id, true, fleets), ctx)); Assert.True(fleet.Drilling);
        house.Treasury.Manpower = 50; house.Treasury.Credits = 500;
        for (int y = 1; y <= rules.DrillCap; y++) { int mp = house.Treasury.Manpower; fleets.Tick(w); Assert.Equal(y, fleet.Veterancy); Assert.Equal(mp - cost, house.Treasury.Manpower); }
        // drill takes it no further: the drill ends of itself and the house is told
        fleets.Tick(w); Assert.Equal(rules.DrillCap, fleet.Veterancy); Assert.False(fleet.Drilling); Assert.Contains(w.News, n => n.Text.Contains("as well drilled as drill makes a fleet"));
        Assert.NotNull(new DrillFleetCommand(me.Id, fleet.Id, true, fleets).Refusal(ctx));
        // the same hulls are worth more in a fight
        Assert.Equal(1 + rules.DrillCap * rules.VeterancyBonus, (1 + fleet.Veterancy * rules.VeterancyBonus) / (1 + raw.Veterancy * rules.VeterancyBonus), 6);
        // away from home, or without the men, it does not drill, and says why
        raw.Drilling = true; house.Treasury.Manpower = 0; Assert.Contains("manpower", fleets.DrillStops(w, raw)); fleets.Tick(w); Assert.Equal(0, raw.Veterancy);
        var elsewhere = w.Holdings.Values.First(h => h.IsPlanet && h.OwnerId != house.Id && h.SystemId != home.SystemId); raw.SystemId = elsewhere.SystemId; house.Treasury.Manpower = 50;
        Assert.Contains("of its house's own", fleets.DrillStops(w, raw));
    }
    [Fact]
    public void AWorldsCitizens_AreRealNumbers_ByItsKindAndPlace()
    {
        // the owner, 2026-11-21: "the citizens of certain cities or planets should be realistic numbers"
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn(); var census = Praetoria.Core.Presentation.Census.Citizens;
        var peopled = w.Holdings.Values.Where(h => h.IsPlanet && h.Population > 0).ToList(); Assert.True(peopled.Count > 20);
        // every peopled world is somewhere between a mining camp and a crowded capital
        foreach (var h in peopled) { long c = census(w, h); Assert.True(c >= 500_000 && c <= 40_000_000_000, $"{h.Name}: {c}"); }
        // the capital of the Reach holds billions; a barren or frozen world of the same scale holds far fewer than a terran one
        var capital = w.Holdings.Values.First(h => h.IsPlanet && h.SystemId == w.Galaxy!.Capital); Assert.True(census(w, capital) >= 2_000_000_000, capital.Name + " " + census(w, capital));
        Assert.True(Praetoria.Core.Presentation.Census.Habitable("terran") > 5 * Praetoria.Core.Presentation.Census.Habitable("barren"));
        // twice as high on the scale is four times the people, and nothing about it is stored: the same world always says the same
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet); long at20 = Praetoria.Core.Presentation.Census.At(w, home, 20), at40 = Praetoria.Core.Presentation.Census.At(w, home, 40);
        Assert.InRange(at40 / (double)at20, 3.99, 4.01); Assert.Equal(census(w, home), census(w, home)); Assert.Equal(0, Praetoria.Core.Presentation.Census.At(w, home, 0));
        // a house's citizens are its worlds' citizens; a world's provinces share them, the seat holding half
        Assert.Equal(w.HoldingsOf(house.Id).Sum(h => census(w, h)), Praetoria.Core.Presentation.Census.CitizensOf(w, house.Id));
        var parted = peopled.FirstOrDefault(h => h.Provinces.Count > 1);
        if (parted != null)
        {
            long all = census(w, parted), sum = Enumerable.Range(0, parted.Provinces.Count).Sum(i => Praetoria.Core.Presentation.Census.CitizensOfProvince(w, parted, i));
            Assert.Equal(all / 2, Praetoria.Core.Presentation.Census.CitizensOfProvince(w, parted, 0)); Assert.True(sum <= all && sum >= all - parted.Provinces.Count);
        }
        // said in words a reader takes in
        Assert.Equal("7.33 billion", Praetoria.Core.Presentation.Census.Say(7_330_000_000)); Assert.Equal("12.4 billion", Praetoria.Core.Presentation.Census.Say(12_400_000_000));
        Assert.Equal("614 million", Praetoria.Core.Presentation.Census.Say(614_000_000)); Assert.Equal("6.73 million", Praetoria.Core.Presentation.Census.Say(6_730_000)); Assert.Equal("86,000", Praetoria.Core.Presentation.Census.Say(86_400));
        Assert.Equal("7.3B", Praetoria.Core.Presentation.Census.Short(7_330_000_000)); Assert.Equal("614M", Praetoria.Core.Presentation.Census.Short(614_000_000));
    }
}
