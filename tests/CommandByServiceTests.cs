using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A command by service (docs/MILITARY.md, "A command by service"; docs/IDEAS_FROM_MODS.md, entry 1; docs/GAPS.md Q15 and R1): a legate
/// named by an office-holder, an officer lent to a house at war, a command sought; what it gives the commander and what stays the owner's; how it
/// ends; what the other houses do; and the Caesar road it opens.</summary>
public class CommandByServiceTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;
    private static World Rings() { var m = typeof(RegionSystemTests).GetMethod("Rings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!; return (World)m.Invoke(null, new object[] { 0 })!; }
    private static Fleet At(World w, string id, string owner, string sys, int hulls, string admiral = "", bool office = false)
    { var f = new Fleet { Id = id, Name = "The " + id, OwnerId = owner, SystemId = sys, Strength = hulls, AdmiralId = admiral, Office = office }; w.Fleets[id] = f; return f; }
    /// <summary>Someone of a house: by default a grown officer of the Navy, a Lieutenant, with tactics 4.</summary>
    private static Character Person(World w, string id, string house, int tactics = 4, int rank = 2, int age = 30, string track = "military")
    {
        var c = new Character { Id = id, Name = "Officer " + id, HouseId = house, Age = age, Alive = true, Sex = "male", CareerTrack = track, CareerRank = rank };
        c.Skills["tactics"] = tactics; w.Characters[id] = c; w.House(house)!.Members.Add(id); w.Pools[id] = ActionPools.ForNpc(); return c;
    }
    private static void Regard(World w, string from, string to, int value) => w.Relationship(from, to).Disposition = value;
    private static void Consul(World w, string house, string who) => w.Offices.Add(new OfficeHolding { OfficeId = "consul_senior", HouseId = house, CharacterId = who, SinceTurn = 0 });
    private static void Admiral(World w, string house, string who) => w.Offices.Add(new OfficeHolding { OfficeId = "admiral", HouseId = house, CharacterId = who, SinceTurn = 0 });
    private static void War(World w, string a, string b) => w.Wars.Add(new War { HouseA = a, HouseB = b, Since = w.Turn, ClosedThrough = w.Turn });
    /// <summary>Every battle goes to the one who attacks.</summary>
    private sealed class Zero : IRng { public ulong State => 0; public double NextDouble() => 0.0; public int NextInt(int lo, int hi) => lo; }

    // ---------------- the ways in ----------------

    [Fact]
    public void A_legate_is_named_by_an_office_holder_and_every_refusal_says_what_is_missing()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Pools["self"].Agents = 3;
        var own = At(w, "own", "vega", "tessaly", 4); var state = At(w, "state", "aurelian", "prime", 5, office: true); var theirs = At(w, "theirs", "corwin", "mid1", 3);
        var lt = Person(w, "lt", "corwin", tactics: 5); var kin = Person(w, "kin", "vega"); var boy = Person(w, "boy", "corwin", age: 12); var frail = Person(w, "frail", "corwin"); frail.Flags.Add(LifeSystem.FrailChar);
        var sour = Person(w, "sour", "aurelian"); Regard(w, "sour", "self", -40);
        string? No(string fleet, string who) => new NameLegateCommand("self", fleet, who, Offices).Refusal(ctx);

        Assert.Equal("you hold no consulship, no Admiralty and no emergency powers: only those name a legate", No("own", "lt"));
        Consul(w, "vega", "self");
        Assert.StartsWith("The theirs is not under your office", No("theirs", "lt"));
        Assert.Equal("Officer kin is of your house: give the command on the fleet's card", No("own", "kin"));
        Assert.Equal("nobody names themselves legate", No("own", "self"));
        Assert.Equal($"Officer boy is 12: a commander is grown, {DynastySystem.AgeOfMajority} or older", No("own", "boy"));
        Assert.Equal("Officer frail is too frail for the sea", No("own", "frail"));
        Assert.Equal("Officer sour thinks too ill of you (-40) to serve under you: it takes -20 or better", No("own", "sour"));
        theirs.AdmiralId = "lt"; Assert.Equal("Officer lt commands The theirs already", No("own", "lt")); theirs.AdmiralId = "";
        War(w, "vega", "corwin"); Assert.Equal("House Corwin is at war with your house", No("own", "lt")); w.Wars.Clear();
        w.WorldCounters[CaesarSystem.BarredKey("lt")] = w.Turn + 3;
        Assert.Equal($"the Senate had Officer lt lay a command down, and Officer lt may hold none until year {w.Turn + 3}", No("own", "lt")); w.WorldCounters.Remove(CaesarSystem.BarredKey("lt"));
        w.Pools["self"].Agents = 0; Assert.Equal("needs 1 agent this year (0 left)", No("own", "lt")); w.Pools["self"].Agents = 3;

        // a consul names to the state's fleet when no Admiral holds it, and to the house's own; the Admiral to the office fleets
        Assert.Null(No("state", "lt")); Admiral(w, "aurelian", "aurelian_head");
        Assert.StartsWith("The state is not under your office", No("state", "lt"));

        Assert.True(ex.TryExecute(new NameLegateCommand("self", "own", "lt", Offices), ctx));
        var s = Assert.Single(w.Service);
        Assert.Equal(("own", "lt", "corwin", "vega", ServiceSystem.Legate, ServiceSystem.ByConsul, "self"), (s.FleetId, s.CommanderId, s.CommanderHouseId, s.OwnerId, s.Kind, s.Authority, s.NamedById));
        Assert.Equal("lt", own.AdmiralId); Assert.Equal(2, w.Pools["self"].Agents);
        Assert.Equal(ServiceSystem.NamedRegard, w.GetRelationship("lt", "self")!.Disposition);
        Assert.Equal(ServiceSystem.BondRegard, w.GetRelationship("corwin_head", "self")!.Disposition);
        Assert.Equal(1, w.Counter(ServiceSystem.CommandsKey)); Assert.Equal(1, w.Counter(ServiceSystem.LegatesKey)); Assert.True(w.HasFlag(ServiceSystem.GivenFlag));
        Assert.Contains(w.News, n => n.Target == ServiceSystem.Target && n.Text.Contains("Officer lt of House Corwin is named legate of The own"));
        Assert.Same(s, ServiceSystem.Of(w, own)); Assert.Same(s, ServiceSystem.Held(w, lt)); Assert.Equal("vega", ServiceSystem.Serves(w, lt));
        Assert.Contains("commands The own of House Vega as legate, named by self, consul, since year 0: no battle yet.", ServiceSystem.Says(w, s));
    }

    [Fact]
    public void A_house_at_war_borrows_an_officer_with_the_leave_of_its_head()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Pools["self"].Agents = 3;
        var own = At(w, "own", "vega", "tessaly", 4); var lt = Person(w, "lt", "corwin");
        string? No(string who = "lt", string fleet = "own") => new BorrowOfficerCommand("self", fleet, who).Refusal(ctx);

        Assert.Equal("your house is at war with nobody: an officer is lent to a house at war", No());
        War(w, "vega", "aurelian");
        Assert.Equal("self is of your house: give the command on the fleet's card", No("self"));
        Assert.Equal("Mara is the head of House Corwin: a head is not lent, though a head may offer their own service", No("corwin_head"));
        Assert.Equal("Mara thinks 0 of self, and lends an officer at 10 (at 0 to a house in the same war)", No());
        Regard(w, "corwin_head", "self", 15);
        War(w, "corwin", "prime_rebels"); Assert.Equal("House Corwin is at war itself, and keeps its officers", No()); w.Wars.RemoveAll(x => x.Involves("prime_rebels"));
        Regard(w, "lt", "self", -30); Assert.Equal("Officer lt thinks too ill of self to serve under self (-30)", No()); Regard(w, "lt", "self", 0);
        // a house in the same war lends at nothing worse than indifference
        Regard(w, "corwin_head", "self", 0); War(w, "corwin", "aurelian"); Assert.Null(No()); w.Wars.RemoveAll(x => x.Between("corwin", "aurelian")); Regard(w, "corwin_head", "self", 15);
        var office = At(w, "office", "vega", "tessaly", 2, office: true);
        Assert.Equal("The office is not a fleet of the house's own in commission: an officer is lent for one of those", No(fleet: "office"));

        Assert.True(ex.TryExecute(new BorrowOfficerCommand("self", "own", "lt"), ctx));
        var s = Assert.Single(w.Service);
        Assert.Equal((ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian", "corwin_head"), (s.Kind, s.Authority, s.EnemyId, s.LeaveById));
        Assert.Equal("lt", own.AdmiralId); Assert.Equal(1, w.Counter(ServiceSystem.LentKey));

        // the war ends, and the officer goes home
        w.Wars.Clear(); w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Empty(w.Service); var past = Assert.Single(w.PastService);
        Assert.Equal("House Vega's war is over, and the officer goes home", past.How); Assert.Equal("", own.AdmiralId); Assert.Equal(1, w.Counter(ServiceSystem.EndedKey));
    }

    [Fact]
    public void A_command_is_sought_and_taken_or_refused_by_plain_reasons()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Pools["self"].Agents = 3;
        var me = w.Protagonist!; me.Skills["tactics"] = 1;
        var cor = At(w, "cor", "corwin", "mid1", 4);
        var weak = Person(w, "weak", "corwin", tactics: 2); cor.AdmiralId = "weak";
        var kin = Person(w, "kin", "vega", tactics: 6, rank: 3); kin.Flags.Add(FamilySystem.DefiantFlag);
        string? No(string who = "self", string taker = "corwin") => new SeekCommandCommand("self", who, taker, Offices).Refusal(ctx);

        Assert.Equal("House Corwin is at war with nobody, and takes no officer from another house", No());
        War(w, "corwin", "aurelian");
        Assert.Equal("it is the house's own: the head gives its commands on each fleet's card", No(taker: "vega"));
        Assert.Equal("a command asks tactics of 3 or a Lieutenant's rank in the Navy, and self has tactics 1 and is not in the Navy", No());
        me.Skills["tactics"] = 3; Assert.Equal("a command asks a name (renown 10), and self's renown is 0", No());
        me.CareerTrack = "military"; me.CareerRank = 2; Regard(w, "corwin_head", "self", -5);
        Assert.Equal("Mara thinks ill of self (-5), and takes nobody from a house thought ill of", No()); Regard(w, "corwin_head", "self", 0);
        Assert.Equal("House Corwin has no command free: every fleet of it has a commander, none with 2 less tactics than self's 3", No());
        me.Skills["tactics"] = 4;
        Assert.Equal("Officer kin is defiant of you, and will not go at your word", No("kin"));
        Assert.Equal("Officer weak is not of your house", No("weak"));
        Assert.Equal("nobody to offer service to", No(taker: "nobody"));
        Assert.Equal("Lucius holds no consulship, no Admiralty and no emergency powers, and names no legate", No(taker: "aurelian_head"));
        Assert.Equal(new[] { "aurelian", "corwin" }, ServiceSystem.TakersFor(w, Offices, me));   // never the seeker's own house, nor a house at war with it

        Assert.True(ex.TryExecute(new SeekCommandCommand("self", "self", "corwin", Offices), ctx));
        var s = Assert.Single(w.Service);
        Assert.True(s.Sought); Assert.Equal(("cor", "self", ServiceSystem.Lent, "aurelian"), (s.FleetId, s.CommanderId, s.Kind, s.EnemyId)); Assert.Equal("", s.LeaveById);
        Assert.Equal("self", cor.AdmiralId); Assert.Equal(2, w.Pools["self"].Agents); Assert.Equal(1, w.Counter(ServiceSystem.SoughtKey));
        Assert.Equal(ServiceSystem.DisplacedRegard, w.GetRelationship("weak", "corwin_head")!.Disposition);   // one displaced thinks the worse of who did it
        // it is in the record of the player's life
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "post" && d.Text == "Lent by House Vega to command The cor for House Corwin in its war with House Aurelian.");
        Assert.Contains(w.News, n => n.Text.StartsWith("You are lent to command The cor for House Corwin"));
        Assert.Equal("self commands The cor already", No());
    }

    // ---------------- what it gives ----------------

    [Fact]
    public void The_victories_and_the_crews_are_the_commanders_and_the_hulls_and_the_spoils_stay_the_owners()
    {
        var w = Rings(); var fleets = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses); w.Turn = 10;   // past the years a saga opens on, when old service is credited
        var me = w.Protagonist!; me.CareerTrack = "military"; me.CareerRank = 3; me.Skills["tactics"] = 6;
        var cor = At(w, "cor", "corwin", "empty", 8); cor.ArrivedTurn = 1;
        var foe = At(w, "foe", "aurelian", "empty", 1); foe.ArrivedTurn = 0;
        War(w, "corwin", "aurelian"); Regard(w, "corwin_head", "self", 20);
        var s = ServiceSystem.Begin(w, cor, me, w.Char("corwin_head")!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        Assert.Same(me, CaesarSystem.GeneralOf(w, w.House("vega")));   // the house's general is the one of it who commands, whoever's the fleet
        Assert.Null(CaesarSystem.GeneralOf(w, w.House("corwin")));
        int hullsBefore = FleetSystem.StrengthOf(w, "corwin"), mineBefore = FleetSystem.StrengthOf(w, "vega");

        fleets.ResolveBattles(w, new Zero());
        Assert.Equal(1, s.Won); Assert.Equal(0, s.Lost);
        Assert.Equal(1, CaesarSystem.Victories(w, me)); Assert.Equal("self", cor.DevotedTo); Assert.True(cor.Devotion > 0);
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "victory" && d.Text == "Won a battle in command of The cor, for House Corwin, as an officer lent.");
        Assert.True(FleetSystem.StrengthOf(w, "corwin") >= hullsBefore - 1); Assert.Equal(mineBefore, FleetSystem.StrengthOf(w, "vega"));   // the hulls are Corwin's
        Assert.Contains("self", w.Wars.Single().Years.Last().ServedA.Concat(w.Wars.Single().Years.Last().ServedB));
        var war = w.Wars.Single(); var year = war.Years.Last();
        Assert.Contains("self of House Vega commanded House Corwin's fleets by service.", WarLedger.Say(w, war, year, "vega"));

        // a standard of Corwin's that comes home goes to Corwin's head, not to the commander of another house
        var standard = new Heirloom { Id = "std", Name = "the Corwin standard", Kind = WealthSystem.Standard, HolderId = "aurelian_head", OfHouseId = "aurelian", FromHouseId = "corwin" };
        w.Heirlooms.Add(standard);
        WealthSystem.StandardWon(w, "corwin", "aurelian", "empty", new List<Character> { me });
        Assert.Equal("corwin_head", standard.HolderId);

        // the house of the commander may pay the crews a donative, and give the fleet a captain of its own
        var caesar = new CaesarSystem(fleets, Offices); var vega = w.House("vega")!; vega.Treasury.Credits = 100;
        Assert.Null(caesar.DonativeRefusal(w, vega, cor));
        Assert.Equal("not a fleet of the house, nor one that one of the house commands by service", caesar.DonativeRefusal(w, vega, foe));
        var mate = Person(w, "mate", "vega", rank: 2); var ctx = new CommandContext(w, TestContent.Rng()); w.Pools["self"].Agents = 2;
        Assert.Null(new AppointCaptainCommand("self", "cor", "mate").Refusal(ctx));
        var stranger = Person(w, "stranger", "aurelian", rank: 2);
        Assert.Equal("Officer stranger is not of your house: the captain you give a fleet commanded by one of yours is one of yours", new AppointCaptainCommand("self", "cor", "stranger").Refusal(ctx));
        Assert.True(new CommandExecutor().TryExecute(new AppointCaptainCommand("self", "cor", "mate"), ctx)); FleetSystem.RefreshAdmiralFlags(w);
        Assert.Equal("mate", cor.CaptainId);   // a legate's own captain stays while the command does

        // commanding from the flag lends tactics to the fleets of the house served, not the commander's own
        me.Flags.Add(FleetSystem.FlagFlag);
        Assert.True(fleets.FlagFactor(w, "corwin") > 0); Assert.Equal(0.0, fleets.FlagFactor(w, "vega"));

        // and when the command ends the captain of the commander's house goes with them
        ServiceSystem.End(w, s, "resigned");
        Assert.Equal("", cor.AdmiralId); Assert.Equal("", cor.CaptainId);
        Assert.Contains(me.Life.Deeds, d => d.Text == "Gave up the command of The cor of House Corwin: resigned. Under them it won 1 battle.");
    }

    // ---------------- how it ends ----------------

    [Fact]
    public void The_owner_or_the_one_who_named_them_relieves_them_the_Senate_recalls_them_and_they_may_resign()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        var own = At(w, "own", "vega", "tessaly", 4); var lt = Person(w, "lt", "corwin");
        string? Relieve(string actor, string fleet = "own") => new RelieveServiceCommand(actor, fleet).Refusal(ctx);
        Assert.Equal("nobody commands it by service", Relieve("self"));
        Consul(w, "aurelian", "aurelian_head"); var cfleet = At(w, "cfleet", "aurelian", "prime", 3);
        ServiceSystem.Begin(w, own, lt, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        Assert.Equal("only the head of House Vega, whose fleet it is, or self, who named them, relieves them (the Senate does it by a recall)", Relieve("corwin_head"));
        Assert.Null(Relieve("self"));
        Assert.True(ex.TryExecute(new RelieveServiceCommand("self", "own"), ctx));
        Assert.Equal("relieved by self", w.PastService.Single().How); Assert.Equal("", own.AdmiralId);
        Assert.Equal(ServiceSystem.NamedRegard + ServiceSystem.RelievedRegard, w.GetRelationship("lt", "self")!.Disposition);

        // the one who named them may relieve them too, on a fleet that is not theirs
        var kin = Person(w, "kin", "vega"); ServiceSystem.Begin(w, cfleet, kin, w.Char("aurelian_head")!, ServiceSystem.Legate, ServiceSystem.ByConsul);
        Assert.Null(Relieve("aurelian_head", "cfleet"));
        // they resign, or the head calls them home; nobody else may
        string? Resign(string actor, string who) => new ResignServiceCommand(actor, who).Refusal(ctx);
        Assert.Equal("Officer lt commands nothing by service", Resign("self", "lt"));
        Assert.Equal("Officer kin is not of your house", Resign("corwin_head", "kin"));
        Assert.Null(Resign("kin", "kin")); Assert.Null(Resign("self", "kin"));
        Assert.True(ex.TryExecute(new ResignServiceCommand("self", "kin"), ctx));
        Assert.Equal("called home by self", w.PastService.Last().How); Assert.Equal("", cfleet.AdmiralId);
        Assert.Contains(kin.Life!.Deeds, d => d.Text == "Gave up the command of The cfleet of House Aurelian: called home by self.");

        // the Senate relieves by its recall: a general who comes home leaves the command by service with the rest
        ServiceSystem.Begin(w, cfleet, kin, w.Char("aurelian_head")!, ServiceSystem.Legate, ServiceSystem.ByConsul);
        w.Recalls.Add(new Recall { CharacterId = "kin", HouseId = "vega", Since = w.Turn, Deadline = w.Turn + 2 });
        new CaesarSystem(new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses), Offices).Obey(w, w.House("vega")!, ordered: false);
        Assert.Equal("recalled by the Senate", w.PastService.Last().How); Assert.Empty(w.Service); Assert.True(CaesarSystem.Barred(w, kin));
    }

    [Fact]
    public void A_commission_lapses_with_the_office_and_is_kept_when_the_office_passes_within_the_house()
    {
        var w = Rings(); var lt = Person(w, "lt", "corwin"); var fleet = At(w, "cf", "aurelian", "prime", 3);
        var heir = Person(w, "heir", "aurelian", tactics: 1, track: "law", rank: 3, age: 45);
        Consul(w, "aurelian", "aurelian_head");
        var s = ServiceSystem.Begin(w, fleet, lt, w.Char("aurelian_head")!, ServiceSystem.Legate, ServiceSystem.ByConsul);
        w.Turn++; ServiceSystem.Tick(w, Offices); Assert.Single(w.Service);   // the office stands
        // the consulship passes to another of the house: the legate is kept on
        w.Offices.Single().CharacterId = "heir";
        w.Turn++; ServiceSystem.Tick(w, Offices); Assert.Single(w.Service); Assert.Equal("heir", s.NamedById);
        // it passes out of the house: the commission lapses
        w.Offices.Clear(); Consul(w, "corwin", "corwin_head");
        w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Empty(w.Service); Assert.Equal("the commission lapsed: Officer heir is no longer consul", w.PastService.Single().How); Assert.Equal("", fleet.AdmiralId);

        // what else ends one: the fleet laid up, the two houses at war, the commander gone to another house, the commander dead
        var f2 = At(w, "f2", "corwin", "mid1", 3); War(w, "corwin", "aurelian"); var o1 = Person(w, "o1", "vega");
        ServiceSystem.Begin(w, f2, o1, w.Char("corwin_head")!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        f2.LaidUp = true; Assert.Equal("the fleet is laid up, and its crews paid off", ServiceSystem.Lapsed(w, Offices, w.Service.Single())); f2.LaidUp = false;
        var other = Person(w, "other", "vega"); var f3 = At(w, "f3", "corwin", "mid1", 2);
        ServiceSystem.Begin(w, f3, other, w.Char("corwin_head")!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        War(w, "vega", "corwin");
        Assert.Equal("House Vega and House Corwin are at war", ServiceSystem.Lapsed(w, Offices, ServiceSystem.Of(w, f3)!));
        other.Alive = false; Assert.Equal("Officer other is dead", ServiceSystem.Lapsed(w, Offices, ServiceSystem.Of(w, f3)!));
    }

    [Fact]
    public void An_office_fleet_does_not_keep_the_admiral_of_a_house_that_has_lost_the_office()
    {
        var w = Rings(); var old = Person(w, "old", "corwin"); var squadron = At(w, "sq", "aurelian", "prime", 4, admiral: "old", office: true);
        w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Equal("", squadron.AdmiralId);
        Assert.Contains(w.History, h => h.Text.Contains("Officer old of House Corwin lays down the command of The sq: it was House Corwin's by the office"));
        // but a legate named to it keeps it
        Admiral(w, "aurelian", "aurelian_head"); var lt = Person(w, "lt", "corwin");
        ServiceSystem.Begin(w, squadron, lt, w.Char("aurelian_head")!, ServiceSystem.Legate, ServiceSystem.ByAdmiral);
        w.Turn++; ServiceSystem.Tick(w, Offices); Assert.Equal("lt", squadron.AdmiralId);
    }

    [Fact]
    public void The_owner_shifts_a_commander_by_service_between_its_fleets_and_gives_nobody_else_of_another_house_a_flag()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Pools["self"].Agents = 3;
        var a = At(w, "a", "vega", "tessaly", 2); var b = At(w, "b", "vega", "tessaly", 5); var lt = Person(w, "lt", "corwin"); var stranger = Person(w, "stranger", "corwin");
        Assert.Equal("Officer stranger is not of your house: one of another house commands a fleet of yours only by service, as a legate or an officer lent", new AssignAdmiralCommand("self", "a", "stranger").Refusal(ctx));
        var s = ServiceSystem.Begin(w, a, lt, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        Assert.True(ex.TryExecute(new AssignAdmiralCommand("self", "b", "lt"), ctx));
        Assert.Equal("lt", b.AdmiralId); Assert.Equal("", a.AdmiralId); Assert.Equal("b", s.FleetId); Assert.Same(s, ServiceSystem.Of(w, b));
        // and the owner giving the fleet to one of its own relieves the commander by service
        var kin = Person(w, "kin", "vega");
        Assert.True(ex.TryExecute(new AssignAdmiralCommand("self", "b", "kin"), ctx));
        Assert.Empty(w.Service); Assert.Equal("relieved by self, who gave the command to Officer kin", w.PastService.Single().How);
    }

    // ---------------- other houses ----------------

    [Fact]
    public void Other_houses_name_legates_borrow_officers_and_seek_commands_and_ask_the_player_for_leave()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        // a consul of another house names a legate to a fleet with no commander, from a house it thinks well of
        Consul(w, "aurelian", "aurelian_head"); var af = At(w, "af", "aurelian", "prime", 3);
        var good = Person(w, "good", "corwin", tactics: 5); Regard(w, "aurelian_head", "corwin_head", 25);
        w.Turn++; ServiceSystem.Tick(w, Offices);
        var named = Assert.Single(w.Service); Assert.Equal(("af", "good", ServiceSystem.Legate), (named.FleetId, named.CommanderId, named.Kind));
        Assert.Equal(1, w.Counter(ServiceSystem.OthersKey));

        // a house at war with a fleet it cannot command asks the player's leave for one of the player's house
        War(w, "corwin", "aurelian"); var cf = At(w, "cf", "corwin", "mid1", 3);
        var kin = Person(w, "kin", "vega", tactics: 5); Regard(w, "self", "corwin_head", 15);
        w.Turn++; ServiceSystem.Tick(w, Offices);
        var ask = Assert.Single(w.ServiceAsks); Assert.Equal((ServiceSystem.AskLend, "cf", "kin"), (ask.Kind, ask.FleetId, ask.CharacterId));
        Assert.Equal("Mara of House Corwin, at war with House Aurelian, asks your leave to take Officer kin of your house to command The cf.", ServiceSystem.AskSays(w, ask));
        Assert.Contains(w.News, n => n.Text == ServiceSystem.AskSays(w, ask));
        Assert.Equal("it was put to the head of the house", new AnswerServiceCommand("kin", ask.Id, true, Offices).Refusal(ctx));
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask.Id, true, Offices), ctx));
        var lent = w.Service.Single(s => s.FleetId == "cf"); Assert.Equal(("kin", ServiceSystem.Lent, "self"), (lent.CommanderId, lent.Kind, lent.LeaveById));
        Assert.Equal(ServiceSystem.LeaveGivenRegard + ServiceSystem.BondRegard, w.GetRelationship("corwin_head", "self")!.Disposition);
        Assert.Empty(w.ServiceAsks);

        // asked again for another, and refused: the asker thinks the worse of it; and an ask not answered lapses at the year's end
        var cf2 = At(w, "cf2", "corwin", "mid1", 2); var kin2 = Person(w, "kin2", "vega", tactics: 4);
        w.Turn++; ServiceSystem.Tick(w, Offices); var ask2 = w.ServiceAsks.Single();
        int before = w.GetRelationship("corwin_head", "self")!.Disposition;
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask2.Id, false, Offices), ctx));
        Assert.Equal(before + ServiceSystem.RefusedRegard, w.GetRelationship("corwin_head", "self")!.Disposition);
        Assert.Equal("nothing is asked", new AnswerServiceCommand("self", ask2.Id, false, Offices).Refusal(ctx));

        // and somebody of another house who is not its head seeks a command for themselves, by the same rules the player is held to
        var w2 = Rings(); War(w2, "corwin", "aurelian"); var unled = At(w2, "unled", "corwin", "mid1", 3);
        var seeker = Person(w2, "seeker", "aurelian", tactics: 5);   // of the enemy: never taken
        var young = new Character { Id = "young", Name = "Officer young", HouseId = "vega", Age = 25, Alive = true, CareerTrack = "military", CareerRank = 2 }; young.Skills["tactics"] = 5;
        w2.Characters["young"] = young; w2.Houses["vega"].Members.Add("young");
        w2.Houses["third"] = new House { Id = "third", Name = "House Third" }; var thirdHead = Person(w2, "third_head", "third", tactics: 0, rank: 0, age: 60);
        var lt3 = Person(w2, "lt3", "third", tactics: 5); Regard(w2, "third_head", "corwin_head", 5); Regard(w2, "corwin_head", "third_head", 5);
        for (int t = 0; t < 12 && !w2.Service.Any(); t++) { w2.Turn++; ServiceSystem.Tick(w2, Offices); }
        var got = Assert.Single(w2.Service);
        Assert.Equal(("lt3", "unled", true, "third_head"), (got.CommanderId, got.FleetId, got.Sought, got.LeaveById));
        Assert.DoesNotContain(w2.Service, s => s.CommanderId == "seeker" || s.CommanderId == "young");
    }

    [Fact]
    public void Other_houses_offer_service_to_the_player_who_takes_them_on_or_declines()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        War(w, "vega", "aurelian"); var mine = At(w, "mine", "vega", "tessaly", 3);
        var lt = Person(w, "lt", "corwin", tactics: 5); Regard(w, "self", "corwin_head", 5); Regard(w, "corwin_head", "self", 5);
        for (int t = 0; t < 12 && !w.ServiceAsks.Any(); t++) { w.Turn++; ServiceSystem.Tick(w, Offices); }
        var ask = Assert.Single(w.ServiceAsks); Assert.Equal((ServiceSystem.AskOffer, "lt", "mine"), (ask.Kind, ask.CharacterId, ask.FleetId));
        Assert.Equal("Officer lt of House Corwin offers their service, to command The mine of House Vega.", ServiceSystem.AskSays(w, ask));
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask.Id, true, Offices), ctx));
        var s = Assert.Single(w.Service); Assert.Equal(("lt", "mine", ServiceSystem.Lent, true), (s.CommanderId, s.FleetId, s.Kind, s.Sought));
    }

    [Fact]
    public void Another_house_s_consul_asks_the_player_to_be_legate_and_the_player_accepts_or_declines()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        var me = w.Protagonist!; me.CareerTrack = "military"; me.CareerRank = 3; me.Skills["tactics"] = 6;
        Consul(w, "aurelian", "aurelian_head"); var af = At(w, "af", "aurelian", "prime", 4); Regard(w, "aurelian_head", "self", 25);
        w.Turn++; ServiceSystem.Tick(w, Offices);
        var ask = Assert.Single(w.ServiceAsks); Assert.Equal((ServiceSystem.AskLegate, "self", "af"), (ask.Kind, ask.CharacterId, ask.FleetId));
        Assert.Equal("Lucius, consul, would name you legate of The af.", ServiceSystem.AskSays(w, ask)); Assert.Empty(w.Service);
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask.Id, true, Offices), ctx));
        var s = Assert.Single(w.Service); Assert.Equal(("self", ServiceSystem.Legate, ServiceSystem.ByConsul, "aurelian_head"), (s.CommanderId, s.Kind, s.Authority, s.NamedById));
        Assert.Same(me, CaesarSystem.GeneralOf(w, w.House("vega")));
        // asked when the consulship has passed, the answer cannot be given
        var w2 = Rings(); var me2 = w2.Protagonist!; me2.CareerTrack = "military"; me2.CareerRank = 3; me2.Skills["tactics"] = 6;
        Consul(w2, "aurelian", "aurelian_head"); At(w2, "af", "aurelian", "prime", 4); Regard(w2, "aurelian_head", "self", 25);
        w2.Turn++; ServiceSystem.Tick(w2, Offices); var ask2 = w2.ServiceAsks.Single(); w2.Offices.Clear();
        Assert.Equal("it no longer stands: Lucius is no longer consul", new AnswerServiceCommand("self", ask2.Id, true, Offices).Refusal(new CommandContext(w2, TestContent.Rng())));
        Assert.Null(new AnswerServiceCommand("self", ask2.Id, false, Offices).Refusal(new CommandContext(w2, TestContent.Rng())));
    }

    // ---------------- the holes the review found ----------------

    [Fact]
    public void Nobody_is_given_the_fleet_of_a_house_their_own_is_at_war_with()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); w.Pools["self"].Agents = 3;
        // Corwin's head is consul and no Admiral sits, so the state fleet, which sails under Aurelian, is the consul's to give
        Consul(w, "corwin", "corwin_head"); var state = At(w, "state", "aurelian", "prime", 4, office: true);
        w.Houses["third"] = new House { Id = "third", Name = "House Third" }; Person(w, "third_head", "third", tactics: 0, rank: 0, age: 60);
        var lt3 = Person(w, "lt3", "third", tactics: 5, age: 50); Regard(w, "corwin_head", "third_head", 25); War(w, "third", "aurelian");
        for (int t = 0; t < 3; t++) { w.Turn++; ServiceSystem.Tick(w, Offices); }
        Assert.Empty(w.Service); Assert.Equal(0, w.Counter(ServiceSystem.CommandsKey)); Assert.Equal("", state.AdmiralId);
        Assert.False(ServiceSystem.Open(w, state, lt3, w.Char("corwin_head")!));
        // nor by the player as consul
        w.Offices.Clear(); Consul(w, "vega", "self");
        Assert.Equal("House Third is at war with House Aurelian, whose fleet it is", new NameLegateCommand("self", "state", "lt3", Offices).Refusal(ctx));
    }

    [Fact]
    public void An_offer_put_to_the_player_as_consul_is_taken_on_by_the_office_or_declined()
    {
        World Offered(out ServiceAsk ask)
        {
            var w = Rings(); Consul(w, "vega", "self"); At(w, "mine", "vega", "tessaly", 3); Person(w, "lt", "corwin", tactics: 5);
            for (int t = 0; t < 12 && !w.ServiceAsks.Any(); t++) { w.Turn++; ServiceSystem.Tick(w, Offices); }
            ask = Assert.Single(w.ServiceAsks); Assert.Equal((ServiceSystem.AskOffer, "lt", ServiceSystem.ByConsul), (ask.Kind, ask.CharacterId, ask.Authority));
            return w;
        }
        // at peace, the player takes the officer on as consul: a legate, not an officer lent for a war there is not
        var w = Offered(out var ask); var ex = new CommandExecutor();
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask.Id, true, Offices), new CommandContext(w, TestContent.Rng())));
        var s = Assert.Single(w.Service);
        Assert.Equal((ServiceSystem.Legate, ServiceSystem.ByConsul, "self", "", ""), (s.Kind, s.Authority, s.NamedById, s.EnemyId, s.LeaveById));
        w.Turn++; ServiceSystem.Tick(w, Offices); Assert.Single(w.Service);   // and it stands while the consulship does
        // or declines: the one who offered thinks the worse of it, and it is remembered
        var w2 = Offered(out var ask2);
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask2.Id, false, Offices), new CommandContext(w2, TestContent.Rng())));
        Assert.Empty(w2.Service); Assert.Empty(w2.ServiceAsks);
        Assert.Equal(ServiceSystem.RefusedRegard, w2.GetRelationship("lt", "self")!.Disposition);
        Assert.Contains(w2.History, h => h.Text.Contains("self does not take Officer lt of House Corwin into service."));
        Assert.True(ServiceSystem.Refused(w2, "lt", "lt"));
    }

    [Fact]
    public void Folding_a_fleet_commanded_by_service_carries_the_service_into_the_other_or_ends_it()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); War(w, "vega", "aurelian");
        var a = At(w, "a", "vega", "tessaly", 2); var b = At(w, "b", "vega", "tessaly", 3); var lt = Person(w, "lt", "corwin"); Person(w, "mate", "corwin");
        var s = ServiceSystem.Begin(w, a, lt, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian"); a.CaptainId = "mate";
        Assert.True(ex.TryExecute(new MergeFleetsCommand("self", "a", "b"), ctx));
        Assert.Equal("lt", b.AdmiralId); Assert.Same(s, ServiceSystem.Of(w, b)); Assert.Equal(("b", "The b"), (s.FleetId, s.FleetName)); Assert.Equal("mate", b.CaptainId);
        // the war ends, and the officer goes home from the fleet they now command, the captain of their house with them
        w.Wars.Clear(); w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Empty(w.Service); Assert.Equal("", b.AdmiralId); Assert.Equal("", b.CaptainId); Assert.Equal("House Vega's war is over, and the officer goes home", w.PastService.Single().How);
        // folded into a fleet with a commander of its own, the service ends there and then
        War(w, "vega", "aurelian"); var c2 = At(w, "c2", "vega", "tessaly", 2); var d = At(w, "d", "vega", "tessaly", 2, admiral: "self"); var lt2 = Person(w, "lt2", "corwin");
        ServiceSystem.Begin(w, c2, lt2, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        Assert.True(ex.TryExecute(new MergeFleetsCommand("self", "c2", "d"), ctx));
        Assert.Empty(w.Service); Assert.Equal("The c2 was folded into The d, which has a commander of its own", w.PastService.Last().How); Assert.Equal("self", d.AdmiralId);
    }

    [Fact]
    public void One_the_player_refused_or_left_unanswered_is_not_asked_for_again_and_the_asker_looks_elsewhere()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        War(w, "corwin", "aurelian"); var cf = At(w, "cf", "corwin", "mid1", 3);
        Person(w, "kin", "vega", tactics: 6); Regard(w, "self", "corwin_head", 15);
        w.Houses["third"] = new House { Id = "third", Name = "House Third" }; Person(w, "third_head", "third", tactics: 0, rank: 0, age: 60);
        Person(w, "lt3", "third", tactics: 4, age: 50); Person(w, "lt4", "third", tactics: 4, age: 50); Regard(w, "third_head", "corwin_head", 15);
        w.Turn++; ServiceSystem.Tick(w, Offices);
        var ask = Assert.Single(w.ServiceAsks); Assert.Equal((ServiceSystem.AskLend, "kin"), (ask.Kind, ask.CharacterId));
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", ask.Id, false, Offices), ctx));
        Assert.Equal(ServiceSystem.RefusedRegard, w.GetRelationship("corwin_head", "self")!.Disposition);
        // the next year Corwin does not ask again, and borrows from a house that will lend
        w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Empty(w.ServiceAsks); Assert.Equal("lt3", cf.AdmiralId); Assert.Equal("third_head", ServiceSystem.Of(w, cf)!.LeaveById);
        Assert.Equal(ServiceSystem.RefusedRegard, w.GetRelationship("corwin_head", "self")!.Disposition);   // no second refusal, no second cost
        // an ask left unanswered for the year is taken as a no
        var cf2 = At(w, "cf2", "corwin", "mid1", 2); Person(w, "kin2", "vega", tactics: 7);
        w.Turn++; ServiceSystem.Tick(w, Offices); Assert.Equal("kin2", w.ServiceAsks.Single().CharacterId);
        w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.True(ServiceSystem.Refused(w, "corwin_head", "kin2")); Assert.Empty(w.ServiceAsks); Assert.Equal("lt4", cf2.AdmiralId);
        // and after five years it may be asked again
        w.Turn += ServiceSystem.RefusedYears; Assert.False(ServiceSystem.Refused(w, "corwin_head", "kin2"));

        // a consul whose legacy the player's head declined names the next best instead of asking again
        var w2 = Rings(); var me = w2.Protagonist!; me.CareerTrack = "military"; me.CareerRank = 3; me.Skills["tactics"] = 6;
        Consul(w2, "aurelian", "aurelian_head"); var af = At(w2, "af", "aurelian", "prime", 4); Regard(w2, "aurelian_head", "self", 25);
        w2.Houses["corwin"].HeadId = "corwin_head"; Person(w2, "good", "corwin", tactics: 5, age: 50); Regard(w2, "aurelian_head", "corwin_head", 25);   // too old to seek a command of himself
        w2.Turn++; ServiceSystem.Tick(w2, Offices); var legate = w2.ServiceAsks.Single(); Assert.Equal(ServiceSystem.AskLegate, legate.Kind);
        Assert.True(ex.TryExecute(new AnswerServiceCommand("self", legate.Id, false, Offices), new CommandContext(w2, TestContent.Rng())));
        Assert.Equal(25 + ServiceSystem.RefusedRegard, w2.GetRelationship("aurelian_head", "self")!.Disposition);
        Assert.Contains(w2.History, h => h.Text.Contains("self declines to be Lucius's legate."));
        w2.Turn++; ServiceSystem.Tick(w2, Offices);
        Assert.Empty(w2.ServiceAsks); Assert.Equal("good", af.AdmiralId);
    }

    [Fact]
    public void Nobody_seeks_to_serve_under_one_they_think_too_ill_of()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); w.Pools["self"].Agents = 3;
        War(w, "corwin", "aurelian"); At(w, "cf", "corwin", "mid1", 3);
        Person(w, "kin", "vega", tactics: 5); Regard(w, "kin", "corwin_head", -80);
        Assert.Equal("Officer kin thinks too ill of Mara (-80) to serve under them: it takes -20 or better", new SeekCommandCommand("self", "kin", "corwin", Offices).Refusal(ctx));
        Regard(w, "kin", "corwin_head", -20); Assert.Null(new SeekCommandCommand("self", "kin", "corwin", Offices).Refusal(ctx));
        Consul(w, "aurelian", "aurelian_head"); At(w, "af", "aurelian", "prime", 3); Regard(w, "kin", "aurelian_head", -30);
        Assert.Equal("Officer kin thinks too ill of Lucius (-30) to serve under them: it takes -20 or better", new SeekCommandCommand("self", "kin", "aurelian_head", Offices).Refusal(ctx));
    }

    [Fact]
    public void Leave_to_lend_one_who_is_head_now_or_has_left_the_house_no_longer_stands()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng());
        War(w, "corwin", "aurelian"); At(w, "cf", "corwin", "mid1", 3); var kin = Person(w, "kin", "vega", tactics: 5); Regard(w, "self", "corwin_head", 15);
        w.Turn++; ServiceSystem.Tick(w, Offices); var ask = w.ServiceAsks.Single(); Assert.Equal("kin", ask.CharacterId);
        // the head dies at the year's end, and the heir asked for is head now
        w.Protagonist!.Alive = false; w.ProtagonistId = "kin";
        Assert.Equal("it no longer stands: Officer kin is the head of the house now, and a head is not lent", new AnswerServiceCommand("kin", ask.Id, true, Offices).Refusal(ctx));
        Assert.Null(new AnswerServiceCommand("kin", ask.Id, false, Offices).Refusal(ctx));
        // one who has married into another house since is not lent as one of yours
        w.ProtagonistId = "self"; w.Char("self")!.Alive = true; kin.HouseId = "aurelian";
        Assert.Equal("it no longer stands: Officer kin is no longer of your house", new AnswerServiceCommand("self", ask.Id, true, Offices).Refusal(ctx));
    }

    [Fact]
    public void A_relative_in_office_does_not_give_the_house_s_own_fleets_to_another_house()
    {
        var w = Rings(); Person(w, "bro", "vega", tactics: 1, track: "law", rank: 3, age: 45); Consul(w, "vega", "bro");
        Person(w, "myadm", "vega", tactics: 2); var mine = At(w, "mine", "vega", "tessaly", 4, admiral: "myadm");
        Person(w, "good", "corwin", tactics: 5); Regard(w, "bro", "corwin_head", 25);
        for (int t = 0; t < 6; t++) { w.Turn++; ServiceSystem.Tick(w, Offices); }   // years enough for the officer to seek a command too
        Assert.Equal("myadm", mine.AdmiralId); Assert.Empty(w.Service); Assert.Empty(w.ServiceAsks);
        Assert.Equal(0, w.GetRelationship("myadm", "bro")?.Disposition ?? 0);
        // on an office fleet of the house the relative fills a command that is empty, and puts out nobody the head put there
        Person(w, "keeper", "vega", tactics: 1); var state = At(w, "state", "vega", "prime", 3, admiral: "keeper", office: true);
        w.Turn++; ServiceSystem.Tick(w, Offices); Assert.Equal("keeper", state.AdmiralId); Assert.Empty(w.Service);
        state.AdmiralId = ""; w.Turn++; ServiceSystem.Tick(w, Offices);
        var s = Assert.Single(w.Service); Assert.Equal(("state", "good", "bro"), (s.FleetId, s.CommanderId, s.NamedById));
    }

    [Fact]
    public void Another_house_s_head_and_people_not_of_the_Reach_are_not_named_legate_or_borrowed()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); w.Pools["self"].Agents = 3;
        Consul(w, "vega", "self"); var own = At(w, "own", "vega", "tessaly", 4); w.Char("corwin_head")!.Skills["tactics"] = 5;
        Assert.Equal("Mara is the head of House Corwin, and a head of house stays at home", new NameLegateCommand("self", "own", "corwin_head", Offices).Refusal(ctx));
        Assert.DoesNotContain(ServiceSystem.Candidates(w, own, 50), c => c.Id is "corwin_head" or "aurelian_head");
        w.Characters["raider"] = new Character { Id = "raider", Name = "Raider", HouseId = "pirates", Age = 30, Alive = true, CareerTrack = "military", CareerRank = 3 };
        Assert.Equal("Raider is not of a house of the Reach", new NameLegateCommand("self", "own", "raider", Offices).Refusal(ctx));
        War(w, "vega", "aurelian");
        Assert.Equal("Raider is not of a house of the Reach", new BorrowOfficerCommand("self", "own", "raider").Refusal(ctx));
    }

    [Fact]
    public void A_relative_commanding_by_service_has_a_command_and_one_given_a_house_fleet_lays_the_other_down()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor(); w.Turn = 5;
        var ambitions = new AmbitionSystem(Content.Businesses, Content.Holdings, Content.Titles, Content.Careers);
        Person(w, "weak", "vega", tactics: 1); var own = At(w, "own", "vega", "tessaly", 3, admiral: "weak");
        var kin = Person(w, "kin", "vega", tactics: 6); kin.Ambition = "command_a_fleet"; Regard(w, "kin", "self", 30);
        War(w, "corwin", "aurelian"); var cf = At(w, "cf", "corwin", "mid1", 3);
        ServiceSystem.Begin(w, cf, kin, w.Char("corwin_head")!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian", "self");
        for (int t = 0; t < 2; t++) { w.Turn++; ambitions.Tick(w, ex, ctx); }
        Assert.Equal("", kin.Pursuit!.Request); Assert.Equal("Commands The cf, of House Corwin, by service.", kin.Pursuit.Note);
        // granted a fleet of the house all the same, the command by service is laid down first, and Corwin is told
        kin.Pursuit.Request = AmbitionSystem.Command; kin.Pursuit.RequestArg = "own"; kin.Pursuit.RequestSince = w.Turn;
        ambitions.Answer(w, w.Protagonist!, kin, true, ex, ctx);
        Assert.Equal("kin", own.AdmiralId); Assert.Equal("", cf.AdmiralId); Assert.Empty(w.Service);
        Assert.Equal("Officer kin was given the command of The own, of House Vega", w.PastService.Single().How);
        // and the same when the head gives them a flag of the house on the fleet's card
        var kin2 = Person(w, "kin2", "vega"); var cf2 = At(w, "cf2", "corwin", "mid1", 2); var b = At(w, "b", "vega", "tessaly", 2); w.Pools["self"].Agents = 2;
        ServiceSystem.Begin(w, cf2, kin2, w.Char("corwin_head")!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian", "self");
        Assert.True(ex.TryExecute(new AssignAdmiralCommand("self", "b", "kin2"), ctx));
        Assert.Empty(w.Service); Assert.Equal("Officer kin2 was given the command of The b, of House Vega", w.PastService.Last().How); Assert.Equal("", cf2.AdmiralId);
    }

    [Fact]
    public void A_captain_of_the_commander_s_house_is_not_sent_off_with_a_detachment()
    {
        var w = Rings(); var ctx = new CommandContext(w, TestContent.Rng()); War(w, "vega", "aurelian");
        var a = At(w, "a", "vega", "tessaly", 3); var lt = Person(w, "lt", "corwin"); Person(w, "mate", "corwin");
        ServiceSystem.Begin(w, a, lt, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian"); a.CaptainId = "mate";
        Assert.Equal("Officer mate, the captain, is of House Corwin and serves with the commander of that house: a detachment goes under a captain of your own house, or under none",
            new DetachHullsCommand("self", "a", 1, Content.Fleets, withCaptain: true).Refusal(ctx));
        Assert.Null(new DetachHullsCommand("self", "a", 1, Content.Fleets).Refusal(ctx));
        Person(w, "ours", "vega"); a.CaptainId = "ours"; Assert.Null(new DetachHullsCommand("self", "a", 1, Content.Fleets, withCaptain: true).Refusal(ctx));
    }

    [Fact]
    public void A_command_by_service_ends_for_the_reason_it_did_and_one_now_lawful_is_kept()
    {
        // the commander grows frail: the year's end lays the command down, and says why
        var w = Rings(); var own = At(w, "own", "vega", "tessaly", 3); var lt = Person(w, "lt", "corwin"); War(w, "vega", "aurelian"); w.Houses["corwin"].HeadId = "corwin_head";
        ServiceSystem.Begin(w, own, lt, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        lt.Health = 1; LifeSystem.Tick(w, Content); w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Equal("Officer lt is too frail for the sea", w.PastService.Single().How); Assert.Equal("", own.AdmiralId);
        // a consul's legate on the state fleet, when an Admiral of the same house is commissioned: the consul is consul still
        Consul(w, "aurelian", "aurelian_head"); var state = At(w, "state", "aurelian", "prime", 3, office: true); var lt2 = Person(w, "lt2", "corwin", age: 50);
        ServiceSystem.Begin(w, state, lt2, w.Char("aurelian_head")!, ServiceSystem.Legate, ServiceSystem.ByConsul);
        Person(w, "aurelian_adm", "aurelian", age: 50); Admiral(w, "aurelian", "aurelian_adm");
        w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Equal("the commission lapsed: an Admiral of the Reach holds the office fleets now, and The state is no longer the consul's", w.PastService.First(p => p.CommanderId == "lt2").How);
        // one who marries into the house whose fleet it is keeps the command, by right now and not by service
        var f3 = At(w, "f3", "vega", "tessaly", 2); var lt3 = Person(w, "lt3", "corwin", age: 50);
        ServiceSystem.Begin(w, f3, lt3, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian");
        lt3.HouseId = "vega"; w.Turn++; ServiceSystem.Tick(w, Offices);
        Assert.Equal("lt3", f3.AdmiralId); Assert.Null(ServiceSystem.Of(w, f3)); Assert.Equal("Officer lt3 is of House Vega now", w.PastService.Single(p => p.CommanderId == "lt3").How);
        Assert.Contains(lt3.Life!.Deeds, d => d.Text.StartsWith("Commanded The f3 of House Vega by service no longer, and kept the command"));
    }

    // ---------------- the Caesar road ----------------

    [Fact]
    public void A_legate_whose_crews_love_them_counts_them_on_the_march_and_the_crews_go_over_with_them()
    {
        var w = Rings(); var lt = Person(w, "lt", "corwin", tactics: 7);
        Admiral(w, "aurelian", "aurelian_head"); var squadron = At(w, "sq", "aurelian", "prime", 6, office: true);
        var s = ServiceSystem.Begin(w, squadron, lt, w.Char("aurelian_head")!, ServiceSystem.Legate, ServiceSystem.ByAdmiral);
        var corwin = w.House("corwin")!; int own = FleetSystem.StrengthOf(w, "corwin");
        Assert.Equal(own, ServiceSystem.MarchStrength(w, corwin, lt));   // not yet theirs
        squadron.DevotedTo = "lt"; squadron.Devotion = CaesarSystem.DevotedAt + 5;
        Assert.Equal(own + 6, ServiceSystem.MarchStrength(w, corwin, lt)); Assert.Equal(6, CaesarSystem.DevotedHulls(w, lt));
        Assert.Same(lt, CaesarSystem.GeneralOf(w, corwin));
        int hulls = ServiceSystem.GoOver(w, lt, corwin);
        Assert.Equal(6, hulls); Assert.Equal("corwin", squadron.OwnerId); Assert.False(squadron.Office); Assert.Equal("lt", squadron.AdmiralId);
        Assert.Empty(w.Service); Assert.Equal("Officer lt crossed the line, and the crews went with them", w.PastService.Single().How);
        Assert.Equal(1, w.Counter(ServiceSystem.WentOverKey));
    }

    // ---------------- the record ----------------

    [Fact]
    public void Commands_by_service_what_is_asked_and_the_war_account_survive_a_save()
    {
        var w = Rings(); var lt = Person(w, "lt", "corwin"); var own = At(w, "own", "vega", "tessaly", 4); War(w, "vega", "aurelian");
        var s = ServiceSystem.Begin(w, own, lt, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian"); s.Won = 2;
        var f2 = At(w, "f2", "vega", "tessaly", 2); var lt2 = Person(w, "lt2", "corwin");
        ServiceSystem.End(w, ServiceSystem.Begin(w, f2, lt2, w.Protagonist!, ServiceSystem.Lent, ServiceSystem.ByWar, "aurelian"), "resigned");
        w.ServiceAsks.Add(new ServiceAsk { Id = "ask_x", Kind = ServiceSystem.AskOffer, FleetId = "own", AskerId = "lt", CharacterId = "lt", Authority = ServiceSystem.ByWar, EnemyId = "aurelian", Turn = w.Turn });
        WarLedger.Served(w, w.Wars[0], "vega", "lt");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        var bs = Assert.Single(back.Service);
        Assert.Equal((s.FleetId, s.CommanderId, s.CommanderHouseId, s.OwnerId, s.Kind, s.Authority, s.NamedById, s.EnemyId, s.Won), (bs.FleetId, bs.CommanderId, bs.CommanderHouseId, bs.OwnerId, bs.Kind, bs.Authority, bs.NamedById, bs.EnemyId, bs.Won));
        Assert.Equal("resigned", back.PastService.Single().How);
        Assert.Equal("ask_x", back.ServiceAsks.Single().Id);
        Assert.Contains("lt", back.Wars[0].Years.Last().ServedA);
        Assert.Same(back.Service[0], ServiceSystem.Of(back, back.Fleets["own"]));
    }

    [Fact]
    public void A_game_with_nobody_steering_replays_its_commands_by_service_the_same()
    {
        List<string> Run()
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 1); var tc = new TurnController(w, Content);
            for (int t = 0; t < 20; t++) { foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); } tc.EndTurn(); }
            return w.PastService.Concat(w.Service).Select(s => $"{s.Since}:{s.CommanderId}:{s.FleetId}:{s.Kind}:{s.Ended}").ToList();
        }
        var a = Run(); Assert.Equal(a, Run());
        Assert.NotEmpty(a);   // in twenty years of the Old Lion somebody of another house commands by service
    }
}
