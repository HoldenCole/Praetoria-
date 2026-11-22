using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

// The bench and the seat (docs/LADDER.md, "The bench and the seat"). The owner's rule is that every position has work
// (docs/PROCESS.md): a Judge hears a dispute, a Justice hears the appeal against a praetor's bent ruling, and a senator
// spends the year's attention on the audit, on their region or on their bench. Each is one decision a year with consequences
// in regard, the estates' approval, legitimacy and the public's trust in the magistrates.
public static partial class LadderSystem
{
    public const string Hearing = "hearing", Appeal = "appeal", SeatKind = "seat";
    public const string ForHouse = "for_house", ForEstate = "for_estate", Overturn = "overturn", Uphold = "uphold", SendBack = "send_back", Audit = "audit", SpeakRegion = "region", Whip = "whip";
    public const string BenchFlag = "bench_business", SeatFlag = "seat_business", AuditFoundFlag = "audit_found", OverturnedForUsFlag = "ruling_overturned_for_us", AbsentFlag = "not_seen_in_the_senate";
    public const string AbsentKey = "senate_absent_years", Estate = "estate";
    public const int JudgeRank = 2, JusticeRank = 3, AbsentYears = 3;

    // the charge of an office of the Reach: a consul's year, the Treasurer's budget, the Speaker's order of business
    public const string Charge = "charge", Consul = "consul", Treasurer = "treasurer", Speaker = "speaker";
    public const string Account = "account", Sweep = "sweep", Party = "party", Balance = "balance", Remit = "remit", Contracts = "contracts", Fair = "fair", Own = "own", Silence = "silence";
    public const string ChargeFlag = "charge_business", RemittedFlag = "levy_remitted_for_us", SilencedFlag = "no_day_in_the_senate";
    public const int SweepBy = 4, SweepCredits = 4, RemitBy = 4, RemitCredits = 5, ContractCredits = 10, BalanceCredits = 5;

    // the desk and the posting: what a rank in the administration or the navy asks of one who holds it and has no command
    public const string Desk = "desk", Posting = "posting";
    public const string Assess = "assess", Press = "press", Skim = "skim", Drill = "drill", Patrol = "patrol", Board = "board";
    public const string DeskFlag = "desk_business", DeskExposedFlag = "desk_exposed";
    public const int DeskRank = 2, PostingRank = 3, PressCredits = 6, SkimCredits = 6, DrillBy = 4;
    /// <summary>A Governor, a Minister or a High Treasurer: the administration's work is theirs.</summary>
    public static bool AtADesk(Character c) => c.Alive && c.CareerTrack == "stewardship" && c.CareerRank >= DeskRank && !c.Flags.Contains(LifeSystem.RetiredChar) && !c.Flags.Contains(DynastySystem.HiddenFlag);
    /// <summary>A Commander or above with no fleet under them this year.</summary>
    public static bool Ashore(World w, Character c) => c.Alive && c.CareerTrack == "military" && c.CareerRank >= PostingRank && !c.Flags.Contains(LifeSystem.RetiredChar) && !c.Flags.Contains(DynastySystem.HiddenFlag)
        && !w.Fleets.Values.Any(f => f.AdmiralId == c.Id);

    // the lowest ranks (docs/LADDER.md): a brief for an Aspirant or an Advocate, the ledgers for a Clerk or an Administrator, the
    // watches for a Cadet, an Ensign or a Lieutenant with no command. Small work, and how it is done is the junior's own.
    public const string Brief = "brief", Ledgers = "ledgers", Watch = "watch";
    public const string Plead = "plead", RichClient = "client", PoorClient = "poor", Diligent = "diligent", Useful = "useful", Pad = "pad", Watches = "watches", Volunteer = "volunteer", Mess = "mess";
    public const string JuniorCaughtFlag = "padded_account_found"; public const int ClientCredits = 4, PadCredits = 3;
    private static bool AtWork(Character c) => c.Alive && c.Age >= LifeSystem.Adult && !c.Flags.Contains(LifeSystem.RetiredChar) && !c.Flags.Contains(DynastySystem.HiddenFlag);
    /// <summary>The work a junior's rank gives them this year, or "" if they have none (no service, or a rank with work of its own).</summary>
    public static string JuniorKind(World w, Character c) => !AtWork(c) ? "" : c.CareerTrack switch
    {
        "law" when c.CareerRank < JudgeRank => Brief,
        "stewardship" when c.CareerRank < DeskRank => Ledgers,
        "military" when c.CareerRank < PostingRank && !w.Fleets.Values.Any(f => f.AdmiralId == c.Id) => Watch,
        _ => ""
    };
    public static bool Junior(string kind) => kind is Brief or Ledgers or Watch;

    // ---------------- the thread from year to year: who is above a junior (docs/LADDER.md) ----------------

    public const int StandingAt = 20, StandingOdds = 8, KinBenchOdds = 15, SpokenAfter = 3; public const string SpokenForFlag = "kin_spoken_for", SpokenForChar = "spoken_for";
    /// <summary>The ship a junior officer serves in: a fleet of their own house that somebody else commands; failing that one of
    /// their patron's; failing that one of the house that holds the capital, which is the Reach's own. One with a living
    /// commander before one without.</summary>
    public static Fleet? ShipOf(World w, Character c)
    {
        var house = w.House(c.HouseId); string patron = house?.PatronId ?? "", chair = RegionSystem.CapitalHolder(w)?.Id ?? "";
        int Near(Fleet f) => f.OwnerId == c.HouseId ? 0 : f.OwnerId == patron ? 1 : 2;
        string taken = PatronOf(w, c)?.Id ?? "";   // one who has been taken up serves where their patron commands
        return w.Fleets.Values.Where(f => f.AdmiralId != c.Id && f.Strength > 0 && (f.OwnerId == c.HouseId || (patron.Length > 0 && f.OwnerId == patron) || (chair.Length > 0 && f.OwnerId == chair && PolitySystem.InReach(w, c.HouseId)) || (taken.Length > 0 && f.AdmiralId == taken)))
            .OrderByDescending(f => taken.Length > 0 && f.AdmiralId == taken).ThenBy(Near).ThenByDescending(f => w.Char(f.AdmiralId) is { Alive: true }).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    /// <summary>The one above a junior, by name, and the same one year after year while they live and keep their place: the judge
    /// an advocate appears before, a clerk's senior, a junior officer's captain. One of their own house if it has one.</summary>
    public static Character? Above(World w, Character c)
    {
        string kind = JuniorKind(w, c); if (kind.Length == 0) return null;
        if (PatronOf(w, c) is { } patron) return patron;   // a patron is above them wherever either is posted
        if (kind == Watch) return ShipOf(w, c) is { } ship && w.Char(ship.AdmiralId) is { Alive: true } captain ? captain : null;
        return w.LivingCharacters().Where(x => x.Id != c.Id && (kind == Brief ? OnTheBench(x) : AtADesk(x)) && PolitySystem.InReach(w, x.HouseId) && !FellOut(c, x))
            .OrderByDescending(x => x.HouseId == c.HouseId).ThenBy(x => Hash(c.Id + ":above:" + x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    /// <summary>What the one above them thinks of them: the whole of a junior's standing.</summary>
    public static int Standing(World w, Character junior) => Above(w, junior) is { } above ? w.GetRelationship(above.Id, junior.Id)?.Disposition ?? 0 : 0;
    private static void Noticed(World w, OfficeBusiness b, Character who, int by)
    {
        if (w.Char(b.SubjectId) is not { Alive: true } above || above.Id == who.Id) return;
        var r = w.Relationship(above.Id, who.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100);
        if (by > 0) w.WorldCounters[UnderKey(who.Id, above.Id)] = w.Counter(UnderKey(who.Id, above.Id)) + 1;   // a year's work under them that they had no fault to find with
    }
    public static string UnderKey(string juniorId, string aboveId) => $"served_under:{juniorId}:{aboveId}";
    /// <summary>One whose senior has come to think well of them, and has had three good years' work of them, is spoken for, once at
    /// each rank, and goes up a step sooner than the years alone would have taken them: never faster than the service allows anybody.</summary>
    private static void SpokenFor(World w, OfficeBusiness b, Character who, bool honest = true)
    {
        int was = who.CareerRank;
        if (w.Char(b.SubjectId) is { Alive: true } above && above.Id != who.Id) { Patronage(w, above, who); Speak(w, above, who); }
        if (honest && who.CareerRank == was) Served(w, who);   // a year of padded accounts is not a year's work
    }
    /// <summary>Years of the year's work done at a step, for the one the player is (docs/UPSTART.md, "The service").</summary>
    public static string ServedKey(string characterId, int rank) => $"served_at:{characterId}:{rank}";
    public const int ServedYears = 4; public const string ServedCounter = "juniors_risen_by_years";
    /// <summary>The years alone: everybody else in a service goes up a step every so often whatever they do
    /// (<see cref="DynastySystem"/>), and the player's own person was the one exception, so that one who began at the bottom and was
    /// never spoken for stayed a cadet for life and could stand for nothing. A junior who does the year's work goes up after
    /// <see cref="ServedYears"/> years of it at a step, never faster than the service allows anybody.</summary>
    private static void Served(World w, Character who)
    {
        if (who.Id != w.ProtagonistId) return;
        string key = ServedKey(who.Id, who.CareerRank); int years = w.Counter(key) + 1; w.WorldCounters[key] = years;
        if (years < ServedYears || who.CareerRank >= (who.Age - DynastySystem.AgeOfMajority) / DynastySystem.YearsPerRank) return;
        w.WorldCounters.Remove(key); who.CareerRank++; Bump(w, ServedCounter);
        LifeSystem.Note(w, who, "office", $"Went up a step after {years} years' work.");
        w.Log($"{who.Name} has done {years} years' work at the bottom of the table, and goes up a step in the service: it is the years that did it, and nobody's word.");
    }
    private static void Speak(World w, Character above, Character who)
    {
        if ((w.GetRelationship(above.Id, who.Id)?.Disposition ?? 0) < StandingAt) return;
        if (w.Counter(UnderKey(who.Id, above.Id)) < SpokenAfter) return;   // it is the work they have seen, not the name
        if (who.CareerRank >= (who.Age - DynastySystem.AgeOfMajority) / DynastySystem.YearsPerRank) return;
        who.Life ??= new Life(); if (!who.Life.Marks.Add("spoken_for:" + who.CareerRank)) return;
        who.CareerRank++; Bump(w, SpokenCounter); LifeSystem.Note(w, who, "office", $"Was spoken for by {above.Name}, and went up a step.");
        if (who.HouseId == w.Protagonist?.HouseId)
        {
            w.Log($"{above.Name} speaks for {who.Name}, who goes up a step in the service sooner than the years alone would have taken them.");
            if (who.Id != w.ProtagonistId) { who.Flags.Add(SpokenForChar); Mark(w, SpokenForFlag); }
        }
    }

    /// <summary>Which kind of charge an office of the Reach carries, or "" if it carries none (the Admiral has the fleets).</summary>
    public static string ChargeOf(OfficeDef? def) => def == null ? "" : def.Elected ? Consul : def.Sphere == "treasury" ? Treasurer : def.Sphere == "senate" ? Speaker : "";
    public static string[] ChoicesOf(OfficeCatalog offices, OfficeBusiness b) => b.Kind != Charge ? DutyChoices(b.Kind) : ChargeOf(offices.Def(b.RungId)) switch
    {
        Consul => new[] { Account, Sweep, Party }, Treasurer => new[] { Balance, Remit, Contracts }, _ => new[] { Fair, Own, Silence }
    };
    private static string? Loosest(World w) => w.Regions.Where(kv => kv.Value.Allegiance < 60).OrderBy(kv => kv.Value.Allegiance).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault();
    private static string? MostLawless(World w) => w.Regions.Where(kv => kv.Value.Lawlessness >= 20).OrderByDescending(kv => kv.Value.Lawlessness).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault();
    /// <summary>The house a Speaker would keep off the floor: the one whose head and the Speaker's think worst of each other.</summary>
    private static House? Enemy(World w, House? of) => of == null ? null : w.Houses.Values.Where(h => h.Id != of.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null)
        .Select(h => (h, bad: Regard(w, of, h) + Regard(w, h, of))).Where(x => x.bad <= -30).OrderBy(x => x.bad).ThenBy(x => x.h.Id, StringComparer.Ordinal).Select(x => x.h).FirstOrDefault();

    /// <summary>One who sits on the bench by their career: a Judge, a Justice, a High Justice.</summary>
    public static bool OnTheBench(Character c) => c.Alive && c.CareerTrack == "law" && c.CareerRank >= JudgeRank && !c.Flags.Contains(LifeSystem.RetiredChar) && !c.Flags.Contains(DynastySystem.HiddenFlag);
    public static OfficeBusiness? DutyOf(World w, string characterId, params string[] kinds) => w.OfficeWork.FirstOrDefault(b => b.HolderId == characterId && b.Choice.Length == 0 && kinds.Contains(b.Kind));
    public static string[] DutyChoices(string kind) => kind == Hearing ? new[] { Law, ForHouse, ForEstate } : kind == Appeal ? new[] { Overturn, SendBack, Uphold } : kind == Desk ? new[] { Assess, Press, Skim }
        : kind == Posting ? new[] { Drill, Patrol, Board } : kind == Brief ? new[] { Plead, RichClient, PoorClient } : kind == Ledgers ? new[] { Diligent, Useful, Pad }
        : kind == Watch ? new[] { Watches, Volunteer, Mess } : new[] { Audit, SpeakRegion, Whip, CommitteeWork };
    private static bool Took(OfficeBusiness b) => b.Choice is Favour or Gift;
    private static string EstateName(PolicyCatalog? policy, string id) => policy?.Estates.FirstOrDefault(e => e.Id == id)?.Name ?? "the petitioners";

    /// <summary>The year's duties: bent rulings go to appeal, the judges of the player's house are given hearings, the player's seat
    /// in the Senate has its year, and one senator of another house audits the accounts.</summary>
    private static void Duties(World w, OfficeCatalog offices, PolicyCatalog? policy)
    {
        var p = w.Protagonist; var mine = p != null ? w.House(p.HouseId) : null; var busy = new HashSet<string>();

        // 1. the appeal: a ruling that went against the plain law last year goes before a Justice who is of none of the houses in it
        foreach (var bent in w.OfficeWork.Where(b => b.Kind == Case && b.Turn == w.Turn - 1 && b.WinnerId.Length > 0 && b.WinnerId != b.RightId && !b.Seen && WeightOf(b) != Doubtful).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            bent.Seen = true; var praetor = w.Char(bent.HolderId); if (praetor == null) continue;
            var involved = new[] { bent.HouseA, bent.HouseB, praetor.HouseId };
            var justices = w.LivingCharacters().Where(c => OnTheBench(c) && c.CareerRank >= JusticeRank && !involved.Contains(c.HouseId) && !busy.Contains(c.Id) && PolitySystem.InReach(w, c.HouseId)).ToList();
            var justice = justices.Where(c => c.HouseId == mine?.Id).OrderByDescending(c => c.Id == p?.Id).ThenByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
                ?? justices.OrderBy(c => Hash(c.Id + ":appeal:" + bent.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (justice == null) continue;
            busy.Add(justice.Id);
            var a = new OfficeBusiness { Id = $"appeal_{w.Turn}_{justice.Id}", RungId = "bench", HolderId = justice.Id, Kind = Appeal, Turn = w.Turn, HouseA = bent.RightId, HouseB = bent.WinnerId, RightId = bent.RightId, SubjectId = praetor.Id, Stake = bent.Stake, Matter = bent.Matter, Weight = bent.Weight };
            w.OfficeWork.Add(a);
            if (p == null || justice.Id != p.Id) DecideDuty(w, offices, policy, justice, Appeal, DutyInclination(w, justice, a));
        }
        // 1b. the charge of each office of the Reach: its holder's own to decide, the player's left open
        foreach (var held in w.Offices.OrderBy(o => o.OfficeId, StringComparer.Ordinal).ToList())
        {
            var def = offices.Def(held.OfficeId); var holder = w.Char(held.CharacterId); if (ChargeOf(def).Length == 0 || holder is not { Alive: true } || w.IsImperium) continue;
            var c = new OfficeBusiness { Id = $"charge_{w.Turn}_{held.OfficeId}", RungId = held.OfficeId, HolderId = holder.Id, Kind = Charge, Turn = w.Turn };
            c.RegionId = ChargeOf(def) == Consul ? MostLawless(w) ?? "" : ChargeOf(def) == Treasurer ? Loosest(w) ?? "" : "";
            if (ChargeOf(def) == Speaker) c.HouseA = Enemy(w, w.House(holder.HouseId))?.Id ?? "";
            if (ChargeOf(def) == Treasurer) DrawBudget(w, c);
            w.OfficeWork.Add(c);
            if (p == null || holder.Id != p.Id) DecideDuty(w, offices, policy, holder, Charge, ChargeInclination(w, offices, holder, c));
        }
        if (p is not { Alive: true } || mine == null) return;

        // 2. a hearing for each of the house's people on the bench who has no appeal before them and is not a sitting praetor
        var estates = policy?.Estates.OrderBy(e => e.Id, StringComparer.Ordinal).ToList() ?? new();
        var others = w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (estates.Count > 0 && others.Count > 0 && PolitySystem.InReach(w, mine.Id))
            foreach (var j in w.LivingCharacters().Where(c => c.HouseId == mine.Id && OnTheBench(c) && !busy.Contains(c.Id) && Seat(w, c.Id)?.OfficeId != Praetor).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
            {
                string id = $"hearing_{w.Turn}_{j.Id}"; var house = others[(int)(Hash(id) % (uint)others.Count)]; var estate = estates[(int)(Hash(id + ":estate") % (uint)estates.Count)];
                var h = new OfficeBusiness { Id = id, RungId = "bench", HolderId = j.Id, Kind = Hearing, Turn = w.Turn, HouseA = house.Id, EstateId = estate.Id, RightId = Hash(id + ":right") % 2 == 0 ? house.Id : Estate };
                w.OfficeWork.Add(h);
                if (j.Id != p.Id) DecideDuty(w, offices, policy, j, Hearing, DutyInclination(w, j, h));
            }

        // 2b. a desk for each of the house's people who has a rank in the administration, and a posting for each officer of rank
        //     who has no fleet under them this year
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && (AtADesk(c) || Ashore(w, c) || JuniorKind(w, c).Length > 0)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            string kind = AtADesk(c) ? Desk : Ashore(w, c) ? Posting : JuniorKind(w, c);
            var d = new OfficeBusiness { Id = $"{kind}_{w.Turn}_{c.Id}", RungId = kind, HolderId = c.Id, Kind = kind, Turn = w.Turn, RegionId = HomeRegion(w, mine.Id) ?? "" };
            if (kind == Brief && others.Count >= 2) { int i = (int)(Hash(d.Id) % (uint)others.Count); d.HouseA = others[i].Id; d.HouseB = others[(i + 1) % others.Count].Id; }   // the client, and the other side
            if (Junior(kind)) { d.SubjectId = Above(w, c)?.Id ?? ""; if (kind == Watch) d.RightId = ShipOf(w, c)?.Id ?? ""; }
            w.OfficeWork.Add(d);
            if (c.Id != p.Id) DecideDuty(w, offices, policy, c, kind, DeskInclination(w, offices, c, d));
        }

        // 3. the seat: the head's year in the Senate
        //    a house with no seat has no senator's year: its head's year is the work of their service (docs/UPSTART.md)
        if (PolitySystem.InReach(w, mine.Id) && OrderSystem.Seated(w, mine) && DutyOf(w, p.Id, Charge) == null)   // one who holds an office of the Reach spends the year on its charge
            w.OfficeWork.Add(new OfficeBusiness { Id = $"seat_{w.Turn}_{p.Id}", RungId = "senate", HolderId = p.Id, Kind = SeatKind, Turn = w.Turn, RegionId = HomeRegion(w, mine.Id) ?? "" });

        // 4. somebody else reads the accounts too: one upright senator a year, and what was taken last year may come out through them
        var auditors = others.Where(h => Economy.HeadOf(w, h) is { } head && (head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just"))).ToList();
        if (auditors.Count > 0)
        {
            var auditor = auditors[(int)(Hash("auditor:" + w.Turn) % (uint)auditors.Count)];
            var found = w.OfficeWork.Where(b => b.Turn == w.Turn - 1 && Took(b) && !b.Seen && Seat(w, b.HolderId) != null && w.Char(b.HolderId) is { } t && t.HouseId != auditor.Id).OrderBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
            if (found != null)
            {
                found.Seen = true;
                if (Hash(found.Id + ":audit") % 2 == 0)
                {
                    var thief = w.Char(found.HolderId)!;
                    w.Log($"{Economy.HeadOf(w, auditor)?.Name} of {auditor.Name}, reading the year's accounts in the Senate, follows the money to {thief.Name}.");
                    Expose(w, offices, thief, found); MoveTrust(w, 2);
                }
            }
        }
    }

    /// <summary>What it is, said to the one who must decide it.</summary>
    public static string DutySays(World w, PolicyCatalog? policy, OfficeBusiness b)
    {
        string a = w.House(b.HouseA)?.Name ?? "a house", bn = w.House(b.HouseB)?.Name ?? "a house";
        return b.Kind switch
        {
            Hearing => $"{EstateName(policy, b.EstateId)} have brought {a} before your bench. The law, read plainly, is with {(b.RightId == Estate ? EstateName(policy, b.EstateId).ToLowerInvariant() : a)}.",
            Appeal => $"{a} appeals against the ruling {w.Char(b.SubjectId)?.Name ?? "a praetor"} gave for {bn} last year. On the plain reading of the law, {a} was in the right.",
            Charge => ChargeSays(w, b),
            Desk => "The year's returns from the house's region cross your desk. How they are assessed, and how hard they are collected, is the administration's to say, and this year the administration is you.",
            Posting => "You have a rank and no fleet under you this year. The service finds work for officers ashore, and lets one of your standing choose it.",
            Brief => (b.HouseA.Length > 0 ? $"A brief has come to you: {a} against {bn}, before a lesser court. It is small, and it is yours." : "A brief has come to you, before a lesser court. It is small, and it is yours.") + AboveSays(w, b),
            Ledgers => "A year of the administration's ledgers is yours to keep. Nobody above you reads them unless something is wrong." + AboveSays(w, b),
            Watch => "You are a junior officer with no command. The year is watches, drills, and whatever you make of them." + AboveSays(w, b),
            _ => "The Senate sits, and a year of it is yours to spend. The motions are the chamber's business; this is your own."
        };
    }
    private static string ChargeSays(World w, OfficeBusiness b) => b.RungId.StartsWith("consul")
        ? "The year is in your name. The magistrates answer to you, the Reach's own fleet sails on your word, and your bench expects something of it."
        : b.RungId == "treasurer" ? "The Reach's budget for the year is yours to draw up: what is saved, what is forgiven, and who is given the work." + BudgetSays(b)
        : "The order of business is yours: what the Senate hears, when, and what it never reaches.";
    /// <summary>What a relative does with a desk or a posting: by their nature.</summary>
    public static string DeskInclination(World w, OfficeCatalog offices, Character c, OfficeBusiness b)
    {
        bool honest = c.NatureTraits.Contains("Honorable") || c.NatureTraits.Contains("Just") || c.NatureTraits.Contains("Loyal");
        bool grasping = !honest && (c.NatureTraits.Contains("Ruthless") || c.NatureTraits.Contains("Cruel") || c.Ambition == "amass_a_fortune");
        var choices = DutyChoices(b.Kind);
        if (b.Kind == Brief) return grasping || c.Ambition == "amass_a_fortune" ? RichClient : !honest && Hash(b.Id + ":poor") % 3 == 0 ? PoorClient : Plead;
        if (b.Kind == Ledgers) return grasping && Hash(b.Id + ":take") % 2 == 0 ? Pad : c.NatureTraits.Contains("Ambitious") || c.Ambition == "master_the_senate" ? Useful : Diligent;
        if (b.Kind == Watch) return c.NatureTraits.Contains("Proud") || c.Ambition is "command_a_fleet" or "earn_a_command" ? Volunteer : choices[(int)(Hash(b.Id + ":watch") % 3)];
        if (b.Kind == Desk) return grasping && Hash(b.Id + ":take") % 2 == 0 ? Skim : honest || Hash(b.Id + ":press") % 2 == 0 ? Assess : Press;
        foreach (var ch in new[] { choices[(int)(Hash(b.Id + ":posting") % 3)], Drill, Board }) if (DutyRefusal(w, offices, c, Posting, ch) == null) return ch;
        return Board;
    }

    /// <summary>What an office-holder other than the player does with the year's charge: by their nature.</summary>
    public static string ChargeInclination(World w, OfficeCatalog offices, Character c, OfficeBusiness b)
    {
        bool honest = c.NatureTraits.Contains("Honorable") || c.NatureTraits.Contains("Just");
        bool grasping = !honest && (c.NatureTraits.Contains("Ruthless") || c.NatureTraits.Contains("Cruel") || c.NatureTraits.Contains("Ambitious") || c.Ambition == "amass_a_fortune");
        var choices = ChoicesOf(offices, b); bool second = DutyRefusal(w, offices, c, Charge, choices[1]) == null, third = DutyRefusal(w, offices, c, Charge, choices[2]) == null;
        if (grasping && third && Hash(b.Id + ":take") % 2 == 0) return choices[2];
        if (second && (honest ? Hash(b.Id + ":second") % 2 == 0 : Hash(b.Id + ":second") % 3 != 0)) return choices[1];
        return choices[0];
    }
    public static string DutyChoiceSays(World w, PolicyCatalog? policy, OfficeBusiness b, string choice)
    {
        string a = w.House(b.HouseA)?.Name ?? "the house", bn = w.House(b.HouseB)?.Name ?? "the house", estate = EstateName(policy, b.EstateId).ToLowerInvariant();
        var f = b.Kind == SeatKind && w.Char(b.HolderId) is { } me ? FactionSystem.Of(w, me.HouseId) : null;
        return choice switch
        {
            Law => b.RightId == Estate ? $"Rule on the law, for {estate}. They think the better of your house; {a} thinks the less." : $"Rule on the law, for {a}. It thinks the better of your house; {estate} think the less.",
            ForHouse => $"Find for {a}, whatever the law says. It will remember it, and so will {estate}." + (b.RightId == Estate ? " The law is not with them, and it will be seen." : ""),
            ForEstate => $"Find for {estate}, whatever the law says. The squares like a judge who does." + (b.RightId == Estate ? "" : " The law is not with them; nobody in the squares will mind."),
            Overturn => $"Overturn it. {bn} pays back {Money.Credits(StakeOf(b) * 2)} to {a}; {a} will not forget it, and neither will {bn} or the praetor's house. The magistrates are trusted more.",
            SendBack => $"Send it back to be heard again. Nothing is settled; {a} is at least heard.",
            Uphold => $"Uphold the praetor. His house and {bn} are obliged to you; {a} is not, and the magistrates are trusted less.",
            Audit => "Read the year's accounts. If a magistrate took money this year you may follow it to them: they are removed, and their house will hate you for it.",
            SpeakRegion => b.RegionId.Length > 0 ? $"Speak for {w.Galaxy?.Region(b.RegionId)?.Name}: it holds a little closer to the Reach, its houses think the better of you, and your house is thought the better of." : "Your house has no worlds to speak for.",
            Plead => "Plead it on the law. You learn the law and something of speaking; if you win, your client's house thinks the better of yours.",
            RichClient => $"Take the side that pays: {Money.Credits(ClientCredits)} to your own purse, and the other house thinks the less of yours.",
            PoorClient => "Plead for somebody who cannot pay. Nothing in it but practice at speaking, and the commons think the better of your house.",
            Diligent => "Keep them exactly. You learn the administration, and the house is a hundred credits the better for it.",
            Useful => "Make yourself useful to those above you. A little influence, a little of how money moves, and less time on the ledgers.",
            Pad => $"Pad an account: {Money.Credits(PadCredits)} to your own purse. One time in five it is found, and it is written against you.",
            Watches => "Stand your watches. You learn your trade.",
            Volunteer => "Volunteer for the patrol. You learn gunnery, the home lanes are a little safer, and one time in ten you come back hurt.",
            Mess => "Make friends in the mess. The officers think the better of your house, the crew you serve with is a little steadier, and you learn something of leading.",
            Assess => "Assess it fairly. The merchants think the better of your house" + (b.RegionId.Length > 0 ? $", {w.Galaxy?.Region(b.RegionId)?.Name} holds a little closer" : "") + ", and the magistrates are trusted a little more.",
            Press => $"Press the collection: {Money.Credits(PressCredits)} more for the house. The commons think the less of it" + (b.RegionId.Length > 0 ? $", and {w.Galaxy?.Region(b.RegionId)?.Name} holds a little less closely." : "."),
            Skim => $"Keep a share: {Money.Credits(SkimCredits)} to your own purse. One time in four the returns are reopened, and you are broken a grade.",
            Drill => $"Drill the house's crews. Every fleet of the house is {DrillBy} the more loyal.",
            Patrol => b.RegionId.Length > 0 ? $"Take a squadron of the house's round the lanes of {w.Galaxy?.Region(b.RegionId)?.Name}: lawlessness there falls by {ReliefBy}." : "The house has no home lanes to patrol.",
            Board => "Sit on a board of inquiry. The officers think the better of your house, the magistrates are trusted a little more, and you gain a little influence.",
            Account => "Call the magistrates to account. Whatever was taken this year is looked for, and what is found is punished. The magistrates are trusted more, and nobody who sits thanks you.",
            Sweep => b.RegionId.Length > 0 ? $"Send the Reach's fleet onto the lanes of {w.Galaxy?.Region(b.RegionId)?.Name}: lawlessness there falls by {SweepBy}, for {Money.Credits(SweepCredits)} of the capital's, and its houses remember it." : "No region is lawless enough to need the fleet.",
            Party => "Spend the year on your bench. Its houses are obliged to you and you gain influence; the magistrates are trusted a little less for it.",
            Balance => BudgetOf(b) is { Surplus: <= 0 } ? $"Balance the books: spend only what came in. Nothing is banked, and the Reach's own fleets go short ({FleetShort} readiness each). Your house is thought the better of."
                : $"Balance the books. {Money.Credits(BalanceSum(b))} are banked for the capital's purse, and your house is thought the better of.",
            Remit => b.RegionId.Length > 0 ? $"Forgive {w.Galaxy?.Region(b.RegionId)?.Name} its levy for the year: it holds closer to the Reach (allegiance +{RemitBy}) for the {Money.Credits(RemitSum(b))} its houses would have paid, out of the capital's purse, and its houses remember it." : "No region holds loosely enough to need it.",
            Contracts => $"Let the Reach's contracts to your own house: {Money.Credits(ContractSum(b))}. The magistrates are trusted less, and one time in four it comes out and the office is taken away.",
            Fair => "Keep the order of business fair. Every bench's leader thinks a little better of you, and so does the Reach.",
            Own => "Bring your own bench's business forward. You gain influence and its leader is obliged; the other benches' leaders are not pleased.",
            Silence => b.HouseA.Length > 0 ? $"Give {w.House(b.HouseA)?.Name} no day in the Senate: whatever they have moved is held over a year. They will hate you for it; those who hate them will not." : "You have no enemy worth the trouble.",
            CommitteeWork => w.Char(b.HolderId) is { } sen && w.House(sen.HouseId) is { } senHouse ? $"Do the work of {CommitteeName(CommitteeOf(w, senHouse))}. Your standing with it rises a point; you learn its business, and it does the Reach a little good."
                : "Do the committee's work.",
            Whip => f == null ? "Your house sits on no bench." : f.LeaderHouseId == w.Char(b.HolderId)?.HouseId ? $"Hold {f.Name} together: every house on your bench thinks a little better of you." : $"Work for {f.Name}: its leader is obliged to you, and you gain a little influence.",
            _ => ""
        };
    }

    public static string? DutyRefusal(World w, OfficeCatalog offices, Character? who, string kind, string choice)
    {
        var b = who != null ? DutyOf(w, who.Id, kind) : null; if (who?.Alive != true || b == null) return "nothing of the kind is waiting";
        if (!ChoicesOf(offices, b).Contains(choice)) return "not a thing to be done with it";
        if (choice is Sweep or Remit && b.RegionId.Length == 0) return choice == Sweep ? "no region is lawless enough to need the fleet" : "no region holds loosely enough to need it";
        if (choice is Sweep or Remit && (RegionSystem.CapitalHolder(w)?.Treasury.Credits ?? 0) < (choice == Sweep ? SweepCredits : RemitSum(b))) return "the capital's purse does not hold it";
        if (choice == Patrol && b.RegionId.Length == 0) return "the house has no home lanes to patrol";
        if (choice is Patrol or Drill && FleetSystem.StrengthOf(w, who.HouseId) <= 0) return "the house has no fleet";
        if (choice == Party && FactionSystem.Of(w, who.HouseId) == null) return "your house sits on no bench";
        if (choice == Silence && b.HouseA.Length == 0) return "you have no enemy worth the trouble";
        if (choice == SpeakRegion && b.RegionId.Length == 0) return "your house has no worlds to speak for";
        if (choice == Whip && FactionSystem.Of(w, who.HouseId) == null) return "your house sits on no bench";
        return null;
    }

    /// <summary>The holder decides.</summary>
    public static void DecideDuty(World w, OfficeCatalog offices, PolicyCatalog? policy, Character who, string kind, string choice)
    {
        TrustOf(w, DutyOf(w, who.Id, kind)!, who); try { DecideDutyIt(w, offices, policy, who, kind, choice); } finally { TrustDone(); }
    }
    private static void DecideDutyIt(World w, OfficeCatalog offices, PolicyCatalog? policy, Character who, string kind, string choice)
    {
        var b = DutyOf(w, who.Id, kind)!; b.Choice = choice; var house = w.House(who.HouseId); var p = w.Protagonist; var mine = p?.HouseId; bool ours = who.HouseId == mine;
        void Legit(int by) { if (house != null && (by < 0 || house.Legitimacy < 80)) house.Legitimacy = Math.Clamp(house.Legitimacy + by, 0, 100); }
        void Approve(int by) => PolicySystem.Sway(w, who.HouseId, b.EstateId, by);
        switch (kind)
        {
            case Hearing:
            {
                var other = w.House(b.HouseA); bool lawForHouse = b.RightId != Estate; bool forHouse = choice == ForHouse || (choice == Law && lawForHouse);
                Shift(w, other, house, choice == Law ? (forHouse ? 5 : -5) : forHouse ? 8 : -8);
                Approve(choice == Law ? (forHouse ? -3 : 4) : forHouse ? -6 : 6);
                if (choice == Law) MoveTrust(w, 1); else if (forHouse != lawForHouse && forHouse) MoveTrust(w, -1);   // a lesser court: a ruling bent for a great house is talked of; one bent for the squares is cheered
                if (who.Life?.Marks.Add("bench:" + (choice == Law ? "law" : "bent")) != false) LifeSystem.Note(w, who, "office", choice == Law ? "Sat as a judge and ruled on the law." : "Sat as a judge and ruled as it suited.");
                if (ours && who.Id == p!.Id) w.Log($"{who.Name} finds for {(forHouse ? other?.Name : EstateName(policy, b.EstateId).ToLowerInvariant())}{(forHouse == lawForHouse ? "" : ", against the plain reading of the law")}.");
                break;
            }
            case Appeal:
            {
                var wronged = w.House(b.HouseA); var winner = w.House(b.HouseB); var praetor = w.Char(b.SubjectId); var bench = praetor != null ? w.House(praetor.HouseId) : null;
                if (choice == Overturn)
                {
                    if (wronged != null && winner != null) { int back = Math.Min(StakeOf(b) * 2, Math.Max(0, winner.Treasury.Credits)); winner.Treasury.Credits -= back; wronged.Treasury.Credits += back; }
                    Shift(w, wronged, house, 10); Shift(w, winner, house, -8); Shift(w, bench, house, -8); MoveTrust(w, 3); Legit(1);
                    if (b.HouseA == mine && !ours) Mark(w, OverturnedForUsFlag);
                    if (ours || b.HouseA == mine || b.HouseB == mine || praetor?.HouseId == mine)
                    {
                        w.Log($"{who.Name}, justice, overturns {praetor?.Name ?? "the praetor"}'s ruling for {winner?.Name}. {winner?.Name} pays {wronged?.Name} back, and the ruling is struck from the rolls.");
                        if (b.HouseA == mine || b.HouseB == mine) w.Tell(b.HouseA == mine ? "good" : "warn", b.HouseA == mine ? $"{who.Name}, justice, has overturned the ruling against you: {winner?.Name} pays you back" : $"{who.Name}, justice, has overturned the ruling you won: you pay {wronged?.Name} back", "senate:ladder");
                    }
                    if (who.Life?.Marks.Add("bench:overturn") != false) LifeSystem.Note(w, who, "office", "Sat as a justice and overturned a bent ruling.");
                }
                else if (choice == Uphold) { Shift(w, bench, house, 8); Shift(w, winner, house, 4); Shift(w, wronged, house, -10); MoveTrust(w, -2); }
                else { Shift(w, wronged, house, 3); MoveTrust(w, 1); }
                break;
            }
            case Desk:
            {
                void Estate_(string id, int by) => PolicySystem.Sway(w, who.HouseId, id, by);
                if (choice == Assess) { Estate_("merchants", 2); MoveTrust(w, 1); if (b.RegionId.Length > 0) { var rs = RegionSystem.StateOf(w, b.RegionId); rs.Allegiance = Math.Clamp(rs.Allegiance + 1, 0, 100); } }
                else if (choice == Press)
                {
                    if (house != null) house.Treasury.Credits += PressCredits; Estate_("commons", -4);
                    if (b.RegionId.Length > 0) { var rs = RegionSystem.StateOf(w, b.RegionId); rs.Allegiance = Math.Clamp(rs.Allegiance - 1, 0, 100); }
                }
                else
                {
                    who.Purse += SkimCredits; MoveTrust(w, -2);
                    if (Hash(b.Id + ":exposed") % 4 == 0)
                    {
                        who.CareerRank = Math.Max(0, who.CareerRank - 1); if (house != null) house.Legitimacy = Math.Clamp(house.Legitimacy - 3, 0, 100);
                        LifeSystem.Note(w, who, "office", "Was broken a grade for keeping a share of the returns.");
                        w.Log($"The year's returns are reopened, and {who.Name} of {house?.Name} is found to have kept a share. {who.Name} is broken a grade.");
                        if (ours && who.Id == p!.Id) Mark(w, DeskExposedFlag);
                    }
                }
                if (choice != Skim && who.Life?.Marks.Add("desk") != false) LifeSystem.Note(w, who, "office", "Had the year's returns across their desk.");
                break;
            }
            case Brief:
            {
                if (choice == Plead)
                {
                    EducationSystem.Gain(w, who, "law", 2); EducationSystem.Gain(w, who, "oratory", 1);
                    // the judge they appear before: one who thinks well of them hears them the more readily, and one of their own
                    // house who is not upright more readily still, which is seen
                    var judge = w.Char(b.SubjectId); int standing = judge is { Alive: true } ? Math.Clamp((w.GetRelationship(judge.Id, who.Id)?.Disposition ?? 0) / 5, -StandingOdds, StandingOdds) : 0;
                    bool partial = judge is { Alive: true } && judge.HouseId == who.HouseId && !judge.NatureTraits.Contains("Honorable") && !judge.NatureTraits.Contains("Just");
                    int roll = (int)(Hash(b.Id + ":won") % 100), plain = (who.Skill("oratory") + who.Skill("law")) * 8 + 30 + standing;
                    bool won = plain + (partial ? KinBenchOdds : 0) > roll;
                    if (won && plain <= roll) { MoveTrust(w, -1); if (ours) w.Log($"{who.Name} wins a case before {judge!.Name}, of the same house. It is remarked on."); }
                    Noticed(w, b, who, won ? 2 : 1);
                    if (won) { Shift(w, w.House(b.HouseA), house, 4); if (who.Life?.Marks.Add("brief:won") != false) LifeSystem.Note(w, who, "office", "Won a case as an advocate."); }
                    else who.Stress = Math.Min(100, who.Stress + 2);
                    if (ours && who.Id == p!.Id) w.Log(won ? $"{who.Name} pleads for {w.House(b.HouseA)?.Name} and wins." : $"{who.Name} pleads for {w.House(b.HouseA)?.Name} and loses. It was well argued.");
                }
                else if (choice == RichClient) { who.Purse += ClientCredits; EducationSystem.Gain(w, who, "oratory", 1); Shift(w, w.House(b.HouseB), house, -3); Noticed(w, b, who, -2); }
                else { PolicySystem.Sway(w, who.HouseId, "commons", 2); EducationSystem.Gain(w, who, "oratory", 2); Noticed(w, b, who, 1); }
                SpokenFor(w, b, who);
                break;
            }
            case Ledgers:
            {
                if (choice == Diligent) { EducationSystem.Gain(w, who, "administration", 2); if (house != null) house.Treasury.Credits += 1; Noticed(w, b, who, 2); }
                else if (choice == Useful) { Noticed(w, b, who, 4); EducationSystem.Gain(w, who, "economics", 1); if (who.Id == p?.Id) w.PoolsFor(who.Id).Influence += 1; else if (p != null) { var r = w.Relationship(p.Id, who.Id); r.Disposition = Math.Clamp(r.Disposition + 2, -100, 100); } }
                else
                {
                    who.Purse += PadCredits;
                    if (Hash(b.Id + ":found") % 5 == 0)
                    {
                        AmbitionSystem.Setback(w, who, "Was reprimanded for a padded account."); if (ours && who.Id == p!.Id) Mark(w, JuniorCaughtFlag);
                        Noticed(w, b, who, -8);
                        if (ours) w.Log($"{who.Name}'s ledgers are gone through, and an account is found padded. It is written against them.");
                    }
                }
                SpokenFor(w, b, who, choice != Pad);
                break;
            }
            case Watch:
            {
                Noticed(w, b, who, choice == Watches ? 2 : choice == Volunteer ? 3 : 1);
                if (choice == Watches) EducationSystem.Gain(w, who, "tactics", 2);
                else if (choice == Volunteer)
                {
                    EducationSystem.Gain(w, who, "gunnery", 2);
                    if (b.RegionId.Length > 0) { var rs = RegionSystem.StateOf(w, b.RegionId); rs.Lawlessness = Math.Clamp(rs.Lawlessness - 1, 0, 100); }
                    if (Hash(b.Id + ":hurt") % 10 == 0) LifeSystem.Wound(w, who, "on patrol in the home lanes");
                }
                else
                {
                    PolicySystem.Sway(w, who.HouseId, "officers", 1); EducationSystem.Gain(w, who, "leadership", 1);
                    if ((w.Fleets.GetValueOrDefault(b.RightId) ?? w.Fleets.Values.Where(f => f.OwnerId == who.HouseId).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault()) is { } crew) crew.Loyalty = Math.Min(100, crew.Loyalty + 2);
                }
                SpokenFor(w, b, who);
                break;
            }
            case Posting:
            {
                if (choice == Drill) foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == who.HouseId).OrderBy(f => f.Id, StringComparer.Ordinal)) f.Loyalty = Math.Min(100, f.Loyalty + DrillBy);
                else if (choice == Patrol) { var rs = RegionSystem.StateOf(w, b.RegionId); rs.Lawlessness = Math.Clamp(rs.Lawlessness - ReliefBy, 0, 100); }
                else { PolicySystem.Sway(w, who.HouseId, "officers", 2); MoveTrust(w, 1); w.PoolsFor(who.Id).Influence += 1; }
                if (ours && who.Id == p!.Id) w.Log(choice == Drill ? $"{who.Name} spends the year drilling the house's crews." : choice == Patrol ? $"{who.Name} takes a squadron round the lanes of {w.Galaxy?.Region(b.RegionId)?.Name}." : $"{who.Name} sits on a board of inquiry.");
                break;
            }
            case Charge:
            {
                var chair = RegionSystem.CapitalHolder(w); var def = offices.Def(b.RungId); var bench = FactionSystem.Of(w, who.HouseId); string title = def?.Name ?? "the office";
                void Purse(int by) { if (chair != null) chair.Treasury.Credits = Math.Max(0, chair.Treasury.Credits + by); }
                void Leaders(int own, int rest) { foreach (var f in w.Factions.Where(f => f.LeaderHouseId != who.HouseId)) Shift(w, w.House(f.LeaderHouseId), house, f.Id == bench?.Id ? own : rest); }
                switch (choice)
                {
                    case Account:
                        MoveTrust(w, 2); Legit(1); int caught = 0;
                        foreach (var x in w.OfficeWork.Where(x => x.Turn == b.Turn && Took(x) && !x.Seen && Seat(w, x.HolderId) != null && w.Char(x.HolderId) is { } t && t.HouseId != who.HouseId).OrderBy(x => x.Id, StringComparer.Ordinal).Take(2).ToList())
                        {
                            x.Seen = true; if (Hash(x.Id + ":account") % 2 != 0) continue;
                            var thief = w.Char(x.HolderId)!; w.Log($"{who.Name}, {title.ToLowerInvariant()}, calls the magistrates to account, and {thief.Name} cannot answer.");
                            Expose(w, offices, thief, x); MoveTrust(w, 2); Shift(w, w.House(thief.HouseId), house, -10); caught++;
                        }
                        foreach (var m in w.Magistrates.Where(m => m.HouseId != who.HouseId).Select(m => m.HouseId).Distinct().ToList()) Shift(w, w.House(m), house, -2);
                        if (ours && caught > 0) Mark(w, AuditFoundFlag);
                        if (ours && caught == 0) w.Log($"{who.Name} calls the magistrates to account. Nothing is proved against any of them, and all of them resent being asked.");
                        break;
                    case Sweep:
                    {
                        var rs = RegionSystem.StateOf(w, b.RegionId); rs.Lawlessness = Math.Clamp(rs.Lawlessness - SweepBy, 0, 100); Purse(-SweepCredits);
                        foreach (var h in w.Houses.Values.Where(h => h.Id != who.HouseId && HomeRegion(w, h.Id) == b.RegionId).OrderBy(h => h.Id, StringComparer.Ordinal)) Shift(w, h, house, 4);
                        if (ours || HomeRegion(w, mine ?? "") == b.RegionId) w.Log($"{who.Name}, {title.ToLowerInvariant()}, sends the Reach's fleet onto the lanes of {w.Galaxy?.Region(b.RegionId)?.Name}.");
                        break;
                    }
                    case Party:
                        foreach (var id in bench!.Members.Where(id => id != who.HouseId)) Shift(w, w.House(id), house, 4);
                        w.PoolsFor(who.Id).Influence += 2; MoveTrust(w, -1); break;
                    case Balance:
                        Purse(BalanceSum(b)); MoveTrust(w, 1); Legit(1);
                        if (BudgetOf(b) is { Surplus: < 0 }) foreach (var f in w.Fleets.Values.Where(f => f.Office).OrderBy(f => f.Id, StringComparer.Ordinal)) f.Readiness = Math.Max(0, f.Readiness - FleetShort);   // a year of shortfall, and the books square: the fleet goes short
                        break;
                    case Remit:
                    {
                        var rs = RegionSystem.StateOf(w, b.RegionId); rs.Allegiance = Math.Clamp(rs.Allegiance + RemitBy, 0, 100); Purse(-RemitSum(b));
                        foreach (var h in w.Houses.Values.Where(h => h.Id != who.HouseId && HomeRegion(w, h.Id) == b.RegionId).OrderBy(h => h.Id, StringComparer.Ordinal)) Shift(w, h, house, 4);
                        if (!ours && mine != null && HomeRegion(w, mine) == b.RegionId) Mark(w, RemittedFlag);
                        if (ours || HomeRegion(w, mine ?? "") == b.RegionId) w.Log($"{who.Name}, {title.ToLowerInvariant()}, forgives {w.Galaxy?.Region(b.RegionId)?.Name} its levy for the year.");
                        break;
                    }
                    case Contracts:
                        if (house != null) house.Treasury.Credits += ContractSum(b); MoveTrust(w, -3);
                        if (Hash(b.Id + ":exposed") % 4 == 0 && def != null)
                        {
                            OfficeSystem.Strip(w, def, "the Reach's contracts were let to their own house", false);
                            w.WorldCounters[BarredKey(who.Id)] = w.Turn + BarYears; w.WorldCounters[ScandalKey(who.HouseId)] = w.Turn; MoveTrust(w, -2); if (house != null) house.Legitimacy = Math.Clamp(house.Legitimacy - 5, 0, 100);
                            who.Flags.Add(ExposedChar); LifeSystem.Note(w, who, "office", "Was removed from office for taking money.");
                            if (ours) Mark(w, who.Id == p!.Id ? ExposedFlag : KinExposedFlag);
                        }
                        break;
                    case Fair: MoveTrust(w, 1); Legit(1); Leaders(2, 2); break;
                    case Own: w.PoolsFor(who.Id).Influence += 2; Leaders(4, -3); break;
                    case Silence:
                    {
                        var foe = w.House(b.HouseA); Shift(w, foe, house, -10); MoveTrust(w, -1);
                        foreach (var held in w.Motions.Where(x => x.Open && !x.HeldOver && x.ProposerHouseId == b.HouseA).OrderBy(x => x.Id, StringComparer.Ordinal).ToList()) HoldOver(w, held, null);   // what they moved is not reached
                        foreach (var h in w.Houses.Values.Where(h => h.Id != who.HouseId && h.Id != b.HouseA && Regard(w, h, foe) <= -20).OrderBy(h => h.Id, StringComparer.Ordinal)) Shift(w, h, house, 5);
                        if (b.HouseA == mine) { Mark(w, SilencedFlag); w.Log($"{who.Name}, {title.ToLowerInvariant()}, gives {foe?.Name} no day in the Senate this year."); }
                        else if (ours) w.Log($"{who.Name} gives {foe?.Name} no day in the Senate this year.");
                        break;
                    }
                }
                if (who.Life?.Marks.Add("charge:" + b.RungId) != false) LifeSystem.Note(w, who, "office", $"Carried the year's charge as {title}.");
                break;
            }
            default:
            {
                w.WorldCounters.Remove(AbsentKey); Sat(w, who);
                if (choice == CommitteeWork) { if (house != null) CommitteeYear(w, who, house); }
                else if (choice == Audit)
                {
                    var found = w.OfficeWork.Where(x => x.Turn == b.Turn && Took(x) && !x.Seen && Seat(w, x.HolderId) != null && w.Char(x.HolderId) is { } t && t.HouseId != who.HouseId).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (found != null && Hash(found.Id + ":audit:" + who.Id) % 2 == 0)
                    {
                        found.Seen = true; var thief = w.Char(found.HolderId)!; var theirs = w.House(thief.HouseId);
                        w.Log($"{who.Name}, reading the year's accounts in the Senate, follows the money to {thief.Name} of {theirs?.Name}.");
                        Expose(w, offices, thief, found); MoveTrust(w, 2); Shift(w, theirs, house, -15); w.PoolsFor(who.Id).Influence += 1; Legit(1);
                        if (ours) Mark(w, AuditFoundFlag);
                        if (who.Life?.Marks.Add("audit:found") != false) LifeSystem.Note(w, who, "office", "Followed stolen money to a magistrate, in the Senate.");
                    }
                    else
                    {
                        if (found != null) found.Seen = true; MoveTrust(w, 1);
                        if (ours) w.Log(found != null ? $"{who.Name} reads the year's accounts and finds something that smells, and nothing that can be proved." : $"{who.Name} reads the year's accounts and finds them in order.");
                    }
                }
                else if (choice == SpeakRegion)
                {
                    var rs = RegionSystem.StateOf(w, b.RegionId); rs.Allegiance = Math.Clamp(rs.Allegiance + 2, 0, 100); Legit(1);
                    foreach (var h in w.Houses.Values.Where(h => h.Id != who.HouseId && HomeRegion(w, h.Id) == b.RegionId).OrderBy(h => h.Id, StringComparer.Ordinal)) Shift(w, h, house, 3);
                    if (ours) w.Log($"{who.Name} speaks for {w.Galaxy?.Region(b.RegionId)?.Name} in the Senate. It is noticed there.");
                }
                else
                {
                    var f = FactionSystem.Of(w, who.HouseId)!;
                    if (f.LeaderHouseId == who.HouseId) foreach (var id in f.Members.Where(id => id != who.HouseId)) Shift(w, w.House(id), house, 3);
                    else { Shift(w, w.House(f.LeaderHouseId), house, 6); w.PoolsFor(who.Id).Influence += 1; }
                    if (ours) w.Log(f.LeaderHouseId == who.HouseId ? $"{who.Name} spends the year holding {f.Name} together." : $"{who.Name} spends the year at the service of {f.Name}.");
                }
                break;
            }
        }
    }

    /// <summary>What someone other than the player does with a hearing or an appeal: by their nature.</summary>
    /// <summary>Who is above them this year, and what that one thinks of them so far.</summary>
    public static string AboveSays(World w, OfficeBusiness b)
    {
        var who = w.Char(b.HolderId); var above = w.Char(b.SubjectId); if (who == null || above is not { Alive: true }) return b.Kind != Watch ? "" : w.Fleets.GetValueOrDefault(b.RightId) is { } idle ? $" You serve in {idle.Name}, which has nobody in command this year." : " There is no ship for you to serve in.";
        int regard = w.GetRelationship(above.Id, who.Id)?.Disposition ?? 0; bool kin = above.HouseId == who.HouseId;
        string thinks = regard >= StandingAt ? "thinks well of you" : regard >= 5 ? "has begun to notice you" : regard <= -10 ? "thinks little of you" : "does not yet know your name";
        string whose = kin ? "of your own house" : "of " + (w.House(above.HouseId)?.Name ?? "another house");
        if (who.PatronId == above.Id) thinks = "has taken you up: where they go, you go, and when they rise you are taken along";
        else if (regard >= StandingAt && above.CareerRank > who.CareerRank) thinks += $" ({Math.Min(SpokenAfter, w.Counter(UnderKey(who.Id, above.Id)))} of the {SpokenAfter} good years it takes to be taken up)";
        else if (regard <= FallOutAt + 8 && regard > FallOutAt) thinks += ", and is close to having done with you";
        return b.Kind switch
        {
            Brief => $" It will be heard by {above.Name}, {whose}, who {thinks}.",
            Ledgers => $" Your senior is {above.Name}, {whose}, who {thinks}.",
            _ => $" You serve in {w.Fleets.GetValueOrDefault(b.RightId)?.Name ?? "the house's ships"}{(w.Fleets.GetValueOrDefault(b.RightId) is { } ship && ship.OwnerId != who.HouseId ? " of " + (w.House(ship.OwnerId)?.Name ?? "the Reach") : "")} under {above.Name}, who {thinks}."
        };
    }

    public static string DutyInclination(World w, Character c, OfficeBusiness b)
    {
        bool honest = c.NatureTraits.Contains("Honorable") || c.NatureTraits.Contains("Just"); var own = w.House(c.HouseId);
        if (b.Kind == Appeal)
        {
            if (honest) return Overturn;
            var praetor = w.Char(b.SubjectId); var bench = praetor != null ? w.House(praetor.HouseId) : null;
            if (Regard(w, own, bench) >= 20 || Regard(w, own, w.House(b.HouseB)) >= 30) return Uphold;
            return Hash(b.Id + ":appeal") % 3 == 0 ? SendBack : Overturn;
        }
        if (honest) return Law;
        if (Regard(w, own, w.House(b.HouseA)) >= 30) return ForHouse;
        return c.Ambition == "master_the_senate" && Hash(b.Id + ":squares") % 2 == 0 ? ForEstate : Law;
    }

    /// <summary>The seat is the house's, not the person's: if the head died or laid the house down after the year's business was
    /// drawn up, the one who has the house now has the year. Called as each year opens.</summary>
    public static void Handover(World w)
    {
        var p = w.Protagonist; if (p is not { Alive: true } || DutyOf(w, p.Id, SeatKind, Charge) != null) return;
        var seat = w.OfficeWork.Where(b => b.Kind == SeatKind && b.Choice.Length == 0 && b.HolderId != p.Id && w.Char(b.HolderId) is var old && (old == null || !old.Alive || old.HouseId == p.HouseId)).OrderBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
        if (seat != null) { seat.HolderId = p.Id; w.WorldFlags.Add(SeatFlag); }
    }

    /// <summary>Duties the player left undone when the year turned.</summary>
    private static void CloseDuties(World w)
    {
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;
        foreach (var b in w.OfficeWork.Where(b => b.Choice.Length == 0 && b.Kind is Hearing or Appeal or SeatKind or Charge or Desk or Posting or Brief or Ledgers or Watch).ToList())
        {
            b.Choice = "left";
            if (b.Kind == SeatKind)
            {
                if (w.Char(b.HolderId) is { } absentee) Missed(w, absentee);
                int years = w.Counter(AbsentKey) + 1; w.WorldCounters[AbsentKey] = years;
                if (years >= AbsentYears && house != null)
                {
                    w.WorldCounters.Remove(AbsentKey); house.Legitimacy = Math.Clamp(house.Legitimacy - 2, 0, 100); Mark(w, AbsentFlag);
                    w.Log($"{p?.Name} has not been seen in the Senate for {AbsentYears} years. It is remarked on, and the house is thought the less of.");
                }
            }
            else if (Junior(b.Kind)) { }   // a junior who lets the year go by has only failed to learn
            else if (b.Kind is Desk or Posting) { if (b.Kind == Desk && house != null) PolicySystem.Sway(w, house.Id, "merchants", -1); }   // the clerks see to it, and the merchants notice who was not at the desk; an officer ashore who does nothing is merely ashore
            else if (b.Kind == Charge) { MoveTrust(w, -1); if (house != null && p != null && b.HolderId == p.Id) { house.Legitimacy = Math.Clamp(house.Legitimacy - 1, 0, 100); w.Log($"{p.Name} held a great office of the Reach last year and did nothing with it. It was noticed, and said."); } }
            else { if (house != null) PolicySystem.Sway(w, house.Id, "commons", -1); if (p != null && b.HolderId == p.Id) w.Log($"The matter before {p.Name}'s bench went unheard last year. It was noticed, and held against the house."); }   // one bench left empty costs its own house, not the Reach's name
        }
    }
}
