using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The ladder of offices (docs/LADDER.md): quaestor, aedile and praetor below the consulship, held by people, filled by
/// election every fifth year, each asking an age and a rung already held.</summary>
public class LadderTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;
    [Fact]
    public void A_saga_opens_with_magistrates_sitting_and_none_of_them_the_players()
    {
        var (_, w, me, _, _) = Start("the_new_man", 5);
        Assert.NotEmpty(w.Magistrates);
        Assert.DoesNotContain(w.Magistrates, m => m.HouseId == me.HouseId);
        foreach (var rung in Offices.Rungs) Assert.True(LadderSystem.Holders(w, rung.Id).Count <= rung.Seats);
        Assert.All(w.Magistrates, m => Assert.Contains(m.OfficeId, w.Char(m.CharacterId)!.Cursus));
        int before = w.Magistrates.Count; LadderSystem.Open(w, Offices);
        Assert.Equal(before, w.Magistrates.Count);
    }

    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); w.Protagonist!.Age = Math.Min(w.Protagonist.Age, 45);
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, new CommandContext(w, TestContent.Rng()));
    }
    private static Character Kin(World w, House h, string id, int age, string ambition = "")
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "male", CareerTrack = "law", CareerRank = 2, Ambition = ambition };
        c.Skills["oratory"] = 9; c.Skills["charisma"] = 9; w.Characters[id] = c; h.Members.Add(id); w.Relationship(id, w.ProtagonistId).Disposition = 30; return c;
    }
    private static int Weight(World w, TurnController tc, string id) => tc.Senate.Weight(w, id);

    [Fact]
    public void The_ladder_has_three_rungs_below_the_consulship_and_each_asks_an_age_and_the_one_before()
    {
        Assert.Equal(new[] { "quaestor", "aedile", "praetor" }, Offices.Rungs.Select(r => r.Id).ToArray()); Assert.Equal(5, Offices.LadderEvery);
        Assert.True(Offices.Rung("quaestor")!.MinAge < Offices.Rung("aedile")!.MinAge && Offices.Rung("aedile")!.MinAge < Offices.Rung("praetor")!.MinAge);
        foreach (var consul in ElectionSystem.ConsulOffices(Offices)) { Assert.Equal(40, consul.MinAge); Assert.Equal("praetor", consul.Requires); }
        var (_, w, _, mine, _) = Start(); var young = Kin(w, mine, "young", 22); var fresh = Kin(w, mine, "fresh", 30); var idle = Kin(w, mine, "idle", 30); idle.CareerTrack = ""; idle.CareerRank = 0;
        Assert.Contains("not before 25", LadderSystem.Bar(w, Offices, young, Offices.Rung("quaestor"))); Assert.Null(LadderSystem.Bar(w, Offices, fresh, Offices.Rung("quaestor")));
        var late = Kin(w, mine, "late", 36); Assert.Contains("been quaestor", LadderSystem.Bar(w, Offices, late, Offices.Rung("praetor"))); Assert.Contains("place in a service", LadderSystem.Bar(w, Offices, idle, Offices.Rung("quaestor")));
        fresh.Cursus.Add("quaestor"); fresh.Age = 34; Assert.Null(LadderSystem.Bar(w, Offices, fresh, Offices.Rung("praetor"))); Assert.Equal("they have held it", LadderSystem.Bar(w, Offices, fresh, Offices.Rung("quaestor")));
        Assert.Equal("praetor", LadderSystem.Next(w, Offices, fresh)!.Id);   // a person stands for the highest rung open to them
    }

    [Fact]
    public void Those_who_open_a_saga_of_an_age_and_rank_have_climbed_already_and_nobody_is_consul_without_the_praetorship()
    {
        var (_, w, _, _, _) = Start();
        Assert.Contains(w.Characters.Values, c => c.Cursus.Contains("praetor")); Assert.All(w.Characters.Values.Where(c => c.Cursus.Contains("praetor")), c => Assert.True(c.Age >= 36 && c.CareerRank >= 2));
        var consul = ElectionSystem.ConsulOffices(Offices).First();
        int houses = w.Houses.Values.Count(h => h.Id != PirateSystem.HouseId && OfficeSystem.Candidate(w, consul, h.Id) != null); Assert.True(houses >= 6, $"{houses} houses could field a consul");
        var able = w.Characters.Values.First(c => c.Alive && c.Cursus.Contains("praetor") && c.Age >= 40 && OfficeSystem.Candidate(w, consul, c.HouseId)?.Id == c.Id);
        able.Cursus.Remove("praetor"); Assert.NotEqual(able.Id, OfficeSystem.Candidate(w, consul, able.HouseId)?.Id);
    }

    [Fact]
    public void An_election_fills_every_rung_from_those_standing_and_writes_it_into_their_lives()
    {
        var (tc, w, me, mine, ctx) = Start(); var k = Kin(w, mine, "orator", 30);
        w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = k.Id });
        w.Turn = 5; Assert.True(LadderSystem.Due(w, Offices)); LadderSystem.Hold(w, Offices, id => Weight(w, tc, id));
        foreach (var rung in Offices.Rungs) { var holders = LadderSystem.Holders(w, rung.Id); Assert.InRange(holders.Count, 1, rung.Seats); Assert.All(holders, h => Assert.Contains(rung.Id, w.Char(h.CharacterId)!.Cursus)); }
        Assert.Equal(w.Magistrates.Count, w.Magistrates.Select(m => m.CharacterId).Distinct().Count());   // nobody sits on two rungs
        Assert.Equal("quaestor", LadderSystem.Seat(w, k.Id)?.OfficeId); Assert.Contains("quaestor", k.Cursus); Assert.Contains(k.Life!.Deeds, d => d.Kind == "office" && d.Text.Contains("quaestor"));
        Assert.True(w.HasFlag(LadderSystem.KinElectedFlag)); Assert.True(w.HasFlag(LadderSystem.HeldFlag)); Assert.Empty(w.Candidacies);
        // the next election ends every term, and a quaestor of last time may stand for more
        k.Age = 35; w.Candidacies.Add(new Candidacy { RungId = "praetor", CharacterId = k.Id }); w.Turn = 10; LadderSystem.Hold(w, Offices, id => Weight(w, tc, id));
        Assert.Equal("praetor", LadderSystem.Seat(w, k.Id)?.OfficeId); Assert.Equal(new[] { "quaestor", "praetor" }, k.Cursus.ToArray());
        _ = me; _ = ctx;
    }

    [Fact]
    public void The_head_stands_for_themselves_only_and_may_put_weight_behind_anyone_of_the_house()
    {
        var (tc, w, me, mine, ctx) = Start(); me.Cursus.Clear(); me.CareerTrack = "law"; me.CareerRank = 2; me.Age = 30; var k = Kin(w, mine, "kin", 30); w.PoolsFor(me.Id).Influence = 6;
        Assert.Equal("whether they stand is their own affair", new LadderCommand(me.Id, LadderCommand.Stand, k.Id, "quaestor", Offices).Refusal(ctx));
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Stand, me.Id, "quaestor", Offices), ctx)); Assert.Equal(4, w.PoolsFor(me.Id).Influence);
        Assert.False(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Stand, me.Id, "quaestor", Offices), ctx));   // already standing
        Assert.Equal("they are not standing", new LadderCommand(me.Id, LadderCommand.Back, k.Id, "", Offices).Refusal(ctx));
        w.Candidacies.Add(new Candidacy { RungId = "quaestor", CharacterId = k.Id }); int regard = w.Relationship(k.Id, me.Id).Disposition;
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Back, k.Id, "", Offices), ctx)); Assert.Equal(LadderSystem.BackVotes, w.Candidacies.First(c => c.CharacterId == k.Id).Backing);
        Assert.Equal(regard + 6, w.Relationship(k.Id, me.Id).Disposition);
        // backing shows at the count
        var rung = Offices.Rung("quaestor")!; var cands = LadderSystem.Candidates(w, Offices, rung); int with = LadderSystem.Tally(w, Offices, rung, cands, id => Weight(w, tc, id))[k.Id];
        w.Candidacies.First(c => c.CharacterId == k.Id).Backing = 0; Assert.Equal(with - LadderSystem.BackVotes, LadderSystem.Tally(w, Offices, rung, cands, id => Weight(w, tc, id))[k.Id]);
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Withdraw, me.Id, "", Offices), ctx)); Assert.DoesNotContain(w.Candidacies, c => c.CharacterId == me.Id);
    }

    [Fact]
    public void One_of_the_house_who_wants_to_rise_puts_their_own_name_down()
    {
        var (_, w, me, mine, _) = Start(); foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id).ToList()) c.Alive = false;
        var keen = Kin(w, mine, "keen", 30, "master_the_senate"); var content = Kin(w, mine, "content", 30, "win_a_great_love");
        w.Turn = 1; LadderSystem.Tick(w, Offices); Assert.Empty(w.Candidacies);   // not four years before the count
        w.Turn = 3; LadderSystem.Tick(w, Offices);
        Assert.Contains(w.Candidacies, c => c.CharacterId == keen.Id && c.RungId == "quaestor"); Assert.DoesNotContain(w.Candidacies, c => c.CharacterId == content.Id); Assert.Contains(LadderSystem.StandingChar, keen.Flags);
        var (_, w2, me2, mine2, _) = Start(); w2.FamilyAutonomy = 0; var keen2 = Kin(w2, mine2, "keen", 30, "master_the_senate"); w2.Turn = 3; LadderSystem.Tick(w2, Offices); Assert.DoesNotContain(w2.Candidacies, c => c.CharacterId == keen2.Id);
        _ = me2;
    }

    [Fact]
    public void Asked_to_stand_down_the_loyal_withdraw_and_the_wilful_do_not()
    {
        var (tc, w, me, mine, ctx) = Start(); foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id).ToList()) c.Alive = false;
        var loyal = Kin(w, mine, "loyal", 30, "master_the_senate"); loyal.NatureTraits.Add("Loyal");
        var proud = Kin(w, mine, "proud", 30, "master_the_senate"); proud.NatureTraits.Add("Proud");
        w.Turn = 3; LadderSystem.Tick(w, Offices); Assert.Equal(2, w.Candidacies.Count);
        int was = w.Relationship(loyal.Id, me.Id).Disposition;
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Dissuade, loyal.Id, "", Offices), ctx));
        Assert.DoesNotContain(w.Candidacies, c => c.CharacterId == loyal.Id); Assert.True(w.Relationship(loyal.Id, me.Id).Disposition < was);
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Dissuade, proud.Id, "", Offices), ctx));
        Assert.Contains(w.Candidacies, c => c.CharacterId == proud.Id);   // it is their name
        Assert.Equal("you have asked them once", new LadderCommand(me.Id, LadderCommand.Dissuade, proud.Id, "", Offices).Refusal(ctx));
        // the one who stood down does not put their name back this term; the head may not withdraw another's name outright
        w.Turn = 4; LadderSystem.Tick(w, Offices); Assert.DoesNotContain(w.Candidacies, c => c.CharacterId == loyal.Id);
        Assert.NotNull(new LadderCommand(me.Id, LadderCommand.Withdraw, proud.Id, "", Offices).Refusal(ctx));
    }

    [Fact]
    public void A_magistrate_is_paid_learns_the_work_and_adds_to_the_houses_standing_in_the_senate()
    {
        var (tc, w, me, mine, _) = Start(); foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id)) c.Cursus.Clear();
        var k = Kin(w, mine, "judge", 36); k.Cursus.Add("quaestor");
        int standing = LadderSystem.SenateStanding(w, Offices, mine);
        w.Magistrates.Add(new OfficeHolding { OfficeId = "praetor", HouseId = mine.Id, CharacterId = k.Id, SinceTurn = w.Turn }); k.Cursus.Add("praetor");
        Assert.True(LadderSystem.SenateStanding(w, Offices, mine) > standing);
        var (score, parts) = tc.Orders.Measure(w, mine, OrderSystem.Senate); Assert.Contains(parts, x => x.what.Contains("magistracies")); _ = score;
        Assert.Contains(EducationSystem.Practice(w, k), x => x.skill == "law" && x.why == "a magistracy");
        int purse = k.Purse; LadderSystem.Tick(w, Offices); Assert.Equal(purse + Offices.Rung("praetor")!.Stipend, k.Purse);
        k.Alive = false; LadderSystem.Tick(w, Offices); Assert.Null(LadderSystem.Seat(w, k.Id));   // a seat lapses with its holder
        _ = me;
    }

    [Fact]
    public void An_aedile_gives_games_out_of_their_own_purse_once()
    {
        var (tc, w, me, mine, ctx) = Start(); int cost = Offices.Rung("aedile")!.Games; Assert.True(cost > 0);
        Assert.Contains("only an aedile", new LadderCommand(me.Id, LadderCommand.Games, me.Id, "", Offices).Refusal(ctx));
        w.Magistrates.Add(new OfficeHolding { OfficeId = "aedile", HouseId = mine.Id, CharacterId = me.Id, SinceTurn = w.Turn }); me.Purse = cost - 1;
        Assert.Contains("credits of your own", new LadderCommand(me.Id, LadderCommand.Games, me.Id, "", Offices).Refusal(ctx));
        me.Purse = cost + 3; int legit = mine.Legitimacy;
        Assert.True(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Games, me.Id, "", Offices), ctx));
        Assert.Equal(3, me.Purse); Assert.Equal(Math.Min(100, legit + 2), mine.Legitimacy); Assert.True(w.HasFlag(LadderSystem.GamesFlag)); Assert.Contains(me.Life!.Deeds, d => d.Text.Contains("games"));
        me.Purse = 100; Assert.False(tc.Executor.TryExecute(new LadderCommand(me.Id, LadderCommand.Games, me.Id, "", Offices), ctx));
    }

    [Fact]
    public void There_is_no_ladder_under_a_throne_and_over_sixty_years_the_republic_still_finds_its_consuls()
    {
        var (_, wi, mei, _, _) = Start("the_imperial_court"); Assert.False(LadderSystem.Active(wi, Offices)); Assert.Contains("under a throne", LadderSystem.Bar(wi, Offices, mei, Offices.Rung("quaestor")));
        var (tc, w, _, _, _) = Start("the_circling_heir", 3); int elections = 0, seated = 0;
        for (int i = 0; i < 60 && !w.IsDynastyDead; i++)
        {
            tc.EndTurn();
            if (w.Turn % 10 == 0 && !w.IsImperium) { elections++; seated += ElectionSystem.ConsulOffices(Offices).Count(d => OfficeSystem.Holding(w, d.Id) != null); }
            if (w.Turn % 5 == 0 && !w.IsImperium) Assert.True(w.Magistrates.Count >= 4, $"turn {w.Turn}: {w.Magistrates.Count} magistrates");
            if (!w.IsDynastyDead) tc.BeginTurn();
        }
        Assert.True(elections == 0 || seated >= elections, $"{seated} consuls seated at {elections} elections");
    }
}
