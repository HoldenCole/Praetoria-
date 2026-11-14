using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Breaking away (docs/BREAKAWAY.md). A house that controls a region, by its own worlds and those of the houses there that
/// would follow it, may leave the Reach and found a state of its own: a republic, a dictatorship or a kingdom. What the
/// Reach does about it depends on its temper. One that has just fought a great war, or that faces a breakaway as strong as
/// itself, or one far out on the rim, lets it go. Otherwise there is a <b>war of independence</b>, and it has no fixed end:
/// the state is recognised when it has held its region long enough and the Reach has tired or been beaten, and it is brought
/// back when it has lost the region or the field. A house may declare with nothing but its own fleet and fight for as long
/// as it can. A state can also come out of the great war by partition, and out of a new throne by houses that will not kneel.
/// </summary>
public sealed class PolitySystem
{
    public const string Republic = "republic", Dictatorship = "dictatorship", Kingdom = "kingdom";
    public static readonly string[] Forms = { Republic, Dictatorship, Kingdom };
    public const int OwnShare = 25, ControlShare = 60, HoldShare = 50, LostShare = 25, WearyYears = 8, WinYears = 3, WinLead = 6, LoseLead = -8, TemperFallsEvery = 3, RefuseYears = 3;
    // true each year while it holds
    public const string IndependentFlag = "independent", WarFlag = "war_of_independence", PossibleFlag = "breakaway_possible", RefuseFlag = "may_refuse_the_throne";
    // true for a year after the deed
    public const string DeclaredFlag = "declared_independence", LetGoFlag = "let_go_by_the_reach", WonFlag = "independence_won", LostFlag = "independence_lost",
        OtherBreaksFlag = "a_state_breaks_away", PartitionFlag = "the_reach_is_partitioned";
    private static readonly string[] YearFlags = { IndependentFlag, WarFlag, PossibleFlag, RefuseFlag };
    private static readonly string[] DeedFlags = { DeclaredFlag, LetGoFlag, WonFlag, LostFlag, OtherBreaksFlag, PartitionFlag };
    public const string CivilWarEndedKey = "civil_war_ended";
    public static string OpposedKey(string houseId) => "opposed_the_throne:" + houseId;

    private readonly FleetSystem _fleets; private readonly OfficeCatalog _offices;
    public PolitySystem(FleetSystem fleets, OfficeCatalog offices) { _fleets = fleets; _offices = offices; }

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static bool Within(World w, string key, int years) => w.WorldCounters.TryGetValue(key, out int t) && w.Turn - t <= years;
    private static bool Real(House h) => h.Id != PirateSystem.HouseId;

    /// <summary>Is this house still of the Reach: in its Senate, under its laws, in its wars?</summary>
    public static bool InReach(World w, string houseId) => w.House(houseId) is { PolityId.Length: 0 };
    public static Polity? Of(World w, string houseId) => w.House(houseId) is { PolityId.Length: > 0 } h ? w.Polities.FirstOrDefault(p => p.Id == h.PolityId) : null;
    public static Polity? OfRegion(World w, string regionId) => w.Polities.FirstOrDefault(p => p.Regions.Contains(regionId));

    public static string FormName(string form) => form switch { Dictatorship => "a republic under one hand", Kingdom => "a kingdom", _ => "a republic" };
    public static string RulerStyle(string form, Character? ruler) => form switch { Dictatorship => "Dictator", Kingdom => ruler?.Sex == "female" ? "Queen" : "King", _ => "First Citizen" };
    public static string NameFor(string form, string regionName)
    {
        string r = regionName.StartsWith("The ", StringComparison.Ordinal) ? regionName[4..] : regionName;
        return form switch { Dictatorship => $"the Protectorate of {r}", Kingdom => $"the Kingdom of {r}", _ => $"the Free State of {r}" };
    }

    // ---------------- who holds a region ----------------

    private static List<Holding> Worlds(World w, string regionId) =>
        w.Galaxy == null ? new() : w.Holdings.Values.Where(h => h.IsPlanet && w.Galaxy.System(h.SystemId)?.RegionId == regionId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    /// <summary>Would this house of the region go with the one that leads it out? A client does; so does one whose head thinks well of
    /// the leader's, or that depends on the leader's house. The house that holds the capital never does.</summary>
    public static bool Joins(World w, House h, House leader)
    {
        if (h.Id == leader.Id || !Real(h) || !InReach(w, h.Id) || RegionSystem.CapitalHolder(w)?.Id == h.Id) return false;
        if (h.PatronId == leader.Id) return true;
        var a = Economy.HeadOf(w, h); var b = Economy.HeadOf(w, leader);
        if (a != null && b != null && (w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0) >= 25) return true;
        return OrderSystem.Sway(w, h, leader).lean >= 2;
    }

    /// <summary>The region's worlds: how many are the house's own, how many with those who would follow, of how many; and who would follow.</summary>
    public static (int own, int with, int total, List<House> backers) Control(World w, House house, string regionId)
    {
        var worlds = Worlds(w, regionId); int own = worlds.Count(h => h.OwnerId == house.Id);
        var backers = worlds.Select(h => h.OwnerId).Distinct().Select(id => w.House(id)).Where(h => h != null && Joins(w, h, house)).Select(h => h!).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        return (own, own + worlds.Count(h => backers.Any(b => b.Id == h.OwnerId)), worlds.Count, backers);
    }

    /// <summary>Regions the house holds a world in, the most held first.</summary>
    public static List<Region> RegionsOf(World w, House house) =>
        w.Galaxy == null ? new() : w.Galaxy.Regions.Where(r => Worlds(w, r.Id).Any(h => h.OwnerId == house.Id)).OrderByDescending(r => Worlds(w, r.Id).Count(h => h.OwnerId == house.Id)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();

    // ---------------- the Reach's temper ----------------

    public static bool Weary(World w) => CaesarSystem.GreatWar(w) || w.CivilWar != null || Within(w, CaesarSystem.GreatWarEndedKey, WearyYears) || Within(w, CivilWarEndedKey, WearyYears);

    /// <summary>How willing the Reach is to fight to keep this region: above nought it goes to war, at nought or below it lets it go.</summary>
    public (int temper, List<string> why) Temper(World w, House house, string regionId)
    {
        var why = new List<string>(); int t = 2; var (_, _, _, backers) = Control(w, house, regionId);
        if (Weary(w)) { t -= 2; why.Add("the Reach has just fought a great war and has no stomach for another"); }
        var gone = backers.Select(b => b.Id).Append(house.Id).ToHashSet(); var chair = RegionSystem.CapitalHolder(w);
        int ours = gone.Sum(id => FleetSystem.StrengthOf(w, id));
        int theirs = (chair != null && !gone.Contains(chair.Id) ? FleetSystem.StrengthOf(w, chair.Id) : 0)
            + w.Houses.Values.Where(h => Real(h) && InReach(w, h.Id) && !gone.Contains(h.Id) && h.Id != chair?.Id).Select(h => FleetSystem.StrengthOf(w, h.Id)).OrderByDescending(x => x).Take(3).Sum();
        if (ours * 2 >= theirs) { t -= 1; why.Add("the breakaway's fleets are a match for what would be sent"); }
        else if (ours * 4 < theirs) { t += 1; why.Add("the breakaway's fleets are few"); }
        string ring = w.Galaxy?.Region(regionId)?.Ring ?? Ring.Middle;
        if (ring == Ring.Rim) { t -= 1; why.Add("the rim is a long way to send a fleet"); }
        else if (ring == Ring.Core) { t += 1; why.Add("the core is too near the capital to let go"); }
        if (w.IsImperium) { t += 1; why.Add("a throne does not let provinces go"); }
        return (t, why);
    }

    // ---------------- declaring ----------------

    public string? DeclareRefusal(World w, House? house, string regionId, string form)
    {
        if (house == null || !Real(house)) return "no such house";
        if (!InReach(w, house.Id)) return "the house has already left the Reach";
        if (!Forms.Contains(form)) return "name a form of government";
        var region = w.Galaxy?.Region(regionId); if (region == null) return "no such region";
        if (OfRegion(w, regionId) != null) return $"{region.Name} has already left the Reach";
        if (RegionSystem.CapitalHolder(w)?.Id == house.Id) return "the house holds the capital: it cannot leave what it sits at the head of";
        if (w.Galaxy!.System(w.Galaxy.Capital)?.RegionId == regionId) return "the capital's own region cannot leave the Reach";
        if (w.CivilWar?.HouseId == house.Id) return "the house is marching on the capital";
        var (own, with, total, _) = Control(w, house, regionId);
        if (total == 0 || own == 0) return $"the house holds no world of {region.Name}";
        if (own * 100 < OwnShare * total) return $"the house holds {own} of the {total} worlds of {region.Name}, and must hold a quarter";
        return with * 100 < ControlShare * total ? $"with the houses that would follow, {with} of the {total} worlds of {region.Name} would go: it takes three in five" : null;
    }

    private void Leave(World w, House h, Polity p)
    {
        h.PolityId = p.Id; if (!p.Members.Contains(h.Id)) p.Members.Add(h.Id); h.Bloc = "";
        foreach (var f in w.Factions) { f.Members.Remove(h.Id); if (f.LeaderHouseId == h.Id) f.LeaderHouseId = f.Members.OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? ""; }
        w.Factions.RemoveAll(f => f.Members.Count == 0);
        OfficeSystem.Vacate(w, _offices, h.Id); w.Magistrates.RemoveAll(m => m.HouseId == h.Id);
        w.Candidacies.RemoveAll(c => w.Char(c.CharacterId)?.HouseId == h.Id);
        foreach (var r in w.Recalls.Where(r => r.HouseId == h.Id).ToList()) { w.Recalls.Remove(r); w.Char(r.CharacterId)?.Flags.Remove(CaesarSystem.RecalledChar); }
        w.WorldFlags.Remove(ElectionSystem.StandFlag(h.Id));
    }

    /// <summary>The house leads the region out. The Reach lets it go, or goes to war.</summary>
    public Polity Declare(World w, House house, string regionId, string form, string origin = "declared")
    {
        var region = w.Galaxy!.Region(regionId)!; var (_, _, _, backers) = Control(w, house, regionId); var (temper, why) = Temper(w, house, regionId);
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w);
        int n = w.Counter("polities_founded") + 1; w.WorldCounters["polities_founded"] = n;
        var p = new Polity { Id = "polity_" + n, Name = NameFor(form, region.Name), Form = form, HeadHouseId = house.Id, Since = w.Turn, Temper = temper, Origin = origin };
        p.Regions.Add(regionId); w.Polities.Add(p);
        Leave(w, house, p); foreach (var b in backers) Leave(w, b, p);
        // a house that leaves the Reach leaves the Reach's wars: the great war and a general's march are no longer its business
        foreach (var id in p.Members.ToList()) foreach (var war in w.Wars.Where(x => x.Involves(id)).ToList()) _fleets.MakePeace(w, war.HouseA, war.HouseB, terms: false);
        if (w.CivilWar is { } cw) { foreach (var id in p.Members) { cw.With.Remove(id); cw.Against.Remove(id); cw.Declared.Remove(id); } }
        if (chair != null && Economy.HeadOf(w, chair) is { } ch && Economy.HeadOf(w, house) is { } hh) { var r = w.Relationship(ch.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition - 30, -100, 100); }
        string who = Economy.HeadOf(w, house)?.Name ?? house.Name;
        w.Log($"{house.Name} DECLARES {region.Name} INDEPENDENT of {(w.IsImperium ? "the throne" : "the Senate")}: {p.Name}, {FormName(form)}, with {who} as its {RulerStyle(form, Economy.HeadOf(w, house))}."
            + (backers.Count > 0 ? $" {string.Join(", ", backers.Select(b => b.Name))} go with it." : ""));
        if (house.Id == mine) Mark(w, DeclaredFlag); else Mark(w, OtherBreaksFlag);
        if (temper <= 0)
        {
            w.Log($"The Reach does not answer with a fleet: {(why.Count > 0 ? why[0] : "it has not the will")}.");
            Recognise(w, p, letGo: true);
        }
        else
        {
            p.WarSince = w.Turn;
            p.Foes = w.Houses.Values.Where(h => Real(h) && InReach(w, h.Id) && h.Id != chair?.Id && FleetSystem.StrengthOf(w, h.Id) > 0)
                .OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(3).Select(h => h.Id).ToList();
            if (chair != null && InReach(w, chair.Id)) p.Foes.Insert(0, chair.Id);
            foreach (var foe in p.Foes) _fleets.DeclareWar(w, foe, house.Id);
            for (int i = 0; i < backers.Count && p.Foes.Count > 0; i++) _fleets.DeclareWar(w, p.Foes[i % p.Foes.Count], backers[i].Id);
            w.Log($"The Reach will not have it. {string.Join(", ", p.Foes.Select(id => w.House(id)?.Name ?? id))} are sent to bring {region.Name} back.");
        }
        return p;
    }

    private void Peace(World w, Polity p)
    {
        foreach (var m in p.Members.ToList()) foreach (var f in p.Foes.ToList()) _fleets.MakePeace(w, m, f, terms: false);
    }

    /// <summary>The Reach accepts that the state is a state.</summary>
    public void Recognise(World w, Polity p, bool letGo = false)
    {
        var mine = w.Protagonist?.HouseId; Peace(w, p); p.Recognised = w.Turn; p.Foes.Clear();
        if (w.House(p.HeadHouseId) is { } head) head.Legitimacy = Math.Clamp(head.Legitimacy + 10, 0, 100);
        w.WorldCounters["polities_recognised"] = w.Counter("polities_recognised") + 1;
        if (!letGo) w.Log($"The Reach makes peace with {p.Name} and names it in the treaty. It is a state now, and will be on the maps.");
        if (p.Members.Contains(mine ?? "")) Mark(w, letGo ? LetGoFlag : WonFlag);
    }

    /// <summary>The state is brought back into the Reach, and those who led it out pay.</summary>
    public void Dissolve(World w, Polity p, string why)
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); Peace(w, p); w.Polities.Remove(p);
        w.Log($"{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} is no more: {why}. Its worlds are the Reach's again.");
        foreach (var id in p.Members)
        {
            var h = w.House(id); if (h == null) continue; h.PolityId = "";
            bool led = id == p.HeadHouseId; h.Legitimacy = Math.Clamp(h.Legitimacy - (led ? 15 : 6), 0, 100);
            if (led)
            {
                w.WorldCounters[CaesarSystem.EnemyKey(id)] = w.Turn + 10;
                if (h.Treasury.Credits > 0) { int fine = h.Treasury.Credits * 3 / 10; h.Treasury.Credits -= fine; if (chair != null) chair.Treasury.Credits += fine; }
            }
        }
        w.WorldCounters["polities_dissolved"] = w.Counter("polities_dissolved") + 1;
        if (p.Members.Contains(mine ?? "")) Mark(w, LostFlag);
    }

    /// <summary>Out of the great war: a side that cannot win but holds a region keeps it, and the peace says so.</summary>
    public Polity Partition(World w, House leader, string regionId, IEnumerable<House> side)
    {
        var region = w.Galaxy!.Region(regionId)!; int n = w.Counter("polities_founded") + 1; w.WorldCounters["polities_founded"] = n;
        var p = new Polity { Id = "polity_" + n, Name = NameFor(Republic, region.Name), Form = Republic, HeadHouseId = leader.Id, Since = w.Turn, Origin = "partition" };
        p.Regions.Add(regionId); w.Polities.Add(p);
        var worlds = Worlds(w, regionId);
        foreach (var h in side.Where(h => h.Id == leader.Id || worlds.Any(x => x.OwnerId == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal)) Leave(w, h, p);
        p.Recognised = w.Turn; w.WorldCounters["polities_recognised"] = w.Counter("polities_recognised") + 1; Mark(w, PartitionFlag);
        w.Log($"The peace partitions the Reach. {region.Name} goes its own way as {p.Name}, {FormName(p.Form)}, under {leader.Name}: {string.Join(", ", p.Members.Select(id => w.House(id)?.Name ?? id))}.");
        return p;
    }

    /// <summary>How a war of independence stands for the state: the lead in its battles.</summary>
    public static int Standing(World w, Polity p)
    {
        int lead = 0;
        foreach (var war in w.Wars) foreach (var m in p.Members) if (war.Involves(m) && p.Foes.Contains(war.Other(m))) lead += war.Lead(m);
        return lead;
    }
    /// <summary>How much of its regions the state's own houses still hold, in hundredths.</summary>
    public static int Share(World w, Polity p)
    {
        var worlds = p.Regions.SelectMany(r => Worlds(w, r)).ToList();
        return worlds.Count == 0 ? 0 : 100 * worlds.Count(h => p.Members.Contains(h.OwnerId)) / worlds.Count;
    }
    /// <summary>The Reach's will to go on with the war: what it was when the state broke away, less one for every three years.</summary>
    public static int Will(World w, Polity p) => p.Temper - (p.WarSince > 0 ? (w.Turn - p.WarSince) / TemperFallsEvery : 0);

    // ---------------- the year ----------------

    public void Tick(World w)
    {
        var me = w.Protagonist; var mine = me?.HouseId;
        foreach (var f in YearFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }

        foreach (var p in w.Polities.ToList())
        {
            p.Members.RemoveAll(id => w.House(id) == null);
            if (p.Members.Count == 0) { w.Polities.Remove(p); continue; }
            if (!p.Members.Contains(p.HeadHouseId))   // the ruling house is gone: the strongest that is left rules
                p.HeadHouseId = p.Members.OrderByDescending(id => FleetSystem.StrengthOf(w, id)).ThenBy(id => id, StringComparer.Ordinal).First();
            if (p.Recognised > 0) continue;
            int years = w.Turn - p.WarSince, share = Share(w, p), lead = Standing(w, p);
            if (share < LostShare) Dissolve(w, p, "its houses have lost the region");
            else if (years >= 2 && lead <= LoseLead) Dissolve(w, p, "it is beaten in the field");
            else if (years >= WinYears && share >= HoldShare && (lead >= WinLead || Will(w, p) <= 0))
            {
                w.Log(lead >= WinLead ? $"{p.Name} has beaten what the Reach sent against it." : $"After {years} years the Reach has no more will to bring {p.Name} back.");
                Recognise(w, p);
            }
        }

        // those who fought a general who is now on the throne need not kneel: one of them that holds a region may take it out
        if (w.IsImperium)
            foreach (var h in w.Houses.Values.Where(h => Real(h) && h.Id != mine && InReach(w, h.Id) && Within(w, OpposedKey(h.Id), RefuseYears)).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal))
            {
                var region = RegionsOf(w, h).FirstOrDefault(r => DeclareRefusal(w, h, r.Id, Republic) == null);
                if (region != null && w.Counter("refusal:" + h.Id) == 0) { w.WorldCounters["refusal:" + h.Id] = w.Turn; Declare(w, h, region.Id, Republic, origin: "refused"); break; }
            }

        if (me is not { Alive: true } || mine == null || w.House(mine) is not { } my) return;
        if (Of(w, mine) is { } own) w.WorldFlags.Add(own.Recognised > 0 ? IndependentFlag : WarFlag);
        else if (RegionsOf(w, my).Any(r => DeclareRefusal(w, my, r.Id, Republic) == null))
        {
            w.WorldFlags.Add(PossibleFlag);
            if (w.IsImperium && Within(w, OpposedKey(mine), RefuseYears)) w.WorldFlags.Add(RefuseFlag);
        }
    }
}
