using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// A triumph is had at the gate (docs/CAESAR.md, "The triumph"; docs/IDEAS_FROM_IMPERATOR.md, entry 1). The Senate's vote does not
/// give a triumph: it offers one. The general must come home and lay the command down for a year to have it, while the fleet sits
/// under its captain (or unled, if it has none) and the crews' devotion to them does not fade; when the year is out they take the
/// command up again, if it is still theirs. Or they decline it and keep the fleet: that is their own choice, and it is no cause.
/// Other houses' generals, and the player's own relatives, choose by their nature and the war they are in; the player is asked.
/// <para>And while someone commands a fleet away from the capital they cannot stand for a magistracy or the consulship, unless the
/// Senate has given their house <b>leave to stand in absence</b>: a motion like the others, which lasts for one election. Both the
/// triumph and the leave may be moved by anyone who has held a magistracy, whatever door their house sits by (docs/GAPS.md Q15).</para>
/// RNG-free.
/// </summary>
public static class TriumphSystem
{
    public const string Home = "home", Keep = "keep", Lapsed = "lapsed", Void = "void", TakenUp = "taken_up";
    /// <summary>Not a triumph: one of another house who came home to stand for office, whose fleet waits for them the same way.</summary>
    public const string Stand = "stand";
    /// <summary>The motion kind (content/senate/motions.json).</summary>
    public const string LeaveKind = "absence", TriumphKind = "triumph";
    /// <summary>How long the command is laid down; how many finished triumphs are kept for the record; the regard a friend who moves
    /// leave for a house without a voice must have for its head.</summary>
    public const int HomeYears = 1, KeptDeep = 40, FriendRegard = 30, LeaveSway = 20;
    // each year while it holds (the player's house)
    public const string AskedFlag = "triumph_at_the_gate", HomeFlag = "home_for_a_triumph", LeaveFlag = "leave_to_stand_in_absence";
    // for a year after the deed (the player's house)
    public const string DeclinedFlag = "triumph_declined", KinDeclinedFlag = "kin_declined_triumph", TakenUpFlag = "command_taken_up";
    public const string HomeChar = "home_for_a_triumph";
    // counters
    public const string HadKey = "triumphs_had", DeclinedKey = "triumphs_declined", LapsedKey = "triumphs_lapsed", TakenUpKey = "commands_taken_up", NotTakenUpKey = "commands_not_taken_up",
        LeaveMovedKey = "absence_moved", LeaveGrantedKey = "absence_granted", LeaveRefusedKey = "absence_refused", LeaveVetoedKey = "absence_vetoed",
        ElectedAwayKey = "elected_in_absence", LeaveSpentKey = "absence_spent", NamesDroppedKey = "names_taken_down_away", CameHomeKey = "came_home_to_stand",
        StoodTakenUpKey = "stood_and_took_up", StoodNotTakenUpKey = "stood_and_did_not_take_up", UnderRecallKey = "triumphs_void_under_recall";
    /// <summary>One of another house who laid a command down to stand for office: the year after which they may take another.</summary>
    public static string StandingKey(string characterId) => "home_to_stand:" + characterId;
    public static bool StandingHome(World w, Character c) => w.Counter(StandingKey(c.Id)) > w.Turn;
    /// <summary>The year the Senate gave a house leave to stand in absence; gone when the next election has been held.</summary>
    public static string LeaveKey(string houseId) => "absence_leave:" + houseId;
    private static readonly string[] YearFlags = { AskedFlag, HomeFlag, LeaveFlag };
    private static readonly string[] DeedFlags = { DeclinedFlag, KinDeclinedFlag, TakenUpFlag };
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Count(World w, string key, int by = 1) => w.WorldCounters[key] = w.Counter(key) + by;
    private static string Nm(World w, string houseId) => w.House(houseId)?.Name ?? houseId;

    // ---------------- the triumph at the gate ----------------

    /// <summary>A triumph voted to this person and not yet answered.</summary>
    public static TriumphVote? Waiting(World w, string characterId) => w.TriumphVotes.FirstOrDefault(t => !t.Done && t.Choice.Length == 0 && t.CharacterId == characterId);
    /// <summary>The year at home of one who came home for a triumph, while it lasts.</summary>
    public static TriumphVote? AtHome(World w, string characterId) => w.TriumphVotes.FirstOrDefault(t => !t.Done && t.Choice == Home && t.CharacterId == characterId);
    /// <summary>The triumph a fleet is held for: its general is home for it, still living and of the house, and nobody else has been given the
    /// fleet meanwhile. One who dies or leaves the house while home has nothing waiting for them, and the fleet is the house's to fill.</summary>
    public static TriumphVote? HeldBy(World w, Fleet f) => w.TriumphVotes.Count == 0 || f.AdmiralId.Length > 0 ? null
        : w.TriumphVotes.FirstOrDefault(t => !t.Done && t.Choice is Home or Stand && t.FleetIds.Contains(f.Id) && w.Char(t.CharacterId) is { Alive: true } g && g.HouseId == t.HouseId);
    /// <summary>Is the fleet held for a general home for a triumph? Then nobody is given it unasked, and its crews' devotion does not fade.</summary>
    public static bool Held(World w, Fleet f) => HeldBy(w, f) != null;
    public static bool HeldFor(World w, Fleet f, string characterId) => HeldBy(w, f)?.CharacterId == characterId;
    /// <summary>Is this person under the Senate's recall? Then they come home at its order (<see cref="CaesarSystem.Obey"/>), not to a triumph
    /// or to stand for office.</summary>
    public static bool UnderRecall(World w, string characterId) => w.Recalls.Any(r => r.CharacterId == characterId);
    /// <summary>Why one home for a triumph may hold no command now, or null: said of "you" on the player's own screen, else by name. Null too
    /// when a longer bar (a recall's) holds them besides, which says its own reason.</summary>
    public static string? HomeRefusal(World w, Character c) => AtHome(w, c.Id) is { } t && w.Counter(CaesarSystem.BarredKey(c.Id)) <= t.HomeUntil + 1
        ? (c.Id == w.ProtagonistId ? "you are home for the triumph, and hold" : c.Name + " is home for the triumph, and holds") + $" no command until the end of year {t.HomeUntil}" : null;

    /// <summary>The Senate has voted a general a triumph. It is had at the gate, so the general's answer is wanted.</summary>
    public static void Voted(World w, House house, Character g, House? proposer)
    {
        foreach (var old in w.TriumphVotes.Where(t => !t.Done && t.Choice.Length == 0 && t.CharacterId == g.Id)) { old.Choice = Void; old.Done = true; }
        w.TriumphVotes.Add(new TriumphVote { CharacterId = g.Id, Name = g.Name, HouseId = house.Id, ProposerHouseId = proposer?.Id ?? "", Turn = w.Turn });
        w.Log($"The Senate votes {g.Name} of {house.Name} a triumph. It is had at the gate: {g.Name} must come home and lay the command down for a year to have it, or let it go and keep the fleet.");
        if (g.Id == w.ProtagonistId)
            w.Tell("good", $"The Senate has voted you a triumph. It is had at the gate: come home and lay your command down for {Counted.Years(HomeYears)} to have it, or decline it and keep the fleet. Answer it this year, or it is taken as declined.", "military", g.Id);
        Prune(w);
    }

    /// <summary>What it would mean to come home, said before it is chosen.</summary>
    public static string HomeSays(World w, Character g)
    {
        var fleets = w.Fleets.Values.Where(f => f.AdmiralId == g.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        string laid = fleets.Count == 0 ? "You command nothing now, so nothing is laid down." : string.Join("; ", fleets.Select(f => $"{f.Name} waits {(FleetSystem.CaptainOf(w, f) is { } cap ? "under its captain, " + cap.Name : "with nobody in command")}")) + ".";
        return $"The house gains 6 legitimacy and glory, the crews march behind you and think the more of you for it, and it counts toward the first rank. You lay the command down until the end of year {w.Turn + HomeYears}: {laid} The crews' regard for you does not fade meanwhile, and you take the command up again when the year is out if it is still yours. Home, you may stand for office in person.";
    }
    public static string KeepSays => "You keep the fleet, and nothing is given: no triumph is counted, and the victories that earned it have had their answer. It is your own choice, and the Senate takes no offence.";

    public static string? ChoiceRefusal(World w, Character? who, string choice)
    {
        if (who is not { Alive: true }) return "no one to answer";
        if (choice is not (Home or Keep)) return "come home for it, or keep the fleet";
        if (Waiting(w, who.Id) == null) return "the Senate has voted you no triumph to answer";
        return choice == Home && UnderRecall(w, who.Id) ? "the Senate has since recalled you, and one under recall comes home at its order, not to a triumph" : null;
    }

    /// <summary>The general comes home for the triumph: the honours are given while the crews still have their commander to march behind,
    /// and the command is laid down for a year. Nobody else is given the fleet meanwhile (it is held), and the general takes no other.</summary>
    public static void ComeHome(World w, CaesarSystem caesar, TriumphVote t, string why = "")
    {
        var g = w.Char(t.CharacterId); var house = w.House(t.HouseId); var mine = w.Protagonist?.HouseId;
        if (g is not { Alive: true } || house == null) { t.Choice = Void; t.Done = true; return; }
        caesar.TriumphVoted(w, house, w.House(t.ProposerHouseId), passed: true, atTheGate: g);
        t.Choice = Home; t.Why = why; t.HomeSince = w.Turn; t.HomeUntil = w.Turn + HomeYears;
        var laid = w.Fleets.Values.Where(f => f.AdmiralId == g.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        foreach (var f in laid) { t.FleetIds.Add(f.Id); f.AdmiralId = ""; }
        FleetSystem.RefreshAdmiralFlags(w);
        string bar = CaesarSystem.BarredKey(g.Id); w.WorldCounters[bar] = Math.Max(w.Counter(bar), t.HomeUntil + 1);   // no other command while home
        g.Flags.Add(HomeChar); Count(w, HadKey);
        string names = laid.Count == 0 ? "" : $" {string.Join(" and ", laid.Select(f => f.Name))} {(laid.Count == 1 ? "waits" : "wait")} for {g.Name} until the end of year {t.HomeUntil}.";
        w.Log($"{g.Name} of {house.Name} comes home and lays the command down to have the triumph.{names}");
        TellOwner(w, g, laid, $"to have the triumph the Senate voted, until the end of year {t.HomeUntil}");
        if (house.Id == mine && g.Id != w.ProtagonistId)
            w.Tell("good", $"{g.Name} comes home for the triumph the Senate voted{(why.Length > 0 ? " (" + why + ")" : "")}, and lays the command down until the end of year {t.HomeUntil}.", "military", g.Id);
    }

    /// <summary>One of another house who commands a fleet of the player's house by service has laid it down to come home: the player is told,
    /// since the fleet waits for them meanwhile and nobody of the house steps into the empty flag unasked.</summary>
    private static void TellOwner(World w, Character g, List<Fleet> laid, string why)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null || g.HouseId == mine) return;
        foreach (var f in laid.Where(f => f.OwnerId == mine))
            w.Tell("warn", $"{g.Name} of {Nm(w, g.HouseId)}, who commands {f.Name} for your house by service, lays the command down {why}. {f.Name} waits for them "
                + (FleetSystem.CaptainOf(w, f) is { } cap ? "under its captain, " + cap.Name : "with nobody in command") + "; give it to another, and it is theirs.", "military:fleets", g.Id);
    }

    /// <summary>The general lets the triumph go and keeps the fleet; or (<see cref="Lapsed"/>) the year ran out without an answer, which is
    /// the same. It is the general's own choice and no cause. The victories that earned it were counted off when it was voted.</summary>
    public static void Decline(World w, TriumphVote t, string choice, string why = "")
    {
        t.Choice = choice; t.Why = why; t.Done = true;
        var g = w.Char(t.CharacterId); var mine = w.Protagonist?.HouseId; string house = Nm(w, t.HouseId);
        Count(w, choice == Lapsed ? LapsedKey : DeclinedKey);
        if (g is { Alive: true }) LifeSystem.Note(w, g, "triumph_declined", choice == Lapsed ? "Was voted a triumph by the Senate, and did not come home to have it." : "Was voted a triumph by the Senate, and declined it to keep the command.");
        if (t.HouseId == mine) Mark(w, t.CharacterId == w.ProtagonistId ? DeclinedFlag : KinDeclinedFlag);
        w.Log(choice == Lapsed ? $"{t.Name} of {house} did not come home for the triumph the Senate voted, and it lapses."
            : $"{t.Name} of {house} declines the triumph the Senate voted, and stays with the fleet{(why.Length > 0 ? ": " + why : "")}.");
        if (t.HouseId == mine && t.CharacterId != w.ProtagonistId)
            w.Tell("info", $"{t.Name} declines the triumph the Senate voted, and keeps the fleet{(why.Length > 0 ? ": " + why : "")}.", "military", t.CharacterId);
    }

    /// <summary>The general voted a triumph has been recalled before answering it: the triumph is void, and the recall is answered as any other
    /// (the general comes home at the Senate's order and may hold no command for five years). Coming home for the triumph is no way out of it.</summary>
    private static void VoidUnderRecall(World w, TriumphVote t, Character g)
    {
        t.Choice = Void; t.Why = "recalled"; t.Done = true; Count(w, UnderRecallKey);
        w.Log($"The Senate has recalled {g.Name} of {Nm(w, t.HouseId)}, and the triumph it voted is void: one under recall comes home at the Senate's order.");
        if (t.HouseId == w.Protagonist?.HouseId)
            w.Tell("warn", $"The Senate has recalled {(g.Id == w.ProtagonistId ? "you" : g.Name)}, and the triumph it voted is void: one under recall comes home at its order, not to a triumph.", "military", g.Id);
    }

    /// <summary>Why a general would decline the triumph to keep the fleet (positive) or come home for it (negative): by their nature, and by
    /// the war they are in. They keep the fleet when the sum is above nought.</summary>
    public static List<(string why, int by)> Weigh(World w, OfficeCatalog offices, Character g)
    {
        var list = new List<(string why, int by)>(); void Add(int by, string why) { if (by != 0) list.Add((why, by)); }
        var fleets = w.Fleets.Values.Where(f => f.AdmiralId == g.Id).ToList(); var owners = fleets.Select(f => f.OwnerId).Distinct().ToList();
        var wars = w.Wars.Where(x => owners.Any(x.Involves)).ToList();
        if (fleets.Any(f => f.IsAway)) Add(2, "the fleet is at war beyond the Reach");   // (an expedition is a war abroad)
        else if (wars.Count > 0) Add(2, "the fleet is at war");
        if (wars.Any(x => owners.Where(x.Involves).Any(o => x.Lead(o) < 0))) Add(1, "and the war is going badly");
        Add(g.Ambition switch { "seize_the_throne" => 2, "command_a_fleet" or "earn_a_command" => 1, "outshine_all_rivals" or "master_the_senate" => -2, _ => 0 },
            g.Ambition is "seize_the_throne" ? "the crews are the road to what they want" : g.Ambition is "outshine_all_rivals" or "master_the_senate" ? "the day in the capital is what they want" : "the command is what they want");
        if (g.NatureTraits.Contains("Loyal") || g.NatureTraits.Contains("Honorable") || g.NatureTraits.Contains("Just")) Add(-1, "they keep the Senate's custom");
        if (g.NatureTraits.Contains("Proud") || g.NatureTraits.Contains("Arrogant") || g.NatureTraits.Contains("Ambitious")) Add(-1, "they want to be seen");
        if (g.NatureTraits.Contains("Ruthless") || g.NatureTraits.Contains("Cruel") || g.NatureTraits.Contains("Paranoid")) Add(1, "what is let go may not come back");
        if (ElectionNear(w, offices) && CouldStand(w, offices, g)) Add(-1, "an election is near and the triumph brings them home to stand");
        return list;
    }
    public static bool WouldKeep(World w, OfficeCatalog offices, Character g) => Weigh(w, offices, g).Sum(x => x.by) > 0;

    /// <summary>The year at home is out: the general takes up again whatever of the command is still theirs.</summary>
    private static void TakeUp(World w, TriumphVote t)
    {
        var g = w.Char(t.CharacterId); var mine = w.Protagonist?.HouseId; t.Done = true;
        if (g != null) { g.Flags.Remove(HomeChar); string bar = CaesarSystem.BarredKey(g.Id); if (w.Counter(bar) == t.HomeUntil + 1) w.WorldCounters.Remove(bar); }
        var back = new List<string>(); var not = new List<string>();
        foreach (var id in t.FleetIds)
        {
            if (NotTheirs(w, g, t, id) is { } no) { not.Add(no); continue; }
            w.Fleets[id].AdmiralId = g!.Id; back.Add(w.Fleets[id].Name);
        }
        FleetSystem.RefreshAdmiralFlags(w);
        t.Ended = back.Count > 0 ? TakenUp : not.FirstOrDefault() ?? "there was no command to take up";
        if (t.Choice == Stand) { Count(w, back.Count > 0 ? StoodTakenUpKey : StoodNotTakenUpKey); if (back.Count > 0 && g != null) w.Log($"The count is past: {g.Name} takes up the command of {string.Join(" and ", back)} again."); return; }
        Count(w, back.Count > 0 ? TakenUpKey : NotTakenUpKey);
        if (g is not { Alive: true }) return;
        w.Log(back.Count > 0 ? $"The year is out: {g.Name} takes up the command of {string.Join(" and ", back)} again." : $"The year is out, and {g.Name} has no command to take up: {t.Ended}.");
        if (t.HouseId == mine)
        {
            if (back.Count > 0) Mark(w, TakenUpFlag);
            w.Tell(back.Count > 0 ? "good" : "info", back.Count > 0 ? $"{(g.Id == w.ProtagonistId ? "You take" : g.Name + " takes")} up the command of {string.Join(" and ", back)} again, the year at home being out."
                : $"The year at home is out, and {(g.Id == w.ProtagonistId ? "you have" : g.Name + " has")} no command to take up: {t.Ended}.", "military", g.Id);
        }
    }

    /// <summary>Why the general may not take this fleet up again when the year at home is out, or null. A command by service lapses by its own
    /// rules meanwhile; a fleet given to somebody else is theirs now.</summary>
    public static string? NotTheirs(World w, Character? g, TriumphVote t, string fleetId)
    {
        if (g is not { Alive: true }) return $"{t.Name} is dead";
        if (!w.Fleets.TryGetValue(fleetId, out var f) || f.Strength <= 0) return "the fleet is no more";
        if (f.AdmiralId.Length > 0) return f.AdmiralId == g.Id ? null : $"{f.Name} was given to {w.Char(f.AdmiralId)?.Name ?? "another"} meanwhile";
        if (g.HouseId != t.HouseId) return $"{g.Name} is no longer of {Nm(w, t.HouseId)}";
        if (g.Flags.Contains(LifeSystem.FrailChar)) return $"{g.Name} is too frail for the sea";
        if (g.Flags.Contains(LifeSystem.RetiredChar)) return $"{g.Name} has laid their work down";
        if (CaesarSystem.Barred(w, g)) return "the Senate had them lay a command down";
        bool own = f.OwnerId == g.HouseId, served = w.Service.Any(s => s.CommanderId == g.Id && s.FleetId == f.Id);
        if (!own && !served) return t.HouseId == f.OwnerId ? $"{f.Name} has passed to {Nm(w, f.OwnerId)}" : "the command by service has lapsed meanwhile";
        return f.LaidUp ? $"{f.Name} is laid up" : null;
    }

    // ---------------- standing in absence ----------------

    /// <summary>A fleet this person commands that is not lying at the capital: under way, beyond the Reach, or anywhere else. Null when the
    /// game has no capital.</summary>
    public static Fleet? AwayCommand(World w, Character c)
    {
        var cap = w.Galaxy?.Capital ?? ""; if (cap.Length == 0 || w.Fleets.Count == 0) return null;
        return w.Fleets.Values.Where(f => f.AdmiralId == c.Id && (f.IsAway || f.Moving || f.SystemId != cap)).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    public static bool HasLeave(World w, string houseId) => w.WorldCounters.ContainsKey(LeaveKey(houseId));
    /// <summary>Why this person may not stand for office while they command away from the capital, or null: said as the ladder says its
    /// bars, of "they", with the ways out there are (a fleet of the house's own is brought to the capital or given to another; a command by
    /// service is resigned).</summary>
    public static string? AwayRefusal(World w, Character c) => AwayCommand(w, c) is { } f && !HasLeave(w, c.HouseId)
        ? $"they command {f.Name} away from the capital: {(f.OwnerId == c.HouseId ? "bring it to the capital, give the command to another" : "resign the command")}, or ask the Senate for leave to stand in absence" : null;
    /// <summary>The same, said of one person by name, or of "you".</summary>
    public static string? AwaySays(World w, Character c) => AwayRefusal(w, c) is { } no ? (c.Id == w.ProtagonistId ? "you command" : c.Name + " commands") + no["they command".Length..] : null;
    public static bool MayStand(World w, Character c) => AwayRefusal(w, c) == null;

    /// <summary>Why the house could not stand for consul when it would but for one who commands away, or null: null too when it is barred
    /// besides (a consul of the last term, an enemy of the Reach), which says its own reason.</summary>
    public static string? ConsulAway(World w, OfficeCatalog offices, string houseId) =>
        ElectionSystem.ConsulOffices(offices).FirstOrDefault() is { } d && OfficeSystem.Candidate(w, d, houseId) == null && OfficeSystem.Candidate(w, d, houseId, evenAway: true) is { } c
        && ConsulTermOk(w, houseId) && !CaesarSystem.Enemy(w, houseId) && PolitySystem.InReach(w, houseId) ? AwaySays(w, c) : null;
    /// <summary>One the house would put up for consul but for their command away; the player's head first, as in the count.</summary>
    public static Character? ConsulAwayOne(World w, OfficeCatalog offices, string houseId) =>
        ElectionSystem.ConsulOffices(offices).FirstOrDefault() is { } d && OfficeSystem.Candidate(w, d, houseId, evenAway: true) is { } c && !MayStand(w, c) ? c : null;

    /// <summary>Is the count for the ladder or the consulship at the end of next year or the year after? Read at the year's end, when this
    /// year's count, if there was one, has been held.</summary>
    public static bool ElectionNear(World w, OfficeCatalog offices) => !w.IsImperium && (LadderSystem.Active(w, offices) && LadderSystem.CountIn(w, offices) is 1 or 2 || ElectionSystem.ConsulOffices(offices).Any() && ElectionSystem.CountIn(w) is 1 or 2);
    /// <summary>Could this person stand for anything, if they were at the capital?</summary>
    public static bool CouldStand(World w, OfficeCatalog offices, Character g)
    {
        string? away = AwayRefusal(w, g);
        if (LadderSystem.Active(w, offices) && offices.Rungs.Any(r => LadderSystem.Bar(w, offices, g, r) == away)) return true;
        return ElectionSystem.ConsulOffices(offices).FirstOrDefault() is { } d && OfficeSystem.Candidate(w, d, g.HouseId, evenAway: true)?.Id == g.Id && ConsulTermOk(w, g.HouseId) && !CaesarSystem.Enemy(w, g.HouseId);
    }
    private static bool ConsulTermOk(World w, string houseId) => w.Counter("consul_term:" + houseId) == 0 || w.Turn - w.Counter("consul_term:" + houseId) >= ElectionSystem.Every * 2 - 1;

    /// <summary>Why the Senate may not be asked for leave for this house, or null.</summary>
    public static string? LeaveRefusal(World w, House? target, string? byHouseId = null)
    {
        if (target == null || World.Outsider(target.Id)) return "no such house";
        bool yours = target.Id == byHouseId; string of = yours ? "your house" : target.Name;
        if (w.IsImperium) return "there are no elections under a throne";
        if (!PolitySystem.InReach(w, target.Id)) return $"{target.Name} is not of the Reach";
        if (HasLeave(w, target.Id)) return $"{(yours ? "your house has" : target.Name + " has")} leave already, until the next election";
        return w.LivingCharacters().Any(c => c.HouseId == target.Id && AwayCommand(w, c) != null) ? null : $"nobody of {of} commands a fleet away from the capital";
    }

    /// <summary>The generals' business, a triumph and leave to stand in absence, may be moved by anyone who has held a magistracy,
    /// whatever door their house sits by (docs/GAPS.md Q15).</summary>
    public static bool MayMove(World w, Character? mover, MotionDef? def) =>
        mover is { Alive: true } && def != null && def.Interest is TriumphKind or LeaveKind && !w.IsImperium && PolitySystem.InReach(w, mover.HouseId) && HeldMagistracy(w, mover);
    public static bool HeldMagistracy(World w, Character c) => c.Cursus.Count > 0 || LadderSystem.Seat(w, c.Id) != null;
    /// <summary>The refusal for a house with no voice that would move the generals' business when the mover has held no magistracy, or null:
    /// for any other motion, for one who has held one (the door is shut to them for another reason, said by the caller), and under a throne,
    /// where the door is not opened to anyone.</summary>
    public static string? NoVoice(World w, Character mover, MotionDef def) => def.Interest is TriumphKind or LeaveKind && !w.IsImperium && !HeldMagistracy(w, mover)
        ? "the house has its votes but no voice in the Senate, and you have held no magistracy: one who has held a magistracy may move it whatever the house's door" : null;

    /// <summary>How a house leans on leave for another to stand in absence: for a friend, a fellow of its bench, and (the Sword) a general
    /// in office; against from enmity, the Optimates from the law kept, and the Senate's guardians against a name that is feared.</summary>
    public static int LeaveLean(World w, House h, House target, int toTarget, OfficeCatalog offices)
    {
        if (h.Id == target.Id) return 4;
        int lean = toTarget >= 20 ? 2 : toTarget <= -20 ? -2 : 0;
        var mine = FactionSystem.Of(w, h.Id); var theirs = FactionSystem.Of(w, target.Id);
        if (mine != null && theirs != null && mine.Id == theirs.Id) lean += 1;
        lean += mine?.PartyId switch { "sword" => 1, "optimates" => -1, _ => 0 };
        int sway = w.LivingCharacters().Where(c => c.HouseId == target.Id && AwayCommand(w, c) != null).Select(c => CaesarSystem.Sway(w, c)).DefaultIfEmpty(0).Max();
        if (sway >= CaesarSystem.FearedSway && (RegionSystem.CapitalHolder(w)?.Id == h.Id || ElectionSystem.IsConsul(w, offices, h.Id))) lean -= 2;   // one who would hold office and keep the fleet
        else if (sway >= CaesarSystem.AlarmSway) lean -= 1;
        if (h.PatronId == target.Id) lean += 2;
        return lean;
    }

    /// <summary>The Senate has voted on leave for a house to stand in absence. Carried, it lasts until the next election has been held.</summary>
    public static void LeaveVoted(World w, House target, bool passed)
    {
        var mine = w.Protagonist?.HouseId; Count(w, passed ? LeaveGrantedKey : LeaveRefusedKey);
        if (passed) w.WorldCounters[LeaveKey(target.Id)] = w.Turn;
        if (target.Id != mine) return;
        w.Tell(passed ? "good" : "warn", passed ? "The Senate gives your house leave to stand in absence: at the next election those of the house who command away from the capital may stand without coming home."
            : "The Senate will not give your house leave to stand in absence. Those of the house who command away from the capital must bring the fleet to the capital, or give the command up, to stand.", "senate:ladder");
    }

    /// <summary>Another house asks leave for one of its own who would stand at an election within two years and commands away: the one it
    /// would put up for consul, if it would lead its bench's nominees; or, for a rung of the ladder, its general, when the general has a name
    /// (sway of <see cref="LeaveSway"/> or more) and is better than whoever else it has for the rung. The rest are not worth the Senate's time.</summary>
    public static Character? WantsLeave(World w, SenateSystem senate, House h, bool ladder, bool consul)
    {
        var offices = senate.Offices;
        if (consul && ConsulAwayOne(w, offices, h.Id) is { } c && ConsulTermOk(w, h.Id) && !CaesarSystem.Enemy(w, h.Id)
            && (FactionSystem.Of(w, h.Id) is not { } bench || bench.Members.Where(id => id == h.Id || ElectionSystem.Eligible(w, offices, id)).OrderByDescending(id => senate.Weight(w, id)).ThenBy(id => id, StringComparer.Ordinal).First() == h.Id))
            return c;
        if (!ladder) return null;
        if (CaesarSystem.GeneralOf(w, h) is not { } general || CaesarSystem.Sway(w, general) < LeaveSway || AwayCommand(w, general) == null) return null;
        var people = w.LivingCharacters().Where(x => x.HouseId == h.Id).ToList(); var away = new List<Character> { general };
        foreach (var rung in offices.Rungs.Reverse())
        {
            var best = away.Where(x => AwayRefusal(w, x) is { } no && LadderSystem.Bar(w, offices, x, rung) == no).OrderByDescending(LadderSystem.Merit).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best == null) continue;
            int present = people.Where(x => LadderSystem.Bar(w, offices, x, rung) == null).Select(LadderSystem.Merit).DefaultIfEmpty(-1).Max();
            if (LadderSystem.Merit(best) > present) return best;
        }
        return null;
    }
    /// <summary>Is a fleet this person commands at war: beyond the Reach, or of a house at war?</summary>
    public static bool AtWar(World w, Character c) => w.Fleets.Values.Any(f => f.AdmiralId == c.Id && (f.IsAway || w.Wars.Any(x => x.Involves(f.OwnerId))));

    /// <summary>Who of another house would stand at the count at the end of next year but for a command away: the one it would put up for
    /// consul, if it would be its bench's nominee and has nobody else who may stand, and for each rung of the ladder the best it has away,
    /// when it has nobody else for the rung who may stand (or, <paramref name="better"/>, when that one is better than anyone it has who may).</summary>
    public static List<Character> AwayCandidates(World w, SenateSystem senate, House h, bool ladder, bool consul, bool better = false)
    {
        var offices = senate.Offices; var list = new List<Character>();
        if (consul && (better || ConsulAway(w, offices, h.Id) != null) && ConsulAwayOne(w, offices, h.Id) is { } c && ConsulTermOk(w, h.Id) && !CaesarSystem.Enemy(w, h.Id)
            && (FactionSystem.Of(w, h.Id) is not { } bench || bench.Members.Where(id => id == h.Id || ElectionSystem.Eligible(w, offices, id)).OrderByDescending(id => senate.Weight(w, id)).ThenBy(id => id, StringComparer.Ordinal).First() == h.Id))
            list.Add(c);
        if (!ladder) return list;
        var people = w.LivingCharacters().Where(x => x.HouseId == h.Id).ToList(); var away = people.Where(x => AwayCommand(w, x) != null).ToList();
        if (away.Count == 0) return list;
        foreach (var rung in offices.Rungs.Reverse())
        {
            var best = away.Where(x => AwayRefusal(w, x) is { } no && LadderSystem.Bar(w, offices, x, rung) == no).OrderByDescending(LadderSystem.Merit).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best == null || list.Contains(best)) continue;
            int present = people.Where(x => LadderSystem.Bar(w, offices, x, rung) == null).Select(LadderSystem.Merit).DefaultIfEmpty(-1).Max();
            if (better ? LadderSystem.Merit(best) > present : present < 0) list.Add(best);
        }
        return list;
    }

    /// <summary>The year before a count, one of another house who commands away and is the only one it has who could stand for a rung (or for
    /// consul), with no leave and none asked, comes home to stand: they lay the command down, unless the fleet is at war, and take no other until
    /// the count is past. The fleet waits for them as it does for a general home for a triumph (held, its crews' regard not fading), and they
    /// take it up again when the count is past. Without it the ladder goes short of candidates, since most of the Reach's officers command somewhere.</summary>
    private static void ComeHomeToStand(World w, SenateSystem senate)
    {
        var offices = senate.Offices; var mine = w.Protagonist?.HouseId; if (w.IsImperium) return;
        bool ladder = LadderSystem.Active(w, offices) && LadderSystem.CountIn(w, offices) == 1, consul = ElectionSystem.ConsulOffices(offices).Any() && w.Emergency == null && ElectionSystem.CountIn(w) == 1;
        if (!ladder && !consul) return;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && !HasLeave(w, h.Id) && !w.Motions.Any(m => m.Open && m.Kind == LeaveKind && m.TargetId == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
            foreach (var p in AwayCandidates(w, senate, h, ladder, consul).Where(p => !AtWar(w, p) && !UnderRecall(w, p.Id)))   // one under recall comes home at the Senate's order
            {
                var laid = w.Fleets.Values.Where(f => f.AdmiralId == p.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList(); foreach (var f in laid) f.AdmiralId = "";   // a command by service waits too, and lapses only by its own rules
                FleetSystem.RefreshAdmiralFlags(w);
                w.TriumphVotes.Add(new TriumphVote { CharacterId = p.Id, Name = p.Name, HouseId = h.Id, Turn = w.Turn, Choice = Stand, Why = "came home to stand for office", HomeSince = w.Turn, HomeUntil = w.Turn + 1, FleetIds = laid.Select(f => f.Id).ToList() });
                w.WorldCounters[StandingKey(p.Id)] = w.Turn + 2; Count(w, CameHomeKey);
                if (laid.Count > 0) w.Log($"{p.Name} of {h.Name} lays down the command of {string.Join(" and ", laid.Select(f => f.Name))} and comes home to stand for office.");
                TellOwner(w, p, laid, $"to come home and stand for office, until the count at the end of year {w.Turn + 1} is past");
            }
    }

    private static bool MayBring(World w, SenateSystem senate, House h, MotionDef def) =>
        !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) is { } head && (senate.MayPropose(w, h.Id) || MayMove(w, head, def))
        && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id) && EdictSystem.Cowed(w, h.Id) == null;

    private static void MoveLeave(World w, SenateSystem senate)
    {
        var def = senate.Motions.Def(LeaveKind); var offices = senate.Offices; var mine = w.Protagonist?.HouseId;
        if (def == null || w.IsImperium || w.CivilWar != null || SenateSystem.Docket(w).Count() >= SenateSystem.DocketSize || w.Motions.Any(m => m.Open && m.Kind == LeaveKind) || EdictSystem.Cowed(w, "") != null) return;
        bool ladder = LadderSystem.Active(w, offices) && LadderSystem.CountIn(w, offices) is 1 or 2, consul = ElectionSystem.ConsulOffices(offices).Any() && w.Emergency == null && ElectionSystem.CountIn(w) is 1 or 2;
        if (!ladder && !consul) return;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && !HasLeave(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (Economy.HeadOf(w, h) is not { } head || WantsLeave(w, senate, h, ladder, consul) == null) continue;
            var mover = MayBring(w, senate, h, def) && !SenateSystem.Stale(w, h.Id, LeaveKind, h.Id) ? h
                : w.Houses.Values.Where(o => o.Id != h.Id && o.Id != mine && MayBring(w, senate, o, def) && !SenateSystem.Stale(w, o.Id, LeaveKind, h.Id) && Economy.HeadOf(w, o) is { } oh && (w.GetRelationship(oh.Id, head.Id)?.Disposition ?? 0) >= FriendRegard)
                    .OrderByDescending(o => o.SphereInfluence.GetValueOrDefault("senate")).ThenBy(o => o.Id, StringComparer.Ordinal).FirstOrDefault();
            if (mover == null) continue;
            senate.Add(w, mover.Id, LeaveKind, h.Id); Count(w, LeaveMovedKey);
            return;   // one a year
        }
    }

    // ---------------- the year ----------------

    /// <summary>Once a year, after the Senate and the generals: answers wanted are given (the player's lapse when the year is out), the year
    /// at home ends, leave that an election has used is spent, other houses ask leave, and the player is told whose name will not count.</summary>
    public static void Tick(World w, SenateSystem senate, CaesarSystem caesar)
    {
        var mine = w.Protagonist?.HouseId; var pid = w.ProtagonistId; var offices = senate.Offices;
        foreach (var f in YearFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        foreach (var t in w.TriumphVotes.Where(t => !t.Done && t.Choice.Length == 0).ToList())
        {
            var g = w.Char(t.CharacterId);
            if (g is not { Alive: true } || w.House(t.HouseId) == null || w.IsImperium) { t.Choice = Void; t.Done = true; continue; }
            if (UnderRecall(w, g.Id)) { VoidUnderRecall(w, t, g); continue; }   // recalled at the same year's end: the recall is answered, and no triumph is had
            if (g.Id == pid) { if (w.Turn > t.Turn) Decline(w, t, Lapsed); continue; }   // the player is asked, and has the year to answer
            var why = Weigh(w, offices, g); bool keep = why.Sum(x => x.by) > 0;
            string because = string.Join(", ", why.Where(x => keep ? x.by > 0 : x.by < 0).Select(x => x.why));
            if (keep) Decline(w, t, Keep, because); else ComeHome(w, caesar, t, because);
        }
        foreach (var m in w.Motions.Where(m => m.Kind == LeaveKind && m.Vetoed && m.ResolvedTurn == w.Turn)) Count(w, LeaveVetoedKey);
        // an election held this year spends every leave given before it, and is where one elected in absence is counted (before anyone
        // who came home to stand takes the command up again)
        if (w.HasFlag("election_held") || w.Counter("mark:" + LadderSystem.HeldFlag) == w.Turn)
        {
            foreach (var who in w.Magistrates.Where(m => m.SinceTurn == w.Turn).Select(m => m.CharacterId).Concat(ElectionSystem.ConsulOffices(offices).Select(d => OfficeSystem.Holding(w, d.Id)).Where(h => h?.SinceTurn == w.Turn).Select(h => h!.CharacterId)))
                if (w.Char(who) is { } c && AwayCommand(w, c) != null) Count(w, ElectedAwayKey);
            foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith("absence_leave:", StringComparison.Ordinal)).ToList()) { w.WorldCounters.Remove(k); Count(w, LeaveSpentKey); }
        }
        foreach (var t in w.TriumphVotes.Where(t => !t.Done && t.Choice is Home or Stand && w.Turn >= t.HomeUntil).ToList()) TakeUp(w, t);
        foreach (var k in w.WorldCounters.Where(kv => kv.Key.StartsWith("home_to_stand:", StringComparison.Ordinal) && kv.Value <= w.Turn).Select(kv => kv.Key).ToList()) w.WorldCounters.Remove(k);
        ComeHomeToStand(w, senate);
        MoveLeave(w, senate);
        if (mine == null) { Prune(w); return; }
        if (Waiting(w, pid) != null) w.WorldFlags.Add(AskedFlag);
        if (w.TriumphVotes.Any(t => !t.Done && t.Choice == Home && t.HouseId == mine)) w.WorldFlags.Add(HomeFlag);
        if (HasLeave(w, mine)) w.WorldFlags.Add(LeaveFlag);
        // a name down for the ladder that will not be counted: the ladder takes it down at the year's end, and the player is told why
        foreach (var cd in w.Candidacies.ToList())
            if (w.Char(cd.CharacterId) is { Alive: true } c && c.HouseId == mine && AwaySays(w, c) is { } away)
            {
                Count(w, NamesDroppedKey);
                w.Tell("warn", $"{(c.Id == pid ? "Your" : c.Name + "'s")} name is taken down for {offices.Rung(cd.RungId)?.Name.ToLowerInvariant() ?? "office"}: {away}.", "senate:ladder", c.Id);
            }
        if (w.HasFlag(ElectionSystem.StandFlag(mine)) && ElectionSystem.CountIn(w) == 1 && ConsulAway(w, offices, mine) is { } noConsul)
            w.Tell("warn", $"Your house has declared for the consulship, and nobody of it may stand at the count next year: {noConsul}.", "senate:ladder");
        Prune(w);
    }

    /// <summary>Every triumph still being answered or had is kept; of those that are done, the latest forty. One who came home to stand is
    /// forgotten once the count is past.</summary>
    private static void Prune(World w)
    {
        w.TriumphVotes.RemoveAll(t => t.Done && t.Choice == Stand);
        int done = w.TriumphVotes.Count(t => t.Done); if (done <= KeptDeep) return;
        foreach (var t in w.TriumphVotes.Where(t => t.Done).Take(done - KeptDeep).ToList()) w.TriumphVotes.Remove(t);
    }
}
