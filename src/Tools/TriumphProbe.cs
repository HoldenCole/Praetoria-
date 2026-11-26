using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// <c>praetoria triumph</c>: the triumph at the gate and leave to stand in absence when nobody steers the player's house (docs/CAESAR.md,
/// "The triumph"): how often triumphs are voted, had at the gate and declined, and why; whether the command is taken up again; who moves
/// a triumph without a door; how often leave to stand in absence is sought and granted; and how many who could have stood for office at a
/// count did not, because they commanded away from the capital. A passive player takes the first answer of every report and gives no command.
/// </summary>
internal static class TriumphProbe
{
    public static int Run(Options o)
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var scenarios = o.Scenario is "academy_crucible" or "all" ? Scripted.Starts(root) : new List<string> { o.Scenario };
        int seeds = Math.Max(1, Math.Min(o.Seeds, 10)), turns = o.Turns > 6 ? o.Turns : 30;
        Console.WriteLine($"=== The triumph at the gate · {Counted.Starts(scenarios.Count)} × {Counted.Seeds(seeds)} × {turns} years · a passive player ===");
        var sum = new Dictionary<string, int>(StringComparer.Ordinal); var why = new Dictionary<string, int>(StringComparer.Ordinal); var ended = new Dictionary<string, int>(StringComparer.Ordinal);
        int emptySeats = 0, elected = 0, electedCommanding = 0, games = 0, mineVoted = 0, doorless = 0, doorlessLeave = 0, barredAtCount = 0, barredWithLeave = 0, counts = 0, generalsAway = 0, generalYears = 0;
        string[] keys = { TriumphSystem.StoodTakenUpKey, TriumphSystem.StoodNotTakenUpKey, "triumphs_moved", "triumphs_voted", TriumphSystem.HadKey, TriumphSystem.DeclinedKey, TriumphSystem.LapsedKey, TriumphSystem.TakenUpKey, TriumphSystem.NotTakenUpKey,
            TriumphSystem.LeaveMovedKey, TriumphSystem.LeaveGrantedKey, TriumphSystem.LeaveRefusedKey, TriumphSystem.LeaveVetoedKey, TriumphSystem.LeaveSpentKey, TriumphSystem.ElectedAwayKey,
            TriumphSystem.NamesDroppedKey, TriumphSystem.CameHomeKey, TriumphSystem.UnderRecallKey, "recalls_voted", "civil_wars", "republics_ended" };
        foreach (var sid in scenarios)
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content); games++;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int t = 0; t < turns && !w.IsDynastyDead; t++)
                {
                    foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
                    // a count at the end of this year: who could have stood but for a command away from the capital
                    if (!w.IsImperium && LadderSystem.Active(w, content.Offices) && LadderSystem.CountIn(w, content.Offices) == 0)
                    {
                        counts++;
                        foreach (var c in w.LivingCharacters().Where(c => !World.Outsider(c.HouseId) && TriumphSystem.AwayCommand(w, c) != null))
                            if (content.Offices.Rungs.Any(r => LadderSystem.Bar(w, content.Offices, c, r) is var no && (no == null || no == TriumphSystem.AwayRefusal(w, c))))
                            { if (TriumphSystem.HasLeave(w, c.HouseId)) barredWithLeave++; else barredAtCount++; }
                    }
                    foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id)))
                        if (CaesarSystem.GeneralOf(w, h) is { } g) { generalYears++; if (TriumphSystem.AwayCommand(w, g) != null) generalsAway++; }
                    tc.EndTurn();
                    if (w.Counter("mark:" + LadderSystem.HeldFlag) == w.Turn)
                    {
                        emptySeats += content.Offices.Rungs.Sum(r => r.Seats) - w.Magistrates.Count;
                        foreach (var mg in w.Magistrates) { elected++; if (w.Fleets.Values.Any(f => f.AdmiralId == mg.CharacterId)) electedCommanding++; }
                    }
                    foreach (var m in w.Motions.Where(m => m.Kind is TriumphSystem.TriumphKind or TriumphSystem.LeaveKind && seen.Add(m.Id)))
                        if (!tc.Senate.MayPropose(w, m.ProposerHouseId)) { if (m.Kind == TriumphSystem.TriumphKind) doorless++; else doorlessLeave++; }
                }
                foreach (var k in keys) sum[k] = sum.GetValueOrDefault(k) + w.Counter(k);
                string? mine = w.Protagonist?.HouseId; mineVoted += w.TriumphVotes.Count(x => x.HouseId == mine && x.Choice != TriumphSystem.Stand);
                foreach (var tv in w.TriumphVotes.Where(x => x.Choice is TriumphSystem.Home or TriumphSystem.Keep))
                    foreach (var part in tv.Why.Split(", ", StringSplitOptions.RemoveEmptyEntries)) { string k = (tv.Choice == TriumphSystem.Home ? "home: " : "keep: ") + part; why[k] = why.GetValueOrDefault(k) + 1; }
                foreach (var tv in w.TriumphVotes.Where(x => x.Choice == TriumphSystem.Home && x.Done)) { string k = tv.Ended.StartsWith("The ") && tv.Ended.Contains(" was given to ") ? "the fleet was given to another" : tv.Ended; ended[k] = ended.GetValueOrDefault(k) + 1; }
                Console.WriteLine($"   {sid} seed {seed}: triumphs voted {w.Counter("triumphs_voted")} of {w.Counter("triumphs_moved")} moved by other houses, had at the gate {w.Counter(TriumphSystem.HadKey)}, declined {w.Counter(TriumphSystem.DeclinedKey)}, lapsed {w.Counter(TriumphSystem.LapsedKey)} · leave sought {w.Counter(TriumphSystem.LeaveGrantedKey) + w.Counter(TriumphSystem.LeaveRefusedKey) + w.Counter(TriumphSystem.LeaveVetoedKey)}, granted {w.Counter(TriumphSystem.LeaveGrantedKey)}"
                    + (w.TriumphVotes.Any(x => x.Choice != TriumphSystem.Stand) ? ": " + string.Join("; ", w.TriumphVotes.Where(x => x.Choice != TriumphSystem.Stand).Take(5).Select(x => $"y{x.Turn} {x.Name} of {w.House(x.HouseId)?.Name ?? x.HouseId} {(x.Choice.Length == 0 ? "to answer" : x.Choice)}")) : ""));
            }
        int S(string k) => sum.GetValueOrDefault(k);
        Console.WriteLine();
        Console.WriteLine($"   in {Counted.Of(games, "game")}: triumphs moved by other houses {S("triumphs_moved")}, voted {S("triumphs_voted")} ({mineVoted} to the player's house), had at the gate {S(TriumphSystem.HadKey)}, declined to keep the fleet {S(TriumphSystem.DeclinedKey)}, lapsed unanswered {S(TriumphSystem.LapsedKey)}, void under a recall {S(TriumphSystem.UnderRecallKey)}");
        Console.WriteLine($"   moved by a house with no door, as one who has held a magistracy: triumphs {doorless}, leave {doorlessLeave}");
        Console.WriteLine($"   the year at home ended: command taken up {S(TriumphSystem.TakenUpKey)}, not {S(TriumphSystem.NotTakenUpKey)}{(ended.Count > 0 ? " (" + string.Join(" · ", ended.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")) + ")" : "")}");
        Console.WriteLine($"   why they chose as they did: {string.Join(" · ", why.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}"))}");
        Console.WriteLine($"   leave to stand in absence: moved by other houses {S(TriumphSystem.LeaveMovedKey)}, granted {S(TriumphSystem.LeaveGrantedKey)}, refused {S(TriumphSystem.LeaveRefusedKey)}, vetoed {S(TriumphSystem.LeaveVetoedKey)}, spent at an election {S(TriumphSystem.LeaveSpentKey)} · elected while commanding away {S(TriumphSystem.ElectedAwayKey)} · names of the player's house taken down {S(TriumphSystem.NamesDroppedKey)}");
        Console.WriteLine($"   at {Counted.Of(counts, "count")} of the ladder: {barredAtCount} who could have stood did not, commanding away without leave; {barredWithLeave} could, by leave · came home the year before to stand {S(TriumphSystem.CameHomeKey)} (took the command up again after the count {S(TriumphSystem.StoodTakenUpKey)}, not {S(TriumphSystem.StoodNotTakenUpKey)}) · seats left empty at a count {emptySeats} · magistrates elected {elected}, of whom commanding a fleet {electedCommanding}");
        Console.WriteLine($"   a house's general commanded away from the capital in {(generalYears == 0 ? 0 : 100 * generalsAway / generalYears)}% of general-years · recalls {S("recalls_voted")} · civil wars {S("civil_wars")} · the Republic fell {S("republics_ended")}");
        return 0;
    }
}
