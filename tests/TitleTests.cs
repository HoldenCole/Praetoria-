using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Titles by era (docs/TITLES.md; the owner's direction): the Republic has no nobles. The same rungs carry civic and military
/// names until a throne stands and noble ones after; ids never change. Prose follows with {role.title}, {role.rankName} and {era:A|B}.</summary>
public class TitleTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    [Fact]
    public void TheHouseLadder_HasNoNobleName_InTheRepublic_AndEveryNobleOne_UnderTheImperium()
    {
        var t = Content.Titles; string rep = World.EraRepublic, imp = World.EraImperium;
        Assert.Equal(new[] { "Citizen", "Warden", "Prefect", "Legate", "Proconsul", "First Citizen", "Dictator" }, t.ByRank.Select(d => d.NameIn(rep)).ToArray());
        Assert.Equal(new[] { "Landless", "Knight", "Baron", "Count", "Duke", "Archduke", "Emperor" }, t.ByRank.Select(d => d.NameIn(imp)).ToArray());
        string[] noble = { "Knight", "Baron", "Count", "Duke", "Archduke", "Emperor", "Lord", "Landless" };
        foreach (var d in t.ByRank) Assert.DoesNotContain(noble, n => d.NameIn(rep).Contains(n));
        Assert.Equal("Prefect", t.NameOf("baron", rep)); Assert.Equal("Baron", t.NameOf("baron", imp)); Assert.Equal("nonesuch", t.NameOf("nonesuch", rep));
        foreach (var d in t.ByRank) { Assert.Equal(d.NameIn(rep), Title.StyleOf(d.Id, rep)); Assert.Equal(d.NameIn(imp), Title.StyleOf(d.Id, imp)); }   // the fallback mirrors the content
        Assert.Equal(new[] { "landless", "knight", "baron", "count", "duke", "archduke", "emperor" }, t.ByRank.Select(d => d.Id).ToArray());           // ids are the same in both
    }

    [Fact]
    public void TheCareerLadders_RenameTheirTopRungs_WhenAThroneStands_AndKeepTheirLength()
    {
        var c = Content.Careers; string rep = World.EraRepublic, imp = World.EraImperium;
        Assert.Equal("Fleet Admiral", c.RankName("military", 6, rep)); Assert.Equal("Grand Admiral", c.RankName("military", 6, imp));
        Assert.Equal("High Treasurer", c.RankName("stewardship", 4, rep)); Assert.Equal("Imperial Treasurer", c.RankName("stewardship", 4, imp));
        Assert.Equal("Governor", c.RankName("stewardship", 2, rep)); Assert.Equal("Viceroy", c.RankName("stewardship", 2, imp));
        Assert.Equal("High Justice", c.RankName("law", 4, rep)); Assert.Equal("Lord Justice", c.RankName("law", 4, imp));
        Assert.Equal("Lieutenant", c.RankName("military", 2, rep)); Assert.Equal("Lieutenant", c.RankName("military", 2, imp));
        Assert.Equal("the Navy", c.ById("military")!.NameIn(rep)); Assert.Equal("the Imperial Navy", c.ById("military")!.NameIn(imp));
        foreach (var tr in c.Defs)
        {
            Assert.True(tr.ImperialRanks.Count <= tr.Ranks.Count);
            foreach (var r in tr.Ranks) { Assert.DoesNotContain("Imperial", r); Assert.DoesNotContain("Lord", r); }   // nothing imperial or noble on the Republic's ladder
            Assert.Null(tr.RankName(tr.Ranks.Count, rep)); Assert.Null(tr.RankName(-1, imp));
        }
    }

    [Fact]
    public void Prose_FollowsTheEra()
    {
        var w = TestContent.EconomyWorld(credits: 10, materials: 5); var self = w.Protagonist!; w.House(self.HouseId)!.Title = "baron"; self.CareerTrack = "stewardship"; self.CareerRank = 4;
        var ctx = new EvalContext(w, new Binding(self.Id), TestContent.Rng(), Content);
        const string line = "{self.name}, {self.title} and {self.rankName}, held {era:open audience|court}; {era:the chair|the crown} watched.";
        w.Era = World.EraRepublic;
        Assert.Equal($"{self.Name}, Prefect and High Treasurer, held open audience; the chair watched.", TextRenderer.Substitute(line, ctx));
        w.Era = World.EraImperium;
        Assert.Equal($"{self.Name}, Baron and Imperial Treasurer, held court; the crown watched.", TextRenderer.Substitute(line, ctx));
        Assert.Equal("{era:no bar}", TextRenderer.Substitute("{era:no bar}", ctx));   // malformed tokens are left for the validator to report
    }

    [Fact]
    public void NoRepublicEvent_NamesANobleRank_InItsProse()
    {
        // every event not gated to the Imperium must read clean in the Republic once its {era:…} tokens resolve
        var rx = new System.Text.RegularExpressions.Regex(@"\b(baron|barons|barony|duke|dukes|duchy|duchess|archduke|knight|knights|vassal|vassals|fief|fiefs|liege|nobles|nobility|your grace|majesty)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var strip = new System.Text.RegularExpressions.Regex(@"\{era:([^|}]*)\|[^}]*\}");
        var bad = new List<string>();
        foreach (var e in Content.Events)
        {
            if (e.Conditions.OfType<EraCondition>().Any(c => c.Era == World.EraImperium && c.Present)) continue;
            var text = Content.Text(e.Id); if (text == null) continue;
            var all = new List<string> { text.Title ?? "", text.Body }; all.AddRange(text.Choices.Values);
            all.AddRange(e.Choices.SelectMany(c => c.Effects).OfType<LogEffect>().Select(l => l.Text));
            foreach (var s in all) { var rep = strip.Replace(s, "$1"); if (rx.IsMatch(rep)) bad.Add($"{e.Id}: {rx.Match(rep).Value}"); }
        }
        Assert.True(bad.Count == 0, "noble words in republic prose: " + string.Join("; ", bad.Distinct()));
    }
}
