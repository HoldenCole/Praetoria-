using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>What the things a head can do with a child of the house are for, said on the button (docs/PLAYTHROUGH.md, Easy: "what
/// fostering is for"). Each clause is a rule: a hidden child cannot be bound into a report, courted or reached by a knife.</summary>
public static class HouseholdWords
{
    public static string Tip(string optionId) => optionId switch
    {
        "foster" => $"Sent to be raised far from the capital under another name. While they are away no knife can find them, no feud can reach them and no report can name them; they cannot be promised in a match either. Their schooling goes on. They come home of themselves at {DynastySystem.AgeOfMajority}, or when you recall them. Costs 1 agent this year.",
        "recall" => "Brought home before their time: at your table again, and within reach of everything a child of the house is within reach of.",
        _ => "",
    };
}
