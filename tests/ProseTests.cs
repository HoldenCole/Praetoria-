using System.Text.RegularExpressions;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Prose that reads as written for the game, not assembled from parts (docs/CONTENT_GUIDE.md, "Tokens" and "What the check
/// refuses"): a person is named in full once and then by the given name or he and she, a relative of yours is said as one, and the
/// slips a reader screenshots (stress for Strain, a county in the Republic, "of age" for a death, American spelling) are refused in the
/// content and in the strings of the screens.</summary>
public class ProseTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    /// <summary>A head, Aurel, with a wife, a grown son (the heir) and a daughter married out, a grandchild, a brother bound by blood
    /// with no parents written down, and a stranger.</summary>
    private static World Family()
    {
        var w = new World { ProtagonistId = "aurel" };
        w.Houses["castellan"] = new House { Id = "castellan", Name = "House Castellan" };
        w.Houses["oriel"] = new House { Id = "oriel", Name = "House Oriel" };
        void Add(string id, string name, string house, string sex, int age, string father = "", string mother = "")
        {
            w.Characters[id] = new Character { Id = id, Name = name, HouseId = house, Sex = sex, Age = age, FatherId = father, MotherId = mother };
            w.Houses[house].Members.Add(id);
        }
        Add("aurel", "Aurel Castellan", "castellan", "male", 68);
        Add("helene", "Helene Castellan", "castellan", "female", 61);
        Add("corvin", "Corvin Castellan", "castellan", "male", 36, "aurel", "helene");
        Add("ysolde", "Ysolde Castellan", "castellan", "female", 30, "aurel", "helene");
        Add("dace", "Dace Oriel", "oriel", "male", 34);
        Add("tamsin", "Tamsin Castellan", "castellan", "female", 9, "corvin");
        Add("bastien", "Bastien Castellan", "castellan", "male", 64);
        Add("maren", "Maren Oriel", "oriel", "female", 45);
        Add("livia", "Livia Serval", "oriel", "", 30);
        Add("nobody", "Quell Ashby", "oriel", "", 30);
        w.Marriages.Add(new Marriage { A = "aurel", B = "helene" });
        w.Marriages.Add(new Marriage { A = "ysolde", B = "dace" });
        w.Relationship("aurel", "bastien").Bond = BondType.Blood;
        return w;
    }

    private static string Say(World w, string role, string id, string line) =>
        TextRenderer.Substitute(line, new EvalContext(w, new Binding("aurel").With(role, id), TestContent.Rng(), Content));

    [Fact]
    public void A_man_is_he_him_and_his_and_a_woman_she_and_her()
    {
        var w = Family();
        const string line = "{x.first}: {x.he} {x.him} {x.his} {x.hers} {x.himself}. {x.He} {x.Him} {x.His} {x.Hers} {x.Himself}.";
        Assert.Equal("Corvin: he him his his himself. He Him His His Himself.", Say(w, "x", "corvin", line));
        Assert.Equal("Maren: she her her hers herself. She Her Her Hers Herself.", Say(w, "x", "maren", line));
        Assert.Equal("Corvin seeks your recognition of his claim.", Say(w, "kin", "corvin", "{kin.first} seeks your recognition of {kin.his} claim."));
        Assert.Equal("By noon, she knew who would break.", Say(w, "x", "maren", "By noon, {x.he} knew who would break."));
    }

    [Fact]
    public void One_whose_sex_was_never_set_follows_the_name_or_is_said_by_it()
    {
        var w = Family();
        // Livia is a woman's name in the catalog; Quell is no one's, so the verb after the word still agrees
        Assert.Equal("She tells herself it is her own.", Say(w, "x", "livia", "{x.He} tells {x.himself} it is {x.his} own."));
        Assert.Equal("Quell tells themself it is Quell's own.", Say(w, "x", "nobody", "{x.He} tells {x.himself} it is {x.his} own."));
    }

    [Fact]
    public void A_relative_of_yours_is_said_as_one_and_the_heir_as_the_heir()
    {
        var w = Family();
        Assert.Equal("your son and heir", Say(w, "x", "corvin", "{x.rel}"));
        Assert.Equal("Your son and heir comes before you.", Say(w, "x", "corvin", "{x.Rel} comes before you."));
        Assert.Equal("your daughter", Say(w, "x", "ysolde", "{x.rel}"));
        Assert.Equal("your wife", Say(w, "x", "helene", "{x.rel}"));
        Assert.Equal("your daughter's husband", Say(w, "x", "dace", "{x.rel}"));
        Assert.Equal("your granddaughter", Say(w, "x", "tamsin", "{x.rel}"));
        Assert.Equal("your kinsman", Say(w, "x", "bastien", "{x.rel}"));      // bound by blood, nothing nearer written down
        Assert.Equal("Maren Oriel", Say(w, "x", "maren", "{x.rel}"));        // no kin at all: named, so the sentence still says who
        Assert.Equal("you", Say(w, "x", "aurel", "{x.rel}"));
        w.Characters["ysolde"].HouseId = "castellan"; w.Houses["castellan"].NamedHeirId = "ysolde";
        Assert.Equal("your daughter and heir", Say(w, "x", "ysolde", "{x.rel}"));
        Assert.Equal("your son", Say(w, "x", "corvin", "{x.rel}"));
    }

    [Fact]
    public void The_petition_and_the_first_morning_read_as_written_in_the_old_lions_house()
    {
        // the review's own cases: Corvin is Aurel's son and heir, not "kin, of your own blood", and he is he; the first morning names the cadet once
        var root = ContentLocator.FindContentDir();
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, "the_old_lion"), 1);
        var ctx = new EvalContext(w, new Binding(w.ProtagonistId).With("petitioner", "corvin_castellan"), TestContent.Rng(), Content);
        string body = TextRenderer.Substitute(Content.Text("the_petition")!.Body, ctx);
        Assert.StartsWith("Your son and heir, Corvin, comes before you", body);
        Assert.Contains("He seeks your recognition of his claim to a legateship", body);
        Assert.Contains("To raise him is to bind him and to arm him", body);
        Assert.DoesNotContain("They", body); Assert.DoesNotContain("{", body);
        Assert.Equal("Recognise the claim. Buy a loyalty; create a rival. Eyes open.", TextRenderer.Substitute(Content.Text("the_petition")!.Choices["recognize"], ctx));
        var log = Content.Event("the_petition")!.Choice("recognize")!.Effects.OfType<LogEffect>().Single().Text;
        Assert.Equal("Aurel Castellan recognised the claim of Corvin Castellan of House Castellan to a legateship of his own.", TextRenderer.Substitute(log, ctx).Split(" A title")[0]);
        var morning = Content.Event("first_formation")!.Choice("measure_them")!.Effects.OfType<LogEffect>().Single().Text;
        w.Protagonist!.Name = "Iria Solenne"; w.Protagonist.Sex = "female";
        Assert.Equal("Iria Solenne said nothing on the first morning, and watched everyone. By noon, she knew who would break.", TextRenderer.Substitute(morning, ctx));
    }

    [Fact]
    public void The_given_name_is_the_first_word_of_the_name()
    {
        Assert.Equal("Iria", TextRenderer.First("Iria Solenne"));
        Assert.Equal("Scion", TextRenderer.First("Scion"));
        var w = Family();
        Assert.Equal("Corvin of House Castellan, Corvin Castellan", Say(w, "x", "corvin", "{x.first} of {x.house}, {x.name}"));
        Assert.Equal("{x.nonesuch} {y.first}", Say(w, "x", "corvin", "{x.nonesuch} {y.first}"));   // left for the validator to report
    }

    private static IReadOnlyList<ContentIssue> Check(string line, bool imperiumOnly = false)
    {
        var e = new EventDef
        {
            Id = "probe", Roles = new[] { new RoleDef { Name = "kin" } },
            Conditions = imperiumOnly ? new ICondition[] { new EraCondition(World.EraImperium, true) } : Array.Empty<ICondition>(),
            Choices = new[] { new Choice { Id = "a", Effects = new IEffect[] { new LogEffect(line) } } }
        };
        var text = new EventText { Id = "probe", Title = "A Probe", Body = "You are asked.", Choices = new Dictionary<string, string> { ["a"] = "Answer." } };
        return ContentValidator.Validate(TestContent.Db(new[] { e }, new[] { text })).Where(i => i.Severity == "error").ToList();
    }

    [Fact]
    public void The_check_refuses_a_name_said_twice_and_passes_it_said_once()
    {
        Assert.Contains(Check("{self.name} said nothing, and watched. By noon, {self.name} knew who would break."), i => i.Message.Contains("{self.name} 2 times"));
        Assert.Empty(Check("{self.name} said nothing, and watched. By noon, {self.he} knew who would break."));
        Assert.Empty(Check("{self.name} answered {kin.name}; {kin.first} did not laugh, and {self.first} did."));
        Assert.Contains(Check("{kin.name} came, and {kin.name} went."), i => i.Message.Contains("{kin.first}"));
    }

    [Fact]
    public void The_check_refuses_the_slips_a_reader_screenshots()
    {
        Assert.Contains(Check("The year added to {self.first}'s stress."), i => i.Message.Contains("Strain"));
        Assert.Contains(Check("{self.name} granted {kin.name} a county of {kin.his} own."), i => i.Message.Contains("county"));
        Assert.Empty(Check("{self.name} granted {kin.name} {era:a legateship|a county} of {kin.his} own."));
        Assert.Contains(Check("{self.name} granted {kin.name} {era:a county|a county}."), i => i.Message.Contains("county"));
        Assert.Empty(Check("{self.name} granted {kin.name} a county.", imperiumOnly: true));
        Assert.Contains(Check("{self.name} died at 78, of age."), i => i.Message.Contains("of old age"));
        Assert.Empty(Check("{kin.name} came of age, and {self.name} died of old age."));
        foreach (var word in new[] { "favor", "honor", "honorable", "colors", "center", "defense", "recognize", "organized", "realize", "neighbors", "maneuver", "{era:honour|honor}" })
            Assert.Contains(Check($"{{self.name}} spoke of {word}."), i => i.Message.Contains("American"));
        foreach (var word in new[] { "favour", "honour", "honourable", "colours", "centre", "defence", "recognise", "organised", "realise", "prize", "seized", "the assizes", "size", "distress" })
            Assert.Empty(Check($"{{self.name}} spoke of {word}."));
    }

    [Fact]
    public void Nothing_in_the_content_outside_the_events_carries_a_slip()
    {
        var issues = ContentValidator.ValidateFiles(ContentLocator.FindContentDir());
        Assert.True(issues.Count == 0, "slips in the catalogs:\n" + string.Join("\n", issues));
    }

    [Fact]
    public void A_natural_death_in_old_age_is_said_so_and_a_death_by_a_hand_is_not_called_age()
    {
        var w = Family();
        DynastySystem.Die(w, "bastien");          // a kill effect, at 64: nothing is known of how, so nothing is said
        Assert.Equal("", w.Deaths.Single(d => d.PersonId == "bastien").How);
        Assert.DoesNotContain(w.Deaths, d => d.How == "of age");
    }

    /// <summary>The strings of the screens: every literal of the code that builds what the player reads (the projections in Core and
    /// the web client), and the words between the tags of the client's pages. A literal with no space is a key, an id or a style.</summary>
    private static IEnumerable<(string where, string text)> ScreenStrings()
    {
        string root = Path.GetFullPath(Path.Combine(ContentLocator.FindContentDir(), "..")); char sep = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "Core", "Presentation"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "src", "Web"), "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".cs") || f.EndsWith(".razor")))
            .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep));
        var literal = new Regex("\"(?:[^\"\\\\\\n]|\\\\.)*\""); var between = new Regex(">([^<>@{}\"]*[A-Za-z][^<>@{}\"]*)<");
        foreach (var f in files)
        {
            bool style = false; int n = 0;
            foreach (var raw in File.ReadLines(f))
            {
                n++;
                if (raw.Contains("<style")) style = true;
                if (style) { if (raw.Contains("</style>")) style = false; continue; }   // a stylesheet is not read by a player
                string line = Regex.Replace(raw, "(^|[\\s;{}])//.*$", "$1");          // nor is a comment
                if (line.TrimStart().StartsWith("///") || line.TrimStart().StartsWith("@*")) continue;
                foreach (Match m in literal.Matches(line)) if (m.Value.Contains(' ')) yield return ($"{Path.GetFileName(f)}:{n}", m.Value.Trim('"'));
                if (f.EndsWith(".razor")) foreach (Match m in between.Matches(line)) yield return ($"{Path.GetFileName(f)}:{n}", m.Groups[1].Value);
            }
        }
    }

    [Fact]
    public void Nothing_the_screens_write_carries_a_slip()
    {
        var bad = new List<string>();
        foreach (var (where, text) in ScreenStrings())
        {
            // code in the string is not read as words: an interpolation's hole, a razor expression, a member (p.Stress, cls.Defense),
            // an identifier (titleSize, GiftSizes); nor is a style (stop-color, align-items: center)
            string words = text;
            for (int i = 0; i < 3; i++) words = Regex.Replace(words, "\\{[^{}]*\\}", " ");
            words = Regex.Replace(words, "@\\([^)]*\\)?|@[\\w.]+|\\b\\w+(\\.\\w+)+\\b|\\b[a-z]+[A-Z]\\w*\\b|\\b[A-Z][a-z]+[A-Z]\\w*\\b", " ");
            words = Regex.Replace(words, "[\\w-]+-(color|center)|(color|center)(-[\\w-]+|\\s*:)|\\b(text-anchor|align|justify|place)[\\w-]*:\\s*\\w+", " ", RegexOptions.IgnoreCase);
            foreach (var slip in ContentValidator.Slips(words)) bad.Add($"{where}: {slip}: {text}");
        }
        Assert.True(bad.Count == 0, "slips in the strings of the screens:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void No_death_in_the_core_is_given_as_of_age()
    {
        string core = Path.GetFullPath(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Core"));
        var bad = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f).Select((l, i) => (f, i, l)))
            .Where(x => !x.l.TrimStart().StartsWith("//") && x.l.Contains("\"of age\"")).Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}").ToList();
        Assert.True(bad.Count == 0, "a death given as \"of age\" (say \"of old age\"): " + string.Join(", ", bad));
    }
}
