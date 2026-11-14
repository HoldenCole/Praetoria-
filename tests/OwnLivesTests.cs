using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The player is one person, not a house (GDD §18 "people, not puppets"; docs/PROCESS.md): kin court for themselves and
/// ask the head's blessing, have a view of a match arranged for them, keep their own marriages, and take up the house's empty
/// posts without being assigned.</summary>
public class OwnLivesTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, Character me) Start(string scenario = "the_new_man", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        return (tc, w, w.Protagonist!);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static void Year(TurnController tc) { tc.EndTurn(); tc.BeginTurn(); }
    private static Character Kin(World w, Character me, string id = "test_kin", string sex = "female", params string[] natures)
    {
        var k = new Character { Id = id, Name = "Test Kin", HouseId = me.HouseId, Age = 22, Sex = sex, CareerTrack = "law", CareerRank = 1 };
        foreach (var n in natures) k.NatureTraits.Add(n);
        k.Skills["charisma"] = 2; w.Characters[k.Id] = k; w.House(me.HouseId)!.Members.Add(k.Id); w.Relationship(k.Id, me.Id).Disposition = 30;
        return k;
    }
    private static Character MatchFor(World w, Character k) => CourtshipSystem.Prospects(w, k).Select(x => x.person).First(c => c.Age >= 16);

    [Fact]
    public void One_of_the_house_courts_for_themselves_and_asks_the_heads_blessing()
    {
        var (tc, w, me) = Start(); var kin = Kin(w, me); var t = MatchFor(w, kin);
        var c = CourtshipSystem.Begin(w, kin, t, own: true); c.Favour = 40; c.Consent = 70; int regard = w.Relationship(kin.Id, me.Id).Disposition;
        Assert.True(c.Own); Assert.Equal(100, c.Wish); Assert.True(CourtshipSystem.IsKinSuit(w, c));
        Assert.False(Do(w, new WooCommand(me.Id, c.Id, CourtshipSystem.ActTime)));   // their courting is their own affair
        Year(tc); c = w.Courtships.Single(x => x.Id == c.Id); c.RivalId = ""; Year(tc); c.RivalId = ""; Assert.True(c.Favour > 40, "the suit advanced with nothing done for it"); int f = c.Favour;
        Year(tc); c.RivalId = ""; Assert.True(c.Favour > f); Assert.True(c.Consent >= 68, "a suit of their own does not decay for want of the head's attention");
        for (int i = 0; i < 4 && c.AskingSince < 0; i++) { Year(tc); c.RivalId = ""; }
        Assert.True(c.AskingSince >= 0, $"favour {c.Favour}: they should have asked"); Assert.Contains(w.History, h => h.Text.Contains("leave to marry"));
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose)));   // the blessing, and the family's yes
        Assert.Equal(t.Id, DynastySystem.Spouse(w, kin)?.Id); Assert.Equal(CourtshipSystem.Love, w.MarriageOf(kin.Id)!.Kind);
        Assert.True(w.Relationship(kin.Id, me.Id).Disposition > regard);
        Assert.False(Do(w, new MarriageCommand(me.Id, kin.Id, MarriageCommand.Divorce)));   // their marriage is their own
    }

    [Fact]
    public void Kin_begin_suits_of_their_own_only_where_the_family_has_wills_of_its_own()
    {
        bool Began(double autonomy)
        {
            var (tc, w, me) = Start(); w.FamilyAutonomy = autonomy; var kin = Kin(w, me); kin.Ambition = "win_a_great_love"; kin.Skills["charisma"] = 5;
            for (int i = 0; i < 15; i++)
            {
                foreach (var s in w.Courtships.Where(x => x.Incoming && x.TargetId == kin.Id).ToList()) w.Courtships.Remove(s);   // keep her free of others' suits
                Year(tc); if (w.IsDynastyDead) break;
                if (w.History.Any(h => h.Text.StartsWith("Test Kin has begun, of their own accord"))) return true;
            }
            return false;
        }
        Assert.True(Began(1.0)); Assert.False(Began(0.0));
    }

    [Fact]
    public void Forbidding_a_match_is_obeyed_by_the_loyal_and_defied_by_the_wilful()
    {
        var (tc, w, me) = Start(); var loyal = Kin(w, me, "loyal_kin", "female", "Loyal"); var t = MatchFor(w, loyal);
        var c = CourtshipSystem.Begin(w, loyal, t, own: true); c.Favour = 90; int regard = w.Relationship(loyal.Id, me.Id).Disposition;
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.BreakOff)));
        Assert.DoesNotContain(c, w.Courtships); Assert.Equal(regard - 8, w.Relationship(loyal.Id, me.Id).Disposition); Assert.True(w.HasFlag(CourtshipSystem.ForbiddenFlag));

        var (tc2, w2, me2) = Start(); var wilful = Kin(w2, me2, "wilful_kin", "female", "Proud"); var t2 = MatchFor(w2, wilful); var mine = w2.House(me2.HouseId)!;
        var c2 = CourtshipSystem.Begin(w2, wilful, t2, own: true); c2.Favour = 90; int legit = mine.Legitimacy; regard = w2.Relationship(wilful.Id, me2.Id).Disposition;
        Assert.True(Do(w2, new SettleSuitCommand(me2.Id, c2.Id, SettleSuitCommand.BreakOff)));
        Assert.True(c2.Forbidden); Assert.Contains(c2, w2.Courtships); Assert.Equal(regard - 20, w2.Relationship(wilful.Id, me2.Id).Disposition);
        tc2.EndTurn();
        Assert.Equal(t2.Id, DynastySystem.Spouse(w2, wilful)?.Id); Assert.True(w2.HasFlag(CourtshipSystem.KinElopedFlag)); Assert.True(mine.Legitimacy < legit + 6);
        _ = tc;
    }

    [Fact]
    public void A_blessing_never_given_is_an_answer_too()
    {
        var (tc, w, me) = Start(); var kin = Kin(w, me, "patient_kin", "female", "Honorable"); var t = MatchFor(w, kin);
        var c = CourtshipSystem.Begin(w, kin, t, own: true); c.Favour = 70; c.Consent = 0;
        bool gaveUp = false;   // the chronicle keeps only recent years, so look as the years pass
        for (int i = 0; i <= CourtshipSystem.BlessingWaitYears + 1 && w.Courtships.Contains(c); i++) { Year(tc); c.RivalId = ""; gaveUp |= w.History.Any(h => h.Text.Contains("has given it up")); }
        Assert.DoesNotContain(c, w.Courtships); Assert.Null(DynastySystem.Spouse(w, kin));
        Assert.True(gaveUp);   // (it costs the head their regard that year; a family's regard mends with time, so it is not measured here)
    }

    [Fact]
    public void A_match_arranged_against_their_inclination_is_resented_or_refused()
    {
        var (_, w, me) = Start(); var kin = Kin(w, me, "meek_kin", "female", "Honorable"); var t = MatchFor(w, kin);
        Assert.True(Do(w, new CourtCommand(me.Id, kin.Id, t.Id))); var c = w.Courtships.Single(x => x.SuitorId == kin.Id);
        Assert.False(c.Own); c.Wish = 10; c.Favour = 40; c.Consent = 80; int regard = w.Relationship(kin.Id, me.Id).Disposition;
        Assert.True(Do(w, new SettleSuitCommand(me.Id, c.Id, SettleSuitCommand.Propose)));
        Assert.Equal(t.Id, DynastySystem.Spouse(w, kin)?.Id); Assert.Equal(regard - 20, w.Relationship(kin.Id, me.Id).Disposition); Assert.Equal(CourtshipSystem.Arranged, w.MarriageOf(kin.Id)!.Kind);

        var (_, w2, me2) = Start(); var proud = Kin(w2, me2, "proud_kin", "female", "Proud"); var t2 = MatchFor(w2, proud);
        Do(w2, new CourtCommand(me2.Id, proud.Id, t2.Id)); var c2 = w2.Courtships.Single(x => x.SuitorId == proud.Id); c2.Wish = 10; c2.Favour = 40; c2.Consent = 80;
        var (ok, _, why) = CourtshipSystem.Answer(w2, c2, CourtshipSystem.Customary, ""); Assert.False(ok); Assert.Contains("will not be married", why);
    }

    [Fact]
    public void An_empty_post_of_the_house_is_taken_up_by_whoever_is_fit_and_willing()
    {
        var (tc, w, me) = Start("the_old_lion"); var house = w.House(me.HouseId)!;
        var fleet = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal).First();
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id)) f.AdmiralId = f.Id == fleet.Id ? "" : me.Id + "_x";
        var officer = Kin(w, me, "officer_kin", "male"); officer.CareerTrack = "military"; officer.Skills["tactics"] = 9; officer.Skills["gunnery"] = 9;
        tc.EndTurn(); Assert.Equal("", fleet.AdmiralId);   // the head has a year to choose
        tc.BeginTurn(); tc.EndTurn(); Assert.Equal(officer.Id, fleet.AdmiralId); Assert.Contains(w.History, h => h.Text.Contains("has taken command of"));

        var (tc2, w2, me2) = Start("the_old_lion"); w2.FamilyAutonomy = 0; var fleet2 = w2.Fleets[fleet.Id]; fleet2.AdmiralId = "";
        var officer2 = Kin(w2, me2, "officer_kin", "male"); officer2.CareerTrack = "military"; officer2.Skills["tactics"] = 9;
        tc2.EndTurn(); tc2.BeginTurn(); tc2.EndTurn(); Assert.Equal("", fleet2.AdmiralId);   // a deferential family waits to be told
    }
}
