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
    public const string EnvoyReport = "the_envoy_of_peace";
    /// <param name="binding">Who the report is about, when the caller has it: a report that names one of the house's dead (a role
    /// bound by <see cref="BuriedCondition"/>) says what was said over them.</param>
    public static string For(World w, EventDef def, ContentDatabase content, Binding? binding = null)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null) return "";
        if (binding != null && SaidOver(w, def, binding) is { Length: > 0 } eulogy) return eulogy;
        var flags = def.Conditions.OfType<WorldFlagCondition>().Where(c => c.Value).Select(c => c.Flag).ToHashSet();
        string Nm(string id) => w.House(id)?.Name ?? id;
        if (def.Id == EnvoyReport)
        {
            /* an envoy's peace: how the war stands and what each answer would bring, by the terms the Military window lists */
            var envoy = w.LivingCharacters().FirstOrDefault(c => c.Flags.Contains(FleetSystem.SuingForPeaceFlag) && w.AtWar(mine, c.HouseId)); if (envoy == null) return "";
            var war = w.Wars.FirstOrDefault(x => x.Between(mine, envoy.HouseId)); if (war == null) return "";
            var rules = content.Fleets; var terms = HousePeace.Terms(w, new FleetSystem(rules), rules, mine, envoy.HouseId);
            int lead = war.Lead(mine);
            string stands = $"The war with {Nm(envoy.HouseId)}, begun in year {war.Since}: {(lead > 0 ? $"you are {lead} ahead" : lead < 0 ? $"you are {-lead} behind" : "neither side is ahead")} by the score (a battle won is 2, a world taken 3), with {Counted.Hulls(FleetSystem.StrengthOf(w, mine))} to their {FleetSystem.StrengthOf(w, envoy.HouseId)}.";
            var asks = terms.Where(t => t.Kind != HousePeace.White).Select(t => t.Accepted ? $"{t.Label}: {t.What}" : $"{t.Label}: not to be had ({t.Why}).").ToList();
            return stands + (asks.Count > 0 ? " What may be asked: " + string.Join(" ", asks) : " Nothing may be asked beyond the peace itself.") + " While the envoy waits, the same terms are under Wars in the Military window.";
        }
        if (flags.Contains("case_won") || flags.Contains("case_lost") || flags.Contains(LadderSystem.SuitWonFlag) || flags.Contains(LadderSystem.SuitLostFlag))
        {
            bool won = flags.Contains("case_won") || flags.Contains(LadderSystem.SuitWonFlag);
            var j = w.Judgments.LastOrDefault(x => x.Kind == "suit" && (won ? x.WinnerId == mine : x.LoserId == mine));
            return j == null ? "" : $"The case: {Nm(j.WinnerId)} against {Nm(j.LoserId)}{(j.Matter.Length > 0 ? " over " + j.Matter : "")}, year {j.Turn}. {(j.Judge.Length > 0 ? j.Judge + ", praetor, " + j.How : char.ToUpperInvariant(j.How[0]) + j.How[1..])}. {Nm(j.LoserId)} paid {Money.Credits(j.Sum)}.";
        }
        if (flags.Contains("battle_won") || flags.Contains("battle_lost"))
        {
            bool won = flags.Contains("battle_won");
            var b = w.Battles.LastOrDefault(x => x.Involves(mine) && (x.WinnerId == mine) == won);
            if (b == null) return "";
            bool att = b.AttackerId == mine;
            return $"The battle of {b.Place}, year {b.Turn}: your {Counted.Hulls((att ? b.AttackerHulls : b.DefenderHulls))} against {(att ? b.DefenderHulls : b.AttackerHulls)} of {Nm(att ? b.DefenderId : b.AttackerId)}. You lost {(att ? b.AttackerLost : b.DefenderLost)}, they lost {(att ? b.DefenderLost : b.AttackerLost)}. The full account is under Military, Battles.";
        }
        if (flags.Contains(LadderSystem.ElectedFlag) || flags.Contains(LadderSystem.KinElectedFlag) || flags.Contains(LadderSystem.LostFlag))
        {
            /* the count for every office somebody of the house stood for: the office, the year, each name and its votes */
            var rungs = w.Counts.Where(x => x.HouseId == mine).Select(x => x.RungId).Distinct().ToList(); if (rungs.Count == 0) return "";
            var said = new List<string>();
            foreach (var r in rungs)
            {
                var row = w.Counts.Where(x => x.RungId == r).OrderByDescending(x => x.Votes).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
                said.Add($"For {row[0].Rung.ToLowerInvariant()}: " + string.Join(", ", row.Take(6).Select(x => $"{x.Name}{(x.HouseId == mine ? " (yours)" : " of " + Nm(x.HouseId))} {x.Votes}{(x.Won ? ", elected" : "")}")) + (row.Count > 6 ? $", and {row.Count - 6} more" : ""));
            }
            return $"The count, year {w.Counts[0].Turn}. " + string.Join(". ", said) + ".";
        }
        if (flags.Contains(LadderSystem.ExposedFlag) || flags.Contains(LadderSystem.KinExposedFlag) || flags.Contains(LadderSystem.ChargeAdmittedFlag))
        {
            /* which offence it was: the charge a praetor found proved, or the money an audit followed (docs/PLAYTHROUGH.md, Easy, P101) */
            var j = w.Judgments.LastOrDefault(x => x.Kind == "charge" && x.LoserId == mine && x.Turn >= w.Turn - 1);
            if (j != null) return $"The charge, year {j.Turn}: {(j.Matter.Length > 0 ? j.Matter : j.How)}. {(j.Judge.Length > 0 ? j.Judge + ", praetor, heard it. " : "")}{Nm(j.WinnerId)} brought it{(j.Sum > 0 ? $", and {Money.Credits(j.Sum)} were paid" : "")}.";
            string me = Nm(mine);
            return w.History.LastOrDefault(h => h.Turn >= w.Turn - 1 && h.Text.StartsWith("It comes out that") && h.Text.Contains(me))?.Text ?? "";
        }
        if (flags.Contains(FleetSystem.ConquestMadeFlag))
        {
            var taken = w.HoldingsOf(mine).Where(h => w.WorldCounters.ContainsKey(FleetSystem.ConqueredKey(h.Id))).OrderByDescending(h => w.Counter(FleetSystem.ConqueredKey(h.Id))).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            return taken == null ? "" : $"The world: {taken.Name}, taken in year {w.Counter(FleetSystem.ConqueredKey(taken.Id))}{(taken.FormerOwnerId.Length > 0 ? " from " + Nm(taken.FormerOwnerId) : "")}. Its unrest stands at {taken.Unrest}, and {taken.Population} live there.";
        }
        if (flags.Contains(FleetSystem.DeclaredOnMeFlag))
        {
            /* who, with what, why, and what they would take to stop: the terms are the ones the Military window offers */
            var war = w.Wars.LastOrDefault(x => x.HouseB == mine && x.Since >= w.Turn - 2); if (war == null) return "";
            var fleets = new FleetSystem(content.Fleets); string foe = war.HouseA; int theirs = FleetSystem.StrengthOf(w, foe), ours = FleetSystem.StrengthOf(w, mine);
            var terms = HousePeace.Terms(w, fleets, content.Fleets, mine, foe).Where(t => t.Accepted).Select(t => t.Label.ToLowerInvariant()).ToList();
            return $"{Nm(foe)} declared war on your house in year {war.Since}: its {Counted.Hulls(theirs)} to your {ours}."
                + (war.Why.Length > 0 ? $" Why: {war.Why}." : "")
                + (terms.Count > 0 ? $" If you sued for peace today it would take: {string.Join(", or ", terms)}." : " It would take no peace you could offer today: the war must turn first.")
                + " The war and its terms are under Military, on the deck.";
        }
        if (flags.Contains(LadderSystem.AbsentFlag))
            return "The seat is used by taking any one of the year's duties under Senate, Your duties, before the year ends. Three years running without one brings this again, and each time costs the house 2 legitimacy.";
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

    /// <summary>What was said over the dead a report names (docs/LIFE.md, "A funeral"): the eulogy, made from what they did.</summary>
    private static string SaidOver(World w, EventDef def, Binding binding)
    {
        foreach (var role in def.Roles.Where(r => r.Constraints.Any(k => k is BuriedCondition b && b.Role == r.Name)))
            if (binding.TryGet(role.Name, out var id) && w.Funerals.LastOrDefault(f => f.PersonId == id) is { Lines.Count: > 0 } f)
                return "What was said at the graveside: " + string.Join(" ", f.Lines);
        return "";
    }
}
