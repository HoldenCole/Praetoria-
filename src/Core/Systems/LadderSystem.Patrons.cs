using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Patrons (docs/LADDER.md, "A patron, and a falling out"). The one above a junior did one thing with what they thought of them:
/// spoke for them once at each rank. Now a senior who thinks well of a junior and has had three good years' work of them TAKES
/// THEM UP: they are that junior's patron from then on, wherever either is posted, and when the patron rises (a step in rank, a
/// magistracy, an office of the Reach) the junior is TAKEN ALONG: they learn their trade faster for it and are paid for the place.
/// And a junior the one above has come to think ill of FALLS OUT with them: the patron is lost, the years under them count for
/// nothing, and that senior will not be above them again. The juniors of other houses have all of it too, by a reckoning of
/// good and bad years in place of the choices the player's people make. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const int PatronAt = StandingAt, FallOutAt = -20, TakenAlongCredits = 3, TakenAlongLearns = 2, GoodYearRegard = 3, BadYearRegard = -6, ReckonedCap = 40;
    public const string PatronFlag = "kin_has_a_patron", FellOutFlag = "kin_fell_out_above", TakenAlongFlag = "kin_taken_along";
    public const string PatronChar = "has_a_patron", FellOutChar = "fell_out_above", TakenAlongChar = "taken_along";
    public const string PatronsCounter = "patrons_found", FallingsOutCounter = "fallings_out", TakenAlongCounter = "taken_along", SpokenCounter = "spoken_for";
    private static string PostKey(string juniorId) => "patron_post:" + juniorId;
    public static bool FellOut(Character junior, Character above) => junior.Life?.Marks.Contains("fell_out:" + above.Id) == true;
    /// <summary>What a senior's post amounts to: their rank, and whether they hold a magistracy or an office of the Reach besides.</summary>
    public static int PostOf(World w, Character c) => c.CareerRank * 2 + (w.Magistrates.Any(m => m.CharacterId == c.Id) || w.Offices.Any(o => o.CharacterId == c.Id) ? 1 : 0);
    private static string TradeOf(Character c) => c.CareerTrack switch { "law" => "law", "stewardship" => "administration", "military" => "tactics", _ => "" };
    public static Character? PatronOf(World w, Character c) =>
        c.PatronId.Length > 0 && w.Char(c.PatronId) is { Alive: true } pat && PolitySystem.InReach(w, pat.HouseId) && !pat.Flags.Contains(LifeSystem.RetiredChar) ? pat : null;

    /// <summary>After a year's work is seen by the one above: a falling out if they have come to think ill of the junior; a patron
    /// if they think well of them and have had three good years of them.</summary>
    private static void Patronage(World w, Character above, Character who)
    {
        if (above.Id == who.Id) return;
        int regard = w.GetRelationship(above.Id, who.Id)?.Disposition ?? 0; bool ours = who.HouseId == w.Protagonist?.HouseId, me = who.Id == w.ProtagonistId;
        who.Life ??= new Life();
        if (regard <= FallOutAt && (who.PatronId == above.Id || w.Counter(UnderKey(who.Id, above.Id)) > 0) && who.Life.Marks.Add("fell_out:" + above.Id))
        {
            bool was = who.PatronId == above.Id; if (was) { who.PatronId = ""; w.WorldCounters.Remove(PostKey(who.Id)); }
            w.WorldCounters.Remove(UnderKey(who.Id, above.Id)); Bump(w, FallingsOutCounter);
            LifeSystem.Note(w, who, "office", was ? $"Fell out with {above.Name}, who had been their patron." : $"Fell out with {above.Name}, who was above them.");
            if (ours)
            {
                w.Log(was ? $"{who.Name} and {above.Name} have fallen out. {above.Name} had been {who.Name}'s patron; the years under them count for nothing now." : $"{who.Name} has fallen out with {above.Name}, who was above them. Somebody else will have to notice their work.");
                if (!me) { who.Flags.Add(FellOutChar); Mark(w, FellOutFlag); }
            }
            return;
        }
        if (who.PatronId.Length > 0 || regard < PatronAt || w.Counter(UnderKey(who.Id, above.Id)) < SpokenAfter || above.CareerRank <= who.CareerRank || FellOut(who, above)) return;
        who.PatronId = above.Id; w.WorldCounters[PostKey(who.Id)] = PostOf(w, above); Bump(w, PatronsCounter);
        LifeSystem.Note(w, who, "office", $"Was taken up by {above.Name}.");
        if (!ours) return;
        w.Log($"{above.Name} takes {who.Name} up. Where {above.Name} goes from now on, {who.Name} goes too.");
        if (!me) { who.Flags.Add(PatronChar); Mark(w, PatronFlag); }
    }

    /// <summary>Each year: a patron who has risen takes their people along; a patron who is dead, retired or gone from the Reach
    /// is no patron; and the juniors of other houses have their year reckoned.</summary>
    private static void Patrons(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var c in w.LivingCharacters().Where(c => c.PatronId.Length > 0).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            var pat = PatronOf(w, c);
            if (pat == null) { c.PatronId = ""; w.WorldCounters.Remove(PostKey(c.Id)); continue; }
            int post = PostOf(w, pat), was = w.Counter(PostKey(c.Id)); w.WorldCounters[PostKey(c.Id)] = post;
            if (was == 0 || post <= was || !AtWork(c)) continue;
            if (TradeOf(c).Length > 0) EducationSystem.Gain(w, c, TradeOf(c), TakenAlongLearns);
            c.Purse += TakenAlongCredits; Bump(w, TakenAlongCounter);
            LifeSystem.Note(w, c, "office", $"Went with {pat.Name} to a new post.");
            if (c.HouseId != mine) continue;
            w.Log($"{pat.Name} has risen, and takes {c.Name} along to the new post.");
            if (c.Id != w.ProtagonistId) { c.Flags.Add(TakenAlongChar); Mark(w, TakenAlongFlag); }
        }
        // Other houses' juniors make no choices: four years in six their work is seen and found good, one is nothing, one is bad.
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId != mine && PolitySystem.InReach(w, c.HouseId) && JuniorKind(w, c).Length > 0).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            var above = Above(w, c); if (above == null || above.Id == c.Id) continue;
            uint roll = Hash($"junior:{c.Id}:{w.Turn}") % 6; var r = w.Relationship(above.Id, c.Id);
            if (roll < 4) { if (r.Disposition < ReckonedCap) r.Disposition += GoodYearRegard; w.WorldCounters[UnderKey(c.Id, above.Id)] = w.Counter(UnderKey(c.Id, above.Id)) + 1; }
            else if (roll == 5) r.Disposition = Math.Max(-100, r.Disposition + BadYearRegard);
            Patronage(w, above, c); Speak(w, above, c);
        }
    }
}
