using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Senate's standing committees (docs/LADDER.md, "Committees"). There are three: the PURSE, the FLEET and the LAWS. Every
/// house of the Reach sits on one, by its head's own service unless the head chooses otherwise. A senator may spend the year on
/// the committee's work, and each such year raises their STANDING with it. Standing does three things: the other houses of the
/// committee lean toward what a senator of standing moves on the committee's own business; whoever stands highest CHAIRS it, and
/// the chair is heard; and a year not attended costs a point of it. The years a senator sat and the years they missed are kept, and
/// count at an election. Only the player's house has a kept standing; the others' is reckoned from their head's rank. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const string Purse = "purse", FleetCommittee = "fleet", Laws = "laws", CommitteeWork = "committee";
    public static readonly string[] Committees = { Purse, FleetCommittee, Laws };
    public const int StandingMax = 10, HeardAt = 3, ChairAt = 6, ChangeEvery = 5, RecordFrom = 5;
    public const string ChairFlag = "committee_chair";

    public static string StandingKey(string houseId) => "committee_standing:" + houseId;
    private static string ChangedKey(string houseId) => "committee_changed:" + houseId;
    public static string SatKey(string characterId) => "senate_sat:" + characterId;
    public static string MissedKey(string characterId) => "senate_missed:" + characterId;

    public static string CommitteeName(string id) => id == Purse ? "the Committee of the Purse" : id == FleetCommittee ? "the Committee of the Fleet" : "the Committee of the Laws";
    /// <summary>The sphere a committee's chair is heard in.</summary>
    public static string CommitteeSphere(string id) => id == Purse ? "treasury" : id == FleetCommittee ? "navy" : "senate";
    /// <summary>What a committee's business is, among the things the Senate votes on.</summary>
    public static bool IsBusinessOf(string committee, string interest) => committee switch
    {
        Purse => interest is "levy" or "relief" or "rescue",
        FleetCommittee => interest is "foreign_war" or "commission" or "reconquest" or "recall" or "triumph" or "peace",
        _ => interest is "censure" or "recognition" or "act" or "embargo" or "breakup"
    };
    public static string BusinessSays(string id) => id == Purse ? "levies, relief and rescues" : id == FleetCommittee ? "wars, commands, recalls and triumphs" : "censures, recognitions, Acts and embargoes";

    /// <summary>The committee a house sits on: the one its head chose, or else the one that fits the head's own service.</summary>
    public static string CommitteeOf(World w, House house)
    {
        if (house.Committee.Length > 0 && Committees.Contains(house.Committee)) return house.Committee;
        var head = Economy.HeadOf(w, house);
        return head?.CareerTrack switch { "stewardship" => Purse, "military" => FleetCommittee, "law" => Laws, _ => Committees[(int)(Hash(house.Id + ":committee") % 3)] };
    }
    public static List<House> CommitteeMembers(World w, string committee) =>
        w.Houses.Values.Where(h => !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null && CommitteeOf(w, h) == committee).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    /// <summary>A house's standing with its committee, nought to ten. The player's is what their years of its work have made it;
    /// another house's is reckoned from its head's rank and how much the house is heard in the committee's sphere.</summary>
    public static int CommitteeStanding(World w, House house)
    {
        if (house.Id == w.Protagonist?.HouseId) return Math.Clamp(w.Counter(StandingKey(house.Id)), 0, StandingMax);
        var head = Economy.HeadOf(w, house); if (head == null) return 0;
        return Math.Clamp(head.CareerRank + house.SphereInfluence.GetValueOrDefault(CommitteeSphere(CommitteeOf(w, house))) / 25, 0, StandingMax - 1);
    }
    /// <summary>Who chairs a committee: the member of highest standing, if it is high enough to chair anything.</summary>
    public static House? ChairOf(World w, string committee) =>
        CommitteeMembers(w, committee).Select(h => (h, s: CommitteeStanding(w, h))).Where(x => x.s >= ChairAt).OrderByDescending(x => x.s).ThenBy(x => x.h.Id, StringComparer.Ordinal).Select(x => x.h).FirstOrDefault();

    /// <summary>What a senator's standing is worth in a vote: on the committee's own business, the other houses of the committee
    /// lean toward what the player's house moves once it has standing, and further when it has the chair.</summary>
    public static int CommitteeLean(World w, string interest, string proposerHouseId, string voterHouseId)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null || proposerHouseId != mine || voterHouseId == mine) return 0;
        var house = w.House(mine); var voter = w.House(voterHouseId); if (house == null || voter == null) return 0;
        string committee = CommitteeOf(w, house); if (!IsBusinessOf(committee, interest) || CommitteeOf(w, voter) != committee) return 0;
        int standing = CommitteeStanding(w, house);
        return standing < HeardAt ? 0 : ChairOf(w, committee)?.Id == mine ? 2 : 1;
    }

    /// <summary>A year spent on the committee's work: standing, something learned, and the committee's own good done.</summary>
    private static void CommitteeYear(World w, Character who, House house)
    {
        string committee = CommitteeOf(w, house); bool ours = house.Id == w.Protagonist?.HouseId; bool wasChair = ChairOf(w, committee)?.Id == house.Id;
        if (ours) w.WorldCounters[StandingKey(house.Id)] = Math.Min(StandingMax, w.Counter(StandingKey(house.Id)) + 1);
        if (committee == Purse)
        {
            EducationSystem.Gain(w, who, "economics", 2); PolicySystem.Sway(w, house.Id, "merchants", 1);
            if (RegionSystem.CapitalHolder(w) is { } chairHouse) chairHouse.Treasury.Credits += 2;   // the levy is watched, and a little less of it goes astray
        }
        else if (committee == FleetCommittee)
        {
            EducationSystem.Gain(w, who, "logistics", 2); PolicySystem.Sway(w, house.Id, "officers", 1);
            foreach (var f in w.Fleets.Values.Where(f => w.Offices.Any(o => o.HouseId == f.OwnerId) && f.Readiness < 100).OrderBy(f => f.Id, StringComparer.Ordinal).Take(2)) f.Readiness = Math.Min(100, f.Readiness + 5);
        }
        else { EducationSystem.Gain(w, who, "law", 2); PolicySystem.Sway(w, house.Id, "magistrates", 1); MoveTrust(w, 1); }
        bool chair = ChairOf(w, committee)?.Id == house.Id;
        if (chair) w.PoolsFor(who.Id).Influence += 1;   /* the chair's weight in its estate is counted by SphereSystem, every year it is held */
        if (ours)
        {
            if (chair && !wasChair) { Mark(w, ChairFlag); LifeSystem.Note(w, who, "office", $"Took the chair of {CommitteeName(committee)}."); w.Log($"{who.Name} takes the chair of {CommitteeName(committee)}."); }
            else w.Log($"{who.Name} spends the year on the work of {CommitteeName(committee)}{(chair ? ", in the chair" : "")}.");
        }
    }
    public static string CommitteeSays(World w, House house)
    {
        string committee = CommitteeOf(w, house); int standing = CommitteeStanding(w, house); var chair = ChairOf(w, committee);
        string heard = chair?.Id == house.Id ? "You have the chair: its houses lean your way by two on its business, and each year in the chair you are heard a little more." : standing >= HeardAt ? "Its houses lean your way on its business." : $"At {HeardAt} its houses begin to lean your way on its business.";
        return $"You sit on {CommitteeName(committee)} ({BusinessSays(committee)}), with {CommitteeMembers(w, committee).Count - 1} other houses. Your standing with it is {standing} of {StandingMax}. {heard} "
            + (chair == null ? "Nobody has standing enough to chair it." : chair.Id == house.Id ? "" : $"{chair.Name} has the chair ({CommitteeStanding(w, chair)}).");
    }

    public static string? ChangeCommitteeRefusal(World w, Character? head, string committee)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of the house has its seat";
        if (!Committees.Contains(committee)) return "no such committee";
        if (CommitteeOf(w, house) == committee) return "you sit on it already";
        return w.WorldCounters.TryGetValue(ChangedKey(house.Id), out int last) && w.Turn - last < ChangeEvery ? $"you changed committees not long ago (again in {ChangeEvery - (w.Turn - last)} year(s))" : null;
    }
    /// <summary>Move to another committee. One starts at the bottom of a new table.</summary>
    public static void ChangeCommittee(World w, Character head, string committee)
    {
        var house = w.House(head.HouseId)!; house.Committee = committee; w.WorldCounters[ChangedKey(house.Id)] = w.Turn; w.WorldCounters[StandingKey(house.Id)] = 0;
        w.Log($"{head.Name} takes a place on {CommitteeName(committee)}, at the bottom of its table.");
    }

    // ---------------- the record of attendance ----------------

    /// <summary>The years a senator sat and the years they did not.</summary>
    public static (int sat, int missed) Attendance(World w, Character c) => (w.Counter(SatKey(c.Id)), w.Counter(MissedKey(c.Id)));
    /// <summary>What the record is worth at a count, once there are five years of it: two votes to one who was nearly always there,
    /// two against one who was mostly not.</summary>
    public static int AttendanceVotes(World w, Character c)
    {
        var (sat, missed) = Attendance(w, c); int years = sat + missed; if (years < RecordFrom) return 0;
        return sat * 5 >= years * 4 ? 2 : sat * 5 <= years * 2 ? -2 : 0;
    }
    public static string AttendanceSays(World w, Character c)
    {
        var (sat, missed) = Attendance(w, c); int years = sat + missed; if (years == 0) return "";
        int votes = AttendanceVotes(w, c);
        return $"You have sat {sat} of {years} years." + (years < RecordFrom ? "" : votes > 0 ? $" It is worth {votes} votes to you at any count." : votes < 0 ? $" It costs you {-votes} votes at any count." : " It counts neither for you nor against you.");
    }
    private static void Sat(World w, Character who) => w.WorldCounters[SatKey(who.Id)] = w.Counter(SatKey(who.Id)) + 1;
    private static void Missed(World w, Character who)
    {
        w.WorldCounters[MissedKey(who.Id)] = w.Counter(MissedKey(who.Id)) + 1;
        if (w.Counter(StandingKey(who.HouseId)) > 0) w.WorldCounters[StandingKey(who.HouseId)] = w.Counter(StandingKey(who.HouseId)) - 1;
    }
}
