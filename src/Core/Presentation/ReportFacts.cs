using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>
/// The facts behind a report that is raised by something that happened: which battle, which ruling and for how much, which
/// office, which world (docs/PLAYTHROUGH.md, reports: "a report names its subject"). The prose of a card is written once for every
/// occasion; this is the line under it that says which occasion it was. Read only. Empty when the report is not of that kind.
/// </summary>
public static class ReportFacts
{
    public static string For(World w, EventDef def, ContentDatabase content)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null) return "";
        var flags = def.Conditions.OfType<WorldFlagCondition>().Where(c => c.Value).Select(c => c.Flag).ToHashSet();
        string Nm(string id) => w.House(id)?.Name ?? id;
        if (flags.Contains("case_won") || flags.Contains("case_lost"))
        {
            bool won = flags.Contains("case_won");
            var j = w.Judgments.LastOrDefault(x => x.Kind == "suit" && (won ? x.WinnerId == mine : x.LoserId == mine));
            return j == null ? "" : $"The case: {Nm(j.WinnerId)} against {Nm(j.LoserId)}{(j.Matter.Length > 0 ? " over " + j.Matter : "")}, year {j.Turn}. {(j.Judge.Length > 0 ? j.Judge + ", praetor, " + j.How : char.ToUpperInvariant(j.How[0]) + j.How[1..])}. {Nm(j.LoserId)} paid {j.Sum} credits.";
        }
        if (flags.Contains("battle_won") || flags.Contains("battle_lost"))
        {
            bool won = flags.Contains("battle_won");
            var b = w.Battles.LastOrDefault(x => x.Involves(mine) && (x.WinnerId == mine) == won);
            if (b == null) return "";
            bool att = b.AttackerId == mine;
            return $"The battle of {b.Place}, year {b.Turn}: your {(att ? b.AttackerHulls : b.DefenderHulls)} hull(s) against {(att ? b.DefenderHulls : b.AttackerHulls)} of {Nm(att ? b.DefenderId : b.AttackerId)}. You lost {(att ? b.AttackerLost : b.DefenderLost)}, they lost {(att ? b.DefenderLost : b.AttackerLost)}. The full account is under Military, Battles.";
        }
        if (flags.Contains("office_granted"))
        {
            var held = content.Offices.Defs.Where(d => OfficeSystem.Holds(w, mine, d.Id)).Select(d => d.Name).ToList();
            return held.Count == 0 ? "" : "The office: " + string.Join(" and ", held) + ". Its work is under Senate, Offices and parties.";
        }
        if (flags.Contains("won_a_world") || flags.Contains("ceded_a_world"))
        {
            string me = Nm(mine);
            return w.History.LastOrDefault(h => h.Text.StartsWith("By the terms of the peace") && h.Text.Contains(me))?.Text ?? "";
        }
        return "";
    }
}
