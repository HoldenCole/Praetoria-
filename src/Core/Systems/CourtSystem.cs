using Praetoria.Core.Creation;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The house's own court (docs/COURT.md; the owner, 2026-11-22: "families (house) should have a small court, but not the same thing as
/// the title court. House court would have different positions that go with you no matter the title you hold, and commoners (can be
/// lower classes for the republic) that are sworn/work to your house over a title").
/// <para>Seven small positions: tutor, wet nurse, physician, captain of the guard, duel master, shipwright, master of ceremonies. Each is
/// held by one of the commons sworn to the house (hired from those who offer, paid a wage, growing loyal and better with the years,
/// leaving if unpaid, and in the end growing old) or by one of the family. Each does one plain thing in a rule the game already has.
/// The court belongs to the house: nothing here reads the house's rank, an office or a throne.</para>
/// RNG-free: who offers, and what happens, comes from the ids and the year.
/// </summary>
public static class CourtSystem
{
    public const string Tutor = "tutor", Nurse = "nurse", Physician = "physician", Guard = "guard", DuelMaster = "duel_master", Shipwright = "shipwright", Ceremonies = "ceremonies";
    public static readonly string[] Positions = { Tutor, Nurse, Physician, Guard, DuelMaster, Shipwright, Ceremonies };
    public const int MaxSkill = 5, OffersEach = 3, OffersChangeEvery = 2, SkillEvery = 6, LeavesUnpaidAfter = 2, StaysAtLoyalty = 60, OldAt = 66, PaidLoyalty = 3, UnpaidLoyalty = -15, GoodwillLoyalty = 8;
    /// <summary>What each position is worth where it acts: a guard's weight against a knife, a nurse's on the cradle, a shipwright's on a hull.</summary>
    public const double GuardPerSkill = 0.04, NursePerSkill = 0.08; public const int ShipwrightPctPerSkill = 4;
    // raised by something the court did, kept through the next year's reports
    public const string CuredFlag = "court_physician_cured", GuardFlag = "court_guard_held", CradleFlag = "court_nurse_at_the_cradle", OldFlag = "court_retainer_grown_old",
        PupilFlag = "court_duel_masters_pupil", FeastFlag = "court_feast_well_ordered", LeftFlag = "court_retainer_left";
    private static readonly string[] DeedFlags = { CuredFlag, GuardFlag, CradleFlag, OldFlag, PupilFlag, FeastFlag, LeftFlag };
    /// <summary>Set by an answer to a scene of the court: the household sees how one of its own was treated, and is the more or less loyal for it.</summary>
    public const string GoodwillKey = "court_goodwill";
    public const string HasCourtFlag = "has_house_court";

    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }

    public static string Name(string role) => role switch
    {
        Tutor => "Tutor", Nurse => "Wet nurse", Physician => "Physician", Guard => "Captain of the guard", DuelMaster => "Duel master", Shipwright => "Shipwright", Ceremonies => "Master of ceremonies", _ => role
    };
    public static string Does(string role) => role switch
    {
        Tutor => "Teaches the children of the house. Each learns as if a tutor had been paid for them, and a very good one teaches as the best paid for would.",
        Nurse => "Takes the infants to nurse, so that the mothers of the house are sooner themselves again and its children come closer together.",
        Physician => "Sees to anyone of the house who falls ill or is hurt. They are less hurt by it and sooner well, and nobody's purse pays.",
        Guard => "Keeps a watch on the persons of the house. A knife meant for one of them is less likely to find them.",
        DuelMaster => "Drills the young of the house at arms each year, so that one called out to the field has more than their temper to bring.",
        Shipwright => "Oversees the house's yards. Some of the materials of every hull laid down come back, and fleets in port are readier.",
        Ceremonies => "Orders the house's weddings, funerals and feasts. The old blood thinks the better of a house that does these things properly.",
        _ => ""
    };
    /// <summary>What one of the family brings to a position: there is no schooling in the nursery or the sickroom, so those are fixed.</summary>
    public static int KinFit(Character c, string role) => Math.Clamp(role switch
    {
        Tutor => (c.Skills.Count > 0 ? c.Skills.Values.Max() : 0) - 1,
        Nurse => 2,
        Physician => Math.Max(c.Skill("engineering"), Math.Max(c.Skill("law"), c.Skill("administration"))) - 1,
        Guard => Math.Max(c.Skill("discipline"), c.Skill("tactics")),
        DuelMaster => Math.Max(c.Skill("gunnery"), c.Skill("tactics")),
        Shipwright => Math.Max(c.Skill("engineering"), c.Skill("logistics")),
        Ceremonies => Math.Max(c.Skill("charisma"), c.Skill("oratory")),
        _ => 0
    }, 1, MaxSkill);

    /// <summary>How good whoever holds a position of a house is, or 0 if nobody does. Only the player's house keeps a court.</summary>
    public static int Skill(World w, string houseId, string role)
    {
        if (w.Protagonist?.HouseId != houseId || w.House(houseId)?.Position(role) is not { } r) return 0;
        if (r.KinId.Length == 0) return r.Skill;
        return w.Char(r.KinId) is { Alive: true } k && k.HouseId == houseId ? KinFit(k, role) : 0;
    }
    public static int TutorPoints(World w, string houseId) { int s = Skill(w, houseId, Tutor); return s >= 4 ? 2 : s >= 1 ? 1 : 0; }
    public static int ShipwrightPct(World w, string houseId) => Skill(w, houseId, Shipwright) * ShipwrightPctPerSkill;
    public static int WageOf(int skill) => 1 + skill / 2;
    public static int Wages(World w, House house) => house.Court.Where(r => r.KinId.Length == 0).Sum(r => r.Wage);

    // ---------------- who offers (docs/COURT.md, "Those who offer") ----------------

    private static readonly Dictionary<string, string[]> Origins = new()
    {
        [Tutor] = new[] { "a schoolmaster of {0} whose school closed", "read law at the capital and never practised", "kept the books of a trading house in {0} for twenty years", "a ship's chaplain put ashore at {0}" },
        [Nurse] = new[] { "a dockworker's wife of {0} with a child of her own at the breast", "nursed three children of a magistrate in {0}", "a farm girl of {0}, strong and steady" },
        [Physician] = new[] { "a surgeon off the lower deck, twenty years of it", "kept a dispensary in the poor quarter of {0}", "apprenticed to a magistrate's physician in {0}", "a bonesetter of {0} with a name in the markets" },
        [Guard] = new[] { "a sergeant of marines, paid off at {0}", "kept the door of a counting-house in {0} for ten years", "served a house that is gone, and will say no more of it", "a watchman of {0} who has seen most things" },
        [DuelMaster] = new[] { "a fencing-master who taught at {0} until the school failed", "fought on the sands at {0} and lived to stop", "a gunnery mate with steadier hands than anyone living", "second to nine men in the field, and principal twice" },
        [Shipwright] = new[] { "a yard foreman of {0}, passed over for a nephew", "built skimmers at {0} since boyhood", "a draughtsman of the state yards, let go in a lean year", "repaired hulls under fire and has the scars" },
        [Ceremonies] = new[] { "a herald's clerk of {0} who knows every precedence", "ran the games at {0} under three aediles", "steward of a great table in {0} until its house fell", "an actor of {0} who tired of the road" },
    };

    /// <summary>Those of the commons who would take a position of this house this year. The same three for two years, then others.</summary>
    public static List<Retainer> Offers(World w, NameCatalog names, House house, string role)
    {
        var list = new List<Retainer>(); if (!Positions.Contains(role)) return list;
        var places = w.Galaxy?.Systems.Values.Where(s => w.Galaxy.RingOf(s.Id) != Galaxy.Ring.Beyond).OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => s.Name).ToList() ?? new List<string>(); if (places.Count == 0) places.Add("the capital");
        string period = $"{house.Id}:{role}:{w.Turn / OffersChangeEvery}"; var lines = Origins[role]; uint first = Hash(period + ":line"), firstPlace = Hash(period + ":place");
        for (int i = 0; i < OffersEach; i++)
        {
            // each thing about them is drawn apart, so that three who offer are three people: no two from one place or with one story
            string key = $"{period}:{i}"; uint h = Hash(key);
            string sex = role == Nurse ? "female" : Hash(key + ":sex") % 2 == 0 ? "male" : "female"; string place = places[(int)((firstPlace + (uint)i) % (uint)places.Count)]; if (place.StartsWith("The ", StringComparison.Ordinal)) place = "the " + place[4..];
            string given = names.IsEmpty ? "Maro" : names.Pick(house.Culture, sex, new Rng.SplitMix64Rng(h));
            int roll = (int)(Hash(key + ":skill") % 100), skill = roll < 35 ? 1 : roll < 68 ? 2 : roll < 88 ? 3 : 4;
            int span = role == Nurse ? 14 : 30, age = (role == Nurse ? 20 : 24) + (int)(Hash(key + ":age") % (uint)span);
            list.Add(new Retainer { Id = "ret_" + Hash(key + ":id").ToString("x8"), Role = role, Name = $"{given} of {place}", Sex = sex, Age = age, Skill = skill,
                Wage = WageOf(skill), From = string.Format(lines[(int)((first + (uint)i) % (uint)lines.Length)], place), Loyalty = 40 });
        }
        return list;
    }

    // ---------------- hiring, naming one of the family, dismissing ----------------

    public static string? HireRefusal(World w, NameCatalog names, Character? head, string role, string offerId)
    {
        var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id || w.ProtagonistId != head.Id) return "only the head of a house takes people into its court";
        if (!Positions.Contains(role)) return "no such position";
        if (house.Position(role) != null) return "the position is filled: let them go first";
        return Offers(w, names, house, role).Any(o => o.Id == offerId) ? null : "nobody of that name offers this year";
    }
    public static void Hire(World w, NameCatalog names, Character head, string role, string offerId)
    {
        var house = w.House(head.HouseId)!; var r = Offers(w, names, house, role).First(o => o.Id == offerId); r.Since = w.Turn; house.Court.Add(r);
        w.Log($"{r.Name} is sworn to {house.Name} as its {Name(role).ToLowerInvariant()}: {r.From}.");
    }
    public static string? AppointRefusal(World w, Character? head, string role, Character? kin)
    {
        var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id || w.ProtagonistId != head.Id) return "only the head of a house names its court";
        if (!Positions.Contains(role)) return "no such position";
        if (house.Position(role) != null) return "the position is filled: let them go first";
        if (kin is not { Alive: true } || kin.HouseId != house.Id || kin.Id == head.Id) return "not someone of your house";
        if (kin.Age < DynastySystem.AgeOfMajority) return "not of age";
        if (role == Nurse && (kin.Sex != "female" || kin.Age > 45)) return "a wet nurse is a woman of an age to nurse";
        if (kin.Flags.Contains(FamilySystem.DefiantFlag) || (w.GetRelationship(kin.Id, head.Id)?.Disposition ?? 0) < FamilySystem.DefyBelow) return "they will not serve you";
        if (house.Duties.Any(d => d.HolderId == kin.Id)) return "they hold a post of the council";
        return house.Court.Any(r => r.KinId == kin.Id) ? "they hold a position already" : null;
    }
    public static void Appoint(World w, Character head, string role, Character kin)
    {
        var house = w.House(head.HouseId)!;
        house.Court.Add(new Retainer { Id = "kin_" + role + "_" + kin.Id, Role = role, Name = kin.Name, Sex = kin.Sex, Age = kin.Age, KinId = kin.Id, Since = w.Turn, Loyalty = 100 });
        var rel = w.Relationship(kin.Id, head.Id); rel.Disposition = Math.Clamp(rel.Disposition + 3, -100, 100);
        w.Log($"{head.Name} gives {kin.Name} the place of {Name(role).ToLowerInvariant()} in the house's court.");
    }
    public static string? DismissRefusal(World w, Character? head, string role)
    {
        var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house dismisses from its court";
        return house.Position(role) == null ? "nobody holds it" : null;
    }
    public static void Dismiss(World w, Character head, string role)
    {
        var house = w.House(head.HouseId)!; var r = house.Position(role)!; house.Court.Remove(r);
        if (r.KinId.Length > 0) { w.Log($"{head.Name} takes the place of {Name(role).ToLowerInvariant()} back from {r.Name}."); return; }
        // one who served long is not turned out without the rest of the household seeing it
        if (w.Turn - r.Since >= 8) foreach (var other in house.Court.Where(o => o.KinId.Length == 0)) other.Loyalty = Math.Max(0, other.Loyalty - 6);
        w.Log($"{r.Name}, {Name(role).ToLowerInvariant()} of {house.Name} for {Counted.Years(w.Turn - r.Since)}, is let go.");
    }

    // ---------------- the year ----------------

    /// <summary>Wages are paid, the court grows a year older and better, and each position does its year's work.</summary>
    public static void Tick(World w, PolicyCatalog? policies)
    {
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        w.WorldFlags.Remove(HasCourtFlag);
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p is not { Alive: true } || house == null) return;
        int Law(string key) => policies != null ? PolicySystem.Modifier(w, policies, house.Id, key) : 0;
        int goodwill = w.Counter(GoodwillKey); w.WorldCounters.Remove(GoodwillKey);
        foreach (var r in house.Court.OrderBy(r => r.Role, StringComparer.Ordinal).ToList())
        {
            if (r.KinId.Length > 0)
            {
                // one of the family holds it for as long as they are of the house and will serve
                var k = w.Char(r.KinId);
                if (k is not { Alive: true } || k.HouseId != house.Id || k.Id == p.Id || k.Flags.Contains(FamilySystem.DefiantFlag)) { house.Court.Remove(r); w.Log($"The place of {Name(r.Role).ToLowerInvariant()} in the court of {house.Name} is empty again."); }
                else { r.Name = k.Name; r.Age = k.Age; }
                continue;
            }
            r.Age++; int wage = Math.Max(0, r.Wage + Law("court_wage"));
            if (house.Treasury.Credits >= wage) { house.Treasury.Credits -= wage; r.UnpaidYears = 0; r.Loyalty = Math.Clamp(r.Loyalty + PaidLoyalty + Law("court_loyalty") + goodwill * GoodwillLoyalty, 0, 100); }
            else { r.UnpaidYears++; r.Loyalty = Math.Clamp(r.Loyalty + UnpaidLoyalty, 0, 100); }
            if (r.UnpaidYears >= LeavesUnpaidAfter && r.Loyalty < StaysAtLoyalty)
            { house.Court.Remove(r); Mark(w, LeftFlag); w.Log($"{r.Name}, unpaid for {Counted.Years(r.UnpaidYears)}, leaves the service of {house.Name}."); w.Tell("warn", $"{r.Name}, your {Name(r.Role).ToLowerInvariant()}, has gone: unpaid for {Counted.Years(r.UnpaidYears)}", "house:court", ""); continue; }
            if (r.Skill < MaxSkill && w.Turn > r.Since && (w.Turn - r.Since) % SkillEvery == 0) { r.Skill++; w.Log($"{r.Name} has grown into the work of {Name(r.Role).ToLowerInvariant()}."); }
            // old age: a nurse's work ends sooner than a herald's
            int goes = (r.Role == Nurse ? 46 : OldAt) + (int)(Hash(r.Id + ":old") % 8);
            if (r.Age == goes) { Mark(w, OldFlag); w.WorldCounters["court_old_since"] = w.Turn - r.Since; w.Log($"{r.Name}, {Name(r.Role).ToLowerInvariant()} of {house.Name} these {Counted.Years(w.Turn - r.Since)}, is grown old in its service."); }
            else if (r.Age >= goes + 3) { house.Court.Remove(r); w.Log(r.Role == Nurse ? $"{r.Name} is past nursing, and goes home with the thanks of {house.Name}." : $"{r.Name}, {Name(r.Role).ToLowerInvariant()} of {house.Name}, is too old for the work and lays it down."); }
        }
        if (house.Court.Count > 0) w.WorldFlags.Add(HasCourtFlag);

        // what the positions do in a year, where it is not done at the moment it matters (the guard, the nurse, the physician, the tutor)
        int master = Skill(w, house.Id, DuelMaster);
        if (master > 0)
        {
            var pupil = w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age is >= 14 and <= 35 && !c.Flags.Contains(DynastySystem.HiddenFlag))
                .OrderBy(c => FeudSystem.Prowess(c)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (pupil != null && EducationSystem.Gain(w, pupil, pupil.Skill("gunnery") <= pupil.Skill("tactics") ? "gunnery" : "tactics", master)) { Mark(w, PupilFlag); pupil.Flags.Add(PupilChar); }
        }
        int wright = Skill(w, house.Id, Shipwright);
        if (wright > 0) foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving && !f.IsAway && f.Readiness < 100)) f.Readiness = Math.Min(100, f.Readiness + wright);
        int herald = Skill(w, house.Id, Ceremonies);
        if (herald > 0) PolicySystem.Sway(w, house.Id, "old_blood", herald >= 4 ? 2 : 1);
    }
    public const string PupilChar = "duel_masters_pupil", CuredChar = "physician_cured", GuardedChar = "guard_held_for";

    // ---------------- at the moment it matters ----------------

    /// <summary>The physician and somebody of the house who is ill. True if they are well again for it.</summary>
    public static bool Treats(World w, Character c)
    {
        int s = Skill(w, c.HouseId, Physician); if (s == 0) return false;
        c.Health = Math.Min(100, c.Health + 2 + s);
        if (Hash(c.Id + ":physician:" + w.Turn) % 10 >= 4 + s) return false;
        c.Flags.Remove(LifeSystem.IllChar); Mark(w, CuredFlag); foreach (var o in w.Characters.Values) o.Flags.Remove(CuredChar); c.Flags.Add(CuredChar);
        w.Log($"{c.Name} is well again, under the house's own physician."); return true;
    }
    /// <summary>The physician when somebody of the house first falls ill: it goes less hard with them.</summary>
    public static void Eases(World w, Character c) { int s = Skill(w, c.HouseId, Physician); if (s > 0) c.Health += 2 * s; }
    /// <summary>An attempt on one of the house failed, and there was a guard.</summary>
    public static void Held(World w, Character target)
    {
        if (Skill(w, target.HouseId, Guard) == 0) return;
        Mark(w, GuardFlag); foreach (var o in w.Characters.Values) o.Flags.Remove(GuardedChar); target.Flags.Add(GuardedChar);
        w.Log($"The guard of {w.House(target.HouseId)?.Name} was between {target.Name} and the knife.");
    }
    /// <summary>A child is born to the house while it has a nurse.</summary>
    public static void Born(World w, Character child) { if (Skill(w, child.HouseId, Nurse) > 0) Mark(w, CradleFlag); }
    /// <summary>One of the house is wed, and the house has somebody to order the day: it is done as it should be.</summary>
    public static void Wedding(World w, House house)
    {
        if (Skill(w, house.Id, Ceremonies) == 0) return;
        house.Legitimacy = Math.Min(100, house.Legitimacy + 1); Mark(w, FeastFlag);
        w.Log($"The wedding is ordered as such things should be, and it is noticed: {house.Name} has somebody who knows how.");
    }
}
