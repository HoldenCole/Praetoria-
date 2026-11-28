using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Four more posts of a house's council (docs/HOUSEHOLD.md, "The council"; the owner, 2026-11-22: "There should also be a few more
/// roles, which will mean more content, laws, and actions"). Each is built as the steward and the marshal are: given to anyone grown
/// of the house, done once a year by the holder's own skill, paid a stipend, told one thing by the head (an instruction), spending
/// only what the treasury holds above the reserve, and saying in a line what it did.
/// <list type="bullet">
/// <item><b>Chancellor</b>: declares the house's vote on what the Senate decides this year, where the head has not.</item>
/// <item><b>Spymaster</b>: keeps the reports on other houses' fleets fresh, watches one house closely, or watches the house's own.</item>
/// <item><b>Envoy</b>: calls on a house or a nation with gifts, and leaves it thinking better of the head.</item>
/// <item><b>Advocate</b>: pleads every suit that names the house, and comes to terms where told to.</item>
/// </list>
/// A post that has a law of its own (content/politics/laws.json, scope <c>post:&lt;role&gt;</c>) reads it here.
/// </summary>
public sealed partial class HouseholdSystem
{
    public const string Chancellor = "chancellor", Spymaster = "spymaster", Envoy = "envoy", Advocate = "advocate";
    // what a post can be told
    public const string AsTheyJudge = "", WithPatron = "patron", Abstain = "abstain", WatchHome = "home", SettleBad = "settle", HousePrefix = "house:", NationPrefix = "nation:";
    // raised by a deed of a post, kept through the next year's reports
    public const string SpyReportFlag = "spymaster_reports", SpyCaughtFlag = "informer_caught", EnvoyWarmFlag = "envoy_made_a_friend", EnvoyColdFlag = "envoy_snubbed",
        AdvocateWonFlag = "advocate_won", AdvocateLostFlag = "advocate_lost", ChancellorVotedFlag = "chancellor_voted";
    /// <summary>The mark on whoever holds a post, for what is written about that post.</summary>
    public static string PostFlag(string role) => "post:" + role;
    /// <summary>The house a spymaster was watching, or an envoy called on, last: who an event about it is with.</summary>
    public const string SpyTargetChar = "spied_on", EnvoyTargetChar = "envoy_called_on";
    public static string CounselEdgeKey(string houseId) => "counsel_edge:" + houseId;

    public const int EnvoyGifts = 2, EnvoyBase = 2, EnvoyCap = 60, EnvoyWarmAt = 20, NationCap = 40, SpyHouses = 2, CaughtOneIn = 5, SpyRegard = -12;

    /// <summary>The laws, for what a post's own law says. Set by the turn controller.</summary>
    public PolicyCatalog? Policies { get; set; }
    /// <summary>How a house leans on a motion (the Senate's own reckoning). Set by the turn controller.</summary>
    public Func<World, Motion, string, int>? Lean { get; set; }
    private int LawOf(World w, House house, string key) => Policies != null ? PolicySystem.Modifier(w, Policies, house.Id, key) : 0;

    // ---------------- what a post can be told (docs/HOUSEHOLD.md, "The council") ----------------

    /// <summary>The instructions a post takes. The steward's are <see cref="Favours"/>; the marshal takes none.</summary>
    public static List<string> Instructions(World w, House house, string role)
    {
        var houses = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => HousePrefix + h.Id);
        return role switch
        {
            Steward => Favours.ToList(),
            Chancellor => new List<string> { AsTheyJudge, WithPatron, Abstain },
            Spymaster => new List<string> { AsTheyJudge, WatchHome }.Concat(houses).ToList(),
            Envoy => new List<string> { AsTheyJudge }.Concat(house.PatronId.Length > 0 ? new[] { WithPatron } : Array.Empty<string>()).Concat(houses)
                .Concat(w.Nations.Where(n => w.House(n.HouseId) != null).OrderBy(n => n.Id, StringComparer.Ordinal).Select(n => NationPrefix + n.Id)).ToList(),
            Advocate => new List<string> { AsTheyJudge, SettleBad },
            _ => new List<string>()
        };
    }
    private static string Named(World w, string instruction) =>
        instruction.StartsWith(HousePrefix, StringComparison.Ordinal) ? w.House(instruction[HousePrefix.Length..])?.Name ?? "a house"
        : instruction.StartsWith(NationPrefix, StringComparison.Ordinal) ? w.House(w.Nations.FirstOrDefault(n => n.Id == instruction[NationPrefix.Length..])?.HouseId ?? "")?.Name ?? "a nation" : "";
    public static string InstructionName(World w, string role, string instruction) => role == Steward ? FavourName(instruction) : (role, instruction) switch
    {
        (Chancellor, AsTheyJudge) => "As the house leans",
        (Chancellor, WithPatron) => "As our patron votes",
        (Chancellor, Abstain) => "Declare nothing",
        (Spymaster, AsTheyJudge) => "The Reach at large",
        (Spymaster, WatchHome) => "Our own house",
        (Envoy, AsTheyJudge) => "Whoever thinks worst of us",
        (Envoy, WithPatron) => "Our patron",
        (Advocate, AsTheyJudge) => "Plead every suit",
        (Advocate, SettleBad) => "Come to terms where the law is plainly against us",
        _ => Named(w, instruction)
    };
    public static string InstructionDoes(World w, string role, string instruction) => role == Steward ? FavourDoes(instruction) : (role, instruction) switch
    {
        (Chancellor, AsTheyJudge) => "On each motion decided this year that you have not voted on, the house is declared the way its own interests lean. You can still vote yourself: your word stands.",
        (Chancellor, WithPatron) => "The house is declared the way its patron votes or leans. With no patron, as the house leans.",
        (Chancellor, Abstain) => "The chancellor declares nothing. What you do not vote on, the house does not vote on.",
        (Spymaster, AsTheyJudge) => "Fresh reports each year on the fleets of the strongest houses, more of them with skill and with informers.",
        (Spymaster, WatchHome) => "Reads the books of everyone who holds a post of yours. What has gone missing is found years sooner than it would be.",
        (Envoy, AsTheyJudge) => "Calls each year on the head, among the houses you are not at war with, who thinks worst of you.",
        (Envoy, WithPatron) => "Calls each year on your patron.",
        (Advocate, AsTheyJudge) => "Every suit that names the house is pleaded, and it costs you no influence.",
        (Advocate, SettleBad) => "A suit where the law is plainly against the house is settled out of court for half of what was asked, if the treasury can spare it above the reserve. The rest are pleaded.",
        (Spymaster, _) => $"Everything {Named(w, instruction)} has under sail is known exactly, with what it has banked and what its head thinks of you. An informer may be caught, and that is remembered.",
        (Envoy, _) when instruction.StartsWith(NationPrefix, StringComparison.Ordinal) => $"Calls each year on the court of {Named(w, instruction)}. A nation that thinks well of the Reach raids it less and treats sooner.",
        (Envoy, _) => $"Calls each year on the head of {Named(w, instruction)}.",
        _ => ""
    };
    public static string? InstructRefusal(World w, Character? head, string role, string instruction)
    {
        var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "not yours to say";
        if (!Roles.Contains(role) || role == Marshal) return "that post takes no instruction";
        if (house.Duty(role) == null) return $"the house has no {RoleName(role).ToLowerInvariant()}";
        return Instructions(w, house, role).Contains(instruction) ? null : "no such instruction";
    }
    public static void Instruct(World w, Character head, string role, string instruction)
    {
        var house = w.House(head.HouseId)!; var d = house.Duty(role)!; if (d.Favour == instruction) return; d.Favour = instruction;
        w.Log($"{head.Name} tells the {RoleName(role).ToLowerInvariant()}: {InstructionName(w, role, instruction).ToLowerInvariant()}.");
    }

    // ---------------- the year's work ----------------

    /// <summary>Asked to be asked (the_chancellors_voice/ask_to_be_asked): the next doubtful vote comes to the head before the chancellor
    /// declares it. A doubtful vote is one on which the house leans within <see cref="DoubtfulWithin"/> of nothing, so that one reason either
    /// way would turn it. The world flag waits for the first such motion; that motion is then the head's to declare (<see cref="UpstairsKey"/>),
    /// and the chancellor declares the rest as before.</summary>
    public const string AskFirstFlag = "chancellor_asks_first", UpstairsPrefix = "chancellor_upstairs:";
    public const int DoubtfulWithin = 1;
    public static string UpstairsKey(string motionId) => UpstairsPrefix + motionId;
    /// <summary>At the Senate's session, once the motions for the next count are on the docket: the house's chancellor, asked to bring the
    /// next doubtful vote upstairs first, brings it, and the player is told what it is and how the house leans, with the year to declare it.</summary>
    public static void BringUpstairs(World w, SenateSystem senate, string? mine)
    {
        foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith(UpstairsPrefix, StringComparison.Ordinal)).ToList())
            if (w.Motions.FirstOrDefault(m => m.Id == k[UpstairsPrefix.Length..]) is not { Open: true }) w.WorldCounters.Remove(k);   // counted, struck or gone
        if (mine == null || !w.HasFlag(AskFirstFlag) || w.House(mine) is not { } house || house.Duty(Chancellor) is not { } d || w.Char(d.HolderId) is not { Alive: true } holder || d.Favour == Abstain) return;
        var m = SenateSystem.Docket(w).FirstOrDefault(m => m.ProposerHouseId != mine && !m.Votes.ContainsKey(mine) && !w.WorldCounters.ContainsKey(UpstairsKey(m.Id)) && Math.Abs(senate.Lean(w, m, mine)) <= DoubtfulWithin);
        if (m == null) return;
        w.WorldFlags.Remove(AskFirstFlag); w.WorldCounters[UpstairsKey(m.Id)] = Math.Max(1, w.Turn);
        int lean = senate.Lean(w, m, mine);
        w.Tell("warn", $"{holder.Name}, your chancellor, brings the vote on this motion to you before declaring it, as you asked: {senate.Describe(w, m)} It is a doubtful one: the house leans {(lean > 0 ? "a little for it" : lean < 0 ? "a little against it" : "neither way")}. "
            + "Declare it yourself before the Senate sits; left alone, the house is counted as it leans.", "senate:chamber", m.ProposerHouseId);
    }

    /// <summary>The chancellor declares the house on what the Senate decides this year and the head has left alone. The head's own
    /// vote is never overwritten, and a vote brought upstairs to the head (<see cref="BringUpstairs"/>) is left for the head.</summary>
    private void ChancellorActs(World w, House house, Character holder, Duty d)
    {
        var open = w.Motions.Where(m => m.Open && m.ProposedTurn < w.Turn && !m.Votes.ContainsKey(house.Id)).OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        int upstairs = house.Id == w.Protagonist?.HouseId ? open.RemoveAll(m => w.WorldCounters.ContainsKey(UpstairsKey(m.Id))) : 0;
        if (open.Count == 0) { d.LastReport = upstairs > 0 ? "Brought the year's one doubtful vote to you, and declared nothing else: nothing else wanted the house's voice." : "Nothing was decided this year that wanted the house's voice."; return; }
        if (d.Favour == Abstain) { d.LastReport = $"Declared nothing on {Counted.Motions(open.Count)}, as told."; return; }
        string follow = d.Favour == WithPatron && house.PatronId.Length > 0 && w.House(house.PatronId) != null ? house.PatronId : house.Id; int yes = 0, no = 0, none = 0; bool stood = false;
        foreach (var m in open)
        {
            int stance = follow != house.Id && m.Votes.TryGetValue(follow, out int theirs) ? theirs : Math.Sign(Lean?.Invoke(w, m, follow) ?? 0);
            // the head's word to the bench's leader that the house will vote at the next vote of the bench: not left in its seat on it, but on its feet with the leader
            if (stance == 0 && house.Id == w.Protagonist?.HouseId && VoteMarks.StandsOn(w, m) != null && FactionSystem.Of(w, house.Id) is { } bench)
            { stance = m.Votes.TryGetValue(bench.LeaderHouseId, out int led) ? led : Math.Sign(Lean?.Invoke(w, m, bench.LeaderHouseId) ?? 0); stood |= stance != 0; }
            m.Votes[house.Id] = stance; if (stance > 0) yes++; else if (stance < 0) no++; else none++;
        }
        Mark(w, ChancellorVotedFlag);
        d.LastReport = $"Declared the house on {Counted.Motions(open.Count)}: for {yes}, against {no}{(none > 0 ? $", standing aside on {none}" : "")}{(follow != house.Id ? $", as {w.House(follow)!.Name} went" : "")}{(upstairs > 0 ? "; and brought the doubtful one to you" : "")}{(stood ? "; and kept the house on its feet with the leader of its bench, as you had promised" : "")}.";
    }

    /// <summary>The spymaster: the Reach at large, one house closely, or the house's own books.</summary>
    private void SpymasterActs(World w, House house, Character head, Character holder, Duty d)
    {
        int skill = Fit(holder, Spymaster); int cost = Math.Max(0, LawOf(w, house, "informers_cost")); bool paid = cost == 0 || house.Treasury.Credits - cost >= d.Reserve;
        if (paid) house.Treasury.Credits -= cost; int informers = paid ? LawOf(w, house, "informers_reach") : 0;
        foreach (var c in w.Characters.Values) c.Flags.Remove(SpyTargetChar);
        void Fresh(Fleet f, int confidence) => w.Intel[f.Id] = new FleetIntel { FleetId = f.Id, SystemId = f.SystemId, Strength = f.Strength, Confidence = Math.Max(confidence, w.Intel.TryGetValue(f.Id, out var old) ? old.Confidence : 0), TurnObserved = w.Turn };
        if (d.Favour == WatchHome)
        {
            var found = house.Duties.Where(x => x.HolderId != holder.Id && x.Skimmed > 0 && w.Char(x.HolderId) is { Alive: true } k && !k.Flags.Contains(SkimmingChar)).OrderBy(x => x.Role, StringComparer.Ordinal).FirstOrDefault();
            if (found != null && skill + informers >= 1)
            {
                var thief = w.Char(found.HolderId)!; thief.Flags.Add(SkimmingChar); Mark(w, SkimsFlag);
                w.Log($"{holder.Name} has been through the books of {house.Name}: {Money.Credits(found.Skimmed)} have gone missing under {thief.Name}.");
                d.LastReport = $"Found {Money.Credits(found.Skimmed)} missing under {thief.Name}, the {RoleName(found.Role).ToLowerInvariant()}.";
            }
            else d.LastReport = "Read the books of everyone who holds a post of yours, and found nothing out of place.";
            return;
        }
        if (d.Favour.StartsWith(HousePrefix, StringComparison.Ordinal) && w.House(d.Favour[HousePrefix.Length..]) is { } target && Economy.HeadOf(w, target) is { } theirHead)
        {
            var theirs = w.Fleets.Values.Where(f => f.OwnerId == target.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList(); foreach (var f in theirs) Fresh(f, 100);
            int regard = w.GetRelationship(theirHead.Id, head.Id)?.Disposition ?? 0; theirHead.Flags.Add(SpyTargetChar);
            d.LastReport = $"{target.Name}: {Counted.Hulls(theirs.Sum(f => f.Strength))} under sail, about {Money.Credits(target.Treasury.Credits)} banked, and {theirHead.Name} thinks {regard:+0;-0;0} of you"
                + (w.EnemiesOf(target.Id).Any() ? $". At war with {string.Join(", ", w.EnemiesOf(target.Id).Select(e => w.House(e)?.Name ?? e).Take(3))}." : ".");
            Mark(w, SpyReportFlag);
            // an informer in another house may be found: the odds fall with the spymaster's craft and rise with the other head's
            int odds = Math.Max(2, CaughtOneIn + skill * 2 - theirHead.Skill("intrigue") * 2);
            if (Hash($"{holder.Id}:caught:{target.Id}:{w.Turn}") % (uint)odds == 0)
            {
                Regard(w, theirHead, head, SpyRegard); Mark(w, SpyCaughtFlag);
                w.Log($"An informer of {house.Name} is found in the household of {target.Name}. {theirHead.Name} knows whose he was.");
                d.LastReport += " One of ours was found there, and they know whose he was.";
            }
            return;
        }
        // the Reach at large: the strongest houses first, as many as craft and informers reach
        int reach = SpyHouses + skill / 2 + informers;
        var watched = w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).Select(h => (h, hulls: w.Fleets.Values.Where(f => f.OwnerId == h.Id).Sum(f => f.Strength))).Where(x => x.hulls > 0)
            .OrderByDescending(x => x.hulls).ThenBy(x => x.h.Id, StringComparer.Ordinal).Take(reach).ToList();
        foreach (var (h, _) in watched) foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == h.Id)) Fresh(f, Math.Min(95, 60 + 8 * skill));
        d.LastReport = watched.Count == 0 ? "No house but ours keeps ships to watch." : $"Fresh reports on the fleets of {string.Join(", ", watched.Select(x => x.h.Name))}." + (paid ? "" : " The informers went unpaid: the treasury is at its reserve.");
    }

    /// <summary>Whom an envoy told to choose for themselves calls on: the head who thinks worst of this one, among houses not at war with it.</summary>
    public static House? EnvoyChoice(World w, House house, Character head) =>
        w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id) && !w.AtWar(h.Id, house.Id) && Economy.HeadOf(w, h) != null)
            .OrderBy(h => w.GetRelationship(Economy.HeadOf(w, h)!.Id, head.Id)?.Disposition ?? 0).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The envoy: a house's head, or a nation's court, thinks better of the head for the call. Gifts come out of the treasury.</summary>
    private void EnvoyActs(World w, House house, Character head, Character holder, Duty d)
    {
        int skill = Fit(holder, Envoy); int cost = EnvoyGifts + Math.Max(0, LawOf(w, house, "envoy_cost")); int gain = EnvoyBase + skill + LawOf(w, house, "envoy_gain");
        foreach (var c in w.Characters.Values) c.Flags.Remove(EnvoyTargetChar);
        if (house.Treasury.Credits - cost < d.Reserve) { d.LastReport = "Stayed at home: the treasury is at its reserve, and an envoy does not call empty-handed."; return; }
        if (d.Favour.StartsWith(NationPrefix, StringComparison.Ordinal) && w.Nations.FirstOrDefault(n => n.Id == d.Favour[NationPrefix.Length..]) is { } nation)
        {
            string name = w.House(nation.HouseId)?.Name ?? "the court beyond the rim";
            if (nation.AtWar) { d.LastReport = $"{name} is at war with the Reach, and receives nobody."; return; }
            if (nation.Disposition >= NationCap) { d.LastReport = $"{name} thinks as well of the Reach as an envoy can make it ({nation.Disposition:+0;-0;0})."; return; }
            house.Treasury.Credits -= cost; int by = 1 + skill / 2; nation.Disposition = Math.Min(NationCap, nation.Disposition + by);
            d.LastReport = $"Called on {name} with gifts ({Money.Credits(cost)}). It thinks {nation.Disposition:+0;-0;0} of the Reach now."; return;
        }
        var target = d.Favour == WithPatron && house.PatronId.Length > 0 ? w.House(house.PatronId) : d.Favour.StartsWith(HousePrefix, StringComparison.Ordinal) ? w.House(d.Favour[HousePrefix.Length..]) : EnvoyChoice(w, house, head);
        var theirHead = target != null ? Economy.HeadOf(w, target) : null;
        if (target == null || theirHead == null) { d.LastReport = "There was nobody to call on."; return; }
        if (w.AtWar(target.Id, house.Id)) { d.LastReport = $"{target.Name} is at war with us and would not receive an envoy."; return; }
        int before = w.GetRelationship(theirHead.Id, head.Id)?.Disposition ?? 0; theirHead.Flags.Add(EnvoyTargetChar);
        if (before >= EnvoyCap) { d.LastReport = $"{theirHead.Name} of {target.Name} already thinks as well of you as an envoy can make them ({before:+0;-0;0})."; return; }
        house.Treasury.Credits -= cost;
        // one with no gift for it, sent to a head who thinks ill of the house, is one year in three shown the door
        if (skill == 0 && before <= -30 && Hash($"{holder.Id}:snub:{target.Id}:{w.Turn}") % 3 == 0)
        {
            Mark(w, EnvoyColdFlag); w.Log($"{holder.Name} calls on {target.Name} for {house.Name} and is kept waiting, and then not seen.");
            d.LastReport = $"Was not received by {theirHead.Name} of {target.Name}. The gifts were kept."; return;
        }
        Regard(w, theirHead, head, Math.Min(gain, EnvoyCap - before)); int after = w.GetRelationship(theirHead.Id, head.Id)?.Disposition ?? 0;
        if (before < EnvoyWarmAt && after >= EnvoyWarmAt) { Mark(w, EnvoyWarmFlag); w.Log($"{holder.Name} has made a friend of {theirHead.Name} of {target.Name} for {house.Name}."); }
        d.LastReport = $"Called on {theirHead.Name} of {target.Name} with gifts ({Money.Credits(cost)}). They think {after:+0;-0;0} of you now, from {before:+0;-0;0}.";
    }

    /// <summary>The advocate: every suit naming the house is pleaded without the head's influence, and one plainly lost is settled
    /// where the head has said so.</summary>
    private void AdvocateActs(World w, House house, Character head, Character holder, Duty d)
    {
        int retainer = Math.Max(0, LawOf(w, house, "counsel_cost")); bool paid = retainer == 0 || house.Treasury.Credits - retainer >= d.Reserve; if (paid) house.Treasury.Credits -= retainer;
        int edge = paid ? LawOf(w, house, "counsel_edge") : 0; if (edge != 0) w.WorldCounters[CounselEdgeKey(house.Id)] = edge; else w.WorldCounters.Remove(CounselEdgeKey(house.Id));
        var suits = LadderSystem.SuitsNaming(w, house.Id); int pleaded = 0, settled = 0;
        foreach (var b in suits)
        {
            bool plainlyLost = b.RightId != house.Id && LadderSystem.PlainlyRead(b);
            if (d.Favour == SettleBad && plainlyLost && LadderSystem.SettleRefusal(w, head, b) == null && house.Treasury.Credits - LadderSystem.StakeOf(b) / 2 >= d.Reserve) { LadderSystem.SettleOut(w, head, b); settled++; continue; }
            if (b.AdvocateId != holder.Id) { b.AdvocateId = holder.Id; pleaded++; }
        }
        // and every charge against one of the house (docs/LADDER.md, "The docket"): answered without being asked. Admitting one is the head's.
        var charges = LadderSystem.ChargesOn(w, house.Id); int answered = 0;
        foreach (var c in charges) if (c.PleaderId != holder.Id) { c.PleaderId = holder.Id; answered++; }
        d.LastReport = suits.Count + charges.Count == 0 ? "No suit names the house this year." : Cap(string.Join("; ", new[] { pleaded > 0 ? $"is to plead {Counted.Of(pleaded, "suit")} at the year's end" : "", answered > 0 ? $"is to answer {Counted.Of(answered, "charge")}" : "", settled > 0 ? $"came to terms on {settled}" : "" }.Where(s => s.Length > 0))) is { Length: > 0 } did ? did + "." : "The suits waiting are already in hand.";
    }
    /// <summary>Whether the one pleading a suit for a house is its advocate by post: the courts tell the house how it went.</summary>
    public static bool IsAdvocateOf(World w, string houseId, string characterId) => w.House(houseId)?.Duty(Advocate)?.HolderId == characterId;
    /// <summary>Called by the courts when a suit the house's advocate pleaded is decided.</summary>
    public static void AdvocateHeard(World w, bool won) { string f = won ? AdvocateWonFlag : AdvocateLostFlag; w.WorldFlags.Add(f); w.WorldCounters["mark:" + f] = w.Turn; }
}
