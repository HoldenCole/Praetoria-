using System.Text.Json;
using System.Text.Json.Serialization;
using Praetoria.Core.State;

namespace Praetoria.Core.Data;

/// <summary>
/// Save/load (BuildSpec §3 "saves = serialize World", §7 "versioned for forward-compat"). The
/// <see cref="World"/> is the single source of truth and already carries the RNG position
/// (<see cref="World.RngState"/>), so a save is the World serialised and nothing else: load it, build a
/// <c>TurnController</c> on it, and the run continues on the exact same deterministic stream. Save
/// between turns (controller phase Idle) — mid-Action briefing bindings are not part of the World.
/// Content (events, catalogs) is not saved; it reloads from <c>/content</c>, so content patches apply to
/// existing saves — the mod-ready stance of GDD §20.
/// </summary>
public static class SaveSystem
{
    /// <summary>Bump when the World shape changes incompatibly; loaders can migrate on this.</summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The on-disk envelope: a version stamp around the World.</summary>
    public sealed class SaveFile
    {
        public int Version { get; set; } = CurrentVersion;
        public string Game { get; set; } = "praetoria";
        public World World { get; set; } = new();
    }

    public static string Serialize(World world) =>
        JsonSerializer.Serialize(new SaveFile { World = world }, Options);

    public static World Deserialize(string json)
    {
        SaveFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SaveFile>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ContentException($"Save file is not valid JSON: {ex.Message}", ex);
        }
        if (file == null || file.Game != "praetoria")
            throw new ContentException("Not a Praetoria save file.");
        if (file.Version > CurrentVersion)
            throw new ContentException($"Save version {file.Version} is newer than this build supports ({CurrentVersion}).");
        return file.World;
    }

    public static void Save(World world, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serialize(world));
    }

    public static World Load(string path)
    {
        if (!File.Exists(path)) throw new ContentException($"Save file not found: {path}");
        return Deserialize(File.ReadAllText(path));
    }
}
