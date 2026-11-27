using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// <c>praetoria gold</c>: foreign gold in the Senate (docs/BEYOND.md, "Foreign gold in the Senate") in games nobody steers. Thirty years
/// a game (`--turns`), seeds 1 up to `--seeds` (six at most), `--scenario` one start or `all` for every start (those under a throne too,
/// where the Senate sits but nothing should be sent, for the record is not kept there). It
/// counts the war motions the Cities wrote about, the offers made, taken, refused, exposed and left to lapse, the war votes the gold
/// turned, the wars bought off before the vote, and the charges that followed before a praetor. With `GOLD_WAR=thessar:8` the heaviest
/// house that may move a motion moves a war on that nation in that year, or the first after it when the docket has room and the Reach fights nobody abroad, and again ten years after each, so a war the
/// houses rarely move can still be measured. `GOLD_PLAYER=take|refuse|expose` makes the passive player answer every offer that way
/// (by default it answers nothing, and what is sent to it lapses). `GOLD_WHY=1` adds each head's natures to the offers, and each
/// house's vote and seats to the count. It prints; it does not pass or fail.
/// </summary>
internal static class GoldProbe
{
    public static int Run(Options o)
    {
        string root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var scenarios = o.Scenario is "academy_crucible" ? new List<string> { "the_old_lion" } : o.Scenario == "all" ? Scripted.Starts(root) : new List<string> { o.Scenario };
        int seeds = Math.Max(1, Math.Min(o.Seeds, 6)), years = o.Turns > 6 ? o.Turns : 30;
        string? force = Environment.GetEnvironmentVariable("GOLD_WAR"), answer = Environment.GetEnvironmentVariable("GOLD_PLAYER");
        Console.WriteLine($"=== Foreign gold in the Senate · {Counted.Starts(scenarios.Count)} × {Counted.Seeds(seeds)} × {years} years · a passive player{(force != null ? " · a war moved: " + force : "")}{(answer != null ? " · the player answers: " + answer : "")} ===");
        var totals = new SortedDictionary<string, int>(StringComparer.Ordinal); int games = 0, throne = 0, chargesOnGold = 0, convictedOnGold = 0, warMotions = 0, warsVoted = 0, chargedPlayer = 0;
        var askedOf = new SortedDictionary<string, int>(StringComparer.Ordinal); var whoTook = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var sid in scenarios)
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, sid), seed); var tc = new TurnController(w, content);
                if (w.IsImperium) throne++;
                games++; var charged = new HashSet<string>(StringComparer.Ordinal); var seenMotions = new HashSet<string>(StringComparer.Ordinal); var ctx = new CommandContext(w, WorldBuilder.RngFor(w));
                var offers = new Dictionary<string, GoldOffer>(StringComparer.Ordinal); var motionLines = new List<string>(); int nextForce = 0;
                for (int t = 0; t < years && !w.IsDynastyDead; t++)
                {
                    foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
                    if (answer != null && w.Protagonist is { } me) foreach (var off in ForeignGoldSystem.Waiting(w, me.HouseId).ToList()) tc.Executor.TryExecute(new ForeignGoldCommand(me.Id, off.Id, answer), ctx);
                    if (force != null && force.Split(':') is { Length: 2 } fp && int.TryParse(fp[1], out int at) && w.Turn >= Math.Max(at, nextForce) && Move(w, tc, fp[0])) nextForce = w.Turn + 10;
                    tc.EndTurn();
                    foreach (var off in w.GoldOffers) offers[off.Id] = off;
                    foreach (var m in w.Motions.Where(m => tc.Senate.Motions.Def(m.Kind)?.Interest == "foreign_war" && ForeignGoldSystem.Payer(w, content.Nations, m.TargetId) != null))
                    {
                        if (seenMotions.Add(m.Id)) { warMotions++; var (f0, a0) = tc.Senate.Tally(w, m); motionLines.Add($"y{m.ProposedTurn} {w.House(m.ProposerHouseId)?.Name} moves war on {m.TargetId}: as the benches lean {f0} to {a0}"); }
                        if (m.Resolved && !seenMotions.Contains("done:" + m.Id)) { seenMotions.Add("done:" + m.Id); motionLines.Add($"y{m.ResolvedTurn} the war on {m.TargetId} is {(m.Passed ? "carried" : m.Vetoed ? "vetoed" : "thrown out")}, {m.ForWeight} to {m.AgainstWeight}"
                            + (Environment.GetEnvironmentVariable("GOLD_WHY") != null ? ": " + string.Join(" ", m.Votes.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}{(v.Value > 0 ? "+" : v.Value < 0 ? "-" : "0")}{tc.Senate.Weight(w, v.Key)}")) : "")); }
                    }
                    foreach (var d in w.OfficeRecord.Where(d => d.What == ForeignGoldSystem.Deed && d.Answered)) if (charged.Add(d.Id)) { chargesOnGold++; if (w.Counter(LadderSystem.BarredKey(d.CharacterId)) > w.Turn) convictedOnGold++; }
                    chargedPlayer += w.Indictments.Count(x => x.DeedId.StartsWith("deed_gold_", StringComparison.Ordinal) && x.Turn == w.Turn && x.HouseId == w.Protagonist?.HouseId);   // a charge against the powerful of any house is held as an indictment too
                }
                warsVoted += w.Counter("foreign_wars");
                foreach (var key in new[] { ForeignGoldSystem.MotionsKey, ForeignGoldSystem.OffersKey, ForeignGoldSystem.TakenKey, ForeignGoldSystem.RefusedKey, ForeignGoldSystem.ExposedKey, ForeignGoldSystem.LapsedKey, ForeignGoldSystem.TurnedKey, ForeignGoldSystem.WithdrawOffersKey, ForeignGoldSystem.BoughtOffKey, ForeignGoldSystem.BrokenKey, ForeignGoldSystem.SpentKey })
                    totals[key] = totals.GetValueOrDefault(key) + w.Counter(key);
                string? mine = w.Protagonist?.HouseId;
                foreach (var off in offers.Values)
                {
                    string who = off.HouseId == mine ? "the player" : off.Ask == ForeignGoldSystem.Withdraw ? "a mover" : "another house";
                    askedOf[$"{who}, {off.Ask}"] = askedOf.GetValueOrDefault($"{who}, {off.Ask}") + 1;
                    if (off.Answer == ForeignGoldSystem.Taken && w.Char(off.CharacterId) is { } taker) { string why = off.HouseId == mine ? "the player, as told" : ForeignGoldSystem.ForSale(taker) ? "for sale by nature" : "in want"; whoTook[why] = whoTook.GetValueOrDefault(why) + 1; }
                }
                var cities = w.Nation("thessar");
                Console.WriteLine($"   {sid} seed {seed}{(w.IsImperium ? " (under a throne)" : "")}: war motions on the Cities or the Crown {seenMotions.Count(x => !x.StartsWith("done:", StringComparison.Ordinal))}; written about {w.Counter(ForeignGoldSystem.MotionsKey)}; offers {w.Counter(ForeignGoldSystem.OffersKey)} (taken {w.Counter(ForeignGoldSystem.TakenKey)}, refused {w.Counter(ForeignGoldSystem.RefusedKey)}, exposed {w.Counter(ForeignGoldSystem.ExposedKey)}, lapsed {w.Counter(ForeignGoldSystem.LapsedKey)}); turned {w.Counter(ForeignGoldSystem.TurnedKey)}; bought off {w.Counter(ForeignGoldSystem.BoughtOffKey)}; the purse at the end {Money.Credits(cities?.Purse ?? 0)}; their regard {Counted.Figure(cities?.Disposition ?? 0)}");
                foreach (var line in motionLines) Console.WriteLine("      " + line);
                foreach (var off in offers.Values.OrderBy(x => x.Turn).ThenBy(x => x.Id, StringComparer.Ordinal).Take(12))
                    Console.WriteLine($"      y{off.Turn} {w.House(off.HouseId)?.Name ?? off.HouseId}{(off.HouseId == mine ? " (the player)" : "")}: {Money.Credits(off.Sum)} {ForeignGoldSystem.AskSays(off)}; {off.Answer}{(off.Turned ? ", and it turned the vote" : "")}{(off.Broken ? ", and voted for the war all the same" : "")}{(Environment.GetEnvironmentVariable("GOLD_WHY") != null && w.Char(off.CharacterId) is { } hd ? $" [{string.Join(",", hd.NatureTraits)} / {string.Join(",", hd.AptitudeTraits)} / {hd.Ambition}; before {off.Before}, seats {off.Seats}]" : "")}");
            }
        Console.WriteLine();
        Console.WriteLine($"   in {Counted.Of(games, "game")}{(throne > 0 ? $" ({throne} of them under a throne)" : "")}: war motions on the Cities or a nation they favour {warMotions}; the Cities wrote about {totals.GetValueOrDefault(ForeignGoldSystem.MotionsKey)}");
        Console.WriteLine($"   offers {totals.GetValueOrDefault(ForeignGoldSystem.OffersKey)}: taken {totals.GetValueOrDefault(ForeignGoldSystem.TakenKey)}, refused {totals.GetValueOrDefault(ForeignGoldSystem.RefusedKey)}, exposed {totals.GetValueOrDefault(ForeignGoldSystem.ExposedKey)}, lapsed {totals.GetValueOrDefault(ForeignGoldSystem.LapsedKey)} · gold taken {Money.Credits(totals.GetValueOrDefault(ForeignGoldSystem.SpentKey))}");
        Console.WriteLine($"   to whom: {string.Join(" · ", askedOf.Select(k => $"{k.Key} {k.Value}"))} · why they took it: {string.Join(" · ", whoTook.Select(k => $"{k.Key} {k.Value}"))}");
        Console.WriteLine($"   war votes turned {totals.GetValueOrDefault(ForeignGoldSystem.TurnedKey)} · offered to withdraw {totals.GetValueOrDefault(ForeignGoldSystem.WithdrawOffersKey)}, bought off {totals.GetValueOrDefault(ForeignGoldSystem.BoughtOffKey)} · took it and voted for the war {totals.GetValueOrDefault(ForeignGoldSystem.BrokenKey)}");
        Console.WriteLine($"   charges before a praetor on gold taken {chargesOnGold} ({convictedOnGold} convicted or admitted; {chargedPlayer} brought against the player's house) · wars abroad begun in all {warsVoted}");
        return 0;
    }

    /// <summary>The heaviest house that may move a motion and has none open moves a war on the nation, if the docket has room and the Reach is at war with nobody abroad.</summary>
    private static bool Move(World w, TurnController tc, string nationId)
    {
        var s = tc.Senate; if (NationSystem.AtWar(w) != null || SenateSystem.Docket(w).Count() >= SenateSystem.DocketSize || w.Motions.Any(m => m.Open && m.Kind == "foreign_war")) return false;
        if (w.Nation(nationId) is not { Fallen: false } n || n.Subdued(w.Turn)) return false;
        var mover = w.Houses.Values.Where(h => h.Id != w.Protagonist?.HouseId && !World.Outsider(h.Id) && s.MayPropose(w, h.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id))
            .OrderByDescending(h => s.Weight(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        if (mover == null) return false;
        s.Add(w, mover.Id, "foreign_war", nationId); return true;
    }
}
