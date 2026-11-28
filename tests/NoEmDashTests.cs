using System.Text.RegularExpressions;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The owner's style: no em dashes in anything a player reads (docs/VISUAL_SYSTEM.md, rule 9). The content is checked by
/// validate_praetoria.py; this reads every string literal in Core (log lines, refusals, reports) and fails on an em dash used as
/// punctuation, and on a lone dash standing for "nothing" too: that is said in a word, or the table cell takes a muted dot.</summary>
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
                    if (lit.Contains('\u2014')) bad.Add($"{Path.GetFileName(path)}:{i + 1}: {lit}");
                }
            }
        }
        Assert.True(bad.Count == 0, "em dashes in Core strings (use a comma, colon or full stop):\n" + string.Join("\n", bad));
    }

    [Fact]
    public void An_empty_bundle_says_nothing_in_a_word()
    {
        Assert.Equal("nothing", Praetoria.Core.Presentation.CourtProjection.Bundle(new Praetoria.Core.State.Resources()));
        var some = new Praetoria.Core.State.Resources(); some.Set(Praetoria.Core.State.Resource.Materials, 3);
        Assert.DoesNotContain('\u2014', Praetoria.Core.Presentation.CourtProjection.Bundle(some));
    }
}
