namespace Praetoria.Core.Data;

/// <summary>
/// Finds the repo's /content directory by walking up from a starting directory. Lets the console
/// harness and the tests locate content without hard-coded absolute paths, regardless of where the
/// binary runs from (bin/Debug/... up to the repo root).
/// </summary>
public static class ContentLocator
{
    public static string FindContentDir(string? startDir = null)
    {
        var dir = new DirectoryInfo(startDir ?? AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "events")))
                return candidate;
            dir = dir.Parent;
        }
        throw new ContentException(
            "Could not locate a /content directory (with an /events subfolder) above " +
            (startDir ?? AppContext.BaseDirectory) + ".");
    }
}
