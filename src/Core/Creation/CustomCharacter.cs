using System.Text.Json;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Creation;

/// <summary>A face, by choice. -1 leaves a feature to the dice of the person's id (js/portrait.js).</summary>
public sealed class FaceSpec
{
    public int Skin { get; set; } = -1;
    public int Hair { get; set; } = -1;
    public int Style { get; set; } = -1;
    /// <summary>-1 by the dice, 0 clean-shaven, 1 bearded.</summary>
    public int Beard { get; set; } = -1;
    public bool Scar { get; set; }
    /// <summary>A painted face chosen from the portrait library (docs/ART_DIRECTION.md, "Portraits"): its place among the faces of the
    /// person's sex; -1 leaves it to the dice. Where the client ships painted portraits this is the choice that shows.</summary>
    public int Portrait { get; set; } = -1;
    public bool IsDefault => Skin < 0 && Hair < 0 && Style < 0 && Beard < 0 && !Scar && Portrait < 0;
    public string Encode() => $"skin={Skin},hair={Hair},style={Style},beard={Beard},scar={(Scar ? 1 : 0)}" + (Portrait >= 0 ? $",face={Portrait}" : "");
}
public sealed class KinSpec
{
    public string Name { get; set; } = ""; public string Sex { get; set; } = "male"; public int Age { get; set; } = 8;
    /// <summary>For a spouse only: the house they come from and keep the name of, as a husband or wife from another house does in
    /// play. Empty for one who is of the head's own house.</summary>
    public string HouseId { get; set; } = "";
    /// <summary>For one of age (docs/CHARACTER.md, "The family, each of them"): the service they are in (empty: the head's), how far
    /// up it they have got, the nature they are known for (empty: by the dice) and a gift (empty: none).</summary>
    public string Track { get; set; } = "";
    public int Rank { get; set; }
    public string Nature { get; set; } = "";
    public string Aptitude { get; set; } = "";
}
/// <summary>A world a character of the player's own making might start on, as the creator shows it (docs/CHARACTER.md, "The creator's
/// map"): what it is, where, whose, what it brings a year, and why a standard start may not take it (null when it may).</summary>
public sealed record StartSite(string SystemId, string Name, double X, double Y, string Kind, string Region, string Ring, string HolderId, string HolderName,
    Resources Yield, IReadOnlyList<string> Deposits, int Belt, int Lanes, int Lawlessness, bool BesideANation, string? Refusal);
public sealed class VentureSpec { public string Kind { get; set; } = ""; public string System { get; set; } = ""; }

/// <summary>
/// A character of the player's own making (docs/CHARACTER.md): the person, their face, the house, what it holds, the family, and
/// where in the world they start. Plain data, so it saves to a small file and loads again. <see cref="CharacterWorkshop"/> prices it,
/// checks it against the mode's rules, and builds the world from it.
/// </summary>
public sealed class CustomCharacter
{
    public const string Standard = "standard", Free = "free";
    public int Version { get; set; } = 1;
    /// <summary>"standard": a points budget and sensible caps. "free": no budget; the run is marked as custom.</summary>
    public string Mode { get; set; } = Standard;
    public bool Imperium { get; set; }
    /// <summary>Begin at sixteen with the Academy year.</summary>
    public bool Academy { get; set; }

    public string Name { get; set; } = "";
    public string Sex { get; set; } = "male";
    public int Age { get; set; } = 24;
    public string Track { get; set; } = "military";
    public int Rank { get; set; } = 1;
    public string Ambition { get; set; } = "";
    public List<string> Nature { get; set; } = new();
    public List<string> Aptitude { get; set; } = new();
    public Dictionary<string, int> Skills { get; set; } = new();
    public FaceSpec Face { get; set; } = new();
    /// <summary>Where they come from (content/backgrounds): a delta on the person and the house, as it is on a scenario's start.
    /// Empty is no background at all.</summary>
    public string Background { get; set; } = "";

    public string HouseName { get; set; } = "";
    public string Accent { get; set; } = Presentation.Palette.Player;
    public string Culture { get; set; } = "reach";
    /// <summary>What the people of the house look like (content/ancestries); empty leaves it to the dice of the house.</summary>
    public string Ancestry { get; set; } = "";
    /// <summary>"old" or "new": whether anybody's grandfather sat with this name.</summary>
    public string Origin { get; set; } = "new";
    public string Title { get; set; } = "landless";
    public int Legitimacy { get; set; } = 10;
    public string Party { get; set; } = "";
    public Dictionary<string, int> Treasury { get; set; } = new() { ["credits"] = 20, ["materials"] = 6, ["manpower"] = 4, ["influence"] = 1 };
    /// <summary>A seat in the Senate by the census, for a house with no land or bench to seat it.</summary>
    public bool Seat { get; set; }
    public string PatronId { get; set; } = "";

    /// <summary>Systems whose world the house holds. A world another house holds is taken from it.</summary>
    public List<string> Worlds { get; set; } = new();
    public int Hulls { get; set; }
    public List<VentureSpec> Ventures { get; set; } = new();

    public KinSpec? Spouse { get; set; }
    public List<KinSpec> Children { get; set; } = new();
    public List<KinSpec> Siblings { get; set; } = new();
    /// <summary>A father and a mother still living, and of the house. Left null they are names in the chronicle and no more.</summary>
    public KinSpec? Father { get; set; }
    public KinSpec? Mother { get; set; }
    /// <summary>Every relative named, with what they are to the head.</summary>
    public IEnumerable<(KinSpec kin, string what)> Kin()
    {
        if (Spouse != null) yield return (Spouse, "the spouse"); if (Father != null) yield return (Father, "the father"); if (Mother != null) yield return (Mother, "the mother");
        foreach (var k in Children) yield return (k, "a child"); foreach (var k in Siblings) yield return (k, "a brother or sister");
    }

    public string FriendId { get; set; } = "";
    public string RivalId { get; set; } = "";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static CustomCharacter FromJson(string json) => JsonSerializer.Deserialize<CustomCharacter>(json, Json) ?? throw new ContentException("Not a character file.");
    public CustomCharacter Clone() => FromJson(ToJson());
}

/// <summary>What a mode allows: the caps on each dial. Standard also has a points budget.</summary>
public sealed record WorkshopLimits(int Budget, int MaxSkill, int MaxNature, int MaxAptitude, int MaxRank, int MaxTitleRank, int MaxLegitimacy, int MaxWorlds, int MaxHulls, int MaxVentures,
    int MaxChildren, int MaxSiblings, int MaxCredits, int MaxGoods, int MaxInfluence, bool AnyWorld)
{
    /// <summary>How far up a service a relative may have got on the first day.</summary>
    public int MaxKinRank { get; init; } = 2;
    public static WorkshopLimits Standard { get; } = new(60, 4, 2, 2, 3, 3, 60, 2, 8, 3, 3, 3, 150, 60, 10, false);
    public static WorkshopLimits Free { get; } = new(0, 10, 9, 11, 6, 5, 100, 12, 80, 12, 8, 6, 5000, 2000, 200, true) { MaxKinRank = 5 };
    public static WorkshopLimits For(string mode) => mode == CustomCharacter.Free ? Free : Standard;
}

/// <summary>
/// Prices, checks and builds a <see cref="CustomCharacter"/> (docs/CHARACTER.md).
/// <list type="bullet">
/// <item><see cref="Cost"/>: one number for how strong the start is, with the lines that make it up. Shown in both modes; in Standard
/// it may not pass the budget.</item>
/// <item><see cref="Problems"/>: everything the mode's rules object to. An empty list is a character that may begin.</item>
/// <item><see cref="Apply"/> then <see cref="Finish"/>: lay the character over a blank start (the standing houses of the Reach, and
/// one empty house for the player), before and after the turn controller exists.</item>
/// </list>
/// </summary>
public static class CharacterWorkshop
{
    public const string RepublicBase = "custom_republic", ImperiumBase = "custom_imperium", SelfId = "custom_self", HouseId = "custom";
    public const string CustomFlag = "custom_character", FreeFlag = "custom_free", PowerCounter = "custom_power";
    public static readonly string[] Skills = { "administration", "charisma", "diplomacy", "discipline", "economics", "engineering", "gunnery", "intrigue", "law", "leadership", "logistics", "oratory", "tactics" };
    public static readonly string[] Goods = { Resource.Credits, Resource.Materials, Resource.Manpower, Resource.Influence, Resource.Exotics, Resource.Helium3, Resource.RareEarths, Resource.Isotopes };
    public static readonly string[] Accents = Presentation.Palette.Choices.Take(10).ToArray();   // gold, then the house list (docs/VISUAL_SYSTEM.md 10)
    public const int MinAge = 16, MaxAge = 80, AcademyAge = 16;
    /// <summary>A spouse from a named house (docs/CHARACTER.md): what it costs in a standard start, and what the head of that house and
    /// the new head think of each other on the first day.</summary>
    public const int SpouseHouseCost = 1, SpouseHouseRegard = 25, SpouseHouseRegardBack = 20;
    /// <summary>The family, each of them (docs/CHARACTER.md): what a living parent, a relative's step in a service and a relative's
    /// gift cost in a standard start, the youngest a relative with a step may be, and how much older than the head a parent is at least.</summary>
    public const int ParentCost = 2, KinRankCost = 2, KinAptitudeCost = 2, KinRankAge = 20, ParentGap = 16, MaxParentAge = 95;

    /// <summary>What a background is worth in points, by the same price list as the dials it moves: a start that takes Old Blood has
    /// bought standing, and one that takes a low birth is paid back for it. Never less than -4.</summary>
    public static int BackgroundCost(BackgroundDef bg)
    {
        var t = bg.Treasury; int pts = (int)Math.Floor(bg.Legitimacy / 5.0) + bg.Aptitude.Count * 4 + bg.Skills.Values.Sum()
            + (int)Math.Floor(t.Credits / 8.0) + t.Influence * 2 + t.Materials / 8 + t.Manpower / 8 + t.Exotics * 2;
        return Math.Max(-4, pts);
    }

    /// <summary>The kind of world an unclaimed system becomes when a new house is given it.</summary>
    public static string SpecFor(ContentDatabase content, string planetClass)
    {
        string spec = planetClass switch { "forge" => "forge_world", "belt" => "mining_belt", "terran" or "ocean" => "agri_world", _ => "frontier_colony" };
        return content.Holdings.Spec(spec) == null ? content.Holdings.Specs.FirstOrDefault()?.Id ?? spec : spec;
    }

    /// <summary>Why this start may not take a world, or null: the same rules <see cref="Problems"/> holds a whole character to, for one world.</summary>
    public static string? WorldRefusal(CustomCharacter c, World baseWorld, string systemId)
    {
        var g = baseWorld.Galaxy; if (g?.System(systemId) is not { } sys) return "there is no such system";
        if (WorkshopLimits.For(c.Mode).AnyWorld) return null;
        if (g.RingOf(systemId) == Galaxy.Ring.Beyond) return "it lies in a nation's space, beyond the Reach: a standard start may not take it";
        if (g.Capital == systemId) return "the capital is not given away in a standard start";
        var holder = baseWorld.PlanetHolder(systemId);
        if (holder != null && holder != HouseId && baseWorld.HoldingsOf(holder).Count(h => h.IsPlanet) < 2) return $"it is its house's only world: a standard start takes an unclaimed world, or one from a house that holds another";
        return null;
    }

    /// <summary>Every system of the map as a place to start: what the creator's map and list show.</summary>
    public static IReadOnlyList<StartSite> Sites(CustomCharacter c, ContentDatabase content, World baseWorld)
    {
        var g = baseWorld.Galaxy; if (g == null) return Array.Empty<StartSite>();
        var list = new List<StartSite>();
        foreach (var sys in g.Systems.Values.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            var held = baseWorld.PlanetOf(sys.Id); var holder = held != null ? baseWorld.House(held.OwnerId) : null;
            var spec = content.Holdings.Spec(held?.Specialization ?? SpecFor(content, sys.PlanetClass));
            var yield = new Resources(); if (spec != null) foreach (var r in Resource.All) yield.Add(r, spec.BaseYield.Get(r) - spec.Upkeep.Get(r));
            var lanes = g.Lanes.Where(l => l.A == sys.Id || l.B == sys.Id).Select(l => l.A == sys.Id ? l.B : l.A).ToList();
            list.Add(new StartSite(sys.Id, sys.Name, sys.X, sys.Y, spec?.Name ?? sys.PlanetClass, g.Regions.FirstOrDefault(r => r.Id == sys.RegionId)?.Name ?? "", g.RingOf(sys.Id),
                holder?.Id ?? "", holder?.Name ?? "", yield, sys.Deposits.Where(d => d.Value > 0).OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => Resource.Label(d.Key)).ToList(), sys.Belt, lanes.Count,
                baseWorld.Regions.TryGetValue(sys.RegionId, out var rs) ? rs.Lawlessness : g.RingOf(sys.Id) == Galaxy.Ring.Beyond ? 0 : g.RulesFor(g.RingOf(sys.Id)).LawlessnessStart, g.RingOf(sys.Id) != Galaxy.Ring.Beyond && lanes.Any(n => g.RingOf(n) == Galaxy.Ring.Beyond), WorldRefusal(c, baseWorld, sys.Id)));
        }
        return list;
    }

    public static string BaseFor(CustomCharacter c) => c.Imperium ? ImperiumBase : RepublicBase;

    // ---------------------------------------------------------------- the price of a start

    /// <summary>How strong the start is, in points, and what it is made of, largest first.</summary>
    public static (int total, List<(string what, int points)> lines) Cost(CustomCharacter c, ContentDatabase content)
    {
        var lines = new List<(string, int)>(); void Add(string what, int pts) { if (pts != 0) lines.Add((what, pts)); }
        Add("skills", c.Skills.Values.Sum(v => Math.Max(0, v) * (Math.Max(0, v) + 1) / 2));
        Add("aptitudes", c.Aptitude.Count * 4);
        Add("rank in the service", Math.Max(0, c.Rank - 1) * 4);
        Add("the house's rank", content.Titles.RankOf(c.Title) * 6);
        Add("legitimacy", c.Legitimacy / 5);
        if (c.Origin == "old") Add("an old name", 3);
        if (c.Seat) Add("a seat by the census", 4);
        if (c.PatronId.Length > 0) Add("a patron", 3);
        int T(string r) => Math.Max(0, c.Treasury.GetValueOrDefault(r));
        Add("the treasury", T(Resource.Credits) / 8 + T(Resource.Influence) * 2 + T(Resource.Materials) / 8 + T(Resource.Manpower) / 8 + T(Resource.Exotics) * 2 + T(Resource.Helium3) / 2 + T(Resource.RareEarths) + T(Resource.Isotopes) * 2);
        Add("worlds", c.Worlds.Count * 10);
        Add("hulls", Math.Max(0, c.Hulls) * 2);
        Add("ventures", c.Ventures.Count * 4);
        Add("family", (c.Spouse != null ? 2 : 0) + c.Children.Sum(k => k.Age >= DynastySystem.AgeOfMajority ? 3 : 2) + c.Siblings.Count + ((c.Father != null ? 1 : 0) + (c.Mother != null ? 1 : 0)) * ParentCost);
        Add("what the family has made of itself", c.Kin().Sum(x => Math.Max(0, x.kin.Rank) * KinRankCost + (x.kin.Aptitude.Length > 0 ? KinAptitudeCost : 0)));
        if (content.Backgrounds.ById(c.Background) is { } bg) Add("a background: " + bg.Name, BackgroundCost(bg));
        if (c.Spouse is { HouseId.Length: > 0 }) Add("a marriage into another house", SpouseHouseCost);
        if (c.FriendId.Length > 0) Add("a friend among the great", 2);
        if (c.RivalId.Length > 0) Add("a rival among the great", -2);
        lines.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return (lines.Sum(x => x.Item2), lines);
    }

    public static string Rating(int points) => points < 30 ? "modest" : points <= 60 ? "able" : points <= 110 ? "formidable" : points <= 220 ? "overpowered" : "absurd";

    // ---------------------------------------------------------------- the rules of a mode

    /// <summary>Everything the mode objects to. Empty means the character may begin.</summary>
    public static List<string> Problems(CustomCharacter c, ContentDatabase content, World? baseWorld = null)
    {
        var no = new List<string>(); var lim = WorkshopLimits.For(c.Mode); var traits = content.Traits;
        if (c.Mode != CustomCharacter.Standard && c.Mode != CustomCharacter.Free) no.Add("the mode must be standard or free");
        if (c.Sex != "male" && c.Sex != "female") no.Add("sex must be male or female");
        if (!c.Academy && (c.Age < MinAge || c.Age > MaxAge)) no.Add($"age must be between {MinAge} and {MaxAge}");
        var track = content.Careers.ById(c.Track); if (!content.Careers.IsEmpty && track == null) no.Add($"no such service as '{c.Track}'");
        int maxRank = Math.Min(lim.MaxRank, track?.MaxRank ?? lim.MaxRank);
        if (c.Rank < 0 || c.Rank > maxRank) no.Add($"rank in the service may be 0 to {maxRank} in this mode");
        if (c.Ambition.Length > 0 && !traits.IsEmpty && !traits.IsAmbition(c.Ambition)) no.Add($"no such ambition as '{c.Ambition}'");
        foreach (var t in c.Nature) if (!traits.IsEmpty && !traits.IsNature(t)) no.Add($"no such nature as '{t}'");
        foreach (var t in c.Aptitude) if (!traits.IsEmpty && !traits.IsAptitude(t)) no.Add($"no such aptitude as '{t}'");
        if (c.Nature.Distinct().Count() != c.Nature.Count || c.Aptitude.Distinct().Count() != c.Aptitude.Count) no.Add("a trait is listed twice");
        if (c.Nature.Count > lim.MaxNature) no.Add($"at most {lim.MaxNature} natures in this mode");
        if (c.Aptitude.Count > lim.MaxAptitude) no.Add($"at most {lim.MaxAptitude} aptitudes in this mode");
        foreach (var (k, v) in c.Skills) { if (!Skills.Contains(k)) no.Add($"no such skill as '{k}'"); else if (v < 0 || v > lim.MaxSkill) no.Add($"{k} may be 0 to {lim.MaxSkill} in this mode"); }
        var title = content.Titles.ById(c.Title);
        if (title == null && !content.Titles.IsEmpty) no.Add($"no such rank as '{c.Title}'");
        else if (content.Titles.RankOf(c.Title) > lim.MaxTitleRank) no.Add(c.Mode == CustomCharacter.Free ? "the throne itself is not for the taking at the start" : $"the house's rank may be at most {content.Titles.ByRank.FirstOrDefault(t => t.Rank == lim.MaxTitleRank)?.NameIn(c.Imperium ? World.EraImperium : World.EraRepublic) ?? "the third"} in this mode");
        if (c.Legitimacy < 0 || c.Legitimacy > lim.MaxLegitimacy) no.Add($"legitimacy may be 0 to {lim.MaxLegitimacy} in this mode");
        if (c.Origin != "old" && c.Origin != "new") no.Add("the name is old or new");
        if (c.Culture.Length > 0 && content.Policies.Cultures.Count > 0 && !content.Policies.Cultures.ContainsKey(c.Culture)) no.Add($"no such culture as '{c.Culture}'");
        if (c.Party.Length > 0 && !content.Parties.IsEmpty && content.Parties.Defs.All(p => p.Id != c.Party)) no.Add($"no such party as '{c.Party}'");
        if (c.Imperium && (c.Seat || c.PatronId.Length > 0)) no.Add("a seat by the census and a patron belong to the Republic");
        foreach (var (k, v) in c.Treasury)
        {
            if (!Goods.Contains(k)) { no.Add($"no such good as '{k}'"); continue; }
            int cap = k == Resource.Credits ? lim.MaxCredits : k == Resource.Influence ? lim.MaxInfluence : lim.MaxGoods;
            if (v < 0 || v > cap) no.Add($"{Resource.Label(k)} may be 0 to {cap} in this mode");
        }
        if (c.Worlds.Count > lim.MaxWorlds) no.Add($"at most {Counted.Worlds(lim.MaxWorlds)} in this mode");
        if (c.Worlds.Distinct().Count() != c.Worlds.Count) no.Add("a world is listed twice");
        if (c.Hulls < 0 || c.Hulls > lim.MaxHulls) no.Add($"hulls may be 0 to {lim.MaxHulls} in this mode");
        if (c.Hulls > 0 && c.Worlds.Count == 0 && c.Ventures.Count == 0) no.Add("a fleet needs a world or a venture to berth at");
        if (c.Ventures.Count > lim.MaxVentures) no.Add($"at most {Counted.Ventures(lim.MaxVentures)} in this mode");
        foreach (var v in c.Ventures) if (content.Businesses.Def(v.Kind) == null) no.Add($"no such venture as '{v.Kind}'");
        if (c.Ventures.GroupBy(v => v.Kind + "@" + v.System).Any(g => g.Count() > 1)) no.Add("one venture of a kind in a system");
        if (c.Children.Count > lim.MaxChildren) no.Add($"at most {lim.MaxChildren} children in this mode");
        if (c.Siblings.Count > lim.MaxSiblings) no.Add($"at most {lim.MaxSiblings} siblings in this mode");
        int age = c.Academy ? AcademyAge : c.Age;
        if (c.Academy && (c.Spouse != null || c.Children.Count > 0)) no.Add("a cadet of sixteen has no spouse or children");
        if (c.Spouse != null && (c.Spouse.Age < MinAge || c.Spouse.Age > MaxAge)) no.Add($"a spouse is between {MinAge} and {MaxAge}");
        foreach (var k in c.Children) if (k.Age < 0 || k.Age > age - MinAge) no.Add($"a child of {k.Age} is too old for a parent of {age}");
        foreach (var k in c.Siblings) if (k.Age < 0 || k.Age > MaxAge) no.Add("a sibling's age is out of range");
        foreach (var k in c.Children.Concat(c.Siblings).Append(c.Spouse).Where(k => k != null)) if (k!.Sex != "male" && k.Sex != "female") no.Add("kin are male or female");
        if (c.FriendId.Length > 0 && c.FriendId == c.RivalId) no.Add("a house cannot be both friend and rival");
        if (c.Background.Length > 0 && !content.Backgrounds.IsEmpty && content.Backgrounds.ById(c.Background) == null) no.Add($"no such background as '{c.Background}'");
        foreach (var (k, what) in new[] { (c.Father, "father"), (c.Mother, "mother") }) if (k != null && (k.Age < age + ParentGap || k.Age > MaxParentAge)) no.Add($"a {what} of {k.Age} is not the age of a parent of one of {age}: {age + ParentGap} to {MaxParentAge}");
        foreach (var (k, what) in c.Kin())
        {
            if (k.Track.Length > 0 && !content.Careers.IsEmpty && content.Careers.ById(k.Track) == null) no.Add($"no such service as '{k.Track}' for {what}");
            int top = Math.Min(lim.MaxKinRank, content.Careers.ById(k.Track.Length > 0 ? k.Track : c.Track)?.MaxRank ?? lim.MaxKinRank);
            if (k.Rank < 0 || k.Rank > top) no.Add($"{what} may be 0 to {top} steps up a service in this mode");
            else if (k.Rank > 0 && k.Age < KinRankAge) no.Add($"{what} is {k.Age}: nobody has a step in a service before {KinRankAge}");
            if (k.Nature.Length > 0 && !traits.IsEmpty && !traits.IsNature(k.Nature)) no.Add($"no such nature as '{k.Nature}'");
            if (k.Aptitude.Length > 0 && !traits.IsEmpty && !traits.IsAptitude(k.Aptitude)) no.Add($"no such aptitude as '{k.Aptitude}'");
        }
        if (baseWorld != null)
        {
            var g = baseWorld.Galaxy;
            foreach (var s in c.Worlds)
            {
                if (g?.System(s) == null) { no.Add($"no such system as '{s}'"); continue; }
                var holder = baseWorld.PlanetHolder(s);
                if (!lim.AnyWorld && holder != null && holder != HouseId && baseWorld.HoldingsOf(holder).Count(h => h.IsPlanet) < 2) no.Add($"{g.System(s)!.Name} is its house's only world: a standard start takes only an unclaimed world, or one from a house that holds another");
                if (!lim.AnyWorld && g.Capital == s) no.Add("the capital is not given away in a standard start");
            }
            foreach (var v in c.Ventures) if (g?.System(v.System) == null) no.Add($"no such system as '{v.System}'");
            foreach (var id in new[] { c.PatronId, c.FriendId, c.RivalId, c.Spouse?.HouseId ?? "" }.Where(x => x.Length > 0)) if (baseWorld.House(id) == null || id == HouseId || World.Outsider(id)) no.Add($"no such house as '{id}'");
        }
        var (total, _) = Cost(c, content);
        if (c.Mode == CustomCharacter.Standard && total > lim.Budget) no.Add($"{total} points is over the standard budget of {lim.Budget}");
        return no;
    }

    // ---------------------------------------------------------------- building the world

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    private static string GivenName(ContentDatabase content, string culture, string sex, string salt, HashSet<string> taken)
    {
        if (content.Names.IsEmpty) return "Scion";
        var rng = new Rng.SplitMix64Rng(Hash(salt)); string pick = "Scion";
        for (int i = 0; i < 12; i++) { pick = content.Names.Pick(culture, sex, rng); if (taken.Add(pick)) break; }
        return pick;
    }

    /// <summary>Lay the character over a blank start. Call on a world built from <see cref="BaseFor"/>, before the turn controller.
    /// Throws <see cref="ContentException"/> if the mode's rules object.</summary>
    public static string Apply(World w, ContentDatabase content, CustomCharacter c)
    {
        var p = w.Protagonist ?? throw new ContentException("The world has no protagonist to shape.");
        var house = w.House(p.HouseId) ?? throw new ContentException("The protagonist has no house.");
        var problems = Problems(c, content, w); if (problems.Count > 0) throw new ContentException("The character cannot begin: " + string.Join("; ", problems) + ".");
        var g = w.Galaxy; var taken = new HashSet<string>();

        // ---- the house
        string given = c.Name.Trim().Length > 0 ? c.Name.Trim().Split(' ')[0] : GivenName(content, c.Culture, c.Sex, "self:" + w.RngState, taken);
        string surname = c.HouseName.Trim().Length > 0 ? c.HouseName.Trim().Replace("House ", "") : c.Name.Trim().Contains(' ') ? c.Name.Trim()[(c.Name.Trim().IndexOf(' ') + 1)..] : "Varro";
        house.Name = content.Titles.RankOf(c.Title) >= 1 ? "House " + surname : surname; taken.Add(surname);   // nobody of the house is given its surname for a first name
        house.AccentColor = c.Accent; house.Culture = c.Culture.Length > 0 ? c.Culture : house.Culture; if (content.Ancestries.Def(c.Ancestry) is { Beyond: false }) house.Ancestry = c.Ancestry; house.Origin = c.Origin; house.Title = c.Title; house.Legitimacy = c.Legitimacy;
        house.Enrolled = c.Seat && !c.Imperium; house.PatronId = c.Imperium ? "" : c.PatronId;
        house.Treasury = new Resources(); foreach (var (k, v) in c.Treasury) house.Treasury.Add(k, v);

        // ---- the person
        p.Name = c.Name.Trim().Contains(' ') ? c.Name.Trim() : $"{given} {surname}"; taken.Add(given);
        p.Sex = c.Sex; p.Age = c.Academy ? AcademyAge : c.Age; p.CareerTrack = c.Track; p.CareerRank = c.Academy ? 0 : c.Rank; p.Ambition = c.Ambition;
        p.NatureTraits = new HashSet<string>(c.Nature); p.AptitudeTraits = new HashSet<string>(c.Aptitude);
        p.Skills = c.Skills.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value); p.Stress = 0; p.Flags.Clear();
        if (!c.Face.IsDefault) w.Cosmetics["look:" + p.Id] = c.Face.Encode();
        if (content.Backgrounds.ById(c.Background) is { } bg)
        {
            // where they come from: the same delta a scenario's start takes (CharacterCreation), on the person and house as made
            house.Legitimacy = Math.Max(0, house.Legitimacy + bg.Legitimacy); house.Treasury.Add(bg.Treasury); house.Treasury.ClampNonCredit();
            foreach (var t in bg.Nature) p.NatureTraits.Add(t); foreach (var t in bg.Aptitude) p.AptitudeTraits.Add(t);
            foreach (var kv in bg.Skills) p.Skills[kv.Key] = Math.Max(0, p.Skill(kv.Key) + kv.Value);
            foreach (var f in bg.Flags) p.Flags.Add(f); p.Stress = Math.Clamp(p.Stress + bg.Stress, 0, 100); p.Flags.Add("background:" + bg.Id);
        }

        // ---- what it holds: every world named is the house's from the first day
        foreach (var old in w.Holdings.Values.Where(h => h.OwnerId == house.Id).ToList()) w.Holdings.Remove(old.Id); w.HoldingsChanged();
        foreach (var old in w.Fleets.Values.Where(f => f.OwnerId == house.Id).ToList()) w.Fleets.Remove(old.Id);
        foreach (var sysId in c.Worlds)
        {
            var sys = g!.System(sysId)!; var held = w.Holdings.Values.Where(h => h.IsPlanet && h.SystemId == sysId).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (held != null)
            {
                string was = held.OwnerId; held.OwnerId = house.Id; held.GovernorId = p.Id; held.FormerOwnerId = ""; held.Rebellion = 0;
                // its old holder's ships go home, or are paid off if there is no home left
                var home = w.Holdings.Values.Where(h => h.OwnerId == was && h.SystemId.Length > 0).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == was && f.SystemId == sysId && !f.Office).ToList()) { if (home != null) { f.SystemId = home.SystemId; f.HomeSystemId = home.SystemId; } else w.Fleets.Remove(f.Id); }
                foreach (var st in w.Holdings.Values.Where(h => !h.IsPlanet && h.SystemId == sysId && h.OwnerId == was)) { st.OwnerId = house.Id; st.GovernorId = p.Id; }
            }
            else
            {
                string spec = SpecFor(content, sys.PlanetClass);
                w.Holdings["custom_" + sysId] = new Holding { Id = "custom_" + sysId, OwnerId = house.Id, Name = sys.Name, Specialization = spec, SystemId = sysId, Population = 10, GovernorId = p.Id, Culture = g.CultureOf(sysId) }; w.HoldingsChanged();
            }
        }
        string? berth = c.Worlds.FirstOrDefault() ?? c.Ventures.Select(v => v.System).FirstOrDefault();
        if (c.Hulls > 0 && berth != null)
        {
            var ships = new Dictionary<string, int>(); int left = c.Hulls;
            // a fleet of any size has a spine: one cruiser to every four hulls, frigates for the rest
            int cruisers = content.ShipClasses.Defs.Any(d => d.Id == "cruiser") ? left / 4 : 0; if (cruisers > 0) { ships["cruiser"] = cruisers; left -= cruisers; }
            string lineClass = content.ShipClasses.Defs.Any(d => d.Id == "frigate") ? "frigate" : content.ShipClasses.Defs.FirstOrDefault()?.Id ?? "frigate"; if (left > 0) ships[lineClass] = left;
            var fleet = new Fleet { Id = "fleet_custom", Name = $"The {surname} Squadron", OwnerId = house.Id, SystemId = berth, HomeSystemId = berth, Ships = ships, AdmiralId = c.Track == "military" ? p.Id : "" };
            fleet.Recount(); w.Fleets[fleet.Id] = fleet;
        }
        foreach (var b in w.Businesses.Values.Where(b => b.OwnerId == house.Id).ToList()) w.Businesses.Remove(b.Id);
        foreach (var v in c.Ventures) FoundBusinessCommand.Open(w, house, content.Businesses.Def(v.Kind)!, g!.System(v.System)!);

        // ---- the family
        Character Kin(string id, KinSpec spec, string sex, string salt)
        {
            string name = spec.Name; int age = spec.Age;
            var k = new Character { Id = id, HouseId = house.Id, Sex = sex, Age = age, Name = (name.Trim().Length > 0 ? name.Trim().Split(' ')[0] : GivenName(content, house.Culture, sex, salt + w.RngState, taken)) + " " + surname };
            taken.Add(k.Name.Split(' ')[0]);
            if (age >= DynastySystem.AgeOfMajority)
            {
                // each of them is somebody: a service of their own, a step in it, what they are like and what they are good at
                k.CareerTrack = spec.Track.Length > 0 ? spec.Track : c.Track; k.CareerRank = age >= KinRankAge ? Math.Max(0, spec.Rank) : 0;
                k.NatureTraits.Add(spec.Nature.Length > 0 ? spec.Nature : content.Traits.Nature.Count > 0 ? content.Traits.Nature[(int)(Hash(id) % (uint)content.Traits.Nature.Count)].Id : "Loyal");
                if (spec.Aptitude.Length > 0) k.AptitudeTraits.Add(spec.Aptitude);
            }
            w.Characters[id] = k; if (!house.Members.Contains(id)) house.Members.Add(id); w.Pools[id] = ActionPools.ForNpc();
            return k;
        }
        void Bond(string a, string b, BondType bond, int warmth, int strength)
        {
            foreach (var (x, y) in new[] { (a, b), (b, a) }) { var rel = w.Relationship(x, y); rel.Bond = bond; rel.BondStrength = strength; rel.Disposition = warmth; }
        }
        foreach (var id in w.Characters.Keys.Where(id => id.StartsWith("custom_") && id != p.Id).ToList()) { w.Characters.Remove(id); foreach (var any in w.Houses.Values) any.Members.Remove(id); w.Pools.Remove(id); }
        Character? spouse = null; string spouseHouse = "";
        if (c.Spouse != null && !c.Academy)
        {
            spouse = Kin("custom_spouse", c.Spouse, c.Sex == "male" ? "female" : "male", "spouse:");
            Bond(p.Id, spouse.Id, BondType.Marriage, 55, 65); p.Flags.Add("wed"); spouse.Flags.Add("wed");
            // a husband or wife from another house keeps that house's name and is of it, as one married in play is (docs/CHARACTER.md)
            if (c.Spouse.HouseId.Length > 0 && w.House(c.Spouse.HouseId) is { } theirs && theirs.Id != house.Id && !World.Outsider(theirs.Id))
            {
                string theirGiven = c.Spouse.Name.Trim().Length > 0 ? c.Spouse.Name.Trim().Split(' ')[0] : GivenName(content, theirs.Culture, spouse.Sex, "spouse-of:" + theirs.Id + w.RngState, taken);
                taken.Add(theirGiven);
                spouse.Name = theirGiven + " " + (theirs.Name.StartsWith("House ") ? theirs.Name[6..] : theirs.Name);
                house.Members.Remove(spouse.Id); spouse.HouseId = theirs.Id; if (!theirs.Members.Contains(spouse.Id)) theirs.Members.Add(spouse.Id);
                spouseHouse = theirs.Id;
            }
        }
        var father = c.Sex == "male" ? p : spouse; var mother = c.Sex == "male" ? spouse : p;
        for (int i = 0; i < c.Children.Count && !c.Academy; i++)
        {
            var k = Kin($"custom_child_{i + 1}", c.Children[i], c.Children[i].Sex, $"child{i}:");
            k.FatherId = father?.Id ?? ""; k.MotherId = mother?.Id ?? ""; Bond(p.Id, k.Id, BondType.Blood, 60, 70); if (spouse != null) Bond(spouse.Id, k.Id, BondType.Blood, 60, 70);
        }
        if (c.Siblings.Count > 0 || c.Father != null || c.Mother != null)
        {
            // a father and a mother: living and of the house where the player made them so, otherwise in the chronicle and not in
            // the world. Brothers and sisters share them.
            foreach (var (id, sex, living) in new[] { ("custom_father", "male", c.Father), ("custom_mother", "female", c.Mother) })
            {
                if (living != null) { var parent = Kin(id, living, sex, id + ":"); Bond(p.Id, parent.Id, BondType.Blood, 60, 70); continue; }
                var dead = new Character { Id = id, HouseId = house.Id, Sex = sex, Age = p.Age + 28, Alive = false, Name = GivenName(content, house.Culture, sex, id + w.RngState, taken) + " " + surname };
                w.Characters[id] = dead;
            }
            if (c.Father != null && c.Mother != null) { Bond("custom_father", "custom_mother", BondType.Marriage, 50, 60); w.Char("custom_father")!.Flags.Add("wed"); w.Char("custom_mother")!.Flags.Add("wed"); }
            p.FatherId = "custom_father"; p.MotherId = "custom_mother";
            for (int i = 0; i < c.Siblings.Count; i++)
            {
                var k = Kin($"custom_sibling_{i + 1}", c.Siblings[i], c.Siblings[i].Sex, $"sibling{i}:");
                k.FatherId = "custom_father"; k.MotherId = "custom_mother"; Bond(p.Id, k.Id, BondType.Blood, 55, 60);
                foreach (var pid in new[] { "custom_father", "custom_mother" }) if (w.Char(pid) is { Alive: true }) Bond(pid, k.Id, BondType.Blood, 60, 70);
            }
        }

        // ---- the world's first opinion of them
        void Regard(string houseId, int theirs, int mine)
        {
            var other = w.House(houseId); var head = other != null ? Economy.HeadOf(w, other) : null; if (head == null) return;
            w.Relationship(head.Id, p.Id).Disposition = theirs; w.Relationship(p.Id, head.Id).Disposition = mine;
        }
        if (spouseHouse.Length > 0) Regard(spouseHouse, SpouseHouseRegard, SpouseHouseRegardBack);   // the marriage first: a friend or a rival named below says more
        if (c.FriendId.Length > 0) Regard(c.FriendId, 45, 35);
        if (c.RivalId.Length > 0) Regard(c.RivalId, -45, -35);
        if (house.PatronId.Length > 0) Regard(house.PatronId, 30, 20);
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == house.Id && (h.GovernorId.Length == 0 || w.Char(h.GovernorId)?.Alive != true))) h.GovernorId = p.Id;

        var (total, _) = Cost(c, content);
        w.WorldFlags.Add(CustomFlag); if (c.Mode == CustomCharacter.Free) w.WorldFlags.Add(FreeFlag); w.WorldCounters[PowerCounter] = total;
        if (c.Academy) AcademySystem.Enter(w);
        string line = $"{p.Name} of {house.Name} takes the stage: a character of the player's own making, {Rating(total)} at {total} points{(c.Mode == CustomCharacter.Free ? ", made without limits" : "")}.";
        w.Log(line);
        return line;
    }

    /// <summary>What must wait for the turn controller: the party's bench, and the first reckoning of the orders with the house as made.</summary>
    public static void Finish(TurnController tc, CustomCharacter c)
    {
        var w = tc.World; var house = w.Protagonist != null ? w.House(w.Protagonist.HouseId) : null; if (house == null) return;
        if (c.Party.Length > 0 && w.Factions.FirstOrDefault(f => f.PartyId == c.Party) is { } bench && !bench.Members.Contains(house.Id))
        {
            foreach (var f in w.Factions) { f.Members.Remove(house.Id); if (f.LeaderHouseId == house.Id) f.LeaderHouseId = f.Members.OrderByDescending(m => tc.Senate.Weight(w, m)).ThenBy(m => m, StringComparer.Ordinal).FirstOrDefault() ?? ""; }
            bench.Members.Add(house.Id);
        }
        if (!w.InAcademy) tc.Orders.Tick(w);
    }

    // ---------------------------------------------------------------- starting points

    /// <summary>Ready-made characters that fill every dial: three that fit the standard budget, and one that does not try to.</summary>
    public static List<(string name, string blurb, CustomCharacter spec)> Presets(ContentDatabase content)
    {
        string Amb(string id) => content.Traits.IsAmbition(id) ? id : content.Traits.Ambitions.FirstOrDefault()?.Id ?? "";
        string Nat(string id) => content.Traits.IsNature(id) ? id : content.Traits.Nature.FirstOrDefault()?.Id ?? id;
        string Apt(string id) => content.Traits.IsAptitude(id) ? id : content.Traits.Aptitude.FirstOrDefault()?.Id ?? id;
        return new()
        {
            ("The Heir", "Born to a name: a rank, a little standing, a sister, and everything still to prove.", new CustomCharacter
            {
                Name = "Lucan Varro", Sex = "male", Age = 22, Track = "law", Rank = 1, Ambition = Amb("master_the_senate"), Nature = { Nat("Proud"), Nat("Just") }, Aptitude = { Apt("Orator") },
                Skills = { ["law"] = 2, ["oratory"] = 2, ["charisma"] = 2 }, HouseName = "Varro", Accent = "#3A86FF", Origin = "old", Title = "knight", Legitimacy = 30, Party = "optimates",
                Treasury = { ["credits"] = 30, ["materials"] = 8, ["manpower"] = 6, ["influence"] = 3 }, Siblings = { new KinSpec { Sex = "female", Age = 19 } }
            }),
            ("The Officer", "No name and no land: a commission, a head for battle, and four hulls that answer to you.", new CustomCharacter
            {
                Name = "Kessa Brand", Sex = "female", Age = 26, Track = "military", Rank = 2, Ambition = Amb("earn_a_command"), Nature = { Nat("Ambitious"), Nat("Just") }, Aptitude = { Apt("Tactician") },
                Skills = { ["tactics"] = 3, ["discipline"] = 2, ["leadership"] = 2, ["gunnery"] = 1 }, HouseName = "Brand", Accent = "#FFBE0B", Origin = "new", Title = "landless", Legitimacy = 8, Party = "sword",
                Treasury = { ["credits"] = 12, ["materials"] = 4, ["manpower"] = 6, ["influence"] = 0, ["helium3"] = 6 }
            }),
            ("The New Man", "A ledger, a trading company and a patron. No seat in the Senate, yet.", new CustomCharacter
            {
                Name = "Marcus Crane", Sex = "male", Age = 24, Track = "stewardship", Rank = 1, Ambition = Amb("amass_a_fortune"), Nature = { Nat("Ambitious"), Nat("Ruthless") }, Aptitude = { Apt("Administrator") },
                Skills = { ["administration"] = 3, ["economics"] = 3, ["charisma"] = 1 }, HouseName = "Crane", Accent = "#2EC27E", Origin = "new", Title = "landless", Legitimacy = 5, Party = "factors", PatronId = "lucent",
                Treasury = { ["credits"] = 45, ["materials"] = 4, ["manpower"] = 0, ["influence"] = 1 }, Ventures = { new VentureSpec { Kind = "trading_company", System = "aurelia_prime" } }
            }),
            ("The Upstart", "Nothing but a name nobody knows: no land, no seat, no patron, two thousand credits, and a mother who kept another house's books. Everything is still to do.", new CustomCharacter
            {
                Name = "Tam Corvane", Sex = "male", Age = 22, Track = "stewardship", Rank = 0, Ambition = Amb("restore_house_fortunes"), Nature = { Nat("Ambitious") }, Background = content.Backgrounds.ById("clerks_child") != null ? "clerks_child" : "",
                Skills = { ["administration"] = 2, ["economics"] = 1 }, HouseName = "Corvane", Accent = "#8AC926", Origin = "new", Title = "landless", Legitimacy = 5,
                Treasury = { ["credits"] = 20, ["materials"] = 6, ["manpower"] = 4, ["influence"] = 1 },
                Mother = new KinSpec { Age = 49, Track = "stewardship", Rank = 1, Nature = Nat("Just") }, Siblings = { new KinSpec { Sex = "female", Age = 19 } }
            }),
            ("The Colossus", "Frankly overpowered: a great house, three worlds, a fleet, a fortune, and every gift. Made without limits.", new CustomCharacter
            {
                Mode = CustomCharacter.Free, Name = "Aurelia Magna", Sex = "female", Age = 30, Track = "military", Rank = 5, Ambition = Amb("seize_the_throne"),
                Nature = { Nat("Ambitious"), Nat("Just"), Nat("Honorable") }, Aptitude = { Apt("Strategist"), Apt("Brilliant"), Apt("Orator"), Apt("Administrator") },
                Skills = Skills.ToDictionary(s => s, _ => 6), HouseName = "Magna", Accent = "#9B5DE5", Origin = "old", Title = "duke", Legitimacy = 90, Party = "optimates",
                Treasury = { ["credits"] = 1200, ["materials"] = 300, ["manpower"] = 200, ["influence"] = 40, ["exotics"] = 10, ["helium3"] = 80, ["rare_earths"] = 30, ["isotopes"] = 20 },
                Worlds = { "sorrow", "the_gallows", "tessaly" }, Hulls = 24, Ventures = { new VentureSpec { Kind = "merchant_bank", System = "sorrow" }, new VentureSpec { Kind = "shipyard_company", System = "tessaly" } },
                Spouse = new KinSpec { Age = 31 }, Children = { new KinSpec { Sex = "female", Age = 8 }, new KinSpec { Sex = "male", Age = 5 } }, Siblings = { new KinSpec { Sex = "male", Age = 27 } }, FriendId = "aurelian"
            })
        };
    }

    /// <summary>A character by the dice, within the mode's rules: for a quick start, or to argue with.</summary>
    public static CustomCharacter Random(ContentDatabase content, ulong seed, string mode, World? baseWorld = null)
    {
        var rng = new Rng.SplitMix64Rng(seed); var lim = WorkshopLimits.For(mode); var t = content.Traits; bool free = mode == CustomCharacter.Free;
        T Pick<T>(IReadOnlyList<T> xs) => xs[rng.NextInt(0, xs.Count)];
        var c = new CustomCharacter { Mode = mode, Sex = rng.NextInt(0, 2) == 0 ? "male" : "female", Age = 18 + rng.NextInt(0, free ? 30 : 16), Accent = Pick(Accents), Origin = rng.NextInt(0, 3) == 0 ? "old" : "new" };
        c.Track = content.Careers.IsEmpty ? "military" : Pick(content.Careers.Defs).Id; c.Rank = rng.NextInt(0, free ? 5 : 3);
        c.Culture = content.Policies.Cultures.Count > 0 ? Pick(content.Policies.Cultures.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList()) : "reach";
        if (t.Ambitions.Count > 0) c.Ambition = Pick(t.Ambitions).Id;
        foreach (var n in t.Nature.OrderBy(_ => rng.NextInt(0, 1000)).Take(Math.Min(2, lim.MaxNature))) c.Nature.Add(n.Id);
        foreach (var a in t.Aptitude.OrderBy(_ => rng.NextInt(0, 1000)).Take(free ? 3 : 1)) c.Aptitude.Add(a.Id);
        foreach (var s in Skills.OrderBy(_ => rng.NextInt(0, 1000)).Take(free ? 8 : 4)) c.Skills[s] = 1 + rng.NextInt(0, free ? 6 : 2);
        int titleRank = rng.NextInt(0, free ? 5 : 2); c.Title = content.Titles.ByRank.FirstOrDefault(x => x.Rank == titleRank)?.Id ?? "landless"; c.Legitimacy = 5 + rng.NextInt(0, free ? 80 : 25);
        if (!content.Parties.IsEmpty) c.Party = Pick(content.Parties.Defs).Id;
        c.Treasury = new() { ["credits"] = 10 + rng.NextInt(0, free ? 800 : 40), ["materials"] = rng.NextInt(0, free ? 200 : 12), ["manpower"] = rng.NextInt(0, free ? 100 : 10), ["influence"] = rng.NextInt(0, free ? 20 : 3) };
        c.HouseName = Pick(new[] { "Varro", "Cato", "Drusus", "Marcell", "Sabine", "Corvin", "Lepid", "Tarquin", "Valen", "Octar" });
        if (free && baseWorld?.Galaxy != null)
        {
            var systems = baseWorld.Galaxy.Systems.Keys.Where(s => s != baseWorld.Galaxy.Capital).OrderBy(s => s, StringComparer.Ordinal).ToList();
            foreach (var s in systems.OrderBy(_ => rng.NextInt(0, 1000)).Take(1 + rng.NextInt(0, 3))) c.Worlds.Add(s);
            c.Hulls = rng.NextInt(0, 20);
        }
        if (rng.NextInt(0, 2) == 0) c.Siblings.Add(new KinSpec { Sex = rng.NextInt(0, 2) == 0 ? "male" : "female", Age = 16 + rng.NextInt(0, 12) });
        // trim to the budget in standard: shed the dearest luxuries first
        if (!free) for (int guard = 0; guard < 40 && Cost(c, content).total > lim.Budget; guard++)
        {
            if (c.Treasury["credits"] > 10) c.Treasury["credits"] -= 10; else if (c.Rank > 0) c.Rank--; else if (c.Legitimacy > 5) c.Legitimacy -= 5; else if (c.Skills.Count > 0) { var k = c.Skills.OrderByDescending(kv => kv.Value).First().Key; if (--c.Skills[k] <= 0) c.Skills.Remove(k); } else break;
        }
        return c;
    }
}
