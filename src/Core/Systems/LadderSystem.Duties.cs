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

    /// <summary>One who sits on the bench by their career: a Judge, a Justice, a High Justice.</summary>
    public static bool OnTheBench(Character c) => c.Alive && c.CareerTrack == "law" && c.CareerRank >= JudgeRank && !c.Flags.Contains(LifeSystem.RetiredChar) && !c.Flags.Contains(DynastySystem.HiddenFlag);
    public static OfficeBusiness? DutyOf(World w, string characterId, params string[] kinds) => w.OfficeWork.FirstOrDefault(b => b.HolderId == characterId && b.Choice.Length == 0 && kinds.Contains(b.Kind));
    public static string[] DutyChoices(string kind) => kind == Hearing ? new[] { Law, ForHouse, ForEstate } : kind == Appeal ? new[] { Overturn, SendBack, Uphold } : new[] { Audit, SpeakRegion, Whip };
    private static bool Took(OfficeBusiness b) => b.Choice is Favour or Gift;
    private static string EstateName(PolicyCatalog? policy, string id) => policy?.Estates.FirstOrDefault(e => e.Id == id)?.Name ?? "the petitioners";

    /// <summary>The year's duties: bent rulings go to appeal, the judges of the player's house are given hearings, the player's seat
    /// in the Senate has its year, and one senator of another house audits the accounts.</summary>
    private static void Duties(World w, OfficeCatalog offices, PolicyCatalog? policy)
    {
        var p = w.Protagonist; var mine = p != null ? w.House(p.HouseId) : null; var busy = new HashSet<string>();

        // 1. the appeal: a ruling that went against the plain law last year goes before a Justice who is of none of the houses in it
        foreach (var bent in w.OfficeWork.Where(b => b.Kind == Case && b.Turn == w.Turn - 1 && b.WinnerId.Length > 0 && b.WinnerId != b.RightId && !b.Seen).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            bent.Seen = true; var praetor = w.Char(bent.HolderId); if (praetor == null) continue;
            var involved = new[] { bent.HouseA, bent.HouseB, praetor.HouseId };
            var justices = w.LivingCharacters().Where(c => OnTheBench(c) && c.CareerRank >= JusticeRank && !involved.Contains(c.HouseId) && !busy.Contains(c.Id) && PolitySystem.InReach(w, c.HouseId)).ToList();
            var justice = justices.Where(c => c.HouseId == mine?.Id).OrderByDescending(c => c.Id == p?.Id).ThenByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
                ?? justices.OrderBy(c => Hash(c.Id + ":appeal:" + bent.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (justice == null) continue;
            busy.Add(justice.Id);
            var a = new OfficeBusiness { Id = $"appeal_{w.Turn}_{justice.Id}", RungId = "bench", HolderId = justice.Id, Kind = Appeal, Turn = w.Turn, HouseA = bent.RightId, HouseB = bent.WinnerId, RightId = bent.RightId, SubjectId = praetor.Id };
            w.OfficeWork.Add(a);
            if (p == null || justice.Id != p.Id) DecideDuty(w, offices, policy, justice, Appeal, DutyInclination(w, justice, a));
        }
        if (p is not { Alive: true } || mine == null) return;

        // 2. a hearing for each of the house's people on the bench who has no appeal before them and is not a sitting praetor
        var estates = policy?.Estates.OrderBy(e => e.Id, StringComparer.Ordinal).ToList() ?? new();
        var others = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != PirateSystem.HouseId && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (estates.Count > 0 && others.Count > 0 && PolitySystem.InReach(w, mine.Id))
            foreach (var j in w.LivingCharacters().Where(c => c.HouseId == mine.Id && OnTheBench(c) && !busy.Contains(c.Id) && Seat(w, c.Id)?.OfficeId != Praetor).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
            {
                string id = $"hearing_{w.Turn}_{j.Id}"; var house = others[(int)(Hash(id) % (uint)others.Count)]; var estate = estates[(int)(Hash(id + ":estate") % (uint)estates.Count)];
                var h = new OfficeBusiness { Id = id, RungId = "bench", HolderId = j.Id, Kind = Hearing, Turn = w.Turn, HouseA = house.Id, EstateId = estate.Id, RightId = Hash(id + ":right") % 2 == 0 ? house.Id : Estate };
                w.OfficeWork.Add(h);
                if (j.Id != p.Id) DecideDuty(w, offices, policy, j, Hearing, DutyInclination(w, j, h));
            }

        // 3. the seat: the head's year in the Senate
        if (PolitySystem.InReach(w, mine.Id))
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
                    Expose(w, offices, thief, found); MoveTrust(w, 4);
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
            _ => "The Senate sits, and a year of it is yours to spend. The motions are the chamber's business; this is your own."
        };
    }
    public static string DutyChoiceSays(World w, PolicyCatalog? policy, OfficeBusiness b, string choice)
    {
        string a = w.House(b.HouseA)?.Name ?? "the house", bn = w.House(b.HouseB)?.Name ?? "the house", estate = EstateName(policy, b.EstateId).ToLowerInvariant();
        var f = b.Kind == SeatKind && w.Char(b.HolderId) is { } me ? FactionSystem.Of(w, me.HouseId) : null;
        return choice switch
        {
            Law => b.RightId == Estate ? $"Rule on the law, for {estate}. They think the better of your house; {a} thinks the less." : $"Rule on the law, for {a}. It thinks the better of your house; {estate} think the less.",
            ForHouse => $"Find for {a}, whatever the law says. It will remember it, and so will {estate}." + (b.RightId == Estate ? " The law is not with them, and it will be seen." : ""),
            ForEstate => $"Find for {estate}, whatever the law says. The squares like a judge who does." + (b.RightId == Estate ? "" : " The law is not with them, and it will be seen."),
            Overturn => $"Overturn it. {bn} pays back {Damages * 2} credits to {a}; {a} will not forget it, and neither will {bn} or the praetor's house. The magistrates are trusted more.",
            SendBack => $"Send it back to be heard again. Nothing is settled; {a} is at least heard.",
            Uphold => $"Uphold the praetor. His house and {bn} are obliged to you; {a} is not, and the magistrates are trusted less.",
            Audit => "Read the year's accounts. If a magistrate took money this year you may follow it to them: they are removed, and their house will hate you for it.",
            SpeakRegion => b.RegionId.Length > 0 ? $"Speak for {w.Galaxy?.Region(b.RegionId)?.Name}: it holds a little closer to the Reach, its houses think the better of you, and your house is thought the better of." : "Your house has no worlds to speak for.",
            Whip => f == null ? "Your house sits on no bench." : f.LeaderHouseId == w.Char(b.HolderId)?.HouseId ? $"Hold {f.Name} together: every house on your bench thinks a little better of you." : $"Work for {f.Name}: its leader is obliged to you, and you gain a little influence.",
            _ => ""
        };
    }

    public static string? DutyRefusal(World w, Character? who, string kind, string choice)
    {
        var b = who != null ? DutyOf(w, who.Id, kind) : null; if (who?.Alive != true || b == null) return "nothing of the kind is waiting";
        if (!DutyChoices(kind).Contains(choice)) return "not a thing to be done with it";
        if (choice == SpeakRegion && b.RegionId.Length == 0) return "your house has no worlds to speak for";
        if (choice == Whip && FactionSystem.Of(w, who.HouseId) == null) return "your house sits on no bench";
        return null;
    }

    /// <summary>The holder decides.</summary>
    public static void DecideDuty(World w, OfficeCatalog offices, PolicyCatalog? policy, Character who, string kind, string choice)
    {
        var b = DutyOf(w, who.Id, kind)!; b.Choice = choice; var house = w.House(who.HouseId); var p = w.Protagonist; var mine = p?.HouseId; bool ours = who.HouseId == mine;
        void Legit(int by) { if (house != null && (by < 0 || house.Legitimacy < 80)) house.Legitimacy = Math.Clamp(house.Legitimacy + by, 0, 100); }
        void Approve(int by) { if (b.EstateId.Length == 0) return; string key = PolicySystem.ApprovalKey(w, who.HouseId, b.EstateId); w.WorldCounters[key] = Math.Clamp(w.Counter(key) + by, -100, 100); }
        switch (kind)
        {
            case Hearing:
            {
                var other = w.House(b.HouseA); bool lawForHouse = b.RightId != Estate; bool forHouse = choice == ForHouse || (choice == Law && lawForHouse);
                Shift(w, other, house, choice == Law ? (forHouse ? 5 : -5) : forHouse ? 8 : -8);
                Approve(choice == Law ? (forHouse ? -3 : 4) : forHouse ? -6 : 6);
                if (choice == Law) MoveTrust(w, 1); else if (forHouse != lawForHouse) MoveTrust(w, forHouse ? -2 : -1);
                if (who.Life?.Marks.Add("bench:" + (choice == Law ? "law" : "bent")) != false) LifeSystem.Note(w, who, "office", choice == Law ? "Sat as a judge and ruled on the law." : "Sat as a judge and ruled as it suited.");
                if (ours && who.Id == p!.Id) w.Log($"{who.Name} finds for {(forHouse ? other?.Name : EstateName(policy, b.EstateId).ToLowerInvariant())}{(forHouse == lawForHouse ? "" : ", against the plain reading of the law")}.");
                break;
            }
            case Appeal:
            {
                var wronged = w.House(b.HouseA); var winner = w.House(b.HouseB); var praetor = w.Char(b.SubjectId); var bench = praetor != null ? w.House(praetor.HouseId) : null;
                if (choice == Overturn)
                {
                    if (wronged != null && winner != null) { int back = Math.Min(Damages * 2, Math.Max(0, winner.Treasury.Credits)); winner.Treasury.Credits -= back; wronged.Treasury.Credits += back; }
                    Shift(w, wronged, house, 10); Shift(w, winner, house, -8); Shift(w, bench, house, -8); MoveTrust(w, 3); Legit(1);
                    if (b.HouseA == mine && !ours) Mark(w, OverturnedForUsFlag);
                    if (ours || b.HouseA == mine || b.HouseB == mine || praetor?.HouseId == mine)
                        w.Log($"{who.Name}, justice, overturns {praetor?.Name ?? "the praetor"}'s ruling for {winner?.Name}. {winner?.Name} pays {wronged?.Name} back, and the ruling is struck from the rolls.");
                    if (who.Life?.Marks.Add("bench:overturn") != false) LifeSystem.Note(w, who, "office", "Sat as a justice and overturned a bent ruling.");
                }
                else if (choice == Uphold) { Shift(w, bench, house, 8); Shift(w, winner, house, 4); Shift(w, wronged, house, -10); MoveTrust(w, -2); }
                else { Shift(w, wronged, house, 3); MoveTrust(w, 1); }
                break;
            }
            default:
            {
                w.WorldCounters.Remove(AbsentKey);
                if (choice == Audit)
                {
                    var found = w.OfficeWork.Where(x => x.Turn == b.Turn && Took(x) && !x.Seen && Seat(w, x.HolderId) != null && w.Char(x.HolderId) is { } t && t.HouseId != who.HouseId).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (found != null && Hash(found.Id + ":audit:" + who.Id) % 2 == 0)
                    {
                        found.Seen = true; var thief = w.Char(found.HolderId)!; var theirs = w.House(thief.HouseId);
                        w.Log($"{who.Name}, reading the year's accounts in the Senate, follows the money to {thief.Name} of {theirs?.Name}.");
                        Expose(w, offices, thief, found); MoveTrust(w, 4); Shift(w, theirs, house, -15); w.PoolsFor(who.Id).Influence += 1; Legit(1);
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
                    foreach (var h in w.Houses.Values.Where(h => h.Id != who.HouseId && HomeRegion(w, h.Id) == b.RegionId)) Shift(w, h, house, 3);
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

    /// <summary>Duties the player left undone when the year turned.</summary>
    private static void CloseDuties(World w)
    {
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;
        foreach (var b in w.OfficeWork.Where(b => b.Choice.Length == 0 && b.Kind is Hearing or Appeal or SeatKind).ToList())
        {
            b.Choice = "left";
            if (b.Kind == SeatKind)
            {
                int years = w.Counter(AbsentKey) + 1; w.WorldCounters[AbsentKey] = years;
                if (years >= AbsentYears && house != null)
                {
                    w.WorldCounters.Remove(AbsentKey); house.Legitimacy = Math.Clamp(house.Legitimacy - 2, 0, 100); Mark(w, AbsentFlag);
                    w.Log($"{p?.Name} has not been seen in the Senate for {AbsentYears} years. It is remarked on, and the house is thought the less of.");
                }
            }
            else { MoveTrust(w, -1); if (p != null && b.HolderId == p.Id) w.Log($"The matter before {p.Name}'s bench went unheard last year. It was noticed."); }
        }
    }
}
