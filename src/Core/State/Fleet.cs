namespace Praetoria.Core.State;

/// <summary>
/// A fleet (BuildSpec §3; GDD §8 the office/house split; GDD §19 fleets as map markers). Strength is an
/// abstract ship count — the military layer stays at "curated scale" (no hull classes yet). A fleet sits in a
/// system or is in transit along lanes (one jump per turn). <see cref="Office"/> marks an Imperial-office
/// fleet the owner commands but does not own (lose the post, lose the fleet); a house fleet is the dynasty's
/// own and survives any office. Serialised with the World.
/// </summary>
public sealed class Fleet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>The house that commands it (the office holder's house for an office fleet).</summary>
    public string OwnerId { get; set; } = "";

    /// <summary>True for an Imperial/state fleet held by office (GDD §8 treasury 1); false for a house fleet (treasury 2).</summary>
    public bool Office { get; set; }

    /// <summary>Hull count — the quick measure intel, threat and content read. Kept in step with <see cref="Ships"/>.</summary>
    public int Strength { get; set; } = 1;

    /// <summary>Composition by ship class (class id → hulls). Empty ⇒ <see cref="Strength"/> hulls of the rules' default class.</summary>
    public Dictionary<string, int> Ships { get; set; } = new();

    /// <summary>The character commanding it, or "" for an unled fleet (GDD §6 "the admiral's skill").</summary>
    public string AdmiralId { get; set; } = "";

    /// <summary>Battles survived, 0..veterancyMax; each adds veterancyBonus to power.</summary>
    public int Veterancy { get; set; }

    /// <summary>Hulls of a class, or all of <see cref="Strength"/> for the default class when no composition is recorded.</summary>
    public int Of(string classId, string defaultClass) =>
        Ships.Count == 0 ? (classId == defaultClass ? Strength : 0) : Ships.GetValueOrDefault(classId);

    /// <summary>Make <see cref="Strength"/> agree with the composition (call after changing <see cref="Ships"/>).</summary>
    public void Recount() { if (Ships.Count > 0) Strength = Ships.Values.Sum(); }

    /// <summary>0..100. Recovers while holding, erodes in transit; a worn fleet is a liability.</summary>
    public int Readiness { get; set; } = 100;

    /// <summary>How it fights (docs/MILITARY.md): "line" (default), "press", "skirmish" or "boarding".</summary>
    public string Stance { get; set; } = "line";
    /// <summary>What it fights with: "guns" (default) or "torpedoes" (burns an isotope a battle for more power).</summary>
    public string Loadout { get; set; } = "guns";
    /// <summary>0..100 loyalty of the crews to the house: pay, fuel, victories and the officers' regard move it; low, they will not sail; lower, they desert.</summary>
    public int Loyalty { get; set; } = 70;
    /// <summary>0..100 supply: full within reach of a world or depot of the house, draining beyond; low, the fleet fights worse; nought, hulls desert.</summary>
    public int Supply { get; set; } = 100;

    public string SystemId { get; set; } = "";

    /// <summary>Where it is going, or "" when holding. Arrival after <see cref="Eta"/> turns.</summary>
    public string DestinationId { get; set; } = "";
    public int Eta { get; set; }

    public bool Moving => DestinationId.Length > 0;
}

/// <summary>
/// What the protagonist's house <em>believes</em> about a rival fleet (GDD §19 mixed visibility: territory is
/// public, fleets are reported intel with a confidence). Refreshed when the fleet is seen in or beside your
/// territory; otherwise the report ages and its confidence decays while the last-known position stands.
/// </summary>
public sealed class FleetIntel
{
    public string FleetId { get; set; } = "";
    /// <summary>How it fights (docs/MILITARY.md): "line" (default), "press", "skirmish" or "boarding".</summary>
    public string Stance { get; set; } = "line";
    /// <summary>What it fights with: "guns" (default) or "torpedoes" (burns an isotope a battle for more power).</summary>
    public string Loadout { get; set; } = "guns";
    /// <summary>0..100 loyalty of the crews to the house: pay, fuel, victories and the officers' regard move it; low, they will not sail; lower, they desert.</summary>
    public int Loyalty { get; set; } = 70;
    /// <summary>0..100 supply: full within reach of a world or depot of the house, draining beyond; low, the fleet fights worse; nought, hulls desert.</summary>
    public int Supply { get; set; } = 100;

    public string SystemId { get; set; } = "";
    public int Strength { get; set; }
    public int Confidence { get; set; }
    public int TurnObserved { get; set; }
}
