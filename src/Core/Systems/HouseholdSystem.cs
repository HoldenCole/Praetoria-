using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The household (docs/HOUSEHOLD.md): the people of a house who were not born to it, and the work the head may hand to any of its
/// members.
/// <para><b>Found family.</b> A person can be taken into the house: a child as a WARD, a grown person as a PROTÉGÉ the head brings
/// on, or as a COMPANION who simply belongs there. They keep their own name, live and work as members of the house, and stand in the
/// line only after everyone of the blood. A head may ADOPT one of them, who then carries the name and stands as a child. People also
/// come to the door and ask. The protagonist may likewise be taken under the wing of a senior in their own service.</para>
/// <para><b>Delegation.</b> The head may hand the routine of the estates and ventures to a STEWARD, and the upkeep of the fleets to a
/// MARSHAL: any grown member of the house, blood or found. They act once a year by their own skill, spend only what is above the
/// reserve the head sets, learn the work, and may not be honest. Nothing has to be delegated.</para>
/// Only the player's house is run by it. RNG-free.
/// </summary>
public sealed class HouseholdSystem
{
    public const string Ward = "ward", Protege = "protege", Companion = "companion";
    public const string Steward = "steward", Marshal = "marshal";
    public static readonly string[] Roles = { Steward, Marshal };
    public const int WillingAt = 50, PetitionYears = 2, MentorEvery = 3, PostYearsPerSkill = 4, SkillFromPostCap = 4, SkimAt = 9, Adult = DynastySystem.AgeOfMajority, TakeInInfluence = 2, AdoptInfluence = 3;

    public const string FoundChar = "taken_in", AdoptedChar = "adopted", SkimmingChar = "skimming", WardOfAgeChar = "ward_of_age";
    // world flags re-reckoned every year
    public const string FoundFamilyFlag = "found_family", HasMentorFlag = "has_mentor", HasProtegeFlag = "has_protege", HasWardFlag = "has_ward", PetitionFlag = "petition_at_the_door", DelegatedFlag = "duties_delegated";
    // world flags raised by a deed, kept through the next events phase
    public const string TookInFlag = "took_someone_in", AdoptedFlag = "adopted_an_heir", WardStayedFlag = "ward_stayed", WardLeftFlag = "ward_went_home", SkimsFlag = "steward_skims", MentorLostFlag = "mentor_lost", MentorTaughtFlag = "mentor_taught";
    private static readonly string[] TickFlags = { FoundFamilyFlag, HasMentorFlag, HasProtegeFlag, HasWardFlag, PetitionFlag, DelegatedFlag };
    private static readonly string[] DeedFlags = { TookInFlag, AdoptedFlag, WardStayedFlag, WardLeftFlag, SkimsFlag, MentorLostFlag, MentorTaughtFlag };

    private readonly HoldingCatalog _holdings; private readonly BusinessCatalog _ventures; private readonly TitleCatalog? _titles; private readonly TradeRules _trade; private readonly int _fuelHullsPerUnit;
    public HouseholdSystem(HoldingCatalog holdings, BusinessCatalog ventures, TitleCatalog? titles, TradeRules trade, int fuelHullsPerUnit = 3)
    { _holdings = holdings; _ventures = ventures; _titles = titles; _trade = trade; _fuelHullsPerUnit = fuelHullsPerUnit; }

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static void Regard(World w, Character? from, Character? to, int delta)
    {
        if (from == null || to == null || from.Id == to.Id) return;
        var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + delta, -100, 100);
    }
    private static Character? Head(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;
    private static string Surname(string name) => name.Contains(' ') ? name[(name.LastIndexOf(' ') + 1)..] : name;
    private static string Given(string name) => name.Contains(' ') ? name[..name.LastIndexOf(' ')] : name;

    // ---------------- found family ----------------

    /// <summary>Of the house, and not born to it or adopted into it.</summary>
    public static bool IsFound(Character c) => c.BornHouseId.Length > 0 && c.AdoptedById.Length == 0;
    public static string KindOf(Character c) => c.Flags.Contains(Ward) ? Ward : c.Flags.Contains(Protege) ? Protege : c.Flags.Contains(Companion) ? Companion : "";
    public static string KindName(string kind) => kind switch { Ward => "ward", Protege => "protégé", Companion => "companion", _ => "of the household" };
    public static List<Character> Found(World w, string houseId) =>
        w.LivingCharacters().Where(c => c.HouseId == houseId && IsFound(c)).OrderByDescending(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
    /// <summary>How many the house can keep who are not of its blood: two, and one more for each step of rank.</summary>
    public static int Room(House h) => 2 + Title.RankOf(h.Title);

    /// <summary>How willing this person (or, for a child, their family) is to come. Fifty and they come.</summary>
    public static (int score, List<string> why) Willing(World w, Character head, Character person, string kind)
    {
        var ours = w.House(head.HouseId); var theirs = w.House(person.HouseId); var why = new List<string>(); int v = 30;
        void Add(int d, string what) { if (d != 0) { v += d; why.Add($"{what} {(d > 0 ? "+" : "")}{d}"); } }
        if (ours == null || theirs == null) return (0, why);
        var theirHead = Economy.HeadOf(w, theirs); bool child = person.Age < Adult;
        var speaker = child && theirHead != null && theirHead.Id != person.Id ? theirHead : person;
        Add((w.GetRelationship(speaker.Id, head.Id)?.Disposition ?? 0) / 2, child ? "what their family thinks of you" : "what they think of you");
        Add(Math.Clamp((ours.Legitimacy - theirs.Legitimacy) / 2, -20, 20), "your house's standing against theirs");
        if (ours.Wealth > theirs.Wealth * 2) Add(10, "your house is far the richer");
        if (theirs.PatronId == ours.Id) Add(15, "their house is your client");
        var bond = w.GetRelationship(person.Id, head.Id)?.Bond; if (bond == BondType.Sworn) Add(25, "sworn to you already");
        int living = w.LivingCharacters().Count(c => c.HouseId == theirs.Id);
        if (living <= 1) Add(20, "the last of their house");
        else if (theirHead?.Id == person.Id) Add(-100, "the head of a house does not leave it");
        if (!child && theirHead != null && (person.MotherId == theirHead.Id || person.FatherId == theirHead.Id) && Title.RankOf(theirs.Title) >= Title.RankOf(ours.Title)) Add(-30, "a child of a house as great as yours");
        bool orphan = child && (person.MotherId.Length == 0 || w.Char(person.MotherId)?.Alive != true) && (person.FatherId.Length == 0 || w.Char(person.FatherId)?.Alive != true);
        if (orphan) Add(30, "an orphan");
        if (kind == Protege && head.CareerTrack.Length > 0 && person.CareerTrack == head.CareerTrack && head.CareerRank > person.CareerRank) Add(10, "you are senior in their own service");
        if (DynastySystem.Spouse(w, person) is { } sp && sp.HouseId != ours.Id) Add(-25, "married elsewhere");
        return (v, why);
    }

    public static string? TakeInRefusal(World w, Character? head, Character? person, string kind)
    {
        if (head?.Alive != true || person?.Alive != true) return "no such person";
        var ours = w.House(head.HouseId); if (ours == null) return "no house";
        if (person.HouseId == ours.Id) return "already of your house";
        if (person.HouseId == PirateSystem.HouseId) return "not from among the raiders";
        if (person.Flags.Contains(DynastySystem.HiddenFlag)) return "they cannot be found";
        if (w.AtWar(ours.Id, person.HouseId)) return "not while your houses are at war";
        if (kind == Ward ? person.Age >= Adult : person.Age < Adult) return kind == Ward ? "a ward is a child" : "they are a child: take them as a ward";
        if (kind is not (Ward or Protege or Companion)) return "no such place in a household";
        if (kind == Ward && person.Age < 6) return "too young to leave home";
        if (Found(w, ours.Id).Count >= Room(ours)) return $"the house cannot keep more than {Room(ours)} who are not of its blood";
        var (score, _) = Willing(w, head, person, kind);
        return score >= WillingAt ? null : $"they will not come (willing {score} of {WillingAt})";
    }

    /// <summary>Bring someone into the house. They keep their name. The head stands as their mentor.</summary>
    public static void TakeIn(World w, Character head, Character person, string kind)
    {
        var ours = w.House(head.HouseId)!; var theirs = w.House(person.HouseId); var theirHead = theirs != null ? Economy.HeadOf(w, theirs) : null;
        if (theirHead != null && theirHead.Id != person.Id) Regard(w, theirHead, head, kind == Ward ? 10 : theirs!.PatronId == ours.Id ? 5 : -5);
        person.BornHouseId = person.HouseId; theirs?.Members.Remove(person.Id);
        person.HouseId = ours.Id; if (!ours.Members.Contains(person.Id)) ours.Members.Add(person.Id);
        person.Flags.Remove(Ward); person.Flags.Remove(Protege); person.Flags.Remove(Companion); person.Flags.Add(kind); person.Flags.Add(FoundChar);
        person.JoinedTurn = w.Turn; if (kind != Companion) person.MentorId = head.Id;
        foreach (var (a, b) in new[] { (person.Id, head.Id), (head.Id, person.Id) })
        {
            var r = w.Relationship(a, b); if (r.Bond == BondType.None) { r.Bond = BondType.Sworn; r.BondStrength = kind == Ward ? 40 : 50; }
            r.Disposition = Math.Clamp(r.Disposition + 15, -100, 100);
        }
        ours.Petitions.RemoveAll(p => p.PersonId == person.Id);
        w.Courtships.RemoveAll(c => c.Incoming && c.TargetId == person.Id);
        if (ours.Id == w.Protagonist?.HouseId) Mark(w, TookInFlag);
        w.Log(kind switch
        {
            Ward => $"{person.Name} comes to {ours.Name} as {head.Name}'s ward.",
            Protege => $"{head.Name} takes {person.Name} into {ours.Name} as a protégé.",
            _ => $"{person.Name} joins the household of {ours.Name}."
        });
    }

    public static string? AdoptRefusal(World w, Character? head, Character? person)
    {
        if (head?.Alive != true || person?.Alive != true || person.HouseId != head.HouseId) return "not of your household";
        if (!IsFound(person)) return "already of the blood";
        if (person.Age < Adult) return "wait until they are of age";
        if (head.Age < person.Age + 10) return "an heir is adopted by someone at least ten years their elder";
        if ((w.GetRelationship(person.Id, head.Id)?.Disposition ?? 0) < 20) return "they do not think well enough of you to take your name";
        return null;
    }

    /// <summary>Adoption as the Romans did it: a grown, able person takes the name and stands as the head's child.</summary>
    public static void Adopt(World w, Character head, Character person)
    {
        var house = w.House(head.HouseId)!;
        foreach (var blood in w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != person.Id && (c.MotherId == head.Id || c.FatherId == head.Id) && c.Age >= Adult))
        { Regard(w, blood, head, -15); Regard(w, blood, person, -15); }
        person.AdoptedById = head.Id; person.Flags.Add(AdoptedChar); person.Flags.Remove(Ward); person.Flags.Remove(Protege); person.Flags.Remove(Companion);
        string old = person.Name; person.Name = Given(person.Name) + " " + house.Name.Replace("House ", "");
        foreach (var (a, b) in new[] { (person.Id, head.Id), (head.Id, person.Id) }) { var r = w.Relationship(a, b); r.Bond = BondType.Blood; r.BondStrength = 80; r.Disposition = Math.Clamp(r.Disposition + 20, -100, 100); }
        Mark(w, AdoptedFlag);
        w.Log($"{head.Name} adopts {old}, who is {person.Name} from this day and stands in the line of {house.Name}.");
    }

    /// <summary>Send someone of the household away. They go back to the house of their birth if it stands.</summary>
    public static void Release(World w, Character head, Character person, bool theirChoice = false)
    {
        var ours = w.House(head.HouseId)!; var home = w.House(person.BornHouseId);
        ours.Members.Remove(person.Id); foreach (var d in ours.Duties.Where(d => d.HolderId == person.Id).ToList()) ours.Duties.Remove(d);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == ours.Id && f.AdmiralId == person.Id)) f.AdmiralId = "";
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == ours.Id && h.GovernorId == person.Id)) h.GovernorId = "";
        if (home != null) { person.HouseId = home.Id; if (!home.Members.Contains(person.Id)) home.Members.Add(person.Id); person.BornHouseId = ""; }
        else { ours.Members.Add(person.Id); return; }   // nowhere to go: they stay
        person.Flags.Remove(Ward); person.Flags.Remove(Protege); person.Flags.Remove(Companion); person.Flags.Remove(FoundChar); person.MentorId = "";
        if (!theirChoice) Regard(w, person, head, -20);
        w.Log(theirChoice ? $"{person.Name} goes home to {home.Name}, and does not forget {ours.Name}." : $"{head.Name} sends {person.Name} back to {home.Name}.");
    }

    // ---------------- under someone's wing ----------------

    public static string? MentorRefusal(World w, Character? me, Character? mentor)
    {
        if (me?.Alive != true || mentor?.Alive != true || me.Id == mentor.Id) return "no such person";
        if (me.CareerTrack.Length == 0 || mentor.CareerTrack != me.CareerTrack) return "not of your service";
        if (mentor.CareerRank <= me.CareerRank) return "not senior to you";
        if (mentor.Age < me.Age + 8) return "too near your own age to be a mentor";
        if (w.AtWar(me.HouseId, mentor.HouseId)) return "your houses are at war";
        int regard = w.GetRelationship(mentor.Id, me.Id)?.Disposition ?? 0;
        return regard >= 10 ? null : $"they do not think enough of you (regard {regard}, needs 10)";
    }

    public static void TakeMentor(World w, Character me, Character mentor)
    {
        me.MentorId = mentor.Id; me.JoinedTurn = w.Turn;
        foreach (var (a, b) in new[] { (me.Id, mentor.Id), (mentor.Id, me.Id) })
        { var r = w.Relationship(a, b); if (r.Bond == BondType.None) { r.Bond = BondType.Sworn; r.BondStrength = 30; } r.Disposition = Math.Clamp(r.Disposition + 10, -100, 100); }
        w.Log($"{mentor.Name} of {w.House(mentor.HouseId)?.Name} takes {me.Name} under their wing.");
    }

    /// <summary>What a mentor passes on: every third year, a step in the skill they are best at, while the pupil is behind them.</summary>
    private static void Teach(World w, Character pupil, Character mentor)
    {
        if (w.Turn <= pupil.JoinedTurn || (w.Turn - pupil.JoinedTurn) % MentorEvery != 0) return;
        var best = mentor.Skills.Where(kv => kv.Value > pupil.Skill(kv.Key) && pupil.Skill(kv.Key) < EducationSystem.Ceiling(pupil, kv.Key)).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).FirstOrDefault();
        if (best.Key == null || !EducationSystem.Step(w, pupil, best.Key)) return;
        Regard(w, pupil, mentor, 3);
        if (pupil.Id == w.ProtagonistId) Mark(w, MentorTaughtFlag);
        w.Log($"{pupil.Name} has learned something of {best.Key} from {mentor.Name}.");
    }

    // ---------------- delegation ----------------

    /// <summary>How good this person is at a duty: their best relevant skill.</summary>
    public static int Fit(Character c, string role) => role == Steward ? Math.Max(c.Skill("administration"), c.Skill("economics")) : Math.Max(c.Skill("logistics"), Math.Max(c.Skill("leadership"), c.Skill("tactics")));
    public static string RoleName(string role) => role == Steward ? "Steward" : "Marshal";
    public static string RoleDoes(string role) => role == Steward ? "builds on the house's worlds and enlarges its ventures" : "keeps the fleets fuelled and in repair";

    public static List<Character> Eligible(World w, House house) =>
        w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != w.ProtagonistId && c.Age >= Adult && !c.Flags.Contains(DynastySystem.HiddenFlag))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

    public static string? AssignRefusal(World w, Character? head, string role, Character? holder)
    {
        if (head?.Alive != true || w.House(head.HouseId) is not { } house) return "no house";
        if (!Roles.Contains(role)) return "no such duty";
        if (holder == null) return house.Duty(role) != null ? null : "nobody holds it";
        if (holder.HouseId != house.Id || !holder.Alive || holder.Id == head.Id) return "not someone of your house";
        if (holder.Age < Adult) return "not of age";
        if (holder.Flags.Contains(FamilySystem.DefiantFlag) || (w.GetRelationship(holder.Id, head.Id)?.Disposition ?? 0) < FamilySystem.DefyBelow) return "they will not serve you";
        return house.Duties.Any(d => d.HolderId == holder.Id && d.Role != role) ? "they hold a duty already" : null;
    }

    /// <summary>Hand a duty to someone, change its reserve, or (holder null) take it back.</summary>
    public static void Assign(World w, Character head, string role, Character? holder, int reserve)
    {
        var house = w.House(head.HouseId)!; var d = house.Duty(role);
        if (holder == null) { if (d != null) { house.Duties.Remove(d); w.Log($"{head.Name} takes the {RoleName(role).ToLowerInvariant()}'s work back into their own hands."); } return; }
        if (d != null && d.HolderId == holder.Id) { d.Reserve = Math.Max(0, reserve); return; }
        if (d != null) house.Duties.Remove(d);
        house.Duties.Add(new Duty { Role = role, HolderId = holder.Id, Since = w.Turn, Reserve = Math.Max(0, reserve) });
        Regard(w, holder, head, 5);
        w.Log($"{head.Name} makes {holder.Name} {RoleName(role).ToLowerInvariant()} of {house.Name}.");
    }

    private void StewardActs(World w, House house, Character holder, Duty d, CommandExecutor executor, CommandContext ctx)
    {
        int skill = Fit(holder, Steward); var done = new List<string>();
        int acts = skill >= 3 ? 2 : skill == 0 && Hash(holder.Id + ":idle:" + w.Turn) % 2 == 0 ? 0 : 1;
        for (int i = 0; i < acts; i++)
        {
            bool did = false;
            // what a world lacks, cheapest first, the deposit worked before the rest
            foreach (var h in w.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                foreach (var b in _holdings.Buildings.Where(b => !b.Megastructure && !h.Buildings.Contains(b.Id) && house.Treasury.Credits - b.Cost.Credits >= d.Reserve)
                             .OrderByDescending(b => b.RequiresDeposit != null).ThenBy(b => b.Cost.Credits).ThenBy(b => b.Id, StringComparer.Ordinal))
                    if (executor.TryExecute(new BuildCommand(holder.Id, h.Id, b.Id, _holdings, _titles), ctx)) { done.Add($"began {b.Name} on {h.Name}"); did = true; break; }
                if (did) break;
            }
            if (did) continue;
            // else enlarge a venture
            foreach (var biz in w.BusinessesOf(house.Id).Where(b => b.CompanyId.Length == 0))
            {
                var def = _ventures.Def(biz.Kind); if (def == null || house.Treasury.Credits - def.ExpandCost.Credits < d.Reserve) continue;
                if (executor.TryExecute(new ExpandBusinessCommand(holder.Id, biz.Id, _ventures), ctx)) { done.Add($"enlarged {def.Name}"); did = true; break; }
            }
            if (!did) break;
        }
        // a steward who is greedy and owes the head nothing helps themselves
        if (holder.AptitudeTraits.Contains("Greedy") && !holder.NatureTraits.Contains("Honorable") && !holder.NatureTraits.Contains("Loyal") && (w.GetRelationship(holder.Id, w.ProtagonistId)?.Disposition ?? 0) < 10 && house.Treasury.Credits >= 6)
        {
            house.Treasury.Credits -= 3; d.Skimmed += 3;
            if (d.Skimmed >= SkimAt && d.Skimmed - 3 < SkimAt) { Mark(w, SkimsFlag); holder.Flags.Add(SkimmingChar); w.Log($"The accounts of {house.Name} do not add up. {holder.Name} keeps them."); }
        }
        d.LastReport = done.Count > 0 ? Cap(string.Join("; ", done)) + "." : acts == 0 ? "Did little this year." : $"Found nothing worth doing with what is above the reserve of {d.Reserve}.";
    }

    private void MarshalActs(World w, House house, Character holder, Duty d, CommandExecutor executor, CommandContext ctx)
    {
        var done = new List<string>();
        int hulls = w.Fleets.Values.Where(f => f.OwnerId == house.Id).Sum(f => f.Strength);
        if (hulls > 0)
        {
            int need = Math.Max(1, FleetSystem.FuelBillFor(w, house.Id, _fuelHullsPerUnit));
            if (house.Treasury.Helium3 < need)
            {
                var buy = new BuyGoodCommand(holder.Id, Resource.Helium3, need - house.Treasury.Helium3 + (Fit(holder, Marshal) >= 3 ? need : 0), _trade);
                if (buy.Refusal(ctx) == null && house.Treasury.Credits - buy.Cost(w) >= d.Reserve && executor.TryExecute(buy, ctx)) done.Add("bought fuel for the year");
            }
        }
        int refits = 0;
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && f.Readiness < 100).OrderBy(f => f.Readiness).ThenBy(f => f.Id, StringComparer.Ordinal))
            if (house.Treasury.Credits > d.Reserve && executor.TryExecute(new RefitFleetCommand(holder.Id, f.Id), ctx)) refits++;
        if (refits > 0) done.Add(refits == 1 ? "put a fleet into refit" : $"put {refits} fleets into refit");
        d.LastReport = done.Count > 0 ? Cap(string.Join("; ", done)) + "." : hulls == 0 ? "The house keeps no ships." : "The fleets wanted nothing this year.";
    }
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ---------------- the year ----------------

    public void Tick(World w, CommandExecutor executor, CommandContext ctx)
    {
        foreach (var f in TickFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p is not { Alive: true } || house == null) return;
        foreach (var c in w.Characters.Values) if (!w.HasFlag(WardStayedFlag) && !w.HasFlag(WardLeftFlag)) c.Flags.Remove(WardOfAgeChar);

        // the household's own people
        foreach (var c in Found(w, house.Id))
        {
            if (c.Flags.Contains(Ward) && c.Age >= Adult)
            {
                // a ward of age chooses for themselves: to stay, if the house has been good to them
                c.Flags.Remove(Ward); c.Flags.Add(WardOfAgeChar);
                if ((w.GetRelationship(c.Id, p.Id)?.Disposition ?? 0) >= 30 || w.House(c.BornHouseId) == null)
                { c.Flags.Add(Companion); Mark(w, WardStayedFlag); w.Log($"{c.Name}, of age now, chooses to stay with {house.Name}."); }
                else { Mark(w, WardLeftFlag); Release(w, p, c, theirChoice: true); if (w.House(c.HouseId) is { } home && Economy.HeadOf(w, home) is { } hh) Regard(w, hh, p, 15); continue; }
            }
            if (c.MentorId == p.Id) Teach(w, c, p);
            if (w.Turn > c.JoinedTurn && (w.GetRelationship(c.Id, p.Id)?.Disposition ?? 0) < 60) Regard(w, c, p, 1);   // a house that keeps you is one you come to think well of
        }

        // whoever has the protagonist under their wing
        if (p.MentorId.Length > 0)
        {
            var m = w.Char(p.MentorId);
            if (m?.Alive != true || w.AtWar(p.HouseId, m.HouseId) || (w.GetRelationship(m.Id, p.Id)?.Disposition ?? 0) < -10)
            {
                w.Log(m?.Alive != true ? $"{m?.Name}, who took {p.Name} under their wing, is dead." : $"{m.Name} will have no more to do with {p.Name}.");
                p.MentorId = ""; Mark(w, MentorLostFlag);
            }
            else Teach(w, p, m);
        }

        // people at the door
        house.Petitions.RemoveAll(pe => w.Char(pe.PersonId) is not { Alive: true } c || c.HouseId == house.Id || w.Turn - pe.Since >= PetitionYears || TakeInRefusal(w, p, c, pe.As) != null);
        if (house.Petitions.Count == 0 && Found(w, house.Id).Count < Room(house) && Hash(house.Id + ":door:" + w.Turn) % 100 < 14)
        {
            var asker = w.LivingCharacters().Where(c => c.HouseId != house.Id && c.HouseId != PirateSystem.HouseId && c.Age >= 6 && DynastySystem.Spouse(w, c) == null)
                .Select(c => (c, kind: c.Age < Adult ? Ward : c.CareerTrack == p.CareerTrack && p.CareerRank > c.CareerRank ? Protege : Companion))
                .Where(x => TakeInRefusal(w, p, x.c, x.kind) == null && Willing(w, p, x.c, x.kind).score >= WillingAt + 10)
                .OrderByDescending(x => Willing(w, p, x.c, x.kind).score + x.c.Skills.Values.Sum()).ThenBy(x => x.c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (asker.c != null)
            {
                house.Petitions.Add(new Petition { PersonId = asker.c.Id, As = asker.kind, Since = w.Turn });
                w.Log(asker.kind == Ward ? $"{w.House(asker.c.HouseId)?.Name} asks whether {house.Name} would take {asker.c.Name} as a ward." : $"{asker.c.Name} of {w.House(asker.c.HouseId)?.Name} asks for a place in the household of {house.Name}.");
            }
        }

        // the duties the head has handed out
        foreach (var d in house.Duties.OrderBy(d => d.Role, StringComparer.Ordinal).ToList())
        {
            var holder = w.Char(d.HolderId);
            if (holder?.Id == p.Id) { house.Duties.Remove(d); w.Log($"{p.Name} is head of {house.Name} now, and the {RoleName(d.Role).ToLowerInvariant()}'s work wants someone else or their own hand."); continue; }
            if (holder?.Alive != true || holder.HouseId != house.Id || holder.Flags.Contains(DynastySystem.HiddenFlag))
            { house.Duties.Remove(d); w.Log($"{house.Name} has no {RoleName(d.Role).ToLowerInvariant()}: the work is the head's again."); continue; }
            if (holder.Flags.Contains(FamilySystem.DefiantFlag) || (w.GetRelationship(holder.Id, p.Id)?.Disposition ?? 0) < FamilySystem.DefyBelow)
            { house.Duties.Remove(d); w.Log($"{holder.Name} lays down the {RoleName(d.Role).ToLowerInvariant()}'s work and will not serve {p.Name}."); continue; }
            if (d.Role == Steward) StewardActs(w, house, holder, d, executor, ctx); else MarshalActs(w, house, holder, d, executor, ctx);
            // they learn the work, and being trusted with it counts for something
            if (w.Turn > d.Since && (w.Turn - d.Since) % PostYearsPerSkill == 0)
            {
                string key = d.Role == Steward ? (holder.Skill("administration") >= holder.Skill("economics") ? "administration" : "economics") : (holder.Skill("logistics") >= holder.Skill("leadership") ? "logistics" : "leadership");
                if (holder.Skill(key) < SkillFromPostCap) { holder.Skills[key] = holder.Skill(key) + 1; w.Log($"{holder.Name} has grown into the {RoleName(d.Role).ToLowerInvariant()}'s work."); }
            }
            if ((w.GetRelationship(holder.Id, p.Id)?.Disposition ?? 0) < 60) Regard(w, holder, p, 1);
        }

        var found = Found(w, house.Id);
        if (found.Count > 0) w.WorldFlags.Add(FoundFamilyFlag);
        if (found.Any(c => c.Flags.Contains(Ward))) w.WorldFlags.Add(HasWardFlag);
        if (found.Any(c => c.Flags.Contains(Protege))) w.WorldFlags.Add(HasProtegeFlag);
        if (p.MentorId.Length > 0) w.WorldFlags.Add(HasMentorFlag);
        if (house.Petitions.Count > 0) w.WorldFlags.Add(PetitionFlag);
        if (house.Duties.Count > 0) w.WorldFlags.Add(DelegatedFlag);
    }
}
