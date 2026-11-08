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
    public double DefenderBonus { get; init; } = 0.25;
    public double LoserLossRatio { get; init; } = 0.5;
    public double WinnerLossRatio { get; init; } = 0.3;
    public int WinnerReadinessCost { get; init; } = 20;
    public int LoserReadinessCost { get; init; } = 40;
    public int DeclareWarCost { get; init; } = 2;
    public int PeaceCost { get; init; } = 1;
    public double PeaceRefusalRatio { get; init; } = 2.0;
    public int ConquestUnrest { get; init; } = 20;

    // ---- classes, admirals, sieges, war score ----
    public string DefaultClass { get; init; } = "frigate";
    public int SiegeTurns { get; init; } = 2;
    public int VeterancyMax { get; init; } = 5;
    public double VeterancyBonus { get; init; } = 0.05;
    public double AdmiralDeathChance { get; init; } = 0.12;
    public int WarWearinessTurns { get; init; } = 6;
    public int TributePerScore { get; init; } = 3;

    public static FleetRules Default { get; } = new();
}

/// <summary>
/// The fleet layer (GDD §6 military pillar, §8 office/house fleets, §19 map behaviour): transit along lanes,
/// readiness, upkeep by hull class, incursions, the protagonist house's intel, <b>battle</b> (composition ×
/// readiness × admiral × veterancy, defenders fortified by their holdings), <b>sieges</b> that end in conquest,
/// war score and weariness. RNG only where a fight is decided; everything else id-ordered and deterministic.
/// </summary>
public sealed class FleetSystem
{
    public const string IncursionsCounter = "incursions";
    public static string IncursionFlag(string houseId) => $"incursion:{houseId}";
    public const string BattlesCounter = "battles";
    public const string BattlesWonCounter = "battles_won";
    public const string BattlesLostCounter = "battles_lost";
    public const string ConquestsCounter = "conquests";
    public const string BattleFoughtFlag = "battle_fought";
    public const string ConquestMadeFlag = "conquest_made";
    public const string HoldingLostFlag = "holding_lost";
    public const string UnderSiegeFlag = "under_siege";          // one of the player's holdings is blockaded
    public const string WarsDeclaredCounter = "wars_declared";
    public static string AtWarFlag(string houseId) => $"at_war:{houseId}";
    public const string SuingForPeaceFlag = "suing_for_peace";
    public const string AdmiralFlag = "admiral";                  // the character commands a fleet

    private readonly FleetRules _rules;
    private readonly HoldingCatalog _catalog;
    private readonly ShipClassCatalog _classes;
    private readonly FrictionTable _friction;

    public FleetSystem(FleetRules rules, HoldingCatalog? catalog = null, ShipClassCatalog? classes = null, FrictionTable? friction = null)
    {
        _rules = rules; _catalog = catalog ?? HoldingCatalog.Empty; _classes = classes ?? ShipClassCatalog.Empty; _friction = friction ?? FrictionTable.Empty;
    }

    public FleetRules Rules => _rules;
    public ShipClassCatalog Classes => _classes;
    public List<BattleReport> LastBattles { get; } = new();

    private ShipClassDef Class(string id) => _classes.Resolve(id);
    private ShipClassDef DefaultClass => _classes.Resolve(_rules.DefaultClass);

    // ---------------- composition ----------------

    /// <summary>Hulls per class, materialising the default class for fleets authored by strength alone.</summary>
    public IEnumerable<(ShipClassDef cls, int count)> Composition(Fleet f)
    {
        if (f.Ships.Count == 0) { if (f.Strength > 0) yield return (DefaultClass, f.Strength); yield break; }
        foreach (var kv in f.Ships.OrderBy(k => k.Key, StringComparer.Ordinal)) if (kv.Value > 0) yield return (Class(kv.Key), kv.Value);
    }

    public string Describe(Fleet f) =>
        string.Join(", ", Composition(f).Select(c => $"{c.count} {c.cls.Name.ToLowerInvariant()}{(c.count == 1 ? "" : "s")}"));

    /// <summary>Per-turn upkeep of a fleet from its classes.</summary>
    public Resources Upkeep(Fleet f)
    {
        var r = new Resources();
        foreach (var (cls, n) in Composition(f)) foreach (var k in Resource.All) r.Add(k, cls.Upkeep.Get(k) * n);
        return r;
    }

    // ---------------- the turn ----------------

    public void Tick(World w)
    {
        LastBattles.Clear();
        if (w.Galaxy == null) { UpdateIntel(w); return; }

        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.Moving)
            {
                f.Eta -= 1;
                f.Readiness = Math.Max(0, f.Readiness - _rules.ReadinessDecayMoving);
                if (f.Eta <= 0) { f.SystemId = f.DestinationId; f.DestinationId = ""; f.Eta = 0; Arrive(w, f); }
            }
            else f.Readiness = Math.Min(100, f.Readiness + _rules.ReadinessRecovery);
        }

        foreach (var house in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var bill = new Resources();
            foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id)) bill.Add(Upkeep(f));
            if (bill.IsEmpty()) continue;
            foreach (var r in Resource.All) house.Treasury.Add(r, -bill.Get(r));
            house.Treasury.ClampNonCredit();
            Desert(w, house);
        }

        RefreshAdmiralFlags(w);
        WarWeariness(w);
        UpdateIntel(w);
    }

    /// <summary>Credits a house is allowed to owe before its unpaid crews start walking (GDD §17 "the one you run out of").</summary>
    public const int DesertionDebt = 20;
    public const int UnpaidReadinessLoss = 10;
    public const string DesertionsCounter = "desertions";

    /// <summary>An insolvent house cannot pay its crews: readiness slides, and past <see cref="DesertionDebt"/> one hull a turn
    /// slips away from the least-ready fleet. Debt is therefore self-limiting — a fleet shrinks until its upkeep is payable —
    /// and the chronicle says why the picket thinned.</summary>
    private void Desert(World w, House house)
    {
        if (house.Treasury.Credits >= 0) return;
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (fleets.Count == 0) return;
        foreach (var f in fleets) f.Readiness = Math.Max(0, f.Readiness - UnpaidReadinessLoss);
        if (house.Treasury.Credits > -DesertionDebt) return;

        var victim = fleets.OrderBy(f => f.Readiness).ThenBy(f => f.Id, StringComparer.Ordinal).First();
        if (victim.Ships.Count > 0)
        {
            var cls = victim.Ships.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).First().Key;
            victim.Ships[cls] -= 1;
            if (victim.Ships[cls] == 0) victim.Ships.Remove(cls);
            victim.Recount();
            if (victim.Ships.Count == 0) victim.Strength = 0;
        }
        else victim.Strength -= 1;
        w.WorldCounters[DesertionsCounter] = w.Counter(DesertionsCounter) + 1;
        if (victim.Strength <= 0)
        {
            w.Fleets.Remove(victim.Id); w.Intel.Remove(victim.Id);
            w.Log($"Unpaid for too long, the last crews of {victim.Name} desert; {house.Name} has no fleet left there.");
        }
        else w.Log($"Unpaid crews desert {victim.Name}: {house.Name} owes {-house.Treasury.Credits} credits it does not have.");
    }

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
        else w.Log($"{f.Name} arrives at {sys?.Name ?? f.SystemId}.");
    }

    public static string? TerritoryOwner(World w, string systemId)
    {
        var owners = w.Holdings.Values.Where(h => h.SystemId == systemId)
            .GroupBy(h => h.OwnerId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
        return owners.Count == 0 ? null : owners[0];
    }

    public static int StrengthOf(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId).Sum(f => f.Strength);
    public static int StrengthAt(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).Sum(f => f.Strength);

    /// <summary>Characters commanding a fleet carry the <c>admiral</c> flag so content can bind "the admiral".</summary>
    public static void RefreshAdmiralFlags(World w)
    {
        var admirals = w.Fleets.Values.Select(f => f.AdmiralId).Where(a => a.Length > 0).ToHashSet();
        foreach (var c in w.Characters.Values)
        {
            if (admirals.Contains(c.Id) && c.Alive) c.Flags.Add(AdmiralFlag); else c.Flags.Remove(AdmiralFlag);
        }
        foreach (var f in w.Fleets.Values) if (f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is not { Alive: true }) f.AdmiralId = "";
    }

    /// <summary>Long wars grind: past the weariness threshold, every holding of a warring house grows restless each turn.</summary>
    public const int ExhaustionTurns = 12;   // a war between rivals with no battle this long ends in a white peace

    private void WarWeariness(World w)
    {
        foreach (var war in w.Wars)
        {
            if (w.Turn - war.Since < _rules.WarWearinessTurns) continue;
            foreach (var h in w.Holdings.Values.Where(h => war.Involves(h.OwnerId))) h.Unrest = Math.Min(100, h.Unrest + 2);
        }
        // exhaustion (GAP §7 "wars are frequent and bloodless"): two rival houses that have not met in battle for twelve turns go home
        var player = w.Protagonist?.HouseId;
        foreach (var war in w.Wars.Where(x => !x.Involves(player ?? "") && w.Turn - Math.Max(x.Since, x.LastBattleTurn) >= ExhaustionTurns).ToList())
        {
            w.Log($"The war between {w.House(war.HouseA)?.Name ?? war.HouseA} and {w.House(war.HouseB)?.Name ?? war.HouseB} ends in exhaustion; no fleet has met another in a dozen turns.");
            MakePeace(w, war.HouseA, war.HouseB, terms: false);
        }
    }

    public static HashSet<string> Watched(World w)
    {
        var set = new HashSet<string>();
        var house = w.Protagonist?.HouseId;
        if (w.Galaxy == null || house == null) return set;
        foreach (var s in w.Holdings.Values.Where(h => h.OwnerId == house && h.SystemId != "").Select(h => h.SystemId).Distinct().ToList())
        { set.Add(s); foreach (var n in w.Galaxy.Neighbors(s)) set.Add(n); }
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house && !f.Moving)) { set.Add(f.SystemId); foreach (var n in w.Galaxy.Neighbors(f.SystemId)) set.Add(n); }
        return set;
    }

    public void UpdateIntel(World w)
    {
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        var ownSystems = w.Holdings.Values.Where(h => h.OwnerId == house).Select(h => h.SystemId).ToHashSet();
        var watched = Watched(w);
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (f.OwnerId == house) continue;
            bool seen = watched.Contains(f.SystemId) || (f.Moving && watched.Contains(f.DestinationId)) || f.OwnerId == PirateSystem.HouseId;   // raiders are talked about on every lane
            if (seen)
                w.Intel[f.Id] = new FleetIntel { FleetId = f.Id, SystemId = f.SystemId, Strength = f.Strength,
                    Confidence = ownSystems.Contains(f.SystemId) ? _rules.ConfidenceInTerritory : _rules.ConfidenceAdjacent, TurnObserved = w.Turn };
            else if (w.Intel.TryGetValue(f.Id, out var old)) old.Confidence = Math.Max(0, old.Confidence - _rules.IntelDecay);
        }
        foreach (var id in w.Intel.Keys.Where(id => !w.Fleets.ContainsKey(id)).ToList()) w.Intel.Remove(id);
    }

    // ---------------- war & peace ----------------

    public void DeclareWar(World w, string a, string b, string? byCharacterId = null)
    {
        if (a == b || w.AtWar(a, b)) return;
        w.Wars.Add(new War { HouseA = a, HouseB = b, Since = w.Turn });
        w.WorldCounters[WarsDeclaredCounter] = w.Counter(WarsDeclaredCounter) + 1;
        RefreshWarFlags(w);
        var who = byCharacterId != null ? w.Char(byCharacterId) : null;
        w.Log($"{(who?.Name ?? w.House(a)?.Name ?? a)} declares war on {w.House(b)?.Name ?? b}.");
        if (who != null) _friction.Apply(w, who, "declare_war");
    }

    public const int CessionLead = 10;   // the side this far behind cedes a world at the peace

    public void MakePeace(World w, string a, string b) => MakePeace(w, a, b, terms: true);

    /// <summary>End a war. With <paramref name="terms"/>, the house behind by <see cref="CessionLead"/> or more cedes its least peopled
    /// world (never the capital) to the winner: wars have prizes (GAP §7).</summary>
    public void MakePeace(World w, string a, string b, bool terms)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(a, b));
        if (war == null) return;
        w.Wars.Remove(war);
        if (terms)
        {
            string? loser = war.Lead(a) >= CessionLead ? b : war.Lead(b) >= CessionLead ? a : null;
            if (loser != null)
            {
                string winner = loser == a ? b : a;
                var ceded = w.HoldingsOf(loser).Where(h => h.IsPlanet && !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (ceded != null && w.HoldingsOf(loser).Count(h => h.IsPlanet) > 1)
                {
                    ceded.FormerOwnerId = loser; ceded.OwnerId = winner; ceded.Unrest = Math.Min(100, ceded.Unrest + 10);
                    var newGov = Economy.HeadOf(w, w.House(winner)!); if (newGov != null) ceded.GovernorId = newGov.Id;
                    w.WorldCounters["cessions"] = w.Counter("cessions") + 1;
                    var player = w.Protagonist?.HouseId; if (winner == player) w.WorldFlags.Add("won_a_world"); if (loser == player) w.WorldFlags.Add("ceded_a_world");
                    w.Log($"By the terms of the peace, {w.House(loser)?.Name ?? loser} cedes {ceded.Name} to {w.House(winner)?.Name ?? winner}.");
                }
            }
        }
        foreach (var s in w.Sieges.Values.Where(s => (war.Involves(s.BesiegerId)) && war.Involves(w.Holding(s.HoldingId)?.OwnerId ?? "")).ToList()) w.Sieges.Remove(s.HoldingId);
        foreach (var c in w.Characters.Values.Where(c => war.Involves(c.HouseId))) c.Flags.Remove(SuingForPeaceFlag);
        RefreshWarFlags(w);
        RefreshSiegeFlag(w);
        w.Log($"{w.House(a)?.Name ?? a} and {w.House(b)?.Name ?? b} make peace.");
    }

    /// <summary>Tribute the lower-scoring side pays the higher on peace (credits), from the war's score difference.</summary>
    public int Tribute(World w, string from, string to)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(from, to));
        if (war == null) return 0;
        int lead = war.Lead(to);
        return lead > 0 ? Math.Min(w.House(from)?.Treasury.Credits ?? 0, lead * _rules.TributePerScore) : 0;
    }

    /// <summary>Would NPC house <paramref name="houseId"/> accept peace from <paramref name="from"/>? Not while it smells
    /// victory — ahead on war score, or hulls past the refusal ratio.</summary>
    public bool WouldAcceptPeace(World w, string houseId, string from)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(houseId, from));
        if (war != null && war.Lead(houseId) >= 5 && StrengthOf(w, houseId) >= StrengthOf(w, from)) return false;
        return StrengthOf(w, houseId) <= Math.Max(1, StrengthOf(w, from)) * _rules.PeaceRefusalRatio;
    }

    public static void RefreshWarFlags(World w)
    {
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("at_war:")).ToList()) w.WorldFlags.Remove(f);
        w.WorldFlags.Remove("at_war_any");
        var house = w.Protagonist?.HouseId;
        if (house == null) return;
        foreach (var e in w.EnemiesOf(house)) w.WorldFlags.Add(AtWarFlag(e));
        if (w.EnemiesOf(house).Any()) w.WorldFlags.Add("at_war_any");   // the generic flag content reads (GAP_ANALYSIS §4 item 4)
    }

    private static void RefreshSiegeFlag(World w)
    {
        var house = w.Protagonist?.HouseId;
        bool any = house != null && w.Sieges.Values.Any(s => w.Holding(s.HoldingId)?.OwnerId == house);
        if (any) w.WorldFlags.Add(UnderSiegeFlag); else w.WorldFlags.Remove(UnderSiegeFlag);
    }

    // ---------------- battle ----------------

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
                    if (!w.AtWar(a, b) || StrengthAt(w, a, systemId) == 0 || StrengthAt(w, b, systemId) == 0) continue;
                    var report = Battle(w, rng, systemId, a, b);
                    LastBattles.Add(report);
                    w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
                    var scored = w.Wars.FirstOrDefault(x => x.Between(a, b)); if (scored != null) { scored.AddScore(report.Winner, 2); scored.LastBattleTurn = w.Turn; }   // raiders have no war to score
                    if (player != null && (a == player || b == player))
                    {
                        w.WorldFlags.Add(BattleFoughtFlag);
                        bool won = report.Winner == player;
                        w.WorldCounters[won ? BattlesWonCounter : BattlesLostCounter] = w.Counter(won ? BattlesWonCounter : BattlesLostCounter) + 1;
                        _friction.Apply(w, w.Protagonist, won ? "battle_won" : "battle_lost");
                    }
                }
        }
    }

    private BattleReport Battle(World w, IRng rng, string systemId, string a, string b)
    {
        string? owner = TerritoryOwner(w, systemId);
        string attacker = owner == a ? b : a, defender = owner == a ? a : b;
        double pa = Power(w, attacker, systemId, defending: false);
        double pd = Power(w, defender, systemId, defending: owner == defender);
        double roll = rng.NextDouble();
        bool attackerWins = roll < pa / (pa + pd);
        string winner = attackerWins ? attacker : defender, loser = attackerWins ? defender : attacker;
        double pw = attackerWins ? pa : pd, pl = attackerWins ? pd : pa;

        int loserStrength = StrengthAt(w, loser, systemId), winnerStrength = StrengthAt(w, winner, systemId);
        int loserLosses = Math.Min(loserStrength, Math.Max(1, (int)Math.Ceiling(loserStrength * _rules.LoserLossRatio)));
        int winnerLosses = Math.Max(0, Math.Min(winnerStrength - 1, (int)Math.Floor(winnerStrength * _rules.WinnerLossRatio * Math.Min(1.0, pl / pw))));

        var loserAdmirals = Admirals(w, loser, systemId); var winnerAdmirals = Admirals(w, winner, systemId);
        ApplyLosses(w, loser, systemId, loserLosses, _rules.LoserReadinessCost);
        ApplyLosses(w, winner, systemId, winnerLosses, _rules.WinnerReadinessCost);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == winner && !f.Moving && f.SystemId == systemId))
            f.Veterancy = Math.Min(_rules.VeterancyMax, f.Veterancy + 1);
        AdmiralOutcomes(w, rng, winnerAdmirals, loserAdmirals);
        Retreat(w, loser, systemId);

        var sys = w.Galaxy!.System(systemId)?.Name ?? systemId;
        w.Log($"Battle of {sys}: {w.House(winner)?.Name ?? winner} defeats {w.House(loser)?.Name ?? loser} " +
              $"({w.House(attacker)?.Name ?? attacker} attacking, {w.House(defender)?.Name ?? defender} holding). " +
              $"Hulls lost — {w.House(attacker)?.Name ?? attacker} {(attackerWins ? winnerLosses : loserLosses)}, {w.House(defender)?.Name ?? defender} {(attackerWins ? loserLosses : winnerLosses)}.");
        return new BattleReport(systemId, attacker, defender, winner, attackerWins ? winnerLosses : loserLosses, attackerWins ? loserLosses : winnerLosses, pa, pd, w.Turn);
    }

    private static List<Character> Admirals(World w, string houseId, string systemId) =>
        w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId && f.AdmiralId.Length > 0)
            .Select(f => w.Char(f.AdmiralId)).Where(c => c is { Alive: true }).Select(c => c!).Distinct().OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

    /// <summary>The victor's admiral learns (tactics, then gunnery); the loser's is wounded — or, on a seeded roll, killed
    /// (GDD §5 "losing a Fleet Admiral means the heir starts as a green cadet"; a dead protagonist triggers succession).</summary>
    private void AdmiralOutcomes(World w, IRng rng, List<Character> winners, List<Character> losers)
    {
        foreach (var c in winners)
        {
            string skill = c.Skill("tactics") <= c.Skill("gunnery") ? "tactics" : "gunnery";
            c.Skills[skill] = c.Skill(skill) + 1;
            w.Log($"{c.Name} learns from the victory ({skill} {c.Skill(skill)}).");
        }
        foreach (var c in losers)
        {
            if (rng.NextDouble() < _rules.AdmiralDeathChance)
            {
                w.Log($"{c.Name} goes down with the line.");
                DynastySystem.Die(w, c.Id);
            }
            else
            {
                c.Flags.Add("wounded");
                c.Stress = Math.Min(100, c.Stress + 10);
            }
        }
    }

    /// <summary>Power = Σ hulls × class stat (attack assaulting, defense holding) × (0.5 + readiness/200) × admiral × veterancy;
    /// defenders × (1 + bonus + the system's holdings' defense).</summary>
    public double Power(World w, string houseId, string systemId, bool defending)
    {
        double p = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId))
        {
            double hulls = 0;
            foreach (var (cls, n) in Composition(f)) hulls += n * (defending ? cls.Defense : cls.Attack);
            p += hulls * (0.5 + f.Readiness / 200.0) * AdmiralFactor(w, f) * (1 + f.Veterancy * _rules.VeterancyBonus);
        }
        if (defending) p *= 1 + _rules.DefenderBonus + Defense(w, systemId, houseId);
        return p;
    }

    /// <summary>The admiral's skill (GDD §6): tactics 5%, gunnery 3%, leadership 2% per point; Tactician +10%, Strategist +5%;
    /// a wounded or broken admiral −10%. Unled fleets fight at 0.85.</summary>
    public static double AdmiralFactor(World w, Fleet f)
    {
        var c = f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId) : null;
        if (c is not { Alive: true }) return 0.85;
        double k = 1.0 + c.Skill("tactics") * 0.05 + c.Skill("gunnery") * 0.03 + c.Skill("leadership") * 0.02;
        if (c.AptitudeTraits.Contains("Tactician")) k += 0.10;
        if (c.AptitudeTraits.Contains("Strategist")) k += 0.05;
        if (c.Flags.Contains("wounded") || c.Flags.Contains(DynastySystem.BrokenFlag)) k -= 0.10;
        return Math.Clamp(k, 0.6, 2.0);
    }

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

    /// <summary>Hulls are lost screen-first: the most numerous (cheapest) class in the largest fleet goes first.</summary>
    private void ApplyLosses(World w, string houseId, string systemId, int losses, int readinessCost)
    {
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).ToList();
        foreach (var f in fleets) f.Readiness = Math.Max(0, f.Readiness - readinessCost);
        while (losses > 0 && fleets.Any(f => f.Strength > 0))
        {
            var f = fleets.Where(f => f.Strength > 0).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).First();
            if (f.Ships.Count > 0)
            {
                var cls = f.Ships.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ThenBy(k => Class(k.Key).Attack).ThenBy(k => k.Key, StringComparer.Ordinal).First().Key;
                f.Ships[cls] -= 1;
                if (f.Ships[cls] == 0) f.Ships.Remove(cls);
                f.Recount();
                if (f.Ships.Count == 0) f.Strength = 0;
            }
            else f.Strength -= 1;
            losses -= 1;
        }
        foreach (var f in fleets.Where(f => f.Strength <= 0).ToList())
        {
            w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id);
            w.Log($"{f.Name} is destroyed.");
        }
    }

    private static void Retreat(World w, string houseId, string systemId)
    {
        var survivors = w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Moving && f.SystemId == systemId).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (survivors.Count == 0) return;
        var havens = w.Holdings.Values.Where(h => h.OwnerId == houseId && h.SystemId != "" && h.SystemId != systemId)
            .Select(h => h.SystemId).Distinct().Select(s => (s, hops: w.Galaxy!.Hops(systemId, s))).Where(x => x.hops > 0)
            .OrderBy(x => x.hops).ThenBy(x => x.s, StringComparer.Ordinal).ToList();
        foreach (var f in survivors)
        {
            if (havens.Count == 0) { w.Fleets.Remove(f.Id); w.Intel.Remove(f.Id); w.Log($"{f.Name}, with no port to run to, scatters and is lost."); continue; }
            f.DestinationId = havens[0].s; f.Eta = havens[0].hops;
            w.Log($"{f.Name} falls back toward {w.Galaxy!.System(havens[0].s)?.Name ?? havens[0].s}.");
        }
    }

    // ---------------- siege & conquest ----------------

    /// <summary>Turns a blockade needs to take a holding: the base plus two per point of its defense.</summary>
    public int SiegeRequired(World w, Holding h) =>
        _rules.SiegeTurns + (int)Math.Ceiling(2 * ((_catalog.Spec(h.Specialization)?.Defense ?? 0) + h.Buildings.Sum(b => _catalog.Building(b)?.Defense ?? 0)));

    /// <summary>Advance sieges: an enemy fleet alone in the sky over a holding tightens the blockade; when the clock
    /// reaches the holding's resistance it changes hands (GDD §13 Military — "take it"). A contested sky lifts the siege.</summary>
    public void ResolveConquests(World w)
    {
        if (w.Galaxy == null) return;
        var player = w.Protagonist?.HouseId;

        foreach (var h in w.Holdings.Values.Where(h => h.SystemId != "").OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var besieger = StrengthAt(w, h.OwnerId, h.SystemId) > 0 ? null
                : w.EnemiesOf(h.OwnerId).FirstOrDefault(e => StrengthAt(w, e, h.SystemId) > 0);
            if (besieger == null)
            {
                if (w.Sieges.Remove(h.Id)) w.Log($"The blockade of {h.Name} is lifted.");
                continue;
            }
            if (!w.Sieges.TryGetValue(h.Id, out var siege) || siege.BesiegerId != besieger)
            {
                siege = new Siege { HoldingId = h.Id, BesiegerId = besieger, Turns = 0, Required = SiegeRequired(w, h) };
                w.Sieges[h.Id] = siege;
                w.Log($"{w.House(besieger)?.Name ?? besieger} lays siege to {h.Name} ({siege.Required} turns to fall).");
            }
            siege.Turns += 1;
            if (siege.Turns < siege.Required) continue;

            w.Sieges.Remove(h.Id);
            Conquer(w, h, besieger, player);
        }
        RefreshSiegeFlag(w);
    }

    private void Conquer(World w, Holding h, string conqueror, string? player)
    {
        string old = h.OwnerId;
        h.OwnerId = conqueror; h.FormerOwnerId = old;
        h.Unrest = Math.Clamp(h.Unrest + _rules.ConquestUnrest, 0, 100);
        w.Wars.FirstOrDefault(x => x.Between(old, conqueror))?.AddScore(conqueror, 3);
        if (conqueror == player)
        {
            w.WorldCounters[ConquestsCounter] = w.Counter(ConquestsCounter) + 1;
            w.WorldFlags.Add(ConquestMadeFlag);
            w.WorldCounters["seizures"] = w.Counter("seizures") + 1;
            _friction.Apply(w, w.Protagonist, "conquest");
        }
        if (old == player) { w.WorldFlags.Add(HoldingLostFlag); _friction.Apply(w, w.Protagonist, "holding_lost"); }
        w.Log($"{w.House(conqueror)?.Name ?? conqueror} takes {h.Name} from {w.House(old)?.Name ?? old}.");

        // titles follow the land (GDD §13): a house stripped of its last holding falls to landless, and the
        // conqueror gains a claim to the rung it held — pressable through the Intrigue path.
        var loser = w.House(old);
        if (loser != null && !w.HoldingsOf(old).Any() && loser.Title != "landless")
        {
            string fallen = loser.Title;
            loser.Title = "landless";
            w.House(conqueror)?.Claims.Add(fallen);
            w.Log($"{loser.Name}, landless, loses its {fallen} rank; {w.House(conqueror)?.Name ?? conqueror} holds a claim to it.");
        }
    }
}
