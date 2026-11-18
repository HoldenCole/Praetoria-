using Praetoria.Core.Crises;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Great-Crisis framework (GDD §11/§16, BuildSpec M6): a clock, an intensity setting, stages with flags and effects,
/// and the archetype's behaviours. V1's archetype is the human galaxy-war: the houses sort into the hegemon's bloc
/// and the alliance against it, the blocs go to war, and when the clock runs out the wars end. The victor gains standing
/// but no throne (docs/PROCESS.md: the Republic stands unless the player ends it). Deterministic; the clock is a world counter.
/// </summary>
public sealed class GreatCrisisSystem
{
    public const string ClockCounter = "great_crisis_clock";    // tenths
    public const string StageCounter = "great_crisis_stage";    // index of the current stage (−1 = not begun)
    public static string StageFlag(string stageId) => $"great_crisis:{stageId}";
    public const string ResolvedFlag = "great_crisis:resolved";
    public const string HegemonBloc = "hegemon";
    public const string AllianceBloc = "alliance";

    private readonly GreatCrisisDef? _def;
    private readonly FleetSystem _fleets;
    public GreatCrisisSystem(GreatCrisisDef? def, FleetSystem fleets) { _def = def; _fleets = fleets; }
    /// <summary>Breakaway states (docs/BREAKAWAY.md): a side that cannot win but holds a region may keep it at the peace.</summary>
    public PolitySystem? Polities { get; set; }
    /// <summary>The war lasts ten years at the least, turns on its last battles no sooner than the sixth, and is forced to a peace at twenty.</summary>
    public const int MinWarYears = 10, ClimaxYears = 6, ExhaustionYears = 16, MaxWarYears = 20;
    public const string BeganKey = "great_war_began", StartHullsKey = "great_war_hulls", PartitionWishFlag = "seek_partition";
    public const string Crushed = "crushed", Held = "held", PartitionEnd = "partition", Exhaustion = "exhaustion";
    /// <summary>Has the war itself begun, and not yet ended? (The houses sort into blocs before it, and may stand down again.)</summary>
    public static bool AtWar(World w) => w.WorldCounters.ContainsKey(BeganKey) && !w.HasFlag(ResolvedFlag);
    public const int TroublePoints = 7, WarPoints = 3, WarPointsCap = 12, OvershadowPoints = 6, MarchPoints = 8, BreakawayPoints = 4, BreakawayYears = 8, EasesBy = 6;
    public const string EasedCounter = "great_crisis_eased", WarsCounter = "great_wars", RearmedFlag = "great_war_forgotten";
    /// <summary>What deliberate stirring adds to the pressure, and how long after a great war before the Reach can come to another.</summary>
    public const int StirPoints = 6, PeaceYears = 15;
    public static string BeganOf(int nth) => "great_war_began:" + nth;
    public static string EndedOf(int nth) => "great_war_ended:" + nth;
    public static string HowOf(int nth, string ending) => $"great_war_how:{nth}:{ending}";
    /// <summary>The great wars the Reach has fought and finished: when each began, how long it ran and how it ended.</summary>
    public static List<(int began, int years, string how)> Past(World w) => Enumerable.Range(1, w.Counter(WarsCounter)).Select(n =>
        (w.Counter(BeganOf(n)), w.Counter(EndedOf(n)) - w.Counter(BeganOf(n)), w.WorldFlags.FirstOrDefault(f => f.StartsWith($"great_war_how:{n}:"))?[$"great_war_how:{n}:".Length..] ?? "")).ToList();
    /// <summary>What this war is called: there may be more than one in a long saga.</summary>
    public static string WarName(World w) => w.Counter(WarsCounter) switch { 0 => "The War of the Reach", 1 => "The Second War of the Reach", 2 => "The Third War of the Reach", _ => "Another War of the Reach" };
    /// <summary>Has the Reach had a great war and not yet forgotten it?</summary>
    public static int PeaceLeft(World w) => w.HasFlag(ResolvedFlag) ? Math.Max(0, PeaceYears - (w.Turn - w.Counter(CaesarSystem.GreatWarEndedKey))) : 0;

    /// <summary>A generation on, the war is something people were told about. Its clock, its stages and its verdict are cleared, and
    /// what is wrong with the Reach can bring another.</summary>
    private void Rearm(World w)
    {
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("great_crisis:")).ToList()) w.WorldFlags.Remove(f);
        w.WorldCounters.Remove(ClockCounter); w.WorldCounters.Remove(StageCounter); w.WorldCounters.Remove(BeganKey); w.WorldCounters.Remove(StartHullsKey);
        foreach (var h in w.Houses.Values) h.Bloc = "";
        w.WorldFlags.Add(RearmedFlag); w.WorldCounters["mark:" + RearmedFlag] = w.Turn;
        w.Log("A generation has grown up that did not fight the last great war. What it cost is something they have been told, not something they know.");
    }

    /// <summary>What is pushing the Reach toward its great war this year, and by how much. Nothing on this list, and the clock runs
    /// back. The war comes out of what is happening, not out of the calendar.</summary>
    public static List<(string what, int points)> Pressure(World w)
    {
        var list = new List<(string, int)>();
        foreach (var t in CaesarSystem.Wrong(w).Where(t => !t.Contains("at war with each other"))) list.Add((t, TroublePoints));
        if (w.Wars.Count > 0) list.Add(($"{w.Wars.Count} war(s) between houses", Math.Min(WarPointsCap, w.Wars.Count * WarPoints)));
        var hulls = w.Houses.Values.Where(h => !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id)).Select(h => (h, n: FleetSystem.StrengthOf(w, h.Id))).OrderByDescending(x => x.n).ThenBy(x => x.h.Id, StringComparer.Ordinal).Take(2).ToList();
        if (hulls.Count == 2 && hulls[0].n >= 6 && hulls[0].n >= 2 * Math.Max(1, hulls[1].n)) list.Add(($"{hulls[0].h.Name} overshadows every other house in arms", OvershadowPoints));
        if (w.Counter("coalition_pressure") > 0) list.Add(("the houses are drawing together against one of their number", w.Counter("coalition_pressure")));
        if (w.Counter("seizures") > 0) list.Add(("worlds have been taken by force", Math.Min(5, w.Counter("seizures"))));
        if (w.CivilWar != null) list.Add(("a general is marching on the capital", MarchPoints));
        if (w.Polities.Any(p => p.Regions.Count > 0 && w.Turn - p.Since <= BreakawayYears)) list.Add(("a region has lately left the Reach", BreakawayPoints));
        if (PolitySystem.Stirring(w)) list.Add(("somebody's money is setting the great houses against one another", StirPoints));
        return list;
    }
    /// <summary>How near the Reach is to its great war, in hundredths of the way (100 = the war).</summary>
    public int Nearness(World w)
    {
        int at = _def?.Stages.FirstOrDefault(x => x.Behavior == "bloc_war")?.At ?? 300;
        return AtWar(w) || w.HasFlag(ResolvedFlag) ? 100 : Math.Clamp(100 * w.Counter(ClockCounter) / Math.Max(1, at), 0, 100);
    }

    public static int WarYears(World w) => w.WorldCounters.TryGetValue(BeganKey, out int t) ? w.Turn - t : 0;
    private static int Hulls(World w, string bloc) => w.Houses.Values.Where(h => h.Bloc == bloc).Sum(h => FleetSystem.StrengthOf(w, h.Id));
    private static bool HoldsCapital(World w, string bloc)
    {
        if (w.Galaxy is not { Capital.Length: > 0 } g) return true;
        if (w.PlanetHolder(g.Capital) is { } holder && w.House(holder)?.Bloc == bloc) return true;
        return w.Fleets.Values.Any(f => !f.Moving && f.SystemId == g.Capital && f.Strength >= 2 && w.House(f.OwnerId)?.Bloc == bloc);
    }
    /// <summary>A region the losing side could keep: not the capital's, three in five of its worlds held by houses of that side. With the
    /// house of that side that holds most of it.</summary>
    private (string regionId, House leader)? Refuge(World w, string bloc)
    {
        if (w.Galaxy == null || Polities == null) return null; string capitalRegion = w.Galaxy.System(w.Galaxy.Capital)?.RegionId ?? "";
        foreach (var r in w.Galaxy.ReachRegions.Where(r => r.Id != capitalRegion && PolitySystem.OfRegion(w, r.Id) == null).OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var worlds = w.Holdings.Values.Where(h => h.IsPlanet && w.Galaxy.System(h.SystemId)?.RegionId == r.Id).ToList(); if (worlds.Count == 0) continue;
            var theirs = worlds.Where(h => w.House(h.OwnerId)?.Bloc == bloc).ToList();
            if (theirs.Count * 100 < PolitySystem.ControlShare * worlds.Count) continue;
            var leader = theirs.GroupBy(h => h.OwnerId).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => w.House(x.Key)).First()!;
            // the player's own house is not taken out of the Reach for them: they ask for it, or it does not happen
            if (leader.Id == w.Protagonist?.HouseId && !w.HasFlag(PartitionWishFlag)) continue;
            return (r.Id, leader);
        }
        return null;
    }

    /// <summary>How the war would end if it ended now, or null if it goes on. Holding the capital is winning, not having won: the
    /// other side fights on from where it stands until it is crushed, takes a region out of the Reach, or both are spent.</summary>
    public string? Ending(World w)
    {
        int h = Hulls(w, HegemonBloc), a = Hulls(w, AllianceBloc); string winning = h >= a ? HegemonBloc : AllianceBloc, losing = h >= a ? AllianceBloc : HegemonBloc;
        int win = Math.Max(h, a), lose = Math.Min(h, a), years = WarYears(w);
        if (win > 0 && lose * 10 <= win && HoldsCapital(w, winning)) return Crushed;   // all resistance: a tenth of the victor's hulls or less is not a fleet
        if (win * 4 >= lose * 5 && Refuge(w, losing) != null) return PartitionEnd;
        if (years >= ExhaustionYears || (w.Counter(StartHullsKey) > 0 && (h + a) * 2 <= w.Counter(StartHullsKey))) return Exhaustion;
        return years >= MaxWarYears ? Exhaustion : null;
    }
    public GreatCrisisDef? Def => _def;
    public bool IsEmpty => _def == null || _def.Stages.Count == 0;

    public GreatCrisisStage? CurrentStage(World w)
    {
        if (_def == null) return null;
        int i = w.WorldCounters.TryGetValue(StageCounter, out var s) ? s : -1;
        return i >= 0 && i < _def.Stages.Count ? _def.Stages[i] : null;
    }

    /// <summary>Advance the clock and enter any stage it crosses. Call once per turn (after the economy and fleets).</summary>
    public void Tick(World w, IRng rng)
    {
        if (w.HasFlag(RearmedFlag) && w.Counter("mark:" + RearmedFlag) < w.Turn - 1) { w.WorldFlags.Remove(RearmedFlag); w.WorldCounters.Remove("mark:" + RearmedFlag); }
        if (_def != null && w.HasFlag(ResolvedFlag) && PeaceLeft(w) == 0 && w.WorldCounters.ContainsKey(CaesarSystem.GreatWarEndedKey)) Rearm(w);
        if (_def == null || w.HasFlag(ResolvedFlag) || w.Turn < _def.StartTurn) return;
        double intensity = _def.IntensityOf(w.CrisisIntensity);
        if (intensity <= 0) return;

        int stage = w.WorldCounters.TryGetValue(StageCounter, out var s) ? s : -1;
        if (AtWar(w))
        {
            // once it has begun the war runs on its own clock: the galaxy's turmoil hurries it
            int turmoil = w.Wars.Count * 3 + w.Counter("coalition_pressure") + Math.Min(5, w.Counter("seizures"));
            w.WorldCounters[ClockCounter] = w.Counter(ClockCounter) + (int)Math.Round(_def.BaseRate * intensity) + turmoil;
        }
        else
        {
            // before it, nothing moves the clock but what is wrong with the Reach; and when nothing is, the houses stand down
            int pressure = Pressure(w).Sum(x => x.points);
            if (pressure > 0) w.WorldCounters[ClockCounter] = w.Counter(ClockCounter) + (int)Math.Round(pressure * intensity);
            else if (w.Counter(ClockCounter) > 0)
            {
                w.WorldCounters[ClockCounter] = Math.Max(0, w.Counter(ClockCounter) - EasesBy);
                // the blocs dissolve if the Reach has eased well back from where they formed
                int blocsAt = _def.Stages.FirstOrDefault(x => x.Behavior == "blocs")?.At ?? 150; int blocsStage = _def.Stages.ToList().FindIndex(x => x.Behavior == "blocs");
                if (stage >= blocsStage && blocsStage >= 0 && w.Counter(ClockCounter) < blocsAt - 30)
                {
                    foreach (var h in w.Houses.Values.Where(h => h.Bloc == HegemonBloc || h.Bloc == AllianceBloc)) h.Bloc = "";
                    for (int i = blocsStage; i <= stage; i++) w.WorldFlags.Remove(StageFlag(_def.Stages[i].Id));
                    stage = blocsStage - 1; w.WorldCounters[StageCounter] = stage; w.WorldCounters[EasedCounter] = w.Counter(EasedCounter) + 1;
                    w.Log($"[{_def.Name}] The houses that had been counting each other's fleets stand down. Whatever was coming has not come.");
                }
            }
        }
        while (stage + 1 < _def.Stages.Count && w.Counter(ClockCounter) >= _def.Stages[stage + 1].At)
        {
            // the war is not over because the clock says so: it runs ten years at the least, and then until something ends it
            string next = _def.Stages[stage + 1].Behavior;
            if (next == "climax" && w.WorldCounters.ContainsKey(BeganKey) && WarYears(w) < ClimaxYears) break;
            if (next == "resolution" && w.WorldCounters.ContainsKey(BeganKey) && (WarYears(w) < MinWarYears || Ending(w) == null)) break;
            stage++;
            w.WorldCounters[StageCounter] = stage;
            Enter(w, rng, _def.Stages[stage]);
        }
    }

    private void Enter(World w, IRng rng, GreatCrisisStage stage)
    {
        w.WorldFlags.Add(StageFlag(stage.Id));
        var ctx = new EvalContext(w, new Binding(w.ProtagonistId), rng);
        foreach (var e in stage.Effects) e.Apply(ctx);
        if (stage.Log.Length > 0) w.Log($"[{_def!.Name}] {stage.Log}");
        switch (stage.Behavior)
        {
            case "blocs": FormBlocs(w); break;
            case "bloc_war": BlocWar(w); break;
            case "climax": Climax(w); break;
            case "resolution": Resolve(w); break;
        }
    }

    /// <summary>The strongest house (hulls, then legitimacy) is the hegemon; houses whose heads are well disposed to its
    /// head join it, the rest draw together against it. The player's house follows the same rule — and can change
    /// sides through the <c>joinBloc</c> effect.</summary>
    public void FormBlocs(World w)
    {
        var houses = w.Houses.Values.Where(h => PolitySystem.InReach(w, h.Id)).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (houses.Count < 2) return;
        var hegemon = houses[0];
        var hegemonHead = w.LivingCharacters().Where(c => c.HouseId == hegemon.Id).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        hegemon.Bloc = HegemonBloc;
        foreach (var h in houses.Skip(1))
        {
            if (!w.HoldingsOf(h.Id).Any() && FleetSystem.StrengthOf(w, h.Id) == 0) { h.Bloc = ""; continue; }   // the landless have nothing to bring
            int disp = 0;
            if (hegemonHead != null)
                foreach (var c in w.LivingCharacters().Where(c => c.HouseId == h.Id)) disp += w.GetRelationship(c.Id, hegemonHead.Id)?.Disposition ?? 0;
            h.Bloc = disp > 0 && !w.AtWar(h.Id, hegemon.Id) ? HegemonBloc : AllianceBloc;
        }
        w.Log($"Blocs: {hegemon.Name} and {string.Join(", ", houses.Where(h => h.Bloc == HegemonBloc && h.Id != hegemon.Id).Select(h => h.Name).DefaultIfEmpty("no one"))} " +
              $"against {string.Join(", ", houses.Where(h => h.Bloc == AllianceBloc).Select(h => h.Name).DefaultIfEmpty("no one"))}.");
    }

    /// <summary>Every hegemon house goes to war with every alliance house. Rival-vs-rival wars included.</summary>
    public void BlocWar(World w)
    {
        // The hegemon fights the whole alliance; each of its followers takes one alliance house (paired in id order), so a
        // galaxy of twenty houses has twenty-odd wars, not a hundred — fronts, not a free-for-all.
        var a = w.Houses.Values.Where(h => h.Bloc == HegemonBloc).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var b = w.Houses.Values.Where(h => h.Bloc == AllianceBloc).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (a.Count == 0 || b.Count == 0) return;
        w.WorldCounters[BeganKey] = w.Turn; w.WorldCounters[StartHullsKey] = Hulls(w, HegemonBloc) + Hulls(w, AllianceBloc);
        var hegemon = a.OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        foreach (var y in b) _fleets.DeclareWar(w, hegemon.Id, y.Id);
        var others = a.Where(h => h.Id != hegemon.Id).ToList();
        for (int i = 0; i < others.Count; i++) _fleets.DeclareWar(w, others[i].Id, b[i % b.Count].Id);
    }

    private void Climax(World w)
    {
        foreach (var f in w.Fleets.Values) f.Readiness = Math.Min(100, f.Readiness + 20);     // everyone commits what they have
        var player = w.Protagonist?.HouseId;
        if (player != null && w.House(player)?.Bloc == HegemonBloc) w.WorldCounters["coalition_pressure"] = SphereSystem.MaxCoalitionPressure;
    }

    /// <summary>The wars end; the bloc with more hulls wins. Its strongest house, if it won decisively and holds the capital, gains
    /// legitimacy, glory and rank, and the Republic is shaken for years; nobody is enthroned.</summary>
    public void Resolve(World w)
    {
        int hegemonHulls = Hulls(w, HegemonBloc), allianceHulls = Hulls(w, AllianceBloc);
        string winning = hegemonHulls >= allianceHulls ? HegemonBloc : AllianceBloc, losing = winning == HegemonBloc ? AllianceBloc : HegemonBloc;
        int winHulls = Math.Max(hegemonHulls, allianceHulls), loseHulls = Math.Min(hegemonHulls, allianceHulls);
        bool decisive = winHulls > 0 && winHulls >= (int)Math.Ceiling(loseHulls * 1.25), capital = HoldsCapital(w, winning);
        string ending = Ending(w) ?? (decisive && capital ? Held : Exhaustion);
        var refuge = ending == PartitionEnd ? Refuge(w, losing) : null; var losers = w.Houses.Values.Where(h => h.Bloc == losing).ToList();
        foreach (var war in w.Wars.ToList()) _fleets.MakePeace(w, war.HouseA, war.HouseB, terms: false);   // the great peace is one settlement, not forty cessions
        var victor = w.Houses.Values.Where(h => h.Bloc == winning).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        // the first house of the winning side is the one that sits in the capital, if one does
        if (victor != null && w.Galaxy is { Capital.Length: > 0 } cap && w.PlanetHolder(cap.Capital) is { } ch && w.House(ch) is { } holder && holder.Bloc == winning) victor = holder;
        w.WorldFlags.Add(ResolvedFlag); w.WorldFlags.Add($"great_crisis:won:{winning}"); w.WorldFlags.Add("great_crisis:ended:" + ending);
        var player = w.Protagonist?.HouseId; int years = WarYears(w);
        if (player != null) w.WorldFlags.Add(w.House(player)?.Bloc == winning ? "great_crisis:player_won" : "great_crisis:player_lost");
        w.WorldCounters[CaesarSystem.GreatWarEndedKey] = w.Turn;   // the Republic is shaken by it for years (docs/CAESAR.md, the times)
        // The owner's rule (docs/PROCESS.md): the Republic stands unless the player ends it. Nobody is handed a throne. A house that has
        // crushed all resistance holds the Reach, and what it does with it is its own to decide: by the Caesar's road, or by force.
        void Raise(House h, int legitimacy, int glory)
        {
            h.Legitimacy = Math.Min(100, h.Legitimacy + legitimacy); h.Glory = Math.Min(12, h.Glory + glory);
            if (Progression.Title.RankOf(h.Title) < 4) h.Title = Progression.Title.Duke;
        }
        string after = years > 0 ? $" after {years} years" : ""; string name = WarName(w); int nth = w.Counter(WarsCounter) + 1; w.WorldCounters[WarsCounter] = nth;
        // what is kept of it for good, after its flags are cleared a generation on: when it began, when it ended, and how
        w.WorldCounters[BeganOf(nth)] = w.Counter(BeganKey); w.WorldCounters[EndedOf(nth)] = w.Turn; w.WorldFlags.Add(HowOf(nth, ending));
        if (victor == null) w.Log($"{name} ends{after}. Nobody is left to say who won it.");
        else if (ending == Crushed)
        {
            Raise(victor, 15, 5); w.WorldFlags.Add("great_crisis:victor"); w.WorldFlags.Add("great_crisis:crushed"); w.WorldCounters[CaesarSystem.ReachWonKey(victor.Id)] = w.Turn;
            if (victor.Id == player) w.WorldFlags.Add("great_crisis:player_holds_the_reach");
            w.Log($"{name} ends{after}. All resistance is crushed: {victor.Name} holds the capital and there is no fleet left that would dispute it. The Senate still sits, because {victor.Name} lets it.");
        }
        else if (ending == PartitionEnd && refuge is { } r && Polities != null)
        {
            Raise(victor, 10, 4); w.WorldFlags.Add("great_crisis:victor"); w.WorldFlags.Add("great_crisis:partition");
            w.Log($"{name} ends{after}. {victor.Name} holds the field, and cannot reach what is left of the other side.");
            Polities.Partition(w, r.leader, r.regionId, losers);
        }
        else if (ending == Held)
        {
            Raise(victor, 10, 4); w.WorldFlags.Add("great_crisis:victor"); if (victor.Id == player) w.WorldFlags.Add("great_crisis:player_holds_the_reach");
            w.Log($"{name} ends with {victor.Name} holding the field and the capital. The Senate still sits. Whether it goes on sitting is now a question people ask aloud.");
        }
        else
        {
            w.WorldFlags.Add("great_crisis:stalemate"); w.WorldCounters["stalemates"] = w.Counter("stalemates") + 1; Raise(victor, 10, 0);
            w.Log($"{name} ends in exhaustion{after}. {victor.Name} holds the field but {(decisive ? "not the capital" : "not decisively enough")}; the fleets go home and nobody calls it a victory.");
        }
        w.WorldFlags.Remove(PartitionWishFlag);
        foreach (var h in w.Houses.Values) h.Bloc = "";
    }
}
