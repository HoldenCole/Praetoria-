using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Commanding a navy (docs/MILITARY.md, "Commanding a navy"; docs/PLAYTHROUGH.md, Hard): captains appointed from the service,
/// squadrons split and joined, a fleet's standing in one line, and the account of a war by year.</summary>
public class NavyTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Rings() { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { 0 })!; }
    private static FleetSystem Fleets(FleetRules? rules = null) => new(rules ?? Content.Fleets, Content.Holdings, Content.ShipClasses);
    private static Fleet At(World w, string id, string owner, string sys, int hulls, string admiral = "") { var f = new Fleet { Id = id, Name = "The " + id, OwnerId = owner, SystemId = sys, Strength = hulls, AdmiralId = admiral }; w.Fleets[id] = f; return f; }
    /// <summary>Someone of a house: by default a grown officer of the naval service, a Lieutenant.</summary>
    private static Character Officer(World w, string id, string house = "vega", int rank = 2, int age = 30, string track = "military")
    {
        var c = new Character { Id = id, Name = "Officer " + id, HouseId = house, Age = age, Alive = true, Sex = "female", CareerTrack = track, CareerRank = rank };
        w.Characters[id] = c; w.House(house)!.Members.Add(id); w.Pools[id] = ActionPools.ForNpc(); return c;
    }
    private static (World w, TurnController tc, House house, Character me, CommandContext ctx) Start(ulong seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!;
        return (w, tc, w.House(me.HouseId)!, me, new CommandContext(w, TestContent.Rng()));
    }
    private static void Year(TurnController tc) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }

    // ---------------- captains ----------------

    [Fact]
    public void A_captain_is_appointed_from_the_service_and_every_refusal_says_what_is_missing()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Pools["self"].Agents = 3;
        var f = At(w, "first", "vega", "tessaly", 4); var theirs = At(w, "theirs", "corwin", "mid1", 2);
        var lt = Officer(w, "lt"); var ensign = Officer(w, "ensign", rank: 1); var clerk = Officer(w, "clerk", track: "law", rank: 3); var boy = Officer(w, "boy", age: 12);
        var stranger = Officer(w, "stranger", house: "corwin"); var dead = Officer(w, "dead"); dead.Alive = false; var old = Officer(w, "old", rank: 4); old.Flags.Add(LifeSystem.FrailChar);
        string? No(string who, string fleet = "first") => new AppointCaptainCommand("self", fleet, who).Refusal(ctx);

        Assert.Equal("not your fleet", No("lt", "theirs")); Assert.Equal("not your fleet", No("lt", "no_such_fleet"));
        Assert.Equal("no one to appoint", No("dead")); Assert.Equal("no one to appoint", No("nobody"));
        Assert.Equal("Officer stranger is not of the house", No("stranger"));
        Assert.Equal($"Officer boy is 12: a captain is a grown officer, {DynastySystem.AgeOfMajority} or older", No("boy"));
        Assert.Equal("Officer clerk is not in the naval service: a captain is an officer of the Navy, a Lieutenant or above", No("clerk"));
        Assert.Equal("Officer ensign is not yet a Lieutenant: a captain is an officer of the Navy, a Lieutenant or above", No("ensign"));
        Assert.Equal("Officer old is too frail for the sea", No("old"));
        var levy = At(w, "levy", "vega", "tessaly", 1); levy.LevyOf = "corwin";
        Assert.Equal("a levy keeps the officers its own house gave it", No("lt", "levy")); w.Fleets.Remove("levy");
        // nobody is admiral and captain at once
        f.AdmiralId = "lt"; Assert.Equal("Officer lt is its admiral: nobody is admiral and captain at once", No("lt")); f.AdmiralId = "";
        var second = At(w, "second", "vega", "tessaly", 2, admiral: "lt");
        Assert.Equal("Officer lt is admiral of The second: nobody is admiral and captain at once", No("lt")); second.AdmiralId = "";
        // the appointment costs an agent, and says so when there is none
        w.Pools["self"].Agents = 0; Assert.Equal("needs 1 agent this year (0 left)", No("lt")); Assert.False(new AppointCaptainCommand("self", "first", "lt").CanExecute(ctx)); w.Pools["self"].Agents = 3;

        // made: the fleet has a captain, an agent is spent, and it is in the record of the officer's life
        Assert.Null(No("lt")); Assert.True(ex.TryExecute(new AppointCaptainCommand("self", "first", "lt"), ctx));
        Assert.Equal("lt", f.CaptainId); Assert.Equal(2, w.Pools["self"].Agents); Assert.Same(lt, FleetSystem.CaptainOf(w, f));
        Assert.Contains(lt.Life!.Deeds, d => d.Text == "Made captain of The first.");
        Assert.Equal("Officer lt is its captain already", No("lt"));
        // nor captain of two fleets: named to a second, the officer leaves the first
        Assert.True(ex.TryExecute(new AppointCaptainCommand("self", "second", "lt"), ctx));
        Assert.Equal("", f.CaptainId); Assert.Equal("lt", second.CaptainId);
        Assert.Contains(w.History, h => h.Text == "Officer lt is made captain of The second, and leaves The first without one.");
        // a captain given a flag leaves the captaincy
        Assert.True(ex.TryExecute(new AssignAdmiralCommand("self", "first", "lt"), ctx));
        Assert.Equal("lt", f.AdmiralId); Assert.Equal("", second.CaptainId);
        Assert.DoesNotContain(w.Fleets.Values, x => x.CaptainId.Length > 0 && w.Fleets.Values.Any(y => y.AdmiralId == x.CaptainId));

        // relieved: free, and refused where there is nobody to relieve
        Assert.Equal("it has no captain", new RelieveCaptainCommand("self", "second").Refusal(ctx)); Assert.Equal("not your fleet", new RelieveCaptainCommand("self", "theirs").Refusal(ctx));
        var other = Officer(w, "other", rank: 3); w.Pools["self"].Agents = 3; Assert.True(ex.TryExecute(new AppointCaptainCommand("self", "second", "other"), ctx)); int agents = w.Pools["self"].Agents;
        Assert.True(ex.TryExecute(new RelieveCaptainCommand("self", "second"), ctx)); Assert.Equal("", second.CaptainId); Assert.Equal(agents, w.Pools["self"].Agents);
        // a captain who dies leaves the post, and so does one who is no longer of the house
        second.CaptainId = "other"; other.Alive = false; FleetSystem.RefreshAdmiralFlags(w); Assert.Equal("", second.CaptainId);
        second.CaptainId = "stranger"; FleetSystem.RefreshAdmiralFlags(w); Assert.Equal("", second.CaptainId);
    }

    [Fact]
    public void A_captain_keeps_the_crews_ready_adds_a_little_by_gunnery_and_commands_a_fleet_that_has_no_admiral()
    {
        var w = Rings(); var fs = Fleets(); var f = At(w, "first", "vega", "tessaly", 4);
        var cap = Officer(w, "cap"); cap.Skills["tactics"] = 2; cap.Skills["gunnery"] = 3; cap.Skills["leadership"] = 1; cap.Skills["discipline"] = 3;
        double unled = fs.PowerOf(w, f, false); Assert.Equal(0.85, FleetSystem.AdmiralFactor(w, f), 6); Assert.Null(FleetSystem.Commander(w, f));

        // with no admiral the captain commands, by the admiral's own reckoning, and the fleet does not fight at 0.85
        f.CaptainId = "cap"; Assert.Same(cap, FleetSystem.Commander(w, f));
        Assert.Equal(1 + 2 * 0.05 + 3 * 0.03 + 1 * 0.02, FleetSystem.AdmiralFactor(w, f), 6); Assert.Equal(1.0, FleetSystem.CaptainFactor(w, f), 6);
        Assert.Equal(unled / 0.85 * 1.21, fs.PowerOf(w, f, false), 6);
        // the captain's own cap is the one that strains: a Lieutenant leads eight
        Assert.Equal(8, fs.CommandCap(cap)); f.Strength = 10; double over = fs.PowerOf(w, f, false); cap.CareerRank = 3; Assert.Equal(over / 0.9, fs.PowerOf(w, f, false), 6); cap.CareerRank = 2; f.Strength = 4;

        // under an admiral the captain's gunnery adds a hundredth a point, six at most
        var self = w.Char("self")!; self.Skills["tactics"] = 4; f.AdmiralId = "self"; Assert.Same(self, FleetSystem.Commander(w, f));
        Assert.Equal(1.2, FleetSystem.AdmiralFactor(w, f), 6); Assert.Equal(1.03, FleetSystem.CaptainFactor(w, f), 6);
        cap.Skills["gunnery"] = 9; Assert.Equal(1 + FleetSystem.CaptainGunneryMax, FleetSystem.CaptainFactor(w, f), 6); cap.Skills["gunnery"] = 3;
        f.CaptainId = ""; double led = fs.PowerOf(w, f, false); f.CaptainId = "cap"; Assert.Equal(led * 1.03, fs.PowerOf(w, f, false), 6);

        // the admiral wounded: the captain commands if that is the better for the fleet, and not if it is not
        self.Flags.Add(LifeSystem.WoundedChar); Assert.Equal(1.1, FleetSystem.CommandFactor(self), 6); Assert.Same(cap, FleetSystem.Commander(w, f));
        Assert.Equal(1.21, FleetSystem.AdmiralFactor(w, f), 6);
        self.Skills["tactics"] = 9; Assert.Same(self, FleetSystem.Commander(w, f)); self.Skills["tactics"] = 4;
        // the admiral dead: the captain has it
        self.Flags.Remove(LifeSystem.WoundedChar); var adm = Officer(w, "adm", rank: 5); f.AdmiralId = "adm"; adm.Alive = false; Assert.Same(cap, FleetSystem.Commander(w, f));
        f.AdmiralId = "";

        // readiness: a point more a year at anchor for each point of discipline, and a point less lost under way for every two
        var plain = At(w, "plain", "vega", "tessaly", 4); f.Readiness = 40; plain.Readiness = 40; w.House("vega")!.Treasury.Credits = 500; w.House("vega")!.Treasury.Helium3 = 50;
        Assert.Equal(3, FleetSystem.CaptainDrills(w, f)); Assert.Equal(1, FleetSystem.CaptainHolds(w, f)); Assert.Equal(0, FleetSystem.CaptainDrills(w, plain));
        fs.Tick(w); Assert.Equal(plain.Readiness + 3, f.Readiness); Assert.Equal(40 + Content.Fleets.ReadinessRecovery + 3, f.Readiness);
        f.Readiness = 40; plain.Readiness = 40; f.DestinationId = "mid1"; f.Eta = 3; plain.DestinationId = "mid1"; plain.Eta = 3;
        fs.Tick(w); Assert.Equal(plain.Readiness + 1, f.Readiness); Assert.Equal(40 - Content.Fleets.ReadinessDecayMoving + 1, f.Readiness);
        cap.Skills["discipline"] = 20; Assert.Equal(FleetSystem.CaptainDrillsMax, FleetSystem.CaptainDrills(w, f)); Assert.Equal(FleetSystem.CaptainHoldsMax, FleetSystem.CaptainHolds(w, f));
    }

    [Fact]
    public void A_captain_who_commands_in_a_won_battle_learns_as_an_admiral_does_and_is_remembered_for_it()
    {
        var w = Rings(); var fs = Fleets(); w.Turn = 3; fs.DeclareWar(w, "vega", "corwin"); w.Turn = 4;
        var mine = At(w, "first", "vega", "mid1", 40); var theirs = At(w, "theirs", "corwin", "mid1", 2);
        var cap = Officer(w, "cap"); cap.Skills["tactics"] = 1; cap.Skills["gunnery"] = 2; mine.CaptainId = "cap";
        fs.ResolveBattles(w, TestContent.Rng());
        var battle = Assert.Single(w.Battles); Assert.Equal("vega", battle.WinnerId);
        Assert.Contains(battle.Notes, n => n.Contains("led by Officer cap (its captain, tactics 1)"));
        Assert.Equal(2, cap.Skill("tactics"));                                        // the weaker of the two crafts first, as for an admiral
        Assert.Contains(cap.Life!.Deeds, d => d.Kind == "victory" && d.Text == "Won a battle as captain, in command of The first.");
        Assert.Contains(w.History, h => h.Text == "Officer cap learns from the victory (tactics 2).");
    }

    [Fact]
    public void When_the_admiral_falls_in_a_battle_the_captain_has_the_command_and_the_house_is_told()
    {
        var w = Rings(); var fs = Fleets(new FleetRules { AdmiralDeathChance = 1.0 }); w.Turn = 3; fs.DeclareWar(w, "corwin", "vega"); w.Turn = 4;
        var adm = Officer(w, "adm", rank: 5); var cap = Officer(w, "cap"); cap.Skills["tactics"] = 2;
        var mine = At(w, "first", "vega", "mid1", 6, admiral: "adm"); mine.CaptainId = "cap"; At(w, "theirs", "corwin", "mid1", 400);
        fs.ResolveBattles(w, TestContent.Rng());
        Assert.Equal("corwin", Assert.Single(w.Battles).WinnerId); Assert.False(adm.Alive); Assert.True(w.Fleets.ContainsKey("first"));
        Assert.Same(cap, FleetSystem.Commander(w, mine)); Assert.Equal(1.1, FleetSystem.AdmiralFactor(w, mine), 6);
        Assert.Contains(w.NewsNow(), n => n.Tone == "warn" && n.Target == "military:deck" && n.SubjectId == "first" && n.Text.StartsWith("Officer cap, captain of The first, has the command: Officer adm is dead."));
        Assert.Contains(w.History, h => h.Text == "Officer cap, captain of The first, has the command: Officer adm is dead.");
        // and the line on the fleet's card says who commands
        FleetSystem.RefreshAdmiralFlags(w); Assert.Contains("It has no admiral: its captain, Officer cap, commands.", fs.Stands(w, mine));
    }

    // ---------------- squadrons ----------------

    [Fact]
    public void A_detachment_may_be_of_a_chosen_class_and_may_take_the_captain_as_its_commander()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); var rules = Content.Fleets; var classes = Content.ShipClasses;
        var f = At(w, "first", "vega", "tessaly", 0, admiral: "self"); f.Ships["frigate"] = 3; f.Ships["cruiser"] = 2; f.Recount();
        // the lightest go when no class is named; a named class goes when it is
        Assert.Equal("frigate", Assert.Single(new DetachHullsCommand("self", "first", 2, rules, classes).Which(w)).cls.Id);
        Assert.Equal("The first has 2 cruisers, not 3", new DetachHullsCommand("self", "first", 3, rules, classes, "cruiser").Refusal(ctx));
        Assert.Equal("The first has no battleship", new DetachHullsCommand("self", "first", 1, rules, classes, "battleship").Refusal(ctx));
        Assert.Equal("it has no captain to send with them", new DetachHullsCommand("self", "first", 1, rules, classes, "cruiser", withCaptain: true).Refusal(ctx));
        Assert.StartsWith("a detachment is at least one hull and leaves at least one", new DetachHullsCommand("self", "first", 5, rules, classes, "cruiser").Refusal(ctx));
        Assert.True(ex.TryExecute(new DetachHullsCommand("self", "first", 1, rules, classes, "cruiser"), ctx));
        var guard = w.Fleets.Values.Single(x => x.Id != "first"); Assert.Equal(1, guard.Strength); Assert.Equal(1, guard.Ships["cruiser"]); Assert.Equal(1, f.Ships["cruiser"]); Assert.Equal(3, f.Ships["frigate"]);
        Assert.Equal("", guard.AdmiralId); Assert.Equal("self", f.AdmiralId);

        // sent with the captain: the squadron has a commander, and the fleet is without a captain
        var cap = Officer(w, "cap"); f.CaptainId = "cap";
        Assert.True(ex.TryExecute(new DetachHullsCommand("self", "first", 2, rules, classes, "frigate", withCaptain: true), ctx));
        var squadron = w.Fleets.Values.Single(x => x.Id != "first" && x.Id != guard.Id);
        Assert.Equal("cap", squadron.AdmiralId); Assert.Equal("", f.CaptainId); Assert.Equal(2, squadron.Ships["frigate"]); Assert.Equal("self", f.AdmiralId);
        Assert.Contains(FleetSystem.AdmiralFlag, cap.Flags);
        Assert.Contains(cap.Life!.Deeds, d => d.Text == $"Given the command of {squadron.Name}, detached from The first.");
        Assert.Contains(w.History, h => h.Text.Contains("under Officer cap, who was the fleet's captain"));
    }

    [Fact]
    public void Folding_one_fleet_into_another_keeps_the_receiving_fleets_admiral_and_the_screen_folds_the_way_its_label_says()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        var keeps = Officer(w, "keeps", rank: 4); var goes = Officer(w, "goes", rank: 4); var cap = Officer(w, "cap");
        var into = At(w, "into", "vega", "tessaly", 3, admiral: "keeps"); var from = At(w, "from", "vega", "tessaly", 2, admiral: "goes"); from.CaptainId = "cap";

        // a fleet with crews and one without are not one fleet, and the refusal says which is which and what to do
        from.LaidUp = true; var merge = new MergeFleetsCommand("self", "from", "into");
        Assert.False(merge.CanExecute(ctx));
        Assert.Equal("The from is laid up, with no crews aboard, and The into is in commission: put The from back in commission first, or lay The into up", merge.Refusal(ctx));
        from.LaidUp = false; Assert.Null(merge.Refusal(ctx));

        Assert.True(ex.TryExecute(merge, ctx));
        Assert.False(w.Fleets.ContainsKey("from")); Assert.Equal(5, into.Strength);
        Assert.Equal("keeps", into.AdmiralId);                       // the receiving fleet's admiral, not the folded one's
        Assert.Equal("cap", into.CaptainId);                         // it had no captain, and takes the other's
        Assert.Contains(w.History, h => h.Text == "The from is folded into The into (5 hulls); Officer keeps commands it.");

        // the screen: the option on a fleet's card reads "Fold <other> into <this>", names who commands afterwards, and the button
        // runs that fold and not the opposite one (it used to fold this fleet into the other, keeping the other admiral)
        var (sw, tc, house, me, _) = Start(); tc.BeginTurn();
        var mine = sw.Fleets.Values.Where(x => x.OwnerId == house.Id && !x.Office && x.LevyOf.Length == 0).OrderBy(x => x.Id, StringComparer.Ordinal).First();
        var extra = new Fleet { Id = "extra", Name = "The Extra", OwnerId = house.Id, SystemId = mine.SystemId, Strength = 2 }; sw.Fleets[extra.Id] = extra; mine.AdmiralId = me.Id;
        var card = CourtProjection.Build(tc, Content).Fleets.Single(l => l.Id == mine.Id); var opt = card.MergeOptions.Single(o => o.Id == "extra");
        Assert.Equal($"Fold The Extra into {mine.Name} · {me.Name} commands", opt.Label); Assert.Null(opt.Refusal);
        string razor = File.ReadAllText(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Web", "Components", "MilitaryScreen.razor"));
        Assert.Contains("new MergeFleetsCommand(pid, o.Id, f.Id)", razor); Assert.DoesNotContain("new MergeFleetsCommand(pid, f.Id, o.Id)", razor);
    }

    // ---------------- the fleet as a whole ----------------

    [Fact]
    public void A_fleets_card_says_in_one_line_where_it_stands_as_a_whole()
    {
        var w = Rings(); var fs = Fleets(); var f = At(w, "first", "vega", "tessaly", 4);
        // in commission, in supply and why, nobody in command
        Assert.Equal("In commission at Tessaly. In supply (100): it lies at Barony, a world of the house. It has no admiral and no captain: it fights at 85 in a hundred. 4 hulls, and nobody whose cap they would strain.", fs.Stands(w, f));
        // one lane off a world of the house is in supply; two is not, and the line says what would feed it
        f.SystemId = "mid1"; Assert.Contains("In supply (100): Barony, a world of the house, is 1 lane off.", fs.Stands(w, f)); Assert.True(fs.InSupply(w, "vega", "mid1"));
        f.SystemId = "prime"; f.Supply = 55; Assert.False(fs.InSupply(w, "vega", "prime")); Assert.Null(fs.SupplyWhy(w, "vega", "prime"));
        Assert.Contains($"Out of supply (55, falling {Content.Fleets.SupplyDrainNear} a year): no world of the house is within 1 lane, no supply depot within 2, and no campaign's object within 2.", fs.Stands(w, f));
        // a depot reaches two lanes, and a campaign feeds the fleets about its object
        w.Holding("barony")!.Buildings.Add("supply_depot"); Assert.Contains("In supply (55): the supply depot at Barony is 2 lanes off, and a depot reaches 2.", fs.Stands(w, f)); w.Holding("barony")!.Buildings.Remove("supply_depot");
        w.Wars.Add(new War { HouseA = "vega", HouseB = "aurelian", Since = 1 }); w.WorldFlags.Add(FleetSystem.CampaignPrefix("vega") + "prime"); w.WorldCounters[FleetSystem.CampaignUntilKey("vega")] = w.Turn + 5;
        Assert.Contains("In supply (55): the campaign's object, Prime, is here, and a campaign feeds the fleets within 2.", fs.Stands(w, f));
        // every system the map circles as in supply has a reason, and no other has
        foreach (var sys in w.Galaxy!.Systems.Keys) Assert.Equal(fs.InSupply(w, "vega", sys), fs.SupplyWhy(w, "vega", sys) != null);

        // who commands and who is captain, and the hulls against the commander's cap
        f.SystemId = "tessaly"; f.Supply = 100; var self = w.Char("self")!; self.CareerTrack = "military"; self.CareerRank = 0; f.AdmiralId = "self"; var cap = Officer(w, "cap"); f.CaptainId = "cap";
        Assert.EndsWith("self commands as admiral, with Officer cap as captain. 4 hulls against a cap of 2: 2 more than self leads without strain, and it fights at 90 in a hundred.", fs.Stands(w, f));
        self.CareerRank = 3; Assert.EndsWith("self commands as admiral, with Officer cap as captain. 4 hulls against a cap of 11.", fs.Stands(w, f));
        f.CaptainId = ""; Assert.Contains("self commands as admiral; it has no captain.", fs.Stands(w, f)); f.CaptainId = "cap";
        self.Flags.Add(LifeSystem.WoundedChar); cap.Skills["tactics"] = 5; Assert.Contains("self, its admiral, is wounded: the captain, Officer cap, commands. 4 hulls against a cap of 8.", fs.Stands(w, f));
        cap.Skills["tactics"] = 0; self.Skills["tactics"] = 6; Assert.Contains("self commands as admiral, wounded, with Officer cap as captain.", fs.Stands(w, f)); self.Flags.Remove(LifeSystem.WoundedChar);
        // under way, and laid up
        f.DestinationId = "mid1"; f.Eta = 1; Assert.StartsWith("In commission, under way to Midway (1 year). In supply where it is bound (100):", fs.Stands(w, f)); f.DestinationId = ""; f.Eta = 0;
        f.LaidUp = true; Assert.StartsWith($"Laid up at Tessaly: the hulls are kept and the crews paid off, at {Content.Fleets.LaidUpUpkeepPct} in a hundred of the keep.", fs.Stands(w, f));

        // and the card carries it, with the captain and the officers who could be one
        var (sw, tc, house, me, ctx) = Start(); tc.BeginTurn(); var mine = sw.Fleets.Values.Where(x => x.OwnerId == house.Id).OrderBy(x => x.Id, StringComparer.Ordinal).First();
        var officer = new Character { Id = "an_officer", Name = "Test Officer", HouseId = house.Id, Age = 30, Alive = true, Sex = "male", CareerTrack = "military", CareerRank = 2 }; sw.Characters[officer.Id] = officer; house.Members.Add(officer.Id);
        ctx.PoolsOf(me.Id).Agents = 2; if (mine.AdmiralId == officer.Id) mine.AdmiralId = "";
        var line = CourtProjection.Build(tc, Content).Fleets.Single(l => l.Id == mine.Id);
        Assert.Equal(tc.Fleets.Stands(sw, mine), line.Standing); Assert.Null(line.CaptainName); Assert.Equal("it has no captain", line.RelieveCaptain!.Refusal);
        var pick = line.CaptainOptions!.Single(o => o.Id == officer.Id); Assert.Null(pick.Refusal); Assert.StartsWith("Make Test Officer captain (Lieutenant, 30 · discipline 0, gunnery 0)", pick.Label); Assert.EndsWith("1 agent", pick.Label);
        Assert.True(tc.Executor.TryExecute(new AppointCaptainCommand(me.Id, mine.Id, officer.Id), ctx));
        line = CourtProjection.Build(tc, Content).Fleets.Single(l => l.Id == mine.Id);
        Assert.Equal("Test Officer", line.CaptainName); Assert.Equal(officer.Id, line.CaptainId); Assert.StartsWith("Lieutenant · discipline 0, gunnery 0", line.CaptainSays); Assert.Null(line.RelieveCaptain!.Refusal);
        Assert.Contains("Test Officer", line.Standing);
    }

    // ---------------- the account of a war ----------------

    [Fact]
    public void A_wars_year_is_written_down_when_the_year_turns_battles_hulls_worlds_the_score_and_the_cost()
    {
        var w = Rings(); var fs = Fleets(); var rng = TestContent.Rng(); w.Turn = 3; var vega = w.House("vega")!; vega.Treasury.Credits = 900; vega.Treasury.Helium3 = 60; vega.Treasury.Manpower = 20;
        fs.DeclareWar(w, "vega", "corwin"); var war = Assert.Single(w.Wars); Assert.Empty(war.Years); Assert.Empty(WarLedger.Brief(w, "vega"));
        var mine = At(w, "first", "vega", "mid1", 40); mine.Stance = "boarding"; At(w, "theirs", "corwin", "mid1", 6); var laid = At(w, "laid", "vega", "tessaly", 5); laid.LaidUp = true;

        // the year turns: the fleets are paid for, the battle is fought, the sieges are resolved, and the war's year is closed
        w.Turn = 4; int tanks = vega.Treasury.Helium3; int keep = fs.Upkeep(mine).Credits;
        fs.Tick(w); fs.ResolveBattles(w, rng); fs.ResolveConquests(w);
        var y = Assert.Single(WarLedger.ClosedYears(war)); var battle = Assert.Single(w.Battles);
        Assert.Equal(4, y.Turn); Assert.Equal(4, war.ClosedThrough); Assert.Equal("vega", battle.WinnerId);
        Assert.Equal(1, y.Battles); Assert.Equal(1, y.WonA); Assert.Equal(0, y.WonB);
        Assert.Equal(battle.AttackerId == "vega" ? battle.AttackerLost : battle.DefenderLost, y.LostA); Assert.Equal(battle.AttackerId == "vega" ? battle.DefenderLost : battle.AttackerLost, y.LostB);
        Assert.Equal(3, y.LostB); Assert.Equal(1, y.TakenA); Assert.Equal(0, y.TakenB);          // six hulls, half lost, and the boarders take one for the three
        Assert.Equal((0, 0, 2, 0), (y.OpenA, y.OpenB, y.CloseA, y.CloseB));
        // the cost is of the fleets in commission: the laid-up hulls are paid for, and are not in it; the fuel is what was burned
        Assert.Equal(keep, y.CreditsA); Assert.Equal(tanks - vega.Treasury.Helium3, y.FuelA); Assert.True(y.FuelA > 0); Assert.False(y.SharedA);
        Assert.Equal(0, y.FuelB);                                                              // Corwin had no helium-3 to burn
        string said = WarLedger.Say(w, war, y, "vega");
        Assert.Equal($"The war with House Corwin, its first year: one battle, and it was won. You lost {(y.LostA == 0 ? "no hull" : Counted.Hulls(y.LostA))} and sank 3. Your prize crews took 1 hull. The score stands 2 to 0; it was 0 to 0. It cost {Money.Credits(keep)} and {y.FuelA} helium-3.", said);
        Assert.Equal(("good", said), Assert.Single(WarLedger.Brief(w, "vega")));
        // the other side reads the same year its own way, and a house in neither reads both by name
        Assert.StartsWith("The war with House Vega, its first year: one battle, and it was lost. You lost 3 hulls and sank ", WarLedger.Say(w, war, y, "corwin"));
        Assert.Contains("Theirs took 1 hull of yours. The score stands 0 to 2; it was 0 to 0.", WarLedger.Say(w, war, y, "corwin"));
        Assert.StartsWith("The war between House Vega and House Corwin, its first year: one battle, which House Vega won.", WarLedger.Say(w, war, y, "aurelian"));
        Assert.StartsWith("Year 4, its first year: one battle, and it was won.", WarLedger.Say(w, war, y, "vega", named: false));
        // closing twice in a year changes nothing, and what happens now is written into the next year's account
        WarLedger.Close(w); Assert.Single(WarLedger.ClosedYears(war)); Assert.Equal(5, WarLedger.Year(w, war).Turn);

        // the next years: the fleet holds the sky over Midway until it falls, and that is in the year's account and is news
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == "corwin").ToList()) w.Fleets.Remove(f.Id);
        mine.DestinationId = ""; mine.SystemId = "mid1";
        for (int i = 0; i < 12 && w.Holding("corwin_world")!.OwnerId != "vega"; i++) { w.Turn++; fs.Tick(w); fs.ResolveBattles(w, rng); fs.ResolveConquests(w); }
        Assert.Equal("vega", w.Holding("corwin_world")!.OwnerId);
        var fell = WarLedger.ClosedYears(war).Last(); Assert.Equal(w.Turn, fell.Turn); var change = Assert.Single(fell.Changed);
        Assert.Equal(("corwin_world", "Midway", "corwin", "vega", "taken"), (change.HoldingId, change.Name, change.FromId, change.ToId, change.How)); Assert.True(change.World); Assert.True(change.Seat);
        Assert.Equal((2, 0, 5, 0), (fell.OpenA, fell.OpenB, fell.CloseA, fell.CloseB));
        string fellSays = WarLedger.Say(w, war, fell, "vega");
        Assert.Contains($"its {WarLedger.Nth(war.Years.IndexOf(fell) + 1)} year: no battle was fought. Midway was taken. It was the last House Corwin held. The score stands 5 to 0; it was 2 to 0. It cost ", fellSays);
        Assert.Contains("Midway was lost. It was the last you held. The score stands 0 to 5; it was 0 to 2.", WarLedger.Say(w, war, fell, "corwin"));
        Assert.Contains(w.News, n => n.Tone == "good" && n.SubjectId == "corwin_world" && n.Target == "galaxy:mid1" && n.Text.StartsWith("Midway is taken from House Corwin: it is yours."));
        // an earlier year is told in the past
        Assert.Contains("The score stood 2 to 0; it had been 0 to 0.", WarLedger.Say(w, war, y, "vega"));

        // it saves and loads: the years, what changed hands, and a fleet's captain
        Officer(w, "cap"); mine.CaptainId = "cap";
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var warBack = Assert.Single(back.Wars);
        Assert.Equal(war.Years.Count, warBack.Years.Count); Assert.Equal(war.ClosedThrough, warBack.ClosedThrough); Assert.Equal("cap", back.Fleets["first"].CaptainId);
        Assert.Equal(WarLedger.Say(w, war, y, "vega"), WarLedger.Say(back, warBack, warBack.Years[0], "vega"));
        Assert.Equal(fellSays, WarLedger.Say(back, warBack, warBack.Years.Single(x => x.Turn == fell.Turn), "vega"));

        // the peace: the war goes among the past wars with its account, and the house is told how the whole of it went
        fs.MakePeace(w, "vega", "corwin");
        Assert.Empty(w.Wars); var past = Assert.Single(w.PastWars); Assert.Same(war, past); Assert.Equal(w.Turn, past.Ended); Assert.Equal("peace was made", past.Ending);
        Assert.All(past.Years, x => Assert.True(x.Turn <= past.Ended));                           // the empty account of the year that was running is not kept
        string over = WarLedger.Over(w, past, "vega");
        Assert.StartsWith($"The war with House Corwin is over: peace was made in year {w.Turn}, after {Counted.Years(w.Turn - 3)}. In all: one battle, and it was won.", over);
        Assert.Contains("Midway was taken.", over); Assert.Contains("The score stood 5 to 0.", over);
        Assert.Contains(w.News, n => n.Target == "military:wars" && n.Text == over && n.SubjectId == "corwin");
        var (own, others) = WarLedger.History(w, "vega"); Assert.Same(past, Assert.Single(own)); Assert.Empty(others);
        var saved = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(over, WarLedger.Over(saved, Assert.Single(saved.PastWars), "vega"));
    }

    [Fact]
    public void A_world_lost_is_news_a_world_ceded_is_in_the_wars_last_account_and_the_keep_is_shared_between_two_wars()
    {
        var w = Rings(); var fs = Fleets(); var rng = TestContent.Rng(); w.Turn = 3; fs.DeclareWar(w, "corwin", "vega"); var war = w.Wars[0];
        // a world lost: Corwin's fleet alone over the barony until it falls
        At(w, "theirs", "corwin", "tessaly", 6);
        for (int i = 0; i < 12 && w.Holding("barony")!.OwnerId == "vega"; i++) { w.Turn++; fs.Tick(w); fs.ResolveBattles(w, rng); fs.ResolveConquests(w); }
        Assert.Equal("corwin", w.Holding("barony")!.OwnerId);
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Target == "military:wars" && n.SubjectId == "barony" && n.Text == "Barony is lost: House Corwin has taken it, and it counts three for them in the war's score.");
        var fell = WarLedger.ClosedYears(war).Last(); Assert.Contains("Barony was lost. It was the last you held.", WarLedger.Say(w, war, fell, "vega"));
        Assert.Equal("warn", WarLedger.Brief(w, "vega").Single().tone);

        // a peace with terms: the house far behind cedes a world, and that is the war's last account, "at its end"
        w.Holdings["corwin_two"] = new Holding { Id = "corwin_two", OwnerId = "corwin", Name = "Lesser Midway", Specialization = "agri", SystemId = "empty", Population = 4 };
        var second = new War { HouseA = "aurelian", HouseB = "corwin", Since = 2, ScoreA = 14, ScoreB = 1, ClosedThrough = w.Turn }; w.Wars.Add(second);
        fs.MakePeace(w, "aurelian", "corwin");
        Assert.DoesNotContain(second, w.Wars); Assert.Contains(second, w.PastWars); Assert.Equal("aurelian", w.Holding("corwin_two")!.OwnerId);
        var last = second.Years.Single(); Assert.True(last.Turn > second.Ended); var ceded = Assert.Single(last.Changed); Assert.Equal(("Lesser Midway", "corwin", "aurelian", "ceded"), (ceded.Name, ceded.FromId, ceded.ToId, ceded.How));
        Assert.Equal("The war with House Corwin, at its end: Lesser Midway was ceded to you. The score stood 14 to 1, as it had.", WarLedger.Say(w, second, last, "aurelian"));
        Assert.Equal("At its end: Lesser Midway was ceded to House Aurelian. The score stood 1 to 14, as it had.", WarLedger.Say(w, second, last, "corwin", named: false));
        Assert.Contains("House Corwin ceded Lesser Midway to House Aurelian.", WarLedger.Over(w, second, "vega"));
        var (own, others) = WarLedger.History(w, "vega"); Assert.Same(war, Assert.Single(own)); Assert.Same(second, Assert.Single(others));

        // ceded to the player's house, or by it, it is news
        var third = new War { HouseA = "vega", HouseB = "corwin", Since = 2, ScoreA = 0, ScoreB = 0 };
        fs.MakePeace(w, "vega", "corwin"); w.Wars.Add(third); third.ScoreA = 12;
        fs.CedeWorld(w, w.Holding("corwin_world")!, "corwin", "vega");
        Assert.Contains(w.News, n => n.Tone == "good" && n.SubjectId == "corwin_world" && n.Text.StartsWith("By the terms of the peace House Corwin cedes Midway to you."));
        fs.CedeWorld(w, w.Holding("corwin_world")!, "vega", "corwin");
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Target == "military:wars" && n.Text == "By the terms of the peace Midway is ceded to House Corwin. It is yours no longer.");
        Assert.Equal(2, WarLedger.Year(w, third).Changed.Count);
        // a seat on a world that passes between the two houses is in the account as well, and is not a world
        WarLedger.Seat(w, w.Holding("corwin_world")!, "The Midway March", "vega", "corwin");
        var seat = WarLedger.Year(w, third).Changed.Last(); Assert.False(seat.World); Assert.Equal(("The Midway March", "vega", "corwin", "taken"), (seat.Name, seat.FromId, seat.ToId, seat.How));
        Assert.Contains("The Midway March was lost.", WarLedger.Say(w, third, WarLedger.Year(w, third), "vega")); Assert.Contains("The Midway March was taken.", WarLedger.Say(w, third, WarLedger.Year(w, third), "corwin"));
        WarLedger.Seat(w, w.Holding("corwin_world")!, "No War's March", "aurelian", "corwin"); Assert.Equal(3, WarLedger.Year(w, third).Changed.Count);   // houses not at war: nothing is written

        // a house in two wars bears its fleets' keep once: it is shared between them, the odd part to the older
        var fourth = new War { HouseA = "aurelian", HouseB = "vega", Since = 3 }; w.Wars.Add(fourth);
        WarLedger.Cost(w, "vega", 7, 3);
        var a = WarLedger.Year(w, third); var b = WarLedger.Year(w, fourth);
        Assert.Equal((4, 2, true), (a.CreditsA, a.FuelA, a.SharedA)); Assert.Equal((3, 1, true), (b.CreditsB, b.FuelB, b.SharedB));
        Assert.EndsWith("It cost 400 credits and 2 helium-3, the keep of your fleets being shared between your wars.", WarLedger.Say(w, third, a, "vega"));

        // a war that ends without a peace (a house is no more) is kept too, and says how
        WarLedger.Strike(w, x => x == fourth, "House Aurelian was no more");
        Assert.DoesNotContain(fourth, w.Wars); Assert.Contains(fourth, w.PastWars); Assert.Equal("House Aurelian was no more", fourth.Ending);
        w.Houses.Remove("aurelian"); Assert.StartsWith("The war with House Aurelian is over: House Aurelian was no more in year", WarLedger.Over(w, fourth, "vega"));
        // only so many past wars are kept: the oldest between other houses go first, and the player's own are kept longest
        for (int i = 0; i < WarLedger.KeptOthers + 5; i++) { var x = new War { HouseA = "corwin", HouseB = "other" + i, Since = 1 }; w.Wars.Add(x); WarLedger.Strike(w, q => q == x, "it ended"); }
        Assert.Equal(WarLedger.KeptOthers, w.PastWars.Count(x => !x.Involves("vega"))); Assert.Contains(war, w.PastWars); Assert.DoesNotContain(second, w.PastWars);
    }

    [Fact]
    public void The_year_in_brief_has_a_paragraph_for_each_war_and_the_battles_fought_as_the_year_turned()
    {
        var (w, tc, house, me, ctx) = Start(); tc.BeginTurn();
        var foe = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null && w.Fleets.Values.Any(f => f.OwnerId == h.Id && !f.Office)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        tc.Fleets.DeclareWar(w, house.Id, foe.Id); var war = w.Wars.Single(x => x.Between(house.Id, foe.Id));
        var home = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        var mine = new Fleet { Id = "the_line", Name = "The Line", OwnerId = house.Id, SystemId = home.SystemId, Strength = 60, AdmiralId = me.Id }; w.Fleets[mine.Id] = mine;
        house.Treasury.Credits += 2000; house.Treasury.Helium3 += 100;
        tc.EndTurn(); Assert.False(w.IsDynastyDead);
        // as the year turns an enemy squadron lies over the home world, and the line is there to meet it
        mine.SystemId = home.SystemId; mine.DestinationId = ""; mine.Eta = 0;
        w.Fleets["the_raid"] = new Fleet { Id = "the_raid", Name = "The Raid", OwnerId = foe.Id, SystemId = home.SystemId, Strength = 2 };
        tc.BeginTurn();
        var fought = w.Battles.Where(b => b.Turn == w.Turn && b.Involves(house.Id) && b.Involves(foe.Id)).ToList(); Assert.NotEmpty(fought);
        var brief = YearInBrief.For(w, Content);
        // the battle is in the brief of the year it was fought in (it used to be a year late)
        Assert.Contains(brief, l => l.Kind == YearInBrief.War && l.Target == "military:battles" && l.Text.StartsWith($"The battle of {fought[0].Place}:"));
        var y = WarLedger.ClosedYears(war).Single(x => x.Turn == w.Turn); Assert.Equal(fought.Count, y.Battles); Assert.True(y.CreditsA > 0);
        var para = Assert.Single(brief, l => l.Target == WarLedger.Target && l.Text.StartsWith($"The war with {foe.Name}, its first year: "));
        Assert.Equal(YearInBrief.War, para.Kind); Assert.Equal(WarLedger.Say(w, war, y, house.Id), para.Text);
        Assert.Contains("The score stands ", para.Text); Assert.Contains("It cost ", para.Text); Assert.Contains(" credits and ", para.Text);
        // the next year, with no battle, the paragraph is of that year and the old battle is not told again
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == foe.Id).ToList()) w.Fleets.Remove(f.Id);
        tc.EndTurn(); if (w.IsDynastyDead || !w.Wars.Contains(war)) return; tc.BeginTurn(); if (!w.Wars.Contains(war)) return;
        brief = YearInBrief.For(w, Content);
        Assert.DoesNotContain(brief, l => l.Target == "military:battles" && l.Text.Contains(fought[0].Place) && !w.Battles.Any(b => b.Turn == w.Turn && b.Involves(house.Id)));
        Assert.Contains(brief, l => l.Target == WarLedger.Target && l.Text.StartsWith($"The war with {foe.Name}, its second year: "));
    }

    [Fact]
    public void Other_houses_give_their_fleets_captains_by_the_same_rule_where_they_have_officers()
    {
        var (w, tc, house, me, ctx) = Start();
        // a house with a fleet that has its admiral, and an officer of the service to spare
        var fleet = w.Fleets.Values.Where(f => f.OwnerId != house.Id && !World.Outsider(f.OwnerId) && !f.Office && f.LevyOf.Length == 0 && w.Char(f.AdmiralId) is { Alive: true } && Economy.HeadOf(w, w.House(f.OwnerId)!) != null)
            .OrderBy(f => f.Id, StringComparer.Ordinal).First();
        var theirs = w.House(fleet.OwnerId)!;
        var officer = new Character { Id = "their_officer", Name = "Their Officer", HouseId = theirs.Id, Age = 34, Alive = true, Sex = "male", CareerTrack = "military", CareerRank = 3 };
        officer.Skills["discipline"] = 8; officer.Skills["gunnery"] = 8; w.Characters[officer.Id] = officer; theirs.Members.Add(officer.Id); w.Pools[officer.Id] = ActionPools.ForNpc();
        Assert.Null(FleetSystem.CaptainRefusal(w, fleet, officer));
        for (int i = 0; i < 8 && !w.IsDynastyDead && officer.Alive && !w.Fleets.Values.Any(f => f.CaptainId == officer.Id || f.AdmiralId == officer.Id); i++) Year(tc);
        Assert.False(w.IsDynastyDead); Assert.True(officer.Alive);
        Assert.Contains(w.Fleets.Values, f => f.OwnerId == theirs.Id && f.CaptainId == officer.Id);
        // and nowhere in the Reach is the rule broken: a captain is alive, of the fleet's house, in the service, a Lieutenant or above,
        // admiral of nothing and captain of one fleet
        var captains = w.Fleets.Values.Where(f => f.CaptainId.Length > 0).ToList();
        Assert.All(captains, f =>
        {
            var c = w.Char(f.CaptainId)!; Assert.True(c.Alive); Assert.True(c.HouseId == f.OwnerId || c.HouseId == f.HiredFrom); Assert.Equal("military", c.CareerTrack); Assert.True(c.CareerRank >= FleetSystem.CaptainRank);
            Assert.DoesNotContain(w.Fleets.Values, x => x.AdmiralId == c.Id); Assert.Single(w.Fleets.Values, x => x.CaptainId == c.Id);
        });
        // the world with captains and war accounts in it saves and loads
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(captains.Select(f => f.Id + ":" + f.CaptainId).OrderBy(x => x), back.Fleets.Values.Where(f => f.CaptainId.Length > 0).Select(f => f.Id + ":" + f.CaptainId).OrderBy(x => x));
        Assert.Equal(w.Wars.Sum(x => x.Years.Count), back.Wars.Sum(x => x.Years.Count)); Assert.Equal(w.PastWars.Count, back.PastWars.Count);
    }
}
