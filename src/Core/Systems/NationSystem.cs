using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The nations beyond the Reach (docs/FOREIGN_NATIONS.md; the owner's direction: one playable nation, the others off the map as the targets of
/// its wars, in the mould of LOTGH's single strong neighbour and Rome's many). RNG-free: every roll is a hash of the turn and the parties.
/// Each turn, after the fleets at home have moved and fought: nations regain strength and drift in their regard for the Reach; the aggressive
/// and the decaying raid the rim when their regard is low; expeditions sail, fight the real battle math against the nation's committed strength,
/// and come home with plunder, valour and fewer hulls; a war ends when either side's score passes the nation's threshold (the Reach subdues it
/// and takes tribute for twenty turns, or the chair buys peace), or when twelve turns pass without a battle.
/// </summary>
public sealed partial class NationSystem
{
    public const int SubduedTurns = 20, FightTurns = 4, ExhaustionTurns = 12, RegenEvery = 5, RaidEvery = 6, RaidUnrest = 8, RaidCredits = 5, RaidMinGap = 4;
    public const double NationPowerPerStrength = 1.0, NationReserve = 1.25, WinStrengthLoss = 0.4, WinHullLoss = 0.15, LossHullLoss = 0.3;
    public const int ScorePerBattle = 2, ExpeditionSupplyDrain = 10, ReadinessWin = 15, ReadinessLoss = 30, LegitimacyTriumph = 8, InfluenceTriumph = 4, LegitimacyDefeat = 6;
    /// <summary>One-turn flags this system raises in BeginTurn, cleared at the top of its next Tick.</summary>
    public static readonly string[] TurnFlags = { "fleet_sailed_abroad", "expedition_returned", "expedition_lost", "expedition_victory", "expedition_defeat", "nation_subdued", "triumph", "war_lost_abroad", "raided_by_nation", "foreign_peace", "nation_freed" , "raid_met_at_the_march", "nation_succession", "nation_commander_fallen" };
    /// <summary>One-turn flags raised in EndTurn (by the Senate's vote or the player's order), cleared with the Senate's at the top of the next EndTurn so the next turn's events see them.</summary>
    public static readonly string[] SenateTurnFlags = { "foreign_war_declared" };
    public const string WarsCounter = "foreign_wars", ExpeditionsCounter = "expeditions", BattlesCounter = "foreign_battles", SubduedCounter = "nations_subdued", TributeCounter = "tribute_received", RaidsCounter = "foreign_raids", LostCounter = "foreign_wars_lost";
    public const string RaidsMetCounter = "raids_met_at_the_march", RaidsRepelledCounter = "raids_turned_back", CommandersFallenCounter = "nation_commanders_fallen", SuccessionsCounter = "nation_rulers_succeeded";
    public const int RaidReach = 2, MarchGuard = 3, HopsPerYear = 3, CommanderDeathPct = 25, RulerOldAge = 58;
    public const double PerSkill = 0.06;
    public static readonly string[] Natures = { "warlike", "wary", "grasping", "mild" };
    public static string WinsKey(string houseId, string nationId) => $"expedition_wins:{houseId}:{nationId}";
    public static string RaidedKey(string houseId) => "raided_turn:" + houseId;
    public static string RaidedByKey(string houseId) => "raided_by:" + houseId;
    /// <summary>The nation that last raided a world of the house within the turns given, or null.</summary>
    public static string? RaiderOf(World w, string houseId, int withinTurns) =>
        w.WorldCounters.TryGetValue(RaidedKey(houseId), out var t) && w.Turn - t <= withinTurns && w.WorldCounters.TryGetValue(RaidedByKey(houseId), out var i) && i >= 0 && i < w.Nations.Count ? w.Nations[i].Id : null;

    private readonly NationCatalog _catalog; private readonly FleetSystem _fleets;
    public NationSystem(NationCatalog catalog, FleetSystem fleets) { _catalog = catalog; _fleets = fleets; }
    public NationCatalog Catalog => _catalog;

    /// <summary>Create the nations from content at the start of a run (idempotent: a nation already in the World is kept).</summary>
    public static void Seed(World w, NationCatalog catalog)
    {
        foreach (var d in catalog.Defs.OrderBy(d => d.Id, StringComparer.Ordinal))
            if (w.Nation(d.Id) == null) w.Nations.Add(new Nation { Id = d.Id, Strength = d.Strength, Disposition = d.Disposition });
        foreach (var n in w.Nations) if (catalog.Def(n.Id) is { } def) { People(w, n, def); Place(w, n, def); }
    }

    // ---- a face (docs/BEYOND.md, stage 0): a ruler and a commander of the fleets, with names of the nation's own ----

    private static string NextName(Nation n, NationDef d) => d.Names.Count == 0 ? $"the {d.RulerTitle} of {d.Name}" : d.Names[n.NamesUsed++ % d.Names.Count];
    private static NationPerson NewRuler(World w, Nation n, NationDef d, string? nature = null)
    {
        uint h = Hash($"ruler:{n.Salt}:{n.Id}:{n.NamesUsed}:{w.Turn}");
        // half the time the kind of ruler its temper makes; otherwise anyone
        string given = nature ?? (h / 7 % 2 == 0 ? d.Temper switch { "aggressive" => "warlike", "mercantile" => "grasping", "decaying" => "mild", _ => "wary" } : Natures[(int)(h / 31 % (uint)Natures.Length)]);
        return new NationPerson { Name = NextName(n, d), Age = 36 + (int)(h / 101 % 20), Nature = given, Since = w.Turn };
    }
    private static NationPerson NewCommander(World w, Nation n, NationDef d)
    {
        uint h = Hash($"commander:{n.Salt}:{n.Id}:{n.NamesUsed}:{w.Turn}");
        return new NationPerson { Name = NextName(n, d), Age = 28 + (int)(h / 101 % 22), Skill = 2 + (int)(h / 7 % 5) + (d.Temper == "defensive" ? 1 : 0), Since = w.Turn };
    }
    /// <summary>Make sure the nation has a ruler and a commander: at the start of a run, and for a save made before they had any.</summary>
    public static void People(World w, Nation n, NationDef d)
    {
        if (n.Salt == 0) { n.Salt = (w.RngState ^ Hash("salt:" + n.Id)) | 1; n.NamesUsed = (int)(n.Salt / 7 % (ulong)Math.Max(1, d.Names.Count)); }
        n.Ruler ??= NewRuler(w, n, d); n.Commander ??= NewCommander(w, n, d);
    }
    /// <summary>What the commander of a nation's fleets is worth to them in a battle: six in a hundred for every point of skill over four, and as much less under it.</summary>
    public static double CommanderFactor(NationPerson? c) => c == null ? 1.0 : 1 + (c.Skill - 4) * PerSkill;
    public static string Styled(NationDef d, NationPerson? person, bool ruler) => person == null ? "" : $"{(ruler ? d.RulerTitle : d.CommanderTitle)} {person.Name}";

    /// <summary>Rulers age and die. The commander of the fleets takes the chair one time in three, if they are any good; otherwise someone new.
    /// A new ruler has a nature of their own, and the nation's regard for the Reach moves with it at once.</summary>
    private static void Reign(World w, Nation n, NationDef d, string? mine)
    {
        People(w, n, d); var r = n.Ruler!; r.Age++; if (n.Commander is { } c) c.Age++;
        if (n.Commander is { Age: >= 66 } old) { w.Log($"{Styled(d, old, false)} of {d.Name} lays down the command, old."); n.Commander = NewCommander(w, n, d); }
        if (r.Age < RulerOldAge || Hash($"reign:{n.Salt}:{n.Id}:{w.Turn}") / 7 % (uint)Math.Max(2, 80 - r.Age) != 0) return;
        Succeed(w, n, d, killed: false);
    }

    /// <summary>The ruler is dead, of age or by someone's hand, and is followed.</summary>
    private static void Succeed(World w, Nation n, NationDef d, bool killed)
    {
        var r = n.Ruler!;
        uint hh = Hash($"heir:{n.Salt}:{n.Id}:{w.Turn}");
        bool general = n.Commander is { Skill: >= 5 } && hh / 7 % 3 == 0; var next = general ? n.Commander! : null;
        // a general who takes the chair is never mild, and is warlike as often as not
        var ruler = NewRuler(w, n, d, general ? (hh / 31 % 2 == 0 ? "warlike" : hh / 101 % 2 == 0 ? "wary" : "grasping") : null); if (next != null) { n.NamesUsed--; ruler.Name = next.Name; ruler.Age = next.Age; n.Commander = NewCommander(w, n, d); }
        n.Ruler = ruler; int shift = ruler.Nature switch { "warlike" => -10, "mild" => 10, "grasping" => 4, _ => 0 };
        if (!n.Subdued(w.Turn)) n.Disposition = Math.Clamp(n.Disposition + shift, -100, 100);
        w.WorldCounters[SuccessionsCounter] = w.Counter(SuccessionsCounter) + 1; w.WorldFlags.Add("nation_succession");
        w.Log($"{Styled(d, r, true)} of {d.Name} is dead{(killed ? "" : $" at {r.Age}")}. {(general ? $"The commander of its fleets takes the chair: {Styled(d, ruler, true)}" : $"{Styled(d, ruler, true)} follows")}, {ruler.Nature} by nature{(shift < 0 ? ", and no friend to the Reach" : shift > 0 ? ", and easier to deal with" : "")}.");
    }

    // ---- a door (docs/BEYOND.md, stage 0): the systems of the rim that face a nation ----

    /// <summary>The nation's march: the rim systems that face it, as far as they are on this map.</summary>
    public static List<string> Marches(World w, NationDef d) => w.Galaxy == null ? new List<string>() : d.Marches.Where(id => w.Galaxy.System(id) != null).ToList();
    /// <summary>Lanes from a system to the nearest of the nation's march systems; 0 with no map or no march.</summary>
    public static int HopsToMarch(World w, NationDef d, string fromSystemId)
    {
        var marches = Marches(w, d); if (marches.Count == 0 || w.Galaxy == null || fromSystemId.Length == 0) return 0;
        return marches.Select(m => w.Galaxy.Hops(fromSystemId, m)).Where(h => h >= 0).DefaultIfEmpty(0).Min();
    }
    /// <summary>Years an expedition sails before it fights: the nation's depth beyond its march, and a year for every three lanes between
    /// where the fleet lies and the march. A fleet posted at the march is nearest. With no march on the map, the old figure.</summary>
    public static int OutTurns(World w, NationDef d, string fromSystemId) => Marches(w, d).Count == 0 ? d.Distance : d.Depth + (HopsToMarch(w, d, fromSystemId) + HopsPerYear - 1) / HopsPerYear;
    /// <summary>A fleet of the Reach standing guard at the nation's march: three hulls at least, in port and not the raiders'.</summary>
    public static Fleet? Guard(World w, NationDef d)
    {
        var marches = Marches(w, d);
        return w.Fleets.Values.Where(f => !f.IsAway && !f.Moving && !World.Outsider(f.OwnerId) && f.Strength >= MarchGuard && marches.Contains(f.SystemId))
            .OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    public static Nation? AtWar(World w) => w.Nations.Where(n => n.AtWar).OrderBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
    public static bool Raided(World w, string nationId, int withinTurns) => w.Nation(nationId) is { } n && n.LastRaidTurn > 0 && w.Turn - n.LastRaidTurn <= withinTurns;
    public static IEnumerable<Fleet> Expeditions(World w, string? nationId = null) =>
        w.Fleets.Values.Where(f => f.IsAway && (nationId == null || f.Away == nationId)).OrderBy(f => f.Id, StringComparer.Ordinal);

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    private static double Roll(string key) => (Hash(key) % 10000) / 10000.0;

    /// <summary>The Senate has voted war (SenateSystem.Apply) — or a test has. One war abroad at a time.</summary>
    public static void DeclareWar(World w, NationCatalog catalog, string nationId, string proposerHouseId)
    {
        var n = w.Nation(nationId); var d = catalog.Def(nationId); if (n == null || d == null || n.AtWar || n.Fallen || n.Subdued(w.Turn)) return;
        if (AtWar(w) != null) return;
        n.AtWar = true; n.BeganIt = false; n.WarSince = w.Turn; n.ScoreReach = 0; n.ScoreNation = 0; n.LastBattleTurn = w.Turn; n.Wars += 1; n.Disposition = Math.Max(-100, n.Disposition - 20);
        w.WorldCounters[WarsCounter] = w.Counter(WarsCounter) + 1;
        w.WorldFlags.Add("foreign_war"); w.WorldFlags.Add("foreign_war_declared"); w.WorldFlags.Add("foreign_war:" + nationId);
        w.Log($"The Reach is at war with {d.Name}, on the motion of {w.House(proposerHouseId)?.Name ?? proposerHouseId}. The fleets that sail against it must beat {n.Strength} hulls; a score of {SuesAt(n, d)} subdues it.");
    }

    public void Tick(World w)
    {
        foreach (var f in TurnFlags) w.WorldFlags.Remove(f);
        ClearMarks(w);
        if (w.WorldFlags.Remove("expedition_sent")) w.WorldFlags.Add("fleet_sailed_abroad");   // the player's order last turn becomes this turn's news
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("nation_subdued:") || f.StartsWith("raided_by:")).ToList()) w.WorldFlags.Remove(f);
        if (_catalog.IsEmpty) return;
        Seed(w, _catalog);
        var mine = w.Protagonist?.HouseId;
        foreach (var n in w.Nations.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var d = _catalog.Def(n.Id); if (d == null || n.Fallen) continue;
            Reign(w, n, d, mine);
            Keep(w, n, d);
            Drift(w, n, d);
            Raid(w, n, d, mine);
            foreach (var f in Expeditions(w, n.Id).ToList()) Sail(w, n, d, f, mine);
            ResolveWar(w, n, d, mine);
            Keep(w, n, d, afterTheWar: true);   // the peace may have moved its capital and its people
            Strike(w, n, d);                       // a nation that begins wars may begin one (docs/BEYOND.md, the Ardent Concord)
            Tribute(w, n, d, mine);
        }
        w.WorldFlags.Remove("ransom_answer_take"); w.WorldFlags.Remove("ransom_answer_keep");   // the player's answer to a ransom has been read by whichever nation offered it
        // standing flags
        foreach (var f in new[] { "foreign_war", "expedition_away", "nation_tributary" }) w.WorldFlags.Remove(f);
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("foreign_war:")).ToList()) w.WorldFlags.Remove(f);
        if (AtWar(w) is { } war) { w.WorldFlags.Add("foreign_war"); w.WorldFlags.Add("foreign_war:" + war.Id); }
        if (mine != null && Expeditions(w).Any(f => f.OwnerId == mine)) w.WorldFlags.Add("expedition_away");
        if (w.Nations.Any(n => n.Subdued(w.Turn))) w.WorldFlags.Add("nation_tributary");
    }

    /// <summary>Strength regrows toward the content figure (decaying nations lose a hull every ten turns instead); regard drifts by temper, and sours at war.</summary>
    private static void Drift(World w, Nation n, NationDef d)
    {
        if (w.Turn % RegenEvery == 0)
        {
            if (d.Temper == "decaying") { if (w.Turn % (RegenEvery * 2) == 0 && n.Strength > 4 && !n.OnMap) n.Strength -= 1; }
            else if (n.Strength < d.Strength && !n.OnMap) n.Strength = Math.Min(d.Strength, n.Strength + Math.Max(1, d.Regen));
        }
        if (n.AtWar) n.Disposition = Math.Max(-80, n.Disposition - 2);
        else if (n.Subdued(w.Turn)) { }
        else if (w.Turn % 3 == 0)
        {
            if (d.Temper == "aggressive" && n.Disposition > -60) n.Disposition -= 1;
            else if (d.Temper == "mercantile" && n.Disposition < 30) n.Disposition += 1;
            else if (d.Temper != "aggressive" && n.Disposition < d.Disposition) n.Disposition += 1;
            // and the one who rules it leans on the scale: the warlike sour it, the mild sweeten it
            if (n.Ruler?.Nature == "warlike" && n.Disposition > -60) n.Disposition -= 1; else if (n.Ruler?.Nature == "mild" && n.Disposition < 30) n.Disposition += 1;
        }
    }

    /// <summary>An aggressive or decaying nation whose regard has fallen raids a rim world about one turn in six: unrest there, credits from its
    /// holder; at war the raid also scores against the Reach.</summary>
    private static void Raid(World w, Nation n, NationDef d, string? mine)
    {
        if (d.Temper is not ("aggressive" or "decaying") || n.Disposition > -20 || n.Subdued(w.Turn) || w.Galaxy == null) return;
        if (w.Turn - n.LastRaidTurn < RaidMinGap || Hash($"raid:{n.Id}:{w.Turn}:{n.Disposition}:{w.Holdings.Count}") % RaidEvery != 0) return;
        if (n.Ruler?.Nature == "wary" && n.Disposition > -40) return;   // a wary ruler raids only when the quarrel is real
        var marches = Marches(w, d); var leader = n.Commander; string led = leader != null ? $" under {Styled(d, leader, false)}" : "";
        // they come through their march, and a fleet standing there meets them before they reach a world (docs/BEYOND.md)
        if (marches.Count > 0 && Guard(w, d) is { } guard)
        {
            n.LastRaidTurn = w.Turn; w.WorldCounters[RaidsMetCounter] = w.Counter(RaidsMetCounter) + 1; if (leader != null) leader.Defeats++;
            string at = w.Galaxy.System(guard.SystemId)?.Name ?? guard.SystemId;
            w.Log($"Raiders of {d.Name}{led} come through {at} and find {guard.Name} waiting. They are turned back at the march.");
            if (guard.AdmiralId.Length > 0 && w.Char(guard.AdmiralId) is { Alive: true } warden) LifeSystem.Note(w, warden, "victory", $"Turned back raiders of {d.Name} at {at}.");
            if (guard.OwnerId == mine) w.WorldFlags.Add("raid_met_at_the_march");
            return;
        }
        var rim = marches.Count > 0
            ? w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && !World.Outsider(h.OwnerId) && marches.Select(m => w.Galaxy.Hops(m, h.SystemId)).Where(x => x >= 0).DefaultIfEmpty(99).Min() <= RaidReach)
                .OrderBy(h => marches.Select(m => w.Galaxy.Hops(m, h.SystemId)).Where(x => x >= 0).DefaultIfEmpty(99).Min()).ThenBy(h => h.Id, StringComparer.Ordinal).Take(5).ToList()
            : new List<Holding>();
        if (rim.Count == 0) rim = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && !World.Outsider(h.OwnerId) && w.Galaxy.RingOf(h.SystemId) == Galaxy.Ring.Rim).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (rim.Count == 0) rim = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId.Length > 0 && !World.Outsider(h.OwnerId)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (rim.Count == 0) return;
        var target = rim[(int)(Hash($"raidat:{n.Id}:{w.Turn}") % (uint)rim.Count)];
        if (FleetSystem.StrengthAt(w, target.OwnerId, target.SystemId) >= 3) { n.LastRaidTurn = w.Turn; w.WorldCounters[RaidsRepelledCounter] = w.Counter(RaidsRepelledCounter) + 1; w.Log($"Raiders of {d.Name}{led} come for {target.Name} and find the fleet of {w.House(target.OwnerId)?.Name} in orbit; they turn back."); return; }
        n.LastRaidTurn = w.Turn;
        target.Unrest = Math.Min(100, target.Unrest + RaidUnrest);
        int take = RaidCredits * (n.Ruler?.Nature == "grasping" ? 2 : 1);   // a grasping ruler's raiders come for the cargo
        if (w.House(target.OwnerId) is { } owner) owner.Treasury.Credits -= Math.Min(take, Math.Max(0, owner.Treasury.Credits));
        w.WorldCounters[RaidsCounter] = w.Counter(RaidsCounter) + 1;
        if (n.AtWar) n.ScoreNation += 1;
        w.WorldCounters[RaidedKey(target.OwnerId)] = w.Turn; w.WorldCounters[RaidedByKey(target.OwnerId)] = w.Nations.IndexOf(n);
        if (target.OwnerId == mine) { w.WorldFlags.Add("raided_by_nation"); w.WorldFlags.Add("raided_by:" + n.Id); }
        if (leader != null) leader.Victories++;
        w.Log($"Raiders of {d.Name} fall on {target.Name}{led}: cargo and crews taken, the world in uproar.{(n.AtWar ? " The war's tally turns a point their way." : "")}");
    }

    /// <summary>One turn of an expedition: sailing out, fighting in the nation's space, or sailing home.</summary>
    private void Sail(World w, Nation n, NationDef d, Fleet f, string? mine)
    {
        var house = w.House(f.OwnerId);
        if (house == null) { w.Fleets.Remove(f.Id); return; }
        switch (f.AwayPhase)
        {
            case "out":
                f.AwayTurns -= 1; f.Supply = Math.Max(0, f.Supply - ExpeditionSupplyDrain / 2);
                if (f.AwayTurns <= 0)
                {
                    if (!n.AtWar) { GoHome(w, f, d, "finds the war over and turns for home"); break; }
                    f.AwayPhase = "fighting"; f.AwayTurns = FightTurns; w.Log($"{f.Name} reaches the space of {d.Name}.");
                }
                break;
            case "fighting":
                if (!n.AtWar) { GoHome(w, f, d, "is recalled: the war is over"); break; }
                f.Supply = Math.Max(0, f.Supply - ExpeditionSupplyDrain);
                if (n.Strength <= 0) { n.ScoreReach += 1; w.Log($"{f.Name} finds nothing left of {d.Name}'s fleets to fight, and sweeps its lanes unopposed."); }
                else Fight(w, n, d, f, house, mine);
                if (f.AwayPhase != "fighting") break;   // broken in the fight: already turned for home with the full distance to sail
                f.AwayTurns -= 1;
                if (f.AwayTurns <= 0) GoHome(w, f, d, "has done its season and turns for home");
                break;
            case "home":
                f.AwayTurns -= 1;
                if (f.AwayTurns <= 0)
                {
                    f.SystemId = f.HomeSystemId.Length > 0 ? f.HomeSystemId : w.HoldingsOf(f.OwnerId).FirstOrDefault(h => h.IsPlanet && h.SystemId.Length > 0)?.SystemId ?? "";
                    house.Treasury.Credits += f.Plunder;
                    w.Log($"{f.Name} comes home from {d.Name}{(f.Plunder > 0 ? $" with {f.Plunder} credits of plunder" : "")}{(f.ExpeditionWins > 0 ? $" and {f.ExpeditionWins} victor{(f.ExpeditionWins == 1 ? "y" : "ies")}" : "")}.");
                    if (f.OwnerId == mine) w.WorldFlags.Add("expedition_returned");
                    f.Away = ""; f.AwayPhase = ""; f.AwayTurns = 0; f.HomeSystemId = ""; f.Plunder = 0; f.ExpeditionWins = 0;
                    if (f.SystemId.Length == 0) { w.Fleets.Remove(f.Id); w.Log($"{f.Name} has no port left to come home to; its crews scatter."); }
                }
                break;
            default:
                f.AwayPhase = "out"; f.AwayTurns = OutTurns(w, d, f.HomeSystemId); break;
        }
    }

    private static void GoHome(World w, Fleet f, NationDef d, string why)
    {
        if (f.AwayPhase == "home") return;
        int back = OutTurns(w, d, f.HomeSystemId);
        f.AwayPhase = "home"; f.AwayTurns = back; w.Log($"{f.Name} {why}: {back} year{(back == 1 ? "" : "s")} out from the Reach.");
    }

    /// <summary>The abstract battle: the fleet's own power (the real math — hulls, readiness, admiral, veterancy, stance, supply, the flag) against
    /// the strength the nation commits — it matches the force sent and a quarter more, up to all it has — a power a hull, by temper (the defensive
    /// fight hardest at home, the mercantile and the decaying softest). A hash decides it in proportion: an even fleet with a plain admiral wins a
    /// little under half its battles; a veteran fleet under a good one, more.</summary>
    private void Fight(World w, Nation n, NationDef d, Fleet f, House house, string? mine)
    {
        double pw = _fleets.PowerOf(w, f, defending: false) * (1 + _fleets.FlagFactor(w, f.OwnerId));
        int committed = Math.Min(n.Strength, Math.Max(2, (int)Math.Ceiling(f.Strength * NationReserve)));
        double temper = d.Temper switch { "defensive" => 1.25, "mercantile" => 0.8, "decaying" => 0.85, _ => 1.0 };
        var theirs = n.Commander; string under = theirs != null ? $" under {Styled(d, theirs, false)}" : "";
        double pn = committed * NationPowerPerStrength * temper * CommanderFactor(theirs);   // who leads them counts
        double pWin = pw / Math.Max(0.001, pw + pn);
        bool win = Roll($"abroad:{n.Id}:{f.Id}:{w.Turn}") < pWin;
        n.LastBattleTurn = w.Turn; w.WorldCounters[BattlesCounter] = w.Counter(BattlesCounter) + 1;
        var commander = f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId) : null; if (commander is { Alive: false }) commander = null;
        if (win)
        {
            int lost = Math.Clamp((int)Math.Floor(f.Strength * WinHullLoss * Math.Min(2.0, pn / Math.Max(0.001, pw))), 0, Math.Max(0, f.Strength - 1));
            for (int i = 0; i < lost; i++) FleetSystem.ShedHull(w, f);
            n.Strength = Math.Max(0, n.Strength - Math.Max(1, (int)Math.Ceiling(committed * WinStrengthLoss)));
            n.ScoreReach += ScorePerBattle; f.Plunder += d.Wealth; f.ExpeditionWins += 1;
            w.WorldCounters[WinsKey(house.Id, n.Id)] = w.Counter(WinsKey(house.Id, n.Id)) + 1;
            f.Readiness = Math.Max(0, f.Readiness - ReadinessWin); f.Veterancy = Math.Min(_fleets.Rules.VeterancyMax, f.Veterancy + 1); f.Loyalty = Math.Min(100, f.Loyalty + _fleets.Rules.LoyaltyWin);
            if (theirs != null)
            {
                theirs.Defeats++;
                if ((int)(Hash($"theirfall:{n.Id}:{f.Id}:{w.Turn}") / 7 % 100) < CommanderDeathPct)
                {
                    w.Log($"{Styled(d, theirs, false)} of {d.Name} is killed in the rout.{(commander != null ? $" {commander.Name} has the name of it." : "")}");
                    if (commander != null) LifeSystem.Note(w, commander, "victory", $"Beat {Styled(d, theirs, false)} of {d.Name} beyond the Reach, who died in the rout.");
                    w.WorldCounters[CommandersFallenCounter] = w.Counter(CommandersFallenCounter) + 1; if (f.OwnerId == mine) w.WorldFlags.Add("nation_commander_fallen");
                    n.Commander = NewCommander(w, n, d);
                }
            }
            w.Log($"{f.Name} beats the fleets of {d.Name}{under} in their own space: {committed} hulls met, {lost} of ours lost, {d.Wealth} credits taken. The tally stands {n.ScoreReach} to {n.ScoreNation}.");
            if (f.OwnerId == mine) w.WorldFlags.Add("expedition_victory");
            CaesarSystem.ExpeditionVictory(w, f, commander);
            if (commander != null)
            {
                string skill = commander.Skill("tactics") <= commander.Skill("gunnery") ? "tactics" : "gunnery"; if (commander.Skill(skill) < EducationSystem.Ceiling(commander, skill)) commander.Skills[skill] = commander.Skill(skill) + 1;
                if (commander.Flags.Contains(FleetSystem.FrontFlag))
                {
                    int v = w.Counter(FleetSystem.ValourKey(commander.Id)) + 1; w.WorldCounters[FleetSystem.ValourKey(commander.Id)] = v;
                    if (v >= _fleets.Rules.AchillesValour && !commander.Flags.Contains(FleetSystem.AchillesFlag))
                    {
                        commander.Flags.Add(FleetSystem.AchillesFlag); w.Log($"{commander.Name} has led from the front through {v} victories, the last beyond the Reach; the name is spoken the way Achilles' is.");
                        if (commander.Id == w.ProtagonistId) w.WorldFlags.Add("became_achilles");
                        house.Legitimacy = Math.Min(100, house.Legitimacy + 2);
                    }
                }
            }
        }
        else
        {
            int lost = Math.Max(1, (int)Math.Ceiling(f.Strength * LossHullLoss));
            for (int i = 0; i < lost && f.Strength > 1; i++) FleetSystem.ShedHull(w, f);
            n.ScoreNation += ScorePerBattle;
            f.Readiness = Math.Max(0, f.Readiness - ReadinessLoss); f.Loyalty = Math.Max(0, f.Loyalty - _fleets.Rules.LoyaltyLoss);
            if (theirs != null) { theirs.Victories++; theirs.Skill = Math.Min(9, theirs.Skill + 1); }
            w.Log($"{f.Name} is beaten in the space of {d.Name}{(theirs != null ? $" by {Styled(d, theirs, false)}" : "")}: {lost} hull{(lost == 1 ? "" : "s")} lost against {committed}. The tally stands {n.ScoreReach} to {n.ScoreNation}.");
            if (f.OwnerId == mine) w.WorldFlags.Add("expedition_defeat");
            if (commander != null)
            {
                if (commander.Flags.Contains(FleetSystem.FlagFlag)) commander.Stress = Math.Min(100, commander.Stress + 6);
                else
                {
                    double death = _fleets.Rules.AdmiralDeathChance * (commander.Flags.Contains(FleetSystem.FrontFlag) ? _fleets.Rules.FrontDeathMult : 1.0);
                    if (Roll($"fall:{n.Id}:{commander.Id}:{w.Turn}") < death) { w.Log($"{commander.Name} falls far from home, in the space of {d.Name}."); DynastySystem.Die(w, commander.Id); }
                    else { commander.Flags.Add("wounded"); commander.Stress = Math.Min(100, commander.Stress + 10); }
                }
            }
            if (f.Strength <= 1) { GoHome(w, f, d, "is broken and limps for home"); if (f.OwnerId == mine) w.WorldFlags.Add("expedition_lost"); }
        }
    }

    /// <summary>The war's end: the Reach's score past the threshold (or the nation's fleets gone) subdues it; the nation's past it, the chair buys
    /// peace at thrice the tribute; twelve turns without a battle, both sides tire.</summary>
    private static void ResolveWar(World w, Nation n, NationDef d, string? mine)
    {
        if (!n.AtWar) return;
        var chair = RegionSystem.CapitalHolder(w);
        if (n.ScoreReach - n.ScoreNation >= SuesAt(n, d) || n.Strength <= 0)
        {
            n.SubduedUntil = w.Turn + SubduedTurns; n.Disposition = Math.Min(100, n.Disposition + 10); if (!n.OnMap) n.Strength = Math.Max(2, n.Strength);
            w.WorldCounters[SubduedCounter] = w.Counter(SubduedCounter) + 1; w.WorldFlags.Add("nation_subdued"); w.WorldFlags.Add("nation_subdued:" + n.Id);
            var top = w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderByDescending(h => w.Counter(WinsKey(h.Id, n.Id))).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault(h => w.Counter(WinsKey(h.Id, n.Id)) > 0);
            if (top != null)
            {
top.Glory = Math.Min(12, top.Glory + 4); top.Legitimacy = Math.Min(100, top.Legitimacy + LegitimacyTriumph); top.SphereInfluence["senate"] = top.SphereInfluence.GetValueOrDefault("senate") + InfluenceTriumph;
                if (top.Id == mine) w.WorldFlags.Add("triumph");
                CaesarSystem.Subdued(w, top);
                w.Log($"{d.Name} sues for peace and will pay the Reach {d.Tribute} credits a year for {SubduedTurns} years. The triumph is {top.Name}'s, whose fleets won it.");
            }
            else w.Log($"{d.Name} sues for peace and will pay the Reach {d.Tribute} credits a year for {SubduedTurns} years.");
            if (n.OnMap) Bump(w, WonCounter, n);
            PeaceOnMap(w, n, d, reachWon: true);
            EndWar(w, n, d);
        }
        else if (n.ScoreNation - n.ScoreReach >= d.Threshold)
        {
            n.Disposition = Math.Max(-100, n.Disposition - 10);
            w.WorldCounters[LostCounter] = w.Counter(LostCounter) + 1; w.WorldFlags.Add("war_lost_abroad");
            if (chair != null) { chair.Treasury.Credits -= d.Tribute * 3; chair.Legitimacy = Math.Max(0, chair.Legitimacy - LegitimacyDefeat); }
            w.Log($"The war with {d.Name} is lost: the chair{(chair != null ? $" of {chair.Name}" : "")} buys peace for {d.Tribute * 3} credits, and the Reach is shamed.");
            if (n.OnMap) Bump(w, LostOnMapCounter, n);
            PeaceOnMap(w, n, d, reachWon: false);
            EndWar(w, n, d);
        }
        else if (n.OnMap && Buys(d) && BuysOff(w, n, d))
        {
            // a nation that would rather buy than fight does not let a war lie: four years with nobody pressing it, or twelve with no
            // battle, and it buys its peace. The house that holds the capital is paid; what each side took goes home (their cities for a
            // ransom); no tribute follows
            int sum = d.Tribute * BoughtPeaceTributes; if (chair != null) chair.Treasury.Credits += sum;
            n.Disposition = Math.Min(100, n.Disposition + 5); Bump(w, BoughtPeaceCounter, n); Mark(w, "nation_bought_peace", n);
            w.Log($"{d.Name} buys its peace: {sum} credits to {(chair != null ? chair.Name : "the Reach")}, and a war that nobody was fighting is over.");
            PeaceOnMap(w, n, d, reachWon: true);
            EndWar(w, n, d);
        }
        else if (w.Turn - Math.Max(n.WarSince, n.LastBattleTurn) >= ExhaustionTurns)
        {
            w.WorldFlags.Add("foreign_peace");
            w.Log($"The war with {d.Name} gutters out: no fleet has met theirs in {ExhaustionTurns} years, and both sides let it lie.");
            if (n.OnMap) Bump(w, GutteredCounter, n);
            PeaceOnMap(w, n, d, reachWon: false);   // each keeps what it holds
            EndWar(w, n, d);
        }
    }

    /// <summary>Whether a nation that buys ends this war for money this year. It does when no battle has been fought in twelve years; or
    /// when, for four years running, there has been no battle and nobody has pressed it: no fleet of the Reach of three hulls or more at
    /// its march, over its worlds, or on its way to either. A fleet standing at the march keeps the war alive.</summary>
    private static bool BuysOff(World w, Nation n, NationDef d)
    {
        int quiet = w.Turn - Math.Max(n.WarSince, n.LastBattleTurn); string key = UnpressedKey(n);
        if (quiet >= ExhaustionTurns) return true;
        var own = d.Worlds.Select(x => x.System).ToHashSet(); var marches = Marches(w, d);
        bool pressed = Watched(w, d) || w.Fleets.Values.Any(f => f.Moving && !f.IsAway && w.OfTheReach(f.OwnerId) && f.Strength >= MarchGuard && (own.Contains(f.DestinationId) || marches.Contains(f.DestinationId)));
        if (pressed || quiet == 0) { w.WorldCounters.Remove(key); return false; }
        int years = w.Counter(key) + 1; w.WorldCounters[key] = years;
        return years >= BuysPeaceAfter;
    }
    private static string UnpressedKey(Nation n) => "unpressed_years:" + n.Id;

    /// <summary>The war is over, however it ended: the scores are struck and every expedition against the nation turns for home.</summary>
    private static void EndWar(World w, Nation n, NationDef d)
    {
        n.AtWar = false; n.BeganIt = false; n.LastWarEnd = w.Turn; w.WorldCounters.Remove(UnpressedKey(n));
        foreach (var h in w.Houses.Keys.ToList()) w.WorldCounters.Remove(WinsKey(h, n.Id));
        foreach (var f in Expeditions(w, n.Id).ToList()) GoHome(w, f, d, "is recalled: the war is over");
    }

    private static void Tribute(World w, Nation n, NationDef d, string? mine)
    {
        if (n.SubduedUntil == w.Turn) { n.Disposition = d.Disposition; w.WorldFlags.Add("nation_freed"); w.Log($"{d.Name} pays its last tribute; the treaty is done and its envoys go home."); return; }
        if (!n.Subdued(w.Turn)) return;
        var chair = RegionSystem.CapitalHolder(w); if (chair == null) return;
        chair.Treasury.Credits += d.Tribute; w.WorldCounters[TributeCounter] = w.Counter(TributeCounter) + d.Tribute;
    }
}
