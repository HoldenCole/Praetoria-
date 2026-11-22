using Praetoria.Core;
using Praetoria.Core.Data;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A count is said as a person says it (docs/WEB_CLIENT.md, "Words").</summary>
public class CountedTests
{
    [Fact]
    public void One_of_a_thing_is_singular_and_anything_else_is_not()
    {
        Assert.Equal("1 hull", Counted.Hulls(1)); Assert.Equal("4 hulls", Counted.Hulls(4)); Assert.Equal("0 hulls", Counted.Hulls(0));
        Assert.Equal("1 more year", Counted.MoreYears(1)); Assert.Equal("3 more years", Counted.MoreYears(3)); Assert.Equal("2 office hulls", Counted.OfficeHulls(2));
        Assert.Equal("1 seat is", Counted.Seats(1) + " " + Counted.Are(1)); Assert.Equal("5 seats are", Counted.Seats(5) + " " + Counted.Are(5));
        Assert.Equal("was", Counted.Were(1)); Assert.Equal("go", Counted.Go(2)); Assert.Equal("sails", Counted.Sail(1));
    }

    [Fact]
    public void Nothing_the_game_writes_for_the_player_says_s_in_brackets()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(ContentLocator.FindContentDir(), "..", "src")); char sep = System.IO.Path.DirectorySeparatorChar;
        var bad = new System.Collections.Generic.List<string>(); var written = new System.Text.RegularExpressions.Regex("[ }][a-z]+\\(s\\)[ .,;:\"<)]");
        foreach (var f in System.IO.Directory.EnumerateFiles(root, "*.*", System.IO.SearchOption.AllDirectories))
        {
            if (!(f.EndsWith(".cs") || f.EndsWith(".razor")) || f.Contains(sep + "obj" + sep) || f.Contains(sep + "bin" + sep)) continue;
            int n = 0; foreach (var line in System.IO.File.ReadLines(f)) { n++; if (written.IsMatch(line) && !line.Contains("=> ")) bad.Add($"{System.IO.Path.GetFileName(f)}:{n}"); }
        }
        Assert.True(bad.Count == 0, "written with (s): " + string.Join(", ", bad));
    }
}
