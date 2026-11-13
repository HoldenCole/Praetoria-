using Praetoria.Core.Diagnostics;
using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Senate as a place of action (GDD §6.1, §7; GAP §10 item 2). Houses move motions (<see cref="Motion"/>); every house votes by
/// <b>weight</b> — its Senate-sphere influence, its title rank, the chair's two, a Praetor's one — and rival houses lean by interest:
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
    public static readonly string[] TurnFlags = { "censured", "recognised", "levied", "motion_passed_mine", "motion_failed_mine", "act_passed", "act_failed", "petition_carried", "petition_refused" };
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

    /// <summary>A house's vote weight: Senate influence (the chair's two are in it) + title rank + one if its head is a Praetor or better.</summary>
    public int Weight(World w, string houseId) => ReadCache.Active ? ReadCache.Get("weight:" + houseId, () => WeightNow(w, houseId)) : WeightNow(w, houseId);
    private int WeightNow(World w, string houseId)
    {
        var h = w.House(houseId); if (h == null || houseId == PirateSystem.HouseId) return 0;
        int weight = h.SphereInfluence.GetValueOrDefault("senate") + _titles.RankOf(h.Title);
        var head = Economy.HeadOf(w, h); if (head is { CareerTrack: "law" } && head.CareerRank >= ProposerLawRank) weight += 1;
        weight += OfficeSystem.HeldBy(w, houseId).Sum(o => _offices.Def(o.OfficeId)?.Weight ?? 0);   // the Speaker's gavel
        weight += OrderSystem.WeightOf(w, h);                                                         // a great general, a great banker, a house with clients (docs/REPUBLIC.md stage 3)
        weight += PolicySystem.Modifier(w, Policies, houseId, "senate_weight");                       // the franchise, the bench's platform, the old blood's regard
        return Math.Max(1, weight);
    }

    /// <summary>The door by which a house sits in the Senate (docs/REPUBLIC.md stage 3): land (a Prefect or better), the bench (a head who
    /// is a Magistrate or better), the purse (enrolled by the census) or the sword (great in Arms). None: it votes, but may not move.</summary>
    public string? Door(World w, House h) => OrderSystem.Door(w, h, _titles, ProposerTitleRank, ProposerLawRank);

    /// <summary>May this house move a motion at all: a baron or better, or a head who is a Magistrate or better.</summary>
    public bool MayPropose(World w, string houseId)
    {
        var h = w.House(houseId); if (h == null) return false;
        return Door(w, h) != null;
    }

    public static IEnumerable<Motion> Docket(World w) => w.Motions.Where(m => m.Open).OrderBy(m => m.ProposedTurn).ThenBy(m => m.Id, StringComparer.Ordinal);

    /// <summary>How a rival house leans on a motion before lobbying: positive for, negative against, zero abstain. Deterministic.</summary>
    public int Lean(World w, Motion m, string houseId) => ReadCache.Active ? ReadCache.Get("lean:" + m.Id + ":" + houseId, () => LeanNow(w, m, houseId)) : LeanNow(w, m, houseId);
    private int LeanNow(World w, Motion m, string houseId)
    {
        int lean = RawLean(w, m, houseId);
        // bloc discipline (FactionSystem): a member leans toward its leader's stance on anything that does not target it; a whip on the leader is half a whip on the bench
        var f = FactionSystem.Of(w, houseId);
        if (f != null && m.TargetId != houseId && _motions.Def(m.Kind) is { } pd) lean += Math.Sign(f.Platform.GetValueOrDefault(pd.Interest));   // the party's stance on the kind
        if (f != null && f.LeaderHouseId != houseId && m.TargetId != houseId && m.ProposerHouseId != houseId)
        {
            int ls = FactionSystem.LeaderStance(w, m, f, id => RawLean(w, m, id));
            lean += ls * FactionSystem.Discipline + m.Lobby.GetValueOrDefault(f.LeaderHouseId) / 2;
        }
        return lean;
    }

    /// <summary>A house's lean by its own interests alone, before its bench is counted.</summary>
    public int RawLean(World w, Motion m, string houseId)
    {
        var def = _motions.Def(m.Kind); var h = w.House(houseId); if (def == null || h == null) return 0;
        if (houseId == m.ProposerHouseId) return 3;
        var head = Economy.HeadOf(w, h); var proposerHead = w.House(m.ProposerHouseId) is { } ph ? Economy.HeadOf(w, ph) : null;
        var targetHouse = def.Target == "house" ? w.House(m.TargetId) : null; var targetHead = targetHouse != null ? Economy.HeadOf(w, targetHouse) : null;
        int Disp(Character? from, Character? to) => from != null && to != null ? w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0 : 0;
        int toProposer = Disp(head, proposerHead), toTarget = Disp(head, targetHead);
        var proposerHouseForSway = w.House(m.ProposerHouseId);
        int lean = toProposer >= 20 ? 1 : toProposer <= -20 ? -1 : 0;
        // sway (docs/REPUBLIC.md stage 3): a house leans with whoever it depends on — its patron, its creditor, the house whose fuel
        // its fleets burn — for what that house wants, and against what is aimed at it
        if (proposerHouseForSway != null && proposerHouseForSway.Id != houseId) lean += Math.Min(2, OrderSystem.Sway(w, h, proposerHouseForSway).lean);
        if (targetHouse != null && targetHouse.Id != houseId)
        {
            int sway = OrderSystem.Sway(w, h, targetHouse).lean;
            if (def.Interest is "commission" or "recognition" or "peace") lean += sway;
            else if (def.Interest is "censure" or "embargo" or "breakup") lean -= sway;
        }
        switch (def.Interest)
        {
            case "censure":
            case "embargo":
                if (houseId == m.TargetId) lean -= 4;
                else if (targetHouse != null && (toTarget >= 30 || (h.Bloc.Length > 0 && h.Bloc == targetHouse.Bloc))) lean -= 2;
                else if (targetHouse != null && (toTarget <= -20 || w.AtWar(houseId, m.TargetId))) lean += 2;
                break;
            case "recognition":
                if (houseId == m.TargetId) lean += 4;
                else if (targetHouse != null && h.Claims.Contains(targetHouse.Title)) lean -= 2;          // a rival claimant to the same rung
                else lean += toTarget >= 10 ? 1 : toTarget <= -20 ? -1 : 0;
                break;
            case "levy":
                if (RegionSystem.CapitalHolder(w)?.Id == houseId) lean += 3;
                else if (h.Treasury.Credits < 20) lean -= 2;
                else lean -= 1;
                if (h.Treasury.Credits > 150) lean += 1;                                                  // the rich can afford to look generous
                break;
            case "relief":
                if (w.Galaxy != null && w.HoldingsOf(houseId).Any(x => x.IsPlanet && w.Galaxy.System(x.SystemId)?.RegionId == m.TargetId)) lean += 3;
                else if (w.Galaxy != null && w.HoldingsOf(houseId).Any(x => x.IsPlanet && w.Galaxy.RingOf(x.SystemId) == Galaxy.Ring.Rim)) lean += 1;   // the rim knows what lawless lanes cost
                break;
            case "commission":
                if (houseId == m.TargetId) lean += 4;
                else
                {
                    var holder = OfficeSystem.HolderHouse(w, m.OfficeId); var holderHouse = holder != null ? w.House(holder) : null; var holderHead = holderHouse != null ? Economy.HeadOf(w, holderHouse) : null;
                    if (holder == houseId) lean -= 4;                                                      // no one votes themselves out of office
                    else if (holderHouse != null && Disp(head, holderHead) >= 30) lean -= 2;             // the holder's friends
                    if (_offices.Def(m.OfficeId) is { } od && OfficeSystem.Candidate(w, od, houseId) is { } ownC && targetHouse != null && (OfficeSystem.Candidate(w, od, targetHouse.Id)?.CareerRank ?? 0) < ownC.CareerRank) lean -= 2;   // a house with a better candidate is jealous
                    lean += toTarget >= 10 ? 1 : toTarget <= -20 ? -2 : 0;
                    if (targetHouse != null && _offices.Def(m.OfficeId) is { } post)   // the chamber prefers a house of the order the post belongs to
                    {
                        string order = post.Track == "military" ? OrderSystem.Arms : post.Track == "stewardship" ? OrderSystem.Commerce : OrderSystem.Senate;
                        int tier = OrderSystem.Tier(targetHouse, order); lean += tier >= 2 ? 1 : tier == 0 && order != OrderSystem.Senate ? -1 : 0;
                    }
                    if (RegionSystem.CapitalHolder(w)?.Id == houseId && toTarget >= 0) lean += 1;         // the chair likes a safe pair of hands
                }
                break;
            case "breakup":
                if (houseId == m.TargetId) lean -= 4;
                else if (targetHouse != null && CompanySystem.ControlledBy(w, targetHouse.Id).Any(c => c.HeldBy(houseId) > 0)) lean -= 2;   // its other holders would be paid out at a loss
                else if (targetHouse != null && (toTarget <= -20 || w.AtWar(houseId, m.TargetId))) lean += 2;
                else if (targetHouse != null && toTarget >= 30) lean -= 2;
                lean += FactionSystem.Of(w, houseId)?.PartyId switch { "factors" => -1, "populares" => 1, _ => 0 };
                break;
            case "rescue":
                if (CompanySystem.PaperOf(w, houseId) >= 30) lean += 2;
                if (RegionSystem.CapitalHolder(w)?.Id == houseId) lean -= 2;                               // it is the chair's purse that opens
                lean += FactionSystem.Of(w, houseId)?.PartyId switch { "factors" => 2, "populares" => -1, _ => 0 };
                break;
            case "act":
                lean += ActLean(w, h, m);
                break;
            case "foreign_war":
                lean += ForeignWarLean(w, h, m.TargetId);
                break;
            case "peace":
                if (houseId == m.TargetId) lean += 3;
                else if (w.AtWar(houseId, m.TargetId)) { var war = w.Wars.FirstOrDefault(x => x.Between(houseId, m.TargetId)); lean += war != null && war.Lead(houseId) < 0 ? 1 : -3; }
                else lean += 1;                                                                            // peace is popular with the bystanders
                break;
        }
        uint hash = 2166136261; foreach (var ch in m.Id + ":" + houseId) hash = (hash ^ ch) * 16777619;
        lean += (int)(hash % 3) - 1;
        lean += m.Lobby.GetValueOrDefault(houseId);
        return lean;
    }

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
            int delta = next.Opinions.GetValueOrDefault(e.Id) - cur.Opinions.GetValueOrDefault(e.Id); if (delta == 0) continue;
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
        return lean;
    }

    /// <summary>The declared or predicted stance of a house: a declared vote if any, else the sign of its lean.</summary>
    public int Stance(World w, Motion m, string houseId) => m.Votes.TryGetValue(houseId, out var v) ? v : Math.Sign(Lean(w, m, houseId));

    public (int forWeight, int againstWeight) Tally(World w, Motion m)
    {
        int f = 0, a = 0;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId)) { int s = Stance(w, m, h.Id); if (s > 0) f += Weight(w, h.Id); else if (s < 0) a += Weight(w, h.Id); }
        return (f, a);
    }

    public string Describe(World w, Motion m)
    {
        var def = _motions.Def(m.Kind); if (def == null) return m.Kind;
        string target = def.Target == "region" ? w.Galaxy?.Regions.FirstOrDefault(r => r.Id == m.TargetId)?.Name ?? m.TargetId : def.Target == "house" ? w.House(m.TargetId)?.Name ?? m.TargetId : def.Target == "nation" ? Nations.Def(m.TargetId)?.Name ?? m.TargetId : "";
        return def.Description.Replace("{proposer}", ProposerName(w, m)).Replace("{target}", target).Replace("{office}", _offices.Def(m.OfficeId)?.Name ?? m.OfficeId)
            .Replace("{law}", Policies.Law(m.LawId)?.Name.ToLowerInvariant() ?? m.LawId).Replace("{option}", Policies.Law(m.LawId)?.Option(m.LawOption)?.Name.ToLowerInvariant() ?? m.LawOption);
    }

    public void Tick(World w)
    {
        if (_motions.IsEmpty) return;
        var mine = w.Protagonist?.HouseId;
        // last turn's news is cleared before this turn's is written — the offices' and the benches' too, since resolving a motion sets theirs
        foreach (var f in TurnFlags.Concat(OfficeSystem.TurnFlags).Concat(FactionSystem.TurnFlags).Concat(ElectionSystem.TurnFlags).Concat(NationSystem.SenateTurnFlags)) w.WorldFlags.Remove(f);
        ElectionSystem.RivalVetoes(w, _offices, this);   // the consuls strike what strikes at them, before the vote
        foreach (var m in Docket(w).Where(m => m.ProposedTurn < w.Turn).ToList()) Resolve(w, m, mine);
        OfficeSystem.Tick(w, _offices);
        if (ElectionSystem.Due(w)) ElectionSystem.Hold(w, _offices, id => Weight(w, id));   // every tenth turn the consuls are elected (docs/REPUBLIC.md)
        w.WorldFlags.Remove("election_near"); if (w.Era != "imperium" && ElectionSystem.NextIn(w) <= 2) w.WorldFlags.Add("election_near");
        FactionSystem.Tick(w, id => Weight(w, id));
        Petition(w);
        Propose(w, mine);
        // prune history
        var resolved = w.Motions.Where(m => m.Resolved).OrderByDescending(m => m.ResolvedTurn).ThenBy(m => m.Id, StringComparer.Ordinal).Skip(HistoryKept).ToList();
        foreach (var m in resolved) w.Motions.Remove(m);
        WriteFlags(w, mine);
    }

    private void Resolve(World w, Motion m, string? mine)
    {
        var def = _motions.Def(m.Kind); if (def == null) { m.Resolved = true; return; }
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId)) if (!m.Votes.ContainsKey(h.Id)) m.Votes[h.Id] = Math.Sign(Lean(w, m, h.Id));   // the rivals declare
        var (f, a) = Tally(w, m);
        m.ForWeight = f; m.AgainstWeight = a; m.Passed = f > a; m.Resolved = true; m.ResolvedTurn = w.Turn;
        if (mine != null && FactionSystem.Of(w, mine) is { } bench && bench.LeaderHouseId != mine && m.TargetId != mine && m.Votes.GetValueOrDefault(mine) != 0)
        {
            int expected = FactionSystem.LeaderStance(w, m, bench, id => RawLean(w, m, id));
            if (expected != 0 && m.Votes[mine] == -expected) FactionSystem.BreakWhip(w, mine, bench);
        }
        FactionSystem.RecordVotes(w, m, m.Kind);
        if (!IsPetition(m)) { string standingKey = "senate_standing:" + m.ProposerHouseId; w.WorldCounters[standingKey] = Math.Max(0, w.Counter(standingKey) + (m.Passed ? 1 : -1)); }
        if (PetitionEstate(m) is { } estate) Answer(w, m, estate, mine);
        string text = Describe(w, m);
        if (m.Passed)
        {
            w.WorldCounters["motions_passed"] = w.Counter("motions_passed") + 1;
            w.WorldCounters["motions_passed:" + m.ProposerHouseId] = w.Counter("motions_passed:" + m.ProposerHouseId) + 1;
            Apply(w, m, def, mine);
            if (m.ProposerHouseId == mine) w.WorldFlags.Add("motion_passed_mine");
            w.Log($"The Senate carries it, {f} to {a}: {text}");
        }
        else
        {
            if (m.ProposerHouseId == mine) w.WorldFlags.Add("motion_failed_mine");
            if (def.Law) w.WorldFlags.Add("act_failed");
            w.Log($"The Senate throws it out, {a} to {f}: {text}");
        }
    }

    /// <summary>The chamber's answer to a petition, felt in every house: heard, the estate's regard rises everywhere; turned away, it falls, and
    /// the worlds of every house it already distrusts stir.</summary>
    private static void Answer(World w, Motion m, string estate, string? mine)
    {
        var name = estate;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal))
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
        var houses = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).ToList(); int need = Math.Max(PetitionMinHouses, houses.Count / 8);
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
            target.Legitimacy = Math.Clamp(target.Legitimacy + def.LegitimacyTarget, 0, 100);
            if (target.Id == mine) w.WorldFlags.Add(def.LegitimacyTarget < 0 ? "censured" : "recognised");
        }
        if (proposer != null && def.LegitimacyProposer != 0) proposer.Legitimacy = Math.Clamp(proposer.Legitimacy + def.LegitimacyProposer, 0, 100);
        if (def.LevyCredits > 0)
        {
            var chair = RegionSystem.CapitalHolder(w);
            foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != chair?.Id && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                int paid = Math.Min(def.LevyCredits, Math.Max(0, h.Treasury.Credits)); h.Treasury.Credits -= def.LevyCredits;
                if (chair != null) chair.Treasury.Credits += paid;
                if (h.Id == mine) w.WorldFlags.Add("levied");
            }
            if (chair != null) chair.Legitimacy = Math.Min(100, chair.Legitimacy + 2);
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
        if (def.Target == "nation") NationSystem.DeclareWar(w, Nations, m.TargetId, m.ProposerHouseId);
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
    }

    /// <summary>Is the house shut out of the exchanges by a Senate embargo this turn?</summary>
    public static bool Embargoed(World w, string houseId) => w.Counter("embargo:" + houseId) > w.Turn;

    /// <summary>Rival houses bring their own business before the Senate (GDD §3 abstracted rivals): one new motion a turn at most, roughly one
    /// turn in three, from the most influential house with a reason — a losing war to end, a shaky title to have recognised, an empty chair's
    /// coffers to fill, a lawless home region, a hated rival to censure or embargo.</summary>
    private void Propose(World w, string? mine)
    {
        if (Docket(w).Count() >= DocketSize) return;
        uint hash = 2166136261; foreach (var ch in "senate:" + w.Turn) hash = (hash ^ ch) * 16777619;
        if (hash % 3 != 0) return;
        var g = w.Galaxy;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && h.Id != PirateSystem.HouseId && MayPropose(w, h.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id))
                 .OrderByDescending(h => h.SphereInfluence.GetValueOrDefault("senate")).ThenByDescending(h => _titles.RankOf(h.Title)).ThenBy(h => h.Id, StringComparer.Ordinal))
        {
            var head = Economy.HeadOf(w, h); if (head == null) continue;
            var war = w.Wars.Where(x => x.Involves(h.Id)).OrderBy(x => x.Lead(h.Id)).FirstOrDefault();
            if (war != null && war.Lead(h.Id) <= -3 && _motions.Def("peace") != null && !Stale(w, h.Id, "peace", h.Id)) { Add(w, h.Id, "peace", h.Id); return; }
            if (_motions.Def("foreign_war") != null && WantedWar(w, h) is { } nation && !Stale(w, h.Id, "foreign_war", nation)) { Add(w, h.Id, "foreign_war", nation); return; }
            if (_motions.Def("commission") != null)
                foreach (var office in _offices.Defs.Where(o => !o.Elected).OrderBy(o => o.Id, StringComparer.Ordinal))
                {
                    var holder = OfficeSystem.HolderHouse(w, office.Id); if (holder == h.Id || OfficeSystem.Candidate(w, office, h.Id) == null) continue;
                    if (office.CommandsFleets && RegionSystem.CapitalHolder(w) is { } ch && ch.Id != h.Id && w.AtWar(h.Id, ch.Id)) continue;   // the chair will not arm its enemy
                    var holderHead = holder != null && w.House(holder) is { } hh ? Economy.HeadOf(w, hh) : null;
                    bool wants = holder == null || (holderHead != null && (w.GetRelationship(head.Id, holderHead.Id)?.Disposition ?? 0) <= -20) || w.AtWar(h.Id, holder);
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
            var rival = w.Houses.Values.Where(r => r.Id != h.Id && r.Id != PirateSystem.HouseId && Economy.HeadOf(w, r) is { } rh && (w.GetRelationship(head.Id, rh.Id)?.Disposition ?? 0) <= -30)
                .OrderBy(r => w.GetRelationship(head.Id, Economy.HeadOf(w, r)!.Id)!.Disposition).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (rival != null)
            {
                if (w.AtWar(h.Id, rival.Id) && _motions.Def("embargo") != null && !Stale(w, h.Id, "embargo", rival.Id)) { Add(w, h.Id, "embargo", rival.Id); return; }
                if (rival.Legitimacy > 20 && _motions.Def("censure") != null && !Stale(w, h.Id, "censure", rival.Id)) { Add(w, h.Id, "censure", rival.Id); return; }
            }
        }
    }

    /// <summary>A war a house would move (docs/FOREIGN_NATIONS.md): a Sword house, one whose head is a Commander or better, or one whose world was raided lately — while the Reach has hulls to send —
    /// names the nation it most wants to fight — one that raided the rim lately or whose regard has sunk — when the Reach fights no other.</summary>
    public string? WantedWar(World w, House h)
    {
        if (Nations.IsEmpty || NationSystem.AtWar(w) != null || w.Motions.Any(m => m.Open && m.Kind == "foreign_war")) return null;
        var head = Economy.HeadOf(w, h); var party = FactionSystem.Of(w, h.Id); var raider = NationSystem.RaiderOf(w, h.Id, 8);
        bool hawk = party?.PartyId == "sword" || (head != null && head.CareerTrack == "military" && head.CareerRank >= 2) || raider != null;
        if (!hawk || w.Wars.Any(x => x.Involves(h.Id))) return null;
        if (w.Fleets.Values.Where(f => f.OwnerId != PirateSystem.HouseId && !f.IsAway).Sum(f => f.Strength) < 6) return null;   // the Reach must have fleets to send, if not this house's own
        return w.Nations.Where(n => !n.Subdued(w.Turn) && Nations.Def(n.Id) != null && (n.Disposition <= -30 || NationSystem.Raided(w, n.Id, 6)))
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

    public Motion Add(World w, string proposerHouseId, string kind, string targetId, string officeId = "", string lawId = "", string lawOption = "")
    {
        int n = w.Counter("motions_proposed") + 1; w.WorldCounters["motions_proposed"] = n;
        w.WorldCounters[$"motion_cd:{proposerHouseId}:{kind}:{(lawId.Length > 0 ? lawId + ":" + lawOption : officeId.Length > 0 ? targetId + ":" + officeId : targetId)}"] = w.Turn + RepeatTurns;
        var m = new Motion { Id = $"motion_{n}", Kind = kind, ProposerHouseId = proposerHouseId, TargetId = targetId, OfficeId = officeId, LawId = lawId, LawOption = lawOption, ProposedTurn = w.Turn };
        w.Motions.Add(m);
        w.Log($"Before the Senate: {Describe(w, m)} The vote is next session.");
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
