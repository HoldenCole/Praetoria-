using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// A command by service (docs/MILITARY.md, "A command by service"; docs/IDEAS_FROM_MODS.md, entry 1; docs/GAPS.md Q15 and R1). A
/// person of any house may command a fleet their house does not own. Three ways in: a <b>legate</b>, named by whoever holds the
/// consulship, the Admiral of the Reach's office or emergency powers, to a fleet under them (an office fleet, the state's fleet, or
/// one of the holder's own house); an officer <b>lent</b> by a friendly house to a house at war, with the leave of its head; and
/// <b>seeking</b> a command, where somebody offers their service to such a holder or to a house at war and is taken or refused
/// by plain reasons (skill in command, renown, the regard between the heads, and whether a command is free). The victories, the
/// valour, the renown and the crews' devotion are the commander's; the hulls, the upkeep and the spoils stay the owner's. The
/// owner, the one who named them, or the Senate (by its recall) may relieve them, and they may resign. It ends of itself when what
/// it rested on is gone. Other houses name legates, borrow officers and seek commands by the same rules, sparingly. RNG-free.
/// </summary>
public static class ServiceSystem
{
    public const string Legate = "legate", Lent = "lent";
    public const string ByConsul = "consul", ByAdmiral = "admiral", ByPowers = "emergency", ByWar = "war";
    /// <summary>What may be put to the player's head: leave to lend one of the house; an offer of service; to be named legate.</summary>
    public const string AskLend = "lend", AskOffer = "offer", AskLegate = "legate";

    /// <summary>What a seeker must bring: tactics of <see cref="SeekTactics"/> or a Lieutenant's rank in the Navy; renown of <see cref="SeekRenown"/>;
    /// a taker whose head thinks at least <see cref="SeekRegard"/> of the seeker's head. A command is free when a fleet has no commander, or one
    /// with <see cref="Displaces"/> less tactics than the seeker (and is neither the taker nor the owner's head).</summary>
    public const int SeekTactics = 3, SeekRenown = 10, SeekRegard = 0, Displaces = 2;
    /// <summary>A head lends an officer to a house it thinks <see cref="LeaveRegard"/> of, or to one in the same war at 0; nobody serves under one
    /// they think worse than <see cref="ServeRegard"/> of.</summary>
    public const int LeaveRegard = 10, ServeRegard = -20;
    /// <summary>How regard moves: the one named thinks the better of who named them, the two heads of each other; one displaced, relieved or refused thinks the worse.</summary>
    public const int NamedRegard = 10, BondRegard = 5, DisplacedRegard = -10, RelievedRegard = -10, ResignRegard = -5, RefusedRegard = -5, LeaveGivenRegard = 10;
    /// <summary>Other houses: a legate is named only with tactics of <see cref="NpcTactics"/>, to a fleet of <see cref="NpcFleetLeast"/> hulls or more, from a house
    /// the holder thinks <see cref="NpcRegard"/> of (or one of the holder's bench at 0); a holder keeps <see cref="NpcLegatesMost"/> at most. Somebody of
    /// another house who is not its head, is <see cref="SeekAgeMost"/> or younger and has no command looks for one at most once in <see cref="SeekEvery"/> years,
    /// and in a given year only one in <see cref="SeekOneIn"/> of them looks.</summary>
    public const int NpcTactics = 4, NpcFleetLeast = 2, NpcRegard = 20, NpcLegatesMost = 2, SeekAgeMost = 45, SeekEvery = 5, SeekOneIn = 3, KeptPast = 40;
    /// <summary>One agent to name a legate, to borrow an officer or to offer service.</summary>
    public const int AgentCost = 1;
    /// <summary>What the player's head refused, or left unanswered for the year, is not asked again by the same asker for the same person for
    /// <see cref="RefusedYears"/> years: they look to the next instead.</summary>
    public const int RefusedYears = 5;
    public const string RefusedPrefix = "service_refused:", SoughtPrefix = "sought:";
    public static string RefusedKey(string askerId, string characterId) => $"{RefusedPrefix}{askerId}:{characterId}";
    /// <summary>Did the player's head say no to this asker about this person (or let it lapse) within <see cref="RefusedYears"/> years?</summary>
    public static bool Refused(World w, string askerId, string characterId) => w.WorldCounters.TryGetValue(RefusedKey(askerId, characterId), out int at) && w.Turn - at < RefusedYears;

    public const string GivenFlag = "service_given", EndedFlag = "service_ended", AskedFlag = "service_asked";
    public const string CommandsKey = "service_commands", OthersKey = "service_commands_others", LegatesKey = "legates_named", LentKey = "officers_lent",
        SoughtKey = "commands_sought", EndedKey = "service_commands_ended", WentOverKey = "service_went_over";
    public const string Target = "military:service";

    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Bump(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
    private static int Regard(World w, Character from, Character to) => from.Id == to.Id ? 100 : w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0;
    private static void Move(World w, Character? from, Character? to, int by)
    { if (from == null || to == null || from.Id == to.Id || by == 0) return; var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100); }
    private static string Nm(World w, string houseId) => w.House(houseId)?.Name ?? houseId;
    private static Character HeadOrSelf(World w, Character c) => w.House(c.HouseId) is { } h && Economy.HeadOf(w, h) is { Alive: true } head ? head : c;
    private static bool Mine(World w, string houseId) => w.Protagonist?.HouseId == houseId;
    /// <summary>A person as a refusal names them: "you" for the player's head, else by name.</summary>
    private static string Who(World w, Character c) => c.Id == w.ProtagonistId ? "you" : c.Name;
    private static string Fig(int n) => Counted.Figure(n);

    // ---------------- reading ----------------

    /// <summary>The command by service a fleet is under, if its admiral holds it so.</summary>
    public static ServiceCommand? Of(World w, Fleet f) => w.Service.Count == 0 ? null : w.Service.FirstOrDefault(s => s.FleetId == f.Id && s.CommanderId == f.AdmiralId);
    /// <summary>The command by service a person holds, if any.</summary>
    public static ServiceCommand? Held(World w, Character c) => w.Service.Count == 0 ? null : w.Service.FirstOrDefault(s => s.CommanderId == c.Id);
    /// <summary>Commands by service held by people of a house.</summary>
    public static List<ServiceCommand> OfHouse(World w, string houseId) => w.Service.Where(s => s.CommanderHouseId == houseId).OrderBy(s => s.Since).ThenBy(s => s.FleetId, StringComparer.Ordinal).ToList();
    /// <summary>The house a commander fights for: the owner of the fleet they command by service, else their own.</summary>
    public static string Serves(World w, Character c) => Held(w, c)?.OwnerId ?? c.HouseId;
    /// <summary>Does one of this house command this fleet by service?</summary>
    public static bool CommandedBy(World w, Fleet f, string houseId) => Of(w, f) is { } s && s.CommanderHouseId == houseId;
    public static string KindName(ServiceCommand s) => s.Kind == Legate ? "legate" : "an officer lent";
    public static string AuthorityName(string authority) => authority switch
    {
        ByConsul => "consul", ByAdmiral => "Admiral of the Reach", ByPowers => "holder of the emergency powers", _ => "head of a house at war"
    };

    /// <summary>What each authority a person holds now gives them, widest first: emergency powers, the Admiralty, the consulship.</summary>
    public static List<string> Authorities(World w, OfficeCatalog offices, Character? c)
    {
        var list = new List<string>(); if (c is not { Alive: true }) return list;
        if (EmergencySystem.Holder(w)?.Id == c.Id) list.Add(ByPowers);
        if (offices.Defs.Any(d => d.CommandsFleets && OfficeSystem.Holding(w, d.Id)?.CharacterId == c.Id)) list.Add(ByAdmiral);
        if (ElectionSystem.ConsulOffices(offices).Any(d => OfficeSystem.Holding(w, d.Id)?.CharacterId == c.Id)) list.Add(ByConsul);
        return list;
    }
    private static bool AdmiralHeld(World w, OfficeCatalog offices) => offices.Defs.Any(d => d.CommandsFleets && OfficeSystem.Holding(w, d.Id) != null);
    /// <summary>A fleet that may be given to someone of another house at all: in commission, with hulls, not a levy or a company out on hire, and of the Reach.</summary>
    public static bool Serviceable(Fleet f) => !f.LaidUp && f.LevyOf.Length == 0 && f.HiredFrom.Length == 0 && f.Strength > 0 && f.OwnerId.Length > 0 && !World.Outsider(f.OwnerId);
    /// <summary>Is the fleet under the holder by this authority? The Admiral: the office fleets under the house's colours, and the house's own. A consul:
    /// the house's own, and the state's fleet when no Admiral holds it. Emergency powers: the house's own and every office fleet.</summary>
    public static bool Under(World w, OfficeCatalog offices, Character holder, string authority, Fleet f)
    {
        if (!Serviceable(f) || w.CivilWar is { SwordHouseId.Length: > 0 } cw && f.Office && f.OwnerId == cw.SwordHouseId) return false;   // the sword's, while the war lasts
        bool own = !f.Office && f.OwnerId == holder.HouseId;
        return authority switch
        {
            ByAdmiral => own || f.Office && f.OwnerId == holder.HouseId,
            ByConsul => own || f.Office && !AdmiralHeld(w, offices),
            ByPowers => own || f.Office,
            _ => false
        };
    }
    /// <summary>The authority by which this person may name a legate to this fleet, widest first, or null.</summary>
    public static string? AuthorityOver(World w, OfficeCatalog offices, Character holder, Fleet f) => Authorities(w, offices, holder).FirstOrDefault(a => Under(w, offices, holder, a, f));
    /// <summary>The fleets of a house that it may give to an officer of another house.</summary>
    public static IEnumerable<Fleet> HouseFleets(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Office && Serviceable(f));
    /// <summary>The house a house at war is fighting: the oldest of its wars.</summary>
    public static string? EnemyOf(World w, string houseId) => w.Wars.Where(x => x.Involves(houseId)).OrderBy(x => x.Since).ThenBy(x => x.Other(houseId), StringComparer.Ordinal).Select(x => x.Other(houseId)).FirstOrDefault();

    // ---------------- who may serve ----------------

    /// <summary>Why this person may not take a command by service now, or null: alive, grown, fit, retired from nothing, free of any command, and not
    /// barred by the Senate.</summary>
    public static string? FreeRefusal(World w, Character? c)
    {
        if (c is not { Alive: true }) return "no such person";
        if (c.Age < DynastySystem.AgeOfMajority) return $"{c.Name} is {c.Age}: a commander is grown, {DynastySystem.AgeOfMajority} or older";
        if (c.Flags.Contains(LifeSystem.FrailChar)) return $"{c.Name} is too frail for the sea";
        if (c.Flags.Contains(LifeSystem.RetiredChar)) return $"{c.Name} has retired";
        if (c.Flags.Contains(DynastySystem.HiddenFlag)) return $"{c.Name} is fostered away";
        if (CaesarSystem.Barred(w, c)) return $"the Senate had {c.Name} lay a command down, and {c.Name} may hold none until year {w.Counter(CaesarSystem.BarredKey(c.Id))}";
        if (TriumphSystem.StandingHome(w, c)) return $"{c.Name} has come home to stand for office";
        if (w.Fleets.Values.Where(f => f.AdmiralId == c.Id).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault() is { } has) return $"{c.Name} commands {has.Name} already";
        return null;
    }
    /// <summary>What a taker asks of one who offers themselves: skill in command, and a name.</summary>
    public static string? FitRefusal(World w, Character c)
    {
        int t = c.Skill("tactics"); bool officer = c.CareerTrack == "military" && c.CareerRank >= FleetSystem.CaptainRank, you = c.Id == w.ProtagonistId;
        if (t < SeekTactics && !officer)
            return $"a command asks tactics of {SeekTactics} or a Lieutenant's rank in the Navy, and {(you ? "you have" : c.Name + " has")} tactics {t} and {(you ? "are" : "is")} {(c.CareerTrack == "military" ? "not yet a Lieutenant" : "not in the Navy")}";
        int renown = LifeSystem.Renown(c);
        return renown >= SeekRenown ? null : $"a command asks a name (renown {SeekRenown}), and {(you ? "your" : c.Name + "'s")} renown is {renown}";
    }
    /// <summary>Is this fleet free for the seeker: of a house the seeker's is at peace with, and with no commander, or one with <see cref="Displaces"/>
    /// less tactics who is not the taker, the owner's head, the player, or a general on the march?</summary>
    public static bool Open(World w, Fleet f, Character seeker, Character taker)
    {
        if (f.OwnerId == seeker.HouseId || w.AtWar(seeker.HouseId, f.OwnerId)) return false;   // nobody commands the fleet of a house their own is at war with
        if (TriumphSystem.Held(w, f)) return false;   // its general is home for a triumph, and takes it up again (docs/CAESAR.md)
        var cmd = FleetSystem.AdmiralOf(w, f); if (cmd == null) return true;
        if (cmd.Id == taker.Id || cmd.Id == seeker.Id || cmd.Id == w.ProtagonistId || w.CivilWar?.GeneralId == cmd.Id) return false;
        if (w.House(f.OwnerId) is { } owner && Economy.HeadOf(w, owner)?.Id == cmd.Id) return false;
        return seeker.Skill("tactics") >= cmd.Skill("tactics") + Displaces;
    }
    /// <summary>May a holder give this fleet by their own will, unasked? Not one of the player's house's own: its head gives those (a relative of the
    /// house in office, whose own fleets they would be, included), and on an office fleet of the player's house only a command that is empty, so that
    /// nobody the head put there is put out unasked.</summary>
    public static bool Gives(World w, Character holder, Fleet f) => holder.Id == w.ProtagonistId || !Mine(w, f.OwnerId) || f.Office && FleetSystem.AdmiralOf(w, f) == null;
    private static Fleet? FreeFleet(World w, IEnumerable<Fleet> fleets, Character seeker, Character taker) =>
        fleets.Where(f => Open(w, f, seeker, taker)).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Where a command by service would go: the fleet, who gives it, as what, under what authority, and the war it is for.</summary>
    public sealed record Plan(Fleet Fleet, Character Taker, string Kind, string Authority, string EnemyId);

    /// <summary>Everyone a person could offer their service to: the holders of the consulship, the Admiralty and emergency powers, and the houses at
    /// war, other than the person's own house.</summary>
    public static List<string> TakersFor(World w, OfficeCatalog offices, Character seeker)
    {
        var list = new List<string>();
        var holders = ElectionSystem.ConsulOffices(offices).Concat(offices.Defs.Where(d => d.CommandsFleets)).Select(d => OfficeSystem.Holding(w, d.Id)?.CharacterId ?? "")
            .Append(EmergencySystem.Holder(w)?.Id ?? "").Where(id => id.Length > 0 && id != seeker.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal);
        foreach (var id in holders) if (w.Char(id) is { Alive: true }) list.Add(id);
        foreach (var h in w.Houses.Values.Where(h => h.Id != seeker.HouseId && !World.Outsider(h.Id) && w.Wars.Any(x => x.Involves(h.Id)) && !w.AtWar(h.Id, seeker.HouseId)).OrderBy(h => h.Id, StringComparer.Ordinal)) list.Add(h.Id);   // not the house's own enemies
        return list;
    }
    /// <summary>A taker's name, for the screen.</summary>
    public static string TakerName(World w, OfficeCatalog offices, string takerId) =>
        w.House(takerId) is { } h ? $"{h.Name}, at war with {Nm(w, EnemyOf(w, h.Id) ?? "")}" : w.Char(takerId) is { } c ? $"{c.Name} of {Nm(w, c.HouseId)}, {string.Join(" and ", Authorities(w, offices, c).Select(AuthorityName).DefaultIfEmpty("who holds no office now"))}" : takerId;

    /// <summary>Why a taker would not take this person on, or null with the plan of where they would go. The plain reasons: the taker must be one who
    /// gives commands to others (an office-holder, or a house at war); the seeker free and fit (tactics, a name); the two houses at peace; the taker's
    /// head not thinking ill of the seeker's head; and a command free under the taker.</summary>
    public static string? SeekRefusal(World w, OfficeCatalog offices, Character? seeker, string takerId, out Plan? plan)
    {
        plan = null;
        var free = FreeRefusal(w, seeker); if (free != null) return free;
        var c = seeker!; var seekerHead = HeadOrSelf(w, c);
        if (w.House(takerId) is { } th)
        {
            if (th.Id == c.HouseId) return "it is the house's own: the head gives its commands on each fleet's card";
            if (Economy.HeadOf(w, th) is not { Alive: true } head) return $"{th.Name} has no head to take anybody on";
            var enemy = EnemyOf(w, th.Id); if (enemy == null) return $"{th.Name} is at war with nobody, and takes no officer from another house";
            if (w.AtWar(th.Id, c.HouseId)) return $"{Nm(w, c.HouseId)} is at war with {th.Name}";
            var fit = FitRefusal(w, c); if (fit != null) return fit;
            int regard = Regard(w, head, seekerHead);
            if (regard < SeekRegard) return $"{head.Name} thinks ill of {Who(w, seekerHead)} ({Fig(regard)}), and takes nobody from a house thought ill of";
            if (WillRefusal(w, c, head) is { } unwilling) return unwilling;
            var fleet = FreeFleet(w, HouseFleets(w, th.Id), c, head);
            if (fleet == null) return $"{th.Name} has no command free: every fleet of it has a commander, none with {Displaces} less tactics than {(c.Id == w.ProtagonistId ? "your" : c.Name + "'s")} {c.Skill("tactics")}";
            plan = new Plan(fleet, head, Lent, ByWar, enemy); return null;
        }
        if (w.Char(takerId) is { Alive: true } holder)
        {
            if (holder.Id == c.Id) return "nobody names themselves legate";
            var auths = Authorities(w, offices, holder);
            if (auths.Count == 0) return $"{holder.Name} holds no consulship, no Admiralty and no emergency powers, and names no legate";
            if (w.AtWar(holder.HouseId, c.HouseId)) return $"{Nm(w, c.HouseId)} is at war with {Nm(w, holder.HouseId)}";
            var fit = FitRefusal(w, c); if (fit != null) return fit;
            var holderHead = HeadOrSelf(w, holder); int regard = Math.Max(Regard(w, holder, seekerHead), Regard(w, holderHead, seekerHead));
            if (regard < SeekRegard) return $"{holder.Name} thinks ill of {Who(w, seekerHead)} ({Fig(regard)}), and names nobody from a house thought ill of";
            if (WillRefusal(w, c, holder) is { } unwilling) return unwilling;
            foreach (var a in auths)
            {
                var fleet = FreeFleet(w, w.Fleets.Values.Where(f => Under(w, offices, holder, a, f) && Gives(w, holder, f)), c, holder);
                if (fleet != null) { plan = new Plan(fleet, holder, Legate, a, ""); return null; }
            }
            return $"no command under {holder.Name} is free: every fleet under the office has a commander, none with {Displaces} less tactics than {(c.Id == w.ProtagonistId ? "your" : c.Name + "'s")} {c.Skill("tactics")}";
        }
        return "nobody to offer service to";
    }

    /// <summary>Nobody serves under one they think worse than <see cref="ServeRegard"/> of.</summary>
    public static string? WillRefusal(World w, Character c, Character taker)
    {
        int will = Regard(w, c, taker); if (will >= ServeRegard) return null;
        string under = taker.Id == w.ProtagonistId ? "you" : "them";
        return $"{(c.Id == w.ProtagonistId ? "you think" : c.Name + " thinks")} too ill of {Who(w, taker)} ({Fig(will)}) to serve under {under}: it takes {Fig(ServeRegard)} or better";
    }

    /// <summary>Why the head of the officer's house would not give leave to lend them to a house at war, or null.</summary>
    public static string? LeaveRefusal(World w, Character lenderHead, Character borrowerHead, Character officer, string enemyId)
    {
        var lender = w.House(lenderHead.HouseId); string lenderName = lender?.Name ?? lenderHead.HouseId;
        if (officer.Id == lenderHead.Id) return $"{officer.Name} is the head of {lenderName}: a head is not lent, though a head may offer their own service";
        if (w.AtWar(lenderHead.HouseId, borrowerHead.HouseId)) return $"{lenderName} is at war with {Nm(w, borrowerHead.HouseId)}";
        if (enemyId == lenderHead.HouseId) return $"the war is with {lenderName} itself";
        bool ally = enemyId.Length > 0 && w.AtWar(lenderHead.HouseId, enemyId); int need = ally ? 0 : LeaveRegard, regard = Regard(w, lenderHead, borrowerHead);
        if (regard < need) return $"{lenderHead.Name} thinks {Fig(regard)} of {Who(w, borrowerHead)}, and lends an officer at {need}" + (ally ? "" : " (at 0 to a house in the same war)");
        if (!ally && w.Wars.Any(x => x.Involves(lenderHead.HouseId))) return $"{lenderName} is at war itself, and keeps its officers";
        int will = Regard(w, officer, borrowerHead);
        return will < ServeRegard ? $"{officer.Name} thinks too ill of {Who(w, borrowerHead)} to serve under {Who(w, borrowerHead)} ({Fig(will)})" : null;
    }

    // ---------------- beginning and ending ----------------

    /// <summary>Somebody of another house takes a command by service: whoever commanded the fleet steps down (a commander by service among them), the
    /// commander leaves any other command and any captaincy, and it is written into the record of their life.</summary>
    public static ServiceCommand Begin(World w, Fleet f, Character commander, Character namer, string kind, string authority, string enemyId = "", string leaveById = "", bool sought = false)
    {
        var was = FleetSystem.AdmiralOf(w, f); var mine = w.Protagonist?.HouseId;
        if (Of(w, f) is { } before) End(w, before, $"{commander.Name} was given the command");
        foreach (var x in w.Fleets.Values.Where(x => x.AdmiralId == commander.Id)) x.AdmiralId = "";
        foreach (var x in w.Fleets.Values.Where(x => x.CaptainId == commander.Id)) x.CaptainId = "";
        f.AdmiralId = commander.Id;
        var s = new ServiceCommand { FleetId = f.Id, FleetName = f.Name, CommanderId = commander.Id, CommanderHouseId = commander.HouseId, OwnerId = f.OwnerId, Kind = kind, Authority = authority,
            NamedById = namer.Id, LeaveById = leaveById, EnemyId = enemyId, Sought = sought, Since = w.Turn };
        w.Service.Add(s); FleetSystem.RefreshAdmiralFlags(w);
        Move(w, commander, namer, NamedRegard);
        var ownHead = HeadOrSelf(w, commander); var namerHead = HeadOrSelf(w, namer);
        Move(w, ownHead, namerHead, BondRegard); Move(w, namerHead, ownHead, BondRegard);
        if (was != null && was.Id != commander.Id && was.HouseId == f.OwnerId) Move(w, was, namer, DisplacedRegard);
        Bump(w, CommandsKey); if (commander.HouseId != mine) Bump(w, OthersKey);
        Bump(w, kind == Legate ? LegatesKey : LentKey); if (sought) Bump(w, SoughtKey);
        string owner = Nm(w, f.OwnerId), whatFor = kind == Legate ? $"legate of {f.Name} by {namer.Name}, {AuthorityName(authority)}" : $"to command {f.Name} for {owner} in its war with {Nm(w, enemyId)}";
        LifeSystem.Note(w, commander, "post", kind == Legate ? $"Named {whatFor}." : $"Lent by {Nm(w, commander.HouseId)} {whatFor}.");
        w.Log(kind == Legate ? $"{commander.Name} of {Nm(w, commander.HouseId)} is named {whatFor}{(f.Office ? ", a fleet of the Reach's own" : $", a fleet of {owner}")}."
                             : $"{commander.Name} of {Nm(w, commander.HouseId)} is lent {whatFor}.");
        if (mine != null && (commander.HouseId == mine || f.OwnerId == mine || namer.HouseId == mine))
        {
            Mark(w, GivenFlag);
            string who = commander.Id == w.ProtagonistId ? "You are" : commander.HouseId == mine ? $"{commander.Name}, of your house, is" : $"{commander.Name} of {Nm(w, commander.HouseId)} is";
            w.Tell("good", $"{who} {(kind == Legate ? "named " + whatFor : "lent " + whatFor)}. The victories and the crews' regard are the commander's; the hulls, their keep and the spoils stay {owner}'s.", Target, f.Id);
        }
        return s;
    }

    /// <summary>A command by service ends. The fleet is left without an admiral (unless <paramref name="keepCommand"/>), a captain of the commander's
    /// house leaves with them, the record is kept, and the house it concerns is told.</summary>
    public static void End(World w, ServiceCommand s, string how, bool keepCommand = false)
    {
        if (!w.Service.Remove(s)) return;
        s.Ended = Math.Max(1, w.Turn); s.How = how; w.PastService.Add(s); if (w.PastService.Count > KeptPast) w.PastService.RemoveRange(0, w.PastService.Count - KeptPast);
        Bump(w, EndedKey);
        if (!keepCommand && w.Fleets.TryGetValue(s.FleetId, out var f))
        {
            if (f.AdmiralId == s.CommanderId) f.AdmiralId = "";
            if (f.CaptainId.Length > 0 && w.Char(f.CaptainId)?.HouseId == s.CommanderHouseId && s.CommanderHouseId != f.OwnerId) f.CaptainId = "";
        }
        FleetSystem.RefreshAdmiralFlags(w);
        var c = w.Char(s.CommanderId); string won = s.Won > 0 ? $" Under them it won {Counted.Of(s.Won, "battle")}." : "";
        if (c is { Alive: true }) LifeSystem.Note(w, c, "post", keepCommand ? $"Commanded {s.FleetName} of {Nm(w, s.OwnerId)} by service no longer, and kept the command: {how}.{won}" : $"Gave up the command of {s.FleetName} of {Nm(w, s.OwnerId)}: {how}.{won}");
        w.Log(keepCommand ? $"{c?.Name ?? "The commander"} of {Nm(w, s.CommanderHouseId)} commands {s.FleetName} of {Nm(w, s.OwnerId)} by service no longer, but keeps the command: {how}."
                          : $"{c?.Name ?? "The commander"} of {Nm(w, s.CommanderHouseId)} no longer commands {s.FleetName} of {Nm(w, s.OwnerId)}: {how}.");
        var mine = w.Protagonist?.HouseId;
        if (mine != null && (s.CommanderHouseId == mine || s.OwnerId == mine || w.Char(s.NamedById)?.HouseId == mine))
        {
            Mark(w, EndedFlag);
            string who = s.CommanderId == w.ProtagonistId ? "You" : c?.Name ?? "The commander";
            w.Tell("info", keepCommand ? $"{who} {(s.CommanderId == w.ProtagonistId ? "command" : "commands")} {s.FleetName} of {Nm(w, s.OwnerId)} by service no longer, but {(s.CommanderId == w.ProtagonistId ? "keep" : "keeps")} the command: {how}."
                                       : $"{who} no longer {(s.CommanderId == w.ProtagonistId ? "command" : "commands")} {s.FleetName} of {Nm(w, s.OwnerId)} by service: {how}.", Target, s.FleetId);
        }
    }

    /// <summary>One who commands by service is given another command (a fleet of their own house, by its head or their own wish): the service ends
    /// first, so the owner is told and the record kept.</summary>
    public static void Leave(World w, Character c, string how) { if (Held(w, c) is { } s) End(w, s, how); }

    /// <summary>One who commands by service has changed house that day (cast out, or taken back): the service ends at once, not at the
    /// year's end, so that nobody commands for a day the fleet of a house their new one is at war with. One who has come into the house
    /// whose fleet it is keeps the command, by right now and not by service.</summary>
    public static void HouseChanged(World w, Character c, string how)
    {
        if (Held(w, c) is { } s && c.HouseId != s.CommanderHouseId) End(w, s, how, keepCommand: NowOwn(w, s));
    }

    /// <summary>A commander by service has died: the command ends that day, and the owner and the commander's house are told.</summary>
    public static void Died(World w, Character c)
    {
        foreach (var s in w.Service.Where(s => s.CommanderId == c.Id).ToList()) End(w, s, $"{c.Name} died");
    }

    /// <summary>The player's seat has passed (called from <see cref="DynastySystem.HandOver"/>): what was asked of the old head by name, to
    /// be named legate, was asked of them and not of the heir, and is not put to the heir. What was asked of the house stands.</summary>
    public static void SeatPassed(World w, Character? previous)
    {
        if (previous != null) w.ServiceAsks.RemoveAll(a => a.Kind == AskLegate && a.CharacterId == previous.Id);
    }

    /// <summary>The crews were the general's before they were the house's: when the general crosses the line, a fleet they command by service goes with
    /// them, and is their house's from that day (docs/CAESAR.md).</summary>
    public static int GoOver(World w, Character general, House house)
    {
        int hulls = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId == general.Id && f.OwnerId != house.Id && f.DevotedTo == general.Id && f.Devotion >= CaesarSystem.DevotedAt).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            string from = f.OwnerId;
            if (Of(w, f) is { } s) End(w, s, $"{general.Name} crossed the line, and the crews went with them", keepCommand: true);
            f.OwnerId = house.Id; f.Office = false; hulls += f.Strength; Bump(w, WentOverKey);
            w.Log($"The crews of {f.Name} were {general.Name}'s before they were {Nm(w, from)}'s: {Counted.Hulls(f.Strength)} go over with {general.Name} to {house.Name}.");
        }
        return hulls;
    }

    /// <summary>The hulls that would follow a general on the march: the house's own, and those they command by service whose crews are theirs.</summary>
    public static int MarchStrength(World w, House house, Character general) =>
        FleetSystem.StrengthOf(w, house.Id) + w.Fleets.Values.Where(f => f.AdmiralId == general.Id && f.OwnerId != house.Id && !f.LaidUp && f.DevotedTo == general.Id && f.Devotion >= CaesarSystem.DevotedAt).Sum(f => f.Strength);

    /// <summary>Why a standing command by service no longer stands, or null.</summary>
    public static string? Lapsed(World w, OfficeCatalog offices, ServiceCommand s)
    {
        if (!w.Fleets.TryGetValue(s.FleetId, out var f) || f.Strength <= 0) return "the fleet is no more";
        var c = w.Char(s.CommanderId);
        if (c is not { Alive: true }) return $"{c?.Name ?? "the commander"} is dead";
        // the commander's own state before the empty flag: the year's end lays a frail or retired commander's command down after this is read
        if (c.Flags.Contains(LifeSystem.FrailChar)) return $"{c.Name} is too frail for the sea";
        if (c.Flags.Contains(LifeSystem.RetiredChar)) return $"{c.Name} has laid down their work";
        bool atTheGate = TriumphSystem.HeldFor(w, f, c.Id);   // home for a triumph, or to stand: the command waits for them, and lapses only by its own rules (docs/CAESAR.md)
        if (CaesarSystem.Barred(w, c) && !atTheGate) return "the Senate had them lay it down";
        if (f.AdmiralId != s.CommanderId && !atTheGate) return "another was given the command";
        if (w.House(s.CommanderHouseId) == null) return $"{Nm(w, s.CommanderHouseId)} is no more";
        if (c.HouseId != s.CommanderHouseId) return c.HouseId == f.OwnerId ? $"{c.Name} is of {Nm(w, f.OwnerId)} now" : $"{c.Name} has left {Nm(w, s.CommanderHouseId)}";
        // another house's head is never lent or named legate, and one who comes to lead their own house goes home to it; the player's own head may serve
        if (c.Id != w.ProtagonistId && w.House(c.HouseId) is { } home && Economy.HeadOf(w, home)?.Id == c.Id) return $"{c.Name} is the head of {home.Name} now, and a head of house stays at home";
        if (f.OwnerId != s.OwnerId) return f.OwnerId == c.HouseId ? $"the fleet has passed to {Nm(w, f.OwnerId)}, {c.Name}'s own house" : $"the fleet has passed to {Nm(w, f.OwnerId)}";
        if (f.LaidUp) return "the fleet is laid up, and its crews paid off";
        if (w.AtWar(s.CommanderHouseId, f.OwnerId)) return $"{Nm(w, s.CommanderHouseId)} and {Nm(w, f.OwnerId)} are at war";
        if (s.Kind == Legate)
        {
            var namer = w.Char(s.NamedById);
            if (namer is not { Alive: true }) return $"{namer?.Name ?? "the one who named them"} is dead, and the commission with them";
            if (!Holds(w, offices, namer, s.Authority)) return $"the commission lapsed: {namer.Name} is no longer {AuthorityName(s.Authority)}";
            if (!Under(w, offices, namer, s.Authority, f))
                return s.Authority == ByConsul && f.Office && AdmiralHeld(w, offices) ? $"the commission lapsed: an Admiral of the Reach holds the office fleets now, and {f.Name} is no longer the consul's"
                    : $"the commission lapsed: {f.Name} is no longer under {namer.Name} as {AuthorityName(s.Authority)}";
        }
        else if (!w.Wars.Any(x => x.Involves(s.OwnerId))) return $"{Nm(w, s.OwnerId)}'s war is over, and the officer goes home";
        return null;
    }
    public static bool Holds(World w, OfficeCatalog offices, Character c, string authority) => Authorities(w, offices, c).Contains(authority);
    /// <summary>A command that has stopped being one by service only because the commander is now of the house whose fleet it is: it is theirs by right.</summary>
    private static bool NowOwn(World w, ServiceCommand s) => w.Fleets.TryGetValue(s.FleetId, out var f) && f.AdmiralId == s.CommanderId && w.Char(s.CommanderId) is { Alive: true } c && c.HouseId == f.OwnerId
        && !c.Flags.Contains(LifeSystem.FrailChar) && !c.Flags.Contains(LifeSystem.RetiredChar) && !CaesarSystem.Barred(w, c);

    /// <summary>The office passed within the house that held it (a holder died, or the next of the house took it up): whoever of that house holds it
    /// now keeps the legate on, and the commission is theirs. True when it was carried over.</summary>
    public static bool Renew(World w, OfficeCatalog offices, ServiceCommand s)
    {
        if (s.Kind != Legate || !w.Fleets.TryGetValue(s.FleetId, out var f)) return false;
        var namer = w.Char(s.NamedById);
        if (namer is { Alive: true } && Holds(w, offices, namer, s.Authority) && Under(w, offices, namer, s.Authority, f)) return false;
        string house = namer?.HouseId ?? "";
        var defs = s.Authority switch { ByConsul => ElectionSystem.ConsulOffices(offices).ToList(), ByAdmiral => offices.Defs.Where(d => d.CommandsFleets).ToList(), _ => new List<OfficeDef>() };
        var next = s.Authority == ByPowers ? EmergencySystem.Holder(w)
            : defs.Select(d => OfficeSystem.Holding(w, d.Id)).Where(h => h != null && h.HouseId == house).Select(h => w.Char(h!.CharacterId)).FirstOrDefault(c => c is { Alive: true });
        if (next == null || next.HouseId != house || next.Id == s.CommanderId || !Under(w, offices, next, s.Authority, f)) return false;
        s.NamedById = next.Id;
        w.Log($"{next.Name} keeps {w.Char(s.CommanderId)?.Name} on as legate of {f.Name}.");
        return true;
    }

    // ---------------- battles ----------------

    /// <summary>A battle was fought: the commands by service in it are credited, and the war's account names who commanded by service.</summary>
    public static void Fought(World w, string winner, string loser, string systemId)
    {
        if (w.Service.Count == 0) return;
        var war = w.Wars.FirstOrDefault(x => x.Between(winner, loser));
        foreach (var s in w.Service.OrderBy(s => s.FleetId, StringComparer.Ordinal))
        {
            if (!w.Fleets.TryGetValue(s.FleetId, out var f) || f.Moving || f.SystemId != systemId || f.AdmiralId != s.CommanderId) continue;
            if (f.OwnerId == winner) s.Won++; else if (f.OwnerId == loser) s.Lost++; else continue;
            if (war != null) WarLedger.Served(w, war, f.OwnerId, s.CommanderId);
        }
    }

    // ---------------- the year ----------------

    /// <summary>Once a year: what has lapsed ends, a commander an office left aboard a fleet that has passed to another house steps down, what was put to the
    /// player and not answered lapses, and the other houses name legates, borrow officers and look for commands.</summary>
    public static void Tick(World w, OfficeCatalog offices)
    {
        foreach (var f in new[] { GivenFlag, EndedFlag, AskedFlag }) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        // what the head let lapse is taken as a no: the asker looks to somebody else for the next five years
        foreach (var a in w.ServiceAsks.Where(a => a.Turn < w.Turn)) w.WorldCounters[RefusedKey(a.AskerId, a.CharacterId)] = w.Turn;
        w.ServiceAsks.RemoveAll(a => a.Turn < w.Turn);
        foreach (var k in w.WorldCounters.Where(kv => kv.Key.StartsWith(RefusedPrefix, StringComparison.Ordinal) && w.Turn - kv.Value >= RefusedYears).Select(kv => kv.Key).ToList()) w.WorldCounters.Remove(k);
        // one who looked for a command may look again after five years, and is forgotten then
        foreach (var k in w.WorldCounters.Where(kv => kv.Key.StartsWith(SoughtPrefix, StringComparison.Ordinal) && w.Turn - kv.Value >= SeekEvery).Select(kv => kv.Key).ToList()) w.WorldCounters.Remove(k);
        foreach (var s in w.Service.ToList()) { Renew(w, offices, s); if (Lapsed(w, offices, s) is { } why) End(w, s, why, keepCommand: NowOwn(w, s)); }
        // the office's fleet goes with the office: one who commanded it for the house that held it does not keep it for the next
        if (w.CivilWar == null)
            foreach (var f in w.Fleets.Values.Where(f => f.Office && f.AdmiralId.Length > 0 && f.LevyOf.Length == 0 && f.HiredFrom.Length == 0).OrderBy(f => f.Id, StringComparer.Ordinal))
                if (w.Char(f.AdmiralId) is { Alive: true } a && a.HouseId != f.OwnerId && Of(w, f) == null)
                {
                    f.AdmiralId = ""; w.Log($"{a.Name} of {Nm(w, a.HouseId)} lays down the command of {f.Name}: it was {Nm(w, a.HouseId)}'s by the office, and the office has passed to {Nm(w, f.OwnerId)}.");
                    if (Mine(w, a.HouseId)) w.Tell("info", $"{(a.Id == w.ProtagonistId ? "You lay" : a.Name + " lays")} down the command of {f.Name}: the office it went with has passed to {Nm(w, f.OwnerId)}.", Target, f.Id);
                }
        FleetSystem.RefreshAdmiralFlags(w);
        if (w.InAcademy || w.Galaxy == null) return;
        NameLegates(w, offices);
        Borrow(w);
        Seek(w, offices);
    }

    /// <summary>Office-holders of other houses name a legate where a fleet under them wants one: it has no commander, or somebody they think well of
    /// is a clearly better commander than the one it has. One a year for each, and only so many at a time.</summary>
    private static void NameLegates(World w, OfficeCatalog offices)
    {
        var holders = ElectionSystem.ConsulOffices(offices).Concat(offices.Defs.Where(d => d.CommandsFleets)).Select(d => OfficeSystem.Holding(w, d.Id)?.CharacterId ?? "")
            .Append(EmergencySystem.Holder(w)?.Id ?? "").Where(id => id.Length > 0 && id != w.ProtagonistId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
        foreach (var id in holders)
        {
            if (w.Char(id) is not { Alive: true } holder || w.Service.Count(s => s.NamedById == holder.Id) >= NpcLegatesMost) continue;
            var holderHead = HeadOrSelf(w, holder); var bench = FactionSystem.Of(w, holder.HouseId);
            foreach (var a in Authorities(w, offices, holder))
            {
                var fleets = w.Fleets.Values.Where(f => f.Strength >= NpcFleetLeast && Under(w, offices, holder, a, f) && Gives(w, holder, f)).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
                if (fleets.Count == 0) continue;
                Character? pick = null; Fleet? into = null;
                foreach (var c in w.LivingCharacters().Where(c => c.Skill("tactics") >= NpcTactics && !World.Outsider(c.HouseId) && FreeRefusal(w, c) == null && FitRefusal(w, c) == null)
                             .OrderByDescending(c => c.Skill("tactics")).ThenByDescending(c => FleetSystem.CommandFactor(c)).ThenBy(c => c.Id, StringComparer.Ordinal))
                {
                    if (w.House(c.HouseId) is not { } ch || w.AtWar(c.HouseId, holder.HouseId)) continue;
                    if (Economy.HeadOf(w, ch)?.Id == c.Id && c.Id != w.ProtagonistId) continue;   // a head of house stays at home; the player's head is asked
                    if (c.Id == w.ProtagonistId && Refused(w, holder.Id, c.Id)) continue;   // and one who said no is not asked again for a while
                    var theirHead = HeadOrSelf(w, c); int regard = Math.Max(Regard(w, holder, theirHead), Regard(w, holderHead, theirHead));
                    bool friend = c.HouseId == holder.HouseId || regard >= NpcRegard || bench != null && FactionSystem.Of(w, c.HouseId)?.Id == bench.Id && regard >= 0;
                    if (!friend || Regard(w, c, holder) < ServeRegard) continue;
                    var f = fleets.FirstOrDefault(f => Open(w, f, c, holder)); if (f == null) continue;
                    pick = c; into = f; break;
                }
                if (pick == null || into == null) continue;
                if (pick.Id == w.ProtagonistId) { if (!w.ServiceAsks.Any(x => x.AskerId == holder.Id)) Ask(w, AskLegate, into, holder, pick, a, ""); }
                else Begin(w, into, pick, holder, Legate, a);
                break;   // one a year
            }
        }
    }

    /// <summary>A house at war with a fleet nobody of its own can command takes an officer from a house that will lend one. One a year for each house;
    /// an officer of the player's house is asked for, and the player's head says yes or no.</summary>
    private static void Borrow(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var house in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && w.Wars.Any(x => x.Involves(h.Id))).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (Economy.HeadOf(w, house) is not { Alive: true } head || EnemyOf(w, house.Id) is not { } enemy) continue;
            var fleet = HouseFleets(w, house.Id).Where(f => f.Strength >= NpcFleetLeast && FleetSystem.AdmiralOf(w, f) == null && !TriumphSystem.Held(w, f)).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (fleet == null) continue;
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId != house.Id && !World.Outsider(c.HouseId) && c.Id != w.ProtagonistId && FreeRefusal(w, c) == null && FitRefusal(w, c) == null)
                         .OrderByDescending(c => c.Skill("tactics")).ThenByDescending(c => FleetSystem.CommandFactor(c)).ThenBy(c => c.Id, StringComparer.Ordinal))
            {
                if (w.House(c.HouseId) is not { } lender || Economy.HeadOf(w, lender) is not { Alive: true } lenderHead) continue;
                if (LeaveRefusal(w, lenderHead, head, c, enemy) != null) continue;
                if (lender.Id == mine)
                {
                    if (Refused(w, head.Id, c.Id)) continue;   // the player's head said no to this one: they look to the next
                    if (!w.ServiceAsks.Any(x => x.AskerId == head.Id)) { Ask(w, AskLend, fleet, head, c, ByWar, enemy); }
                    break;
                }
                Begin(w, fleet, c, head, Lent, ByWar, enemy, lenderHead.Id);
                break;
            }
        }
    }

    /// <summary>Somebody of another house who is not its head, is not old and has no command looks for one: offers their service to an office-holder or a
    /// house at war, by the rules the player is held to. Their own head lets them go to a house it does not think ill of. Sparing: one in three of them
    /// in a year, and nobody more often than once in five years.</summary>
    private static void Seek(World w, OfficeCatalog offices)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId != mine && !World.Outsider(c.HouseId) && c.Age <= SeekAgeMost && (c.CareerTrack == "military" || c.Skill("tactics") >= SeekTactics)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            if (Hash($"{c.Id}:seek:{w.Turn}") % SeekOneIn != 0) continue;
            if (w.WorldCounters.TryGetValue(SoughtPrefix + c.Id, out int last) && w.Turn - last < SeekEvery) continue;
            if (w.House(c.HouseId) is not { } own || Economy.HeadOf(w, own) is not { Alive: true } head || head.Id == c.Id) continue;
            if (FreeRefusal(w, c) != null || FitRefusal(w, c) != null) continue;
            if (HouseFleets(w, own.Id).Any(f => FleetSystem.AdmiralOf(w, f) == null)) continue;   // the house has a fleet for them: its head will give it
            w.WorldCounters[SoughtPrefix + c.Id] = w.Turn;
            foreach (var t in TakersFor(w, offices, c))
            {
                if (SeekRefusal(w, offices, c, t, out var plan) != null || plan == null) continue;
                if (plan.Kind == Lent && Regard(w, head, plan.Taker) < SeekRegard) continue;   // the head's leave to go
                if (plan.Taker.Id == w.ProtagonistId)
                {
                    if (Refused(w, c.Id, c.Id)) continue;   // turned down by the player's head: they offer themselves elsewhere
                    if (!w.ServiceAsks.Any(x => x.CharacterId == c.Id)) Ask(w, AskOffer, plan.Fleet, c, c, plan.Authority, plan.EnemyId);
                    break;
                }
                Begin(w, plan.Fleet, c, plan.Taker, plan.Kind, plan.Authority, plan.EnemyId, plan.Kind == Lent ? head.Id : "", sought: true);
                break;
            }
        }
    }

    // ---------------- what is put to the player ----------------

    private static void Ask(World w, string kind, Fleet f, Character asker, Character who, string authority, string enemyId)
    {
        var a = new ServiceAsk { Id = $"ask_{kind}_{f.Id}_{who.Id}_{w.Turn}", Kind = kind, FleetId = f.Id, AskerId = asker.Id, CharacterId = who.Id, Authority = authority, EnemyId = enemyId, Turn = w.Turn };
        if (w.ServiceAsks.Any(x => x.Id == a.Id)) return;
        w.ServiceAsks.Add(a); Mark(w, AskedFlag);
        w.Tell("info", AskSays(w, a), Target, f.Id);
    }

    /// <summary>What is asked, in a sentence.</summary>
    public static string AskSays(World w, ServiceAsk a)
    {
        var asker = w.Char(a.AskerId); var who = w.Char(a.CharacterId); var f = w.Fleets.GetValueOrDefault(a.FleetId);
        string fleet = f?.Name ?? a.FleetId, owner = f != null ? Nm(w, f.OwnerId) : "";
        return a.Kind switch
        {
            AskLend => $"{asker?.Name} of {Nm(w, asker?.HouseId ?? "")}, at war with {Nm(w, a.EnemyId)}, asks your leave to take {who?.Name} of your house to command {fleet}.",
            AskOffer => $"{who?.Name} of {Nm(w, who?.HouseId ?? "")} offers their service, to command {fleet} of {owner}.",
            _ => $"{asker?.Name}, {AuthorityName(a.Authority)}, would name you legate of {fleet}."
        };
    }

    /// <summary>What an answer to this ask is, in a few words naming things by id, for the record of what was done (the undo button reads
    /// the names in place of the ids).</summary>
    public static string AnswerSays(ServiceAsk a, bool yes) => a.Kind switch
    {
        AskLend => yes ? $"give leave to lend {a.CharacterId} to {a.AskerId}" : $"refuse leave to lend {a.CharacterId} to {a.AskerId}",
        AskOffer => yes ? $"take {a.CharacterId} into service on {a.FleetId}" : $"decline the service of {a.CharacterId}",
        _ => yes ? $"accept being named legate of {a.FleetId}" : $"decline being named legate of {a.FleetId}"
    };

    /// <summary>Why the player's answer cannot be given now, or null.</summary>
    public static string? AnswerRefusal(World w, OfficeCatalog offices, Character? head, ServiceAsk? a, bool yes)
    {
        if (head is not { Alive: true } || a == null) return "nothing is asked";
        if (a.Turn < w.Turn - 1) return "it was not answered in time";
        // to be named legate was asked of a head by name: an heir who has the seat since neither takes it nor refuses it
        if (a.Kind is not (AskLend or AskOffer) && a.CharacterId != head.Id) return $"it was put to {w.Char(a.CharacterId)?.Name ?? "another"}, who no longer leads the house";
        if (!yes) return null;
        var f = w.Fleets.GetValueOrDefault(a.FleetId); var who = w.Char(a.CharacterId); var asker = w.Char(a.AskerId);
        if (f == null || asker is not { Alive: true }) return "it no longer stands: the fleet or the one who asked is gone";
        var free = FreeRefusal(w, who); if (free != null) return "it no longer stands: " + free;
        switch (a.Kind)
        {
            case AskLend:
                // the one asked for must still be of the house, and not its head: the heir who has come into the house since is not lent
                if (who!.HouseId != head.HouseId || who.Id == head.Id) return "it no longer stands: " + (who.Id == head.Id ? $"{who.Name} is the head of the house now, and a head is not lent" : $"{who.Name} is no longer of your house");
                if (f.OwnerId != asker.HouseId || !w.Wars.Any(x => x.Involves(asker.HouseId))) return "it no longer stands: the war is over";
                if (w.AtWar(head.HouseId, asker.HouseId)) return $"it no longer stands: you are at war with {Nm(w, asker.HouseId)}";
                if (WillRefusal(w, who, asker) is { } unwilling) return "it no longer stands: " + unwilling;
                return FleetSystem.AdmiralOf(w, f) == null ? null : $"it no longer stands: {f.Name} has a commander";
            case AskOffer:
                if (w.AtWar(head.HouseId, who!.HouseId)) return $"you are at war with {Nm(w, who.HouseId)}";
                if (f.OwnerId == who.HouseId || who.HouseId == head.HouseId) return $"it no longer stands: {who.Name} is of {Nm(w, who.HouseId)} now";
                if (w.AtWar(who.HouseId, f.OwnerId)) return $"it no longer stands: {Nm(w, who.HouseId)} is at war with {Nm(w, f.OwnerId)}, whose fleet it is";
                if (WillRefusal(w, who, head) is { } sour) return "it no longer stands: " + sour;
                if (OfferAuthority(w, offices, head, a, f) == null) return $"it no longer stands: {f.Name} is not yours to give";
                return FleetSystem.AdmiralOf(w, f)?.Id == head.Id ? $"you command {f.Name} yourself" : null;
            default:
                if (w.AtWar(head.HouseId, f.OwnerId)) return $"it no longer stands: your house is at war with {Nm(w, f.OwnerId)}, whose fleet it is";
                return AuthorityOver(w, offices, asker, f) == a.Authority ? null : $"it no longer stands: {asker.Name} is no longer {AuthorityName(a.Authority)}";
        }
    }

    /// <summary>By what the head takes on one who offers service to this fleet: the office the offer was made to, while the fleet is under an office
    /// the head holds; the house's war, when the offer was to a house at war and the fleet is one of its own; else whichever of the two still stands.</summary>
    public static string? OfferAuthority(World w, OfficeCatalog offices, Character head, ServiceAsk a, Fleet f)
    {
        var byOffice = AuthorityOver(w, offices, head, f);
        bool war = f.OwnerId == head.HouseId && !f.Office && Serviceable(f) && EnemyOf(w, head.HouseId) != null;
        return a.Authority != ByWar ? byOffice ?? (war ? ByWar : null) : war ? ByWar : byOffice;
    }

    /// <summary>The player answers: yes begins the command; no is taken a little amiss by whoever asked, and the same is not asked again for
    /// <see cref="RefusedYears"/> years.</summary>
    public static void Answer(World w, OfficeCatalog offices, Character head, ServiceAsk a, bool yes)
    {
        w.ServiceAsks.Remove(a);
        var f = w.Fleets.GetValueOrDefault(a.FleetId); var who = w.Char(a.CharacterId); var asker = w.Char(a.AskerId);
        if (!yes)
        {
            w.WorldCounters[RefusedKey(a.AskerId, a.CharacterId)] = w.Turn;
            Move(w, a.Kind == AskOffer ? who : asker, head, RefusedRegard);
            w.Log(a.Kind switch
            {
                AskLend => $"{head.Name} will not lend {who?.Name} to {Nm(w, asker?.HouseId ?? "")}.",
                AskOffer => $"{head.Name} does not take {who?.Name} of {Nm(w, who?.HouseId ?? "")} into service.",
                _ => $"{head.Name} declines to be {asker?.Name}'s legate."
            });
            return;
        }
        if (f == null || who == null || asker == null) return;
        switch (a.Kind)
        {
            case AskLend: Begin(w, f, who, asker, Lent, ByWar, a.EnemyId, head.Id); Move(w, asker, head, LeaveGivenRegard); break;
            case AskOffer:
                string auth = OfferAuthority(w, offices, head, a, f) ?? ByWar; bool lent = auth == ByWar;   // taken on as the offer was made: by the office, or for the war
                Begin(w, f, who, head, lent ? Lent : Legate, auth, lent ? EnemyOf(w, head.HouseId) ?? "" : "", lent && w.House(who.HouseId) is { } oh && Economy.HeadOf(w, oh) is { } ohd ? ohd.Id : "", sought: true);
                break;
            default: Begin(w, f, who, asker, Legate, a.Authority); break;
        }
    }

    // ---------------- for the screens ----------------

    /// <summary>Who of other houses could be given this fleet by service now, the best commanders first: never a house's head, who stays at home.</summary>
    public static List<Character> Candidates(World w, Fleet f, int take) =>
        w.LivingCharacters().Where(c => c.HouseId != f.OwnerId && !World.Outsider(c.HouseId) && w.House(c.HouseId) is { } h && Economy.HeadOf(w, h)?.Id != c.Id && FreeRefusal(w, c) == null)
            .OrderByDescending(c => c.Skill("tactics")).ThenByDescending(c => FleetSystem.CommandFactor(c)).ThenBy(c => c.Id, StringComparer.Ordinal).Take(take).ToList();
    /// <summary>Who of the head's house could offer their service: the head, and every grown member who commands nothing.</summary>
    public static List<Character> Seekers(World w, Character head) =>
        w.LivingCharacters().Where(c => c.HouseId == head.HouseId && (c.Id == head.Id || c.Age >= DynastySystem.AgeOfMajority && !c.Flags.Contains(DynastySystem.HiddenFlag)))
            .OrderByDescending(c => c.Id == head.Id).ThenByDescending(c => c.Skill("tactics")).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
    /// <summary>A command by service in a line: who, what, whose, as what, since when, and how it went.</summary>
    public static string Says(World w, ServiceCommand s)
    {
        var c = w.Char(s.CommanderId); var namer = w.Char(s.NamedById);
        string what = s.Kind == Legate ? $"legate, named by {namer?.Name ?? "a magistrate"}{(namer != null ? ", " + AuthorityName(s.Authority) : "")}" : $"an officer lent for its war with {Nm(w, s.EnemyId)}";
        string who = $"{c?.Name ?? s.CommanderId} of {Nm(w, s.CommanderHouseId)}", fleet = $"{s.FleetName} of {Nm(w, s.OwnerId)} as {what}";
        string record = s.Won + s.Lost == 0 ? (s.Ended > 0 ? "no battle" : "no battle yet") : $"{Counted.Of(s.Won, "battle")} won, {s.Lost} lost";
        return s.Ended > 0 ? $"{who} commanded {fleet}, from year {s.Since} to year {s.Ended}: {record}. It ended: {s.How}." : $"{who} commands {fleet}, since year {s.Since}: {record}.";
    }

    /// <summary>The year's account of the commands by service: how many stand, and of whom.</summary>
    public static string Count(World w) => $"{Counted.Of(w.Service.Count, "command")} by service stand: {w.Service.Count(s => s.Kind == Legate)} legates and {w.Service.Count(s => s.Kind == Lent)} lent";
}
