using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Caesar road (docs/CAESAR.md; docs/REPUBLIC.md stages 4 and 5). Crews come to follow the commander who wins with them
/// (<see cref="Fleet.Devotion"/>), and a commander's victories are counted toward a <b>triumph</b> the Senate may vote.
/// A general with enough sway may be <b>recalled</b>. The head of his house answers: he comes home, or the house
/// <b>crosses the line</b> and marches on the capital. That is a civil war in which the other houses declare for him, for
/// the Senate, or stand aside; a few of them fight. If he holds the capital's orbit or breaks the Senate's fleets the
/// Republic ends with his house on the throne; if he is broken he dies and his house pays for it.
/// </summary>
public sealed class CaesarSystem
{
    public const int DevotedAt = 50, TriumphLaurels = 3, RecallSway = 40, FearedSway = 45, AlarmSway = 55, MarchSway = 40, MarchHulls = 4, RecallYears = 2, HoldYears = 2, WarYears = 10,
        DecisiveLead = 8, GrievanceYears = 5, BarredYears = 5, DonativePerHull = 2, DonativeDevotion = 8;
    // true each year while it holds
    public const string RecalledFlag = "recalled", KinRecalledFlag = "kin_recalled", CivilWarFlag = "civil_war", MyCivilWarFlag = "civil_war_mine",
        SwordFlag = "sword_of_the_senate", SwayFlag = "general_has_sway", LaurelsFlag = "laurels_earned", KinLovedFlag = "kin_general_loved",
        TroubledFlag = "republic_troubled", GreatNameFlag = "great_name_rises", HourFlag = "the_hour_is_mine";
    public const string GreatNameChar = "great_name";
    // true for a year after the deed
    public const string TriumphFlag = "triumph_voted", KinTriumphFlag = "kin_triumph", RefusedFlag = "triumph_refused", ObeyedFlag = "recall_obeyed",
        CrossedFlag = "crossed_the_line", MarchesFlag = "caesar_marches", CrownedFlag = "caesar_crowned", FallenFlag = "republic_fell",
        SavedFlag = "republic_saved", BrokenFlag = "caesar_broken", ServedFlag = "served_caesar", OpposedFlag = "opposed_caesar";
    /// <summary>For good, once a general has taken the throne by this road.</summary>
    public const string RepublicFallenFlag = "republic_fallen";
    public const string TriumphatorChar = "triumphator", NewTriumphatorChar = "new_triumphator", CaesarChar = "caesar", RecalledChar = "recalled_general", LovedChar = "loved_by_the_fleet";
    public const string General = "general", Senate = "senate";
    private static readonly string[] YearFlags = { RecalledFlag, KinRecalledFlag, CivilWarFlag, MyCivilWarFlag, SwordFlag, SwayFlag, LaurelsFlag, KinLovedFlag, TroubledFlag, GreatNameFlag, HourFlag };
    private static readonly string[] DeedFlags = { TriumphFlag, KinTriumphFlag, RefusedFlag, ObeyedFlag, CrossedFlag, MarchesFlag, CrownedFlag, FallenFlag, SavedFlag, BrokenFlag, ServedFlag, OpposedFlag };

    private readonly FleetSystem _fleets; private readonly OfficeCatalog _offices;
    public CaesarSystem(FleetSystem fleets, OfficeCatalog offices) { _fleets = fleets; _offices = offices; }

    public static string LaurelKey(string characterId) => "laurels:" + characterId;
    public static string TriumphKey(string characterId) => "triumphs:" + characterId;
    public static string VictoryKey(string characterId) => "victories:" + characterId;
    public static string RefusedKey(string houseId) => "triumph_refused:" + houseId;
    public static string CensuredKey(string houseId) => "censured_at:" + houseId;
    public static string ReachWonKey(string houseId) => "reach_won:" + houseId;
    public static string BarredKey(string characterId) => "no_command:" + characterId;
    public static string EnemyKey(string houseId) => "enemy_of_the_reach:" + houseId;
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static bool Within(World w, string key, int years) => w.WorldCounters.TryGetValue(key, out int t) && w.Turn - t <= years;
    private static bool Real(House h) => !World.Outsider(h.Id);

    /// <summary>One the Senate has had lay down a command may not take another for some years.</summary>
    public static bool Barred(World w, Character c) => w.Counter(BarredKey(c.Id)) > w.Turn || ChargeSystem.Abroad(w, c);   // nor may one in exile (docs/LADDER.md)
    /// <summary>A house whose general marched and lost may not stand for consul for ten years.</summary>
    public static bool Enemy(World w, string houseId) => w.Counter(EnemyKey(houseId)) > w.Turn;

    /// <summary>Is the Reach in its great war (docs: the War of the Reach)? Nobody marches on a capital that every house is already fighting over.</summary>
    public static bool GreatWar(World w) => GreatCrisisSystem.AtWar(w);

    // ---------------- the crews and their commander ----------------

    /// <summary>Hulls whose crews are this commander's before they are the house's.</summary>
    public static int DevotedHulls(World w, Character c) => w.Fleets.Values.Where(f => f.AdmiralId == c.Id && f.DevotedTo == c.Id && f.Devotion >= DevotedAt).Sum(f => f.Strength);
    public static int Laurels(World w, Character c) => w.Counter(LaurelKey(c.Id));
    public static int Triumphs(World w, Character c) => w.Counter(TriumphKey(c.Id));
    /// <summary>Battles won in command, in a whole life.</summary>
    public static int Victories(World w, Character c) => w.Counter(VictoryKey(c.Id));
    private static void Won(World w, string id, int n = 1) { w.WorldCounters[LaurelKey(id)] = w.Counter(LaurelKey(id)) + n; w.WorldCounters[VictoryKey(id)] = w.Counter(VictoryKey(id)) + n; }

    /// <summary>What a commander could move the Reach with, 0 to 100: the battles won, those won from the front, the name that comes of
    /// them, the hulls that are his, the triumphs voted him and his rank in the service.</summary>
    public static int Sway(World w, Character c) => Math.Clamp(SwayParts(w, c).Sum(x => x.points), 0, 100);
    public static IEnumerable<(string what, int points)> SwayParts(World w, Character c)
    {
        int won = Math.Min(30, Victories(w, c) * 6); if (won > 0) yield return ("battles won in command", won);
        int v = Math.Min(15, w.Counter(FleetSystem.ValourKey(c.Id)) * 3); if (v > 0) yield return ("of them, won from the front", v);
        if (c.Flags.Contains(FleetSystem.AchillesFlag)) yield return ("the name the Reach gives", 10);
        int d = Math.Min(30, DevotedHulls(w, c) * 2); if (d > 0) yield return ("hulls whose crews follow", d);
        int t = Math.Min(30, Triumphs(w, c) * 12); if (t > 0) yield return ("triumphs", t);
        if (c.CareerTrack == "military" && c.CareerRank > 0) yield return ("rank in the service", c.CareerRank * 2);
    }

    /// <summary>The house's foremost commander: one of its own who commands a fleet, of its own or another's by service (docs/MILITARY.md,
    /// "A command by service"), by sway.</summary>
    public static Character? GeneralOf(World w, House? house)
    {
        if (house == null) return null;
        return w.Fleets.Values.Where(f => f.AdmiralId.Length > 0 && (f.OwnerId == house.Id || ServiceSystem.CommandedBy(w, f, house.Id))).Select(f => w.Char(f.AdmiralId))
            .Where(c => c is { Alive: true } && c.HouseId == house.Id).Select(c => c!).Distinct()
            .OrderByDescending(c => Sway(w, c)).ThenByDescending(c => Laurels(w, c)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>A battle won at home: the victor's crews take to the one who led them, and each commander has a victory to his name.</summary>
    public static void Victory(World w, string winner, string loser, string systemId)
    {
        var led = new HashSet<string>();
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == winner && !f.Moving && f.SystemId == systemId && f.AdmiralId.Length > 0))
        {
            var c = w.Char(f.AdmiralId); if (c is not { Alive: true }) continue;
            Attach(w, f, c); f.Devotion = Math.Min(100, f.Devotion + (c.Flags.Contains(FleetSystem.FrontFlag) ? 12 : 6)); led.Add(c.Id);
        }
        foreach (var id in led) Won(w, id);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == loser && !f.Moving && f.SystemId == systemId)) f.Devotion = Math.Max(0, f.Devotion - 6);
    }
    /// <summary>A battle won beyond the Reach counts the same.</summary>
    public static void ExpeditionVictory(World w, Fleet f, Character? commander)
    {
        if (commander is not { Alive: true }) return;
        Attach(w, f, commander); f.Devotion = Math.Min(100, f.Devotion + (commander.Flags.Contains(FleetSystem.FrontFlag) ? 12 : 6));
        Won(w, commander.Id);
    }
    /// <summary>A nation brought to terms: two more to the name of the general of the house whose fleets won it.</summary>
    public static void Subdued(World w, House house)
    {
        if (GeneralOf(w, house) is { } g) Won(w, g.Id, 2);
    }
    /// <summary>How long after a saga opens someone taking a command is credited with the service they had before it.</summary>
    public const int VeteranYears = 3;
    private static void Attach(World w, Fleet f, Character c)
    {
        if (f.DevotedTo == c.Id) return;
        f.DevotedTo = c.Id; f.Devotion = Math.Min(55, 20 + 5 * f.Veterancy);
        // a saga does not open on a Reach without generals: one who has risen in the service and holds a command in its first years
        // already has battles to their name and crews who know them
        if (w.Turn <= VeteranYears && c.CareerTrack == "military" && c.CareerRank >= 3 && w.Counter("veteran:" + c.Id) == 0)
        {
            w.WorldCounters["veteran:" + c.Id] = 1; w.WorldCounters[VictoryKey(c.Id)] = Math.Max(Victories(w, c), (c.CareerRank - 2) * 2);
            f.Devotion = Math.Min(48, 36 + 4 * (c.CareerRank - 2));   // known to the crews, not yet followed before the house: that takes a victory or some years
        }
    }

    /// <summary>When a world is first opened, the crews already know the commanders they have: nothing is gained, it is only written down.</summary>
    public static void Open(World w)
    {
        // once per world: a saved game that is opened again must not write them down a second time, or a commander appointed in
        // the year of the save is a point ahead of where an unbroken run would have him
        if (w.Counter("crews_opened") != 0) return; w.WorldCounters["crews_opened"] = 1;
        foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId.Length > 0 && f.DevotedTo.Length == 0).OrderBy(f => f.Id, StringComparer.Ordinal))
            if (w.Char(f.AdmiralId) is { Alive: true } c) Attach(w, f, c);
    }

    private void Crews(World w, string? mine)
    {
        foreach (var f in w.Fleets.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var adm = f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId) : null; if (adm is not { Alive: true }) adm = null;
            bool hadOne = f.DevotedTo.Length > 0 && f.Devotion >= DevotedAt;
            if (adm == null)
            {
                if (hadOne) { f.Loyalty = Math.Max(0, f.Loyalty - 8); if (f.OwnerId == mine) w.Log($"The crews of {f.Name} followed {w.Char(f.DevotedTo)?.Name ?? "their commander"}, and are sullen without one."); }
                f.DevotedTo = ""; f.Devotion = 0; continue;
            }
            if (f.DevotedTo != adm.Id)
            {
                if (hadOne && w.Char(f.DevotedTo) is { Alive: true } old)
                {
                    f.Loyalty = Math.Max(0, f.Loyalty - 10);
                    if (f.OwnerId == mine) w.Log($"The crews of {f.Name} were {old.Name}'s. They do not take to {adm.Name}.");
                    f.DevotedTo = adm.Id; f.Devotion = 10;
                }
                else Attach(w, f, adm);
            }
            else f.Devotion = Math.Min(100, f.Devotion + 1 + (f.Loyalty >= 80 ? 1 : 0));
        }
    }

    // ---------------- the triumph ----------------

    public string? TriumphRefusal(World w, House? target)
    {
        if (target == null || !Real(target)) return "no such house";
        if (!PolitySystem.InReach(w, target.Id)) return $"{target.Name} is not of the Reach";
        if (w.IsImperium) return "under a throne honours are the throne's to give";
        if (w.CivilWar != null || GreatWar(w)) return "not while the Reach is at war with itself";
        var g = GeneralOf(w, target); if (g == null) return $"nobody of {target.Name} commands a fleet";
        if (w.Recalls.Any(r => r.HouseId == target.Id)) return $"{g.Name} is under recall";
        int n = Laurels(w, g);
        return n >= TriumphLaurels ? null : $"{g.Name} has {n} of the {TriumphLaurels} victories a triumph asks";
    }

    /// <summary>How a house leans on a triumph for the general of <paramref name="target"/>.</summary>
    public int TriumphLean(World w, House h, House target, int toTarget)
    {
        if (h.Id == target.Id) return 4;
        var g = GeneralOf(w, target); int lean = toTarget >= 20 ? 2 : toTarget <= -20 ? -2 : 0;
        var mine = FactionSystem.Of(w, h.Id); var theirs = FactionSystem.Of(w, target.Id);
        if (mine != null && theirs != null && mine.Id == theirs.Id) lean += 1;
        lean += mine?.PartyId switch { "sword" => 1, "optimates" when g != null && Sway(w, g) >= 60 => -1, _ => 0 };
        if (OrderSystem.Tier(h, OrderSystem.Arms) >= 2) lean -= 1;                       // the other great names in arms would rather it were theirs
        if (g != null && g.Flags.Contains(FleetSystem.AchillesFlag)) lean += 1;          // the squares want to see him
        return lean;
    }

    /// <summary>The Senate has voted on a triumph.</summary>
    public void TriumphVoted(World w, House target, House? proposer, bool passed)
    {
        var mine = w.Protagonist?.HouseId; var g = GeneralOf(w, target); if (g == null) return;
        if (!passed)
        {
            w.WorldCounters[RefusedKey(target.Id)] = w.Turn; g.Stress = Math.Min(100, g.Stress + 5);
            if (target.Id == mine) Mark(w, RefusedFlag);
            w.Log($"The Senate will not vote {g.Name} a triumph. {g.Name}'s crews hear of it before {g.Name} does.");
            foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId == g.Id)) f.Devotion = Math.Min(100, f.Devotion + 4);
            return;
        }
        w.WorldCounters[TriumphKey(g.Id)] = Triumphs(w, g) + 1; w.WorldCounters[LaurelKey(g.Id)] = 0; w.WorldCounters["triumphs_voted"] = w.Counter("triumphs_voted") + 1;
        g.Flags.Add(TriumphatorChar); g.Flags.Add(NewTriumphatorChar);
        LegitimacyBook.Move(w, target, 6, LegitimacyBook.War, "A triumph voted to the house's general"); target.Glory = Math.Min(12, target.Glory + 2);
        target.SphereInfluence["senate"] = target.SphereInfluence.GetValueOrDefault("senate") + 1;
        foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId == g.Id)) { Attach(w, f, g); f.Devotion = Math.Min(100, f.Devotion + 10); f.Loyalty = Math.Min(100, f.Loyalty + 5); }
        LifeSystem.Note(w, g, "triumph", "Was voted a triumph by the Senate.");
        if (proposer != null && proposer.Id != target.Id && Economy.HeadOf(w, target) is { } th && Economy.HeadOf(w, proposer) is { } ph)
        { var r = w.Relationship(th.Id, ph.Id); r.Disposition = Math.Clamp(r.Disposition + 10, -100, 100); }
        if (target.Id == mine) Mark(w, g.Id == w.ProtagonistId ? TriumphFlag : KinTriumphFlag);
        w.Log($"{g.Name} of {target.Name} rides in triumph through the capital. The crews march behind, and the crowd learns their commander's name.");
    }

    // ---------------- the recall ----------------

    public string? RecallRefusal(World w, House? target)
    {
        if (target == null || !Real(target)) return "no such house";
        if (!PolitySystem.InReach(w, target.Id)) return $"{target.Name} is not of the Reach";
        if (w.IsImperium) return "under a throne a general is dismissed, not recalled";
        if (w.CivilWar != null || GreatWar(w)) return "not while the Reach is at war with itself";
        var g = GeneralOf(w, target); if (g == null) return $"nobody of {target.Name} commands a fleet";
        if (w.Recalls.Any(r => r.HouseId == target.Id)) return $"{g.Name} is under recall already";
        int s = Sway(w, g);
        if (NationSystem.DefiedBy(w, target.Id) != null) return null;   // fighting on against a peace the Senate voted is cause enough, whatever the name
        return s >= RecallSway ? null : $"{g.Name} has not the sway to trouble the Senate ({s} of {RecallSway})";
    }

    public int RecallLean(World w, House h, House target, int toTarget)
    {
        if (h.Id == target.Id) return -4;
        var g = GeneralOf(w, target); int sway = g != null ? Sway(w, g) : 0; int lean = toTarget >= 20 ? -2 : toTarget <= -20 ? 2 : 0;
        var mine = FactionSystem.Of(w, h.Id); var theirs = FactionSystem.Of(w, target.Id);
        if (mine != null && theirs != null && mine.Id == theirs.Id) lean -= 2;
        lean += mine?.PartyId switch { "optimates" => 1, "sword" => -1, _ => 0 };
        if (sway >= FearedSway && (RegionSystem.CapitalHolder(w)?.Id == h.Id || ElectionSystem.IsConsul(w, _offices, h.Id))) lean += 2;   // the Senate guards itself
        if (sway >= FearedSway) lean += 1;                                                                                        // and a name that large frightens the benches
        if (sway >= 70) lean += 1;
        if (FleetSystem.StrengthOf(w, target.Id) > 2 * Math.Max(1, FleetSystem.StrengthOf(w, h.Id))) lean -= 1;                // and some do not dare
        if (h.PatronId == target.Id) lean -= 2;
        if (NationSystem.DefiedBy(w, target.Id) != null) lean += NationSystem.RecallLean(w, target);   // the Senate voted peace and was not listened to: how hard it pushes depends on whose fleet it is
        return lean;
    }

    public void RecallVoted(World w, House target, House? proposer, bool passed)
    {
        var g = GeneralOf(w, target); if (g == null) return;
        if (proposer != null && Economy.HeadOf(w, target) is { } th && Economy.HeadOf(w, proposer) is { } ph && th.Id != ph.Id)
        { var r = w.Relationship(th.Id, ph.Id); r.Disposition = Math.Clamp(r.Disposition - (passed ? 20 : 10), -100, 100); }
        if (!passed) return;
        w.Recalls.Add(new Recall { CharacterId = g.Id, HouseId = target.Id, Since = w.Turn, Deadline = w.Turn + RecallYears });
        g.Flags.Add(RecalledChar); w.WorldCounters["recalls_voted"] = w.Counter("recalls_voted") + 1;
        w.Log($"The Senate orders {g.Name} of {target.Name} to lay down the command and come home, within {RecallYears} years.");
    }

    public string? ObeyRefusal(World w, House? house) => house != null && w.Recalls.Any(r => r.HouseId == house.Id) ? null : "nobody of the house is under recall";

    /// <summary>The general comes home. <paramref name="ordered"/>: the head of the house told him to, against what he would have done.</summary>
    public void Obey(World w, House house, bool ordered, bool late = false)
    {
        var recall = w.Recalls.First(r => r.HouseId == house.Id); w.Recalls.Remove(recall);
        var g = w.Char(recall.CharacterId); var mine = w.Protagonist?.HouseId; if (g == null) return;
        g.Flags.Remove(RecalledChar);
        foreach (var s in w.Service.Where(s => s.CommanderId == g.Id).ToList()) ServiceSystem.End(w, s, "recalled by the Senate");   // the Senate relieves a commander by service too
        foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId == g.Id)) { if (f.Devotion >= DevotedAt) f.Loyalty = Math.Max(0, f.Loyalty - 5); f.AdmiralId = ""; f.DevotedTo = ""; f.Devotion = 0; }
        FleetSystem.RefreshAdmiralFlags(w);
        w.WorldCounters[BarredKey(g.Id)] = w.Turn + BarredYears; g.Stress = Math.Min(100, g.Stress + 10);
        LegitimacyBook.Move(w, house, (late ? -6 : 4), LegitimacyBook.War, late ? "The general came home late to the Senate's recall" : "The general came home at the Senate's recall");
        LifeSystem.Note(w, g, "recall", "Was recalled by the Senate, and came home.");
        NationSystem.CameHome(w, house.Id);   // and if the house was fighting on against a peace the Senate voted, that war is over
        if (ordered && g.Id != w.ProtagonistId && w.Protagonist is { } head && CourtshipSystem.Wilful(g)) { var r = w.Relationship(g.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition - 15, -100, 100); }
        if (house.Id == mine) Mark(w, ObeyedFlag);
        w.Log(late ? $"The Senate's officers take the command from {g.Name} of {house.Name}, who had not answered the recall. It is not forgotten."
                   : $"{g.Name} of {house.Name} lays down the command and comes home, as the Senate ordered. The crews watch the shuttle go.");
    }

    // ---------------- the line ----------------

    /// <summary>Has the Senate given this house a cause: a recall, a triumph refused, or a censure, in the last five years? Or a charge
    /// against one of its powerful, or the threat of one (docs/LADDER.md, "A charge against the powerful")?</summary>
    public static string? Grievance(World w, House house) =>
        w.Recalls.Any(r => r.HouseId == house.Id) ? "the Senate has recalled its general"
        : Within(w, RefusedKey(house.Id), GrievanceYears) ? "the Senate refused its general a triumph"
        : Within(w, CensuredKey(house.Id), GrievanceYears) ? "the Senate censured the house"
        : Within(w, ReachWonKey(house.Id), GrievanceYears) ? "the War of the Reach left the Reach in the house's hands" : ChargeSystem.Cause(w, house);

    public string? CrossRefusal(World w, House? house)
    {
        if (house == null || !Real(house)) return "no such house";
        if (!PolitySystem.InReach(w, house.Id)) return "the house has left the Reach, and its capital is no longer the house's to march on";
        if (w.IsImperium) return "there is a throne already, and a throne is seized, not marched on";
        if (w.CivilWar != null || GreatWar(w)) return "the Reach is already at war with itself";
        if (w.Galaxy is not { Capital.Length: > 0 }) return "there is no capital to march on";
        if (RegionSystem.CapitalHolder(w)?.Id == house.Id) return "the house sits in the capital already: there is nowhere to march";
        var g = GeneralOf(w, house); if (g == null) return "nobody of the house commands a fleet";
        int hulls = DevotedHulls(w, g);
        if (hulls < MarchHulls) return $"{hulls} hulls would follow {g.Name} against the Senate, and it takes {MarchHulls}";
        int s = Sway(w, g);
        if (s < MarchSway) return $"{g.Name} has not the name for it (sway {s} of {MarchSway})";
        return Grievance(w, house) == null ? "the Senate has given no cause: no recall, no triumph refused, no censure, no charge against the house's great" : null;
    }

    /// <summary>How a house would declare: 2 or more for the general, −1 or less for the Senate, between them aside.</summary>
    public int SideScore(World w, House h, House general, Character g)
    {
        var head = Economy.HeadOf(w, h); var ghead = Economy.HeadOf(w, general);
        int disp = head != null && ghead != null ? w.GetRelationship(head.Id, ghead.Id)?.Disposition ?? 0 : 0;
        int s = Math.Clamp(disp / 20, -3, 3);
        var fh = FactionSystem.Of(w, h.Id); var fg = FactionSystem.Of(w, general.Id);
        if (fh != null && fg != null && fh.Id == fg.Id) s += 2;
        s += fh?.PartyId switch { "optimates" => -1, "sword" => 1, _ => 0 };
        s += Math.Min(2, OrderSystem.Sway(w, h, general).lean);
        if (h.PatronId == general.Id) s += 3; if (general.PatronId == h.Id) s += 1;
        if (RegionSystem.CapitalHolder(w)?.Id == h.Id) s -= 4;
        if (ElectionSystem.IsConsul(w, _offices, h.Id)) s -= 2;
        if (w.AtWar(h.Id, general.Id)) s -= 3;
        if (FleetSystem.StrengthOf(w, general.Id) >= 2 * Math.Max(1, FleetSystem.StrengthOf(w, h.Id))) s += 1;
        if (g.Flags.Contains(FleetSystem.AchillesFlag)) s += 1;
        // the three gates, as the other houses weigh them: the times, the man, and whether he is loved
        int troubles = Troubles(w).Count; s += troubles >= 3 ? 1 : troubles == 2 ? 0 : troubles == 1 ? -1 : -2;   // a sound Republic is defended
        if (FirstRank(w, g)) s += 1;
        if (Loved(w, g, general)) s += 1; else if (g.Skill("charisma") <= 2 && !g.Flags.Contains(FleetSystem.AchillesFlag)) s -= 1;
        // old ties (docs/LIFE.md): one who has been the player's friend for years stands with the player's house, whichever side that is
        if (head != null && w.Protagonist?.HouseId is { } mine && mine != h.Id)
        {
            int tie = head.Flags.Contains(LifeSystem.OldFriendChar) ? 2 : head.Flags.Contains(LifeSystem.OldEnemyChar) ? -2 : 0;
            if (general.Id == mine) s += tie; else if (w.CivilWar is { } now && now.HouseId == general.Id && now.Declared.TryGetValue(mine, out var side)) s += side == General ? tie : -tie;
        }
        return s + (int)(Hash(h.Id + ":side:" + general.Id) % 3) - 1;
    }

    public const string SwordHeldKey = "sword_holds_state_fleets";
    /// <summary>A house breaks with its bench only for a strong reason of its own.</summary>
    public const int BreaksForGeneral = 4, BreaksForSenate = -3;

    /// <summary>Which side each house of the Reach takes when this house's general marches. Each weighs it for itself first; then the
    /// parties go to war as parties: a bench whose leader has declared brings its members with it, all but one with a strong reason
    /// of its own. The general's own bench is his. The player says for themselves (unless the capital is theirs), except in a forecast.</summary>
    public Dictionary<string, string> Sides(World w, House house, Character g, bool forecast = false)
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); var own = new Dictionary<string, int>(); var sides = new Dictionary<string, string>();
        foreach (var h in w.Houses.Values.Where(h => Real(h) && h.Id != house.Id && PolitySystem.InReach(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (h.Id == mine && !forecast) { if (chair?.Id == mine) sides[h.Id] = Senate; continue; }   // the player says for themselves, unless the throne stands on their world
            int s = SideScore(w, h, house, g); own[h.Id] = s;
            if (chair?.Id == h.Id || s <= -1) sides[h.Id] = Senate; else if (s >= 2) sides[h.Id] = General;   // the house the capital belongs to is never for the one marching on it
        }
        foreach (var f in w.Factions.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            string lead = f.LeaderHouseId == house.Id ? General : sides.GetValueOrDefault(f.LeaderHouseId, ""); if (lead.Length == 0) continue;
            foreach (var id in f.Members.Where(id => id != f.LeaderHouseId && id != chair?.Id && own.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal))
                if (lead == General ? own[id] > BreaksForSenate : own[id] < BreaksForGeneral) sides[id] = lead;
        }
        return sides;
    }

    /// <summary>The Senate's sword commands the Reach's own fleets for as long as the war lasts (docs/CAESAR.md): every hull of the
    /// state that is not the marching general's own to keep goes under the sword's colours.</summary>
    public static int ArmTheSword(World w, CivilWar cw)
    {
        if (cw.SwordHouseId.Length == 0) return 0; int hulls = 0; w.WorldCounters[SwordHeldKey] = 1;
        foreach (var f in w.Fleets.Values.Where(f => f.Office && f.OwnerId != cw.SwordHouseId && !(f.OwnerId == cw.HouseId && f.AdmiralId == cw.GeneralId && f.Devotion >= DevotedAt)).OrderBy(f => f.Id, StringComparer.Ordinal))
        { f.OwnerId = cw.SwordHouseId; hulls += f.Strength; }
        return hulls;
    }

    /// <summary>The house crosses the line: its general marches on the capital.</summary>
    public void Cross(World w, House house)
    {
        var g = GeneralOf(w, house)!; var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); string cause = Grievance0(w, house);
        ServiceSystem.GoOver(w, g, house);   // crews that are a legate's before they are the owner's go with them (docs/MILITARY.md, "A command by service")
        var cw = new CivilWar { GeneralId = g.Id, HouseId = house.Id, Since = w.Turn, StartHulls = FleetSystem.StrengthOf(w, house.Id) };
        w.CivilWar = cw; w.Recalls.RemoveAll(r => r.HouseId == house.Id); g.Flags.Remove(RecalledChar); g.Flags.Add(CaesarChar);
        w.WorldCounters["civil_wars"] = w.Counter("civil_wars") + 1;
        // the crews that are his follow him; some of the rest will not fire on the Republic
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id))
        {
            if (f.AdmiralId == g.Id && f.Devotion >= DevotedAt) f.Loyalty = Math.Max(f.Loyalty, 85);
            else f.Loyalty = Math.Max(0, f.Loyalty - 15);
        }
        w.Log($"{g.Name} of {house.Name} CROSSES THE LINE. {cause} The fleets that are {g.Name}'s turn toward {w.Galaxy?.System(w.Galaxy.Capital)?.Name ?? "the capital"}.");
        foreach (var (id, side) in Sides(w, house, g)) cw.Declared[id] = side;
        List<string> Strongest(string side, int take) => cw.Declared.Where(kv => kv.Value == side && FleetSystem.StrengthOf(w, kv.Key) > 0)
            .OrderByDescending(kv => FleetSystem.StrengthOf(w, kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(take).Select(kv => kv.Key).ToList();
        cw.With = Strongest(General, 3); cw.Against = Strongest(Senate, 4);
        if (chair != null && chair.Id != house.Id && !cw.Against.Contains(chair.Id)) { cw.Declared[chair.Id] = Senate; cw.Against.Add(chair.Id); }
        var consul = ElectionSystem.ConsulOffices(_offices).Select(d => OfficeSystem.HolderHouse(w, d.Id)).FirstOrDefault(id => id != null && cw.Against.Contains(id));
        cw.SwordHouseId = consul ?? cw.Against.OrderByDescending(id => FleetSystem.StrengthOf(w, id)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault() ?? "";
        int stateHulls = ArmTheSword(w, cw);
        foreach (var a in cw.Against) _fleets.DeclareWar(w, house.Id, a);
        for (int i = 0; i < cw.With.Count && cw.Against.Count > 0; i++) _fleets.DeclareWar(w, cw.With[i], cw.Against[i % cw.Against.Count]);
        string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => w.House(id)?.Name ?? id).DefaultIfEmpty("nobody"));
        w.Log($"The houses declare. With {g.Name}: {Names(cw.Declared.Where(kv => kv.Value == General).Select(kv => kv.Key))}. For the Senate: {Names(cw.Declared.Where(kv => kv.Value == Senate).Select(kv => kv.Key))}."
            + (cw.SwordHouseId.Length > 0 ? $" The Senate gives its war to {w.House(cw.SwordHouseId)?.Name}{(stateHulls > 0 ? $", and with it the Reach's own {stateHulls} hulls" : "")}." : " Nobody stands in the gate."));
        if (house.Id == mine) Mark(w, CrossedFlag); else Mark(w, MarchesFlag);
        if (cw.Against.Count == 0) Win(w, cw);
    }
    private static string Grievance0(World w, House house) { var why = Grievance(w, house); return why == null ? "" : char.ToUpperInvariant(why[0]) + why[1..] + "."; }

    public string? JoinRefusal(World w, House? house, string side)
    {
        var cw = w.CivilWar; if (cw == null) return "the Reach is at peace with itself";
        if (house == null || !Real(house)) return "no such house";
        if (house.Id == cw.HouseId) return "it is your own war";
        if (side != General && side != Senate) return "name a side";
        return cw.Declared.ContainsKey(house.Id) ? $"the house has declared for {(cw.Declared[house.Id] == General ? "the general" : "the Senate")}" : null;
    }

    /// <summary>A house that had stood aside declares, and if it has fleets it fights.</summary>
    public void Join(World w, House house, string side)
    {
        var cw = w.CivilWar!; var general = w.House(cw.HouseId); cw.Declared[house.Id] = side; bool fights = FleetSystem.StrengthOf(w, house.Id) > 0;
        if (side == General)
        {
            if (fights) { cw.With.Add(house.Id); if (cw.SwordHouseId.Length > 0) _fleets.DeclareWar(w, house.Id, cw.SwordHouseId); }
        }
        else
        {
            if (fights) { cw.Against.Add(house.Id); _fleets.DeclareWar(w, house.Id, cw.HouseId); }
            if (cw.SwordHouseId.Length == 0) { cw.SwordHouseId = house.Id; int hulls = ArmTheSword(w, cw); if (hulls > 0) w.Log($"The Senate puts the Reach's own {hulls} hulls under {house.Name}."); }
            LegitimacyBook.Move(w, house, 2, LegitimacyBook.War, "The Senate put the Reach's own fleet under the house");
        }
        if (general != null && Economy.HeadOf(w, general) is { } gh && Economy.HeadOf(w, house) is { } hh)
        { var r = w.Relationship(gh.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition + (side == General ? 20 : -20), -100, 100); }
        w.Log($"{house.Name} declares for {(side == General ? w.Char(cw.GeneralId)?.Name ?? "the general" : "the Senate")}{(fights ? ", and its fleets with it" : "")}.");
    }

    /// <summary>How the civil war stands for the general: the lead in its battles, and the years nobody met counted to the stronger side.</summary>
    public static int Standing(World w, CivilWar cw)
    {
        int lead = 0;
        foreach (var war in w.Wars)
            foreach (var mineSide in cw.With.Prepend(cw.HouseId))
                if (war.Involves(mineSide) && cw.Against.Contains(war.Other(mineSide))) lead += war.Lead(mineSide);
        return lead + cw.Drift;
    }
    public static int Hulls(World w, IEnumerable<string> houses) => houses.Sum(id => FleetSystem.StrengthOf(w, id));
    /// <summary>Does the general's fleet hold the capital's orbit, with none of the Senate's there?</summary>
    public static bool HoldsCapital(World w, CivilWar cw)
    {
        var cap = w.Galaxy?.Capital ?? ""; if (cap.Length == 0) return false;
        if (w.PlanetHolder(cap) == cw.HouseId) return false;   // a house that sits in the capital already has taken nothing by staying there: it must break the Senate's fleets
        if (!w.Fleets.Values.Any(f => f.OwnerId == cw.HouseId && !f.Moving && !f.IsAway && f.SystemId == cap && f.Strength >= 2)) return false;
        return !w.Fleets.Values.Any(f => cw.Against.Contains(f.OwnerId) && !f.Moving && !f.IsAway && f.SystemId == cap && f.Strength >= 2);
    }

    private void Peace(World w, CivilWar cw)
    {
        foreach (var a in cw.With.Prepend(cw.HouseId).ToList()) foreach (var b in cw.Against.ToList()) _fleets.MakePeace(w, a, b, terms: false);
    }

    /// <summary>The general has the capital, or has broken those who stood in the way. The Republic ends.</summary>
    public void Win(World w, CivilWar cw)
    {
        var house = w.House(cw.HouseId)!; var g = w.Char(cw.GeneralId); var mine = w.Protagonist?.HouseId; var ghead = Economy.HeadOf(w, house);
        Peace(w, cw); w.CivilWar = null; w.Recalls.Clear(); foreach (var c in w.Characters.Values) c.Flags.Remove(RecalledChar);
        w.WorldCounters[PolitySystem.CivilWarEndedKey] = w.Turn;
        foreach (var (id, side) in cw.Declared) if (side == Senate) w.WorldCounters[PolitySystem.OpposedKey(id)] = w.Turn;   // they need not kneel (docs/BREAKAWAY.md)
        ThroneSystem.Enthrone(w, house.Id, seized: true, byCharacterId: g?.Id);
        w.WorldFlags.Add(RepublicFallenFlag); w.WorldCounters["republics_ended"] = w.Counter("republics_ended") + 1;
        if (g != null) { g.Flags.Remove(CaesarChar); g.Flags.Add("first_emperor"); LifeSystem.Note(w, g, "throne", "Marched on the capital and ended the Republic."); }
        foreach (var (id, side) in cw.Declared.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var h = w.House(id); if (h == null) continue; var hh = Economy.HeadOf(w, h);
            LegitimacyBook.Move(w, h, (side == General ? 4 : -6), LegitimacyBook.War, side == General ? "Stood with the general who took the capital" : "Stood against the general who took the capital");
            if (ghead != null && hh != null) { var r = w.Relationship(ghead.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition + (side == General ? 20 : -20), -100, 100); }
        }
        if (w.House(cw.SwordHouseId) is { } sword && sword.Treasury.Credits > 0)
        {
            int fine = sword.Treasury.Credits * 3 / 10; sword.Treasury.Credits -= fine; house.Treasury.Credits += fine;
            w.Log($"{sword.Name}, which carried the Senate's war, is proscribed: {Money.Credits(fine)} of its estate go to the new throne.");
        }
        if (house.Id == mine) Mark(w, CrownedFlag);
        else { Mark(w, FallenFlag); if (mine != null && cw.Declared.TryGetValue(mine, out var side)) Mark(w, side == General ? ServedFlag : OpposedFlag); }
    }

    /// <summary>The general is broken. He does not outlive it, and his house pays.</summary>
    public void Lose(World w, CivilWar cw, string why)
    {
        var house = w.House(cw.HouseId); var g = w.Char(cw.GeneralId); var mine = w.Protagonist?.HouseId; var sword = w.House(cw.SwordHouseId);
        Peace(w, cw); w.CivilWar = null; w.WorldCounters["caesars_broken"] = w.Counter("caesars_broken") + 1; w.WorldCounters[PolitySystem.CivilWarEndedKey] = w.Turn;
        w.Log($"The march on the capital is over: {why}. The Republic stands.");
        if (house != null)
        {
            LegitimacyBook.Move(w, house, -25, LegitimacyBook.War, "Declared an enemy of the Reach"); w.WorldCounters[EnemyKey(house.Id)] = w.Turn + 10;
            if (house.Treasury.Credits > 0) { int fine = house.Treasury.Credits * 4 / 10; house.Treasury.Credits -= fine; if (sword != null) sword.Treasury.Credits += fine; w.Log($"{house.Name} is declared an enemy of the Reach and fined {Money.Credits(fine)}."); }
            foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id)) { f.DevotedTo = ""; f.Devotion = 0; f.Loyalty = Math.Max(0, f.Loyalty - 10); }
            OfficeSystem.Vacate(w, _offices, house.Id);
            if (house.Id == mine) Mark(w, BrokenFlag);
        }
        foreach (var (id, side) in cw.Declared.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (w.House(id) is { } h) LegitimacyBook.Move(w, h, (side == General ? -6 : 2), LegitimacyBook.War, side == General ? "Stood with a general who was broken" : "Stood by the Senate against a general");
        if (sword != null)
        {
            LegitimacyBook.Move(w, sword, 10, LegitimacyBook.War, "Carried the Senate's war against a general"); sword.Glory = Math.Min(12, sword.Glory + 3);
            if (GeneralOf(w, sword) is { } sg) { w.WorldCounters[TriumphKey(sg.Id)] = Triumphs(w, sg) + 1; sg.Flags.Add(TriumphatorChar); LifeSystem.Note(w, sg, "triumph", "Carried the Senate's war against a general who marched on the capital, and won it."); }
        }
        if (mine != null && mine != cw.HouseId && cw.Declared.TryGetValue(mine, out var mineSide)) Mark(w, mineSide == Senate ? SavedFlag : BrokenFlag);
        if (g is { Alive: true })
        {
            g.Flags.Remove(CaesarChar);
            w.Log($"{g.Name} does not wait for the Senate's officers.");
            DynastySystem.Die(w, g.Id, "by their own hand, before the Senate's officers came");
        }
    }

    // ---------------- what the head can do for the crews ----------------

    public static int DonativeCost(Fleet f) => Math.Max(4, f.Strength * DonativePerHull);
    public string? DonativeRefusal(World w, House? house, Fleet? f)
    {
        if (house == null || f == null || (f.OwnerId != house.Id || f.Office) && !ServiceSystem.CommandedBy(w, f, house.Id)) return "not a fleet of the house, nor one that one of the house commands by service";
        if (f.AdmiralId.Length == 0) return "it has no commander for the crews to thank";
        if (w.Counter("donative:" + f.Id) == w.Turn + 1) return "they have had one this year";
        return house.Treasury.Credits >= DonativeCost(f) ? null : $"it takes {Money.Credits(DonativeCost(f))}";
    }
    /// <summary>A gift to the crews in their commander's name: they take to him, and to the house a little.</summary>
    public void Donative(World w, House house, Fleet f)
    {
        var c = w.Char(f.AdmiralId)!; house.Treasury.Credits -= DonativeCost(f); w.WorldCounters["donative:" + f.Id] = w.Turn + 1;
        Attach(w, f, c); f.Devotion = Math.Min(100, f.Devotion + DonativeDevotion); f.Loyalty = Math.Min(100, f.Loyalty + 4);
        w.Log($"A donative is paid to the crews of {f.Name} in {c.Name}'s name.");
    }

    // ---------------- the year ----------------

    // ---------------- the three gates ----------------
    // The owner's rule (docs/PROCESS.md, 2026-11-14): the Republic stands unless the player ends it. There was one Caesar, and for
    // a reason: he was extremely able, extremely well liked, and the times were his. Another house's general marches only when all
    // three hold. The player may march without them, and then finds what they are worth in who declares.

    public const int GateTactics = 6, GateVictories = 4, GateCharisma = 5, GateCharm = 8, GateTriumphs = 2, GateHeroVictories = 7, GateLegitimacy = 50, TroubledAt = 2, GreatWarMemory = 15;
    public const string GreatWarEndedKey = "great_war_ended";

    /// <summary>What is wrong with the Republic this year. Two or more and it is troubled: the times a general needs.</summary>
    public static List<string> Troubles(World w)
    {
        var list = new List<string>(); if (w.IsImperium) return list;
        if (Within(w, GreatWarEndedKey, GreatWarMemory) || GreatWar(w)) list.Add("the War of the Reach is in living memory");
        list.AddRange(Wrong(w));
        return list;
    }
    /// <summary>What is wrong with the Reach this year, under either government and apart from the great war itself. The same list
    /// tells a general the times are his and pushes the Reach toward its great war (docs/BREAKAWAY.md, what sets it off).</summary>
    public static List<string> Wrong(World w)
    {
        var list = new List<string>();
        if (w.Galaxy != null && w.Regions.Values.Count(r => r.Lawlessness >= 50) >= 2) list.Add("the lanes of more than one region are lawless");
        if (RegionSystem.CapitalHolder(w) is { Legitimacy: < 40 } chair) list.Add($"{chair.Name}, which holds the capital, is not respected");
        if (w.Nations.Any(n => n.AtWar && n.ScoreNation > n.ScoreReach)) list.Add("a war beyond the Reach is going badly");
        if (w.Wars.Count >= 4) list.Add("the houses are at war with each other");
        if (w.WorldFlags.Contains(CompanySystem.CrashFlag)) list.Add("the Exchange has crashed");
        if (LadderSystem.Trust(w) < LadderSystem.TrustLow) list.Add("the magistrates are known to be for sale");
        return list;
    }
    public static bool Troubled(World w) => Troubles(w).Count >= TroubledAt;

    /// <summary>A commander of the first rank: the skill, the victories, and a triumph to show for them.</summary>
    public static bool FirstRank(World w, Character c) => c.Skill("tactics") >= GateTactics && Victories(w, c) >= GateVictories && Triumphs(w, c) > 0;
    /// <summary>Loved: by nature or by the name the Reach has given, by crews who would follow, and of a house that is respected.</summary>
    public static bool Loved(World w, Character c, House house) => Charms(w, c) && DevotedHulls(w, c) >= MarchHulls && house.Legitimacy >= GateLegitimacy;
    /// <summary>Liked by nature (charisma, or charisma and a tongue), or made the squares' own by what they have done: the name the Reach
    /// gives one who leads from the front, or a second triumph.</summary>
    public static bool Charms(World w, Character c) =>
        c.Skill("charisma") >= GateCharisma || c.Skill("charisma") + c.Skill("oratory") >= GateCharm || c.Flags.Contains(FleetSystem.AchillesFlag) || Triumphs(w, c) >= GateTriumphs || Victories(w, c) >= GateHeroVictories;

    /// <summary>The three gates for this house's general, each with whether it is met and what it lacks.</summary>
    public static List<(string name, bool met, string says)> Gates(World w, House house)
    {
        var g = GeneralOf(w, house); var list = new List<(string, bool, string)>(); if (g == null) return list;
        var lacks = new List<string>();
        if (g.Skill("tactics") < GateTactics) lacks.Add($"tactics {g.Skill("tactics")} of {GateTactics}");
        if (Victories(w, g) < GateVictories) lacks.Add($"{Victories(w, g)} of {GateVictories} battles won");
        if (Triumphs(w, g) == 0) lacks.Add("no triumph");
        list.Add(("A commander of the first rank", lacks.Count == 0, lacks.Count == 0 ? $"tactics {g.Skill("tactics")}, {Victories(w, g)} battles won, {Counted.Triumphs(Triumphs(w, g))}" : string.Join(", ", lacks)));
        lacks = new List<string>();
        if (!Charms(w, g)) lacks.Add($"charisma {g.Skill("charisma")} of {GateCharisma}, no name the Reach has given, not a second triumph, and {Victories(w, g)} of {GateHeroVictories} victories");
        if (DevotedHulls(w, g) < MarchHulls) lacks.Add($"{DevotedHulls(w, g)} of {MarchHulls} hulls would follow");
        if (house.Legitimacy < GateLegitimacy) lacks.Add($"the house's legitimacy is {house.Legitimacy} of {GateLegitimacy}");
        list.Add(("Loved", lacks.Count == 0, lacks.Count == 0 ? "by the crews, by the squares, and of a house that is respected" : string.Join(", ", lacks)));
        var troubles = Troubles(w);
        list.Add(("The times", troubles.Count >= TroubledAt, troubles.Count == 0 ? "the Republic is sound" : (troubles.Count >= TroubledAt ? "" : "only one thing is wrong: ") + string.Join("; ", troubles)));
        return list;
    }
    public static bool Ready(World w, House house) { var gates = Gates(w, house); return gates.Count > 0 && gates.All(x => x.met); }

    /// <summary>Would another house's general cross? Only one who has passed all three gates, wants it, and is not plainly outmatched.</summary>
    public bool WouldCross(World w, House house)
    {
        var g = GeneralOf(w, house); if (g == null || !Ready(w, house)) return false;
        // one with the record, the love and the times mostly would: only the loyal by nature, and those who want a quiet life, do not
        bool wants = g.Ambition is "seize_the_throne" or "outshine_all_rivals" || CourtshipSystem.Wilful(g) || (!g.NatureTraits.Contains("Loyal") && g.Ambition != "win_a_great_love");
        int strongest = w.Houses.Values.Where(h => Real(h) && h.Id != house.Id).Select(h => FleetSystem.StrengthOf(w, h.Id)).DefaultIfEmpty(0).Max();
        return wants && ServiceSystem.MarchStrength(w, house, g) * 5 >= strongest * 2;   // the hulls that would go with him, his house's and any whose crews are his by service
    }

    /// <summary>If this house crossed today, which houses would declare for its general and which for the Senate.</summary>
    public (List<House> forGeneral, List<House> forSenate) Forecast(World w, House house)
    {
        var g = GeneralOf(w, house); var a = new List<House>(); var b = new List<House>(); if (g == null) return (a, b);
        var chair = RegionSystem.CapitalHolder(w);
        _ = chair;
        foreach (var (id, side) in Sides(w, house, g, forecast: true).OrderBy(kv => kv.Key, StringComparer.Ordinal)) (side == General ? a : b).Add(w.House(id)!);
        return (a, b);
    }

    public void Tick(World w)
    {
        var p = w.Protagonist; var mine = p?.HouseId;
        foreach (var f in YearFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        if (!w.HasFlag(TriumphFlag) && !w.HasFlag(KinTriumphFlag)) foreach (var c in w.Characters.Values) c.Flags.Remove(NewTriumphatorChar);
        foreach (var c in w.Characters.Values) { c.Flags.Remove(LovedChar); c.Flags.Remove(GreatNameChar); }
        Crews(w, mine);

        // the recalls: another house's general answers in the year; the player's house has until the deadline
        foreach (var r in w.Recalls.ToList())
        {
            var house = w.House(r.HouseId); var g = w.Char(r.CharacterId);
            if (house == null || g is not { Alive: true } || w.IsImperium || !w.Fleets.Values.Any(f => f.AdmiralId == g.Id)) { w.Recalls.Remove(r); g?.Flags.Remove(RecalledChar); continue; }
            if (w.Turn <= r.Since) continue;
            if (house.Id != mine) { if (w.CivilWar == null && WouldCross(w, house) && CrossRefusal(w, house) == null) Cross(w, house); else Obey(w, house, ordered: false); }
            else if (g.Id != p!.Id && w.FamilyAutonomy > 0 && !CourtshipSystem.Wilful(g)) Obey(w, house, ordered: false);     // it is the law, and he keeps it
            else if (w.Turn >= r.Deadline) Obey(w, house, ordered: false, late: true);
        }
        // a general with a grievance and the fleet for it may not wait to be recalled
        if (!w.IsImperium && w.CivilWar == null)
            foreach (var h in w.Houses.Values.Where(h => Real(h) && h.Id != mine).OrderBy(h => h.Id, StringComparer.Ordinal))
                if (Grievance(w, h) != null && WouldCross(w, h) && CrossRefusal(w, h) == null) { Cross(w, h); break; }

        // a throne taken some other way, or the great war breaking out, ends a march without a victor
        if (w.CivilWar is { } stale && (w.IsImperium || GreatWar(w))) { Peace(w, stale); w.CivilWar = null; w.Char(stale.GeneralId)?.Flags.Remove(CaesarChar); w.Log("The march on the capital is swallowed by a larger war."); }
        if (w.IsImperium && w.Recalls.Count > 0) { foreach (var r in w.Recalls) w.Char(r.CharacterId)?.Flags.Remove(RecalledChar); w.Recalls.Clear(); }
        if (w.CivilWar is { } cw)
        {
            var house = w.House(cw.HouseId); var g = w.Char(cw.GeneralId);
            if (house == null || g is not { Alive: true }) Lose(w, cw, "the general is dead, and the cause with him");
            else
            {
                cw.With.RemoveAll(id => w.House(id) == null); cw.Against.RemoveAll(id => w.House(id) == null);
                bool met = w.Wars.Any(x => x.LastBattleTurn >= w.Turn && (x.Involves(cw.HouseId) || cw.With.Any(x.Involves)) && cw.Against.Any(x.Involves));
                if (!met)
                {
                    int hg = Hulls(w, cw.With.Prepend(cw.HouseId)), hs = Hulls(w, cw.Against);
                    if (hg * 5 > hs * 6) cw.Drift++; else if (hs * 5 > hg * 6) cw.Drift--;
                }
                cw.Held = HoldsCapital(w, cw) ? cw.Held + 1 : 0;
                int years = w.Turn - cw.Since, standing = Standing(w, cw), hulls = FleetSystem.StrengthOf(w, house.Id);
                if (cw.Held >= HoldYears || cw.Against.Count == 0 || (years >= 2 && standing >= DecisiveLead)) Win(w, cw);
                else if (hulls == 0) Lose(w, cw, $"{g.Name} has no fleet left");
                else if (years >= 1 && hulls * 4 <= cw.StartHulls) Lose(w, cw, $"{g.Name}'s fleets are broken");
                else if (standing <= -DecisiveLead) Lose(w, cw, $"{g.Name} is beaten in the field");
                else if (years >= WarYears) Lose(w, cw, $"after {years} years the crews will follow {g.Name} no further");
            }
        }

        if (p is not { Alive: true } || mine == null || w.House(mine) is not { } my) return;
        if (w.Recalls.FirstOrDefault(r => r.HouseId == mine) is { } rc) w.WorldFlags.Add(rc.CharacterId == p.Id ? RecalledFlag : KinRecalledFlag);
        if (w.CivilWar is { } now) { w.WorldFlags.Add(CivilWarFlag); if (now.HouseId == mine) w.WorldFlags.Add(MyCivilWarFlag); if (now.SwordHouseId == mine) w.WorldFlags.Add(SwordFlag); }
        if (w.IsImperium) return;
        if (Troubled(w)) w.WorldFlags.Add(TroubledFlag);
        if (Ready(w, my)) w.WorldFlags.Add(HourFlag);
        // a general of another house who is of the first rank and loved: the one the Reach may yet have to reckon with
        foreach (var h in w.Houses.Values.Where(h => Real(h) && h.Id != mine && h.Id != RegionSystem.CapitalHolder(w)?.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
            if (GeneralOf(w, h) is { } rg && FirstRank(w, rg) && Loved(w, rg, h)) { rg.Flags.Add(GreatNameChar); w.WorldFlags.Add(GreatNameFlag); }
        if (Sway(w, p) >= RecallSway) w.WorldFlags.Add(SwayFlag);
        if (GeneralOf(w, my) is { } mg && Laurels(w, mg) >= TriumphLaurels) w.WorldFlags.Add(LaurelsFlag);
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine && c.Id != p.Id && DevotedHulls(w, c) >= MarchHulls)) { c.Flags.Add(LovedChar); w.WorldFlags.Add(KinLovedFlag); }
    }
}
