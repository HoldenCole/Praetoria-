namespace Praetoria.Core.State;

/// <summary>A foreign nation as it stands (docs/FOREIGN_NATIONS.md): its strength, its regard for the Reach, the war if there is one and its score,
/// whether it is subdued and paying tribute, when it last raided. Serialised with the World.</summary>
public sealed class Nation
{
    public string Id { get; set; } = "";
    public int Strength { get; set; }
    public int Disposition { get; set; }
    public bool AtWar { get; set; }
    public int WarSince { get; set; }
    public int ScoreReach { get; set; }
    public int ScoreNation { get; set; }
    public int LastBattleTurn { get; set; }
    /// <summary>The turn until which the nation pays tribute to the chair; 0 when free.</summary>
    public int SubduedUntil { get; set; }
    public int LastRaidTurn { get; set; }
    public int Wars { get; set; }
    /// <summary>Who rules it, and who leads its fleets (docs/BEYOND.md, stage 0). Null until the nation is first reckoned.</summary>
    public NationPerson? Ruler { get; set; }
    public NationPerson? Commander { get; set; }
    /// <summary>How many of the nation's names have been used, so the next person has a new one.</summary>
    public int NamesUsed { get; set; }
    /// <summary>Fixed when its people are first reckoned, from the world's own dice, so that no two games have the same rulers in the same years.</summary>
    public ulong Salt { get; set; }
    /// <summary>The house that holds the nation's worlds and fleets, once it is on the map (docs/BEYOND.md); "" while it is off it.</summary>
    public string HouseId { get; set; } = "";
    /// <summary>The system its ruler sits in: taking it subdues the nation.</summary>
    public string CapitalSystemId { get; set; } = "";
    public bool OnMap => HouseId.Length > 0;
    /// <summary>Its last world has been taken: it raids nobody, wars with nobody, and pays nobody.</summary>
    public bool Fallen { get; set; }
    /// <summary>What a victory over it is worth in plunder (from its definition; kept here so the fleets can pay it without the catalogue).</summary>
    public int Wealth { get; set; }
    /// <summary>How its fleets fight on the map, hull for hull, by its temper: a hundred is a fleet of the Reach's. 0 until it is first reckoned.</summary>
    public int Mettle { get; set; }
    /// <summary>Whether the nation began the war now being fought (then every house defends, not only those that wanted a war).</summary>
    public bool BeganIt { get; set; }
    /// <summary>The year its last war ended; 0 if it has had none.</summary>
    public int LastWarEnd { get; set; }
    /// <summary>The house that holds a nation's worlds on the map is named for it, so that anything walking the houses can tell at a
    /// glance, and without the world in hand, that it is not a house of the Reach.</summary>
    public const string HousePrefix = "nation_";
    public static string HouseIdFor(string nationId) => HousePrefix + nationId;
    public static bool IsHouse(string houseId) => houseId.StartsWith(HousePrefix, StringComparison.Ordinal);
    public bool Subdued(int turn) => SubduedUntil > turn;
}

/// <summary>A person of a foreign nation: its ruler or the commander of its fleets. Not yet a character of the Reach's world: they have a
/// name, an age, and what matters about them from the Reach's side of the border.</summary>
public sealed class NationPerson
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    /// <summary>A commander's skill, 1 to 9. A ruler's is not used.</summary>
    public int Skill { get; set; }
    /// <summary>A ruler's nature: "warlike", "wary", "grasping" or "mild". A commander's is not used.</summary>
    public string Nature { get; set; } = "";
    public int Since { get; set; }
    public int Victories { get; set; }
    public int Defeats { get; set; }
    /// <summary>The person of the Reach's world who stands for them, once the nation is on the map.</summary>
    public string CharacterId { get; set; } = "";
}
