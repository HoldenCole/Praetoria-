using System.Text.Json;

namespace Praetoria.Core.Data;

/// <summary>A subject read at the Academy: the skill it trains and the service it leads to.</summary>
public sealed record AcademySubject(string Id, string Name, string Skill, string Track, string Description);
/// <summary>A Standard of the Institute (imperium register): the faction a cadet is sorted into.</summary>
public sealed record AcademyStandard(string Id, string Name, string Accent, string Motto);
/// <summary>A lesson of the tutorial (docs/ACADEMY.md): the term it is taught in, a one-line brief, and the full paragraphs for a first run.
/// <see cref="ImperiumFull"/> replaces <see cref="Full"/> under the Imperium when it is not empty.</summary>
public sealed record AcademyLesson(string Id, int Term, string Title, string Brief, IReadOnlyList<string> Full, IReadOnlyList<string> ImperiumFull, string ImperiumTitle = "", string ImperiumBrief = "");
/// <summary>How one register names the school and its terms.</summary>
public sealed record AcademyRegister(string Name, string CadetWord, IReadOnlyList<string> Terms, string Blurb);

/// <summary>The Academy's content (content/academy/academy.json): the two registers, the subjects, the Standards, the lessons.</summary>
public sealed class AcademyCatalog
{
    public AcademyRegister Republic { get; init; } = new("The Academy", "cadet", Array.Empty<string>(), "");
    public AcademyRegister Imperium { get; init; } = new("The Institute", "cadet", Array.Empty<string>(), "");
    public IReadOnlyList<AcademySubject> Subjects { get; init; } = Array.Empty<AcademySubject>();
    public IReadOnlyList<AcademyStandard> Standards { get; init; } = Array.Empty<AcademyStandard>();
    public IReadOnlyList<AcademyLesson> Lessons { get; init; } = Array.Empty<AcademyLesson>();
    public static AcademyCatalog Empty { get; } = new();
    public bool IsEmpty => Subjects.Count == 0;
    public AcademyRegister Register(string register) => register == "imperium" ? Imperium : Republic;
    public AcademySubject? Subject(string id) => Subjects.FirstOrDefault(s => s.Id == id);
    public AcademyStandard? Standard(string id) => Standards.FirstOrDefault(s => s.Id == id);

    public static AcademyCatalog LoadFromDirectory(string contentRoot)
    {
        var file = Path.Combine(contentRoot, "academy", "academy.json"); if (!File.Exists(file)) return Empty;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        static List<string> Strings(JsonElement el, string name) =>
            el.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList() : new List<string>();
        static AcademyRegister Reg(JsonElement root, string name, string fallback) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Object
                ? new AcademyRegister(ConditionParser.OptStr(el, "name", fallback), ConditionParser.OptStr(el, "cadet", "cadet"), Strings(el, "terms"), ConditionParser.OptStr(el, "blurb", ""))
                : new AcademyRegister(fallback, "cadet", Array.Empty<string>(), "");
        IEnumerable<JsonElement> Arr(string name) => root.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object) : Enumerable.Empty<JsonElement>();
        return new AcademyCatalog
        {
            Republic = Reg(root, "republic", "The Academy"), Imperium = Reg(root, "imperium", "The Institute"),
            Subjects = Arr("subjects").Select(el => new AcademySubject(ConditionParser.Str(el, "id"), ConditionParser.Str(el, "name"), ConditionParser.Str(el, "skill"), ConditionParser.Str(el, "track"), ConditionParser.OptStr(el, "description", ""))).ToList(),
            Standards = Arr("standards").Select(el => new AcademyStandard(ConditionParser.Str(el, "id"), ConditionParser.Str(el, "name"), ConditionParser.OptStr(el, "accent", "#C9D1DC"), ConditionParser.OptStr(el, "motto", ""))).ToList(),
            Lessons = Arr("lessons").Select(el => new AcademyLesson(ConditionParser.Str(el, "id"), ConditionParser.Int(el, "term", 1), ConditionParser.Str(el, "title"), ConditionParser.OptStr(el, "brief", ""), Strings(el, "full"), Strings(el, "imperiumFull"), ConditionParser.OptStr(el, "imperiumTitle", ""), ConditionParser.OptStr(el, "imperiumBrief", ""))).ToList()
        };
    }
}
