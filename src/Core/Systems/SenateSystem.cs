using Praetoria.Core.Diagnostics;
using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Senate as a place of action (GDD §6.1, §7; GAP §10 item 2). Houses move motions (<see cref="Motion"/>); every house votes by
/// <b>weight</b> — its Senate-sphere influence, its title rank, the chair's two, a Justice's one — and rival houses lean by interest:
/// a target votes against its own censure and its friends with it, the poor vote down a levy, a region's lords vote for its relief,
/// a losing belligerent begs for peace. Lobbying moves a lean. A motion proposed in turn T is voted at the end of turn T+1, so there is
/// always one turn to whip. Passing lifts the proposer's standing in the Senate sphere (counter <c>senate_standing:&lt;house&gt;</c>, read
/// by <see cref="SphereSystem"/>); failing costs it. Flags for the player: <c>motion_open</c>, <c>motion_against_me</c>, <c>censured</c>,
/// <c>recognised</c>, <c>levied</c>, <c>embargoed</c>, <c>motion_passed_mine</c>, <c>motion_failed_mine</c>; counters
/// <c>motions_passed</c>, <c>motions_passed:&lt;house&gt;</c>, <c>embargo:&lt;house&gt;</c> (the turn it lifts). An <b>Act</b> (kind with
/// <c>law: true</c>) sets a Senate-scoped law for the whole Reach (PolicySystem, holder <c>senate</c>): flags <c>act_open</c>, <c>act_passed</c>,
/// <c>act_passed:&lt;law&gt;</c>, <c>act_failed</c>; counter <c>acts_passed</c>. A <b>petition</b> is an Act brought by an estate (proposer
/// <c>estate:&lt;id&gt;</c>): flags <c>petition_open</c>, <c>petition_open:&lt;estate&gt;</c>, <c>petition_carried</c>, <c>petition_refused</c> (and
/// per estate); counters <c>petitions_heard</c>, <c>petitions_refused</c>, <c>petition_cd:&lt;estate&gt;</c>.
/// </summary>
public sealed class SenateSystem
{
    /// <summary>The Exchange, when the age has one: the Senate may break a company up or stand behind the market (docs/ECONOMY_MARKET.md stage D).</summary>
    public CompanySystem? Exchange { get; set; }
    public const int DocketSize = 2, HistoryKept = 12, LobbyWeight = 2, BribeWeight = 3, BribeCredits = 5, ProposerLawRank = 3, ProposerTitleRank = 2, RepeatTurns = 10;
    /// <summary>Petitions: an estate hostile in <see cref="PetitionMinHouses"/> houses or an eighth of them (whichever is more) brings its own
    /// Act; heard, its regard rises <see cref="PetitionSwing"/> in every house; refused, it falls <see cref="PetitionSpurn"/> and its worlds
    /// stir; the estate will not come again for <see cref="PetitionCooldown"/> turns.</summary>
    public const int PetitionMinHouses = 3, PetitionSwing = 10, PetitionSpurn = 6, PetitionUnrest = 3, PetitionCooldown = 12;
    public const string EstatePrefix = "estate:", EstateAccent = "#c9a24a";
    public static readonly string[] TurnFlags = { "censured", "recognised", "levied", "motion_passed_mine", "motion_failed_mine", "act_passed", "act_failed", "petition_carried", "petition_refused", DefiedPatronFlag, ObligedPatronFlag };
    public const string DefiedPatronFlag = "defied_patron", ObligedPatronFlag = "obliged_patron";
    public const int DefyRegard = -15, ObligeRegard = 5;
    /// <summary>The generals and the Senate (docs/CAESAR.md): triumphs and recalls are voted here.</summary>
    public CaesarSystem? Caesar { get; set; }
    /// <summary>States that have left the Reach (docs/BREAKAWAY.md): going back for one is voted here.</summary>
    public PolitySystem? Polities { get; set; }
    /// <summary>The ladder of titles (docs/TITLES.md, "Rungs that are earned"): a petition for a rung above Prefect is voted here.</summary>
    public ProgressionSystem? TitleLadder { get; set; }
    public static bool IsPetition(Motion m) => m.ProposerHouseId.StartsWith(EstatePrefix);
    public static string? PetitionEstate(Motion m) => IsPetition(m) ? m.ProposerHouseId[EstatePrefix.Length..] : null;
    /// <summary>Who brings the motion: a house's name, or an estate's for a petition.</summary>
    public string ProposerName(World w, Motion m) => PetitionEstate(m) is { } e ? Policies.Estate(e)?.Name ?? e : w.House(m.ProposerHouseId)?.Name ?? m.ProposerHouseId;

    private readonly MotionCatalog _motions; private readonly TitleCatalog _titles; private readonly FleetSystem? _fleets; private readonly OfficeCatalog _offices;
    public SenateSystem(MotionCatalog motions, TitleCatalog titles, FleetSystem? fleets = null, OfficeCatalog? offices = null) { _motions = motions; _titles = titles; _fleets = fleets; _offices = offices ?? OfficeCatalog.Empty; }
    public MotionCatalog Motions => _motions;
    public OfficeCatalog Offices => _offices;
    /// <summary>Laws and estates (PolicySystem): the franchise, the Speaker's orders, the old blood's regard.</summary>
    public PolicyCatalog Policies { get; set; } = PolicyCatalog.Empty;
    /// <summary>The nations beyond the Reach (docs/FOREIGN_NATIONS.md): the targets of the war motion.</summary>
    public NationCatalog Nations { get; set; } = NationCatalog.Empty;

    /// <summary>A house's vote weight: Senate influence (the chair's two are in it) + title rank + one if its head is a Justice or better.</summary>
    public int Weight(World w, string houseId) => ReadCache.Active ? ReadCache.Get("weight:" + houseId, () => WeightNow(w, houseId)) : WeightNow(w, houseId);
    private int WeightNow(World w, string houseId)
    {
        var h = w.House(houseId); if (h == null || World.Outsider(houseId)) return 0;
        if (!PolitySystem.InReach(w, houseId)) return 0;   // a house that has left the Reach has left its Senate (docs/BREAKAWAY.md)
        int weight = h.SphereInfluence.GetValueOrDefault("senate") + _titles.RankOf(h.Title);
        var head = Economy.HeadOf(w, h); if (head is { CareerTrack: "law" } && head.CareerRank >= ProposerLawRank) weight += 1;
        weight += OfficeSystem.HeldBy(w, houseId).Sum(o => _offices.Def(o.OfficeId)?.Weight ?? 0);   // the Speaker's gavel
        weight += OrderSystem.WeightOf(w, h);                                                         // a great general, a great banker, a house with clients (docs/REPUBLIC.md stage 3)
        weight += PolicySystem.Modifier(w, Policies, houseId, "senate_weight");                       // the franchise, the bench's platform, the old blood's regard
        return Math.Max(1, weight);
    }

    /// <summary>The door by which a house sits in the Senate (docs/REPUBLIC.md stage 3): land (a Prefect or better), the bench (a head who
    /// is a Judge or better), the purse (enrolled by the census) or the sword (great in Arms). None: it votes, but may not move.</summary>
    public string? Door(World w, House h) => OrderSystem.Door(w, h, _titles, ProposerTitleRank, ProposerLawRank);

    /// <summary>May this house move a motion at all: a baron or better, or a head who is a Judge or better.</summary>
    public bool MayPropose(World w, string houseId)
    {
        var h = w.House(houseId); if (h == null || !PolitySystem.InReach(w, houseId)) return false;
        if (ChargeSystem.HeadAbroad(w, h)) return false;   // a head in exile moves nothing but the return (docs/LADDER.md, "A charge against the powerful")
        return Door(w, h) != null;
    }

    public static IEnumerable<Motion> Docket(World w) => w.Motions.Where(m => m.Open).OrderBy(m => m.ProposedTurn).ThenBy(m => m.Id, StringComparer.Ordinal);

    /// <summary>How a rival house leans on a motion before lobbying: positive for, negative against, zero abstain. Deterministic.</summary>
    public int Lean(World w, Motion m, string houseId) => ReadCache.Active ? ReadCache.Get("lean:" + m.Id + ":" + houseId, () => LeanNow(w, m, houseId)) : LeanNow(w, m, houseId);
    private int LeanNow(World w, Motion m, string houseId) => LeanNow(w, m, houseId, null);
    /// <summary>Why a house leans as it does on a motion: each cause and what it is worth, the heaviest first. They sum to <see cref="Lean"/>.</summary>
    public List<(string why, int by)> Why(World w, Motion m, string houseId)
    { var list = new List<(string why, int by)>(); LeanNow(w, m, houseId, list); return list.OrderByDescending(x => Math.Abs(x.by)).ToList(); }
    private int LeanNow(World w, Motion m, string houseId, List<(string why, int by)>? why)
    {
        int lean = RawLean(w, m, houseId, true, why);
        // bloc discipline (FactionSystem): a member leans toward its leader's stance on anything that does not target it; a whip on the leader is half a whip on the bench
        var f = FactionSystem.Of(w, houseId);
        void Add(int by, string because) { if (by == 0) return; lean += by; why?.Add((because, by)); }
        if (f != null && m.TargetId != houseId && _motions.Def(m.Kind) is { } pd) Add(Math.Sign(f.Platform.GetValueOrDefault(pd.Interest)), "the platform of " + f.Name);   // the party's stance on the kind
        if (f != null && f.LeaderHouseId != houseId && m.TargetId != houseId && m.ProposerHouseId != houseId)
        {
            int ls = LeaderStance(w, m, f);
            Add(ls * FactionSystem.Discipline, "it follows " + (w.House(f.LeaderHouseId)?.Name ?? "its leader") + ", who leads " + f.Name); Add(m.Lobby.GetValueOrDefault(f.LeaderHouseId) / 2, "its leader has been lobbied");
        }
        Add(DealingsSystem.CowedLean(w, _motions.Def(m.Kind), m, houseId, lean, out var feared), "it is afraid of " + feared);   // a house that has been threatened stays away (docs/WEALTH.md, "Dealings between houses")
        return lean;
    }

    /// <summary>Old ties (docs/LIFE.md): a head who has been the player's friend for years is for what the player's house moves and
    /// stands by it when something is aimed at it; an old enemy the other way. Regard already moves a vote: this is what years add.</summary>
    public static int OldTie(World w, MotionDef def, Motion m, Character? head, string houseId)
    {
        var mine = w.Protagonist?.HouseId; if (head == null || mine == null || mine == houseId) return 0;
        int tie = head.Flags.Contains(LifeSystem.OldFriendChar) ? 1 : head.Flags.Contains(LifeSystem.OldEnemyChar) ? -1 : 0; if (tie == 0) return 0;
        int lean = m.ProposerHouseId == mine ? tie : 0;
        if (def.Target == "house" && m.TargetId == mine) lean += def.Interest is "censure" or "embargo" or "breakup" or "recall" or "reconquest" ? -2 * tie : tie;
        return lean;
    }

    /// <summary>A house's lean by its own interests alone, before its bench is counted.</summary>
    public int RawLean(World w, Motion m, string houseId) => RawLean(w, m, houseId, true);
    /// <summary>What a bench's leader is for, as the bench reads it: the leader's own interests and the party's line on the kind, a
    /// persuasion counted and a purse not. The forecast and the count both read this, so that they agree (docs/PLAYTHROUGH.md 13:
    /// a count forecast at 94 to 62 came in at 138 to nought, because the bench followed its leader one way in the forecast and
    /// another at the count).</summary>
    public int LeaderLean(World w, Motion m, string leaderId)
    {
        int lean = RawLean(w, m, leaderId, false);
        if (FactionSystem.Of(w, leaderId) is { } f && m.TargetId != leaderId && _motions.Def(m.Kind) is { } pd) lean += Math.Sign(f.Platform.GetValueOrDefault(pd.Interest));
        return lean;
    }
    /// <summary>The leader's stance a member follows: the vote the leader cast, unless that vote was paid for.</summary>
    public int LeaderStance(World w, Motion m, Faction f) =>
        m.Votes.TryGetValue(f.LeaderHouseId, out var v) && !m.Bought.ContainsKey(f.LeaderHouseId) ? v : Math.Sign(LeaderLean(w, m, f.LeaderHouseId));

    /// <summary>What it costs to buy a house's vote on a motion: more for a house that weighs more in the Senate and stands higher,
    /// more for one that leads a bench, more for one whose head thinks ill of the buyer.</summary>
    public static int BribeCost(World w, House target, Character? buyer)
    {
        var head = Economy.HeadOf(w, target); int disp = head != null && buyer != null ? w.GetRelationship(head.Id, buyer.Id)?.Disposition ?? 0 : 0;
        bool leads = FactionSystem.Of(w, target.Id) is { } f && f.LeaderHouseId == target.Id && f.Members.Count > 1;
        return BribeCredits + target.SphereInfluence.GetValueOrDefault("senate") + 2 * Progression.Title.RankOf(target.Title) + (leads ? BenchLeaderBribe : 0) + Math.Max(0, -disp) / 10;
    }
    public const int BenchLeaderBribe = 5, BribeOutOneIn = 4, BribeOutLegitimacy = 3, BribeRefusedRegard = 5;
    /// <summary>A house whose legitimacy is this far above what a rung asks is thought to deserve it: a point of lean for its petition.</summary>
    public const int ElevationWellAbove = 15;
    /// <summary>Why a house's head would not take the purse, or null: the upright will not, nor one who hates the buyer.</summary>
    public static string? BribeRefused(World w, House target, Character? buyer)
    {
        var head = Economy.HeadOf(w, target); if (head == null) return "no one to speak to";
        if (head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just")) return $"{head.Name} is upright and would not take it";
        int disp = buyer != null ? w.GetRelationship(head.Id, buyer.Id)?.Disposition ?? 0 : 0;
        return disp <= -50 ? $"{head.Name} thinks too ill of you to take your coin" : null;
    }
    /// <summary>Whether a purse comes out at the count: one time in four, never with a head who is for sale by nature.</summary>
    public static bool BribeComesOut(World w, Motion m, string houseId)
    {
        var head = w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null; if (head == null) return false;
        if (head.NatureTraits.Contains("Ruthless") || head.NatureTraits.Contains("Cruel") || head.Ambition == "amass_a_fortune") return false;
        uint hash = 2166136261; foreach (var ch in m.Id + ":out:" + houseId) hash = (hash ^ ch) * 16777619;
        return hash % BribeOutOneIn == 0;
    }

    /// <summary>The same, with each thing that moved it and by how much written into <paramref name="why"/> when one is given:
    /// the roll call's reasons are the reckoning's own terms, so the two cannot part.</summary>
    public int RawLean(World w, Motion m, string houseId, bool bought, List<(string why, int by)>? why = null)
    {
        var def = _motions.Def(m.Kind); var h = w.House(houseId); if (def == null || h == null) return 0;
        if (houseId == m.ProposerHouseId) { why?.Add(("it moved the motion", 3)); return 3; }
        var head = Economy.HeadOf(w, h); var proposerHead = w.House(m.ProposerHouseId) is { } ph ? Economy.HeadOf(w, ph) : null;
        var targetHouse = def.Target == "house" ? w.House(m.TargetId) : null; var targetHead = targetHouse != null ? Economy.HeadOf(w, targetHouse) : null;
        int Disp(Character? from, Character? to) => from != null && to != null ? w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0 : 0;
        int toProposer = Disp(head, proposerHead), toTarget = Disp(head, targetHead);
        var proposerHouseForSway = w.House(m.ProposerHouseId);
        string pn = ProposerName(w, m), tn = targetHouse?.Name ?? "the house it names";
        int lean = 0; void Add(int by, string because) { if (by == 0) return; lean += by; why?.Add((because, by)); }
        Add(toProposer >= 20 ? 1 : toProposer <= -20 ? -1 : 0, "what its head thinks of " + pn);
        Add(OldTie(w, def, m, head, houseId), "old ties with your house"); Add(LadderSystem.CommitteeLean(w, def.Interest, m.ProposerHouseId, houseId), "your standing on its committee");
        Add(LadderSystem.ChairLeanOf(w, m, houseId), "the chair's word"); Add(WealthSystem.FavourLean(w, houseId, m.ProposerHouseId), "it owes " + pn + " a favour"); Add(FeudSystem.Lean(w, houseId, m.ProposerHouseId), "there is blood between it and " + pn);
        Add(houseId != m.ProposerHouseId ? EmergencySystem.HolderLean(w, m.ProposerHouseId) : 0, pn + " holds emergency powers");
        // sway (docs/REPUBLIC.md stage 3): a house leans with whoever it depends on — its patron, its creditor, the house whose fuel
        // its fleets burn — for what that house wants, and against what is aimed at it
        if (proposerHouseForSway != null && proposerHouseForSway.Id != houseId) Add(Math.Min(2, OrderSystem.Sway(w, h, proposerHouseForSway).lean), "it depends on " + pn);
        if (targetHouse != null && targetHouse.Id != houseId)
        {
            int sway = OrderSystem.Sway(w, h, targetHouse).lean;
            if (def.Interest is "commission" or "recognition" or "peace") Add(sway, "it depends on " + tn);
            else if (def.Interest is "censure" or "embargo" or "breakup") Add(-sway, "it depends on " + tn);
        }
        switch (def.Interest)
        {
            case "censure":
            case "embargo":
                if (houseId == m.TargetId) Add(-4, "it is the house struck at");
                else if (targetHouse != null && (toTarget >= 30 || (h.Bloc.Length > 0 && h.Bloc == targetHouse.Bloc))) Add(-2, "it stands with " + tn);
                else if (targetHouse != null && (toTarget <= -20 || w.AtWar(houseId, m.TargetId))) Add(2, "it is no friend of " + tn);
                break;
            case "recognition":
                if (houseId == m.TargetId) Add(4, "it is the house to be recognised");
                else if (targetHouse != null && h.Claims.Contains(targetHouse.Title)) Add(-2, "it claims the same rank");          // a rival claimant to the same rung
                else Add(toTarget >= 10 ? 1 : toTarget <= -20 ? -1 : 0, "what its head thinks of " + tn);
                break;
            case "elevation":
                // a petition for a rank (docs/TITLES.md, "Rungs that are earned"): what the head thinks of the house that asks, whether
                // its standing is well above what the rung wants, and who would be passed, who claims the rung, who is at war with it
                if (targetHouse != null && houseId != m.TargetId)
                {
                    Add(toTarget >= 10 ? 1 : toTarget <= -20 ? -1 : 0, "whether its head would see " + tn + " raised");
                    if (_titles.ById(m.TitleId) is { } rung)
                    {
                        if (targetHouse.Legitimacy >= rung.LegitimacyRequirement + ElevationWellAbove) Add(1, tn + " stands well above what the rank asks");
                        if (h.Claims.Contains(rung.Id)) Add(-2, "it claims the same rank");
                        if (_titles.RankOf(h.Title) == rung.Rank - 1) Add(-1, "it is of " + tn + "'s rank today, and would be passed");
                    }
                    if (targetHouse.Origin == "new" && h.Origin == "old") Add(-1, "an old family does not care to see a new name rise");
                    if (w.AtWar(houseId, m.TargetId)) Add(-2, "it is at war with " + tn);
                }
                break;
            case "levy":
                if (RegionSystem.CapitalHolder(w)?.Id == houseId) Add(3, "the levy fills its own treasury: it holds the capital");
                else if (h.Treasury.Credits < 20) Add(-2, "it cannot afford a levy");
                else Add(-1, "a levy costs it");
                if (h.Treasury.Credits > 150) Add(1, "it is rich enough to look generous");                                                  // the rich can afford to look generous
                break;
            case "relief":
                if (w.Galaxy != null && w.HoldingsOf(houseId).Any(x => x.IsPlanet && w.Galaxy.System(x.SystemId)?.RegionId == m.TargetId)) Add(3, "its own worlds lie in the region");
                else if (w.Galaxy != null && w.HoldingsOf(houseId).Any(x => x.IsPlanet && w.Galaxy.RingOf(x.SystemId) == Galaxy.Ring.Rim)) Add(1, "it holds worlds on the rim, and knows what lawless lanes cost");   // the rim knows what lawless lanes cost
                break;
            case "commission":
                if (houseId == m.TargetId) Add(4, "the office would be its own");
                else
                {
                    var holder = OfficeSystem.HolderHouse(w, m.OfficeId); var holderHouse = holder != null ? w.House(holder) : null; var holderHead = holderHouse != null ? Economy.HeadOf(w, holderHouse) : null;
                    if (holder == houseId) Add(-4, "it holds the office now");                                                      // no one votes themselves out of office
                    else if (holderHouse != null && Disp(head, holderHead) >= 30) Add(-2, "it is a friend of the house that holds the office");             // the holder's friends
                    if (_offices.Def(m.OfficeId) is { } od && OfficeSystem.Candidate(w, od, houseId) is { } ownC && targetHouse != null && (OfficeSystem.Candidate(w, od, targetHouse.Id)?.CareerRank ?? 0) < ownC.CareerRank) Add(-2, "it has the better candidate of its own");   // a house with a better candidate is jealous
                    Add(toTarget >= 10 ? 1 : toTarget <= -20 ? -2 : 0, "what its head thinks of " + tn);
                    if (targetHouse != null && _offices.Def(m.OfficeId) is { } post)   // the chamber prefers a house of the order the post belongs to
                    {
                        string order = post.Track == "military" ? OrderSystem.Arms : post.Track == "stewardship" ? OrderSystem.Commerce : OrderSystem.Senate;
                        int tier = OrderSystem.Tier(targetHouse, order); Add(tier >= 2 ? 1 : tier == 0 && order != OrderSystem.Senate ? -1 : 0, "whether " + tn + " is of the order the office belongs to");
                    }
                    if (RegionSystem.CapitalHolder(w)?.Id == houseId && toTarget >= 0) Add(1, "it holds the capital, and wants a safe pair of hands");         // the chair likes a safe pair of hands
                }
                break;
            case "breakup":
                if (houseId == m.TargetId) Add(-4, "it is the house to be broken up");
                else if (targetHouse != null && CompanySystem.ControlledBy(w, targetHouse.Id).Any(c => c.HeldBy(houseId) > 0)) Add(-2, "it holds shares in what would be broken up");   // its other holders would be paid out at a loss
                else if (targetHouse != null && (toTarget <= -20 || w.AtWar(houseId, m.TargetId))) Add(2, "it is no friend of " + tn);
                else if (targetHouse != null && toTarget >= 30) Add(-2, "it is a friend of " + tn);
                Add(FactionSystem.Of(w, houseId)?.PartyId switch { "factors" => -1, "populares" => 1, _ => 0 }, "its bench's view of great companies");
                break;
            case "rescue":
                if (CompanySystem.PaperOf(w, houseId) >= 30) Add(2, "it holds paper that a rescue would save");
                if (RegionSystem.CapitalHolder(w)?.Id == houseId) Add(-2, "it holds the capital, and it is its purse that opens");                               // it is the chair's purse that opens
                Add(FactionSystem.Of(w, houseId)?.PartyId switch { "factors" => 2, "populares" => -1, _ => 0 }, "its bench's view of a rescue");
                break;
            case "triumph":
                if (Caesar != null && targetHouse != null) Add(Caesar.TriumphLean(w, h, targetHouse, toTarget), "what it makes of " + tn + "'s victories");
                break;
            case "recall":
                if (Caesar != null && targetHouse != null) Add(Caesar.RecallLean(w, h, targetHouse, toTarget), "what it makes of " + tn + "'s command");
                break;
            case TriumphSystem.LeaveKind:
                if (targetHouse != null) Add(TriumphSystem.LeaveLean(w, h, targetHouse, toTarget, _offices), "what it makes of " + tn + " standing in absence");
                break;
            case ChargeSystem.HomeKind:
                if (targetHouse != null) Add(ChargeSystem.HomeLean(w, h, targetHouse, toTarget), "what it makes of the exile, and of " + tn);
                break;
            case "emergency":
                if (targetHouse != null) Add(EmergencySystem.GrantLean(w, h, targetHouse, toTarget), "what it makes of the danger, and of " + tn);
                break;
            case "for_life":
                if (targetHouse != null) Add(EmergencySystem.ForLifeLean(w, h, targetHouse, toTarget), "what it makes of powers for life");
                break;
            case "reconquest":
                if (Polities != null && targetHouse != null) Add(Polities.ReconquestLean(w, h, targetHouse, toTarget), "what it makes of a reconquest");
                break;
            case "act":
                Add(ActLean(w, h, m), (why != null ? SenateActs.Because(w, h, Policies, m, _offices) : null) ?? "the law itself: its own laws, its estates and its bench");
                break;
            case "annul":
                if (EdictSystem.InForce(w, m.TargetId) is { } edict) Add(EdictSystem.AnnulLean(w, h, edict), "what the edict does to it, and what it thinks of whoever issued it");
                break;
            case "foreign_war":
                Add(ForeignWarLean(w, h, m.TargetId), "the war itself: its trade, its bench, how near the nation lies");
                break;
            case "foreign_peace":
                Add(ForeignPeaceLean(w, h, m.TargetId), "the peace itself: its trade, its bench, how the war goes");
                break;
            case "peace":
                if (houseId == m.TargetId) Add(3, "it is the house the peace is for");
                else if (w.AtWar(houseId, m.TargetId)) { var war = w.Wars.FirstOrDefault(x => x.Between(houseId, m.TargetId)); Add(war != null && war.Lead(houseId) < 0 ? 1 : -3, "it is at war with " + tn + " itself, and how that war goes"); }
                else Add(1, "peace costs a bystander nothing");                                                                            // peace is popular with the bystanders
                break;
        }
        uint hash = 2166136261; foreach (var ch in m.Id + ":" + houseId) hash = (hash ^ ch) * 16777619;
        Add((int)(hash % 3) - 1, "its own temper on the day");
        Add(m.Lobby.GetValueOrDefault(houseId), "it has been lobbied"); Add(bought ? m.Bought.GetValueOrDefault(houseId) : 0, "its vote has been paid for");
        return lean;
    }

    /// <summary>How a house leans on a peace with a nation (docs/BEYOND.md, "Whose war"). The Factors want their trade back and the Sword
    /// its war. One house's own war is nobody else's quarrel. A war going badly, or a long one, is worth ending; one going well is not.
    /// A house whose world the nation holds will not vote a peace that cedes it, and will vote one that brings it home.</summary>
    public int ForeignPeaceLean(World w, House h, string nationId)
    {
        var n = w.Nation(nationId); if (n == null) return 0;
        if (n.WarOf == h.Id) return -4;
        int lean = 0; var party = FactionSystem.Of(w, h.Id);
        if (party?.PartyId == "sword") lean -= 2; else if (party?.PartyId == "factors") lean += 2;
        if (n.WarOf.Length > 0) lean += 1;
        int lead = n.ScoreReach - n.ScoreNation;
        if (n.BeganIt && Invaded(w, n)) lean -= 2;   // no peace with an invader that leaves it holding what it took
        if (lead < 0) lean += 1; else if (lead >= 3) lean -= 1;
        if (w.Turn - n.WarSince >= 8) lean += 1;
        if (w.Holdings.Values.Any(x => x.IsPlanet && x.OwnerId == n.HouseId && x.FormerOwnerId == h.Id)) lean += lead >= 0 ? 2 : -3;
        if (Economy.HeadOf(w, h) is { } head && head.CareerTrack == "military" && head.CareerRank >= 2) lean -= 1;
        return lean;
    }

    /// <summary>A peace a house would move: one that is not of the war party, when another house is fighting a war of its own beyond the
    /// Reach, or the Reach's war has run five years and is not being won, or (a house of the Factors) three years.</summary>
    public string? WantedPeace(World w, House h)
    {
        if (Nations.IsEmpty || NationSystem.AtWar(w) is not { } n || NationSystem.PeaceRefusal(w, Nations, n.Id) != null || w.Motions.Any(m => m.Open && m.Kind == "foreign_peace")) return null;
        var head = Economy.HeadOf(w, h); var party = FactionSystem.Of(w, h.Id);
        if (n.WarOf == h.Id || party?.PartyId == "sword" || (head != null && head.CareerTrack == "military" && head.CareerRank >= 2)) return null;
        int years = w.Turn - n.WarSince; bool factors = party?.PartyId == "factors";
        if (n.BeganIt && Invaded(w, n) && years < 10) return null;   // nobody moves to cede worlds to an invader until the war is ten years old
        bool wants = n.WarOf.Length > 0 ? years >= (factors ? 2 : 4)   // a house's own war is left to run a while: raiding the border is an old habit
            : (years >= 5 && n.ScoreReach <= n.ScoreNation) || (factors && years >= 3 && n.ScoreReach - n.ScoreNation < 3)
              || (years >= 3 && Nations.Def(n.Id) is { } nd && NationSystem.Best(n, nd) == "tribute");   // they would pay now: why fight on
        return wants ? n.Id : null;
    }
    /// <summary>Whether the nation holds a world of the Reach's that it took in this war.</summary>
    private static bool Invaded(World w, Nation n) => w.Holdings.Values.Any(x => x.IsPlanet && x.OwnerId == n.HouseId && x.FormerOwnerId.Length > 0 && w.OfTheReach(x.FormerOwnerId));

    /// <summary>How a house leans on a war beyond the Reach (docs/FOREIGN_NATIONS.md): the Sword party wants it, the Factors fear for their trade;
    /// a house with fleets to send sees glory, a house at war at home sees none; a nation that raids is hated; one already paying tribute is left be.</summary>
    public int ForeignWarLean(World w, House h, string nationId)
    {
        var n = w.Nation(nationId); var d = Nations.Def(nationId); if (n == null || d == null) return 0;
        int lean = 0; var party = FactionSystem.Of(w, h.Id);
        if (party?.PartyId == "sword") lean += 2; else if (party?.PartyId == "factors") lean -= 2;
        int hulls = w.Fleets.Values.Where(f => f.OwnerId == h.Id && !f.Office).Sum(f => f.Strength);
        if (hulls >= 3) lean += 1; else if (hulls == 0) lean -= 1;
        if (w.Wars.Any(x => x.Involves(h.Id))) lean -= 2;
        if (n.Disposition <= -30) lean += 1;
        if (NationSystem.Raided(w, nationId, 8)) lean += 1;
        if (d.Temper == "mercantile") lean -= 1;
        if (n.OnMap && d.Wealth >= RichAt && h.Treasury.Credits < PlunderWant) lean += 1;   // a house short of money remembers what a rich neighbour is worth
        if (n.Subdued(w.Turn) || NationSystem.AtWar(w) != null) lean -= 3;
        if (Economy.HeadOf(w, h) is { } head && head.CareerTrack == "military" && head.CareerRank >= 2) lean += 1;
        return lean;
    }

    /// <summary>How a house leans on an Act by its estates and its situation: pleasing an estate that is hostile to it counts double, galling a
    /// loyal one counts double against; a house at war wants the Reach's levy, a house with lawless worlds wants the assizes, a house that buys
    /// fuel wants open lanes, a house of many worlds likes a protected market it can tax. Zero when the option is already the law.</summary>
    public int ActLean(World w, House h, Motion m)
    {
        var law = Policies.Law(m.LawId); var next = law?.Option(m.LawOption); if (law == null || next == null) return 0;
        var cur = PolicySystem.ReachOption(w, Policies, law) ?? law.DefaultOption; if (cur.Id == next.Id) return 0;
        int score = 0;
        foreach (var e in Policies.Estates)
        {
            // what the estate would feel: the Reach's law counts at half and within a cap, so an estate the Reach's law already holds at
            // the cap is moved no further; put back on the scale of the opinions themselves, which the lean below is reckoned in
            int said = PolicySystem.ReachOpinion(w, Policies, e.Id);
            int delta = (PolicySystem.ReachCounts(said - cur.Opinions.GetValueOrDefault(e.Id) + next.Opinions.GetValueOrDefault(e.Id)) - PolicySystem.ReachCounts(said)) * PolicySystem.ReachShare; if (delta == 0) continue;
            int a = PolicySystem.Approval(w, h.Id, e.Id);
            int mult = (delta > 0 && a <= PolicySystem.Hostile) || (delta < 0 && a >= PolicySystem.Loyal) ? 2 : 1;
            score += delta * mult;
        }
        int lean = Math.Clamp(score / 10, -3, 3);
        if (FactionSystem.Of(w, h.Id) is { } party && party.Laws.TryGetValue(law.Id, out var pos)) { if (pos == next.Id) lean += 2; else if (pos == cur.Id) lean -= 2; }   // the party line on the law
        if (PetitionEstate(m) is { } pe && PolicySystem.Approval(w, h.Id, pe) <= PolicySystem.Hostile) lean += 1;   // the petitioners are at this house's own gate
        var fx = next.Effects; var cfx = cur.Effects;
        int Fx(string k) => fx.GetValueOrDefault(k) - cfx.GetValueOrDefault(k);
        if (Fx("manpower_per_world") > 0 && w.Wars.Any(x => x.Involves(h.Id))) lean += 2;
        if (Fx("lawlessness_drift") < 0 && w.Galaxy is { } g && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && g.System(x.SystemId)?.RegionId is { } r && RegionSystem.StateOf(w, r).Lawlessness >= 40)) lean += 2;
        if (Fx("market_price_pct") < 0 && w.Fleets.Values.Any(f => f.OwnerId == h.Id && !f.Office && f.Strength > 0)) lean += 1;
        if (Fx("market_price_pct") > 0 && w.HoldingsOf(h.Id).Count(x => x.IsPlanet) >= 3) lean += 1;
        if (Fx("unrest_drift") > 0 && w.HoldingsOf(h.Id).Any(x => x.IsPlanet && x.Unrest >= 50)) lean -= 2;
        lean += SenateActs.Interest(w, h, cur, next, _offices);   // what the Act does to this house: a lender and a cap on usury, a hired fleet and the ban (docs/SENATE.md, "The Senate's law, enlarged")
        return lean;
    }

    /// <summary>The declared or predicted stance of a house: a declared vote if any, else the sign of its lean.</summary>
    public int Stance(World w, Motion m, string houseId) => m.Votes.TryGetValue(houseId, out var v) ? v : Math.Sign(Lean(w, m, houseId));

    public (int forWeight, int againstWeight) Tally(World w, Motion m)
    {
        int f = 0, a = 0;
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id))) { int s = Stance(w, m, h.Id); if (s > 0) f += Weight(w, h.Id); else if (s < 0) a += Weight(w, h.Id); }
        return (f, a);
    }

    /// <summary>Why a motion against a house was moved, as far as it can be read from how things stand: a motion that strikes at a
    /// house names its ground (docs/PLAYTHROUGH.md, the Senate: "a censure should name its ground"). Empty for anything else.</summary>
    public string Ground(World w, Motion m)
    {
        var def = _motions.Def(m.Kind); if (def == null || def.Target != "house" || def.Interest is not ("censure" or "embargo" or "breakup")) return "";
        var mover = w.House(m.ProposerHouseId); var target = w.House(m.TargetId); if (mover == null || target == null) return "";
        var mh = Economy.HeadOf(w, mover); var th = Economy.HeadOf(w, target); int d = mh != null && th != null ? w.GetRelationship(mh.Id, th.Id)?.Disposition ?? 0 : 0;
        if (FeudSystem.Between(w, mover.Id, target.Id)) return $"The ground: there is blood between {mover.Name} and {target.Name}.";
        if (w.AtWar(mover.Id, target.Id)) return $"The ground: {mover.Name} is at war with {target.Name}.";
        if (d <= -30) return $"The ground: {mh!.Name} thinks ill of {th!.Name} ({d}), and has the votes to say so.";
        return $"The ground: things have been put about against {target.Name}, and {mover.Name} has taken them up.";
    }

    public string Describe(World w, Motion m)
    {
        var def = _motions.Def(m.Kind); if (def == null) return m.Kind;
        string target = def.Target == "region" ? w.Galaxy?.Regions.FirstOrDefault(r => r.Id == m.TargetId)?.Name ?? m.TargetId : def.Target == "house" ? w.House(m.TargetId)?.Name ?? m.TargetId : def.Target == "nation" ? Nations.Def(m.TargetId)?.Name ?? m.TargetId : def.Target == "edict" ? EdictSystem.Title(w, m.TargetId) : "";
        return def.Description.Replace("{proposer}", ProposerName(w, m)).Replace("{target}", target).Replace("{office}", _offices.Def(m.OfficeId)?.Name ?? m.OfficeId).Replace("{rank}", _titles.ById(m.TitleId)?.NameIn(w.Era) ?? m.TitleId)
            .Replace("{law}", Policies.Law(m.LawId)?.Name.ToLowerInvariant() ?? m.LawId).Replace("{option}", Policies.Law(m.LawId)?.Option(m.LawOption)?.Name.ToLowerInvariant() ?? m.LawOption);
    }

    public void Tick(World w)
    {
        if (_motions.IsEmpty) return;
        var mine = w.Protagonist?.HouseId;
        // last turn's news is cleared before this turn's is written — the offices' and the benches' too, since resolving a motion sets theirs
        foreach (var f in TurnFlags.Concat(OfficeSystem.TurnFlags).Concat(FactionSystem.TurnFlags).Concat(ElectionSystem.TurnFlags).Concat(NationSystem.SenateTurnFlags)) w.WorldFlags.Remove(f);
        foreach (var m in Docket(w).Where(m => m.ProposedTurn < w.Turn)) (m.ForecastFor, m.ForecastAgainst) = Tally(w, m);   // where it stood before the chair spoke
        ElectionSystem.RivalVetoes(w, _offices, this);   // the consuls strike what strikes at them, before the vote
        LadderSystem.ChairActs(w, this);   // the Speaker orders the docket and the presiding consul speaks from the chair (docs/LADDER.md, "The chair")
        foreach (var m in Docket(w).Where(m => m.ProposedTurn < w.Turn).ToList()) Resolve(w, m, mine);
        OfficeSystem.Tick(w, _offices);
        if (LadderSystem.Due(w, _offices)) LadderSystem.Hold(w, _offices, id => Weight(w, id));   // every fifth year the magistrates below the consulship (docs/LADDER.md), before the consuls
        if (ElectionSystem.Due(w)) ElectionSystem.Hold(w, _offices, id => Weight(w, id));   // every tenth turn the consuls are elected (docs/REPUBLIC.md)
        w.WorldFlags.Remove("election_near"); if (w.Era != "imperium" && ElectionSystem.NextIn(w) <= 2) w.WorldFlags.Add("election_near");
        FactionSystem.Tick(w, id => Weight(w, id));
        Petition(w);
        Propose(w, mine);
        SenateActs.Legislate(w, this, mine);   // the chamber's own law: a repeal by the house a law hurts, or an Act of a bench's platform (docs/SENATE.md, "The Senate's law, enlarged")
        // prune history
        var resolved = w.Motions.Where(m => m.Resolved).OrderByDescending(m => m.ResolvedTurn).ThenBy(m => m.Id, StringComparer.Ordinal).Skip(HistoryKept).ToList();
        foreach (var m in resolved) w.Motions.Remove(m);
        WriteFlags(w, mine);
    }

    /// <summary>What a house's patron wants of a motion: +1 carried, -1 thrown out, 0 no stake. The patron has a stake when it moved
    /// the motion or is its target.</summary>
    public int PatronWish(World w, Motion m, string houseId)
    {
        var h = w.House(houseId); if (h == null || h.PatronId.Length == 0) return 0; var def = _motions.Def(m.Kind); if (def == null) return 0;
        if (m.TargetId == h.PatronId && def.Target == "house") return def.Interest is "censure" or "embargo" or "breakup" ? -1 : 1;
        return m.ProposerHouseId == h.PatronId ? 1 : 0;
    }

    private void Resolve(World w, Motion m, string? mine)
    {
        var def = _motions.Def(m.Kind); if (def == null) { m.Resolved = true; return; }
        // a patron notices how a client votes on what touches the patron (docs/REPUBLIC.md stage 3): a vote the client cast, not one left to drift
        if (mine != null && m.Votes.TryGetValue(mine, out int cast) && cast != 0 && PatronWish(w, m, mine) is var wish && wish != 0 && w.House(mine) is { } client && w.House(client.PatronId) is { } patron
            && Economy.HeadOf(w, patron) is { } ph && Economy.HeadOf(w, client) is { } chd)
        {
            var rel = w.Relationship(ph.Id, chd.Id); bool with = cast == wish;
            rel.Disposition = Math.Clamp(rel.Disposition + (with ? ObligeRegard : DefyRegard), -100, 100);
            w.WorldFlags.Add(with ? ObligedPatronFlag : DefiedPatronFlag);
            w.Log(with ? $"{client.Name} votes as {patron.Name} wished. It is noted." : $"{client.Name} votes against the wishes of its patron, {patron.Name}. It is noted.");
        }
        // the rivals declare: the leaders of the benches first, then the rest by name. The order is fixed because a house leans with its
        // leader's vote when that is already cast; taken in the order the houses happen to be held, a house made after a save could vote
        // before its leader in one game and after in the same game resumed
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderBy(h => FactionSystem.Of(w, h.Id)?.LeaderHouseId == h.Id ? 0 : 1).ThenBy(h => h.Id, StringComparer.Ordinal).ToList())
            if (!m.Votes.ContainsKey(h.Id)) m.Votes[h.Id] = Math.Sign(Lean(w, m, h.Id));
        WealthSystem.FavoursCalled(w, m);   // a favour owed to the proposer is spent on this vote (docs/WEALTH.md "A gift that asks")
        DealingsSystem.MotionCounted(w, m);   // a vote pledged to another house was kept or broken (docs/WEALTH.md, "Dealings between houses")
        // a purse comes out one time in four: the vote it bought is struck, and the house that paid is thought the less of
        foreach (var (boughtId, payerId) in m.BoughtBy.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList())
        {
            if (!BribeComesOut(w, m, boughtId)) continue;
            m.Votes[boughtId] = 0; var payer = w.House(payerId); if (payer != null) LegitimacyBook.Move(w, payer, -BribeOutLegitimacy, LegitimacyBook.Scandal, "A bought vote came out on the floor");
            if (payerId == mine) w.WorldFlags.Add("bribe_out");
            w.Log($"It comes out on the floor that {payer?.Name ?? "a house"} paid {w.House(boughtId)?.Name} for its vote. The vote is struck, and {payer?.Name} is thought the less of.");
        }
        if (_fleets?.Catalog is { } provinceRules) ProvinceSystem.NoteVotes(w, provinceRules, m, def.Interest, def.Target == "house", mine, ObligeRegard, DefyRegard);   // the houses over those who voted take note (docs/PROVINCES.md)
        var (f, a) = Tally(w, m);
        Character? casting = null; bool byChair = f == a && f > 0 && LadderSystem.CastingVote(w, m, out casting);   // a tied Senate goes as the presiding consul voted
        m.ForWeight = f; m.AgainstWeight = a; m.Passed = f > a || byChair; m.Resolved = true; m.ResolvedTurn = w.Turn;
        if (Math.Abs(f - m.ForecastFor) + Math.Abs(a - m.ForecastAgainst) > Math.Max(10, (f + a) / 8))
        {
            var why = new List<string>();
            if (m.Chair != 0) why.Add($"{LadderSystem.Presiding(w)?.Name ?? "the presiding consul"} spoke {(m.Chair > 0 ? "for" : "against")} it from the chair");
            if (m.BoughtBy.Keys.Any(id => m.Votes.GetValueOrDefault(id) == 0 && BribeComesOut(w, m, id))) why.Add("a purse came out on the floor");
            m.Swing = $"it had stood at {m.ForecastFor} to {m.ForecastAgainst}" + (why.Count > 0 ? ": " + string.Join(", and ", why) : ": the houses' minds moved with the year's events and the favours called in");
        }
        if (mine != null && FactionSystem.Of(w, mine) is { } bench && bench.LeaderHouseId != mine && m.TargetId != mine && m.Votes.GetValueOrDefault(mine) != 0)
        {
            int expected = LeaderStance(w, m, bench);
            if (expected != 0 && m.Votes[mine] == -expected) FactionSystem.BreakWhip(w, mine, bench);
        }
        FactionSystem.RecordVotes(w, m, m.Kind, platform: !def.Rank);
        if (!IsPetition(m)) { string standingKey = "senate_standing:" + m.ProposerHouseId; w.WorldCounters[standingKey] = Math.Max(0, w.Counter(standingKey) + (m.Passed ? 1 : -1)); }
        if (PetitionEstate(m) is { } estate) Answer(w, m, estate, mine);
        string text = Describe(w, m);
        if (m.Passed)
        {
            w.WorldCounters["motions_passed"] = w.Counter("motions_passed") + 1;
            w.WorldCounters["motions_passed:" + m.ProposerHouseId] = w.Counter("motions_passed:" + m.ProposerHouseId) + 1;
            Apply(w, m, def, mine);
            if (m.ProposerHouseId == mine) w.WorldFlags.Add("motion_passed_mine");
            w.Log(byChair ? $"The Senate is tied, {f} to {a}, and {casting?.Name}, presiding, gives the casting vote for it: {text}" : $"The Senate carries it, {f} to {a}: {text}");
        }
        else
        {
            if (m.ProposerHouseId == mine) w.WorldFlags.Add("motion_failed_mine");
            if (def.Law) w.WorldFlags.Add("act_failed");
            if (def.Rank) TitleLadder?.Refused(w, m);
            if (def.Target == "house" && w.House(m.TargetId) is { } spared)
            {
                if (def.Interest == "triumph") Caesar?.TriumphVoted(w, spared, w.House(m.ProposerHouseId), passed: false);
                if (def.Interest == "recall") Caesar?.RecallVoted(w, spared, w.House(m.ProposerHouseId), passed: false);
                if (def.Interest == TriumphSystem.LeaveKind) TriumphSystem.LeaveVoted(w, spared, passed: false);
                if (def.Interest == "emergency") EmergencySystem.GrantVoted(w, spared, w.House(m.ProposerHouseId), _offices, passed: false);
                if (def.Interest == "for_life") EmergencySystem.ForLifeVoted(w, spared, passed: false);
            }
            w.Log($"The Senate throws it out, {a} to {f}: {text}");
        }
    }

    /// <summary>The chamber's answer to a petition, felt in every house of the Reach: heard, the estate's regard rises everywhere; turned away, it
    /// falls, and the worlds of every house it already distrusts stir. A house that has broken away is not moved by it.</summary>
    private static void Answer(World w, Motion m, string estate, string? mine)
    {
        var name = estate;
        foreach (var h in w.Houses.Values.Where(h => w.OfTheReach(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))   // not a house that has broken away: the Senate's law is not its law (docs/BREAKAWAY.md)
        {
            bool hostile = PolicySystem.Approval(w, h.Id, estate) <= PolicySystem.Hostile;
            if (h.Id == mine) { string off = "estate_offset:" + estate; w.WorldCounters[off] = Math.Clamp(w.Counter(off) + (m.Passed ? PetitionSwing : -PetitionSpurn), -100, 100); }
            else { string key = PolicySystem.ApprovalKey(w, h.Id, estate); w.WorldCounters[key] = Math.Clamp(w.Counter(key) + (m.Passed ? PetitionSwing : -PetitionSpurn), -100, 100); }
            if (!m.Passed && hostile) foreach (var x in w.HoldingsOf(h.Id).Where(x => x.IsPlanet)) x.Unrest = Math.Clamp(x.Unrest + PetitionUnrest, 0, 100);
        }
        w.WorldFlags.Add(m.Passed ? "petition_carried" : "petition_refused"); w.WorldFlags.Add((m.Passed ? "petition_carried:" : "petition_refused:") + estate);
        w.WorldCounters[m.Passed ? "petitions_heard" : "petitions_refused"] = w.Counter(m.Passed ? "petitions_heard" : "petitions_refused") + 1;
    }

    /// <summary>An estate hostile across the Reach brings its own Act (GAP §16 item 8): hostile in <see cref="PetitionMinHouses"/> houses or a
    /// eighth of them, with a Senate law whose option would please it, no petition already on the docket, and not lately heard.</summary>
    public void Petition(World w)
    {
        if (_motions.Def("petition") == null || Policies.IsEmpty || Docket(w).Count() >= DocketSize || w.Motions.Any(m => m.Open && IsPetition(m))) return;
        var houses = w.Houses.Values.Where(h => w.OfTheReach(h.Id)).ToList(); int need = Math.Max(PetitionMinHouses, houses.Count / 8);   // the houses of the Reach, whose law it would be
        foreach (var e in Policies.Estates.OrderByDescending(e => houses.Count(h => PolicySystem.Approval(w, h.Id, e.Id) <= PolicySystem.Hostile)).ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            int hostile = houses.Count(h => PolicySystem.Approval(w, h.Id, e.Id) <= PolicySystem.Hostile);
            if (hostile < need || w.Counter("petition_cd:" + e.Id) > w.Turn) continue;
            var want = WantedBy(w, e.Id); if (want == null) continue;
            w.WorldCounters["petition_cd:" + e.Id] = w.Turn + PetitionCooldown;
            var m = Add(w, EstatePrefix + e.Id, "petition", "", lawId: want.Value.law.Id, lawOption: want.Value.option.Id);
            w.Log($"{e.Name} are hostile in {hostile} houses of the Reach, and have come to the Senate's door.");
            return;
        }
    }

    /// <summary>The Senate law and option that would most please an estate against what is in force — null when none would.</summary>
    public (LawDef law, LawOptionDef option)? WantedBy(World w, string estateId)
    {
        (LawDef law, LawOptionDef option)? best = null; int bestDelta = 0;
        foreach (var law in Policies.Laws.Where(l => l.Scope == "senate").OrderBy(l => l.Id, StringComparer.Ordinal))
        {
            if (PolicySystem.OnCooldown(w, PolicySystem.SenateHolder, law.Id) || w.Motions.Any(m => m.Open && m.LawId == law.Id)) continue;
            var cur = PolicySystem.ReachOption(w, Policies, law) ?? law.DefaultOption;
            foreach (var o in law.Options.Where(o => o.Id != cur.Id).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                int delta = o.Opinions.GetValueOrDefault(estateId) - cur.Opinions.GetValueOrDefault(estateId);
                if (delta > bestDelta) { bestDelta = delta; best = (law, o); }
            }
        }
        return best;
    }

    private void Apply(World w, Motion m, MotionDef def, string? mine)
    {
        var proposer = w.House(m.ProposerHouseId); var target = def.Target == "house" ? w.House(m.TargetId) : null;
        if (target != null && def.LegitimacyTarget != 0)
        {
            LegitimacyBook.Move(w, target, def.LegitimacyTarget, LegitimacyBook.Senate, def.Name + ", carried in the Senate");
            if (target.Id == mine) w.WorldFlags.Add(def.LegitimacyTarget < 0 ? "censured" : "recognised");
            if (def.LegitimacyTarget < 0) w.WorldCounters[CaesarSystem.CensuredKey(target.Id)] = w.Turn;   // a cause, to a general who wants one
        }
        if (proposer != null && def.LegitimacyProposer != 0) LegitimacyBook.Move(w, proposer, def.LegitimacyProposer, LegitimacyBook.Senate, def.Name + ", moved and carried");
        if (def.LevyCredits > 0)
        {
            var chair = RegionSystem.CapitalHolder(w);
            foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != chair?.Id && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                int paid = Math.Min(def.LevyCredits, Math.Max(0, h.Treasury.Credits)); h.Treasury.Credits -= def.LevyCredits;
                if (chair != null) chair.Treasury.Credits += paid;
                if (h.Id == mine) w.WorldFlags.Add("levied");
            }
            if (chair != null) LegitimacyBook.Move(w, chair, 2, LegitimacyBook.Senate, "The levy, paid to the chair");
        }
        if (def.Target == "region" && w.Galaxy != null)
        {
            bool paid = proposer == null || def.ReliefCost == 0 || proposer.Treasury.Credits >= def.ReliefCost;
            if (proposer != null && paid && def.ReliefCost > 0) proposer.Treasury.Credits -= def.ReliefCost;
            int scale = paid ? 1 : 2;
            var rs = RegionSystem.StateOf(w, m.TargetId);
            rs.Lawlessness = Math.Clamp(rs.Lawlessness + def.LawlessnessRegion / scale, 0, 100); rs.Allegiance = Math.Clamp(rs.Allegiance + def.AllegianceRegion / scale, 0, 100);
            foreach (var h in w.Holdings.Values.Where(x => x.IsPlanet && w.Galaxy.System(x.SystemId)?.RegionId == m.TargetId)) h.Prosperity = Math.Clamp(h.Prosperity + def.ProsperityRegion / scale, 0, 100);
        }
        if (def.Rank && target != null) TitleLadder?.Carried(w, m);
        if (def.Interest == "triumph" && target != null) Caesar?.TriumphVoted(w, target, proposer, passed: true);
        if (def.Interest == "recall" && target != null) Caesar?.RecallVoted(w, target, proposer, passed: true);
        if (def.Interest == TriumphSystem.LeaveKind && target != null) TriumphSystem.LeaveVoted(w, target, passed: true);
        if (def.Interest == ChargeSystem.HomeKind && target != null) ChargeSystem.Home(w, target, proposer);   // an exile called home (docs/LADDER.md)
        if (def.Interest == "emergency" && target != null) EmergencySystem.GrantVoted(w, target, proposer, _offices, passed: true);
        if (def.Interest == "for_life" && target != null) EmergencySystem.ForLifeVoted(w, target, passed: true);
        if (def.Interest == "reconquest" && target != null && Polities != null && PolitySystem.Of(w, target.Id) is { Recognised: > 0 } state) Polities.Reconquer(w, state);
        if (def.Target == "nation") { if (def.Interest == "foreign_peace") NationSystem.VotePeace(w, Nations, m.TargetId, m.ProposerHouseId); else NationSystem.DeclareWar(w, Nations, m.TargetId, m.ProposerHouseId); }
        if (def.Peace && target != null && _fleets != null)
            foreach (var enemy in w.EnemiesOf(target.Id).ToList()) _fleets.MakePeace(w, target.Id, enemy, terms: false);
        if (def.Office && target != null && _offices.Def(m.OfficeId) is { } office)
        {
            var who = OfficeSystem.Candidate(w, office, target.Id);
            if (who != null) OfficeSystem.Grant(w, office, target.Id, who);
            else w.Log($"The commission to {target.Name} lapses: no one of the house has the rank for the {office.Name}.");
        }
        if (def.Law && Policies.Law(m.LawId) is { } law && law.Option(m.LawOption) is { } opt)
        {
            PolicySystem.Enact(w, Policies, PolicySystem.SenateHolder, law.Id, opt.Id);
            w.WorldFlags.Add("act_passed"); w.WorldFlags.Add("act_passed:" + law.Id); w.WorldCounters["acts_passed"] = w.Counter("acts_passed") + 1;
            w.Log($"An Act of the Senate: {law.Name.ToLowerInvariant()} is now {opt.Name.ToLowerInvariant()} throughout the Reach.");
        }
        if (def.EmbargoTurns > 0 && target != null)
        {
            w.WorldCounters["embargo:" + target.Id] = w.Turn + def.EmbargoTurns;
            if (target.Id == mine) w.WorldFlags.Add("embargoed");
        }
        if (def.Breakup && target != null && Exchange != null && CompanySystem.ControlledBy(w, target.Id).Where(c => !c.Chartered).OrderByDescending(CompanySystem.Cap).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault() is { } broken)
        {
            if (target.Id == mine) w.WorldFlags.Add("company_broken_up");
            Exchange.Liquidate(w, broken, "the Senate has broken it up");
        }
        if (def.Rescue && Exchange != null) Exchange.Rescue(w, "the Senate votes the Reach's purse behind the Exchange");
        if (def.Interest == EdictSystem.AnnulKind) EdictSystem.AnnulCarried(w, m.TargetId);   // the edict ends at once (docs/EDICTS.md)
    }

    /// <summary>Is the house shut out of the exchanges by a Senate embargo this turn?</summary>
    public static bool Embargoed(World w, string houseId) => w.Counter("embargo:" + houseId) > w.Turn;

    /// <summary>Rival houses bring their own business before the Senate (GDD §3 abstracted rivals): one new motion a turn at most, roughly one
    /// turn in three, from the most influential house with a reason — a losing war to end, a shaky title to have recognised, an empty chair's
    /// coffers to fill, a lawless home region, a hated rival to censure or embargo.</summary>
    private void Propose(World w, string? mine)
    {
        if (Docket(w).Count() >= DocketSize || EdictSystem.Cowed(w, "") != null) return;   // nobody brings business while the holder rules by decree (docs/EDICTS.md)
        if (Generals(w, mine)) return;
        if (EdictSystem.MoveAnnulment(w, this, mine)) return;   // a house an edict galls moves to have it struck down
        uint hash = 2166136261; foreach (var ch in "senate:" + w.Turn) hash = (hash ^ ch) * 16777619;
        if (hash % 3 != 0) return;
        var g = w.Galaxy;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && MayPropose(w, h.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id))
                 .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenByDescending(h => _titles.RankOf(h.Title)).ThenBy(h => h.Id, StringComparer.Ordinal))
        {
            var head = Economy.HeadOf(w, h); if (head == null) continue;
            var war = w.Wars.Where(x => x.Involves(h.Id)).OrderBy(x => x.Lead(h.Id)).FirstOrDefault();
            if (war != null && war.Lead(h.Id) <= -3 && _motions.Def("peace") != null && !Stale(w, h.Id, "peace", h.Id)) { Add(w, h.Id, "peace", h.Id); return; }
            if (PolitySystem.StirredAgainst(w, h.Id) is { Legitimacy: > 20 } talkedOf && talkedOf.Id != h.Id && _motions.Def("censure") != null && !Stale(w, h.Id, "censure", talkedOf.Id))
            {
                Add(w, h.Id, "censure", talkedOf.Id); w.WorldCounters["censures_stirred"] = w.Counter("censures_stirred") + 1;
                w.Log($"{h.Name} moves a censure of {talkedOf.Name}. It has heard a great deal about that house lately, and has believed it."); return;
            }
            if (_motions.Def("foreign_peace") != null && WantedPeace(w, h) is { } quiet && !Stale(w, h.Id, "foreign_peace", quiet)) { Add(w, h.Id, "foreign_peace", quiet); w.WorldCounters["foreign_peace_moved"] = w.Counter("foreign_peace_moved") + 1; return; }
            if (_motions.Def("foreign_war") != null && WantedWar(w, h) is { } nation && !Stale(w, h.Id, "foreign_war", nation)) { Add(w, h.Id, "foreign_war", nation); w.WorldCounters["foreign_war_moved:" + nation] = w.Counter("foreign_war_moved:" + nation) + 1; return; }
            if (_motions.Def("commission") != null)
                foreach (var office in _offices.Defs.Where(o => !o.Elected).OrderBy(o => o.Id, StringComparer.Ordinal))
                {
                    var holder = OfficeSystem.HolderHouse(w, office.Id); if (holder == h.Id || OfficeSystem.Candidate(w, office, h.Id) == null) continue;
                    if (office.CommandsFleets && RegionSystem.CapitalHolder(w) is { } ch && ch.Id != h.Id && w.AtWar(h.Id, ch.Id)) continue;   // the chair will not arm its enemy
                    var holderHead = holder != null && w.House(holder) is { } hh ? Economy.HeadOf(w, hh) : null;
                    bool wants = holder == null || (holderHead != null && (w.GetRelationship(head.Id, holderHead.Id)?.Disposition ?? 0) <= -20) || w.AtWar(h.Id, holder);
                    // a house that many others depend on reaches for the post its order fits, whoever holds it: that is what the sway is for
                    string fits = office.Track == "military" ? OrderSystem.Arms : office.Track == "stewardship" ? OrderSystem.Commerce : OrderSystem.Senate;
                    if (!wants && !w.IsImperium && OrderSystem.Tier(h, fits) >= 1 && OrderSystem.Swayed(w, h).Sum(x => x.lean) >= 6) wants = true;
                    if (wants && !Stale(w, h.Id, "commission", h.Id + ":" + office.Id)) { Add(w, h.Id, "commission", h.Id, office.Id); return; }
                }
            var title = _titles.ById(h.Title);
            if (title != null && title.LegitimacyRequirement > h.Legitimacy && _motions.Def("recognition") != null && !Stale(w, h.Id, "recognition", h.Id)) { Add(w, h.Id, "recognition", h.Id); return; }
            if (Exchange != null && w.WorldFlags.Contains(CompanySystem.CrashFlag) && CompanySystem.PaperOf(w, h.Id) >= 40 && _motions.Def("rescue") != null && !Stale(w, h.Id, "rescue", "")) { Add(w, h.Id, "rescue", ""); return; }
            if (RegionSystem.CapitalHolder(w)?.Id == h.Id && h.Treasury.Credits < 40 && _motions.Def("levy") != null && !Stale(w, h.Id, "levy", "")) { Add(w, h.Id, "levy", ""); return; }
            if (g != null && _motions.Def("relief") != null)
            {
                var lawless = w.HoldingsOf(h.Id).Where(x => x.IsPlanet).Select(x => g.System(x.SystemId)?.RegionId ?? "").Where(r => r.Length > 0 && RegionSystem.StateOf(w, r).Lawlessness >= 50).OrderBy(r => r, StringComparer.Ordinal).FirstOrDefault();
                if (lawless != null && h.Treasury.Credits >= 20 && !Stale(w, h.Id, "relief", lawless)) { Add(w, h.Id, "relief", lawless); return; }
            }
            if (_motions.Def("act") != null && WantedAct(w, h) is { } act && !Stale(w, h.Id, "act", act.law.Id + ":" + act.option.Id)) { Add(w, h.Id, "act", "", lawId: act.law.Id, lawOption: act.option.Id); return; }
            var rival = w.Houses.Values.Where(r => r.Id != h.Id && !World.Outsider(r.Id) && Economy.HeadOf(w, r) is { } rh && (w.GetRelationship(head.Id, rh.Id)?.Disposition ?? 0) <= -30)
                .OrderBy(r => w.GetRelationship(head.Id, Economy.HeadOf(w, r)!.Id)!.Disposition).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (rival != null)
            {
                if (w.AtWar(h.Id, rival.Id) && _motions.Def("embargo") != null && !Stale(w, h.Id, "embargo", rival.Id)) { Add(w, h.Id, "embargo", rival.Id); return; }
                if (rival.Legitimacy > 20 && _motions.Def("censure") != null && !Stale(w, h.Id, "censure", rival.Id)) { Add(w, h.Id, "censure", rival.Id); return; }
            }
        }
    }

    /// <summary>The generals' business (docs/CAESAR.md) is moved whoever else has business: a house asks a triumph for its own general
    /// who has earned one; a house that hates a general with sway, or one that guards the Senate (the chair, a consul) against one
    /// with a great deal, moves to have him home. A recall is a serious act: it is not moved against every commander who has won twice.
    /// True if something was moved.</summary>
    private bool Generals(World w, string? mine)
    {
        // going back for a state: the capital's holder moves it once the treaty has lapsed, the Reach is rested, and what would be sent is twice what would be met
        if (Polities != null && _motions.Def("reconquest") != null && !w.Motions.Any(m => m.Open && m.Kind == "reconquest") && RegionSystem.CapitalHolder(w) is { } seat && seat.Id != mine
            && PolitySystem.InReach(w, seat.Id) && MayPropose(w, seat.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == seat.Id))
            foreach (var p in w.Polities.Where(p => w.House(p.HeadHouseId) is { } ruler && Polities.ReconquestRefusal(w, ruler) == null).OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                int theirs = p.Members.Sum(id => FleetSystem.StrengthOf(w, id));
                int ours = FleetSystem.StrengthOf(w, seat.Id) + w.Houses.Values.Where(h => h.Id != seat.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id)).Select(h => FleetSystem.StrengthOf(w, h.Id)).OrderByDescending(x => x).Take(3).Sum();
                if (ours >= 2 * Math.Max(1, theirs) && !p.Leverage && !Stale(w, seat.Id, "reconquest", p.HeadHouseId)) { Add(w, seat.Id, "reconquest", p.HeadHouseId); return true; }
            }
        if (!w.IsImperium && _motions.Def("emergency") != null && !w.Motions.Any(m => m.Open && m.Kind == "emergency")
            && EmergencySystem.WouldMove(w, _offices, h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && MayPropose(w, h.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id) && Economy.HeadOf(w, h) != null) is { } powers && !Stale(w, powers.mover.Id, "emergency", powers.target.Id))
        { Add(w, powers.mover.Id, "emergency", powers.target.Id); w.WorldCounters[EmergencySystem.MovedKey] = w.Counter(EmergencySystem.MovedKey) + 1; return true; }
        if (ChargeSystem.MoveHome(w, this, mine)) return true;   // an exile's house, or a friend, moves to have them home (docs/LADDER.md)
        if (Caesar == null || w.IsImperium || w.CivilWar != null) return false;
        bool Can(House h, string kind = "recall") => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && (MayPropose(w, h.Id) || TriumphSystem.MayMove(w, Economy.HeadOf(w, h), _motions.Def(kind)))   // anyone who has held a magistracy may move a triumph (docs/GAPS.md Q15)
            && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id) && Economy.HeadOf(w, h) != null;
        if (_motions.Def("triumph") != null && !w.Motions.Any(m => m.Open && m.Kind == "triumph"))
        {
            var earned = w.Houses.Values.Where(h => !World.Outsider(h.Id) && Caesar.TriumphRefusal(w, h) == null).OrderByDescending(h => CaesarSystem.Laurels(w, CaesarSystem.GeneralOf(w, h)!)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
            foreach (var t in earned)
            {
                // the house itself, or a friend of standing if it has no voice of its own
                var mover = Can(t, "triumph") && !Stale(w, t.Id, "triumph", t.Id) ? t : w.Houses.Values.Where(h => Can(h, "triumph") && !Stale(w, h.Id, "triumph", t.Id) && Economy.HeadOf(w, t) is { } th && (w.GetRelationship(Economy.HeadOf(w, h)!.Id, th.Id)?.Disposition ?? 0) >= 30)
                    .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (mover != null && t.Id != mine) { Add(w, mover.Id, "triumph", t.Id); w.WorldCounters["triumphs_moved"] = w.Counter("triumphs_moved") + 1; return true; }
            }
        }
        if (_motions.Def("recall") != null && !w.Motions.Any(m => m.Open && m.Kind == "recall"))
            foreach (var t in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != RegionSystem.CapitalHolder(w)?.Id && Caesar.RecallRefusal(w, h) == null).OrderByDescending(h => CaesarSystem.Sway(w, CaesarSystem.GeneralOf(w, h)!)).ThenBy(h => h.Id, StringComparer.Ordinal))
            {
                var tg = CaesarSystem.GeneralOf(w, t)!; int sway = CaesarSystem.Sway(w, tg); var th = Economy.HeadOf(w, t); if (th == null) continue;
                bool great = CaesarSystem.FirstRank(w, tg) && CaesarSystem.Loved(w, tg, t);   // a commander of the first rank, and loved: the Senate's guardians always move to have that one home
                bool defies = NationSystem.DefiedBy(w, t.Id) != null && NationSystem.Leeway(w, t) <= 1;   // a general who fights on against a peace the Senate voted: the chair and the consuls move to have him home, unless his name gives them pause
                var mover = w.Houses.Values.Where(h => h.Id != t.Id && Can(h) && !Stale(w, h.Id, "recall", t.Id)
                        && ((w.GetRelationship(Economy.HeadOf(w, h)!.Id, th.Id)?.Disposition ?? 0) <= -30 || ((sway >= CaesarSystem.AlarmSway || great || defies) && (RegionSystem.CapitalHolder(w)?.Id == h.Id || ElectionSystem.IsConsul(w, _offices, h.Id)))))
                    .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (mover != null) { Add(w, mover.Id, "recall", t.Id); w.WorldCounters["recalls_moved"] = w.Counter("recalls_moved") + 1; return true; }
            }
        return false;
    }

    /// <summary>A war a house would move (docs/FOREIGN_NATIONS.md): a Sword house, one whose head is a Commander or better, or one whose world was raided lately — while the Reach has hulls to send —
    /// names the nation it most wants to fight — one that raided the rim lately or whose regard has sunk — when the Reach fights no other.</summary>
    public const int PlunderWant = 40, RichAt = 8;
    public string? WantedWar(World w, House h)
    {
        if (Nations.IsEmpty || NationSystem.AtWar(w) != null || w.Motions.Any(m => m.Open && m.Kind == "foreign_war")) return null;   // (a house's own war is left to that house: nobody moves to make it the Reach's but the player)
        var head = Economy.HeadOf(w, h); var party = FactionSystem.Of(w, h.Id); var raider = NationSystem.RaiderOf(w, h.Id, 8);
        bool hawk = party?.PartyId == "sword" || (head != null && head.CareerTrack == "military" && head.CareerRank >= 2) || raider != null;
        if (!hawk || w.Wars.Any(x => x.Involves(h.Id))) return null;
        if (w.Fleets.Values.Where(f => !World.Outsider(f.OwnerId) && !f.IsAway).Sum(f => f.Strength) < 6) return null;   // the Reach must have fleets to send, if not this house's own
        // a war for plunder (docs/BEYOND.md, the Free Cities and the Hollow Crown): a house that wants a war and is short of money will
        // name a rich neighbour on the map, when no nation has given the Reach a better reason (those are named first, below)
        bool greedy = h.Treasury.Credits < PlunderWant;
        return w.Nations.Where(n => !n.Fallen && !n.Subdued(w.Turn) && Nations.Def(n.Id) is { } nd && (n.Disposition <= -30 || NationSystem.Raided(w, n.Id, 6) || (greedy && n.OnMap && nd.Wealth >= RichAt)))
            .OrderByDescending(n => n.Id == raider ? 2 : NationSystem.Raided(w, n.Id, 6) ? 1 : 0).ThenBy(n => n.Disposition).ThenBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
    }

    /// <summary>An Act a house would move: the Senate-scoped law and option that most pleases an estate hostile to it (or best serves its
    /// situation, by <see cref="ActLean"/>), not already the law, not already on the docket, not on the Senate's cooldown. Null when none would.</summary>
    public (LawDef law, LawOptionDef option)? WantedAct(World w, House h)
    {
        (LawDef law, LawOptionDef option)? best = null; int bestLean = 1;   // it must want it more than a shrug
        foreach (var law in Policies.Laws.Where(l => l.Scope == "senate").OrderBy(l => l.Id, StringComparer.Ordinal))
        {
            if (PolicySystem.OnCooldown(w, PolicySystem.SenateHolder, law.Id) || w.Motions.Any(m => m.Open && m.LawId == law.Id)) continue;
            var cur = PolicySystem.ReachOption(w, Policies, law) ?? law.DefaultOption;
            foreach (var o in law.Options.Where(o => o.Id != cur.Id).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                int lean = ActLean(w, h, new Motion { Id = "probe", Kind = "act", LawId = law.Id, LawOption = o.Id });
                if (lean > bestLean) { bestLean = lean; best = (law, o); }
            }
        }
        return best;
    }

    /// <summary>The chamber tires of the same motion: the same house may not move the same kind against the same target for <see cref="RepeatTurns"/> turns.</summary>
    public static bool Stale(World w, string proposerHouseId, string kind, string targetId) => w.Counter($"motion_cd:{proposerHouseId}:{kind}:{targetId}") > w.Turn;

    /// <summary>How a motion would stand if it were moved now, before anything is paid to move it: the weight for and against as the
    /// houses lean today, reckoned exactly as the docket's own count is (docs/PLAYTHROUGH.md, the Senate: "how it would fare, before
    /// paying to move it"). Nothing is written. The vote itself is at the end of the next year, and leans move in between.</summary>
    public (int forWeight, int againstWeight) Forecast(World w, string proposerHouseId, string kind, string targetId, string officeId = "", string lawId = "", string lawOption = "", string titleId = "")
    {
        var m = new Motion { Id = $"motion_{w.Counter("motions_proposed") + 1}", Kind = kind, ProposerHouseId = proposerHouseId, TargetId = targetId, OfficeId = officeId, LawId = lawId, LawOption = lawOption, TitleId = titleId, ProposedTurn = w.Turn };
        int f = 0, a = 0;
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id))) { int s = Math.Sign(LeanNow(w, m, h.Id, null)); if (s > 0) f += Weight(w, h.Id); else if (s < 0) a += Weight(w, h.Id); }
        return (f, a);
    }

    public Motion Add(World w, string proposerHouseId, string kind, string targetId, string officeId = "", string lawId = "", string lawOption = "", string titleId = "")
    {
        int n = w.Counter("motions_proposed") + 1; w.WorldCounters["motions_proposed"] = n;
        w.WorldCounters[$"motion_cd:{proposerHouseId}:{kind}:{(lawId.Length > 0 ? lawId + ":" + lawOption : officeId.Length > 0 ? targetId + ":" + officeId : titleId.Length > 0 ? targetId + ":" + titleId : targetId)}"] = w.Turn + RepeatTurns;
        var m = new Motion { Id = $"motion_{n}", Kind = kind, ProposerHouseId = proposerHouseId, TargetId = targetId, OfficeId = officeId, LawId = lawId, LawOption = lawOption, TitleId = titleId, ProposedTurn = w.Turn };
        w.Motions.Add(m);
        w.Log($"Before the Senate: {Describe(w, m)} The vote is next session.");
        // a house the player owes a favour to has moved: this is where it is called in, and the player is told so
        if (w.Protagonist?.HouseId is { } owing && owing != proposerHouseId && WealthSystem.FavourOwed(w, owing, proposerHouseId) && w.House(proposerHouseId) is { } owed)
            w.Tell("warn", $"{owed.Name} has moved a motion, and you owe it a favour: this is where it is called in. Vote for it and the account is even; vote otherwise and its head thinks {-WealthSystem.FavourBroken:+0;-0} of you.", "senate:chamber", owed.Id);
        return m;
    }

    private void WriteFlags(World w, string? mine)
    {
        foreach (var f in w.WorldFlags.Where(f => f is "motion_open" or "motion_against_me" or "embargoed" or "act_open" or "petition_open" || f.StartsWith("petition_open:")).ToList()) w.WorldFlags.Remove(f);
        if (mine == null) return;
        var open = Docket(w).ToList();
        if (open.Count > 0) w.WorldFlags.Add("motion_open");
        if (open.Any(m => _motions.Def(m.Kind)?.Law == true)) w.WorldFlags.Add("act_open");
        foreach (var e in open.Select(PetitionEstate).Where(e => e != null)) { w.WorldFlags.Add("petition_open"); w.WorldFlags.Add("petition_open:" + e); }
        if (open.Any(m => m.TargetId == mine && _motions.Def(m.Kind) is { } d && d.Target == "house" && (d.LegitimacyTarget < 0 || d.EmbargoTurns > 0))) w.WorldFlags.Add("motion_against_me");
        if (Embargoed(w, mine)) w.WorldFlags.Add("embargoed");
    }
}
