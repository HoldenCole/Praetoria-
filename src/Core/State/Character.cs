namespace Praetoria.Core.State;

/// <summary>
/// A person in the world (BuildSpec §3). Entities are plain data with composed fields —
/// "composition over deep inheritance". Traits are split per GDD §18 into Nature
/// (constraining, stress-bearing) and Aptitude (gating/flavour). Skills crystallise in the
/// Academy Crucible (§13). Everything here is serialisable; the World is the save.
/// </summary>
public sealed class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string HouseId { get; set; } = "";
    public int Age { get; set; }
    public bool Alive { get; set; } = true;

    /// <summary>"male" / "female" (or "" if unspecified). Used by the dynasty layer for births and
    /// matrilineal/patrilineal succession (GDD §14/§18).</summary>
    public string Sex { get; set; } = "";

    /// <summary>Blood parents, "" if unknown/founder. Carry succession rights (GDD §14 blood ties).</summary>
    public string MotherId { get; set; } = "";
    public string FatherId { get; set; } = "";

    // Career ladder (GDD §13) — resets each generation. V1 tracks: military / stewardship / law.
    public string CareerTrack { get; set; } = "";
    public int CareerRank { get; set; }

    // GDD §18 two trait classes.
    public HashSet<string> NatureTraits { get; set; } = new();
    public HashSet<string> AptitudeTraits { get; set; } = new();

    public Dictionary<string, int> Skills { get; set; } = new();

    public int Stress { get; set; }
    public string Ambition { get; set; } = "";

    /// <summary>
    /// Per-character status flags / armed gates (e.g. "feud", "humiliated"). The engine
    /// arms and clears these via consequences — this is how unscripted storylines chain.
    /// </summary>
    public HashSet<string> Flags { get; set; } = new();

    // ---- personal wealth (docs/WEALTH.md)
    /// <summary>This person's own money, apart from their house's treasury. Negative is debt.</summary>
    public int Purse { get; set; }
    /// <summary>What they did with their money last year, in a line.</summary>
    public string LastSpend { get; set; } = "";
    /// <summary>What they have said shall be done with what is theirs when they die. Null: all of it to the heir.</summary>
    public Will? Will { get; set; }

    // ---- education (docs/EDUCATION.md)
    /// <summary>Learning banked toward the next step in each skill.</summary>
    public Dictionary<string, int> Learning { get; set; } = new();
    /// <summary>The skill this person has set themselves to study. Their own choice; the player chooses only their own.</summary>
    public string Study { get; set; } = "";
    /// <summary>For a child: the course the head has set (arms, forum, counting), or empty to follow their own bent.</summary>
    public string Upbringing { get; set; } = "";
    /// <summary>A paid teacher: 0 none, 1 a good one, 2 the best.</summary>
    public int Tutor { get; set; }

    // ---- found family (docs/HOUSEHOLD.md): people taken into a house they were not born to
    /// <summary>The house this person was born to, when they now live in another. Empty for someone in the house of their birth.</summary>
    public string BornHouseId { get; set; } = "";
    /// <summary>The head who adopted them. An adopted person carries the name and stands in the line as a child of that head.</summary>
    public string AdoptedById { get; set; } = "";
    /// <summary>Whoever has this person under their wing: a mentor, or the head who took them in.</summary>
    public string MentorId { get; set; } = "";
    /// <summary>The turn they joined the house they now live in, or took their mentor.</summary>
    public int JoinedTurn { get; set; }

    public int Skill(string key) => Skills.TryGetValue(key, out var v) ? v : 0;
}
