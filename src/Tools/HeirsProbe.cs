using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// What the old head's friends and enemies do when they are handed down (docs/LIFE.md, "Friends and enemies handed down"). Games of
/// thirty years with a passive player, each played twice from the same seed: once with the rule, and once without it (nothing handed
/// down to the player's heir, and another house's new head starting with half the old one's regard for the player, as before). It
/// counts the ties handed down at each succession, the enmities taken up at once, and what can be counted downstream: feuds begun,
/// knives, marriages, the Senate's votes, the old ties at the count, and what the heads of the Reach think of the player at the end.
/// It prints; it does not pass or fail. <c>dotnet run --project src/Tools -- heirs --seeds 2</c>
/// </summary>
internal static class HeirsProbe
{
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
        tc.EndTurn();
    }

    private sealed class Tally
    {
        public int Games, Yours, Others, Feuds, Knives, Marriages, Motions, Passed, VotesFor, VotesAgainst, TieVotes, EnemyYears, HeadsEnd, ViewsEnd, RegardEnd, HostileEnd, FriendlyEnd;
        public readonly Dictionary<string, int> OfYou = new();
        public int TieVotesCast, TieVotesWith, FeudsTied, MarriagesTied; public readonly Dictionary<string, int> Kinds = new();
        public readonly List<int> PerSuccession = new(); public readonly List<string> YourLines = new();
    }

    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); string sid = o.Scenario == "academy_crucible" ? "the_old_lion" : o.Scenario;
        var content = ContentLoader.LoadFromDirectory(root); int seeds = Math.Max(1, Math.Min(o.Seeds, 6)), years = 30;
        Console.WriteLine($"=== Friends and enemies handed down: {sid}, {Counted.Of(seeds, "game")} of {years} years, a passive player, each with the rule and without it ===\n");
        var with = new Tally(); var without = new Tally();
        try
        {
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                foreach (bool on in new[] { true, false })
                {
                    InheritedRegard.Disabled = !on; var t = on ? with : without; t.Games++;
                    var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
                    var seen = new HashSet<InheritedTie>(ReferenceEqualityComparer.Instance); var all = new List<InheritedTie>();
                    var motions = new HashSet<string>(); var weddings = new HashSet<string>(); var feuds = new HashSet<string>();
                    for (int y = 0; y < years && !w.IsDynastyDead; y++)
                    {
                        var heads = w.Houses.Values.ToDictionary(h => h.Id, h => h.HeadId); int successions = w.Successions.Count; int newsSeq = w.NewsSeq;
                        PassiveTurn(tc);
                        // what the player reads of it: their own succession, and another house's new head who takes up a view of them
                        foreach (var n in w.News.Where(n => n.Seq > newsSeq && (n.Text.Contains("'s friends: ") || n.Text.Contains(" enemies: ") || n.Text.Contains("opinion of you") || n.Text.Contains("quarrel with you"))))
                            t.YourLines.Add($"      told: {n.Text}");
                        t.Others += w.Houses.Values.Count(h => heads.TryGetValue(h.Id, out var was) && was.Length > 0 && h.HeadId.Length > 0 && h.HeadId != was);
                        foreach (var s in w.Successions.Skip(successions)) t.Yours++;
                        // the ties handed down this year, grouped by the succession they came with
                        var fresh = w.InheritedTies.Where(x => seen.Add(x)).ToList(); all.AddRange(fresh);
                        foreach (var g in fresh.GroupBy(x => (x.HeirId, x.Turn)))
                        {
                            t.PerSuccession.Add(g.Count()); foreach (var x in g) t.Kinds[x.Kind] = t.Kinds.GetValueOrDefault(x.Kind) + 1;
                            foreach (var x in g.Where(x => x.FromId == x.HeirId && x.ToId == w.ProtagonistId)) t.OfYou[x.Kind] = t.OfYou.GetValueOrDefault(x.Kind) + 1;
                            if (g.Key.HeirId != w.ProtagonistId) continue;
                            var heir = w.Char(g.Key.HeirId); var old = w.Char(g.First().OldHeadId);
                            t.YourLines.Add($"   seed {seed}, year {g.Key.Turn}: {old?.Name} to {heir?.Name} (aged {heir?.Age}): " + string.Join(", ", g.GroupBy(x => x.Kind).OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Count()}")));
                        }
                        // the Senate's votes: every motion decided, and the votes cast by a head who holds a tie handed down of the mover's head
                        foreach (var m in w.Motions.Where(m => m.Resolved && motions.Add(m.Id)))
                        {
                            t.Motions++; if (m.Passed) t.Passed++;
                            var mover = w.House(m.ProposerHouseId) is { } mh ? Economy.HeadOf(w, mh) : null;
                            foreach (var (hid, v) in m.Votes.OrderBy(v => v.Key, StringComparer.Ordinal))
                            {
                                if (v > 0) t.VotesFor++; else if (v < 0) t.VotesAgainst++;
                                if (mover == null || w.House(hid) is not { } vh || Economy.HeadOf(w, vh) is not { } voter) continue;
                                var tie = all.LastOrDefault(x => x.FromId == voter.Id && x.ToId == mover.Id && x.Kind is InheritedRegard.Friend or InheritedRegard.Enemy or InheritedRegard.Vengeful && m.ResolvedTurn - x.Turn < InheritedRegard.FadeYears);
                                if (tie == null || v == 0) continue;
                                t.TieVotesCast++; if ((tie.Kind == InheritedRegard.Friend) == (v > 0)) t.TieVotesWith++;
                            }
                        }
                        // a tie handed down between two houses: the houses of the one who holds it and the one it is of
                        bool Tied(string a, string b, Func<InheritedTie, bool> kind) => all.Any(x => kind(x) && w.Turn - x.Turn <= InheritedRegard.FadeYears && w.Char(x.FromId) is { } f && w.Char(x.ToId) is { } o && ((f.HouseId == a && o.HouseId == b) || (f.HouseId == b && o.HouseId == a)));
                        foreach (var m in w.Marriages.Where(m => weddings.Add($"{m.A}:{m.B}:{m.Since}")))
                        {
                            t.Marriages++;
                            if (w.Char(m.A) is { } ma && w.Char(m.B) is { } mb && ma.HouseId != mb.HouseId && Tied(ma.HouseId, mb.HouseId, x => x.Kind == InheritedRegard.Friend)) t.MarriagesTied++;
                        }
                        foreach (var f in w.Feuds.Where(f => f.Since == w.Turn || f.Since == w.Turn - 1).Where(f => feuds.Add(f.Id)))
                            if (Tied(f.WrongedId, f.OwingId, x => x.Kind is InheritedRegard.Enemy or InheritedRegard.Vengeful)) t.FeudsTied++;
                        if (w.Protagonist is { } me) { t.TieVotes += LifeSystem.TieVotes(w, me.HouseId); if (w.HasFlag(LifeSystem.OldEnemyFlag)) t.EnemyYears++; }
                    }
                    t.Feuds += w.Counter(FeudSystem.BegunKey); t.Knives += w.Counter("knives_between_houses");
                    Console.WriteLine($"seed {seed} {(on ? "with" : "without")}: {Counted.Successions(w.Counter(InheritedRegard.SuccessionsKey))} handed down, {Counted.Of(all.Count, "tie")} ({w.Counter(InheritedRegard.FriendsKey)} friend, {w.Counter(InheritedRegard.EnemiesKey)} enemy, {w.Counter(InheritedRegard.VengefulKey)} vengeful, {w.Counter(InheritedRegard.LetGoKey)} let go, {w.Counter(InheritedRegard.UnseenKey)} unseen), feuds begun {w.Counter(FeudSystem.BegunKey)}");
                    if (w.Protagonist is { } p)
                        foreach (var h in w.Houses.Values.Where(h => h.Id != p.HouseId && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
                        {
                            if (Economy.HeadOf(w, h) is not { } head) continue;
                            int d = w.GetRelationship(head.Id, p.Id)?.Disposition ?? 0; t.HeadsEnd++; if (d == 0) continue;
                            t.ViewsEnd++; t.RegardEnd += d; if (d <= InheritedRegard.IllAt) t.HostileEnd++; if (d >= InheritedRegard.WellAt) t.FriendlyEnd++;
                        }
                }
        }
        finally { InheritedRegard.Disabled = false; }

        Console.WriteLine();
        var ps = with.PerSuccession.OrderBy(x => x).ToList();
        Console.WriteLine($"With the rule: {Counted.Successions(with.Yours)} of the player's house and {with.Others} of other houses' heads; {Counted.Of(ps.Count, "succession")} handed something down, "
            + (ps.Count > 0 ? $"a median of {ps[ps.Count / 2]} and at most {ps[^1]} ties each" : "none") + ".");
        Console.WriteLine("Ties handed down, by kind: " + string.Join(", ", with.Kinds.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")) + ".");
        Console.WriteLine("What other houses' new heads took up of the old head's regard for the player: " + (with.OfYou.Count == 0 ? "nothing" : string.Join(", ", with.OfYou.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}"))) + ".");
        Console.WriteLine("At the player's successions:"); foreach (var l in with.YourLines) Console.WriteLine(l);
        Console.WriteLine($"Votes cast in the Senate by a head who held a tie handed down of the mover's head: {with.TieVotesCast}, of which {with.TieVotesWith} went the way the tie leans.");
        Console.WriteLine($"Feuds begun between two houses with an enmity handed down between them: {with.FeudsTied} of {with.Feuds}. Marriages between two houses with a friendship handed down between them: {with.MarriagesTied} of {with.Marriages}.\n");
        Console.WriteLine($"{"",-34}{"with",8}{"without",10}");
        void Row(string what, Func<Tally, string> f) => Console.WriteLine($"{what,-34}{f(with),8}{f(without),10}");
        Row("player's successions", t => t.Yours.ToString()); Row("other houses' new heads", t => t.Others.ToString());
        Row("feuds begun", t => t.Feuds.ToString()); Row("knives between houses", t => t.Knives.ToString()); Row("marriages made", t => t.Marriages.ToString());
        Row("motions decided", t => t.Motions.ToString()); Row("motions passed", t => t.Passed.ToString()); Row("votes for / against", t => $"{t.VotesFor}/{t.VotesAgainst}");
        Row("old ties at the count, summed", t => t.TieVotes.ToString()); Row("years with an old enemy", t => t.EnemyYears.ToString());
        Row("heads with a view of you at end", t => $"{t.ViewsEnd}/{t.HeadsEnd}"); Row("their mean regard of you", t => t.ViewsEnd > 0 ? (t.RegardEnd / t.ViewsEnd).ToString() : "-");
        Row("of them friendly (30 or more)", t => t.FriendlyEnd.ToString()); Row("of them hostile (-30 or less)", t => t.HostileEnd.ToString());
        return 0;
    }
}
