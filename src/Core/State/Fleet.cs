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

    public int Strength { get; set; } = 1;

    /// <summary>0..100. Recovers while holding, erodes in transit; a worn fleet is a liability.</summary>
    public int Readiness { get; set; } = 100;

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
    public string SystemId { get; set; } = "";
    public int Strength { get; set; }
    public int Confidence { get; set; }
    public int TurnObserved { get; set; }
}
