using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// A charge against the powerful is a gamble (docs/LADDER.md, "A charge against the powerful"; docs/IDEAS_FROM_IMPERATOR.md, 2).
/// <para>A charge brought before a praetor against a person of great sway, or against the head of a house of Legate rank or
/// above, is held a year whoever brings it, and it is a cause to cross the line for the house of the accused in the year it is
/// heard, as a recall or a censure is (<see cref="CaesarSystem.Grievance"/>). The accused has three answers: stand trial; go
/// into EXILE before the verdict, so that their offices and commands lapse and they live at a nation's court beyond the Reach,
/// their purse with them, until the Senate votes them home (the motion <see cref="HomeKind"/>); or, if they could, cross the
/// line. An acquittal costs the house that brought it standing and gives the accused renown. A conviction of one who commands
/// forces the choice: lay the command down, or cross. And a house may let it be known that a general will be charged the day
/// the command is laid down: that THREAT is a cause too, and costs the house that makes it if the charge is never brought.</para>
/// Other houses' people answer by their nature and by what they expect of the court; their generals cross only as they would at
/// a recall (<see cref="CaesarSystem.WouldCross"/>). RNG-free.
/// </summary>
public sealed class ChargeSystem
{
    /// <summary>Sway that makes a person one of the powerful, and the rank of house whose head is (3: Legate).</summary>
    public const int PowerfulSway = 40, PowerfulRank = 3;
    /// <summary>An acquittal costs the accuser this much legitimacy on top of the point any charge let go costs; the accused's house,
    /// when one of its own goes abroad, this much (twice for its head); the accuser gains as for a conviction.</summary>
    public const int AcquittalLegitimacy = 3, ExileLegitimacy = 2, ExileAccuserLegitimacy = 2, ExileRegard = 20;
    /// <summary>The Senate's vote to call an exile home: its house gains this, the exile thinks this much better of the one who moved it,
    /// and other houses move it for an exile who has been abroad this long.</summary>
    public const int HomeLegitimacy = 2, HomeRegard = 15, HomeAfterYears = 3, HomeFriendAt = 30;
    /// <summary>A threat: its clout, what the general's head thinks of it, what an empty one costs, and the years after the command is
    /// laid down within which the charge must be brought (to the end of the year after).</summary>
    public const int ThreatInfluence = 1, ThreatRegard = 10, ThreatEmptyLegitimacy = 3, ThreatGraceYears = 1;
    /// <summary>A convicted commander has to the end of the year after the verdict to lay the command down or cross; the court's
    /// officers take a command not laid down by then, and the house loses this.</summary>
    public const int LayDownYears = 1, TakenLegitimacy = 3;
    public const string ExiledChar = "in_exile", HomeKind = "home";
    public const string Trial = "trial", ExileAnswer = "exile", Cross = "cross", LayDown = "lay_down", LaidDown = "laid_down", Crossed = "crossed", Exiled = "exiled";
    public const string Kept = "kept", Empty = "empty", Lapsed = "lapsed";
    // true for a year after the deed, for the player's house (content may read them)
    public const string WentAbroadFlag = "went_into_exile", CalledHomeFlag = "called_home_from_exile", ThrownBackFlag = "charge_thrown_back";
    private static readonly string[] DeedFlags = { WentAbroadFlag, CalledHomeFlag, ThrownBackFlag };
    // counters, for the probe
    public const string BroughtKey = "powerful_charged", HeardKey = "powerful_tried", ConvictedKey = "powerful_convicted", AcquittedKey = "powerful_acquitted",
        ExiledKey = "powerful_exiled", CrossedKey = "powerful_crossed", LaidDownKey = "powerful_laid_down", HomeKey = "exiles_home", HomeMovedKey = "exiles_home_moved",
        ThreatsKey = "charge_threats", ThreatsKeptKey = "charge_threats_kept", ThreatsEmptyKey = "charge_threats_empty", ThreatsLapsedKey = "charge_threats_lapsed";

    private readonly NationCatalog _nations; private readonly OfficeCatalog _offices; private readonly CaesarSystem _caesar;
    public ChargeSystem(NationCatalog nations, OfficeCatalog offices, CaesarSystem caesar) { _nations = nations; _offices = offices; _caesar = caesar; }
    public CaesarSystem Caesar => _caesar;

    private static void Bump(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static string Nm(World w, string houseId) => w.House(houseId)?.Name ?? "a house";
    private static void Regard(World w, Character? from, Character? to, int by)
    {
        if (from == null || to == null || from.Id == to.Id) return; var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100);
    }
    private static Character? Head(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;

    // ---------------- who is powerful ----------------

    /// <summary>One of the powerful: a person of great sway, or the head of a house of Legate rank or above.</summary>
    public static bool Powerful(World w, Character? c) => c is { Alive: true } && (CaesarSystem.Sway(w, c) >= PowerfulSway || (w.House(c.HouseId) is { } h && Economy.HeadOf(w, h)?.Id == c.Id && Title.RankOf(h.Title) >= PowerfulRank));
    public static bool Powerful(World w, OfficeDeed? d) => d != null && Powerful(w, w.Char(d.CharacterId));
    /// <summary>Why they count among the powerful, in words.</summary>
    public static string PowerfulWhy(World w, Character c)
    {
        int sway = CaesarSystem.Sway(w, c);
        if (sway >= PowerfulSway) return $"a person of great sway ({sway})";
        return w.House(c.HouseId) is { } h ? $"the head of {h.Name}, a house of {Title.StyleOf(h.Title, w.Era)} rank" : "one of the powerful";
    }
    /// <summary>Whether this person commands a fleet: their house's, or another's by service.</summary>
    public static bool Commands(World w, Character c) => w.Fleets.Values.Any(f => f.AdmiralId == c.Id) || ServiceSystem.Held(w, c) != null;

    // ---------------- exile ----------------

    public static bool Abroad(World w, Character? c) => c != null && c.Flags.Contains(ExiledChar);
    public static Exile? ExileOf(World w, string characterId) => w.Exiles.FirstOrDefault(x => x.CharacterId == characterId);
    /// <summary>Where an exile lives, in words: "at the court of the Vashti Khanates", or among the far worlds.</summary>
    public static string WhereSays(Exile? x) => x == null || x.NationName.Length == 0 ? "among the far worlds beyond the rim" : "at the court of " + x.NationName;
    /// <summary>Why this person can do nothing in the Reach's politics, or null: they are in exile. <paramref name="you"/>: the
    /// refusal is shown to the player about themselves.</summary>
    public static string? AbroadSays(World w, Character? c, bool you = false) =>
        !Abroad(w, c) ? null : (you ? "you are" : c!.Name + " is") + " in exile " + WhereSays(ExileOf(w, c!.Id)) + " until the Senate votes " + (you ? "you" : "them") + " home";
    /// <summary>A command laid down at a court's order bars another until this year (as the Senate's recall does, <see cref="CaesarSystem.BarredKey"/>).</summary>
    public static string CourtBarKey(string characterId) => "court_no_command:" + characterId;
    /// <summary>Why this person may hold no command now, or null: they are in exile, or a court had them lay one down.</summary>
    public static string? NoCommandSays(World w, Character? c, bool you = false) => AbroadSays(w, c, you)
        ?? (c != null && w.Counter(CourtBarKey(c.Id)) > w.Turn ? $"a court had {(you ? "you" : c.Name)} lay a command down, and {(you ? "you" : c.Name)} may hold none until year {w.Counter(CourtBarKey(c.Id))}" : null);
    /// <summary>Is the head of this house abroad? Then the house moves nothing in the Senate but its exile's return.</summary>
    public static bool HeadAbroad(World w, House? h) => h != null && Abroad(w, Economy.HeadOf(w, h));
    /// <summary>The exiles of a house, longest abroad first.</summary>
    public static List<Exile> ExilesOf(World w, string houseId) => w.Exiles.Where(x => x.HouseId == houseId && w.Char(x.CharacterId) is { Alive: true }).OrderBy(x => x.Since).ThenBy(x => x.CharacterId, StringComparer.Ordinal).ToList();

    /// <summary>The nation whose court takes an exile in: one not fallen and not at war with the Reach, the friendliest first.</summary>
    private (string id, string name) Refuge(World w)
    {
        var n = w.Nations.Where(n => !n.Fallen && !n.AtWar && _nations.Def(n.Id) != null).OrderByDescending(n => n.Disposition).ThenBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
        return n == null ? ("", "") : (n.Id, _nations.Def(n.Id)!.Name);
    }

    /// <summary>They go into exile before the verdict: the charge is not heard, their offices and commands lapse, and they live at
    /// a nation's court with their purse until the Senate votes them home.</summary>
    public void Exile(World w, Character who, Indictment? c)
    {
        if (Abroad(w, who)) return;
        var house = w.House(who.HouseId); bool head = house != null && Economy.HeadOf(w, house)?.Id == who.Id; var (nid, nname) = Refuge(w);
        var accuser = c != null ? w.House(c.AccuserId) : null; var mine = w.Protagonist?.HouseId;
        var x = new Exile { CharacterId = who.Id, HouseId = who.HouseId, WasHead = head, NationId = nid, NationName = nname, Since = w.Turn, AccuserId = c?.AccuserId ?? "", ChargeId = c?.Id ?? "" };
        w.Exiles.Add(x); who.Flags.Add(ExiledChar);
        // the offices of the Reach and the magistracies lapse
        w.Magistrates.RemoveAll(m => m.CharacterId == who.Id); w.Candidacies.RemoveAll(cd => cd.CharacterId == who.Id);
        foreach (var o in w.Offices.Where(o => o.CharacterId == who.Id).ToList()) { if (_offices.Def(o.OfficeId) is { } def) OfficeSystem.Strip(w, def, "they have gone into exile", false); else w.Offices.Remove(o); }
        LayCommandDown(w, who, $"{who.Name} has gone into exile");
        if (c != null)
        {
            c.Outcome = Exiled; c.Answer = ExileAnswer; c.HeardTurn = w.Turn;
            if (LadderSystem.DeedOf(w, c) is { } d) d.Answered = true;   // the exile is the price of it: it is not heard again
            if (accuser != null) LegitimacyBook.Move(w, accuser, ExileAccuserLegitimacy, LegitimacyBook.Courts, "The one it charged went into exile rather than face the court");
            Regard(w, house != null ? Economy.HeadOf(w, house) : null, accuser != null ? Economy.HeadOf(w, accuser) : null, -ExileRegard);
            if (!head) Regard(w, who, accuser != null ? Economy.HeadOf(w, accuser) : null, -ExileRegard);
        }
        if (house != null) LegitimacyBook.Move(w, house, -ExileLegitimacy * (head ? 2 : 1), LegitimacyBook.Courts, head ? "The head of the house went into exile rather than face a charge" : "One of the house went into exile rather than face a charge");
        Bump(w, ExiledKey);
        LifeSystem.Note(w, who, "exile", $"Went into exile {WhereSays(x)} rather than wait for the verdict{(accuser != null ? " on the charge " + accuser.Name + " brought" : "")}.");
        w.Log($"{who.Name} of {house?.Name} does not wait for the verdict, and leaves the Reach for exile {WhereSays(x)}, with what is in {(who.Sex == "female" ? "her" : who.Sex == "male" ? "his" : "their")} own purse. {(head ? "The house is run by letter." : "What they held lapses.")}");
        if (who.HouseId == mine)
        {
            if (who.Id == w.ProtagonistId) Mark(w, WentAbroadFlag);
            w.Tell("warn", who.Id == w.ProtagonistId
                ? $"You have gone into exile {WhereSays(x)}. Your house is still yours, and you may run it by letter, but you may hold no office and no command, bring nothing before the courts and move nothing in the Senate but your own return. The Senate must vote you home."
                : $"{who.Name} has gone into exile {WhereSays(x)}. What they held has lapsed. The Senate must vote them home.", "senate:ladder", who.Id);
        }
    }

    /// <summary>Whatever they command, they command no longer: their fleets are left without an admiral, a command by service ends, and a
    /// captaincy is given up.</summary>
    private static void LayCommandDown(World w, Character who, string how)
    {
        foreach (var s in w.Service.Where(s => s.CommanderId == who.Id).ToList()) ServiceSystem.End(w, s, how);
        foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId == who.Id).OrderBy(f => f.Id, StringComparer.Ordinal)) { if (f.Devotion >= CaesarSystem.DevotedAt) f.Loyalty = Math.Max(0, f.Loyalty - 5); f.AdmiralId = ""; f.DevotedTo = ""; f.Devotion = 0; }
        foreach (var f in w.Fleets.Values.Where(f => f.CaptainId == who.Id)) f.CaptainId = "";
        w.Recalls.RemoveAll(r => r.CharacterId == who.Id); who.Flags.Remove(CaesarSystem.RecalledChar);
        FleetSystem.RefreshAdmiralFlags(w);
    }

    /// <summary>Why the Senate cannot be asked to call one of this house home, or null.</summary>
    public static string? HomeRefusal(World w, House? target)
    {
        if (target == null) return "no such house";
        return ExilesOf(w, target.Id).Count == 0 ? $"nobody of {target.Name} is in exile" : null;
    }
    /// <summary>Who of the house a motion to call home would bring back: the one longest abroad.</summary>
    public static Character? ExileNamed(World w, string houseId) => ExilesOf(w, houseId).Select(x => w.Char(x.CharacterId)).FirstOrDefault(c => c is { Alive: true });
    public static string ExileNameOf(World w, string houseId) => ExileNamed(w, houseId)?.Name ?? "the exile";

    /// <summary>How a house leans on calling an exile of <paramref name="target"/> home: its own want them back; the house that brought the
    /// charge does not; friends and enemies of the house as they are; too soon is too soon, and long enough is long enough; and a great
    /// name is thought safer abroad.</summary>
    public static int HomeLean(World w, House h, House target, int toTarget)
    {
        if (h.Id == target.Id) return 4;
        var x = ExilesOf(w, target.Id).FirstOrDefault(); if (x == null) return 0;
        int lean = toTarget >= HomeFriendAt ? 2 : toTarget >= 10 ? 1 : toTarget <= -20 ? -2 : 0;
        if (x.AccuserId == h.Id) lean -= 3;
        int years = w.Turn - x.Since; if (years < 2) lean -= 1; else if (years >= 5) lean += 1;
        if (w.Char(x.CharacterId) is { } c && CaesarSystem.Sway(w, c) >= CaesarSystem.FearedSway && FactionSystem.Of(w, h.Id)?.PartyId != "sword") lean -= 1;
        return lean;
    }

    /// <summary>The Senate votes an exile home: they come back to the Reach and may stand and command again.</summary>
    public static void Home(World w, House target, House? proposer)
    {
        var x = ExilesOf(w, target.Id).FirstOrDefault(); var who = x != null ? w.Char(x.CharacterId) : null; if (x == null || who == null) return;
        w.Exiles.Remove(x); who.Flags.Remove(ExiledChar);
        LegitimacyBook.Move(w, target, HomeLegitimacy, LegitimacyBook.Senate, "The Senate called one of the house home from exile");
        if (proposer != null && proposer.Id != target.Id) Regard(w, who, Economy.HeadOf(w, proposer), HomeRegard);
        Bump(w, HomeKey);
        LifeSystem.Note(w, who, "exile", $"Was called home from exile by the Senate after {Counted.Years(w.Turn - x.Since)}.");
        w.Log($"The Senate calls {who.Name} of {target.Name} home from exile {WhereSays(x)}. {(who.Sex == "female" ? "She" : who.Sex == "male" ? "He" : "They")} may stand for office and hold a command again.");
        if (target.Id == w.Protagonist?.HouseId)
        {
            Mark(w, CalledHomeFlag);
            w.Tell("good", who.Id == w.ProtagonistId ? "The Senate has voted you home. You may stand, hold a command and bring business before the Senate and the courts again." : $"The Senate has voted {who.Name} home from exile.", "senate:ladder", who.Id);
        }
    }

    /// <summary>Other houses move to call an exile home: the exile's own house, once they have been abroad <see cref="HomeAfterYears"/>
    /// years, or a house whose head is a friend of theirs or of their house. The player's own house moves it only when the player
    /// says. True if something was moved.</summary>
    public static bool MoveHome(World w, SenateSystem senate, string? mine)
    {
        if (senate.Motions.Def(HomeKind) == null || w.Motions.Any(m => m.Open && m.Kind == HomeKind)) return false;
        bool Can(House h) => !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && senate.Door(w, h) != null && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id) && Economy.HeadOf(w, h) is { } hd && !Abroad(w, hd);
        foreach (var x in w.Exiles.OrderBy(x => x.Since).ThenBy(x => x.CharacterId, StringComparer.Ordinal).ToList())
        {
            var target = w.House(x.HouseId); var who = w.Char(x.CharacterId); if (target == null || who is not { Alive: true } || w.Turn - x.Since < HomeAfterYears) continue;
            var th = Economy.HeadOf(w, target);
            House? mover = target.Id != mine && Door(senate, w, target) && !SenateSystem.Stale(w, target.Id, HomeKind, target.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == target.Id) ? target : null;
            mover ??= w.Houses.Values.Where(h => h.Id != target.Id && h.Id != mine && h.Id != x.AccuserId && Can(h) && !SenateSystem.Stale(w, h.Id, HomeKind, target.Id)
                    && Economy.HeadOf(w, h) is { } hh && ((w.GetRelationship(hh.Id, who.Id)?.Disposition ?? 0) >= HomeFriendAt || (th != null && th.Id != who.Id && (w.GetRelationship(hh.Id, th.Id)?.Disposition ?? 0) >= HomeFriendAt)))
                .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (mover == null) continue;
            senate.Add(w, mover.Id, HomeKind, target.Id); Bump(w, HomeMovedKey);
            return true;
        }
        return false;
    }
    /// <summary>A house whose head is abroad may still move its exile's return: its people at home ask it in the head's name.</summary>
    private static bool Door(SenateSystem senate, World w, House h) => !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && senate.Door(w, h) != null;
    /// <summary>May this house move its own exile's return although its head is abroad? Its people at home ask it in the head's name.</summary>
    public static bool MayMoveHome(World w, SenateSystem senate, House h) => HeadAbroad(w, h) && Door(senate, w, h);

    // ---------------- the cause ----------------

    /// <summary>What a charge against the powerful gives a house as a cause to cross the line, or null: a charge against one of its
    /// own waiting to be heard or heard this year or last; a conviction of its general who must lay the command down; or a threat
    /// that its general will be charged on laying it down.</summary>
    public static string? Cause(World w, House house)
    {
        if (w.Indictments.Count == 0 && w.ChargeThreats.Count == 0) return null;
        foreach (var c in w.Indictments.Where(c => c.Powerful && c.HouseId == house.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var who = w.Char(c.AccusedId); if (who == null) continue;
            if (c.Sentence == LayDown) return $"a praetor has convicted {who.Name}, who must lay the command down";
            if (c.Outcome.Length == 0) return $"{Nm(w, c.AccuserId)} has charged {who.Name} before a praetor";
            if (c.HeardTurn >= w.Turn - 1) return $"{Nm(w, c.AccuserId)} brought {who.Name} before a praetor";
        }
        foreach (var t in w.ChargeThreats.Where(t => t.Outcome.Length == 0 && t.HouseId == house.Id && t.LaidDownTurn == 0 && w.Turn - t.Turn <= CaesarSystem.GrievanceYears).OrderBy(t => t.Id, StringComparer.Ordinal))
            if (w.Char(t.AccusedId) is { Alive: true } g && Commands(w, g)) return $"{Nm(w, t.AccuserId)} has let it be known that {g.Name} will be charged the day the command is laid down";
        return null;
    }

    // ---------------- a charge is brought, and heard ----------------

    /// <summary>A charge is brought: one against the powerful is marked so (it was held a year by <see cref="LadderSystem.Indict"/>);
    /// any threat the accuser made against the accused is kept.</summary>
    public static void Brought(World w, string accuserId, OfficeDeed d, Indictment? held)
    {
        foreach (var t in w.ChargeThreats.Where(t => t.Outcome.Length == 0 && t.AccuserId == accuserId && t.AccusedId == d.CharacterId)) { t.Outcome = Kept; Bump(w, ThreatsKeptKey); }
        if (held == null || !Powerful(w, d)) return;
        held.Powerful = true; Bump(w, BroughtKey);
        var who = w.Char(d.CharacterId)!; var mine = w.Protagonist?.HouseId;
        w.Log($"{who.Name} is {PowerfulWhy(w, who)}. The charge {Nm(w, accuserId)} brings is talked of across the Reach, and nobody expects {who.Name} only to wait for it.");
        if (accuserId == mine && d.HouseId != mine)
            w.Tell("info", $"Your charge against {who.Name} of {Nm(w, d.HouseId)} is heard at the end of next year. {who.Name} is {PowerfulWhy(w, who)}: they may stand trial, go into exile, or, if they have the fleet and the name for it, cross the line. If they are let go, your house loses {1 + AcquittalLegitimacy} legitimacy.", "senate:ladder", who.Id);
        else if (d.HouseId == mine)
            w.Tell("warn", $"{who.Name} is {PowerfulWhy(w, who)}. Before the charge is heard " + (who.Id == w.ProtagonistId ? "you" : who.Name) + " may stand trial, go into exile beyond the Reach, or cross the line if the house has the fleet and the name. It is a cause, as a recall is.", "senate:ladder", who.Id);
    }

    /// <summary>A charge against the powerful has been heard. Let go, the house that brought it pays for it and the accused is the
    /// better known; proved, one who commands must lay the command down, or cross.</summary>
    public static void Heard(World w, Indictment? held, Character who, House accuser, bool guilty)
    {
        if (held is not { Powerful: true }) return;
        held.HeardTurn = w.Turn; Bump(w, HeardKey); var mine = w.Protagonist?.HouseId;
        if (!guilty)
        {
            Bump(w, AcquittedKey);
            LegitimacyBook.Move(w, accuser, -AcquittalLegitimacy, LegitimacyBook.Courts, "A charge against one of the powerful thrown back");
            LifeSystem.Note(w, who, "office", $"Was charged by {accuser.Name} and acquitted, and the Reach thought the more of them for it.");
            if (who.HouseId == mine) Mark(w, ThrownBackFlag);
            w.Log($"The charge {accuser.Name} brought against {who.Name} is thrown back, and {accuser.Name} is laughed at for it.");
            if (accuser.Id == mine) w.Tell("warn", $"{who.Name} is acquitted. The charge cost your house {1 + AcquittalLegitimacy} legitimacy, and made {who.Name} better known.", "senate:ladder", who.Id);
            return;
        }
        Bump(w, ConvictedKey);
        if (!Commands(w, who)) return;
        held.Sentence = LayDown; held.AnswerBy = w.Turn + LayDownYears;
        w.Log($"{who.Name} is convicted, and holds a command: the court orders it laid down by the end of year {held.AnswerBy}.");
        if (who.HouseId == mine) w.Tell("warn", $"{(who.Id == w.ProtagonistId ? "You are" : who.Name + " is")} convicted while in command. The command must be laid down by the end of year {held.AnswerBy}, or the house crosses the line.", "senate:ladder", who.Id);
    }

    // ---------------- answers ----------------

    /// <summary>The charge against the powerful a command or a screen names: one of the house's people, waiting, or convicted and in
    /// command.</summary>
    public static Indictment? Pending(World w, string chargeId) => w.Indictments.FirstOrDefault(c => c.Id == chargeId && c.Powerful);
    /// <summary>The charges against the powerful that touch a house: against its people, or brought by it, still to be answered.</summary>
    public static List<Indictment> Touching(World w, string houseId) =>
        w.Indictments.Where(c => c.Powerful && (c.HouseId == houseId || c.AccuserId == houseId) && (c.Outcome.Length == 0 || c.Sentence == LayDown)).OrderBy(c => c.Turn).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

    /// <summary>Why the head of the house may not give this answer for the accused, or null.</summary>
    public string? AnswerRefusal(World w, Character? head, Indictment? c, string answer)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house answers a charge for it";
        if (c == null || !c.Powerful || c.HouseId != house.Id) return "no such charge is waiting";
        var who = w.Char(c.AccusedId); if (who is not { Alive: true } || who.HouseId != house.Id) return "there is nobody of the house to answer it";
        bool self = who.Id == head.Id; bool wilful = !self && CourtshipSystem.Wilful(who) && w.FamilyAutonomy > 0;
        if (answer == LayDown)
        {
            if (c.Sentence != LayDown) return "no court has ordered a command laid down";
            return wilful ? $"{who.Name} will not lay it down for the asking: they wait to hear whether the house will cross with them" : null;
        }
        if (answer == Cross)
        {
            if (c.Outcome.Length > 0 && c.Sentence != LayDown) return "it has been answered";
            if (CaesarSystem.GeneralOf(w, house)?.Id != who.Id) return $"{(self ? "you command" : who.Name + " commands")} none of the house's fleets: only its general can cross the line";
            return _caesar.CrossRefusal(w, house);
        }
        if (c.Outcome.Length > 0) return "it has been answered";
        if (wilful) return $"{who.Name} will answer it as they think fit: it is their name";
        if (answer == Trial) return c.Answer == Trial ? (self ? "you have said you will stand trial" : $"{who.Name} is to stand trial already") : null;
        if (answer == ExileAnswer) return Abroad(w, who) ? $"{who.Name} is abroad already" : null;
        return "no such answer";
    }

    /// <summary>The answer is given. Exile and crossing take effect at once; standing trial is said, and the charge is heard at the year's end.</summary>
    public void Answer(World w, Character head, Indictment c, string answer)
    {
        var who = w.Char(c.AccusedId)!; var house = w.House(c.HouseId)!;
        switch (answer)
        {
            case Trial: c.Answer = Trial; w.Log($"{who.Name} will stand trial on the charge {Nm(w, c.AccuserId)} brought, and says so."); break;
            case ExileAnswer: Exile(w, who, c); break;
            case LayDown: LaidDownBy(w, who, c, late: false); break;
            case Cross:
                c.Answer = Cross; _caesar.Cross(w, house);
                if (w.CivilWar?.HouseId == house.Id) Settle(w, c, house);
                break;
        }
    }

    /// <summary>A convicted commander lays the command down: the fleets are left without an admiral and they may hold none for some years.</summary>
    private static void LaidDownBy(World w, Character who, Indictment c, bool late)
    {
        LayCommandDown(w, who, "laid down at the order of the court");
        w.WorldCounters[CaesarSystem.BarredKey(who.Id)] = w.Turn + CaesarSystem.BarredYears; w.WorldCounters[CourtBarKey(who.Id)] = w.Turn + CaesarSystem.BarredYears;
        c.Sentence = LaidDown; Bump(w, LaidDownKey);
        if (late && w.House(c.HouseId) is { } h) LegitimacyBook.Move(w, h, -TakenLegitimacy, LegitimacyBook.Courts, "The court's officers took a command that was not laid down");
        LifeSystem.Note(w, who, "office", "Was convicted while in command, and laid the command down.");
        w.Log(late ? $"The court's officers take the command from {who.Name} of {Nm(w, who.HouseId)}, who had not laid it down." : $"{who.Name} of {Nm(w, who.HouseId)} lays the command down, as the court ordered.");
    }

    /// <summary>The house has crossed the line: whatever waited against its people is not heard by the Republic's courts.</summary>
    private static void Settle(World w, Indictment c, House house)
    {
        if (c.Sentence == LayDown) c.Sentence = Crossed;
        if (c.Outcome.Length == 0) { c.Outcome = Crossed; c.HeardTurn = w.Turn; }
        Bump(w, CrossedKey);
        w.Log($"The charge against {w.Char(c.AccusedId)?.Name ?? "the general"} will not be heard: {house.Name} has crossed the line.");
    }

    /// <summary>Would one of another house, or a wilful relative, go into exile rather than wait for the verdict? The upright and the
    /// loyal stand trial; the proud and the fearful go, if they expect the court to convict.</summary>
    public static bool WouldFlee(World w, Indictment c)
    {
        var who = w.Char(c.AccusedId); var d = LadderSystem.DeedOf(w, c); var judge = LadderSystem.JudgeOf(w, c); if (who == null || d == null || judge == null) return false;
        if (who.NatureTraits.Contains("Honorable") || who.NatureTraits.Contains("Just") || who.NatureTraits.Contains("Loyal")) return false;
        bool proud = who.NatureTraits.Contains("Proud") || who.NatureTraits.Contains("Arrogant") || who.NatureTraits.Contains("Vengeful") || who.AptitudeTraits.Contains("Paranoid");
        if (!proud) return false;
        int defence = LadderSystem.DefenceOf(w, LadderSystem.PleaderOf(w, c));
        var (guilty, why) = LadderSystem.Verdict(w, d, c.AccuserId, judge, defence);
        return why.StartsWith("neither", StringComparison.Ordinal) ? LadderSystem.ProvedChance(w, c.AccuserId, defence) >= 50 : guilty;
    }

    // ---------------- the threat ----------------

    /// <summary>Is an act on the record the one a standing threat names? It is not forgotten while the threat stands.</summary>
    public static bool Threatened(World w, string deedId) => w.ChargeThreats.Count > 0 && w.ChargeThreats.Any(t => t.Outcome.Length == 0 && t.DeedId == deedId);
    /// <summary>Has this house said it will charge this person the day the command is laid down, and do they still command? Then it waits.</summary>
    public static bool Waits(World w, string accuserId, string characterId) =>
        w.ChargeThreats.Count > 0 && w.ChargeThreats.Any(t => t.Outcome.Length == 0 && t.AccuserId == accuserId && t.AccusedId == characterId) && w.Char(characterId) is { } c && Commands(w, c);

    /// <summary>What the house could charge this general with: an act of theirs on the record a court would hear.</summary>
    public static OfficeDeed? ThreatDeed(World w, string accuserId, Character general) => LadderSystem.Chargeable(w, accuserId).FirstOrDefault(d => d.CharacterId == general.Id);

    public static string? ThreatRefusal(World w, Character? head, Character? general)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house speaks for it";
        if (AbroadSays(w, head, head.Id == w.ProtagonistId) is { } away) return away;
        if (w.IsImperium) return "there are no courts of the Republic";
        if (!PolitySystem.InReach(w, house.Id)) return "the courts of the Reach are not yours to use";
        if (general is not { Alive: true } || general.HouseId == house.Id) return "name a general of another house";
        if (!Commands(w, general)) return $"{general.Name} holds no command";
        if (w.ChargeThreats.Any(t => t.Outcome.Length == 0 && t.AccuserId == house.Id)) return "your house has let one such thing be known already, and has not yet made it good";
        if (w.ChargeThreats.Any(t => t.Outcome.Length == 0 && t.AccusedId == general.Id)) return $"it is known already that {general.Name} will be charged";
        return ThreatDeed(w, house.Id, general) == null ? $"there is nothing on the record against {general.Name} that a court would hear" : null;
    }

    /// <summary>The house lets it be known that the general will be charged the day the command is laid down.</summary>
    public static ChargeThreat Threaten(World w, OfficeCatalog offices, Character head, Character general)
    {
        var house = w.House(head.HouseId)!; var d = ThreatDeed(w, house.Id, general)!; var mine = w.Protagonist?.HouseId;
        var t = new ChargeThreat { Id = $"threat_{w.Turn}_{house.Id}_{general.Id}", AccuserId = house.Id, AccusedId = general.Id, HouseId = general.HouseId, DeedId = d.Id, Turn = w.Turn };
        w.ChargeThreats.Add(t); Bump(w, ThreatsKey);
        Regard(w, Head(w, general.HouseId), head, -ThreatRegard); Regard(w, general, head, -ThreatRegard);
        w.Log($"{house.Name} lets it be known that {general.Name} of {Nm(w, general.HouseId)} will be charged the day the command is laid down: {general.Name} {LadderSystem.DeedSays(w, offices, d)}.");
        if (general.HouseId == mine) w.Tell("warn", $"{house.Name} lets it be known that {(general.Id == w.ProtagonistId ? "you" : general.Name)} will be charged the day the command is laid down. It is a cause, as a recall is.", "senate:ladder", general.Id);
        return t;
    }

    // ---------------- the year ----------------

    /// <summary>Each year, after the generals' business and before the courts sit: other houses' people answer the charges against
    /// them; a convicted commander's time runs out; the threats are made good or found empty; and one house of the Reach, hating
    /// another's general enough, lets it be known that the general will be charged.</summary>
    public void Tick(World w)
    {
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        w.Exiles.RemoveAll(x => w.Char(x.CharacterId) is not { Alive: true } c || !c.Flags.Contains(ExiledChar));
        foreach (var x in w.Exiles) if (w.Char(x.CharacterId) is { } c) x.HouseId = c.HouseId;   // one who has married into another house is that house's exile now
        if (!LadderSystem.Active(w, _offices)) return;
        var mine = w.Protagonist?.HouseId;
        foreach (var c in w.Indictments.Where(c => c.Powerful).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            var who = w.Char(c.AccusedId); var house = w.House(c.HouseId); if (who is not { Alive: true } || house == null) continue;
            bool marching = w.CivilWar?.HouseId == house.Id;
            if (c.Outcome.Length == 0 && c.Turn < w.Turn)
            {
                if (marching) { Settle(w, c, house); continue; }
                bool ours = house.Id == mine; bool wilful = ours && who.Id != w.ProtagonistId && CourtshipSystem.Wilful(who) && w.FamilyAutonomy > 0;
                if (ours && !wilful) continue;   // the player's answer, or the head's for a relative: standing trial unless they say otherwise
                if (c.Answer.Length == 0 && WouldFlee(w, c)) Exile(w, who, c); else if (c.Answer.Length == 0) c.Answer = Trial;
            }
            else if (c.Sentence == LayDown)
            {
                if (marching) { Settle(w, c, house); continue; }
                if (!Commands(w, who)) { c.Sentence = LaidDown; continue; }
                bool ours = house.Id == mine;
                if (!ours) LaidDownBy(w, who, c, late: false);   // had they meant to cross, they would have crossed with the year's generals
                else if (who.Id != w.ProtagonistId && w.FamilyAutonomy > 0 && !CourtshipSystem.Wilful(who)) LaidDownBy(w, who, c, late: false);   // it is the court's order, and they keep the law
                else if (w.Turn >= c.AnswerBy) LaidDownBy(w, who, c, late: true);
            }
        }
        Threats(w, mine);
    }

    private void Threats(World w, string? mine)
    {
        foreach (var t in w.ChargeThreats.Where(t => t.Outcome.Length == 0).OrderBy(t => t.Id, StringComparer.Ordinal).ToList())
        {
            var g = w.Char(t.AccusedId); var accuser = w.House(t.AccuserId); var head = accuser != null ? Economy.HeadOf(w, accuser) : null;
            if (g is not { Alive: true } || accuser == null) { t.Outcome = Lapsed; Bump(w, ThreatsLapsedKey); continue; }
            var d = w.OfficeRecord.FirstOrDefault(x => x.Id == t.DeedId);
            if (d != null && (d.Answered || LadderSystem.UnderCharge(w, d.Id))) { t.Outcome = Lapsed; Bump(w, ThreatsLapsedKey); continue; }   // another answered for it first
            bool commands = Commands(w, g);
            if (d != null && commands) continue;
            if (d != null && t.LaidDownTurn == 0)
            {
                t.LaidDownTurn = w.Turn;
                if (t.AccuserId == mine) w.Tell("warn", $"{g.Name} has laid the command down. You let it be known they would be charged that day: bring the charge by the end of year {w.Turn + ThreatGraceYears}, or your house will be thought to threaten what it will not do.", "senate:ladder", g.Id);
            }
            if (d != null && t.AccuserId != mine && head != null && LadderSystem.ProsecuteRefusal(w, head, d) == null)
            {
                if (Powerful(w, d)) LadderSystem.Indict(w, _offices, head, d); else LadderSystem.Prosecute(w, _offices, head, d);
                continue;
            }
            if (d != null && w.Turn <= t.LaidDownTurn + ThreatGraceYears) continue;
            t.Outcome = Empty; Bump(w, ThreatsEmptyKey);
            LegitimacyBook.Move(w, accuser, -ThreatEmptyLegitimacy, LegitimacyBook.Courts, "It let it be known a general would be charged, and never charged them");
            w.Log($"{accuser.Name} said {g.Name} would be charged the day the command was laid down. Nothing was ever brought, and it is remembered.");
            if (t.AccuserId == mine) w.Tell("warn", $"Your house said {g.Name} would be charged, and never brought it: {ThreatEmptyLegitimacy} legitimacy lost.", "senate:ladder", g.Id);
        }
        // one house in the whole Reach a year lets such a thing be known: one whose head hates another house enough to charge its people,
        // about one of the powerful of that house who commands and has something on the record, the one of greatest sway first. A house
        // would rather not charge a powerful commander while the fleet is theirs: it says it will wait
        if (w.ChargeThreats.Any(t => t.Turn == w.Turn)) return;
        var commanders = w.Fleets.Values.Where(f => f.AdmiralId.Length > 0).Select(f => w.Char(f.AdmiralId)).Concat(w.Service.Select(s => w.Char(s.CommanderId)))
            .Where(c => c is { Alive: true } && !World.Outsider(c.HouseId) && w.OfficeRecord.Any(d => d.CharacterId == c.Id && !d.Answered) && Powerful(w, c)).Select(c => c!).Distinct()
            .OrderByDescending(c => CaesarSystem.Sway(w, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (commanders.Count == 0) return;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var head = Economy.HeadOf(w, h); if (head == null || Abroad(w, head)) continue;
            foreach (var g in commanders)
            {
                var oh = Head(w, g.HouseId); if (g.HouseId == h.Id || oh == null || (w.GetRelationship(head.Id, oh.Id)?.Disposition ?? 0) > LadderSystem.ProsecuteAt || ThreatRefusal(w, head, g) != null) continue;
                Threaten(w, _offices, head, g); return;
            }
        }
    }
}
