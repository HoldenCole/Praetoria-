using System.Text.Json;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Does each law do anything (docs/LAWS_REVIEW.md)? For every option of every law, and for each thing the option says it does,
/// two copies of the same game are played side by side with a passive player: one with that single effect in force (the option's
/// other effects and what the estates think of it set aside), one without. If nothing in the saved game differs after a year,
/// and nothing after six, the effect is written in the content and read by no rule. It prints; it does not pass or fail.
/// </summary>
internal static class LawsProbe
{
    private static readonly string[] Skip = { "\"Laws\"", "law_cd:", "laws_enacted" };
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
        tc.EndTurn();
    }
    private static void Flatten(JsonElement e, string path, Dictionary<string, string> into)
    {
        if (e.ValueKind == JsonValueKind.Object) { foreach (var p in e.EnumerateObject()) Flatten(p.Value, path + "/" + p.Name, into); }
        else if (e.ValueKind == JsonValueKind.Array) { int i = 0; foreach (var x in e.EnumerateArray()) Flatten(x, path + "/" + i++, into); }
        else into[path] = e.ToString();
    }
    private static Dictionary<string, string> Leaves(World w)
    {
        var d = new Dictionary<string, string>(); using var doc = JsonDocument.Parse(SaveSystem.Serialize(w)); Flatten(doc.RootElement, "", d);
        foreach (var k in d.Keys.Where(k => k.Contains("/News/") || k.Contains("/History/") || k.Contains("NewsSeq")).ToList()) d.Remove(k);
        return d;
    }
    /// <summary>Who would hold this law at the start: the player's house where it holds the position, else the first house that does.</summary>
    private static string? Holder(World w, PolicyCatalog cat, LawDef law)
    {
        if (law.Scope == "senate") return PolicySystem.SenateHolder;
        if (law.Scope == "chair") return RegionSystem.CapitalHolder(w)?.Id;
        if (law.Scope == "emperor") return ThroneSystem.Emperor(w)?.Id;
        if (law.Scope.StartsWith("office:")) return OfficeSystem.HolderHouse(w, law.Scope["office:".Length..]);
        string mine = w.Protagonist!.HouseId; if (PolicySystem.HoldsScope(w, mine, law.Scope)) return mine;
        return w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault(h => PolicySystem.HoldsScope(w, h.Id, law.Scope))?.Id;
    }
    /// <summary>Put somebody in the position a law needs, where nobody is in it when the game opens: an office, a post of the council.</summary>
    private static void Seat(World w, LawDef law)
    {
        string mine = w.Protagonist!.HouseId; var house = w.House(mine)!;
        if (law.Scope.StartsWith("office:") && OfficeSystem.HolderHouse(w, law.Scope["office:".Length..]) == null)
            w.Offices.Add(new OfficeHolding { OfficeId = law.Scope["office:".Length..], HouseId = mine, CharacterId = w.Protagonist.Id, SinceTurn = w.Turn });
        if (law.Scope.StartsWith("post:") && house.Duty(law.Scope["post:".Length..]) == null)
        {
            var kin = w.LivingCharacters().Where(c => c.HouseId == mine && c.Id != w.Protagonist.Id && c.Age >= 18).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (kin != null) house.Duties.Add(new Duty { Role = law.Scope["post:".Length..], HolderId = kin.Id, Since = w.Turn });
        }
    }
    private static string Where(IEnumerable<string> paths)
    {
        string Part(string p) { var s = p.Split('/', StringSplitOptions.RemoveEmptyEntries); if (s.Length > 1 && s[0] == "World") s = s.Skip(1).ToArray(); return s.Length == 0 ? p : s[0] is "Houses" or "Holdings" or "Characters" or "Fleets" && s.Length >= 3 ? s[0] + "." + string.Join(".", s.Skip(2).Take(2).Where(x => !int.TryParse(x, out _))) : s[0] == "WorldCounters" && s.Length >= 2 ? "counter " + s[1].Split(':')[0] : s[0]; }
        return string.Join(", ", paths.Select(Part).GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(4).Select(g => $"{g.Key} ×{g.Count()}"));
    }

    /// <summary>Which laws anybody changes in a game: thirty years, a passive player, how many houses changed each law and to what.</summary>
    private static int Use(Options o, string root, string sid, ContentDatabase content)
    {
        var cat = content.Policies; int seeds = Math.Max(1, Math.Min(o.Seeds, 4)), years = 30;
        Console.WriteLine($"=== Which laws anybody changes: {sid}, {seeds} games of {years} years, a passive player ===\n");
        var motions = new SortedDictionary<(string, bool), int>(); var changes = new Dictionary<string, int>(); var ends = new Dictionary<string, Dictionary<string, int>>(); var starts = new Dictionary<string, Dictionary<string, int>>(); int houses = 0, edictYears = 0;
        int moved = 0, carried = 0, lost = 0, petitions = 0, repeals = 0, yearsWithAct = 0, yearsPlayed = 0, byBench = 0, byHurt = 0; var movedBy = new Dictionary<string, int>(); var movedLaw = new Dictionary<string, int[]>();
        var estates = new SortedDictionary<(int, string), int[]>();   // (year, estate) -> houses loyal, houses hostile, the Reach's law's own opinion summed over the games
        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
            string Now(string holder, LawDef law) => PolicySystem.OptionId(w, cat, holder, law.Id);
            List<string> Holders(LawDef law) => law.Scope == "senate" ? new List<string> { PolicySystem.SenateHolder } : w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != w.Protagonist!.HouseId && PolicySystem.HoldsScope(w, h.Id, law.Scope)).Select(h => h.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            void Tally(Dictionary<string, Dictionary<string, int>> into) { foreach (var law in cat.Laws) foreach (var h in Holders(law)) { if (!into.TryGetValue(law.Id, out var d)) into[law.Id] = d = new(); string oid = Now(h, law); d[oid] = d.GetValueOrDefault(oid) + 1; } }
            var last = new Dictionary<(string, string), string>(); Tally(starts); var seen = new HashSet<string>(); var counted = new HashSet<string>(); var decided = new HashSet<string>();
            for (int y = 0; y < years && !w.IsDynastyDead; y++)
            {
                PassiveTurn(tc);
                foreach (var law in cat.Laws) foreach (var h in law.Scope == "senate" ? new List<string> { PolicySystem.SenateHolder } : w.Houses.Values.Where(x => !World.Outsider(x.Id) && x.Id != w.Protagonist!.HouseId).Select(x => x.Id).ToList())
                {
                    string oid = Now(h, law); if (last.TryGetValue((h, law.Id), out var was) && was != oid) changes[law.Id] = changes.GetValueOrDefault(law.Id) + 1; last[(h, law.Id)] = oid;
                }
                if (ThroneSystem.Emperor(w) != null) edictYears++;
                // the chamber's law: every Act and petition moved this year, by whom, and how the count went
                bool any = false; yearsPlayed++;
                foreach (var m in w.Motions.Where(m => m.LawId.Length > 0).OrderBy(m => m.Id, StringComparer.Ordinal))
                {
                    if (!movedLaw.TryGetValue(m.LawId, out var ml)) movedLaw[m.LawId] = ml = new int[3];
                    if (seen.Add(m.Id))
                    {
                        moved++; any = true; ml[0]++; if (SenateSystem.IsPetition(m)) petitions++;
                        string by = SenateSystem.IsPetition(m) ? "an estate" : FactionSystem.Of(w, m.ProposerHouseId)?.PartyId is { Length: > 0 } pid ? pid : "no party"; movedBy[by] = movedBy.GetValueOrDefault(by) + 1;
                        if (cat.Law(m.LawId)?.Option(m.LawOption)?.Default == true) repeals++;
                    }
                    if (m.Resolved && counted.Add(m.Id)) { if (m.Passed) { carried++; ml[1]++; } else { lost++; ml[2]++; } }
                }
                if (any) yearsWithAct++;
                if ((y + 1) % 10 == 0)   // what the estates make of the houses, and what the Reach's law alone says to them
                    foreach (var e in cat.Estates)
                    {
                        if (!estates.TryGetValue((y + 1, e.Id), out var et)) estates[(y + 1, e.Id)] = et = new int[3];
                        foreach (var h in w.Houses.Values.Where(x => !World.Outsider(x.Id))) { int a = w.Counter(PolicySystem.ApprovalKey(w, h.Id, e.Id)); if (a >= PolicySystem.Loyal) et[0]++; else if (a <= PolicySystem.Hostile) et[1]++; }
                        et[2] += PolicySystem.ReachOpinion(w, cat, e.Id);
                    }
                foreach (var m in w.Motions.Where(m => m.Resolved && decided.Add(m.Id))) { var k = (m.Kind + (m.Kind is "commission" ? ":" + m.OfficeId : m.Kind is "act" or "petition" ? ":" + m.LawId : ""), m.Passed); motions[k] = motions.GetValueOrDefault(k) + 1; }
            }
            Tally(ends); houses += w.Houses.Values.Count(h => !World.Outsider(h.Id)); byBench += w.Counter(SenateActs.BenchMovedKey); byHurt += w.Counter(SenateActs.RepealMovedKey);
        }
        Console.WriteLine($"{"law",-20} {"set by",-30} {"changed",8}   in force among those who hold the position, at the start -> after {years} years");
        foreach (var law in cat.Laws)
        {
            string S(Dictionary<string, Dictionary<string, int>> d) => d.TryGetValue(law.Id, out var x) ? string.Join(" ", law.Options.Select(op => $"{op.Id} {x.GetValueOrDefault(op.Id)}")) : "nobody holds it";
            string by = PolicySystem.ScopeName(law.Scope); if (by.Length > 30) by = by[..30];
            Console.WriteLine($"{law.Id,-20} {by,-30} {changes.GetValueOrDefault(law.Id),8}   {S(starts)}  ->  {S(ends)}");
        }
        Console.WriteLine($"\n{houses} houses in all; years with an emperor on the throne: {edictYears}.");
        Console.WriteLine($"Acts of the Senate: moved {moved} (petitions of an estate {petitions}; back to the custom {repeals}), carried {carried}, lost {lost}; an Act was moved in {yearsWithAct} of {yearsPlayed} years.");
        Console.WriteLine($"  of them moved by the chamber's own business (SenateActs.Legislate): a bench's platform {byBench}, a repeal by a house the law hurt {byHurt}");
        Console.WriteLine("  moved by: " + (movedBy.Count == 0 ? "nobody" : string.Join(", ", movedBy.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value}"))));
        Console.WriteLine("  by law (moved/carried/lost): " + string.Join(", ", movedLaw.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value[0]}/{kv.Value[1]}/{kv.Value[2]}")));
        Console.WriteLine($"\nthe estates, summed over the {seeds} games: houses loyal / houses hostile / what the Reach's law alone says to the estate");
        foreach (var yr in estates.Keys.Select(k => k.Item1).Distinct()) Console.WriteLine($"   year {yr,2}: " + string.Join(", ", estates.Where(kv => kv.Key.Item1 == yr).Select(kv => $"{kv.Key.Item2} {kv.Value[0]}/{kv.Value[1]}/{kv.Value[2]:+0;-0;0}")));
        Console.WriteLine("\nmotions decided, by kind: carried / lost");
        foreach (var kind in motions.Keys.Select(k => k.Item1).Distinct()) Console.WriteLine($"   {kind,-34} {motions.GetValueOrDefault((kind, true)),4} / {motions.GetValueOrDefault((kind, false)),-4}");
        return 0;
    }

    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); string sid = o.Scenario == "academy_crucible" ? "the_old_lion" : o.Scenario; ulong seed = 7;
        var content = ContentLoader.LoadFromDirectory(root); var cat = content.Policies;
        if (Environment.GetEnvironmentVariable("LAWS_NOISE") != null)
        {
            var s0 = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); string f0 = SaveSystem.Serialize(s0);
            var a0 = SaveSystem.Deserialize(f0); var b0 = SaveSystem.Deserialize(f0); var la0 = Leaves(a0); var lb0 = Leaves(b0);
            Console.WriteLine("before a year: " + la0.Keys.Union(lb0.Keys).Count(k => la0.GetValueOrDefault(k) != lb0.GetValueOrDefault(k)));
            PassiveTurn(new TurnController(a0, content)); PassiveTurn(new TurnController(b0, content)); la0 = Leaves(a0); lb0 = Leaves(b0);
            var ch = la0.Keys.Union(lb0.Keys).Where(k => la0.GetValueOrDefault(k) != lb0.GetValueOrDefault(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            Console.WriteLine("after a year: " + ch.Count); foreach (var k in ch.Take(40)) Console.WriteLine($"  {k}: {la0.GetValueOrDefault(k)} | {lb0.GetValueOrDefault(k)}");
            return 0;
        }
        if (Environment.GetEnvironmentVariable("LAWS_USE") != null) return Use(o, root, sid, content);
        Console.WriteLine($"=== Does each law do anything: {sid}, seed {seed}, a passive player; one effect at a time, the estates' opinions set aside ===\n");
        Console.WriteLine($"{"law",-20} {"set by",-22} {"option",-16} {"effect",-24} {"after 1 year",-14} {"after 6",-10} where it shows");
        int dead = 0, slow = 0, live = 0, unheld = 0;
        foreach (var law in cat.Laws)
        {
            var start = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); Seat(start, law);
            string? holder = Holder(start, cat, law); string by = PolicySystem.ScopeName(law.Scope); if (by.Length > 22) by = by[..22];
            if (holder == null) { Console.WriteLine($"{law.Id,-20} {by,-22} nobody holds this position when the game opens: not tried"); unheld++; continue; }
            string frozen = SaveSystem.Serialize(start);
            foreach (var opt in law.Options.Where(x => !x.Default))
            {
                var keys = opt.Effects.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
                if (law.TaxPolicy) keys.Add("(the tithe itself)");
                if (keys.Count == 0) { Console.WriteLine($"{law.Id,-20} {by,-22} {opt.Id,-16} {"(none: opinions only)",-24}"); continue; }
                var effects = (Dictionary<string, int>)opt.Effects; var opinions = (Dictionary<string, int>)opt.Opinions;
                var keepE = new Dictionary<string, int>(effects); var keepO = new Dictionary<string, int>(opinions);
                foreach (var key in keys)
                {
                    effects.Clear(); opinions.Clear();
                    int[] diff = new int[2]; string where = "";
                    for (int pass = 0; pass < 2; pass++)
                    {
                        int years = pass == 0 ? 1 : 6;
                        var a = SaveSystem.Deserialize(frozen); var b = SaveSystem.Deserialize(frozen);
                        foreach (var x in new[] { a, b })   // the option is in force in both; only in the second does it do the one thing
                        {
                            if (law.TaxPolicy && x == a) continue;   // the tithe is the option itself: the first copy keeps the customary one
                            if (!x.Laws.TryGetValue(holder, out var set)) x.Laws[holder] = set = new Dictionary<string, string>();
                            set[law.Id] = opt.Id;
                        }
                        if (law.TaxPolicy && b.House(holder) is { } hh) hh.TaxPolicy = opt.Id;
                        // the first copy is played with the option doing nothing, then the second with it doing the one thing
                        effects.Clear(); var ta = new TurnController(a, content); for (int y = 0; y < years; y++) PassiveTurn(ta);
                        if (keepE.TryGetValue(key, out int v)) effects[key] = v;
                        var tb = new TurnController(b, content); for (int y = 0; y < years; y++) PassiveTurn(tb);
                        var la = Leaves(a); var lb = Leaves(b); var changed = la.Keys.Union(lb.Keys).Where(k => la.GetValueOrDefault(k) != lb.GetValueOrDefault(k)).ToList();
                        diff[pass] = changed.Count; where = Where(changed); if (changed.Count > 0) break;
                    }
                    string verdict = diff[0] > 0 ? "moves " + diff[0] : "nothing"; string six = diff[0] > 0 ? "" : diff[1] > 0 ? "moves " + diff[1] : "NOTHING";
                    if (diff[0] > 0) live++; else if (diff[1] > 0) slow++; else dead++;
                    Console.WriteLine($"{law.Id,-20} {by,-22} {opt.Id,-16} {key + (keepE.TryGetValue(key, out int kv) ? $" {kv:+0;-0}" : ""),-24} {verdict,-14} {six,-10} {where}");
                }
                effects.Clear(); foreach (var (k, x) in keepE) effects[k] = x; opinions.Clear(); foreach (var (k, x) in keepO) opinions[k] = x;
            }
        }
        Console.WriteLine($"\n{live} effects move something within a year, {slow} only within six, {dead} move nothing in six years; {unheld} laws could not be tried because nobody holds the position.");
        return 0;
    }
}
