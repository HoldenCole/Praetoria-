namespace Praetoria.Core.Presentation;

/// <summary>What each skill is for, in a line a player can act on. Every clause here is a rule the engine has; when a rule
/// changes, this changes with it. Engineering is read by nothing yet (docs/GAPS.md), and says so.</summary>
public static class SkillWords
{
    public static string For(string skill) => skill switch
    {
        "tactics" => "The admiral's first skill: a fleet fights 5% harder for each point of its admiral's tactics (and 15% worse with no admiral at all). It also counts in a duel, in who a house picks to command, and in how much the Senate will let a general do.",
        "gunnery" => "A fleet fights 3% harder for each point of its admiral's gunnery. It counts in a duel and in who a house picks to command.",
        "leadership" => "A fleet fights 2% harder for each point of its admiral's leadership. It counts in who is picked to command a fleet or a levy, and it makes a marshal fit for the post.",
        "logistics" => "Makes a marshal fit for the post: a marshal of 3 or more in logistics, leadership or tactics buys a second year of helium-3 ahead when the tanks run short.",
        "discipline" => "Keeps a purse closed: a relative under 2 in discipline can lose their own coin at the tables.",
        "law" => "With oratory, the whole of a case: who the house sends to plead, whether the brief wins, and whether doubtful law falls your way in a suit.",
        "oratory" => "Two votes in an election for each point; with law, the whole of a case in court; and it brings a family round in a courtship.",
        "diplomacy" => "Brings a family round in a courtship, where the better of diplomacy and oratory is what counts.",
        "charisma" => "Two votes in an election for each point; three points of favour on the first day of a courtship for each, and more for every year of paying court; and it keeps a marriage warm.",
        "intrigue" => "Seven points in the hundred on a knife in the dark for each point; in a courtship it does a rival more harm and is less often found out.",
        "administration" => "The governor's skill: each point lifts what a world's prosperity settles at by 4, and a governor of 3 or more brings in a credit more a year. It makes a steward fit for the post, and is what a governor is picked by.",
        "economics" => "Makes a steward fit for the post (3 or more does two things a year, not one), and a head who has 2 will always see to a failing venture before it folds.",
        "engineering" => "Read at the Institute for its marks. Nothing else in the Reach turns on it yet.",
        _ => "",
    };
}
