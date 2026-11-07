using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Tuning for the fleet layer, loaded from /content/fleets (data, not code).</summary>
public sealed class FleetRules
{
    public Resources ShipCost { get; init; } = new() { Materials = 6, Manpower = 3, Credits = 2 };
    public Resources UpkeepPerStrength { get; init; } = new() { Credits = 1 };
    public int ReadinessRecovery { get; init; } = 10;
    public int ReadinessDecayMoving { get; init; } = 5;
    public int IntelDecay { get; init; } = 15;
    public int ConfidenceInTerritory { get; init; } = 100;
    public int ConfidenceAdjacent { get; init; } = 65;
    public int MovePoolCost { get; init; } = 1;

    // ---- combat (GDD §6) ----
    /// <summary>Power multiplier for the side that holds the system (before holding/building defense).</summary>
    public double DefenderBonus { get; init; } = 0.25;
    /// <summary>Share of hulls the loser loses (rounded up).</summary>
    public double LoserLossRatio { get; init; } = 0.5;
    /// <summary>Share of hulls the winner loses, scaled by how close the fight was (rounded down).</summary>
    public double WinnerLossRatio { get; init; } = 0.3;
    public int WinnerReadinessCost { get; init; } = 20;
    public int LoserReadinessCost { get; init; } = 40;
    /// <summary>Influence-pool cost to declare war / to sue for peace.</summary>
    public int DeclareWarCost { get; init; } = 2;
    public int PeaceCost { get; init; } = 1;
    /// <summary>A house whose hulls exceed yours by this ratio refuses peace — it smells victory.</summary>
    public double PeaceRefusalRatio { get; init; } = 2.0;
    /// <summary>Unrest a conquered holding carries into its new house.</summary>
    public int ConquestUnrest { get; init; } = 20;

    public static FleetRules Default { get; } = new();
}

/// <summary>
/// The fleet layer's per-turn rules (GDD §6 military pillar, §19 map behaviour): transit along lanes, readiness,
/// upkeep from the owning treasury, incursions (a fleet arriving in another house's territory), and the
/// protagonist house's <em>intel</em> picture of rival fleets. RNG-free and id-ordered: deterministic.
/// </summary>
public sealed class FleetSystem
{
    public const string IncursionsCounter = "incursions";
    public static string IncursionFlag(string houseId) => $"incursion:{houseId}";

    // engine-written, player-centric (GDD §15 L1 — the state events read)
    public const string BattlesCounter = "battles";            // every battle anywhere
    public const string BattlesWonCounter = "battles_won";     // the player's house won
    public const string BattlesLostCounter = "battles_lost";
    public const string ConquestsCounter = "conquests";        // holdings the player's house took
    public const string BattleFoughtFlag = "battle_fought";    // the player's house fought this turn (content clears it)
    public const string ConquestMadeFlag = "conquest_made";    // the player's house took a holding (content clears it)
    public const string HoldingLostFlag = "holding_lost";      // ...or lost one
    public const string WarsDeclaredCounter = "wars_declared";
    public static string AtWarFlag(string houseId) => $"at_war:{houseId}";

    private readonly FleetRules _rules;
    private readonly HoldingCatalog _catalog;
    public FleetSystem(FleetRules rules, HoldingCatalog? catalog = null) { _rules = rules; _catalog = catalog ?? HoldingCatalog.Empty; }
    public FleetRules Rules => _rules;

    /// <summary>The battles fought this turn (cleared at the next <see cref="Tick"/>), for the chronicle and UI.</summary>
    public List<BattleReport> LastBattles { get; } = new();

    // ---------------- war & peace ----------------

    public void DeclareWar(World w, string a, string b, string? byCharacterId = null)
    {
        if (a == b || w.AtWar(a, b)) return;
        w.Wars.Add(new War { HouseA = a, HouseB = b, Since = w.Turn });
        w.WorldCounters[WarsDeclaredCounter] = w.Counter(WarsDeclaredCounter) + 1;
        RefreshWarFlags(w);
        var who = byCharacterId != null ? w.Char(byCharacterId)?.Name : null;
        w.Log($"{(who ?? w.House(a)?.Name ?? a)} declares war on {w.House(b)?.Name ?? b}.");
    }

    public void MakePeace(World w, string a, string b)
    {
        int removed = w.Wars.RemoveAll(x => x.Between(a, b));
        if (removed == 0) return;
        RefreshWarFlags(w);
        w.Log($"{w.House(a)?.Name ?? a} and {w.House(b)?.Name ?? b} make peace.");
    }

    /// <summary>Would NPC house <paramref name="houseId"/> accept peace from <paramref name="from"/>? Not while it
    /// smells victory (its hulls exceed the suitor's by the refusal ratio). The player's house never auto-decides:
    /// an NPC suing for peace raises <see cref="SuingForPeaceFlag"/> on its head and the envoy event asks.</summary>
    public bool WouldAcceptPeace(World w, string houseId, string from) =>
        StrengthOf(w, houseId) <= Math.Max(1, StrengthOf(w, from)) * _rules.PeaceRefusalRatio;

    /// <summary>Char flag on an NPC head that has sued the player's house for peace; content answers it.</summary>
    public const string SuingForPeaceFlag = "suing_for_peace";

    /// <summary>Mirror the protagonist house's wars into <c>at_war:&lt;house&gt;</c> flags so content can read them.</summary>
    public static void RefreshWarFlags(World w)
    {
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("at_war:")).ToList()) w.WorldFlags.Remove(f);
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        foreach (var e in w.EnemiesOf(house)) w.WorldFlags.Add(AtWarFlag(e));
    }

    // ---------------- battle ----------------

    /// <summary>Fleets of warring houses that share a system fight — once per hostile pair per system per turn,
    /// in ordinal order so the outcome is reproducible from the seed. Call after <see cref="Tick"/>.</summary>
    public void ResolveBattles(World w, IRng rng)
    {
        LastBattles.Clear();
        if (w.Wars.Count == 0 || w.Galaxy == null) return;
        var player = w.Protagonist?.HouseId;

        foreach (var systemId in w.Fleets.Values.Where(f => !f.Moving).Select(f => f.SystemId).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList())
        {
            var houses = w.Fleets.Values.Where(f => !f.Moving && f.SystemId == systemId).Select(f => f.OwnerId).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToList();
            for (int i = 0; i < houses.Count; i++)
                for (int j = i + 1; j < houses.Count; j++)
                {
                    string a = houses[i], b = houses[j];
                    if (!w.AtWar(a, b)) continue;
                    if (StrengthAt(w, a, systemId) == 0 || StrengthAt(w, b, systemId) == 0) continue;   // already broken this turn
                    var report = Battle(w, rng, systemId, a, b);
                    LastBattles.Add(report);
                    w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
                    if (player != null && (a == player || b == player))
                    {
                        w.WorldFlags.Add(BattleFoughtFlag);
                        w.WorldCounters[report.Winner == player ? BattlesWonCounter : BattlesLostCounter] =
                            w.Counter(report.Winner == player ? BattlesWonCounter : BattlesLostCounter) + 1;
                    }
                }
        }
    }

    private BattleReport Battle(World w, IRng rng, string systemId, string a, string b)
    {
        string? owner = TerritoryOwner(w, systemId);
        // the house holding the system defends; otherwise the ordinal-first house is the nominal attacker
        string attacker = owner == a ? b : a, defender = owner == a ? a : b;
        double pa = Power(w, attacker, systemId, defending: false);
        double pd = Power(w, defender, systemId, defending: owner == defender);
        double roll = rng.NextDouble();
        bool attackerWins = roll < pa / (pa + pd);
        string winner = attackerWins ? attacker : defender, loser = attackerWins ? defender : attacker;
        double pw = attackerWins ? pa : pd, pl = attackerWins ? pd : pa;

        int loserStrength = StrengthAt(w, loser, systemId), winnerStrength = StrengthAt(w, winner, systemId);
        int loserLosses = Math.Min(loserStrength, Math.Max(1, (int)Math.Ceiling(loserStrength * _rules.LoserLossRatio)));
        int winnerLosses = Math.Min(winnerStrength - 1, (int)Math.Floor(winnerStrength * _rules.WinnerLossRatio * Math.Min(1.0, pl / pw)));
        winnerLosses = Math.Max(0, winnerLosses);

        ApplyLosses(w, loser, systemId, loserLosses, _rules.LoserReadinessCost);
        ApplyLosses(w, winner, systemId, winnerLosses, _rules.WinnerReadinessCost);
        Retreat(w, loser, systemId);

        var sys = w.Galaxy!.System(systemId)?.Name ?? systemId;
        w.Log($"Battle of {sys}: {w.House(winner)?.Name ?? winner} defeats {w.House(loser)?.Name ?? loser} " +
              $"({w.House(attacker)?.Name ?? attacker} attacking, {w.House(defender)?.Name ?? defender} holding). " +
              $"Hulls lost — {w.House(attacker)?.Name ?? attacker} {(attackerWins ? winnerLosses : loserLosses)}, {w.House(defender)?.Name ?? defender} {(attackerWins ? loserLosses : winnerLosses)}.");
        return new BattleReport(systemId, attacker, defender, winner,
            attackerWins ? winnerLosses : loserLosses, attackerWins ? loserLosses : winnerLosses, pa, pd, w.Turn);
    }

    /// <summary>Combat power: hulls × (0.5 + readiness/200), defenders × (1 + bonus + the system's holdings' defense).</summary>
    public double Power(World w, string houseId, string systemId, bool defending)
    {
        double p = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId))
            p += f.Strength * (0.5 + f.Readiness / 200.0);
        if (defending) p *= 1 + _rules.DefenderBonus + Defense(w, systemId, houseId);
        return p;
    }

    /// <summary>Fortress worlds and batteries a house holds in the system (data: spec/building `defense`).</summary>
    public double Defense(World w, string systemId, string houseId)
    {
        double d = 0;
        foreach (var h in w.Holdings.Values.Where(h => h.SystemId == systemId && h.OwnerId == houseId))
        {
            d += _catalog.Spec(h.Specialization)?.Defense ?? 0;
            foreach (var b in h.Buildings) d += _catalog.Building(b)?.Defense ?? 0;
        }
        return d;
    }

    public static int StrengthAt(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).Sum(f => f.Strength);

    private static void ApplyLosses(World w, string houseId, string systemId, int losses, int readinessCost)
    {
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId)
            .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        foreach (var f in fleets) f.Readiness = Math.Max(0, f.Readiness - readinessCost);
        while (losses > 0 && fleets.Any(f => f.Strength > 0))
        {
            var f = fleets.Where(f => f.Strength > 0).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).First();
            f.Strength -= 1; losses -= 1;
        }
        foreach (var f in fleets.Where(f => f.Strength <= 0).ToList())
        {
            w.Fleets.Remove(f.Id);
            w.Intel.Remove(f.Id);
            w.Log($"{f.Name} is destroyed.");
        }
    }

    /// <summary>Survivors fall back to the nearest system their house holds; with nowhere to go they scatter and are lost.</summary>
    private static void Retreat(World w, string houseId, string systemId)
    {
        var survivors = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (survivors.Count == 0) return;
        var havens = w.Holdings.Values.Where(h => h.OwnerId == houseId && h.SystemId != "" && h.SystemId != systemId)
            .Select(h => h.SystemId).Distinct()
            .Select(s => (s, hops: w.Galaxy!.Hops(systemId, s))).Where(x => x.hops > 0)
            .OrderBy(x => x.hops).ThenBy(x => x.s, StringComparer.Ordinal).ToList();
        foreach (var f in survivors)
        {
            if (havens.Count == 0)
            {
                w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id);
                w.Log($"{f.Name}, with no port to run to, scatters and is lost.");
                continue;
            }
            f.DestinationId = havens[0].s; f.Eta = havens[0].hops;
            w.Log($"{f.Name} falls back toward {w.Galaxy!.System(havens[0].s)?.Name ?? havens[0].s}.");
        }
    }

    // ---------------- conquest ----------------

    /// <summary>An unopposed fleet in an enemy's system takes its holdings (GDD §13 Military — "take it"; every
    /// seizure spikes rival fear via the <c>seizures</c> counter when the player does it). Call after battles.</summary>
    public void ResolveConquests(World w)
    {
        if (w.Wars.Count == 0 || w.Galaxy == null) return;
        var player = w.Protagonist?.HouseId;
        foreach (var h in w.Holdings.Values.Where(h => h.SystemId != "").OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            if (StrengthAt(w, h.OwnerId, h.SystemId) > 0) continue;                       // the owner still holds the sky
            var enemy = w.EnemiesOf(h.OwnerId).Where(e => StrengthAt(w, e, h.SystemId) > 0).FirstOrDefault();
            if (enemy == null) continue;

            string old = h.OwnerId;
            h.OwnerId = enemy;
            h.Unrest = Math.Clamp(h.Unrest + _rules.ConquestUnrest, 0, 100);
            w.WorldCounters[ConquestsCounter] = w.Counter(ConquestsCounter) + (enemy == player ? 1 : 0);
            if (enemy == player) { w.WorldFlags.Add(ConquestMadeFlag); w.WorldCounters["seizures"] = w.Counter("seizures") + 1; }
            if (old == player) w.WorldFlags.Add(HoldingLostFlag);
            w.Log($"{w.House(enemy)?.Name ?? enemy} takes {h.Name} from {w.House(old)?.Name ?? old}.");
        }
    }

    /// <summary>Call once per turn, after the economy accrues.</summary>
    public void Tick(World w)
    {
        if (w.Galaxy == null) { UpdateIntel(w); return; }

        // 1. transit + readiness
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.Moving)
            {
                f.Eta -= 1;
                f.Readiness = Math.Max(0, f.Readiness - _rules.ReadinessDecayMoving);
                if (f.Eta <= 0)
                {
                    f.SystemId = f.DestinationId;
                    f.DestinationId = "";
                    f.Eta = 0;
                    Arrive(w, f);
                }
            }
            else
            {
                f.Readiness = Math.Min(100, f.Readiness + _rules.ReadinessRecovery);
            }
        }

        // 2. upkeep — ships eat (GDD §17 Credits "the one you run out of")
        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            int strength = w.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength);
            if (strength == 0) continue;
            foreach (var r in Resource.All)
                if (_rules.UpkeepPerStrength.Get(r) != 0)
                    house.Treasury.Add(r, -_rules.UpkeepPerStrength.Get(r) * strength);
            house.Treasury.ClampNonCredit();
        }

        UpdateIntel(w);
    }

    /// <summary>A fleet entering territory another house holds is an incursion: public, remembered, and the
    /// kind of thing the §7 fear machine and authored events read (`incursions` counter, `incursion:&lt;house&gt;` flag).</summary>
    private static void Arrive(World w, Fleet f)
    {
        string? owner = TerritoryOwner(w, f.SystemId);
        var sys = w.Galaxy!.System(f.SystemId);
        if (owner != null && owner != f.OwnerId)
        {
            w.WorldCounters[IncursionsCounter] = w.Counter(IncursionsCounter) + 1;
            w.WorldFlags.Add(IncursionFlag(owner));
            w.Log($"{f.Name} of {w.House(f.OwnerId)?.Name ?? f.OwnerId} arrives at {sys?.Name ?? f.SystemId} — {w.House(owner)?.Name ?? owner}'s space.");
        }
        else
        {
            w.Log($"{f.Name} arrives at {sys?.Name ?? f.SystemId}.");
        }
    }

    /// <summary>The house holding a system (by its holdings), or null for the frontier. Ties → ordinal id.</summary>
    public static string? TerritoryOwner(World w, string systemId)
    {
        var owners = w.Holdings.Values.Where(h => h.SystemId == systemId)
            .GroupBy(h => h.OwnerId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
        return owners.Count == 0 ? null : owners[0];
    }

    /// <summary>Total fleet strength a house commands.</summary>
    public static int StrengthOf(World w, string houseId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId).Sum(f => f.Strength);

    /// <summary>Systems the protagonist's house can see into: its territory and everything one lane out.</summary>
    public static HashSet<string> Watched(World w)
    {
        var set = new HashSet<string>();
        var house = w.Protagonist?.HouseId;
        if (w.Galaxy == null || house == null) return set;
        var own = w.Holdings.Values.Where(h => h.OwnerId == house && h.SystemId != "").Select(h => h.SystemId).Distinct().ToList();
        foreach (var s in own) { set.Add(s); foreach (var n in w.Galaxy.Neighbors(s)) set.Add(n); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house && !f.Moving)) { set.Add(f.SystemId); foreach (var n in w.Galaxy.Neighbors(f.SystemId)) set.Add(n); }
        return set;
    }

    /// <summary>Refresh the protagonist house's picture of rival fleets (GDD §19 mixed visibility).</summary>
    public void UpdateIntel(World w)
    {
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        var ownSystems = w.Holdings.Values.Where(h => h.OwnerId == house).Select(h => h.SystemId).ToHashSet();
        var watched = Watched(w);

        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.OwnerId == house) continue;
            string where = f.Moving ? f.DestinationId : f.SystemId;   // a fleet in transit is seen where it is bound
            bool seen = watched.Contains(f.SystemId) || (f.Moving && watched.Contains(f.DestinationId));
            if (seen)
            {
                w.Intel[f.Id] = new FleetIntel
                {
                    FleetId = f.Id, SystemId = f.Moving ? f.SystemId : where, Strength = f.Strength,
                    Confidence = ownSystems.Contains(f.SystemId) ? _rules.ConfidenceInTerritory : _rules.ConfidenceAdjacent,
                    TurnObserved = w.Turn
                };
            }
            else if (w.Intel.TryGetValue(f.Id, out var old))
            {
                old.Confidence = Math.Max(0, old.Confidence - _rules.IntelDecay);
            }
        }
        // fleets that no longer exist fall out of the picture
        foreach (var id in w.Intel.Keys.Where(id => !w.Fleets.ContainsKey(id)).ToList()) w.Intel.Remove(id);
    }
}
