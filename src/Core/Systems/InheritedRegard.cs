using Praetoria.Core.Presentation;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Friends and enemies handed down (docs/LIFE.md). When the head of any house gives way to an heir, by death, by laying the house down
/// in life or by standing aside, the old head's ties go with the house. Those who thought well of the old head think the better of a
/// grown heir, by a warmth that fades over ten years; a child was never seen at the parent's side, and takes on no friendship. Those
/// who thought ill of the old head think the worse of the heir, child or grown, by a coldness that fades the same way; but one who is
/// vengeful by nature is the heir's enemy at once, and one who is just or loyal lets it go. The head of any house but the player's also
/// takes up the old head's view of others by the same rule, its own nature deciding an enmity: this is what another house's new head
/// thinks of you. What the player thinks is the player's own, and nothing of it is handed down. People of the house itself are left
/// to the house's own rules. RNG-free.
/// </summary>
public static class InheritedRegard
{
    /// <summary>A regard this good of the old head is a friendship that passes on, and this bad an enmity that does. An old friend or
    /// an old enemy of the old head by the years (docs/LIFE.md, "Old friends and old enemies") counts whatever the figure.</summary>
    public const int WellAt = 30, IllAt = -30;
    /// <summary>What a friendship that passes on adds to the regard, and what an enmity takes from it.</summary>
    public const int Warmth = 20, Coldness = 20;
    /// <summary>The years over which a warmth or a coldness handed down fades to nothing, a tenth of it each year.</summary>
    public const int FadeYears = 10;
    public const string Friend = "friend", Enemy = "enemy", Vengeful = "vengeful", LetGo = "let_go", Unseen = "unseen";
    public const string SuccessionsKey = "regard_handed_successions", FriendsKey = "regard_handed_friends", EnemiesKey = "regard_handed_enemies",
        VengefulKey = "regard_handed_vengeful", LetGoKey = "regard_handed_let_go", UnseenKey = "regard_handed_unseen";
    /// <summary>The part of the old head's regard for the player that another house's new head started with before this rule, kept
    /// only for <see cref="Disabled"/>.</summary>
    public const int HalfBefore = 50;

    /// <summary>For a probe's game played without the rule, to count what it changes (src/Tools, "heirs"). Nothing in the game sets
    /// it. While it is set nothing is handed down, and another house's new head starts with half of what the old one thought of the
    /// player when they had no view of their own, as the game did before.</summary>
    public static bool Disabled { get; set; }

    private static int Move(World w, Character from, Character to, int by)
    {
        var r = w.Relationship(from.Id, to.Id); int before = r.Disposition; r.Disposition = Math.Clamp(before + by, -100, 100); return r.Disposition - before;
    }
    /// <summary>The nature that decides what an enemy of the old head does: vengeful first, then just, then loyal.</summary>
    public static string NatureOf(Character c) => c.NatureTraits.Contains("Vengeful") ? "Vengeful" : c.NatureTraits.Contains("Just") ? "Just" : c.NatureTraits.Contains("Loyal") ? "Loyal" : "";

    /// <summary>The old head's ties go to the heir. Called once at every succession: the player's (DynastySystem.HandOver) and every
    /// other house's (HeadSystem). Returns every tie handed down, kept or not.</summary>
    public static List<InheritedTie> Pass(World w, Character? old, Character heir)
    {
        var made = new List<InheritedTie>();
        if (old == null || old.Id == heir.Id) return made;
        string you = w.ProtagonistId; bool yours = heir.Id == you;
        if (Disabled) { HalfAsBefore(w, old, heir, yours); return made; }
        bool grown = heir.Age >= DynastySystem.AgeOfMajority;
        bool Outside(Character c) => c.Alive && !World.Outsider(c.HouseId) && c.Id != heir.Id && c.Id != old.Id && c.HouseId != heir.HouseId && c.HouseId != old.HouseId;
        bool ByYears(Dictionary<string, int>? ties, string id) => ties != null && ties.TryGetValue(id, out int since) && w.Turn - since >= LifeSystem.OldTieYears;
        // what others thought of the old head, now of the heir; and, for any heir but the player, what the old head thought of others
        var ofOld = w.Relationships.Where(r => r.ToId == old.Id).OrderBy(r => r.FromId, StringComparer.Ordinal).ToList();
        var oldsView = yours ? new List<Relationship>() : w.Relationships.Where(r => r.FromId == old.Id).OrderBy(r => r.ToId, StringComparer.Ordinal).ToList();
        foreach (var r in ofOld)
        {
            if (r.FromId == you || w.Char(r.FromId) is not { } x || !Outside(x)) continue;
            int sign = r.Disposition >= WellAt || ByYears(old.Life?.Friends, x.Id) ? 1 : r.Disposition <= IllAt || ByYears(old.Life?.Enemies, x.Id) ? -1 : 0;
            if (sign != 0) made.Add(Hand(w, x, heir, old, heir, sign, r.Disposition, grown));
        }
        foreach (var r in oldsView)
        {
            if (w.Char(r.ToId) is not { } y || !Outside(y)) continue;
            int sign = r.Disposition >= WellAt ? 1 : r.Disposition <= IllAt ? -1 : 0;
            if (sign != 0) made.Add(Hand(w, heir, y, old, heir, sign, r.Disposition, grown));
        }
        w.WorldCounters[SuccessionsKey] = w.Counter(SuccessionsKey) + 1;
        foreach (var (kind, key) in new[] { (Friend, FriendsKey), (Enemy, EnemiesKey), (Vengeful, VengefulKey), (LetGo, LetGoKey), (Unseen, UnseenKey) })
        {
            int n = made.Count(t => t.Kind == kind); if (n > 0) w.WorldCounters[key] = w.Counter(key) + n;
        }
        // a warmth that moved nothing (a regard already at the top) is not kept
        w.InheritedTies.AddRange(made.Where(t => t.Kind is not (Friend or Enemy) || t.Left != 0));
        if (yours) Tell(w, old, heir, made);
        return made;
    }

    private static InheritedTie Hand(World w, Character holder, Character of, Character old, Character heir, int sign, int was, bool grown)
    {
        var t = new InheritedTie { FromId = holder.Id, ToId = of.Id, HeirId = heir.Id, OldHeadId = old.Id, HouseId = heir.HouseId, Turn = w.Turn };
        if (sign > 0)
        {
            if (!grown) { t.Kind = Unseen; return t; }   // a child was never seen at the parent's side
            t.Kind = Friend; t.Amount = t.Left = Move(w, holder, of, Warmth); return t;
        }
        t.Nature = NatureOf(holder);
        if (t.Nature == "Vengeful")
        {
            // an enemy at once: as bitter as they were of the old head, and no less than an enemy, and it does not fade
            var r = w.Relationship(holder.Id, of.Id); int before = r.Disposition;
            r.Disposition = Math.Min(before, Math.Min(was, LifeSystem.EnemyAt)); t.Kind = Vengeful; t.Amount = r.Disposition - before;
            if (of.Id == w.ProtagonistId) EnemyOfYours(w, holder, of);
            return t;
        }
        if (t.Nature.Length > 0) { t.Kind = LetGo; return t; }
        t.Kind = Enemy; t.Amount = t.Left = Move(w, holder, of, -Coldness); return t;
    }

    /// <summary>One who takes up the old quarrel at once is an old enemy of the player at once: their enmity is the old head's, and it
    /// is as old (docs/LIFE.md: it counts in the Senate, at the count, in a court and in a match).</summary>
    private static void EnemyOfYours(World w, Character holder, Character you)
    {
        you.Life ??= new Life(); int since = w.Turn - LifeSystem.OldTieYears;
        if (!you.Life.Enemies.TryGetValue(holder.Id, out int had) || had > since) you.Life.Enemies[holder.Id] = since;
        you.Life.Friends.Remove(holder.Id); holder.Flags.Remove(LifeSystem.OldFriendChar); holder.Flags.Add(LifeSystem.OldEnemyChar);
    }

    /// <summary>The rule before this one, for <see cref="Disabled"/> only: another house's new head starts with half of what the old
    /// one thought of the player, when they had no view of their own; the player's own heir inherits nothing.</summary>
    private static void HalfAsBefore(World w, Character old, Character heir, bool yours)
    {
        if (yours || w.Protagonist is not { } p) return;
        int was = w.GetRelationship(old.Id, p.Id)?.Disposition ?? 0;
        if (was != 0 && w.GetRelationship(heir.Id, p.Id) == null) w.Relationship(heir.Id, p.Id).Disposition = was * HalfBefore / 100;
    }

    /// <summary>The year's turn: a warmth or a coldness handed down fades by a tenth of what it was, and is gone in ten years. A tie
    /// that does not fade is kept to be shown while it touches the player, for ten years; one between others is not kept past the
    /// year it was made. Ties with someone who has died are let go.</summary>
    public static void Tick(World w)
    {
        if (w.InheritedTies.Count == 0) return;
        string you = w.ProtagonistId;
        foreach (var t in w.InheritedTies.ToList())
        {
            int years = w.Turn - t.Turn;
            if (w.Char(t.FromId) is not { Alive: true } || w.Char(t.ToId) is not { Alive: true }) { w.InheritedTies.Remove(t); continue; }
            if (t.Kind is Friend or Enemy && t.Left != 0)
            {
                int now = t.Amount * Math.Max(0, FadeYears - years) / FadeYears, gone = t.Left - now;
                if (gone != 0) { var r = w.Relationship(t.FromId, t.ToId); r.Disposition = Math.Clamp(r.Disposition - gone, -100, 100); t.Left = now; }
            }
            bool fading = t.Kind is Friend or Enemy && t.Left != 0, shown = t.ToId == you && years < FadeYears;
            if (!fading && !shown && years > 0) w.InheritedTies.Remove(t);
        }
    }

    // ---------------- in words ----------------

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string Poss(Character c) => c.Sex == "female" ? "her" : c.Sex == "male" ? "his" : "their";
    private static string Pronoun(Character c) => c.Sex == "female" ? "she" : c.Sex == "male" ? "he" : "they";
    private static string Obj(Character c) => c.Sex == "female" ? "her" : c.Sex == "male" ? "him" : "them";
    private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
    private static string Named(World w, Character c) => w.House(c.HouseId) is { } h ? $"{c.Name} of {h.Name}" : c.Name;
    /// <summary>A few names, heads of houses first, and how many more.</summary>
    private static string Names(World w, IEnumerable<InheritedTie> ties, Func<InheritedTie, string> whose)
    {
        var people = ties.Select(t => w.Char(whose(t))).Where(c => c != null).Select(c => c!)
            .OrderByDescending(c => w.House(c.HouseId) is { } h && Economy.HeadOf(w, h)?.Id == c.Id).ThenBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var shown = people.Take(4).Select(c => Named(w, c)).ToList(); int more = people.Count - shown.Count;
        if (more > 0) return string.Join(", ", shown) + $" and {more} more";
        return shown.Count <= 1 ? string.Join("", shown) : string.Join(", ", shown.Take(shown.Count - 1)) + " and " + shown[^1];
    }
    /// <summary>Who the old head was to the heir, from the heir's side: "your father", "your aunt", or the old head's name.</summary>
    private static string WasTo(World w, Character heir, Character old, string whose)
    {
        string kin = Kinship.Of(w, heir, old);
        return kin.StartsWith("your ", StringComparison.Ordinal) ? whose + kin[4..] : old.Name;
    }

    /// <summary>What came down to the heir at a succession, for the player: the title and the words for the old head's friends, and for
    /// their enemies. Empty words when there were none.</summary>
    public static (string FriendsTitle, string Friends, string EnemiesTitle, string Enemies) Summary(World w, Character old, Character heir, IEnumerable<InheritedTie> ties)
    {
        var of = ties.Where(t => t.ToId == heir.Id && t.HeirId == heir.Id).ToList(); string poss = Poss(old), was = WasTo(w, heir, old, "your");
        var friends = of.Where(t => t.Kind == Friend && t.Amount != 0).ToList(); var unseen = of.Where(t => t.Kind == Unseen).ToList();
        var vengeful = of.Where(t => t.Kind == Vengeful).ToList(); var letGo = of.Where(t => t.Kind == LetGo).ToList(); var cold = of.Where(t => t.Kind == Enemy && t.Amount != 0).ToList();
        string Who(InheritedTie t) => t.FromId;
        string f = friends.Count > 0
            ? (friends.Count == 1 ? $"{Names(w, friends, Who)} thinks {Warmth} the better of you for {poss} sake" : $"{Names(w, friends, Who)}. Each thinks {Warmth} the better of you for {poss} sake") + $", and it fades over {Counted.Years(FadeYears)}."
            : unseen.Count > 0 ? $"{Names(w, unseen, Who)} thought well of {Obj(old)}, but you were a child and never seen at {poss} side, and none of it passes to you." : "";
        var parts = new List<string>();
        if (vengeful.Count > 0) parts.Add(vengeful.Count == 1 ? $"{Names(w, vengeful, Who)} is vengeful by nature, and your enemy from this day" : $"{Names(w, vengeful, Who)} are vengeful by nature, and your enemies from this day");
        if (letGo.Count > 0) parts.Add(letGo.Count == 1 ? $"{Names(w, letGo, Who)} is {letGo[0].Nature.ToLowerInvariant()} by nature, and lets it go" : $"{Names(w, letGo, Who)} are just or loyal by nature, and let it go");
        if (cold.Count > 0) parts.Add((cold.Count == 1 ? $"{Names(w, cold, Who)} thinks" : $"{Names(w, cold, Who)} think") + $" {Coldness} the worse of you for {poss} sake, and it fades over {Counted.Years(FadeYears)}");
        string e = parts.Count > 0 ? Cap(string.Join("; ", parts)) + "." : "";
        string ft = was == old.Name ? $"{old.Name}'s friends" : $"{Cap(was)}'s friends";
        return (ft, f, $"{Cap(poss)} enemies", e);
    }

    /// <summary>The player is told at the succession what came down to them.</summary>
    private static void Tell(World w, Character old, Character heir, List<InheritedTie> made)
    {
        var (ft, f, et, e) = Summary(w, old, heir, made);
        string? Subject(string kind) => made.Where(t => t.ToId == heir.Id && t.Kind == kind).Select(t => w.Char(t.FromId)?.HouseId).FirstOrDefault(h => h is { Length: > 0 });
        if (f.Length > 0) w.Tell(made.Any(t => t.Kind == Friend) ? "good" : "info", $"{ft}: {f}", "houses", Subject(Friend) ?? Subject(Unseen) ?? "");
        if (e.Length > 0) w.Tell("warn", $"{et}: {e}", "houses", Subject(Vengeful) ?? Subject(Enemy) ?? Subject(LetGo) ?? "");
    }

    /// <summary>What another house's new head took up of the old head's regard for the player, in a sentence to add to the news that
    /// they lead it now, or "".</summary>
    public static string OfYou(World w, IEnumerable<InheritedTie> made, Character old, Character now)
    {
        var t = made.FirstOrDefault(x => x.FromId == now.Id && x.ToId == w.ProtagonistId); if (t == null) return "";
        return t.Kind switch
        {
            Friend => $" {now.Name} takes up {old.Name}'s good opinion of you: {t.Amount} the better, fading over {Counted.Years(FadeYears)}.",
            Enemy => $" {now.Name} takes up {old.Name}'s quarrel with you: {-t.Amount} the worse, fading over {Counted.Years(FadeYears)}.",
            Vengeful => $" {now.Name} is vengeful by nature, and takes up {old.Name}'s quarrel with you at once: your enemy now.",
            LetGo => $" {now.Name} is {t.Nature.ToLowerInvariant()} by nature, and lets {old.Name}'s quarrel with you go.",
            Unseen => $" {now.Name} is a child, and takes on nothing of {old.Name}'s good opinion of you.",
            _ => ""
        };
    }

    /// <summary>Why <paramref name="holderId"/> thinks as they do of <paramref name="ofId"/>, as far as it was handed down: one line
    /// for each tie, with its kind, for a person's page.</summary>
    public static List<(string Kind, string Text)> Why(World w, string holderId, string ofId)
    {
        var lines = new List<(string, string)>();
        foreach (var t in w.InheritedTies.Where(t => t.FromId == holderId && t.ToId == ofId).OrderBy(t => t.Turn))
        {
            var old = w.Char(t.OldHeadId); var holder = w.Char(holderId); var of = w.Char(ofId); if (old == null || holder == null || of == null) continue;
            string gone = $"gone by year {t.Turn + FadeYears}";
            if (t.ToId == t.HeirId)
            {
                // the heir is the one thought of: what this person thought of the head before them
                string was = WasTo(w, of, old, "your"), poss = Poss(old);
                lines.Add((t.Kind, t.Kind switch
                {
                    Friend => $"Thought well of {was}, and so of you: {Signed(t.Left)} of it still, {gone}.",
                    Enemy => $"Thought ill of {was}, and so of you: {Signed(t.Left)} of it still, {gone}.",
                    Vengeful => $"Was the enemy of {was}, and is vengeful by nature: your enemy since year {t.Turn}, and it does not fade.",
                    LetGo => $"Was the enemy of {was}, and is {t.Nature.ToLowerInvariant()} by nature: lets it go with {Obj(old)}.",
                    Unseen => $"Thought well of {was}, but you were a child and never seen at {poss} side.",
                    _ => ""
                }));
            }
            else
            {
                // the heir is the one who thinks: they took up the view of the head before them
                string was = Cap(WasTo(w, holder, old, Poss(holder))), pr = Pronoun(holder), be = pr == "they" ? "are" : "is";
                lines.Add((t.Kind, t.Kind switch
                {
                    Friend => $"{was} thought well of you: {Signed(t.Left)} of it still, {gone}.",
                    Enemy => $"{was} thought ill of you: {Signed(t.Left)} of it still, {gone}.",
                    Vengeful => $"{was} was your enemy, and {pr} {be} vengeful by nature: your enemy since year {t.Turn}, and it does not fade.",
                    LetGo => $"{was} was your enemy, but {pr} {be} {t.Nature.ToLowerInvariant()} by nature, and {(pr == "they" ? "let" : "lets")} it go.",
                    Unseen => $"{was} thought well of you, but {pr} took the house as a child and took on none of it.",
                    _ => ""
                }));
            }
        }
        return lines;
    }
}
