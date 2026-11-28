using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The head's word to one of the house who stood alone and lost (the_one_you_did_not_speak_for/say_you_will_next_time: "Say you will
/// speak for them next time, and mean it", and they "would hold {self.name} to that"; docs/events/votes.md, "Promises", round 2). It is
/// remembered (<see cref="SpeakKey"/>, the year it was given), the head is reminded when that relative next stands, and it is judged at
/// that count of the ladder: spoken for among the houses (<see cref="Candidacy.Backing"/>) or a canvass paid for (<see cref="CampaignOf"/>),
/// it is kept; neither, broken, with what the word said it would cost, written into their life. It lapses, with a notice, if they die,
/// leave the house, or do not stand within <see cref="SpeakYears"/> years (two counts of the ladder: whether they stand is their own
/// affair, and a word for a candidacy that never comes is owed nothing), or when the ladder ends under a throne. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    /// <summary>On the relative the report named, set by the choice and made the word as it is made (VoteMarks.Bargains, <see cref="SpeakWord"/>).</summary>
    public const string SpeakChar = "promised_to_be_spoken_for";
    public const string SpeakPrefix = "speak_promised:", SpeakRemindedPrefix = "speak_reminded:";
    /// <summary>What the word is worth either way, as a word on a vote is (DealingsSystem.PledgeKeptRegard, PledgeBrokenRegard), and how long it waits.</summary>
    public const int SpeakKept = DealingsSystem.PledgeKeptRegard, SpeakBroken = DealingsSystem.PledgeBrokenRegard, SpeakYears = 10;
    public static string SpeakKey(string characterId) => SpeakPrefix + characterId;
    private static string SpeakRemindedKey(string characterId) => SpeakRemindedPrefix + characterId;
    public static bool SpeakPromised(World w, string characterId) => w.WorldCounters.ContainsKey(SpeakKey(characterId));

    /// <summary>The word is given: remembered, and the head told what it binds them to.</summary>
    public static void SpeakWord(World w, Character kin)
    {
        var p = w.Protagonist; if (p == null || kin.HouseId != p.HouseId || kin.Id == p.Id || !kin.Alive) return;
        w.WorldCounters[SpeakKey(kin.Id)] = Math.Max(1, w.Turn); w.WorldCounters.Remove(SpeakRemindedKey(kin.Id));
        w.Tell("warn", $"You told {kin.Name} you would speak for them the next time they stand. When they stand for an office of the ladder again, speak for them among the houses or put coin behind their canvass before the count: "
            + $"do, and they think {SpeakKept:+0;-0} of you; let the count come with neither, and they think {-SpeakBroken:+0;-0} of you, and it is written in their life. Whether they stand is their own affair: if they have not within {Counted.Years(SpeakYears)}, the word lapses, and nothing is owed.", "senate:ladder", kin.Id);
    }

    /// <summary>Each year, after the house's people have put their names down: a word that can no longer be kept lapses, with a notice; the
    /// head is reminded, once, when the relative's next candidacy is down.</summary>
    private static void SpeakDue(World w, OfficeCatalog offices, Character head)
    {
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(SpeakPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            string id = key[SpeakPrefix.Length..]; var kin = w.Char(id); int since = w.Counter(key);
            var cd = w.Candidacies.FirstOrDefault(c => c.CharacterId == id);
            string? lapse = kin is not { Alive: true } ? "The one you promised to speak for at their next election is dead, and your word goes with them."
                : kin.HouseId != head.HouseId ? $"{kin.Name} is no longer of your house, and your word to speak for them at their next election lapses."
                : cd == null && w.Turn - since >= SpeakYears ? $"{kin.Name} has not stood for office in {Counted.Years(SpeakYears)} since you promised to speak for them, so the word lapses, and nothing is owed either way." : null;
            if (lapse != null) { SpeakForget(w, id); w.Tell("info", lapse, "senate:ladder", id); continue; }
            if (cd == null || w.Counter(SpeakRemindedKey(id)) == w.Turn + NextIn(w, offices)) continue;
            int count = w.Turn + NextIn(w, offices); w.WorldCounters[SpeakRemindedKey(id)] = count;
            w.Tell("warn", $"{kin!.Name} stands for {offices.Rung(cd.RungId)?.Name.ToLowerInvariant() ?? "office"} at the count in year {count}, and you told them you would speak for them. Speak for them among the houses, or put coin behind their canvass, before then: "
                + $"they think {SpeakKept:+0;-0} of you for it; let the count come with neither, and they think {-SpeakBroken:+0;-0} of you, and it is written in their life.", "senate:ladder", id);
        }
    }
    private static void SpeakForget(World w, string id) { w.WorldCounters.Remove(SpeakKey(id)); w.WorldCounters.Remove(SpeakRemindedKey(id)); }

    /// <summary>At the count of the ladder, the relative stood: spoken for or canvassed, the word is kept; neither, broken. True if broken.</summary>
    private static bool SpeakJudged(World w, OfficeCatalog offices, Character c, string rungId)
    {
        var p = w.Protagonist; if (p == null || !SpeakPromised(w, c.Id)) return false;
        SpeakForget(w, c.Id); string rung = offices.Rung(rungId)?.Name.ToLowerInvariant() ?? "office";
        bool kept = (w.Candidacies.FirstOrDefault(cd => cd.CharacterId == c.Id)?.Backing ?? 0) > 0 || CampaignOf(w, c.Id) != null;
        var r = w.Relationship(c.Id, p.Id); r.Disposition = Math.Clamp(r.Disposition + (kept ? SpeakKept : -SpeakBroken), -100, 100);
        if (kept)
        {
            w.Tell("good", $"You kept your word to {c.Name}: you spoke for them when they stood for {rung}. They think {SpeakKept:+0;-0} of you for it.", "senate:ladder", c.Id);
            w.Log($"{p.Name} spoke for {c.Name} at the count for {rung}, as promised.");
            return false;
        }
        LifeSystem.Note(w, c, "senate:ladder", $"Stood for {rung}; {p.Name} had promised to speak for them, and did not.");
        w.Tell("warn", $"You told {c.Name} you would speak for them the next time they stood, and they stood for {rung} with neither your word among the houses nor your coin behind their canvass. They think {-SpeakBroken:+0;-0} of you, and it is written in their life.", "senate:ladder", c.Id);
        w.Log($"{c.Name} stood for {rung} without a word from {p.Name}, who had promised one.");
        return true;
    }

    /// <summary>The ladder has ended under a throne (<see cref="Active"/> is false): before the candidacies and the canvasses go, the player is
    /// told, once, what will now never be counted: their own people standing, the coin behind a canvass (named, with what it cost: it bought
    /// votes at a count that will not be held), the votes a report gave a candidate for the next count (the doors, the coin, the good
    /// grace), and the word to speak for one of the house. Nothing is said when nothing was waiting.</summary>
    private static void LadderEnds(World w, OfficeCatalog offices)
    {
        SpeakEnds(w);
        var p = w.Protagonist; if (p == null) return; string mine = p.HouseId;
        var lost = w.Campaigns.Where(f => !f.Counted && f.HouseId == mine).OrderBy(f => f.CharacterId, StringComparer.Ordinal).ToList();
        var standing = w.Candidacies.Where(cd => w.Char(cd.CharacterId)?.HouseId == mine).ToList();
        bool marks = false;
        foreach (var c in w.Characters.Values) marks |= c.Flags.Remove(DoorsChar) | c.Flags.Remove(CoinChar) | c.Flags.Remove(GraceChar);
        if (lost.Count == 0 && standing.Count == 0 && !marks) return;
        string coin = lost.Count == 0 ? "" : $" The coin put behind {string.Join(" and ", lost.Select(f => (f.CharacterId == p.Id ? "your own canvass" : (w.Char(f.CharacterId)?.Name ?? "a canvass") + "'s canvass")))} ({Money.Credits(lost.Sum(f => f.Spent))}) bought votes at a count that will not be held, and is gone.";
        w.Tell("info", $"A throne stands, and the magistrates will not be elected again: the ladder's counts are over{(standing.Count > 0 ? ", and nobody of your house stands at one now" : "")}.{coin}{(marks ? " The votes the doors, the coin and good grace had won for the next count count for nothing." : "")}", "senate:ladder");
    }

    /// <summary>There is no ladder under a throne: a word to speak for one of the house at its next count can never be kept, and goes.</summary>
    private static void SpeakEnds(World w)
    {
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(SpeakPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            string id = key[SpeakPrefix.Length..]; SpeakForget(w, id);
            if (w.Char(id) is { Alive: true } kin) w.Tell("info", $"A throne stands, and the magistrates will not be elected again: your word to speak for {kin.Name} at their next election lapses, and nothing is owed.", "senate:ladder", id);
        }
    }
}
