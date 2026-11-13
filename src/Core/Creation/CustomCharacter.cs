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
    public bool IsDefault => Skin < 0 && Hair < 0 && Style < 0 && Beard < 0 && !Scar;
    public string Encode() => $"skin={Skin},hair={Hair},style={Style},beard={Beard},scar={(Scar ? 1 : 0)}";
}
public sealed class KinSpec { public string Name { get; set; } = ""; public string Sex { get; set; } = "male"; public int Age { get; set; } = 8; }
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

    public string HouseName { get; set; } = "";
    public string Accent { get; set; } = "#E63946";
    public string Culture { get; set; } = "reach";
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
    public static WorkshopLimits Standard { get; } = new(60, 4, 2, 2, 3, 3, 60, 2, 8, 3, 3, 3, 150, 60, 10, false);
    public static WorkshopLimits Free { get; } = new(0, 10, 9, 11, 6, 5, 100, 12, 80, 12, 8, 6, 5000, 2000, 200, true);
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
    public static readonly string[] Accents = { "#E63946", "#3A86FF", "#FFBE0B", "#2EC27E", "#C9D1DC", "#FB8500", "#9B5DE5", "#00B4D8", "#F15BB5", "#8AC926" };
    public const int MinAge = 16, MaxAge = 80, AcademyAge = 16;

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
        Add("family", (c.Spouse != null ? 2 : 0) + c.Children.Sum(k => k.Age >= DynastySystem.AgeOfMajority ? 3 : 2) + c.Siblings.Count);
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
        if (c.Worlds.Count > lim.MaxWorlds) no.Add($"at most {lim.MaxWorlds} world(s) in this mode");
        if (c.Worlds.Distinct().Count() != c.Worlds.Count) no.Add("a world is listed twice");
        if (c.Hulls < 0 || c.Hulls > lim.MaxHulls) no.Add($"hulls may be 0 to {lim.MaxHulls} in this mode");
        if (c.Hulls > 0 && c.Worlds.Count == 0 && c.Ventures.Count == 0) no.Add("a fleet needs a world or a venture to berth at");
        if (c.Ventures.Count > lim.MaxVentures) no.Add($"at most {lim.MaxVentures} venture(s) in this mode");
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
            foreach (var id in new[] { c.PatronId, c.FriendId, c.RivalId }.Where(x => x.Length > 0)) if (baseWorld.House(id) == null || id == HouseId || id == PirateSystem.HouseId) no.Add($"no such house as '{id}'");
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
        house.Name = content.Titles.RankOf(c.Title) >= 1 ? "House " + surname : surname;
        house.AccentColor = c.Accent; house.Culture = c.Culture.Length > 0 ? c.Culture : house.Culture; house.Origin = c.Origin; house.Title = c.Title; house.Legitimacy = c.Legitimacy;
        house.Enrolled = c.Seat && !c.Imperium; house.PatronId = c.Imperium ? "" : c.PatronId;
        house.Treasury = new Resources(); foreach (var (k, v) in c.Treasury) house.Treasury.Add(k, v);

        // ---- the person
        p.Name = c.Name.Trim().Contains(' ') ? c.Name.Trim() : $"{given} {surname}"; taken.Add(given);
        p.Sex = c.Sex; p.Age = c.Academy ? AcademyAge : c.Age; p.CareerTrack = c.Track; p.CareerRank = c.Academy ? 0 : c.Rank; p.Ambition = c.Ambition;
        p.NatureTraits = new HashSet<string>(c.Nature); p.AptitudeTraits = new HashSet<string>(c.Aptitude);
        p.Skills = c.Skills.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value); p.Stress = 0; p.Flags.Clear();
        if (!c.Face.IsDefault) w.Cosmetics["look:" + p.Id] = c.Face.Encode();

        // ---- what it holds: every world named is the house's from the first day
        foreach (var old in w.Holdings.Values.Where(h => h.OwnerId == house.Id).ToList()) w.Holdings.Remove(old.Id);
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
                string spec = sys.PlanetClass switch { "forge" => "forge_world", "belt" => "mining_belt", "terran" or "ocean" => "agri_world", _ => "frontier_colony" };
                if (content.Holdings.Spec(spec) == null) spec = content.Holdings.Specs.FirstOrDefault()?.Id ?? spec;
                w.Holdings["custom_" + sysId] = new Holding { Id = "custom_" + sysId, OwnerId = house.Id, Name = sys.Name, Specialization = spec, SystemId = sysId, Population = 10, GovernorId = p.Id, Culture = g.CultureOf(sysId) };
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
        Character Kin(string id, string name, string sex, int age, string salt)
        {
            var k = new Character { Id = id, HouseId = house.Id, Sex = sex, Age = age, Name = (name.Trim().Length > 0 ? name.Trim().Split(' ')[0] : GivenName(content, house.Culture, sex, salt + w.RngState, taken)) + " " + surname };
            taken.Add(k.Name.Split(' ')[0]);
            if (age >= DynastySystem.AgeOfMajority) { k.CareerTrack = c.Track; k.CareerRank = 0; k.NatureTraits.Add(content.Traits.Nature.Count > 0 ? content.Traits.Nature[(int)(Hash(id) % (uint)content.Traits.Nature.Count)].Id : "Loyal"); }
            w.Characters[id] = k; if (!house.Members.Contains(id)) house.Members.Add(id); w.Pools[id] = ActionPools.ForNpc();
            return k;
        }
        void Bond(string a, string b, BondType bond, int warmth, int strength)
        {
            foreach (var (x, y) in new[] { (a, b), (b, a) }) { var rel = w.Relationship(x, y); rel.Bond = bond; rel.BondStrength = strength; rel.Disposition = warmth; }
        }
        foreach (var id in w.Characters.Keys.Where(id => id.StartsWith("custom_") && id != p.Id).ToList()) { w.Characters.Remove(id); house.Members.Remove(id); w.Pools.Remove(id); }
        Character? spouse = null;
        if (c.Spouse != null && !c.Academy)
        {
            spouse = Kin("custom_spouse", c.Spouse.Name, c.Sex == "male" ? "female" : "male", c.Spouse.Age, "spouse:");
            Bond(p.Id, spouse.Id, BondType.Marriage, 55, 65); p.Flags.Add("wed"); spouse.Flags.Add("wed");
        }
        var father = c.Sex == "male" ? p : spouse; var mother = c.Sex == "male" ? spouse : p;
        for (int i = 0; i < c.Children.Count && !c.Academy; i++)
        {
            var k = Kin($"custom_child_{i + 1}", c.Children[i].Name, c.Children[i].Sex, c.Children[i].Age, $"child{i}:");
            k.FatherId = father?.Id ?? ""; k.MotherId = mother?.Id ?? ""; Bond(p.Id, k.Id, BondType.Blood, 60, 70); if (spouse != null) Bond(spouse.Id, k.Id, BondType.Blood, 60, 70);
        }
        if (c.Siblings.Count > 0)
        {
            // brothers and sisters share parents, who are in the chronicle and not in the world
            foreach (var (id, sex) in new[] { ("custom_father", "male"), ("custom_mother", "female") })
            {
                var dead = new Character { Id = id, HouseId = house.Id, Sex = sex, Age = p.Age + 28, Alive = false, Name = GivenName(content, house.Culture, sex, id + w.RngState, taken) + " " + surname };
                w.Characters[id] = dead;
            }
            p.FatherId = "custom_father"; p.MotherId = "custom_mother";
            for (int i = 0; i < c.Siblings.Count; i++)
            {
                var k = Kin($"custom_sibling_{i + 1}", c.Siblings[i].Name, c.Siblings[i].Sex, c.Siblings[i].Age, $"sibling{i}:");
                k.FatherId = "custom_father"; k.MotherId = "custom_mother"; Bond(p.Id, k.Id, BondType.Blood, 55, 60);
            }
        }

        // ---- the world's first opinion of them
        void Regard(string houseId, int theirs, int mine)
        {
            var other = w.House(houseId); var head = other != null ? Economy.HeadOf(w, other) : null; if (head == null) return;
            w.Relationship(head.Id, p.Id).Disposition = theirs; w.Relationship(p.Id, head.Id).Disposition = mine;
        }
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
