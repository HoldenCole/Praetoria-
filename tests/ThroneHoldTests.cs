using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A throne that can be lost, stage 1 (docs/THRONE.md; docs/GAPS.md R5): the hold on a throne, a pretender who rises when it is
/// weak, sides taken, and how it ends, for a player who is emperor as for anyone.</summary>
public class ThroneHoldTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me, House mine, House throne, House duke) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_imperial_court"), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!, w.House(w.Protagonist!.HouseId)!, ThroneSystem.Emperor(w)!, w.House("valmont")!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static void Hulls(World w, House h, int n, string? at = null)
    {
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == h.Id).ToList()) w.Fleets.Remove(f.Id);
        if (n > 0) w.Fleets["test_" + h.Id] = new Fleet { Id = "test_" + h.Id, Name = "the fleet of " + h.Name, OwnerId = h.Id, SystemId = at ?? w.Galaxy!.Systems.Keys.OrderBy(k => k, StringComparer.Ordinal).First(), Strength = n };
    }
    private static bool Eligible(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = Math.Max(100, w.House(me.HouseId)!.Treasury.Credits); w.PoolsFor(me.Id).Influence = 9;
        return new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == eventId);
    }
    private static void Weaken(World w, House throne) { throne.Legitimacy = 5; Hulls(w, throne, 1); }

    [Fact]
    public void TheHold_IsMadeOfThingsThatCanBeSeen()
    {
        var (tc, w, me, mine, throne, duke) = Start(); int hold = ThroneSystem.Hold(w); var parts = ThroneSystem.HoldParts(w);
        Assert.Equal(Math.Clamp(ThroneSystem.HoldBase + parts.Sum(p => p.points), 0, 100), hold); Assert.Contains(parts, p => p.what == "the standing of the house");
        Assert.Contains(parts, p => p.what.Contains("years on the throne") && p.points == 10);   // a throne that was there when the story opened is an old one
        throne.Legitimacy -= 40; Assert.Equal(hold - 20, ThroneSystem.Hold(w)); throne.Legitimacy += 40;
        var head = Economy.HeadOf(w, throne)!; int age = head.Age; head.Age = 9; Assert.Contains(ThroneSystem.HoldParts(w), p => p.what == "a child on the throne" && p.points == -15); head.Age = age;
        // a throne newly taken by force is weaker for it, for ten years
        w.WorldCounters[ThroneSystem.SinceKey] = w.Turn; w.WorldCounters[ThroneSystem.HowKey] = 1;
        var taken = ThroneSystem.HoldParts(w); Assert.Contains(taken, p => p.what == "new to the throne" && p.points == -5); Assert.Contains(taken, p => p.what == "took it by force" && p.points == -10);
        Assert.Equal("firm", ThroneSystem.HoldName(ThroneSystem.FirmAt)); Assert.Equal("holds", ThroneSystem.HoldName(ThroneSystem.ShakenAt)); Assert.Equal("shaken", ThroneSystem.HoldName(ThroneSystem.RiseAt)); Assert.Equal("failing", ThroneSystem.HoldName(ThroneSystem.RiseAt - 1));
        // and no throne, no hold
        var rep = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7); Assert.Equal(0, ThroneSystem.Hold(rep)); Assert.Empty(ThroneSystem.HoldParts(rep));
    }

    [Fact]
    public void AWeakThrone_BringsAPretender_AndNeverThePlayersHouseUnasked()
    {
        var (tc, w, me, mine, throne, duke) = Start(); Weaken(w, throne); Hulls(w, duke, 8); Hulls(w, mine, 30); mine.Title = Title.Duke;
        Assert.True(ThroneSystem.Hold(w) < ThroneSystem.RiseAt);
        w.Relationship(Economy.HeadOf(w, duke)!.Id, Economy.HeadOf(w, throne)!.Id).Disposition = -20;
        w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets); Assert.Null(w.Rising);                 // one bad year is not enough
        w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets);
        var r = w.Rising!; Assert.Equal(duke.Id, r.HouseId); Assert.Equal(throne.Id, r.EmperorId); Assert.True(w.AtWar(duke.Id, throne.Id));
        Assert.Contains(ThroneSystem.PretenderChar, Economy.HeadOf(w, duke)!.Flags); Assert.Equal(1, w.Counter(ThroneSystem.RisenKey));
        Assert.True(w.HasFlag(ThroneSystem.RisenFlag)); Assert.True(w.HasFlag(ThroneSystem.SideAskedFlag)); Assert.True(Eligible(w, "a_standard_is_raised"));
        Assert.DoesNotContain(mine.Id, r.With); Assert.DoesNotContain(mine.Id, r.Against); Assert.False(w.AtWar(mine.Id, throne.Id));   // the strongest house was the player's, and it was not sent
        // a war for the throne is not ended by treaty
        tc.Fleets.MakePeace(w, duke.Id, throne.Id); Assert.True(w.AtWar(duke.Id, throne.Id));
        // a firm throne brings nobody
        var (tc2, w2, _, _, throne2, duke2) = Start(); Hulls(w2, duke2, 40);
        for (int y = 0; y < 5; y++) { w2.Turn++; ThroneSystem.Tick(w2, Content.Titles, tc2.Fleets); }
        Assert.True(ThroneSystem.Hold(w2) >= ThroneSystem.RiseAt); Assert.Null(w2.Rising);
    }

    [Fact]
    public void WhoMayRise_AndTheHousesTakeASide()
    {
        var (tc, w, me, mine, throne, duke) = Start(); Hulls(w, throne, 10);
        Assert.Contains("at least", ThroneSystem.RiseRefusal(w, mine, Content.Titles));                     // a knight
        mine.Claims.Add(Title.Emperor); Hulls(w, mine, 2); Assert.Contains("so small a fleet", ThroneSystem.RiseRefusal(w, mine, Content.Titles));
        Hulls(w, mine, 6); Assert.Null(ThroneSystem.RiseRefusal(w, mine, Content.Titles));                  // a claim will do for rank
        Assert.Equal("you hold the throne", ThroneSystem.RiseRefusal(w, throne, Content.Titles));
        // two houses with a view: one loves the pretender, one the throne
        var others = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != throne.Id && h.Id != PirateSystem.HouseId && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).Take(2).ToList();
        var friend = others[0]; var loyal = others[1]; var emperor = Economy.HeadOf(w, throne)!;
        foreach (var h in w.Houses.Values) if (Economy.HeadOf(w, h) is { } hh) { w.Relationship(hh.Id, me.Id).Disposition = 0; w.Relationship(hh.Id, emperor.Id).Disposition = 0; }
        w.Relationship(Economy.HeadOf(w, friend)!.Id, me.Id).Disposition = 60; w.Relationship(Economy.HeadOf(w, loyal)!.Id, emperor.Id).Disposition = 60;
        Assert.Equal(60, ThroneSystem.SideLean(w, friend, mine, throne)); Assert.Equal(-60, ThroneSystem.SideLean(w, loyal, mine, throne));
        w.PoolsFor(me.Id).Influence = 9;
        Assert.True(Do(w, new ThroneCommand(me.Id, ThroneCommand.Raise, Content.Titles, tc.Fleets)));
        var r = w.Rising!; Assert.Equal(mine.Id, r.HouseId); Assert.Contains(friend.Id, r.With); Assert.Contains(loyal.Id, r.Against);
        Assert.True(w.AtWar(mine.Id, throne.Id)); Assert.True(w.AtWar(friend.Id, throne.Id)); Assert.True(w.AtWar(loyal.Id, mine.Id));
        Assert.False(w.HasFlag(ThroneSystem.SideAskedFlag)); Assert.Contains(me.Life!.Deeds, d => d.Text.Contains("Raised a standard"));
        Assert.Equal("your standard is raised already", ThroneSystem.RiseRefusal(w, mine, Content.Titles));
        Assert.Equal("it is your own war", ThroneSystem.BackRefusal(w, mine, ThroneCommand.Throne));
        // blood owed by the throne counts for the pretender
        w.Feuds.Add(new BloodFeud { Id = "f", WrongedId = loyal.Id, OwingId = throne.Id, VictimName = "x" }); Assert.Equal(-40, ThroneSystem.SideLean(w, loyal, mine, throne));
    }

    [Fact]
    public void ThePlayer_MayTakeASide_InAnotherHousesRising()
    {
        var (tc, w, me, mine, throne, duke) = Start(); Hulls(w, throne, 10); Hulls(w, duke, 8);
        ThroneSystem.Rise(w, duke, tc.Fleets); var r = w.Rising!; var pretender = Economy.HeadOf(w, duke)!; var emperor = Economy.HeadOf(w, throne)!;
        int a = w.GetRelationship(pretender.Id, me.Id)?.Disposition ?? 0, b = w.GetRelationship(emperor.Id, me.Id)?.Disposition ?? 0;
        Assert.Null(ThroneSystem.BackRefusal(w, mine, ThroneCommand.Pretender));
        Assert.True(Do(w, new ThroneCommand(me.Id, ThroneCommand.Pretender, Content.Titles, tc.Fleets)));
        Assert.Contains(mine.Id, r.With); Assert.True(w.AtWar(mine.Id, throne.Id));
        Assert.Equal(a + ThroneSystem.BackedRegard, w.GetRelationship(pretender.Id, me.Id)!.Disposition); Assert.Equal(b + ThroneSystem.CrossedRegard, w.GetRelationship(emperor.Id, me.Id)!.Disposition);
        Assert.Equal("your house has chosen", ThroneSystem.BackRefusal(w, mine, ThroneCommand.Throne));
        // through the report: the answer is carried out at the year's end
        var (tc2, w2, me2, mine2, throne2, duke2) = Start(); Hulls(w2, throne2, 10); Hulls(w2, duke2, 8); ThroneSystem.Rise(w2, duke2, tc2.Fleets);
        w2.WorldFlags.Add(ThroneSystem.ForThroneFlag); w2.Turn++; ThroneSystem.Tick(w2, Content.Titles, tc2.Fleets);
        Assert.Contains(mine2.Id, w2.Rising!.Against); Assert.True(w2.AtWar(mine2.Id, duke2.Id));
    }

    [Fact]
    public void ThePretender_TakesTheThrone_OnTheTermsOfASeizure_AndTheOldHousePlaysOn()
    {
        var (tc, w, me, mine, throne, duke) = Start(); Weaken(w, throne); Hulls(w, duke, 9, w.Galaxy!.Capital);
        ThroneSystem.Rise(w, duke, tc.Fleets); var war = w.Wars.First(x => x.Between(duke.Id, throne.Id));
        w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets); Assert.Equal(throne.Id, ThroneSystem.Emperor(w)!.Id);          // not yet winning the war
        Assert.Contains("winning the war", ThroneSystem.SeizeRefusal(w, duke.Id, Content.Titles));
        war.AddScore(duke.Id, 6); w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets);
        Assert.Equal(duke.Id, ThroneSystem.Emperor(w)!.Id); Assert.Null(w.Rising); Assert.False(w.AtWar(duke.Id, throne.Id));
        Assert.Equal(Title.Archduke, throne.Title); Assert.Contains(Title.Emperor, throne.Claims); Assert.All(w.LivingCharacters().Where(c => c.HouseId == throne.Id), c => Assert.Contains(ThroneSystem.DeposedFlag, c.Flags));
        Assert.Equal(1, w.Counter(ThroneSystem.WonKey)); Assert.True(w.HasFlag(ThroneSystem.WonFlag)); Assert.True(Eligible(w, "a_new_house_on_the_throne")); Assert.False(w.HasFlag(ThroneSystem.CastDownFlag));
        Assert.DoesNotContain(ThroneSystem.PretenderChar, Economy.HeadOf(w, duke)!.Flags);
        // the new throne is weak in its turn: new, and taken by force
        var parts = ThroneSystem.HoldParts(w); Assert.Contains(parts, p => p.what == "took it by force"); Assert.Contains(parts, p => p.what == "new to the throne");
        // the story is not told as if the player had done it
        Assert.Null(CourtProjection.Build(tc, Content).EpilogueText);
        // and the house that was cast down may rise again one day, on its claim
        w.WorldCounters.Remove(ThroneSystem.LastRisingKey); Hulls(w, throne, 9); throne.Title = "count";
        Assert.Null(ThroneSystem.RiseRefusal(w, throne, Content.Titles));
    }

    [Fact]
    public void APretender_IsBroken_ByTheWar_ByASpentFleet_OrByTheYears()
    {
        foreach (string how in new[] { "war", "spent", "years" })
        {
            var (tc, w, me, mine, throne, duke) = Start(); Hulls(w, throne, 10); Hulls(w, duke, 8); duke.Legitimacy = 60; duke.Claims.Add(Title.Emperor); int standing = throne.Legitimacy;
            ThroneSystem.Rise(w, duke, tc.Fleets); var war = w.Wars.First(x => x.Between(duke.Id, throne.Id));
            if (how == "war") { war.AddScore(throne.Id, 6); w.Turn += 2; }
            else if (how == "spent") { Hulls(w, duke, 3); w.Turn++; }
            else w.Turn += ThroneSystem.PretenderYears;
            ThroneSystem.Tick(w, Content.Titles, tc.Fleets);
            Assert.Null(w.Rising); Assert.Equal(throne.Id, ThroneSystem.Emperor(w)!.Id); Assert.False(w.AtWar(duke.Id, throne.Id));
            Assert.Equal(60 - ThroneSystem.BrokenStanding, duke.Legitimacy); Assert.DoesNotContain(Title.Emperor, duke.Claims); Assert.Contains(ThroneSystem.BrokenChar, Economy.HeadOf(w, duke)!.Flags);
            Assert.Equal(Math.Min(100, standing + ThroneSystem.HeldStanding), throne.Legitimacy); Assert.Equal(1, w.Counter(ThroneSystem.BrokenKey)); Assert.Equal(1, w.Counter("pretender_broken_by:" + how));
            Assert.True(Eligible(w, "the_pretender_is_broken"));
            Assert.Contains("too lately dead", ThroneSystem.RiseRefusal(w, w.House(w.Protagonist!.HouseId) is { } m && m.Claims.Add(Title.Emperor) ? Fleeted(w, m) : mine, Content.Titles));
        }
        static House Fleeted(World w, House h) { w.Fleets["x"] = new Fleet { Id = "x", OwnerId = h.Id, SystemId = w.Galaxy!.Systems.Keys.OrderBy(k => k, StringComparer.Ordinal).First(), Strength = 20 }; return h; }
    }

    [Fact]
    public void APlayerOnTheThrone_CanBeCastDown_AndPlaysOn()
    {
        var (tc, w, me, mine, old, duke) = Start(); ThroneSystem.Enthrone(w, mine.Id, seized: false, me.Id);
        Assert.Equal(mine.Id, ThroneSystem.Emperor(w)!.Id); Assert.Contains(Title.Emperor, old.Claims);
        Assert.True(ThroneSystem.Hold(w) < ThroneSystem.ShakenAt);                                         // a knight's house on a throne
        w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets); Assert.True(w.HasFlag(ThroneSystem.WeakFlag)); Assert.True(Eligible(w, "the_hold_is_shaken"));
        Hulls(w, old, 9, w.Galaxy!.Capital); Hulls(w, mine, 2);
        w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets);
        var r = w.Rising!; Assert.Equal(old.Id, r.HouseId); Assert.True(w.HasFlag(ThroneSystem.AgainstMeFlag)); Assert.True(Eligible(w, "a_standard_against_you")); Assert.False(w.HasFlag(ThroneSystem.SideAskedFlag));
        w.Wars.First(x => x.Between(old.Id, mine.Id)).AddScore(old.Id, 6);
        w.Turn++; ThroneSystem.Tick(w, Content.Titles, tc.Fleets);
        Assert.Equal(old.Id, ThroneSystem.Emperor(w)!.Id); Assert.Equal(Title.Archduke, mine.Title); Assert.Contains(Title.Emperor, mine.Claims);
        Assert.True(w.HasFlag(ThroneSystem.CastDownFlag)); Assert.True(Eligible(w, "cast_down")); Assert.Contains(ThroneSystem.DeposedFlag, me.Flags);
        Assert.False(w.IsDynastyDead); Assert.True(me.Alive); Assert.Equal(me.Id, w.ProtagonistId);
        // and the fallen house may be the next pretender, when the last one is forgotten
        w.Turn += ThroneSystem.RestYears; Hulls(w, mine, 9);
        Assert.Null(ThroneSystem.RiseRefusal(w, mine, Content.Titles));
    }

    [Fact]
    public void AThrone_DoesNotStandEmpty_AndOutmatchingTheThroneIsWhatASeizureAsks()
    {
        var (tc, w, me, mine, throne, duke) = Start(); Hulls(w, throne, 40); Hulls(w, duke, 41); duke.Title = Title.Duke;
        tc.Fleets.DeclareWar(w, duke.Id, throne.Id); w.Wars.First(x => x.Between(duke.Id, throne.Id)).AddScore(duke.Id, 6);
        Assert.True(ThroneSystem.Hold(w) >= ThroneSystem.ShakenAt, string.Join("; ", ThroneSystem.HoldParts(w).Select(x => x.what + " " + x.points))); Assert.Contains("by a quarter", ThroneSystem.SeizeRefusal(w, duke.Id, Content.Titles));   // a throne that holds: a quarter more
        throne.Legitimacy = 5; w.WorldCounters[ThroneSystem.SinceKey] = w.Turn; w.WorldCounters[ThroneSystem.HowKey] = 1; Assert.True(ThroneSystem.Hold(w) < ThroneSystem.ShakenAt);
        Assert.DoesNotContain("outnumber", ThroneSystem.SeizeRefusal(w, duke.Id, Content.Titles) ?? "");                                                 // a shaken one: more at all
        // the house on the throne loses its title and nobody is enthroned: the strongest house of rank takes it
        var (tc2, w2, me2, mine2, throne2, duke2) = Start(); Hulls(w2, duke2, 12); throne2.Title = "landless";
        Assert.Null(ThroneSystem.Emperor(w2)); w2.Turn++; ThroneSystem.Tick(w2, Content.Titles, tc2.Fleets);
        Assert.NotNull(ThroneSystem.Emperor(w2)); Assert.NotEqual(mine2.Id, ThroneSystem.Emperor(w2)!.Id); Assert.True(w2.IsImperium);
    }
}
