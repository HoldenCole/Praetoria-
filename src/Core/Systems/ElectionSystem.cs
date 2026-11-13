using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The consulship (docs/REPUBLIC.md, stage 2). Every <see cref="Every"/> turns the Senate elects the two consuls — the elected offices of
/// <c>content/senate/offices.json</c>. Each party nominates its weightiest member with a qualified candidate who was not consul last term;
/// the player may <b>stand</b> (StandForConsulCommand) and runs as their party's nominee, or alone. Every house's weight goes to its party's
/// nominee; a house with no party votes for the nominee its head likes best. The two highest tallies take the senior and junior chairs. A
/// consul may <b>veto</b> one open motion a term (VetoCommand; rival consuls veto what strikes at their house or their party). Flags:
/// <c>election_near</c>, <c>election_held</c>, <c>consul_elected</c>, <c>consul_lost</c>, <c>vetoed_mine</c>; counters <c>elections</c>,
/// <c>consul_term:&lt;house&gt;</c> (the turn last elected), <c>veto_used:&lt;house&gt;</c> (the turn of the last veto).
/// </summary>
public static class ElectionSystem
{
    public const int Every = 10, StandInfluence = 2;
    public static readonly string[] TurnFlags = { "election_held", "consul_elected", "consul_lost", "vetoed_mine" };
    public static string StandFlag(string houseId) => "stands_for_consul:" + houseId;
    public static bool Due(World w) => w.Turn > 0 && w.Turn % Every == 0 && w.Era != "imperium";
    public static int NextIn(World w) => w.Turn % Every == 0 ? Every : Every - w.Turn % Every;
    public static IEnumerable<OfficeDef> ConsulOffices(OfficeCatalog offices) => offices.Defs.Where(d => d.Elected).OrderByDescending(d => d.Weight).ThenBy(d => d.Id, StringComparer.Ordinal);
    public static bool IsConsul(World w, OfficeCatalog offices, string houseId) => ConsulOffices(offices).Any(d => OfficeSystem.Holds(w, houseId, d.Id));
    /// <summary>Eligible: a qualified member for the office, and not elected at the last election.</summary>
    public static bool Eligible(World w, OfficeCatalog offices, string houseId) =>
        houseId != PirateSystem.HouseId && w.House(houseId) != null && ConsulOffices(offices).FirstOrDefault() is { } d && OfficeSystem.Candidate(w, d, houseId) != null && w.Turn - w.Counter("consul_term:" + houseId) >= Every * 2 - 1 || (w.Counter("consul_term:" + houseId) == 0 && houseId != PirateSystem.HouseId && w.House(houseId) != null && ConsulOffices(offices).FirstOrDefault() is { } d2 && OfficeSystem.Candidate(w, d2, houseId) != null);

    /// <summary>The nominees as they stand: one a party (the weightiest eligible member, or the player if standing), plus the player alone if unaffiliated and standing.</summary>
    public static List<(string houseId, string? partyId)> Nominees(World w, OfficeCatalog offices, Func<string, int> weight)
    {
        var list = new List<(string, string?)>(); var mine = w.Protagonist?.HouseId;
        foreach (var f in w.Factions.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            string? pick = mine != null && f.Members.Contains(mine) && w.HasFlag(StandFlag(mine)) && Eligible(w, offices, mine) ? mine
                : f.Members.Where(h => Eligible(w, offices, h) && !(h == mine && !w.HasFlag(StandFlag(mine)))).OrderByDescending(weight).ThenBy(h => h, StringComparer.Ordinal).FirstOrDefault();
            if (pick != null) list.Add((pick, f.Id));
        }
        if (mine != null && w.HasFlag(StandFlag(mine)) && Eligible(w, offices, mine) && !list.Any(x => x.Item1 == mine)) list.Add((mine, null));
        return list;
    }

    /// <summary>Each house's weight to its party's nominee; an unaffiliated house to the nominee its head likes best (ties by weight).</summary>
    public static Dictionary<string, int> Tally(World w, OfficeCatalog offices, Func<string, int> weight, List<(string houseId, string? partyId)> nominees)
    {
        var tally = nominees.ToDictionary(n => n.houseId, _ => 0);
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId))
        {
            var party = FactionSystem.Of(w, h.Id); string? vote = null;
            if (nominees.Any(n => n.houseId == h.Id)) vote = h.Id;
            else if (h.PatronId.Length > 0 && nominees.Any(n => n.houseId == h.PatronId)) vote = h.PatronId;   // a client votes for the patron, whatever the party says
            else if (party != null) vote = nominees.FirstOrDefault(n => n.partyId == party.Id).houseId;
            if (vote == null && nominees.Count > 0)
            {
                var head = Economy.HeadOf(w, h);
                vote = nominees.OrderByDescending(n => head != null && w.House(n.houseId) is { } nh && Economy.HeadOf(w, nh) is { } nhead ? w.GetRelationship(head.Id, nhead.Id)?.Disposition ?? 0 : 0).ThenByDescending(n => weight(n.houseId)).ThenBy(n => n.houseId, StringComparer.Ordinal).First().houseId;
            }
            if (vote != null) tally[vote] = tally.GetValueOrDefault(vote) + weight(h.Id);
        }
        foreach (var n in nominees) tally[n.houseId] = tally.GetValueOrDefault(n.houseId) + w.Counter(OrderSystem.CanvassKey(n.houseId));   // coin spent on the voters in the nominee's name
        return tally;
    }

    public static void Hold(World w, OfficeCatalog offices, Func<string, int> weight)
    {
        var consuls = ConsulOffices(offices).ToList(); if (consuls.Count == 0) return;
        var mine = w.Protagonist?.HouseId;
        var nominees = Nominees(w, offices, weight); var tally = Tally(w, offices, weight, nominees);
        var winners = tally.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(consuls.Count).ToList();
        foreach (var d in consuls) if (OfficeSystem.Holding(w, d.Id) is { } old) { if (old.HouseId == mine) w.WorldFlags.Add("consul_lost"); OfficeSystem.Strip(w, d, "the term ends", false); }
        for (int i = 0; i < winners.Count; i++)
        {
            var who = OfficeSystem.Candidate(w, consuls[i], winners[i].Key); if (who == null) continue;
            OfficeSystem.Grant(w, consuls[i], winners[i].Key, who);
            w.WorldCounters["consul_term:" + winners[i].Key] = w.Turn;
            if (winners[i].Key == mine) { w.WorldFlags.Add("consul_elected"); w.WorldFlags.Remove("consul_lost"); }
        }
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("stands_for_consul:")).ToList()) w.WorldFlags.Remove(f);
        OrderSystem.SweepCanvass(w);
        w.WorldCounters["elections"] = w.Counter("elections") + 1; w.WorldFlags.Add("election_held");
        w.Log($"The Senate elects its consuls: {string.Join(", ", winners.Select((kv, i) => $"{w.House(kv.Key)?.Name ?? kv.Key} ({kv.Value}) as {consuls[Math.Min(i, consuls.Count - 1)].Name}"))}.{(nominees.Count > winners.Count ? " Also standing: " + string.Join(", ", tally.Where(kv => winners.All(x => x.Key != kv.Key)).OrderByDescending(kv => kv.Value).Select(kv => $"{w.House(kv.Key)?.Name ?? kv.Key} ({kv.Value})")) + "." : "")}");
    }

    /// <summary>A consul's veto: the motion is struck before the vote. Once a term.</summary>
    public static bool MayVeto(World w, OfficeCatalog offices, string houseId) =>
        ConsulOffices(offices).Any(d => d.Veto && OfficeSystem.Holds(w, houseId, d.Id)) && w.Counter("veto_used:" + houseId) != TermStart(w, offices, houseId);
    /// <summary>A term's mark: the turn the chair was taken plus one, so a veto is remembered against the term it was spent in.</summary>
    public static int TermStart(World w, OfficeCatalog offices, string houseId) => ConsulOffices(offices).Select(d => OfficeSystem.Holding(w, d.Id)).FirstOrDefault(h => h?.HouseId == houseId)?.SinceTurn + 1 ?? int.MaxValue;
    public static void Veto(World w, OfficeCatalog offices, string houseId, Motion m)
    {
        m.Resolved = true; m.Passed = false; m.ResolvedTurn = w.Turn; m.Vetoed = true; m.VetoedBy = houseId;
        w.WorldCounters["veto_used:" + houseId] = TermStart(w, offices, houseId); w.WorldCounters["vetoes"] = w.Counter("vetoes") + 1;
        if (m.ProposerHouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("vetoed_mine");
    }

    /// <summary>Rival consuls veto what strikes at them: a motion against their house, their party's leader, or an Act that undoes their party's line.</summary>
    public static void RivalVetoes(World w, OfficeCatalog offices, SenateSystem senate)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var d in ConsulOffices(offices).Where(d => d.Veto))
        {
            var h = OfficeSystem.Holding(w, d.Id); if (h == null || h.HouseId == mine || !MayVeto(w, offices, h.HouseId)) continue;
            var party = FactionSystem.Of(w, h.HouseId);
            foreach (var m in SenateSystem.Docket(w).Where(m => m.ProposedTurn < w.Turn && m.ProposerHouseId != h.HouseId).OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                var def = senate.Motions.Def(m.Kind); if (def == null) continue;
                bool against = def.Target == "house" && (def.LegitimacyTarget < 0 || def.EmbargoTurns > 0) && (m.TargetId == h.HouseId || (party != null && m.TargetId == party.LeaderHouseId));
                bool undoes = def.Law && party != null && party.Laws.TryGetValue(m.LawId, out var line) && line != m.LawOption && senate.Policies.Law(m.LawId) is { } ld && (PolicySystem.ReachOption(w, senate.Policies, ld) ?? ld.DefaultOption).Id == line;
                if (!against && !undoes) continue;
                Veto(w, offices, h.HouseId, m);
                w.Log($"{w.House(h.HouseId)?.Name ?? h.HouseId}, as {d.Name}, vetoes: {senate.Describe(w, m)}");
                break;
            }
        }
    }
}
