using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Creation;

/// <summary>The player's choices at character creation. Null/empty means "keep the scenario's default".</summary>
public sealed class CreationOptions
{
    public string? Name { get; set; }
    public string? Sex { get; set; }
    public string? BackgroundId { get; set; }
    public List<string> Nature { get; set; } = new();
    public List<string> Aptitude { get; set; } = new();
    public string? CareerTrack { get; set; }
    public string? Ambition { get; set; }

    public bool IsEmpty =>
        Name == null && Sex == null && BackgroundId == null && Nature.Count == 0 &&
        Aptitude.Count == 0 && CareerTrack == null && Ambition == null;
}

/// <summary>
/// Character customisation (GDD §13/§18): shapes the scenario's protagonist before turn 1 — name and sex,
/// a <see cref="BackgroundDef"/> (origin: legitimacy, treasury, traits, skills), chosen nature/aptitude
/// traits, career track and ambition. Choices are checked against the data catalogs so a typo cannot create
/// a trait the content never references. Applied once, through the World, before the TurnController starts.
/// </summary>
public static class CharacterCreation
{
    public const int MaxNature = 3;
    public const int MaxAptitude = 3;
    public const int MaxPickedNature = 2;     // the player picks up to 2; a background may add one more
    public const int MaxPickedAptitude = 2;

    /// <summary>Apply the choices to the world's protagonist. Throws <see cref="ContentException"/> on an
    /// unknown background/trait/track/ambition or on too many picks. Returns a chronicle line.</summary>
    public static string Apply(World world, ContentDatabase content, CreationOptions o)
    {
        var p = world.Protagonist ?? throw new ContentException("The world has no protagonist to customise.");
        var house = world.House(p.HouseId) ?? throw new ContentException($"Protagonist house '{p.HouseId}' is missing.");
        var traits = content.Traits;

        if (o.Nature.Count > MaxPickedNature) throw new ContentException($"Pick at most {MaxPickedNature} nature traits.");
        if (o.Aptitude.Count > MaxPickedAptitude) throw new ContentException($"Pick at most {MaxPickedAptitude} aptitude traits.");
        foreach (var t in o.Nature) if (!traits.IsEmpty && !traits.IsNature(t)) throw new ContentException($"Unknown nature trait '{t}'.");
        foreach (var t in o.Aptitude) if (!traits.IsEmpty && !traits.IsAptitude(t)) throw new ContentException($"Unknown aptitude trait '{t}'.");
        if (o.Ambition != null && !traits.IsEmpty && !traits.IsAmbition(o.Ambition)) throw new ContentException($"Unknown ambition '{o.Ambition}'.");
        if (o.CareerTrack != null && !content.Careers.IsEmpty && content.Careers.ById(o.CareerTrack) == null)
            throw new ContentException($"Unknown career track '{o.CareerTrack}'.");
        if (o.Sex != null && o.Sex != "male" && o.Sex != "female") throw new ContentException("Sex must be 'male' or 'female'.");

        BackgroundDef? bg = null;
        if (o.BackgroundId != null)
            bg = content.Backgrounds.ById(o.BackgroundId) ?? throw new ContentException($"Unknown background '{o.BackgroundId}'.");

        if (!string.IsNullOrWhiteSpace(o.Name)) p.Name = o.Name.Trim();
        if (o.Sex != null) p.Sex = o.Sex;
        if (o.Nature.Count > 0) p.NatureTraits = new HashSet<string>(o.Nature);        // picks replace the defaults
        if (o.Aptitude.Count > 0) p.AptitudeTraits = new HashSet<string>(o.Aptitude);
        if (o.CareerTrack != null) p.CareerTrack = o.CareerTrack;
        if (o.Ambition != null) p.Ambition = o.Ambition;

        if (bg != null)
        {
            house.Legitimacy = Math.Max(0, house.Legitimacy + bg.Legitimacy);
            house.Treasury.Add(bg.Treasury);
            house.Treasury.ClampNonCredit();
            foreach (var t in bg.Nature) p.NatureTraits.Add(t);                        // a background adds on top
            foreach (var t in bg.Aptitude) p.AptitudeTraits.Add(t);
            foreach (var kv in bg.Skills) p.Skills[kv.Key] = Math.Max(0, p.Skill(kv.Key) + kv.Value);
            foreach (var f in bg.Flags) p.Flags.Add(f);
            p.Stress = Math.Clamp(p.Stress + bg.Stress, 0, 100);
            if (o.CareerTrack == null && bg.TrackHint != null) p.CareerTrack = bg.TrackHint;
            if (o.Ambition == null && bg.AmbitionHint != null) p.Ambition = bg.AmbitionHint;
            p.Flags.Add("background:" + bg.Id);
        }

        if (p.NatureTraits.Count > MaxNature) throw new ContentException($"Too many nature traits ({p.NatureTraits.Count} > {MaxNature}).");
        if (p.AptitudeTraits.Count > MaxAptitude) throw new ContentException($"Too many aptitude traits ({p.AptitudeTraits.Count} > {MaxAptitude}).");

        string line = $"{p.Name} of {house.Name} takes the stage" +
                      (bg != null ? $" — {bg.Name.ToLowerInvariant()}" : "") +
                      $"; {string.Join(", ", p.NatureTraits.OrderBy(t => t, StringComparer.Ordinal))}" +
                      (p.AptitudeTraits.Count > 0 ? $" ({string.Join(", ", p.AptitudeTraits.OrderBy(t => t, StringComparer.Ordinal))})" : "") +
                      $", serving in {content.Careers.ById(p.CareerTrack)?.Name ?? p.CareerTrack}.";
        world.Log(line);
        return line;
    }
}
