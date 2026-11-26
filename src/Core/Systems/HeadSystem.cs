using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Who leads a house other than the player's (docs/HOUSEHOLD.md, "Who leads another house"). The owner's rule, kept simple:
/// the eldest grown person of the house leads it, and stays its head until death. A head can be challenged for good reason:
/// someone of the house stands well above them in rank, or the head is old and failing. No dice: a hash of the house and
/// the year decides whether a challenge that has a reason is made this year.
/// </summary>
public static class HeadSystem
{
    /// <summary>A head this old can be asked to stand aside; this old and in poor health, or simply very old, has "good reason" against them.</summary>
    public const int OldAt = 65, VeryOldAt = 78;
    /// <summary>How many ranks above the head a challenger must stand to challenge on merit.</summary>
    public const int RankLead = 2;
    /// <summary>A challenge that has a reason is made in one year of this many.</summary>
    public const int ChallengeOneIn = 4;
    public const string ChallengesKey = "heads_challenged";

    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static bool Of(Character c, House h) => c.Alive && c.HouseId == h.Id;

    /// <summary>The eldest grown person of the house; a child only when it has no grown person left. Ties go to the lower id. One its
    /// head struck from the line (docs/HOUSEHOLD.md) leads only when nobody else is left.</summary>
    public static Character? Eldest(World w, House h)
    {
        Character? best = null;
        foreach (var c in w.Characters.Values)
        {
            if (!Of(c, h)) continue;
            if (best == null) { best = c; continue; }
            bool struck = DisownSystem.IsStruck(c), bestStruck = DisownSystem.IsStruck(best);
            if (struck != bestStruck) { if (!struck) best = c; continue; }
            bool grown = c.Age >= DynastySystem.AgeOfMajority, bestGrown = best.Age >= DynastySystem.AgeOfMajority;
            if (grown != bestGrown) { if (grown) best = c; continue; }
            if (c.Age > best.Age || (c.Age == best.Age && string.CompareOrdinal(c.Id, best.Id) < 0)) best = c;
        }
        return best;
    }

    /// <summary>The head the house has: the one it is recorded as having while they live and are of it, else its eldest.</summary>
    public static Character? HeadOf(World w, House h) =>
        h.HeadId.Length > 0 && w.Characters.TryGetValue(h.HeadId, out var c) && Of(c, h) ? c : Eldest(w, h);

    /// <summary>Why this head could be challenged by this person, in words, or null when there is no good reason.</summary>
    public static string? Cause(Character head, Character challenger)
    {
        if (challenger.Id == head.Id || challenger.Age < DynastySystem.AgeOfMajority) return null;
        if (challenger.CareerRank >= head.CareerRank + RankLead) return "stands well above them in rank";
        bool failing = head.Age >= VeryOldAt || (head.Age >= OldAt && head.Health < LifeSystem.Frail);
        if (failing && challenger.Age < OldAt) return head.Age >= VeryOldAt ? "the head is very old" : "the head is old and failing";
        return null;
    }

    /// <summary>Whoever of the house has the best reason to challenge its head: the highest in rank, then the elder.</summary>
    public static (Character who, string why)? Challenger(World w, House h, Character head)
    {
        Character? best = null; string? why = null;
        foreach (var c in w.Characters.Values)
        {
            if (!Of(c, h) || DisownSystem.IsStruck(c)) continue;
            var cause = Cause(head, c); if (cause == null) continue;
            if (best == null || c.CareerRank > best.CareerRank || (c.CareerRank == best.CareerRank && (c.Age > best.Age || (c.Age == best.Age && string.CompareOrdinal(c.Id, best.Id) < 0)))) { best = c; why = cause; }
        }
        return best != null ? (best, why!) : null;
    }

    /// <summary>The year's turn: a house whose head has died takes its eldest; a head with a reason against them may be made to stand aside.</summary>
    public static void Tick(World w)
    {
        string playerHouse = w.Protagonist?.HouseId ?? "";
        InheritedRegard.Tick(w);   // what was handed down at a succession fades (docs/LIFE.md, "Friends and enemies handed down")
        foreach (var h in w.Houses.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList())
        {
            if (h.Id == playerHouse || Nation.IsHouse(h.Id)) continue;
            bool had = h.HeadId.Length > 0; var old = had && w.Characters.TryGetValue(h.HeadId, out var o) ? o : null;
            var head = HeadOf(w, h); if (head == null) { h.HeadId = ""; continue; }
            if (head.Id != h.HeadId)
            {
                h.HeadId = head.Id;
                if (had) { w.Log($"{head.Name} now leads House {h.Name}, as the eldest of it."); Passed(w, h, old, head, $"{head.Name} now leads House {h.Name}, as the eldest of it."); }
                continue;
            }
            if (Challenger(w, h, head) is not { } ch || Hash($"{h.Id}:{w.Turn}:head") / 11 % ChallengeOneIn != 0) continue;
            h.HeadId = ch.who.Id; w.WorldCounters[ChallengesKey] = w.Counter(ChallengesKey) + 1;
            string line = ch.why == "stands well above them in rank"
                ? $"{head.Name} stands aside as head of House {h.Name}: {ch.who.Name} stands well above them in rank, and the house would have it so."
                : $"{head.Name} stands aside as head of House {h.Name}: {ch.why}, and {ch.who.Name} leads it now.";
            w.Log(line); Passed(w, h, head, ch.who, line);
        }
    }

    /// <summary>The old head's friends and enemies go with the house, and the new head takes up the old one's view of others, the
    /// player among them (docs/LIFE.md, "Friends and enemies handed down"). The player is told when it is a house they have had to do
    /// with, and what the new head now thinks of them.</summary>
    private static void Passed(World w, House h, Character? old, Character now, string line)
    {
        if (old == null) return;
        var made = InheritedRegard.Pass(w, old, now);
        if (w.Protagonist is not { } p) return;
        int was = w.GetRelationship(old.Id, p.Id)?.Disposition ?? 0;
        if (was != 0 || w.Dealings.Any(d => d.FromHouseId == h.Id || d.ToHouseId == h.Id)) w.Tell("info", line + InheritedRegard.OfYou(w, made, old, now), "houses", h.Id);
    }
}
