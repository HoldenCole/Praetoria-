using System.Text.RegularExpressions;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The owner's style: no em dashes in anything a player reads (docs/VISUAL_SYSTEM.md, rule 9). The content is checked by
/// validate_praetoria.py; this reads every string literal in Core (log lines, refusals, reports) and fails on an em dash used as
/// punctuation. A lone dash standing for "nothing" in a table cell is not punctuation and is allowed.</summary>
public class NoEmDashTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Praetoria.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("no Praetoria.sln above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void NoStringInCoreCarriesAnEmDash()
    {
        var bad = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Root(), "src/Core"), "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains('\u2014')) continue;
                var code = Regex.Replace(line, "//.*$", "");   // a comment is not read by a player
                foreach (Match m in Regex.Matches(code, "\\$?@?\"(?:[^\"\\\\]|\\\\.)*\""))
                {
                    var lit = m.Value.TrimStart('$', '@').Trim('"');
                    if (lit.Contains('\u2014') && lit.Trim() != "\u2014") bad.Add($"{Path.GetFileName(path)}:{i + 1}: {lit}");
                }
            }
        }
        Assert.True(bad.Count == 0, "em dashes in Core strings (use a comma, colon or full stop):\n" + string.Join("\n", bad));
    }
}
