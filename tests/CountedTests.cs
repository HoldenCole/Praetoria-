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

    [Fact]
    public void A_name_lowered_for_a_sentence_keeps_the_Reach_and_the_Senate_capital()
    {
        Assert.Equal("the citizenship of the Reach", Counted.Lower("The Citizenship of the Reach"));
        Assert.Equal("citizenship for the whole Reach", Counted.Lower("Citizenship for the whole Reach"));
        Assert.Equal("the Reach's assizes", Counted.Lower("The Reach's assizes"));
        Assert.Equal("the navigation act", Counted.Lower("The Navigation Act"));
        Assert.Equal("open to the Reach, by the Senate's leave", Counted.Lower("Open to the Reach, by the Senate's leave"));
    }

    [Fact]
    public void Report_titles_keep_their_short_words_low()
    {
        // one house style for every report's title: "A Death in the House", never "A Death In The House" (a, an, the, and, but,
        // or, nor, for, of, in, on, at, to, by and as are low unless they open or close the title)
        var small = new System.Collections.Generic.HashSet<string> { "A", "An", "The", "And", "But", "Or", "Nor", "For", "Of", "In", "On", "At", "To", "By", "As" };
        var bad = new System.Collections.Generic.List<string>();
        foreach (var f in System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(ContentLocator.FindContentDir(), "text"), "*.json"))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(f), "\"title\":\\s*\"([^\"]+)\""))
            {
                var words = m.Groups[1].Value.Split(' ');
                for (int i = 1; i < words.Length - 1; i++)
                    if (small.Contains(words[i]) && !words[i - 1].EndsWith(":") && !(words[i - 1] == "Brought" && words[i] == "On")) { bad.Add(System.IO.Path.GetFileName(f) + ": " + m.Groups[1].Value); break; }
            }
        Assert.True(bad.Count == 0, "titles with a capital on a short word: " + string.Join("; ", bad));
    }
}
