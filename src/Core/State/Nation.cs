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
}
