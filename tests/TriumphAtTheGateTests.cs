using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A triumph is had at the gate (docs/CAESAR.md, "The triumph"; docs/IDEAS_FROM_IMPERATOR.md, entry 1; docs/GAPS.md Q15): the general
/// voted one comes home and lays the command down for a year, or declines it and keeps the fleet; one who commands away from the capital stands
/// for office only by the Senate's leave to stand in absence; and anyone who has held a magistracy may move a triumph.</summary>
public class TriumphAtTheGateTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;
    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Protagonist!.Age = Math.Min(w.Protagonist.Age, 45);
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    /// <summary>A fleet of the house under this commander, its crews already theirs, lying at <paramref name="at"/> (else a world of the house that is not the capital).</summary>
    private static Fleet Legion(World w, House h, Character c, int hulls = 10, int devotion = 80, string? at = null)
    {
        at ??= w.HoldingsOf(h.Id).Where(x => x.IsPlanet && x.SystemId != w.Galaxy!.Capital).Select(x => x.SystemId).FirstOrDefault()
            ?? w.Galaxy!.Systems.Keys.Where(k => k != w.Galaxy.Capital).OrderBy(k => k, StringComparer.Ordinal).First();
        foreach (var other in w.Fleets.Values.Where(x => x.AdmiralId == c.Id)) other.AdmiralId = "";
        var f = new Fleet { Id = "legion_" + c.Id, Name = "The Tenth", OwnerId = h.Id, SystemId = at, HomeSystemId = at, Strength = hulls, AdmiralId = c.Id, DevotedTo = c.Id, Devotion = devotion };
        w.Fleets[f.Id] = f; return f;
    }
    private static House Rival(World w, House mine) => w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id)
        .OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).First();
    private static Character Officer(World w, House h, string id)
    {
        var c = new Character { Id = id, Name = "Officer " + id, HouseId = h.Id, Age = 30, Alive = true, Sex = "female", CareerTrack = "military", CareerRank = 2 };
        c.Skills["discipline"] = 3; c.Skills["tactics"] = 3; w.Characters[id] = c; h.Members.Add(id); w.Pools[id] = ActionPools.ForNpc(); return c;
    }
    private static void War(World w, string a, string b) => w.Wars.Add(new War { HouseA = a, HouseB = b, Since = w.Turn, ClosedThrough = w.Turn });

    // ---------------- the triumph at the gate ----------------

    [Fact]
    public void A_triumph_is_had_at_the_gate_the_fleet_waits_under_its_captain_and_the_command_is_taken_up_again()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me); var cap = Officer(w, mine, "flagcaptain"); f.CaptainId = cap.Id;
        var kin = w.LivingCharacters().First(c => c.HouseId == mine.Id && c.Id != me.Id && c.Age >= 18);
        TriumphCommand Answer(string who, string choice) => new(who, choice, tc.Caesar);
        Assert.Equal("the Senate has voted you no triumph to answer", Answer(me.Id, TriumphSystem.Home).Refusal(ctx));
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3;
        tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        // voted, not had: the count starts again, nothing is given, and nothing is laid down until the general answers
        Assert.Equal(0, CaesarSystem.Triumphs(w, me)); Assert.Equal(0, CaesarSystem.Laurels(w, me)); Assert.Equal(1, w.Counter("triumphs_voted"));
        Assert.NotNull(TriumphSystem.Waiting(w, me.Id)); Assert.Equal(me.Id, f.AdmiralId); Assert.False(w.HasFlag(CaesarSystem.TriumphFlag));
        Assert.Equal("come home for it, or keep the fleet", Answer(me.Id, "stay").Refusal(ctx));
        Assert.Equal("no one to answer", Answer(kin.Id, TriumphSystem.Home).Refusal(ctx));   // the player answers for themselves only
        Assert.Contains("lay the command down until the end of year", TriumphSystem.HomeSays(w, me).Replace("You lay", "lay"));
        int legit = mine.Legitimacy;
        Assert.True(tc.Executor.TryExecute(Answer(me.Id, TriumphSystem.Home), ctx));
        // had: the honours, and the command laid down for the year
        Assert.Equal(1, CaesarSystem.Triumphs(w, me)); Assert.True(mine.Legitimacy >= Math.Min(100, legit + 1)); Assert.True(w.HasFlag(CaesarSystem.TriumphFlag));
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "triumph"); Assert.Equal(1, w.Counter(TriumphSystem.HadKey));
        Assert.Equal("", f.AdmiralId); Assert.True(TriumphSystem.HeldFor(w, f, me.Id)); Assert.Equal(cap.Id, FleetSystem.Commander(w, f)?.Id);
        Assert.Equal(90, f.Devotion); Assert.NotEqual(me.Id, CaesarSystem.GeneralOf(w, mine)?.Id); Assert.Contains(TriumphSystem.HomeChar, me.Flags);
        Assert.Equal("the Senate has voted you no triumph to answer", Answer(me.Id, TriumphSystem.Keep).Refusal(ctx));
        // no other command meanwhile, and the refusal says why: it is the triumph, not the Senate's order
        Assert.Equal($"you are home for the triumph, and hold no command until the end of year {w.Turn + TriumphSystem.HomeYears}", new AssignAdmiralCommand(me.Id, f.Id, me.Id).Refusal(ctx));
        Assert.Equal($"you are home for the triumph, and hold no command until the end of year {w.Turn + TriumphSystem.HomeYears}", ServiceSystem.FreeRefusal(w, me));
        var t = w.TriumphVotes.Single(); Assert.Equal(TriumphSystem.Home, t.Choice); Assert.Equal(w.Turn + TriumphSystem.HomeYears, t.HomeUntil);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); var bt = back.TriumphVotes.Single();
        Assert.Equal(t.HomeUntil, bt.HomeUntil); Assert.Equal(TriumphSystem.Home, bt.Choice); Assert.Equal(new[] { f.Id }, bt.FleetIds); Assert.True(TriumphSystem.Held(back, back.Fleets[f.Id]));

        // the year passes: the crews wait for them, their regard does not fade, and no relative steps into the empty flag
        tc.EndTurn(); tc.BeginTurn();
        Assert.Equal("", f.AdmiralId); Assert.Equal(me.Id, f.DevotedTo); Assert.Equal(90, f.Devotion); Assert.True(w.HasFlag(TriumphSystem.HomeFlag));
        tc.EndTurn(); tc.BeginTurn();
        // and the year at home is out: the command is taken up again
        Assert.Equal(me.Id, f.AdmiralId); Assert.False(CaesarSystem.Barred(w, me)); Assert.True(t.Done); Assert.Equal(TriumphSystem.TakenUp, t.Ended);
        Assert.Equal(1, w.Counter(TriumphSystem.TakenUpKey)); Assert.DoesNotContain(TriumphSystem.HomeChar, me.Flags); Assert.Equal(me.Id, CaesarSystem.GeneralOf(w, mine)?.Id);
    }

    [Fact]
    public void Declining_keeps_the_fleet_gives_nothing_and_is_no_cause_and_an_answer_left_for_the_year_is_a_refusal()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me);
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        Assert.True(tc.Executor.TryExecute(new TriumphCommand(me.Id, TriumphSystem.Keep, tc.Caesar), ctx));
        Assert.Equal(me.Id, f.AdmiralId); Assert.Equal(0, CaesarSystem.Triumphs(w, me)); Assert.Equal(0, CaesarSystem.Laurels(w, me));
        Assert.Null(CaesarSystem.Grievance(w, mine)); Assert.True(w.HasFlag(TriumphSystem.DeclinedFlag)); Assert.Equal(1, w.Counter(TriumphSystem.DeclinedKey));
        Assert.Contains(me.Life!.Deeds, d => d.Kind == "triumph_declined"); Assert.Equal(TriumphSystem.Keep, w.TriumphVotes.Single().Choice);
        Assert.Contains("you have 0 of the 3 victories", tc.Caesar.TriumphRefusal(w, mine));   // the victories that earned it have had their answer

        // voted again, and left unanswered: the year it was voted in ends with the answer still wanted, and the next it lapses
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        tc.EndTurn(); tc.BeginTurn(); Assert.NotNull(TriumphSystem.Waiting(w, me.Id)); Assert.True(w.HasFlag(TriumphSystem.AskedFlag));
        tc.EndTurn(); tc.BeginTurn();
        Assert.Null(TriumphSystem.Waiting(w, me.Id)); Assert.Equal(1, w.Counter(TriumphSystem.LapsedKey)); Assert.Equal(me.Id, f.AdmiralId); Assert.Equal(0, CaesarSystem.Triumphs(w, me));
        Assert.Equal(TriumphSystem.Lapsed, w.TriumphVotes.Last().Choice);
    }

    [Fact]
    public void Other_houses_generals_choose_by_their_nature_and_the_war_they_are_in()
    {
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); var g = Economy.HeadOf(w, rival)!;
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == rival.Id)) x.AdmiralId = "";   // the head is the house's only commander
        var f = Legion(w, rival, g);
        // keeps the Senate's custom, and at peace: comes home
        g.NatureTraits.Clear(); g.NatureTraits.Add("Loyal"); g.Ambition = "";
        Assert.False(TriumphSystem.WouldKeep(w, Offices, g)); Assert.Contains(TriumphSystem.Weigh(w, Offices, g), x => x.why == "they keep the Senate's custom");
        // wants the throne, will not let go, and the fleet is at war: keeps it
        g.NatureTraits.Clear(); g.NatureTraits.Add("Ruthless"); g.Ambition = "seize_the_throne"; War(w, rival.Id, mine.Id);
        var why = TriumphSystem.Weigh(w, Offices, g);
        Assert.Contains(why, x => x.why == "the fleet is at war" && x.by == 2); Assert.Contains(why, x => x.why == "what is let go may not come back"); Assert.True(TriumphSystem.WouldKeep(w, Offices, g));
        w.WorldCounters[CaesarSystem.LaurelKey(g.Id)] = 3; tc.Caesar.TriumphVoted(w, rival, null, passed: true);
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);   // another house's general answers in the year it is voted
        var kept = w.TriumphVotes.Single(); Assert.Equal(TriumphSystem.Keep, kept.Choice); Assert.Contains("at war", kept.Why); Assert.Equal(g.Id, f.AdmiralId); Assert.Equal(0, CaesarSystem.Triumphs(w, g));

        // the same general at peace, and loyal: home for it, and nobody of the house is put in the empty flag while it waits
        w.Wars.Clear(); g.NatureTraits.Clear(); g.NatureTraits.Add("Loyal"); g.Ambition = "";
        w.WorldCounters[CaesarSystem.LaurelKey(g.Id)] = 3; tc.Caesar.TriumphVoted(w, rival, null, passed: true); TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        var home = w.TriumphVotes.Last(); Assert.Equal(TriumphSystem.Home, home.Choice); Assert.Contains("custom", home.Why);
        Assert.Equal(1, CaesarSystem.Triumphs(w, g)); Assert.Equal("", f.AdmiralId); Assert.True(TriumphSystem.Held(w, f));
        Assert.Equal($"{g.Name} is home for the triumph, and holds no command until the end of year {home.HomeUntil}", ServiceSystem.FreeRefusal(w, g));
        tc.EndTurn(); tc.BeginTurn(); Assert.Equal("", f.AdmiralId);
        tc.EndTurn(); tc.BeginTurn(); Assert.Equal(g.Alive ? g.Id : "", f.AdmiralId);

        // one of the player's own house chooses for themselves, by the same rule, and the head is told
        var (tc2, w2, me2, mine2, _) = Start(); var kin = w2.LivingCharacters().First(c => c.HouseId == mine2.Id && c.Id != me2.Id && c.Age >= 18);
        foreach (var x in w2.Fleets.Values.Where(x => x.OwnerId == mine2.Id || x.AdmiralId == me2.Id)) x.AdmiralId = "";
        var kf = Legion(w2, mine2, kin); kin.NatureTraits.Clear(); kin.NatureTraits.Add("Paranoid"); kin.Ambition = "command_a_fleet";
        w2.WorldCounters[CaesarSystem.LaurelKey(kin.Id)] = 3; tc2.Caesar.TriumphVoted(w2, mine2, null, passed: true); TriumphSystem.Tick(w2, tc2.Senate, tc2.Caesar);
        Assert.Equal(TriumphSystem.Keep, w2.TriumphVotes.Single().Choice); Assert.Equal(kin.Id, kf.AdmiralId); Assert.True(w2.HasFlag(TriumphSystem.KinDeclinedFlag));
        Assert.Contains(w2.News, n => n.Text.Contains("declines the triumph"));
    }

    [Fact]
    public void A_command_by_service_waits_for_its_general_and_lapses_meanwhile_only_by_its_own_rules()
    {
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); var consul = Economy.HeadOf(w, rival)!;
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id || x.AdmiralId == me.Id)) x.AdmiralId = "";
        var at = w.HoldingsOf(rival.Id).Where(x => x.IsPlanet && x.SystemId != w.Galaxy!.Capital).Select(x => x.SystemId).FirstOrDefault() ?? w.Galaxy!.Systems.Keys.First(k => k != w.Galaxy.Capital);
        var f = new Fleet { Id = "lent_fleet", Name = "The Borrowed", OwnerId = rival.Id, SystemId = at, Strength = 6 }; w.Fleets[f.Id] = f;
        w.Offices.RemoveAll(o => o.OfficeId == "consul_senior"); w.Offices.Add(new OfficeHolding { OfficeId = "consul_senior", HouseId = rival.Id, CharacterId = consul.Id, SinceTurn = w.Turn });
        var s = ServiceSystem.Begin(w, f, me, consul, ServiceSystem.Legate, ServiceSystem.ByConsul);
        Assert.Equal(me.Id, CaesarSystem.GeneralOf(w, mine)?.Id);   // a legate is a general like any other
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        Assert.True(tc.Executor.TryExecute(new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar), ctx));
        Assert.Equal("", f.AdmiralId); Assert.Null(ServiceSystem.Lapsed(w, Offices, s));   // home is not one of the service's own ends
        ServiceSystem.Tick(w, Offices); Assert.Contains(s, w.Service);
        Assert.False(ServiceSystem.Open(w, f, w.LivingCharacters().First(c => c.HouseId != rival.Id && c.HouseId != mine.Id && !World.Outsider(c.HouseId)), consul));   // nobody is named to it meanwhile
        // the year out, the legate takes it up again
        var t = w.TriumphVotes.Single(); w.Turn = t.HomeUntil; TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal(me.Id, f.AdmiralId); Assert.Contains(s, w.Service); Assert.Equal(TriumphSystem.TakenUp, t.Ended);

        // again, and this time the consul's term ends while the legate is home: the commission lapses by its own rule, and there is nothing to take up
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        Assert.True(tc.Executor.TryExecute(new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar), ctx));
        w.Offices.RemoveAll(o => o.OfficeId == "consul_senior"); ServiceSystem.Tick(w, Offices);
        Assert.DoesNotContain(s, w.Service); Assert.Contains("the commission lapsed", s.How);
        var t2 = w.TriumphVotes.Last(); w.Turn = t2.HomeUntil; TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal("", f.AdmiralId); Assert.Equal("the command by service has lapsed meanwhile", t2.Ended); Assert.Equal(1, w.Counter(TriumphSystem.NotTakenUpKey));
    }

    [Fact]
    public void A_fleet_given_to_another_while_the_general_is_home_is_theirs_now()
    {
        var (tc, w, me, mine, ctx) = Start(); var f = Legion(w, mine, me); var other = Officer(w, mine, "stand_in");
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        Assert.True(tc.Executor.TryExecute(new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar), ctx));
        w.PoolsFor(me.Id).Agents = 3; Assert.True(tc.Executor.TryExecute(new AssignAdmiralCommand(me.Id, f.Id, other.Id), ctx));   // the head may give it to another
        Assert.False(TriumphSystem.Held(w, f));
        var t = w.TriumphVotes.Single(); w.Turn = t.HomeUntil; TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal(other.Id, f.AdmiralId); Assert.Equal($"{f.Name} was given to {other.Name} meanwhile", t.Ended); Assert.False(CaesarSystem.Barred(w, me));
    }

    // ---------------- standing in absence ----------------

    [Fact]
    public void One_who_commands_away_from_the_capital_may_not_stand_and_every_refusal_says_why()
    {
        var (tc, w, me, mine, ctx) = Start(); string cap = w.Galaxy!.Capital;
        me.Cursus.Clear(); me.CareerTrack = "military"; me.CareerRank = 2; me.Age = 30; w.Candidacies.RemoveAll(c => c.CharacterId == me.Id);
        foreach (var x in w.Fleets.Values.Where(x => x.AdmiralId == me.Id)) x.AdmiralId = "";
        Assert.Null(new LadderCommand(me.Id, LadderCommand.Stand, me.Id, LadderSystem.Quaestor, Offices).Refusal(ctx) is { } r0 && !r0.Contains("influence") ? r0 : null);
        var f = Legion(w, mine, me);
        Assert.Equal($"they command {f.Name} away from the capital: bring it to the capital, give the command to another, or ask the Senate for leave to stand in absence", LadderSystem.Bar(w, Offices, me, Offices.Rung(LadderSystem.Quaestor)));
        Assert.Equal($"you command {f.Name} away from the capital: bring it to the capital, give the command to another, or ask the Senate for leave to stand in absence", TriumphSystem.AwaySays(w, me));
        Assert.Contains("away from the capital", new LadderCommand(me.Id, LadderCommand.Stand, me.Id, LadderSystem.Quaestor, Offices).Refusal(ctx));
        Assert.Null(LadderSystem.Next(w, Offices, me));
        // under way, or beyond the Reach, is away too; lying at the capital is not
        f.SystemId = cap; Assert.Null(TriumphSystem.AwayCommand(w, me)); Assert.Null(LadderSystem.Bar(w, Offices, me, Offices.Rung(LadderSystem.Quaestor)));
        f.DestinationId = "elsewhere"; Assert.Same(f, TriumphSystem.AwayCommand(w, me)); f.DestinationId = "";
        f.Away = "vashti"; Assert.Same(f, TriumphSystem.AwayCommand(w, me)); f.Away = ""; f.SystemId = f.HomeSystemId;

        // the consulship: the only one of the house who could stand commands away
        me.Age = 45; me.Cursus.AddRange(new[] { LadderSystem.Quaestor, LadderSystem.Praetor }); me.CareerRank = 3;
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id)) c.Cursus.Clear();
        w.WorldCounters.Remove("consul_term:" + mine.Id); w.Offices.RemoveAll(o => o.HouseId == mine.Id && Offices.Def(o.OfficeId)?.Elected == true);
        var consul = ElectionSystem.ConsulOffices(Offices).First();
        Assert.Null(OfficeSystem.Candidate(w, consul, mine.Id)); Assert.Equal(me.Id, OfficeSystem.Candidate(w, consul, mine.Id, evenAway: true)?.Id);
        Assert.False(ElectionSystem.Eligible(w, Offices, mine.Id));
        Assert.Equal($"you command {f.Name} away from the capital: bring it to the capital, give the command to another, or ask the Senate for leave to stand in absence", new StandForConsulCommand(me.Id, Offices).Refusal(ctx));
        Assert.Equal(me.Id, TriumphSystem.ConsulAwayOne(w, Offices, mine.Id)?.Id);
        // home for a triumph is home: the general may stand in person
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        Assert.True(tc.Executor.TryExecute(new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar), ctx));
        Assert.True(ElectionSystem.Eligible(w, Offices, mine.Id)); Assert.Null(TriumphSystem.AwayRefusal(w, me));
    }

    [Fact]
    public void Leave_to_stand_in_absence_is_a_motion_like_the_others_and_lasts_for_one_election()
    {
        var (tc, w, me, mine, ctx) = Start(); w.PoolsFor(me.Id).Influence = 20;
        me.Cursus.Clear(); me.CareerTrack = "military"; me.CareerRank = 2; me.Age = 30; w.Candidacies.RemoveAll(c => c.CharacterId == me.Id);
        foreach (var x in w.Fleets.Values.Where(x => x.AdmiralId == me.Id)) x.AdmiralId = "";
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id)) x.AdmiralId = "";
        w.Motions.Clear();
        ProposeMotionCommand Leave(string target) => new(me.Id, TriumphSystem.LeaveKind, target, tc.Senate);
        Assert.Equal("nobody of your house commands a fleet away from the capital", Leave(mine.Id).Refusal(ctx));
        var f = Legion(w, mine, me);
        Assert.Null(Leave(mine.Id).Refusal(ctx));
        Assert.Contains("stand for office at the next election without coming home", Praetoria.Core.Presentation.CourtProjection.MotionDoes(tc.Senate.Motions.Def(TriumphSystem.LeaveKind)!));
        Assert.True(tc.Executor.TryExecute(Leave(mine.Id), ctx)); var m = w.Motions.Single(x => x.Open && x.Kind == TriumphSystem.LeaveKind);
        Assert.Equal("you already have a motion before the Senate", Leave(mine.Id).Refusal(ctx));
        // the houses lean by what they make of it; the house itself is for it
        var other = Rival(w, mine);
        Assert.Equal(4, TriumphSystem.LeaveLean(w, mine, mine, 0, Offices)); Assert.True(TriumphSystem.LeaveLean(w, other, mine, 30, Offices) > TriumphSystem.LeaveLean(w, other, mine, -30, Offices));
        // carried: the leave stands until the next election has been held
        foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        w.Turn++; tc.Senate.Tick(w); if (!m.Resolved) { w.Turn++; tc.Senate.Tick(w); }   // the Speaker may hold a motion over once
        Assert.True(m.Passed);
        Assert.True(TriumphSystem.HasLeave(w, mine.Id)); Assert.Equal(1, w.Counter(TriumphSystem.LeaveGrantedKey));
        Assert.Null(TriumphSystem.AwayRefusal(w, me)); Assert.Null(LadderSystem.Bar(w, Offices, me, Offices.Rung(LadderSystem.Quaestor)));
        Assert.Equal("the chamber heard that from you lately", Leave(mine.Id).Refusal(ctx));
        Assert.Equal("your house has leave already, until the next election", TriumphSystem.LeaveRefusal(w, mine, mine.Id));
        Assert.Equal($"{mine.Name} has leave already, until the next election", TriumphSystem.LeaveRefusal(w, mine, other.Id));
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.True(TriumphSystem.HasLeave(back, mine.Id));
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar); Assert.True(w.HasFlag(TriumphSystem.LeaveFlag));
        // an election is held: it is spent
        w.WorldFlags.Add("election_held"); TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.False(TriumphSystem.HasLeave(w, mine.Id)); Assert.Equal(1, w.Counter(TriumphSystem.LeaveSpentKey)); Assert.NotNull(TriumphSystem.AwayRefusal(w, me));
        // thrown out, nothing is given, and the house is told
        TriumphSystem.LeaveVoted(w, mine, passed: false);
        Assert.False(TriumphSystem.HasLeave(w, mine.Id)); Assert.Equal(1, w.Counter(TriumphSystem.LeaveRefusedKey)); Assert.Contains(w.News, n => n.Text.Contains("will not give your house leave"));
        Assert.Equal("no such house", TriumphSystem.LeaveRefusal(w, null));
    }

    [Fact]
    public void A_name_down_for_the_ladder_is_taken_down_with_the_reason_when_its_holder_sails_away()
    {
        var (tc, w, me, mine, ctx) = Start(); w.PoolsFor(me.Id).Influence = 20;
        me.Cursus.Clear(); me.CareerTrack = "military"; me.CareerRank = 2; me.Age = 30; w.Candidacies.RemoveAll(c => c.CharacterId == me.Id);
        foreach (var x in w.Fleets.Values.Where(x => x.AdmiralId == me.Id)) x.AdmiralId = "";
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Stand, me.Id, LadderSystem.Quaestor, Offices), ctx));
        Legion(w, mine, me);
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Contains(w.News, n => n.Text.StartsWith("Your name is taken down for quaestor: you command The Tenth away from the capital")); Assert.Equal(1, w.Counter(TriumphSystem.NamesDroppedKey));
        LadderSystem.Tick(w, Offices, Content.Policies); Assert.DoesNotContain(w.Candidacies, c => c.CharacterId == me.Id);
    }

    [Fact]
    public void Other_houses_ask_leave_for_one_who_would_stand_and_commands_away_or_have_them_come_home_to_stand()
    {
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); var head = Economy.HeadOf(w, rival)!;
        while (LadderSystem.CountIn(w, Offices) != 2 || ElectionSystem.CountIn(w) is 1 or 2) w.Turn++;   // the ladder's count two years off, not the consuls'
        w.Motions.Clear(); w.Emergency = null; w.Wars.Clear();
        foreach (var x in w.Fleets.Values) x.AdmiralId = "";   // nobody else in the Reach commands away from the capital
        var star = Officer(w, rival, "rising_star"); star.Skills["oratory"] = 9; star.Skills["charisma"] = 9; star.Age = 28;   // the best the house has for quaestor
        Assert.Null(LadderSystem.Bar(w, Offices, star, Offices.Rung(LadderSystem.Quaestor)));
        var f = Legion(w, rival, star, devotion: 30);
        Assert.Null(TriumphSystem.WantsLeave(w, tc.Senate, rival, ladder: true, consul: false));   // a commander with no name is not worth the Senate's time
        foreach (var x in w.LivingCharacters().Where(x => x.HouseId == rival.Id && x.Id != star.Id)) { x.CareerTrack = ""; x.CareerRank = 0; }   // and the only one it has who could stand
        Assert.Contains(star, TriumphSystem.AwayCandidates(w, tc.Senate, rival, ladder: true, consul: false));
        w.WorldCounters[CaesarSystem.VictoryKey(star.Id)] = 3; Assert.True(CaesarSystem.Sway(w, star) >= TriumphSystem.LeaveSway);   // the house's general, with a name
        Assert.Equal(star.Id, TriumphSystem.WantsLeave(w, tc.Senate, rival, ladder: true, consul: false)?.Id);
        head.Cursus.Clear(); head.Cursus.Add(LadderSystem.Quaestor);   // one who has held a magistracy may move it, whatever the house's door
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        var m = w.Motions.SingleOrDefault(x => x.Open && x.Kind == TriumphSystem.LeaveKind);
        Assert.NotNull(m); Assert.Equal(rival.Id, m!.TargetId); Assert.Equal(1, w.Counter(TriumphSystem.LeaveMovedKey));
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar); Assert.Single(w.Motions, x => x.Open && x.Kind == TriumphSystem.LeaveKind);   // one at a time

        // the year before the count, with the leave refused: at war, the general stays with the fleet and does not stand
        w.Motions.Clear(); w.Turn++; Assert.Equal(1, LadderSystem.CountIn(w, Offices)); War(w, rival.Id, mine.Id);
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal(star.Id, f.AdmiralId); Assert.False(TriumphSystem.StandingHome(w, star));
        // at peace, the general comes home to stand, and is given no other command until the count is past
        w.Wars.Clear(); w.Motions.Clear(); TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal("", f.AdmiralId); Assert.True(TriumphSystem.StandingHome(w, star)); Assert.Equal(1, w.Counter(TriumphSystem.CameHomeKey));
        Assert.Null(LadderSystem.Bar(w, Offices, star, Offices.Rung(LadderSystem.Quaestor)));
        Assert.Equal($"{star.Name} has come home to stand for office", ServiceSystem.FreeRefusal(w, star));
        Assert.True(TriumphSystem.HeldFor(w, f, star.Id)); Assert.Equal(30, f.Devotion);   // the fleet waits for them, as for a triumph
        // the count is past: the command is taken up again, and the record of it goes
        w.Turn++; TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal(star.Id, f.AdmiralId); Assert.Equal(1, w.Counter(TriumphSystem.StoodTakenUpKey)); Assert.DoesNotContain(w.TriumphVotes, t => t.Choice == TriumphSystem.Stand);
        w.Turn++; Assert.False(TriumphSystem.StandingHome(w, star));
    }

    // ---------------- who may move a triumph ----------------

    [Fact]
    public void Anyone_who_has_held_a_magistracy_may_move_a_triumph_and_leave_whatever_the_house_door()
    {
        var (tc, w, me, mine, ctx) = Start(); w.PoolsFor(me.Id).Influence = 20; w.Motions.Clear();
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id)) x.AdmiralId = "";
        var f = Legion(w, mine, me);
        // a house with no voice in the Senate: no land of rank, no Judge at its head, not enrolled, not great in arms
        mine.Title = "citizen"; me.CareerTrack = "military"; mine.Enrolled = false; mine.Orders.Clear(); me.Cursus.Clear(); w.Magistrates.RemoveAll(x => x.CharacterId == me.Id);
        Assert.False(tc.Senate.MayPropose(w, mine.Id));
        w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3;
        var triumph = new ProposeMotionCommand(me.Id, TriumphSystem.TriumphKind, mine.Id, tc.Senate);
        Assert.Equal("the house has its votes but no voice in the Senate, and you have held no magistracy: one who has held a magistracy may move it whatever the house's door", triumph.Refusal(ctx));
        Assert.Equal("the house has its votes but no voice in the Senate: it may not move a motion", new ProposeMotionCommand(me.Id, "censure", Rival(w, mine).Id, tc.Senate).Refusal(ctx));
        me.Cursus.Add(LadderSystem.Quaestor);
        Assert.True(TriumphSystem.HeldMagistracy(w, me));
        Assert.Null(triumph.Refusal(ctx)); Assert.Null(new ProposeMotionCommand(me.Id, TriumphSystem.LeaveKind, mine.Id, tc.Senate).Refusal(ctx));
        Assert.Equal("the house has its votes but no voice in the Senate: it may not move a motion", new ProposeMotionCommand(me.Id, "censure", Rival(w, mine).Id, tc.Senate).Refusal(ctx));   // only the generals' business
        Assert.True(tc.Executor.TryExecute(triumph, ctx)); Assert.Contains(w.Motions, m => m.Open && m.Kind == TriumphSystem.TriumphKind && m.ProposerHouseId == mine.Id);
        // under a throne the new door is shut, and the refusal says the throne's rule, not the Republic's
        w.Era = World.EraImperium; string throne = $"the Senate hears a {Praetoria.Core.Progression.Title.StyleOf(Praetoria.Core.Progression.Title.Baron, w.Era)}, or a Judge";
        Assert.False(TriumphSystem.MayMove(w, me, tc.Senate.Motions.Def(TriumphSystem.LeaveKind)));
        Assert.Equal(throne, new ProposeMotionCommand(me.Id, TriumphSystem.LeaveKind, mine.Id, tc.Senate).Refusal(ctx));
        me.Cursus.Clear(); Assert.Equal(throne, new ProposeMotionCommand(me.Id, TriumphSystem.TriumphKind, mine.Id, tc.Senate).Refusal(ctx));
        w.Era = World.EraRepublic;

        // the other houses move by the same rule: a house with no door whose head has held a magistracy asks a triumph for its own general
        var (tc2, w2, me2, mine2, _) = Start(); var rival = Rival(w2, mine2); var rh = Economy.HeadOf(w2, rival)!;
        foreach (var x in w2.Fleets.Values.Where(x => x.OwnerId == rival.Id)) x.AdmiralId = "";
        Legion(w2, rival, rh);
        rival.Title = "citizen"; rh.CareerTrack = "military"; rival.Enrolled = false; rival.Orders.Clear(); rh.Cursus.Clear(); w2.Magistrates.RemoveAll(x => x.CharacterId == rh.Id);
        foreach (var h in w2.Houses.Values) foreach (var c in w2.LivingCharacters().Where(c => c.HouseId == h.Id)) w2.WorldCounters[CaesarSystem.LaurelKey(c.Id)] = 0;
        w2.WorldCounters[CaesarSystem.LaurelKey(rh.Id)] = 3; w2.Motions.Clear();
        foreach (var o in w2.Houses.Values.Where(o => o.Id != rival.Id && Economy.HeadOf(w2, o) != null)) w2.Relationship(Economy.HeadOf(w2, o)!.Id, rh.Id).Disposition = 0;   // no friend to move it for them
        Assert.False(tc2.Senate.MayPropose(w2, rival.Id));
        w2.Turn++; tc2.Senate.Tick(w2); Assert.DoesNotContain(w2.Motions, m => m.Kind == TriumphSystem.TriumphKind && m.ProposerHouseId == rival.Id);
        rh.Cursus.Add(LadderSystem.Quaestor); w2.Motions.Clear();
        w2.Turn++; tc2.Senate.Tick(w2); Assert.Contains(w2.Motions, m => m.Kind == TriumphSystem.TriumphKind && m.ProposerHouseId == rival.Id && m.TargetId == rival.Id);
    }

    // ---------------- mended after the review ----------------

    [Fact]
    public void A_general_under_recall_does_not_get_out_of_it_by_coming_home_for_a_triumph_or_to_stand()
    {
        // another house's general is voted a triumph and recalled at the same year's end: the triumph is void, and the recall is answered
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); var g = Economy.HeadOf(w, rival)!; w.Wars.Clear();
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == rival.Id || ServiceSystem.CommandedBy(w, x, rival.Id))) x.AdmiralId = "";
        var f = Legion(w, rival, g, devotion: 90); g.NatureTraits.Clear(); g.NatureTraits.Add("Loyal"); g.Ambition = "";
        Assert.False(TriumphSystem.WouldKeep(w, Offices, g));   // free of the recall, they would come home for it
        w.WorldCounters[CaesarSystem.LaurelKey(g.Id)] = 3; tc.Caesar.TriumphVoted(w, rival, null, passed: true); tc.Caesar.RecallVoted(w, rival, null, passed: true);
        Assert.Contains(w.Recalls, r => r.CharacterId == g.Id);
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        var t = w.TriumphVotes.Single(); Assert.Equal(TriumphSystem.Void, t.Choice); Assert.True(t.Done); Assert.Equal(1, w.Counter(TriumphSystem.UnderRecallKey));
        Assert.Equal(g.Id, f.AdmiralId); Assert.False(TriumphSystem.Held(w, f)); Assert.Equal(0, CaesarSystem.Triumphs(w, g));
        Assert.Contains(w.History, h => h.Text.Contains("the triumph it voted is void"));
        w.Turn++; tc.Caesar.Tick(w);   // the next year the recall is obeyed, with all that follows
        Assert.DoesNotContain(w.Recalls, r => r.CharacterId == g.Id); Assert.Equal("", f.AdmiralId); Assert.Equal("", f.DevotedTo); Assert.Equal(0, f.Devotion);
        Assert.Equal(w.Turn + CaesarSystem.BarredYears, w.Counter(CaesarSystem.BarredKey(g.Id)));

        // the player, voted a triumph and recalled at once: coming home for it is refused, and the triumph is void at the year's end
        var (tc2, w2, me2, mine2, ctx2) = Start(); var f2 = Legion(w2, mine2, me2);
        foreach (var x in w2.Fleets.Values.Where(x => x.Id != f2.Id && (x.OwnerId == mine2.Id || ServiceSystem.CommandedBy(w2, x, mine2.Id)))) x.AdmiralId = "";
        w2.WorldCounters[CaesarSystem.LaurelKey(me2.Id)] = 3; tc2.Caesar.TriumphVoted(w2, mine2, null, passed: true); tc2.Caesar.RecallVoted(w2, mine2, null, passed: true);
        Assert.Contains(w2.Recalls, r => r.CharacterId == me2.Id);
        Assert.Equal("the Senate has since recalled you, and one under recall comes home at its order, not to a triumph", new TriumphCommand(me2.Id, TriumphSystem.Home, tc2.Caesar).Refusal(ctx2));
        TriumphSystem.Tick(w2, tc2.Senate, tc2.Caesar);
        Assert.Equal(TriumphSystem.Void, w2.TriumphVotes.Single().Choice); Assert.Equal(me2.Id, f2.AdmiralId); Assert.Null(TriumphSystem.Waiting(w2, me2.Id));
        Assert.Contains(w2.News, n => n.Tone == "warn" && n.Text.StartsWith("The Senate has recalled you, and the triumph it voted is void"));

        // the year before a count, the only one a house has who could stand is under recall: they do not come home to stand out of it
        var (tc3, w3, me3, mine3, _) = Start(); var rival3 = Rival(w3, mine3);
        while (LadderSystem.CountIn(w3, Offices) != 1 || ElectionSystem.CountIn(w3) is 1 or 2) w3.Turn++;
        w3.Motions.Clear(); w3.Emergency = null; w3.Wars.Clear();
        foreach (var x in w3.Fleets.Values) x.AdmiralId = "";
        var star = Officer(w3, rival3, "recalled_star"); star.Age = 28;
        foreach (var x in w3.LivingCharacters().Where(x => x.HouseId == rival3.Id && x.Id != star.Id)) { x.CareerTrack = ""; x.CareerRank = 0; }
        var f3 = Legion(w3, rival3, star, devotion: 30);
        Assert.Contains(star, TriumphSystem.AwayCandidates(w3, tc3.Senate, rival3, ladder: true, consul: false));
        tc3.Caesar.RecallVoted(w3, rival3, null, passed: true); Assert.Contains(w3.Recalls, r => r.CharacterId == star.Id);
        TriumphSystem.Tick(w3, tc3.Senate, tc3.Caesar);
        Assert.Equal(star.Id, f3.AdmiralId); Assert.False(TriumphSystem.StandingHome(w3, star)); Assert.Equal(0, w3.Counter(TriumphSystem.CameHomeKey));
    }

    [Fact]
    public void A_consul_who_dies_in_office_is_followed_by_one_of_the_house_though_they_command_away()
    {
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); var holder = Economy.HeadOf(w, rival)!; var consul = Offices.Def("consul_senior")!;
        w.Wars.Clear(); w.Offices.RemoveAll(o => o.OfficeId == consul.Id); w.Offices.Add(new OfficeHolding { OfficeId = consul.Id, HouseId = rival.Id, CharacterId = holder.Id, SinceTurn = w.Turn });
        foreach (var x in w.LivingCharacters().Where(x => x.HouseId == rival.Id && x.Id != holder.Id)) x.Cursus.Clear();
        var heir = Officer(w, rival, "consul_heir"); heir.Age = 50; heir.CareerRank = 3; heir.Cursus.AddRange(new[] { LadderSystem.Quaestor, LadderSystem.Praetor });
        Legion(w, rival, heir); Assert.NotNull(TriumphSystem.AwayRefusal(w, heir));   // they could not stand for it
        holder.Alive = false; OfficeSystem.Tick(w, Offices);
        Assert.Equal(heir.Id, OfficeSystem.Holding(w, consul.Id)?.CharacterId);   // but succeeding to the seat is not standing for it
        Assert.Equal(rival.Id, OfficeSystem.Holding(w, consul.Id)?.HouseId);
    }

    [Fact]
    public void The_screens_show_the_general_s_gauges_only_for_a_general_and_the_new_door_only_under_the_Republic()
    {
        string Razor(string name) => File.ReadAllText(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Web", "Components", name));
        // the house's only commander home for a triumph: no general, so no gauges, crews line or gates; the line saying nobody commands is
        // left out because the gate says who is home and why
        var caesar = Razor("CaesarPanel.razor");
        Assert.DoesNotContain("general == null && !atTheGate", caesar); Assert.Contains("@if (general == null) { if (!atTheGate) {", caesar);
        // under a throne the generals' business is not opened to those who have held a magistracy
        Assert.Contains("(!w.IsImperium && TriumphSystem.HeldMagistracy(w, w.Protagonist!) ? MayMoveGenerals : string.Empty)", Razor("SenateScreen.razor"));
    }

    [Fact]
    public void The_player_is_told_when_another_house_s_commander_lays_down_a_fleet_of_the_player_s_house()
    {
        // a legate of another house on a fleet of the player's is voted a triumph and comes home for it
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); w.Wars.Clear(); w.Motions.Clear();
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == rival.Id || x.OwnerId == mine.Id)) x.AdmiralId = "";
        var legate = Officer(w, rival, "rival_legate"); legate.NatureTraits.Clear(); legate.NatureTraits.Add("Loyal"); legate.Ambition = "";
        string at = w.HoldingsOf(mine.Id).Where(x => x.IsPlanet && x.SystemId != w.Galaxy!.Capital).Select(x => x.SystemId).FirstOrDefault() ?? w.Galaxy!.Systems.Keys.First(k => k != w.Galaxy.Capital);
        var f = new Fleet { Id = "my_fleet", Name = "The Lent", OwnerId = mine.Id, SystemId = at, HomeSystemId = at, Strength = 6 }; w.Fleets[f.Id] = f;
        ServiceSystem.Begin(w, f, legate, me, ServiceSystem.Legate, ServiceSystem.ByConsul);
        Assert.Equal(legate.Id, CaesarSystem.GeneralOf(w, rival)?.Id);
        // one who commands by service is told the way out that is theirs
        Assert.Equal($"{legate.Name} commands The Lent away from the capital: resign the command, or ask the Senate for leave to stand in absence", TriumphSystem.AwaySays(w, legate));
        w.WorldCounters[CaesarSystem.LaurelKey(legate.Id)] = 3; tc.Caesar.TriumphVoted(w, rival, null, passed: true);
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.Equal(TriumphSystem.Home, w.TriumphVotes.Single().Choice); Assert.Equal("", f.AdmiralId);
        Assert.Contains(w.News, n => n.Tone == "warn" && n.Target == "military:fleets"
            && n.Text == $"{legate.Name} of {rival.Name}, who commands The Lent for your house by service, lays the command down to have the triumph the Senate voted, until the end of year {w.Turn + TriumphSystem.HomeYears}. The Lent waits for them with nobody in command; give it to another, and it is theirs.");

        // an officer of another house on a fleet of the player's comes home the year before a count to stand
        var (tc2, w2, me2, mine2, _) = Start(); var rival2 = Rival(w2, mine2);
        while (LadderSystem.CountIn(w2, Offices) != 1 || ElectionSystem.CountIn(w2) is 1 or 2) w2.Turn++;
        w2.Motions.Clear(); w2.Emergency = null; w2.Wars.Clear();
        foreach (var x in w2.Fleets.Values) x.AdmiralId = "";
        var lent = Officer(w2, rival2, "lent_officer"); lent.Age = 28;
        foreach (var x in w2.LivingCharacters().Where(x => x.HouseId == rival2.Id && x.Id != lent.Id)) { x.CareerTrack = ""; x.CareerRank = 0; }
        string at2 = w2.HoldingsOf(mine2.Id).Where(x => x.IsPlanet && x.SystemId != w2.Galaxy!.Capital).Select(x => x.SystemId).FirstOrDefault() ?? w2.Galaxy!.Systems.Keys.First(k => k != w2.Galaxy.Capital);
        var f2 = new Fleet { Id = "my_fleet", Name = "The Lent", OwnerId = mine2.Id, SystemId = at2, HomeSystemId = at2, Strength = 6 }; w2.Fleets[f2.Id] = f2;
        ServiceSystem.Begin(w2, f2, lent, me2, ServiceSystem.Legate, ServiceSystem.ByConsul);
        Assert.Contains(lent, TriumphSystem.AwayCandidates(w2, tc2.Senate, rival2, ladder: true, consul: false));
        TriumphSystem.Tick(w2, tc2.Senate, tc2.Caesar);
        Assert.Equal("", f2.AdmiralId); Assert.True(TriumphSystem.HeldFor(w2, f2, lent.Id));
        Assert.Contains(w2.News, n => n.Tone == "warn" && n.Target == "military:fleets"
            && n.Text == $"{lent.Name} of {rival2.Name}, who commands The Lent for your house by service, lays the command down to come home and stand for office, until the count at the end of year {w2.Turn + 1} is past. The Lent waits for them with nobody in command; give it to another, and it is theirs.");
    }

    [Fact]
    public void A_house_brings_its_consul_home_to_stand_only_when_nobody_else_of_it_may_stand()
    {
        var (tc, w, me, mine, _) = Start(); var rival = Rival(w, mine);
        foreach (var b in w.Factions) b.Members.Remove(rival.Id);   // a house of no bench is its own nominee
        w.WorldCounters.Remove("consul_term:" + rival.Id); w.WorldCounters.Remove(CaesarSystem.EnemyKey(rival.Id)); w.Emergency = null; w.Wars.Clear();
        w.Offices.RemoveAll(o => o.HouseId == rival.Id && Offices.Def(o.OfficeId)?.Elected == true);
        foreach (var x in w.Fleets.Values) x.AdmiralId = "";
        foreach (var x in w.LivingCharacters().Where(x => x.HouseId == rival.Id)) x.Cursus.Clear();
        var home = Officer(w, rival, "at_home"); home.Age = 50; home.CareerRank = 2; home.Cursus.AddRange(new[] { LadderSystem.Quaestor, LadderSystem.Praetor });
        var away = Officer(w, rival, "away_general"); away.Age = 55; away.CareerRank = 4; away.Cursus.AddRange(new[] { LadderSystem.Quaestor, LadderSystem.Praetor });
        Legion(w, rival, away);
        var consul = ElectionSystem.ConsulOffices(Offices).First();
        Assert.Equal(home.Id, OfficeSystem.Candidate(w, consul, rival.Id)?.Id); Assert.Equal(away.Id, TriumphSystem.ConsulAwayOne(w, Offices, rival.Id)?.Id);
        Assert.DoesNotContain(away, TriumphSystem.AwayCandidates(w, tc.Senate, rival, ladder: false, consul: true));   // it has one who may stand
        home.Cursus.Clear(); Assert.Null(OfficeSystem.Candidate(w, consul, rival.Id));
        Assert.Contains(away, TriumphSystem.AwayCandidates(w, tc.Senate, rival, ladder: false, consul: true));        // and now it has not
    }

    [Fact]
    public void A_fleet_is_held_only_while_its_general_lives_and_is_of_the_house()
    {
        var (tc, w, me, mine, ctx) = Start(); var rival = Rival(w, mine); w.Wars.Clear();
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == rival.Id || ServiceSystem.CommandedBy(w, x, rival.Id))) x.AdmiralId = "";
        var g = Officer(w, rival, "home_general"); g.NatureTraits.Clear(); g.NatureTraits.Add("Loyal"); var f = Legion(w, rival, g);
        w.WorldCounters[CaesarSystem.LaurelKey(g.Id)] = 3; tc.Caesar.TriumphVoted(w, rival, null, passed: true); TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.True(TriumphSystem.Held(w, f)); Assert.True(TriumphSystem.HeldFor(w, f, g.Id));
        int until = w.TriumphVotes.Single().HomeUntil;
        Assert.Equal($"{g.Name} is home for the triumph, and holds no command until the end of year {until}", FleetSystem.CaptainRefusal(w, f, g));   // not made captain meanwhile either
        g.Alive = false; Assert.False(TriumphSystem.Held(w, f));   // dead: nothing waits for them, and the house may fill the flag
        g.Alive = true; Assert.True(TriumphSystem.Held(w, f));
        g.HouseId = mine.Id; Assert.False(TriumphSystem.Held(w, f));   // married out, or cast out: the same
    }

    // ---------------- mended after the three Imperator ideas met ----------------

    [Fact]
    public void One_who_goes_into_exile_has_no_triumph_at_the_gate()
    {
        var (tc, w, me, mine, ctx) = Start(); Legion(w, mine, me); w.WorldCounters[CaesarSystem.LaurelKey(me.Id)] = 3; tc.Caesar.TriumphVoted(w, mine, null, passed: true);
        var t = TriumphSystem.Waiting(w, me.Id)!; int legit = mine.Legitimacy;
        tc.Charges.Exile(w, me, null);
        Assert.Null(TriumphSystem.Waiting(w, me.Id)); Assert.Equal(TriumphSystem.Void, t.Choice); Assert.True(t.Done); Assert.Equal("went into exile", t.Why);
        Assert.StartsWith("you are in exile", new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar).Refusal(ctx));
        Assert.StartsWith("you are in exile", new TriumphCommand(me.Id, TriumphSystem.Keep, tc.Caesar).Refusal(ctx));
        Assert.Equal(0, CaesarSystem.Triumphs(w, me)); Assert.False(w.HasFlag(CaesarSystem.TriumphFlag));
        // one voted a triumph while abroad has it void at the year's end, another house's general as the player
        var rival = Rival(w, mine); var g = Officer(w, rival, "abroad_general"); g.NatureTraits.Add("Loyal"); Legion(w, rival, g);
        tc.Charges.Exile(w, g, null); TriumphSystem.Voted(w, rival, g, null); TriumphSystem.Voted(w, mine, me, null);
        Assert.StartsWith("you are in exile", new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar).Refusal(ctx));
        TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        Assert.All(w.TriumphVotes, x => Assert.Equal(TriumphSystem.Void, x.Choice)); Assert.Equal(0, CaesarSystem.Triumphs(w, g)); Assert.Equal(0, CaesarSystem.Triumphs(w, me));
    }

    [Fact]
    public void A_house_whose_head_is_in_exile_moves_neither_a_triumph_nor_leave()
    {
        // a house with no voice whose head has held a magistracy, and a general of laurels of its own who is not the head
        (TurnController tc, World w, House rival, Character rh) Setup()
        {
            var (tc, w, _, mine, _) = Start(); var rival = Rival(w, mine); var rh = Economy.HeadOf(w, rival)!;
            foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == rival.Id || x.AdmiralId == rh.Id)) x.AdmiralId = "";
            rival.Title = "citizen"; rh.CareerTrack = "military"; rival.Enrolled = false; rival.Orders.Clear(); rh.Cursus.Clear(); rh.Cursus.Add(LadderSystem.Quaestor); w.Magistrates.RemoveAll(x => x.CharacterId == rh.Id);
            var g = Officer(w, rival, "laurelled"); Legion(w, rival, g);
            foreach (var c in w.LivingCharacters()) w.WorldCounters[CaesarSystem.LaurelKey(c.Id)] = 0;
            w.WorldCounters[CaesarSystem.LaurelKey(g.Id)] = 3; w.Motions.Clear();
            foreach (var o in w.Houses.Values.Where(o => o.Id != rival.Id && Economy.HeadOf(w, o) != null)) w.Relationship(Economy.HeadOf(w, o)!.Id, rh.Id).Disposition = 0;
            return (tc, w, rival, rh);
        }
        var (tc, w, rival, rh) = Setup();
        Assert.True(TriumphSystem.MayMove(w, rh, tc.Senate.Motions.Def(TriumphSystem.TriumphKind)));
        w.Turn++; tc.Senate.Tick(w); Assert.Contains(w.Motions, m => m.Kind == TriumphSystem.TriumphKind && m.ProposerHouseId == rival.Id);   // at home, the head moves it
        var (tc2, w2, rival2, rh2) = Setup();
        tc2.Charges.Exile(w2, rh2, null); Assert.True(ChargeSystem.HeadAbroad(w2, rival2));
        Assert.False(TriumphSystem.MayMove(w2, rh2, tc2.Senate.Motions.Def(TriumphSystem.TriumphKind))); Assert.False(TriumphSystem.MayMove(w2, rh2, tc2.Senate.Motions.Def(TriumphSystem.LeaveKind)));
        var kin = Officer(w2, rival2, "former_quaestor"); kin.Cursus.Add(LadderSystem.Quaestor);
        Assert.False(TriumphSystem.MayMove(w2, kin, tc2.Senate.Motions.Def(TriumphSystem.TriumphKind)));                              // nor anyone of it: the house moves nothing but its exile's return
        w2.Turn++; tc2.Senate.Tick(w2); Assert.DoesNotContain(w2.Motions, m => m.ProposerHouseId == rival2.Id && m.Kind != ChargeSystem.HomeKind);
    }

    [Fact]
    public void The_gate_lists_only_those_still_home_and_says_hold_to_you()
    {
        var (tc, w, me, mine, ctx) = Start();
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id || x.AdmiralId == me.Id)) x.AdmiralId = "";
        var kin = Officer(w, mine, "home_kin"); kin.NatureTraits.Add("Loyal"); Legion(w, mine, kin);
        TriumphSystem.Voted(w, mine, kin, null); TriumphSystem.Tick(w, tc.Senate, tc.Caesar);
        var t = Assert.Single(TriumphSystem.HomeFor(w, mine.Id));
        Assert.StartsWith($"{kin.Name} is home for the triumph until the end of year {t.HomeUntil}, and holds no command until then.", TriumphSystem.HomeLine(w, t));
        kin.Alive = false; Assert.Empty(TriumphSystem.HomeFor(w, mine.Id));                                                           // dead: nobody is home
        kin.Alive = true; kin.HouseId = Rival(w, mine).Id; Assert.Empty(TriumphSystem.HomeFor(w, mine.Id));                           // married out, or cast out: the same
        kin.HouseId = mine.Id;
        // the player home: "you are", and "hold"
        var f = Legion(w, mine, me); TriumphSystem.Voted(w, mine, me, null); Assert.True(tc.Executor.TryExecute(new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar), ctx));
        var mt = TriumphSystem.HomeFor(w, mine.Id).Single(x => x.CharacterId == me.Id);
        Assert.StartsWith($"You are home for the triumph until the end of year {mt.HomeUntil}, and hold no command until then.", TriumphSystem.HomeLine(w, mt));
        // the gate and the Military panel read the same list
        string Razor(string name) => File.ReadAllText(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Web", "Components", name));
        Assert.Contains("TriumphSystem.HomeFor(w, p.HouseId)", Razor("TriumphGate.razor")); Assert.Contains("TriumphSystem.HomeLine(w, t)", Razor("TriumphGate.razor"));
        Assert.Contains("TriumphSystem.HomeFor(w, house.Id).Count > 0", Razor("CaesarPanel.razor"));
    }

    [Fact]
    public void The_gate_says_you_hold_no_command_and_says_nothing_of_laying_down_what_you_do_not_command()
    {
        var (tc, w, me, mine, ctx) = Start();
        foreach (var x in w.Fleets.Values.Where(x => x.AdmiralId == me.Id)) x.AdmiralId = "";
        TriumphSystem.Voted(w, mine, me, null);
        string says = TriumphSystem.HomeSays(w, me);
        Assert.Contains("You command nothing now, so nothing is laid down.", says); Assert.DoesNotContain("You lay the command down until", says); Assert.DoesNotContain(": You", says);
        Legion(w, mine, me); Assert.Contains($"You lay the command down until the end of year {w.Turn + TriumphSystem.HomeYears}: The Tenth waits", TriumphSystem.HomeSays(w, me));
    }

    [Fact]
    public void Standing_for_consul_says_the_bar_that_holds_when_another_holds_besides_the_command_away()
    {
        var (tc, w, me, mine, ctx) = Start(); w.PoolsFor(me.Id).Influence = 20;
        me.Age = 45; me.CareerRank = 3; me.Cursus.Clear(); me.Cursus.AddRange(new[] { LadderSystem.Quaestor, LadderSystem.Praetor });
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id)) c.Cursus.Clear();
        w.Offices.RemoveAll(o => o.HouseId == mine.Id && Offices.Def(o.OfficeId)?.Elected == true); w.WorldFlags.Remove(ElectionSystem.StandFlag(mine.Id));
        var f = Legion(w, mine, me); var stand = new StandForConsulCommand(me.Id, Offices);
        w.WorldCounters["consul_term:" + mine.Id] = Math.Max(1, w.Turn);   // the house held the consulship last term
        Assert.Equal("a consul may not stand again at the next election", stand.Refusal(ctx));
        w.WorldCounters.Remove("consul_term:" + mine.Id); w.WorldCounters[CaesarSystem.EnemyKey(mine.Id)] = w.Turn + 10;
        Assert.Equal($"the house is an enemy of the Reach until year {w.Turn + 10}, and may not stand for consul", stand.Refusal(ctx));
        w.WorldCounters.Remove(CaesarSystem.EnemyKey(mine.Id));
        Assert.Equal($"you command {f.Name} away from the capital: bring it to the capital, give the command to another, or ask the Senate for leave to stand in absence", stand.Refusal(ctx));   // only the command away holds now
        f.SystemId = w.Galaxy!.Capital; Assert.Null(stand.Refusal(ctx));
    }
}
