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
/// <c>motions_passed</c>, <c>motions_passed:&lt;house&gt;</c>, <c>embargo:&lt;house&gt;</c> (the turn it lifts).
/// </summary>
public sealed class SenateSystem
{
    public const int DocketSize = 2, HistoryKept = 12, LobbyWeight = 2, BribeWeight = 3, BribeCredits = 5, ProposerLawRank = 3, ProposerTitleRank = 2, RepeatTurns = 10;
    public static readonly string[] TurnFlags = { "censured", "recognised", "levied", "motion_passed_mine", "motion_failed_mine" };

    private readonly MotionCatalog _motions; private readonly TitleCatalog _titles; private readonly FleetSystem? _fleets;
    public SenateSystem(MotionCatalog motions, TitleCatalog titles, FleetSystem? fleets = null) { _motions = motions; _titles = titles; _fleets = fleets; }
    public MotionCatalog Motions => _motions;

    /// <summary>A house's vote weight: Senate influence (the chair's two are in it) + title rank + one if its head is a Praetor or better.</summary>
    public int Weight(World w, string houseId)
    {
        var h = w.House(houseId); if (h == null || houseId == PirateSystem.HouseId) return 0;
        int weight = h.SphereInfluence.GetValueOrDefault("senate") + _titles.RankOf(h.Title);
        var head = Economy.HeadOf(w, h); if (head is { CareerTrack: "law" } && head.CareerRank >= ProposerLawRank) weight += 1;
        return Math.Max(1, weight);
    }

    /// <summary>May this house move a motion at all: a baron or better, or a head who is a Magistrate or better.</summary>
    public bool MayPropose(World w, string houseId)
    {
        var h = w.House(houseId); if (h == null) return false;
        if (_titles.RankOf(h.Title) >= ProposerTitleRank) return true;
        var head = Economy.HeadOf(w, h); return head is { CareerTrack: "law" } && head.CareerRank >= ProposerLawRank;
    }

    public static IEnumerable<Motion> Docket(World w) => w.Motions.Where(m => m.Open).OrderBy(m => m.ProposedTurn).ThenBy(m => m.Id, StringComparer.Ordinal);

    /// <summary>How a rival house leans on a motion before lobbying: positive for, negative against, zero abstain. Deterministic.</summary>
    public int Lean(World w, Motion m, string houseId)
    {
        var def = _motions.Def(m.Kind); var h = w.House(houseId); if (def == null || h == null) return 0;
        if (houseId == m.ProposerHouseId) return 3;
        var head = Economy.HeadOf(w, h); var proposerHead = w.House(m.ProposerHouseId) is { } ph ? Economy.HeadOf(w, ph) : null;
        var targetHouse = def.Target == "house" ? w.House(m.TargetId) : null; var targetHead = targetHouse != null ? Economy.HeadOf(w, targetHouse) : null;
        int Disp(Character? from, Character? to) => from != null && to != null ? w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0 : 0;
        int toProposer = Disp(head, proposerHead), toTarget = Disp(head, targetHead);
        int lean = toProposer >= 20 ? 1 : toProposer <= -20 ? -1 : 0;
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
        string target = def.Target == "region" ? w.Galaxy?.Regions.FirstOrDefault(r => r.Id == m.TargetId)?.Name ?? m.TargetId : def.Target == "house" ? w.House(m.TargetId)?.Name ?? m.TargetId : "";
        return def.Description.Replace("{proposer}", w.House(m.ProposerHouseId)?.Name ?? m.ProposerHouseId).Replace("{target}", target);
    }

    public void Tick(World w)
    {
        if (_motions.IsEmpty) return;
        var mine = w.Protagonist?.HouseId;
        foreach (var f in TurnFlags) w.WorldFlags.Remove(f);
        foreach (var m in Docket(w).Where(m => m.ProposedTurn < w.Turn).ToList()) Resolve(w, m, mine);
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
        string standingKey = "senate_standing:" + m.ProposerHouseId;
        w.WorldCounters[standingKey] = Math.Max(0, w.Counter(standingKey) + (m.Passed ? 1 : -1));
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
            w.Log($"The Senate throws it out, {a} to {f}: {text}");
        }
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
        if (def.Peace && target != null && _fleets != null)
            foreach (var enemy in w.EnemiesOf(target.Id).ToList()) _fleets.MakePeace(w, target.Id, enemy, terms: false);
        if (def.EmbargoTurns > 0 && target != null)
        {
            w.WorldCounters["embargo:" + target.Id] = w.Turn + def.EmbargoTurns;
            if (target.Id == mine) w.WorldFlags.Add("embargoed");
        }
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
            var title = _titles.ById(h.Title);
            if (title != null && title.LegitimacyRequirement > h.Legitimacy && _motions.Def("recognition") != null && !Stale(w, h.Id, "recognition", h.Id)) { Add(w, h.Id, "recognition", h.Id); return; }
            if (RegionSystem.CapitalHolder(w)?.Id == h.Id && h.Treasury.Credits < 40 && _motions.Def("levy") != null && !Stale(w, h.Id, "levy", "")) { Add(w, h.Id, "levy", ""); return; }
            if (g != null && _motions.Def("relief") != null)
            {
                var lawless = w.HoldingsOf(h.Id).Where(x => x.IsPlanet).Select(x => g.System(x.SystemId)?.RegionId ?? "").Where(r => r.Length > 0 && RegionSystem.StateOf(w, r).Lawlessness >= 50).OrderBy(r => r, StringComparer.Ordinal).FirstOrDefault();
                if (lawless != null && h.Treasury.Credits >= 20 && !Stale(w, h.Id, "relief", lawless)) { Add(w, h.Id, "relief", lawless); return; }
            }
            var rival = w.Houses.Values.Where(r => r.Id != h.Id && r.Id != PirateSystem.HouseId && Economy.HeadOf(w, r) is { } rh && (w.GetRelationship(head.Id, rh.Id)?.Disposition ?? 0) <= -30)
                .OrderBy(r => w.GetRelationship(head.Id, Economy.HeadOf(w, r)!.Id)!.Disposition).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (rival != null)
            {
                if (w.AtWar(h.Id, rival.Id) && _motions.Def("embargo") != null && !Stale(w, h.Id, "embargo", rival.Id)) { Add(w, h.Id, "embargo", rival.Id); return; }
                if (rival.Legitimacy > 20 && _motions.Def("censure") != null && !Stale(w, h.Id, "censure", rival.Id)) { Add(w, h.Id, "censure", rival.Id); return; }
            }
        }
    }

    /// <summary>The chamber tires of the same motion: the same house may not move the same kind against the same target for <see cref="RepeatTurns"/> turns.</summary>
    public static bool Stale(World w, string proposerHouseId, string kind, string targetId) => w.Counter($"motion_cd:{proposerHouseId}:{kind}:{targetId}") > w.Turn;

    public Motion Add(World w, string proposerHouseId, string kind, string targetId)
    {
        int n = w.Counter("motions_proposed") + 1; w.WorldCounters["motions_proposed"] = n;
        w.WorldCounters[$"motion_cd:{proposerHouseId}:{kind}:{targetId}"] = w.Turn + RepeatTurns;
        var m = new Motion { Id = $"motion_{n}", Kind = kind, ProposerHouseId = proposerHouseId, TargetId = targetId, ProposedTurn = w.Turn };
        w.Motions.Add(m);
        w.Log($"Before the Senate: {Describe(w, m)} The vote is next session.");
        return m;
    }

    private void WriteFlags(World w, string? mine)
    {
        foreach (var f in new[] { "motion_open", "motion_against_me", "embargoed" }) w.WorldFlags.Remove(f);
        if (mine == null) return;
        var open = Docket(w).ToList();
        if (open.Count > 0) w.WorldFlags.Add("motion_open");
        if (open.Any(m => m.TargetId == mine && _motions.Def(m.Kind) is { } d && d.Target == "house" && (d.LegitimacyTarget < 0 || d.EmbargoTurns > 0))) w.WorldFlags.Add("motion_against_me");
        if (Embargoed(w, mine)) w.WorldFlags.Add("embargoed");
    }
}
