namespace Praetoria.Core.State;

/// <summary>A foreign nation as it stands (docs/FOREIGN_NATIONS.md): its strength, its regard for the Reach, the war if there is one and its score,
/// whether it is subdued and paying tribute, when it last raided. Serialised with the World.</summary>
public sealed class Nation
{
    public string Id { get; set; } = "";
    public int Strength { get; set; }
    /// <summary>How strong this nation is in this game, in a hundred of what is written for it: fixed when it is first reckoned, from the
    /// game's own dice and the setting chosen at the start (docs/BEYOND.md). 0 in a save made before it varied, which reads as a hundred.</summary>
    public int Might { get; set; }
    /// <summary>How readily it attacks in this game, in a hundred of what is written: the setting chosen at the start and the game's own
    /// dice (docs/BEYOND.md). It shortens or lengthens the odds of a great ruler and the rest between its wars. 0 reads as a hundred.</summary>
    public int Boldness { get; set; }
    /// <summary>A client state of the Reach (docs/BEYOND.md, "Kinds of peace"): beaten decisively and left its own ruler, it pays the
    /// chair and its patron every year, raids nobody and wars with nobody, until it throws the treaty off. The patron is the house
    /// whose fleets beat it; "" if none.</summary>
    public bool Vassal { get; set; }
    public string PatronId { get; set; } = "";
    /// <summary>The year until which the Reach pays this nation tribute, after a war lost with nothing to cede; 0 when it pays none.</summary>
    public int PaidUntil { get; set; }
    /// <summary>How its last war ended: "client", "tribute", "territory", "white", "ceded", "paid", "bought". "" if it has had none.</summary>
    public string LastPeace { get; set; } = "";
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
    /// <summary>Years added to a siege of a world it holds (from its definition; kept here for the fleets).</summary>
    public int Walls { get; set; }
    /// <summary>What is still in its capital for whoever first takes it, and the standing that goes with it. Both 0 once taken.</summary>
    public int Treasure { get; set; }
    public int Titles { get; set; }
    /// <summary>The name of the thing in its vaults, until the vaults are opened.</summary>
    public string Regalia { get; set; } = "";
    public bool BeganIt { get; set; }
    /// <summary>Whose war it is (docs/BEYOND.md, "Whose war"). "" is the Reach's: voted by the Senate or begun by the nation, and every
    /// house is at war with it. A house's id is that house's own war: begun without the Senate's leave, or fought on after the Senate
    /// voted peace. Then only that house is at war with the nation.</summary>
    public string WarOf { get; set; } = "";
    /// <summary>The Senate voted peace in this war, and the house whose war it now is did not listen.</summary>
    public bool Defied { get; set; }
    /// <summary>The year the Senate voted peace, while those in the field are being told; 0 when no peace is voted.</summary>
    public int PeaceVoted { get; set; }
    /// <summary>The year its last war ended; 0 if it has had none.</summary>
    public int LastWarEnd { get; set; }
    /// <summary>The house that holds a nation's worlds on the map is named for it, so that anything walking the houses can tell at a
    /// glance, and without the world in hand, that it is not a house of the Reach.</summary>
    public const string HousePrefix = "nation_";
    public static string HouseIdFor(string nationId) => HousePrefix + nationId;
    public static bool IsHouse(string houseId) => houseId.StartsWith(HousePrefix, StringComparison.Ordinal);
    public bool Subdued(int turn) => Vassal || SubduedUntil > turn;
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
    /// <summary>A ruler of the kind that unites a nation and leads it to conquest (docs/BEYOND.md, the Great Khan), and since when.</summary>
    public bool Great { get; set; }
    public int GreatSince { get; set; }
    public int Victories { get; set; }
    public int Defeats { get; set; }
    /// <summary>The person of the Reach's world who stands for them, once the nation is on the map.</summary>
    public string CharacterId { get; set; } = "";
}
