using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// A suit that names the player's house (docs/PLAYTHROUGH.md 12: "the courts fine the player every year in silence": no notice,
/// no sum, and nothing to do about it). A case before another house's praetor that names the player's house is not decided the
/// year it is brought: it stands on the docket for a year, with who brought it, over what and for how much, and in that year the
/// head may come to terms out of court or send someone of the house to plead. Every ruling that touches the house is kept.
/// </summary>
public static partial class LadderSystem
{
    public const string SettledOut = "settled_out", Lapsed = "lapsed";
    public const int PleadInfluence = 1;
    /// <summary>An advocate this much better than the other side's turns a doubtful case; one of this craft keeps a praetor to the law.</summary>
    public const int AdvocateEdge = 2, AdvocateHolds = 8;

    public static int Craft(Character? c) => c == null ? 0 : c.Skill("law") + c.Skill("oratory");
    public static string OtherIn(OfficeBusiness b, string houseId) => b.HouseA == houseId ? b.HouseB : b.HouseA;

    /// <summary>The suits naming a house that wait to be heard.</summary>
    public static List<OfficeBusiness> SuitsNaming(World w, string houseId) =>
        w.OfficeWork.Where(b => b.Held && b.Choice.Length == 0 && b.Kind == Case && (b.HouseA == houseId || b.HouseB == houseId)).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
    public static string MatterOf(OfficeBusiness b) => MatterSays(b).Length > 0 ? MatterSays(b) : "a quarrel between the two houses";
    /// <summary>How the law stands for a house in a suit, in words.</summary>
    public static string LawFor(World w, OfficeBusiness b, string houseId) =>
        (WeightOf(b) == Plain ? "The law, read plainly, is " : WeightOf(b) == Arguable ? "The law is arguable, and leans " : "The law is doubtful. If anything it leans ") + (b.RightId == houseId ? "with you" : "against you") + ".";

    /// <summary>A case naming the player's house is brought: it waits a year, and the house is told.</summary>
    private static void HoldOver(World w, Character judge, OfficeBusiness b, string mine)
    {
        b.Held = true; var other = w.House(OtherIn(b, mine));
        w.Log($"{other?.Name ?? "A house"} brings a suit against {w.House(mine)?.Name} over {MatterOf(b)}: {StakeOf(b)} credits turn on it. {judge.Name}, praetor, hears it at the year's end. {LawFor(w, b, mine)}");
    }

    /// <summary>The suits held from last year are heard, before this year's business is made.</summary>
    private static void HearHeld(World w, OfficeCatalog offices)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var b in w.OfficeWork.Where(b => b.Held && b.Choice.Length == 0).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            var judge = w.Char(b.HolderId);
            if (judge is not { Alive: true } || mine == null) { b.Choice = Lapsed; w.Log($"The suit over {MatterOf(b)} lapses: the praetor who was to hear it is gone."); continue; }
            string choice = Inclination(w, judge, b); string other = OtherIn(b, mine); var adv = w.Char(b.AdvocateId) is { Alive: true } a && a.HouseId == mine ? a : null;
            if (adv != null)
            {
                int ours = Craft(adv), theirs = Craft(Advocate(w, other));
                if (WeightOf(b) == Doubtful && b.RightId == other && ours >= theirs + AdvocateEdge)
                { b.RightId = mine; w.Log($"{adv.Name} argues the suit over {MatterOf(b)} better than {w.House(other)?.Name}'s pleader, and where the law was doubtful it now leans to {w.House(mine)?.Name}."); LifeSystem.Note(w, adv, "office", "Pleaded for the house and turned a doubtful case."); }
                if (choice is Friend or Gift && b.RightId == mine && ours >= AdvocateHolds && BoughtWinner(w, b, judge) == null)
                { choice = Law; w.Log($"{judge.Name}, praetor, would have found another way, and with {adv.Name} pleading in open court did not dare."); }
            }
            // Decide reads the holder's waiting business: this one, since this year's has not been made yet
            Decide(w, offices, judge, choice);
        }
    }

    /// <summary>A ruling that touched the player's house is written down where the house can find it.</summary>
    private static void Judged(World w, string judge, string winner, string loser, int sum, OfficeBusiness? b, string how, string kind = "suit")
    {
        var mine = w.Protagonist?.HouseId; if (mine == null || (winner != mine && loser != mine)) return;
        w.Judgments.Add(new JudgmentRecord { Turn = w.Turn, Judge = judge, WinnerId = winner, LoserId = loser, Sum = sum, Matter = b != null ? MatterOf(b) : "", How = how, Kind = kind });
        if (w.Judgments.Count > 40) w.Judgments.RemoveRange(0, w.Judgments.Count - 40);
    }

    public static string? SettleRefusal(World w, Character? head, OfficeBusiness? b)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house comes to terms for it";
        if (b == null || !b.Held || b.Choice.Length > 0 || (b.HouseA != house.Id && b.HouseB != house.Id)) return "no such suit is waiting";
        var other = w.House(OtherIn(b, house.Id)); int half = StakeOf(b) / 2;
        if (b.RightId != house.Id) return house.Treasury.Credits >= half ? null : $"the house has {house.Treasury.Credits} credits and terms would take {half}";
        return WeightOf(b) == Doubtful ? $"the law is doubtful: {other?.Name} would rather be heard than pay" : null;
    }
    /// <summary>Come to terms before the hearing: the side the law is against pays half of what was asked, now, and the suit is over.</summary>
    public static void SettleOut(World w, Character head, OfficeBusiness b)
    {
        var house = w.House(head.HouseId)!; var other = w.House(OtherIn(b, house.Id)); var right = w.House(b.RightId); var wrong = b.RightId == house.Id ? other : house;
        int half = Math.Min(StakeOf(b) / 2, Math.Max(0, wrong?.Treasury.Credits ?? 0));
        if (wrong != null && right != null) { wrong.Treasury.Credits -= half; right.Treasury.Credits += half; }
        b.Choice = SettledOut; b.WinnerId = b.RightId; Shift(w, other, house, 4);
        w.Log($"{house.Name} and {other?.Name} come to terms over {MatterOf(b)} before it is heard. {wrong?.Name} pays {half} credits, and the suit is withdrawn.");
        Judged(w, "", b.RightId, wrong?.Id ?? "", half, b, "settled between the two houses before the hearing");
    }

    public static string? PleadRefusal(World w, Character? head, OfficeBusiness? b, Character? advocate)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of a house sends its pleader";
        if (b == null || !b.Held || b.Choice.Length > 0 || (b.HouseA != house.Id && b.HouseB != house.Id)) return "no such suit is waiting";
        if (advocate is not { Alive: true } || advocate.HouseId != house.Id || advocate.Age < DynastySystem.AgeOfMajority) return "there is no one of age in the house to send";
        return b.AdvocateId == advocate.Id ? "they are already to plead it" : null;
    }
    public static void SendPleader(World w, Character head, OfficeBusiness b, Character advocate)
    {
        b.AdvocateId = advocate.Id; var other = OtherIn(b, head.HouseId);
        w.Log($"{advocate.Name} is to plead for {w.House(head.HouseId)?.Name} in the suit over {MatterOf(b)} (law and oratory {Craft(advocate)}; {w.House(other)?.Name}'s best has {Craft(Advocate(w, other))}).");
    }
    /// <summary>What sending this advocate would do, in words, for the docket.</summary>
    public static string PleadSays(World w, OfficeBusiness b, Character advocate)
    {
        string other = OtherIn(b, advocate.HouseId); int ours = Craft(advocate), theirs = Craft(Advocate(w, other));
        string turn = WeightOf(b) == Doubtful && b.RightId == other ? (ours >= theirs + AdvocateEdge ? "would turn the doubtful law your way" : $"would need {theirs + AdvocateEdge} to turn the doubtful law (their best has {theirs})") : "";
        string hold = b.RightId == advocate.HouseId ? (ours >= AdvocateHolds ? "keeps the praetor to the law" : $"would need {AdvocateHolds} to keep a partial praetor to the law") : "";
        string says = string.Join("; ", new[] { turn, hold }.Where(x => x.Length > 0));
        return $"law and oratory {ours}" + (says.Length > 0 ? ": " + says : ": the law is plain enough that pleading will not move it");
    }
}
