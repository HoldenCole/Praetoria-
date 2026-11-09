using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Laws and estates (GDD §6.1 "legislation", §9 "resource policy"; the owner's ask: when you are head of a house, a world, a region, the
/// chair, an office or the throne, there are laws to set, and groups that like or dislike them). Every house holds an option for every
/// law (<see cref="World.Laws"/>; the default until chosen). A law's <b>scope</b> is the position it needs; its option's <b>effects</b> are
/// modifier keys that the economy, the regions, the fleet and the Senate read through <see cref="Modifier"/> (the house's own laws, plus
/// the estates' loyal or hostile effects for the player's house) and <see cref="ReachModifier"/> (laws marked reach-wide, held by the
/// chair, the Emperor or an office). The six <b>estates</b> hold an approval of the player's house that drifts toward what its laws say
/// (the sum of the options' opinions) plus what events cultivate (<c>estate_offset:&lt;id&gt;</c>, decaying); past +30 they are loyal,
/// under −30 hostile. Flags: <c>estate_loyal:&lt;id&gt;</c>, <c>estate_hostile:&lt;id&gt;</c>, <c>law:&lt;law&gt;:&lt;option&gt;</c>;
/// counters <c>estate:&lt;id&gt;</c>, <c>laws_enacted</c>.
/// </summary>
public static class PolicySystem
{
    public const int Loyal = 30, Hostile = -30, DriftStep = 3, OffsetDecay = 1;

    /// <summary>The counter that holds an estate's approval of a house: the player's are <c>estate:&lt;id&gt;</c> (content reads them), a rival's <c>estate:&lt;house&gt;:&lt;id&gt;</c>.</summary>
    public static string ApprovalKey(World w, string houseId, string estateId) => houseId == w.Protagonist?.HouseId ? "estate:" + estateId : $"estate:{houseId}:{estateId}";
    public static int Approval(World w, string houseId, string estateId) => w.Counter(ApprovalKey(w, houseId, estateId));

    /// <summary>Every house begins with its culture's laws (content/politics/cultures.json), the player's too; a law the culture does not name stays at its default.</summary>
    public static void ApplyCultures(World w, PolicyCatalog cat)
    {
        if (cat.IsEmpty) return;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId))
        {
            if (!cat.Cultures.TryGetValue(h.Culture, out var set)) continue;
            foreach (var (lawId, optionId) in set)
            {
                var law = cat.Law(lawId); if (law?.Option(optionId) == null) continue;
                if (!w.Laws.TryGetValue(h.Id, out var mine)) w.Laws[h.Id] = mine = new Dictionary<string, string>();
                if (!mine.ContainsKey(lawId)) { mine[lawId] = optionId; if (law.TaxPolicy) h.TaxPolicy = optionId; }
            }
        }
    }

    public static string OptionId(World w, PolicyCatalog cat, string houseId, string lawId)
    {
        var law = cat.Law(lawId); if (law == null) return "";
        return w.Laws.TryGetValue(houseId, out var set) && set.TryGetValue(lawId, out var o) && law.Option(o) != null ? o : law.DefaultOption.Id;
    }
    public static LawOptionDef? Option(World w, PolicyCatalog cat, string houseId, string lawId) => cat.Law(lawId)?.Option(OptionId(w, cat, houseId, lawId));

    /// <summary>The dominant house of a region: most worlds there, ties by id. Null for an empty region.</summary>
    public static string? Dominant(World w, string regionId)
    {
        var g = w.Galaxy; if (g == null) return null;
        return w.Holdings.Values.Where(h => h.IsPlanet && g.System(h.SystemId)?.RegionId == regionId).GroupBy(h => h.OwnerId)
            .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key).FirstOrDefault();
    }
    public static IEnumerable<string> RegionsDominated(World w, string houseId) => w.Galaxy?.Regions.Where(r => Dominant(w, r.Id) == houseId).Select(r => r.Id) ?? Enumerable.Empty<string>();

    /// <summary>Does the house hold the position a scope names?</summary>
    public static bool HoldsScope(World w, string houseId, string scope)
    {
        if (scope == "house") return true;
        if (scope == "world") return w.HoldingsOf(houseId).Any(h => h.IsPlanet);
        if (scope == "region") return RegionsDominated(w, houseId).Any();
        if (scope == "chair") return RegionSystem.CapitalHolder(w)?.Id == houseId;
        if (scope == "emperor") return w.House(houseId)?.Title == Progression.Title.Emperor;
        if (scope == "bench") return FactionSystem.Leads(w, houseId);
        if (scope.StartsWith("office:")) return OfficeSystem.Holds(w, houseId, scope["office:".Length..]);
        return false;
    }
    public static string ScopeName(string scope) => scope switch
    {
        "house" => "the head of a house", "world" => "the holder of a world", "region" => "the dominant house of a region", "chair" => "the chair",
        "emperor" => "the Emperor", "bench" => "the leader of a bench", _ when scope.StartsWith("office:") => "the " + scope["office:".Length..].Replace('_', ' '), _ => scope
    };

    /// <summary>A house's own modifier: its non-reach laws in force where it holds the scope, plus the estates' effects if it is the player's house.
    /// <paramref name="regionId"/> limits region-scope laws to a region the house dominates.</summary>
    public static int Modifier(World w, PolicyCatalog cat, string houseId, string key, string? regionId = null)
    {
        if (cat.IsEmpty) return 0; int sum = 0;
        foreach (var law in cat.Laws)
        {
            if (law.Reach || !HoldsScope(w, houseId, law.Scope)) continue;
            if (law.Scope == "region" && regionId != null && Dominant(w, regionId) != houseId) continue;
            if (law.Scope == "region" && regionId == null && key != "lawlessness_drift" && key != "allegiance_drift") { /* region laws fall on the region's worlds; callers pass the region */ }
            sum += Option(w, cat, houseId, law.Id)?.Effects.GetValueOrDefault(key) ?? 0;
        }
        if (houseId != PirateSystem.HouseId)   // the estates' regard, for every house
            foreach (var e in cat.Estates)
            {
                int a = Approval(w, houseId, e.Id);
                if (a >= Loyal) sum += e.Loyal.GetValueOrDefault(key); else if (a <= Hostile) sum += e.Hostile.GetValueOrDefault(key);
            }
        return sum;
    }

    /// <summary>Reach-wide laws in force: each held by whoever holds its scope.</summary>
    public static int ReachModifier(World w, PolicyCatalog cat, string key)
    {
        if (cat.IsEmpty) return 0; int sum = 0;
        foreach (var law in cat.Laws.Where(l => l.Reach))
        {
            string? holder = law.Scope == "chair" ? RegionSystem.CapitalHolder(w)?.Id : law.Scope == "emperor" ? ThroneSystem.Emperor(w)?.Id : law.Scope.StartsWith("office:") ? OfficeSystem.HolderHouse(w, law.Scope["office:".Length..]) : null;
            if (holder != null) sum += Option(w, cat, holder, law.Id)?.Effects.GetValueOrDefault(key) ?? 0;
        }
        return sum;
    }

    /// <summary>What the player's laws say about an estate: the sum of the options' opinions, where the house holds the scope.</summary>
    public static int LawOpinion(World w, PolicyCatalog cat, string houseId, string estateId) =>
        cat.Laws.Where(l => HoldsScope(w, houseId, l.Scope)).Sum(l => Option(w, cat, houseId, l.Id)?.Opinions.GetValueOrDefault(estateId) ?? 0);

    public static void Enact(World w, PolicyCatalog cat, string houseId, string lawId, string optionId)
    {
        if (!w.Laws.TryGetValue(houseId, out var set)) w.Laws[houseId] = set = new Dictionary<string, string>();
        set[lawId] = optionId;
        var law = cat.Law(lawId); var house = w.House(houseId);
        if (law is { TaxPolicy: true } && house != null) house.TaxPolicy = optionId;
        w.WorldCounters[$"law_cd:{houseId}:{lawId}"] = w.Turn + (law?.Cooldown ?? 6);
        w.WorldCounters["laws_enacted"] = w.Counter("laws_enacted") + 1;
    }
    public static bool OnCooldown(World w, string houseId, string lawId) => w.Counter($"law_cd:{houseId}:{lawId}") > w.Turn;

    /// <summary>Each turn: the estates drift toward what the laws say, offsets decay, legitimacy moves every fifth turn, flags are written.</summary>
    public static void Tick(World w, PolicyCatalog cat)
    {
        if (cat.IsEmpty) return;
        var mine = w.Protagonist?.HouseId; if (mine == null) return; var house = w.House(mine); if (house == null) return;
        // every house's estates drift toward what its laws say (rivals have no offsets: events speak only to the player)
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal))
            foreach (var e in cat.Estates)
            {
                string key = ApprovalKey(w, h.Id, e.Id); int a = w.Counter(key); int diff = Math.Clamp(LawOpinion(w, cat, h.Id, e.Id), -100, 100) - a;
                if (diff != 0) w.WorldCounters[key] = Math.Clamp(a + Math.Sign(diff) * Math.Min(DriftStep, Math.Abs(diff)), -100, 100);
            }
        w.WorldCounters["rivals_with_hostile_commons"] = w.Houses.Values.Count(h => h.Id != mine && h.Id != PirateSystem.HouseId && Approval(w, h.Id, "commons") <= Hostile);
        // keep the tithe in step with the law for every house that chose one
        foreach (var (hid, set) in w.Laws) foreach (var law in cat.Laws.Where(l => l.TaxPolicy)) if (set.TryGetValue(law.Id, out var o) && w.House(hid) is { } hh) hh.TaxPolicy = o;
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("estate_loyal:") || f.StartsWith("estate_hostile:") || f.StartsWith("law:")).ToList()) w.WorldFlags.Remove(f);
        foreach (var e in cat.Estates)
        {
            string offKey = "estate_offset:" + e.Id; int off = w.Counter(offKey);
            if (off != 0) w.WorldCounters[offKey] = off - Math.Sign(off) * Math.Min(OffsetDecay, Math.Abs(off));
            int target = Math.Clamp(LawOpinion(w, cat, mine, e.Id) + off, -100, 100);
            int a = w.Counter("estate:" + e.Id); int diff = target - a;
            a = Math.Clamp(a + Math.Sign(diff) * Math.Min(DriftStep, Math.Abs(diff)), -100, 100);
            w.WorldCounters["estate:" + e.Id] = a;
            if (a >= Loyal) w.WorldFlags.Add("estate_loyal:" + e.Id); else if (a <= Hostile) w.WorldFlags.Add("estate_hostile:" + e.Id);
        }
        foreach (var law in cat.Laws) w.WorldFlags.Add($"law:{law.Id}:{OptionId(w, cat, mine, law.Id)}");
        if (w.Turn > 0 && w.Turn % 5 == 0)
        {
            int legit = Modifier(w, cat, mine, "legitimacy_per_5") + ReachModifier(w, cat, "legitimacy_per_5");
            if (legit != 0) house.Legitimacy = Math.Clamp(house.Legitimacy + legit, 0, 100);
        }
    }
}
