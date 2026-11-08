using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Persistent Senate factions (GDD §6.1 "voting blocs"; GAP §12 item 8). Every decided motion leaves an <b>affinity</b> between each pair
/// of houses that both declared: +1 for voting together, −1 for voting apart (counter <c>affinity:&lt;a&gt;|&lt;b&gt;</c>, clamped). When a
/// house outside any bloc has <see cref="FormThreshold"/> or more affinity with two others also outside one, a faction forms around the
/// weightiest of them; houses whose affinity with a leader reaches the threshold join (one a turn per bench); a member whose affinity
/// with its leader falls under one leaves; a bench of one dissolves; a dead or fallen leader is replaced by the weightiest member.
/// <b>Discipline</b>: a member leans <see cref="Discipline"/> toward its leader's stance on every motion that does not target it, and a
/// whip on the leader is half a whip on the bench. The player joins (two influence, if the leader does not dislike them), leaves (the
/// leader takes it ill), or founds a bench (two influence, two houses with the affinity). Flags: <c>in_faction</c>, <c>faction_leader</c>,
/// <c>faction_formed</c>, <c>faction_joined</c>, <c>faction_dissolved</c>, <c>broke_the_whip</c>, <c>faction_against_me</c>; counters
/// <c>factions</c>, <c>factions_formed</c>, <c>faction:&lt;house&gt;</c> (nothing; membership is on the faction).
/// </summary>
public static class FactionSystem
{
    public const int FormThreshold = 3, LeaveBelow = 1, AffinityMin = -6, AffinityMax = 12, Discipline = 2, JoinInfluence = 2, FoundInfluence = 2, BreakWhipAffinity = 2, BreakWhipDisposition = 5;
    public static readonly string[] TurnFlags = { "faction_formed", "faction_joined", "faction_dissolved", "broke_the_whip" };

    public static string AffinityKey(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? $"affinity:{a}|{b}" : $"affinity:{b}|{a}";
    public static int Affinity(World w, string a, string b) => a == b ? AffinityMax : w.Counter(AffinityKey(a, b));
    public static void AddAffinity(World w, string a, string b, int delta) { if (a == b) return; var k = AffinityKey(a, b); w.WorldCounters[k] = Math.Clamp(w.Counter(k) + delta, AffinityMin, AffinityMax); }

    public static Faction? Of(World w, string houseId) => w.Factions.FirstOrDefault(f => f.Members.Contains(houseId));
    public static bool Leads(World w, string houseId) => w.Factions.Any(f => f.LeaderHouseId == houseId);

    /// <summary>After a motion is decided: every pair that declared draws closer or apart; each bench's platform remembers the kind.</summary>
    public static void RecordVotes(World w, Motion m, string kind)
    {
        var declared = m.Votes.Where(kv => kv.Value != 0 && kv.Key != PirateSystem.HouseId && w.Houses.ContainsKey(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        for (int i = 0; i < declared.Count; i++)
            for (int j = i + 1; j < declared.Count; j++)
                AddAffinity(w, declared[i].Key, declared[j].Key, declared[i].Value == declared[j].Value ? 1 : -1);
        foreach (var f in w.Factions)
        {
            int net = f.Members.Sum(h => m.Votes.GetValueOrDefault(h));
            if (net != 0) f.Platform[kind] = f.Platform.GetValueOrDefault(kind) + Math.Sign(net);
            if (f.LeaderHouseId == m.ProposerHouseId) f.Record += m.Passed ? 1 : -1;
        }
    }

    /// <summary>The stance a member's bench expects of it: the leader's declared vote, else the sign of the leader's own lean (computed without discipline).</summary>
    public static int LeaderStance(World w, Motion m, Faction f, Func<string, int> rawLean) =>
        m.Votes.TryGetValue(f.LeaderHouseId, out var v) ? v : Math.Sign(rawLean(f.LeaderHouseId));

    public static void Tick(World w, Func<string, int> weight)
    {
        var mine = w.Protagonist?.HouseId;   // turn flags are cleared by SenateSystem.Tick before motions resolve
        // 1. the benches hold or break
        foreach (var f in w.Factions.ToList())
        {
            f.Members.RemoveAll(h => !w.Houses.ContainsKey(h));
            if (!w.Houses.ContainsKey(f.LeaderHouseId) || !f.Members.Contains(f.LeaderHouseId))
            {
                var next = f.Members.OrderByDescending(weight).ThenBy(h => h, StringComparer.Ordinal).FirstOrDefault();
                if (next != null) { f.LeaderHouseId = next; w.Log($"{w.House(next)?.Name} takes the lead of {f.Name}."); }
            }
            foreach (var h in f.Members.Where(h => h != f.LeaderHouseId && Affinity(w, h, f.LeaderHouseId) < LeaveBelow).ToList())
            {
                f.Members.Remove(h); w.Log($"{w.House(h)?.Name} breaks with {f.Name}.");
                if (h == mine) w.WorldFlags.Add("faction_dissolved");
            }
            if (f.Members.Count < 2)
            {
                w.Factions.Remove(f); w.Log($"{f.Name} is no more; its last benches sit apart.");
                if (f.Members.Contains(mine ?? "")) w.WorldFlags.Add("faction_dissolved");
            }
        }
        // 2. the benches grow: a house with the affinity joins a leader, one a bench a turn (the player chooses for themselves)
        foreach (var f in w.Factions.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var joiner = w.Houses.Values.Where(h => h.Id != mine && h.Id != PirateSystem.HouseId && Of(w, h.Id) == null && Affinity(w, h.Id, f.LeaderHouseId) >= FormThreshold)
                .OrderByDescending(h => Affinity(w, h.Id, f.LeaderHouseId)).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (joiner != null) { f.Members.Add(joiner.Id); w.Log($"{joiner.Name} takes its seat with {f.Name}."); }
        }
        // 3. a new bench forms around the weightiest unaligned house with two friends
        var free = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && Of(w, h.Id) == null).OrderByDescending(h => weight(h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        foreach (var leader in free.Where(h => h.Id != mine))
        {
            var friends = free.Where(h => h.Id != leader.Id && h.Id != mine && Affinity(w, h.Id, leader.Id) >= FormThreshold).Take(4).Select(h => h.Id).ToList();
            if (friends.Count < 2) continue;
            Form(w, leader.Id, friends, mine);
            break;
        }
        // 4. flags
        foreach (var fl in new[] { "in_faction", "faction_leader", "faction_against_me" }) w.WorldFlags.Remove(fl);
        w.WorldCounters["factions"] = w.Factions.Count;
        if (mine != null)
        {
            var f = Of(w, mine);
            if (f != null) { w.WorldFlags.Add("in_faction"); if (f.LeaderHouseId == mine) w.WorldFlags.Add("faction_leader"); }
            if (w.Motions.Any(m => m.Open && m.TargetId == mine && Leads(w, m.ProposerHouseId) && m.ProposerHouseId != mine)) w.WorldFlags.Add("faction_against_me");
        }
    }

    public static Faction Form(World w, string leaderId, IEnumerable<string> members, string? mine)
    {
        int n = w.Counter("factions_formed") + 1; w.WorldCounters["factions_formed"] = n;
        var f = new Faction { Id = $"faction_{n}", LeaderHouseId = leaderId, FormedTurn = w.Turn };
        f.Members.Add(leaderId); foreach (var m in members) if (!f.Members.Contains(m)) f.Members.Add(m);
        f.Name = NameFor(w, f);
        w.Factions.Add(f);
        if (f.Members.Contains(mine ?? "")) w.WorldFlags.Add("faction_formed");
        w.Log($"{f.Name} takes shape in the Senate around {w.House(leaderId)?.Name}: {string.Join(", ", f.Members.Where(m => m != leaderId).Select(m => w.House(m)?.Name ?? m))} sit with it.");
        return f;
    }

    /// <summary>What the chamber calls a bench: the rim's, the old blood's, the court's, the purse-strings', or the leader's.</summary>
    public static string NameFor(World w, Faction f)
    {
        var g = w.Galaxy; var leader = w.House(f.LeaderHouseId);
        int rim = g == null ? 0 : f.Members.Count(h => w.HoldingsOf(h).Any(x => x.IsPlanet && g.RingOf(x.SystemId) == Galaxy.Ring.Rim));
        double avgRank = f.Members.Count == 0 ? 0 : f.Members.Average(h => Progression.Title.RankOf(w.House(h)?.Title ?? ""));
        string[] candidates =
        {
            RegionSystem.CapitalHolder(w)?.Id == f.LeaderHouseId ? "The Court Party" : "",
            rim * 2 >= f.Members.Count ? "The Rim Benches" : "",
            avgRank >= 3 ? "The Old Blood" : "",
            f.Platform.GetValueOrDefault("levy") < 0 ? "The Purse-Strings" : "",
            f.Platform.GetValueOrDefault("peace") > 0 ? "The Peace Benches" : "",
            $"The {(leader?.Name ?? f.LeaderHouseId).Replace("House ", "").Replace("The ", "")} Bloc"
        };
        foreach (var c in candidates.Where(c => c.Length > 0)) if (!w.Factions.Any(x => x.Id != f.Id && x.Name == c)) return c;
        return $"The {(leader?.Name ?? f.LeaderHouseId).Replace("House ", "")} Benches";
    }

    /// <summary>The player's bench expected one thing and got another: affinity and the leader's regard both suffer.</summary>
    public static void BreakWhip(World w, string houseId, Faction f)
    {
        AddAffinity(w, houseId, f.LeaderHouseId, -BreakWhipAffinity);
        var leaderHead = w.House(f.LeaderHouseId) is { } lh ? Economy.HeadOf(w, lh) : null; var head = w.House(houseId) is { } hh ? Economy.HeadOf(w, hh) : null;
        if (leaderHead != null && head != null) w.Relationship(leaderHead.Id, head.Id).Disposition = Math.Clamp((w.GetRelationship(leaderHead.Id, head.Id)?.Disposition ?? 0) - BreakWhipDisposition, -100, 100);
        if (houseId == w.Protagonist?.HouseId) w.WorldFlags.Add("broke_the_whip");
    }
}
