using System.Runtime.CompilerServices;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Local law: a law for one world (docs/POLITICS.md, "Local law"; the owner: "more laws that can be done. Executive edits, laws to
/// vote on, local laws"). A law of scope "world" in laws.json falls on every world a house holds. These are kept on the world itself
/// (<see cref="Holding.LocalLaws"/>), set by the head of the house that holds it (<see cref="SetLocalLawCommand"/>), and read for that
/// holding only: the three yields in the world's line of the ledger, the yearly drifts of unrest, prosperity and population (Economy),
/// the lawlessness of the world's own region (RegionSystem), and whether another house may found a venture there. A world carries only
/// so many that are off their default (<see cref="Limit"/>). What the estates think of a local law weighs on the holder by the share of
/// its worlds that keep it (<see cref="Opinion"/>). A world that changes hands keeps its laws. The other houses decree too
/// (<see cref="Tick"/>). Counters: <c>local_law_cd:&lt;holding&gt;:&lt;law&gt;</c>, <c>local_laws_set</c>.
/// </summary>
public static class LocalLawSystem
{
    /// <summary>How many local laws off their default a world can carry; one more on the house's seat or a world of high development.</summary>
    public const int BaseLimit = 2, GreatLimit = 3, HighDevelopment = 6;
    /// <summary>The effect that shuts a world to other houses' ventures.</summary>
    public const string ClosedKey = "closed_to_ventures";
    /// <summary>The per-world effects a rule reads for the one holding (tests/LocalLawTests.cs holds the content to this list).</summary>
    public static readonly IReadOnlySet<string> Read = new HashSet<string> { "credits_per_world", "materials_per_world", "manpower_per_world", "unrest_drift", "prosperity_drift", "pop_growth", "lawlessness_drift", ClosedKey };

    // the laws of this world's game, for a command that is not handed the catalog (the founding of a venture)
    private static readonly ConditionalWeakTable<World, PolicyCatalog> ByWorld = new();
    public static void Use(World w, PolicyCatalog cat) => ByWorld.AddOrUpdate(w, cat);
    private static PolicyCatalog? Of(World w) => ByWorld.TryGetValue(w, out var c) ? c : null;

    public static string OptionId(PolicyCatalog cat, Holding h, string lawId)
    {
        var law = cat.Local.Law(lawId); if (law == null) return "";
        return h.LocalLaws.TryGetValue(lawId, out var o) && law.Option(o) != null ? o : law.DefaultOption.Id;
    }
    public static LocalLawOptionDef? Option(PolicyCatalog cat, Holding h, string lawId) => cat.Local.Law(lawId)?.Option(OptionId(cat, h, lawId));

    /// <summary>The local laws of a world that are off their default, in the content's order.</summary>
    public static IEnumerable<(LocalLawDef law, LocalLawOptionDef option)> InForce(PolicyCatalog cat, Holding h)
    {
        if (h.LocalLaws.Count == 0 || !h.IsPlanet) yield break;
        foreach (var law in cat.Local.Laws)
            if (h.LocalLaws.TryGetValue(law.Id, out var oid) && law.Option(oid) is { Default: false } o) yield return (law, o);
    }
    public static int Count(PolicyCatalog cat, Holding h) => InForce(cat, h).Count();

    /// <summary>What the local laws of one world add to a per-world effect, for that world alone.</summary>
    public static int Effect(PolicyCatalog cat, Holding h, string key)
    {
        if (h.LocalLaws.Count == 0 || !h.IsPlanet) return 0; int sum = 0;
        foreach (var (_, o) in InForce(cat, h)) sum += o.Effects.GetValueOrDefault(key);
        return sum;
    }
    /// <summary>The same, cause by cause, for the lists a player reads: each local law that moves the thing, named in words.</summary>
    public static IEnumerable<(string what, int by)> Causes(PolicyCatalog cat, Holding h, string key, int times = 1)
    {
        foreach (var (_, o) in InForce(cat, h)) if (o.Effects.GetValueOrDefault(key) is not 0 and var v) yield return ("local law: " + o.Short, v * times);
    }
    /// <summary>What the local laws of the worlds of a region add to one of the region's own drifts (lawlessness): a free port's smugglers, a closed world's quiet.</summary>
    public static int InRegion(PolicyCatalog cat, IEnumerable<Holding> worlds, string key) { int sum = 0; foreach (var h in worlds) sum += Effect(cat, h, key); return sum; }

    /// <summary>Is the world of this system shut to the ventures of every house but its holder's?</summary>
    public static bool Closed(World w, string systemId) => Of(w) is { } cat && w.PlanetOf(systemId) is { LocalLaws.Count: > 0 } p && Effect(cat, p, ClosedKey) > 0;

    /// <summary>What the local laws on a house's worlds say to an estate. A law on one world of five weighs a fifth as much as a law on
    /// all of them: the opinions are summed over the worlds and divided by the count of the house's worlds, to the nearest whole
    /// number, a half going away from nought. A house with one world feels its local laws in full.</summary>
    public static int Opinion(World w, PolicyCatalog cat, string houseId, string estateId)
    {
        if (cat.Local.IsEmpty) return 0; int worlds = 0, sum = 0;
        foreach (var h in w.HoldingsOf(houseId))
        {
            if (!h.IsPlanet) continue; worlds++;
            foreach (var (_, o) in InForce(cat, h)) sum += o.Opinions.GetValueOrDefault(estateId);
        }
        return Scaled(sum, worlds);
    }
    public static int Scaled(int sum, int worlds) => worlds <= 0 || sum == 0 ? 0 : (2 * sum + Math.Sign(sum) * worlds) / (2 * worlds);
    /// <summary>The local laws that move an estate, for the line the Laws window shows under it: "local law on 2 of 5 worlds −4".</summary>
    public static IEnumerable<string> Says(World w, PolicyCatalog cat, string houseId, string estateId)
    {
        if (cat.Local.IsEmpty) yield break;
        var planets = w.HoldingsOf(houseId).Where(h => h.IsPlanet).ToList(); int with = planets.Count(h => InForce(cat, h).Any(x => x.option.Opinions.GetValueOrDefault(estateId) != 0));
        int by = Opinion(w, cat, houseId, estateId);
        if (with > 0) yield return $"local law on {with} of {Counted.Worlds(planets.Count)} {(by > 0 ? "+" : "")}{by}";
    }

    /// <summary>How many local laws off their default this world can carry.</summary>
    public static int Limit(World w, Holding h) => IsSeat(w, h) || SystemWorks.DevelopmentLevel(w, h.SystemId) >= HighDevelopment ? GreatLimit : BaseLimit;
    public static bool IsSeat(World w, Holding h) => w.House(h.OwnerId) is { SeatGrade: > 0 } house && house.SeatHoldingId == h.Id;
    public static string LimitWhy(World w, Holding h) => IsSeat(w, h) ? "the house's seat stands on it" : SystemWorks.DevelopmentLevel(w, h.SystemId) >= HighDevelopment ? $"its development is {SystemWorks.DevelopmentLevel(w, h.SystemId)}" : $"one more on the house's seat, or at development {HighDevelopment}";

    public static string CooldownKey(string holdingId, string lawId) => $"local_law_cd:{holdingId}:{lawId}";
    public static int CooldownLeft(World w, string holdingId, string lawId) => Math.Max(0, w.Counter(CooldownKey(holdingId, lawId)) - w.Turn);

    /// <summary>Why this head may not set this law on this world to this option, in plain words; null when they may. The influence is
    /// asked for by the command.</summary>
    public static string? Refusal(World w, PolicyCatalog cat, Character? actor, string holdingId, string lawId, string optionId)
    {
        var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var h = w.Holding(holdingId); if (h == null) return "no such world";
        if (!h.IsPlanet) return $"a local law is made for a world, and {h.Name} is not one";
        if (h.OwnerId != house.Id) return $"{h.Name} is not held by {house.Name}";
        if (Economy.HeadOf(w, house)?.Id != actor.Id) return $"only the head of {house.Name} makes law for its worlds";
        var law = cat.Local.Law(lawId); if (law == null) return "no such local law";
        var option = law.Option(optionId); if (option == null) return "no such option";
        string now = OptionId(cat, h, lawId);
        if (now == optionId) return $"already the law on {h.Name}";
        if (CooldownLeft(w, h.Id, lawId) is > 0 and var left) return $"changed too lately on {h.Name}: {Counted.MoreYears(left)}";
        if (!option.Default && law.Option(now)!.Default && Count(cat, h) is var n && n >= Limit(w, h))
            return $"{h.Name} can carry {Counted.Of(Limit(w, h), "local", "law")} off the custom and has {n}: lift one first";
        return null;
    }

    /// <summary>Put the option in force on the world and settle it there for the law's cooldown. A default is not kept: the world is as it was.</summary>
    public static void Set(World w, PolicyCatalog cat, Holding h, string lawId, string optionId)
    {
        var law = cat.Local.Law(lawId); var option = law?.Option(optionId); if (law == null || option == null) return;
        if (option.Default) h.LocalLaws.Remove(lawId); else h.LocalLaws[lawId] = optionId;
        w.WorldCounters[CooldownKey(h.Id, lawId)] = w.Turn + law.Cooldown;
        w.WorldCounters["local_laws_set"] = w.Counter("local_laws_set") + 1;
    }

    /// <summary>What an option does, in words, from its own numbers: "costs 200 credits a year; unrest eases by 2 a year".</summary>
    public static string Does(LocalLawOptionDef o)
    {
        var parts = new List<string>();
        foreach (var (key, v) in o.Effects.OrderBy(kv => Order(kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            int n = Math.Abs(v); if (v == 0) continue;
            parts.Add(key switch
            {
                "credits_per_world" => v > 0 ? $"brings {Money.Credits(n)} more a year" : $"costs {Money.Credits(n)} a year",
                "materials_per_world" => v > 0 ? $"{n} more materials a year" : $"{n} fewer materials a year",
                "manpower_per_world" => v > 0 ? $"{n} more manpower a year" : $"{n} less manpower a year",
                "unrest_drift" => v < 0 ? $"unrest eases by {n} a year" : $"unrest rises by {n} a year",
                "prosperity_drift" => v > 0 ? $"prosperity settles {n * 2} higher" : $"prosperity settles {n * 2} lower",
                "pop_growth" => v > 0 ? $"its people grow by {n} more a year" : $"its people grow by {n} less a year",
                "lawlessness_drift" => v > 0 ? $"lawlessness in its region rises by {n} a year" : $"lawlessness in its region falls by {n} a year",
                ClosedKey => "no other house may found a venture here",
                _ => $"{(v > 0 ? "+" : "−")}{n} {key.Replace('_', ' ')}"
            });
        }
        return parts.Count == 0 ? "the custom: nothing is added and nothing taken" : string.Join("; ", parts);
    }
    private static int Order(string key) => key switch { "credits_per_world" => 0, "materials_per_world" => 1, "manpower_per_world" => 2, "unrest_drift" => 3, "prosperity_drift" => 4, "pop_growth" => 5, "lawlessness_drift" => 6, _ => 7 };
    /// <summary>What the estates make of an option, as it stands in the content: "Commons +10, Magistrates −5".</summary>
    public static string Minds(PolicyCatalog cat, LocalLawOptionDef o) => o.Opinions.Count == 0 ? "" :
        string.Join(", ", o.Opinions.OrderByDescending(kv => Math.Abs(kv.Value)).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{(cat.Estate(kv.Key)?.Name ?? kv.Key).Replace("The ", "")} {(kv.Value > 0 ? "+" : "−")}{Math.Abs(kv.Value)}"));
    /// <summary>A world's local laws off their default, as a list in words: "a garrison, the tithe remitted". Empty when it keeps the custom.</summary>
    public static string Summary(PolicyCatalog cat, Holding h) => string.Join(", ", InForce(cat, h).Select(x => x.option.Short));

    // ---- the other houses ----

    public const string TitheLaw = "local_tithe", GarrisonLaw = "garrison", SettlersLaw = "settlers", DoleLaw = "dole";
    /// <summary>What moves another house's head to decree: unrest at which a world is put under martial rule, at which a taken world is,
    /// at which a world is given the dole or a garrison; the unrest under which those are lifted again.</summary>
    public const int MartialUnrest = 70, TakenUnrest = 40, RestlessUnrest = 45, MartialLifted = 25, CalmUnrest = 10;
    /// <summary>The treasury (in the books' hundreds) a house wants in hand to pay a dole, under which it lays a surcharge, over which
    /// it lifts one, that it wants before it grants land, and under which it stops granting.</summary>
    public const int DoleTreasury = 8, PoorTreasury = 4, EasyTreasury = 12, GrantsTreasury = 15, GrantsStop = 5;
    public const int SurchargeUnrest = 15, SurchargeProsperity = 55, SurchargeLiftedUnrest = 40;

    /// <summary>The other houses make local law (docs/POLITICS.md, "Local law"). At the year's turn each head looks at the house's
    /// worlds in id order and decrees where there is a plain reason: martial rule on a world in revolt, near it, or taken and still
    /// restless; the dole (or, short of coin, a garrison) on a restless world; a surcharge on a rich quiet world when the treasury is
    /// low; settlers' grants on a thinly peopled world when it is full; and each is lifted when its reason has passed. One decree a
    /// house a year, and in a year when no world of its is in revolt only about one year in two (a hash of the house and the year).</summary>
    public static void Tick(World w, PolicyCatalog cat, HoldingCatalog holdings, CommandExecutor executor, CommandContext ctx)
    {
        if (cat.Local.IsEmpty || w.InAcademy) return; var mine = w.Protagonist?.HouseId;
        foreach (var house in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var worlds = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).OrderBy(h => h.Id, StringComparer.Ordinal).ToList(); if (worlds.Count == 0) continue;
            var head = Economy.HeadOf(w, house); if (head == null) continue;
            uint hash = 2166136261; foreach (var ch in house.Id + ":local:" + w.Turn) hash = (hash ^ ch) * 16777619;
            if (hash % 2 != 0 && !worlds.Any(h => h.InRebellion)) continue;
            foreach (var h in worlds)
            {
                var want = Wanted(w, cat, holdings, house, h); if (want == null) continue;
                if (executor.TryExecute(new SetLocalLawCommand(head.Id, h.Id, want.Value.law, want.Value.option, cat), ctx)) break;
            }
        }
    }

    /// <summary>The one change a house's head would make to this world's local law this year, or null: the plain reasons, in order.</summary>
    public static (string law, string option)? Wanted(World w, PolicyCatalog cat, HoldingCatalog holdings, House house, Holding h)
    {
        string Now(string law) => OptionId(cat, h, law); bool Has(string law, string option) => cat.Local.Law(law)?.Option(option) != null;
        (string, string)? To(string law, string option) => Has(law, option) && Now(law) != option && CooldownLeft(w, h.Id, law) == 0 ? (law, option) : null;
        int coin = house.Treasury.Credits; string garrison = Now(GarrisonLaw), dole = Now(DoleLaw), tithe = Now(TitheLaw), settlers = Now(SettlersLaw);
        bool taken = h.FormerOwnerId.Length > 0 && h.FormerOwnerId != house.Id && w.House(h.FormerOwnerId) != null;
        // a world in revolt or on the edge of it, or one taken from another house and not yet quiet: the soldiers rule it
        if (h.InRebellion || h.Unrest >= MartialUnrest || (taken && h.Unrest >= TakenUnrest)) { if (To(GarrisonLaw, "martial_rule") is { } m) return m; }
        // martial rule is lifted when the world is quiet again; a surcharge is taken off a world that has begun to stir
        else if (garrison == "martial_rule" && h.Unrest <= MartialLifted) { if (To(GarrisonLaw, "none") is { } m) return m; }
        if (tithe == "surcharge" && (h.Unrest >= SurchargeLiftedUnrest || coin >= EasyTreasury) && To(TitheLaw, "customary") is { } off) return off;
        // a restless world: bread where there is the coin for it, billeted soldiers where there is not
        if (h.Unrest >= RestlessUnrest && h.Population > 0 && garrison == "none" && dole == "none")
        {
            if (coin >= DoleTreasury && To(DoleLaw, "corn_dole") is { } d) return d;
            if (To(GarrisonLaw, "quartered") is { } q) return q;
        }
        if (dole != "none" && dole.Length > 0 && (h.Unrest <= CalmUnrest || coin < 0) && To(DoleLaw, "none") is { } nd) return nd;
        if (garrison == "quartered" && h.Unrest <= CalmUnrest && To(GarrisonLaw, "none") is { } ng) return ng;
        // the treasury is low: a rich and quiet world is asked for more
        if (coin < PoorTreasury && coin >= 0 && h.Unrest <= SurchargeUnrest && h.Prosperity >= SurchargeProsperity && h.Population > 0 && To(TitheLaw, "surcharge") is { } s) return s;
        // a thinly peopled world, and coin in hand: land for those who will come
        int room = holdings.Spec(h.Specialization)?.MaxPopulation ?? 0;
        if (settlers == "none" && room > 0 && h.Population > 0 && h.Population * 3 < room && coin >= GrantsTreasury && h.Unrest < RestlessUnrest && To(SettlersLaw, "land_grants") is { } g) return g;
        if (settlers == "land_grants" && (coin < GrantsStop || (room > 0 && h.Population * 3 >= room * 2)) && To(SettlersLaw, "none") is { } ns) return ns;
        return null;
    }
}
