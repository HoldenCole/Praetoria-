using Praetoria.Core.Events;
using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>
/// What a choice on a report will cost and give, and what a greyed one lacks, in words (docs/PLAYTHROUGH.md 8: every one of five
/// players said a report's choices were blind: no cost before, "requirements not met" with no requirement, no outcome after).
/// Read only. It describes what touches the player and the player's house; what a choice does to others stays in the story.
/// </summary>
public static class ChoiceStakes
{
    private static string Signed(int n) => (n > 0 ? "+" : "−") + Math.Abs(n);
    private static string Op(CompareOp op) => op.ToString().ToLowerInvariant() switch { "gte" or "ge" => "at least", "gt" => "more than", "lte" or "le" => "at most", "lt" => "less than", "eq" => "exactly", "neq" or "ne" => "not", _ => "at least" };

    /// <summary>What the choice does to the player's house and person, as far as its effects say: "−5 materials · −2 credits · legitimacy +2".</summary>
    public static string Of(Choice choice, EvalContext ctx, Character player)
    {
        var parts = new List<string>(); var sums = new Dictionary<string, int>(); int legit = 0, stress = 0;
        bool Mine(string role) => ctx.Actor(role) is { } a && a.HouseId == player.HouseId;
        bool Me(string role) => ctx.Actor(role)?.Id == player.Id;
        foreach (var e in choice.Effects)
            switch (e)
            {
                case AdjustResourceEffect r when Mine(r.Role): sums[r.ResourceKey] = sums.GetValueOrDefault(r.ResourceKey) + r.Delta; break;
                case AdjustLegitimacyEffect l when Mine(l.Role): legit += l.Delta; break;
                case AdjustStressEffect s when Me(s.Role): stress += s.Delta; break;
                case AdjustSkillEffect k when Me(k.Role) && k.Delta != 0: parts.Add(k.Skill.Replace('_', ' ') + " " + Signed(k.Delta)); break;
                case AdjustRelationshipEffect a when ctx.Binding.TryGet(a.To, out var to) && to == player.Id && ctx.Binding.TryGet(a.From, out var from) && from != player.Id && ctx.World.Char(from) is { } who && a.Delta != 0:
                    parts.Add(who.Name + " thinks " + (a.Delta > 0 ? "better" : "worse") + " of you (" + Signed(a.Delta) + ")"); break;
                case AdvanceCareerEffect c when Me(c.Role): parts.Add("a step up in the service"); break;
                case DeclareWarEffect: parts.Add("war"); break;
                case MakePeaceEffect: parts.Add("peace"); break;
                case GrantClaimEffect g when Mine(g.Role): parts.Add("a claim to a higher rank"); break;
            }
        var money = Resource.All.Where(k => sums.GetValueOrDefault(k) != 0).Select(k => Signed(sums[k]) + " " + Resource.Label(k)).ToList();
        if (legit != 0) money.Add("legitimacy " + Signed(legit));
        if (stress != 0) money.Add("stress " + Signed(stress));
        return string.Join(" · ", money.Concat(parts));
    }

    /// <summary>What the player's treasury would be short of if this choice were taken, or null: a choice that takes more than the
    /// house has is said to, before it is chosen.</summary>
    public static string? Overdraws(Choice choice, EvalContext ctx, Character player)
    {
        var house = ctx.World.House(player.HouseId); if (house == null) return null; int credits = 0;
        foreach (var e in choice.Effects) if (e is AdjustResourceEffect r && r.ResourceKey == Resource.Credits && ctx.Actor(r.Role) is { } a && a.HouseId == player.HouseId) credits += r.Delta;
        return credits < 0 && house.Treasury.Credits + credits < 0 ? $"this takes {-credits} credits and the treasury holds {house.Treasury.Credits}: it would leave the house {-(house.Treasury.Credits + credits)} below nought" : null;
    }

    /// <summary>What a greyed choice lacks, in words: every requirement that does not hold, said plainly.</summary>
    public static string Unmet(Choice choice, EvalContext ctx)
    {
        var lacks = new List<string>();
        foreach (var c in choice.Requirements) if (!c.Evaluate(ctx)) lacks.Add(Say(c, ctx, true));
        lacks = lacks.Where(x => x.Length > 0).Distinct().ToList();
        return lacks.Count == 0 ? "not open to you as things stand" : string.Join("; ", lacks);
    }
    private static string Who(EvalContext ctx, string role) => role is "self" or "" ? "you" : ctx.Actor(role)?.Name ?? "they";
    private static string Say(ICondition c, EvalContext ctx, bool want)
    {
        switch (c)
        {
            case AllCondition all: return string.Join("; ", all.Of.Where(x => x.Evaluate(ctx) != want).Select(x => Say(x, ctx, want)).Where(x => x.Length > 0));
            case AnyCondition any: return want ? "one of: " + string.Join(", or ", any.Of.Select(x => Say(x, ctx, true)).Where(x => x.Length > 0)) : string.Join("; ", any.Of.Where(x => x.Evaluate(ctx)).Select(x => Say(x, ctx, false)).Where(x => x.Length > 0));
            case NotCondition not: return Say(not.Of, ctx, !want);
            case SkillCondition s: { var a = ctx.Actor(s.Role); return want ? $"needs {s.Skill.Replace('_', ' ')} {Op(s.Op)} {s.Value} ({Who(ctx, s.Role)} {(a == null ? "have none" : "have " + a.Skill(s.Skill))})" : $"not for one with {s.Skill.Replace('_', ' ')} {Op(s.Op)} {s.Value}"; }
            case ResourceCondition r: { var a = ctx.Actor(r.Role); var h = a != null ? ctx.World.House(a.HouseId) : null; return want ? $"needs {Op(r.Op)} {r.Value} {Resource.Label(r.ResourceKey)} in the treasury (it holds {h?.Treasury.Get(r.ResourceKey) ?? 0})" : $"only with fewer than {r.Value} {Resource.Label(r.ResourceKey)}"; }
            case TraitCondition t: return (want == t.Present ? "needs one who is " : "not for one who is ") + t.Trait.Replace('_', ' ');
            case RankCondition k: return want ? (k.VsRole != null ? "needs a higher rank in the service than " + Who(ctx, k.VsRole) : $"needs rank {Op(k.Op)} {k.Value} in the service") : "not at your rank in the service";
            case RelationshipCondition rel: { ctx.Binding.TryGet(rel.From, out var f); ctx.Binding.TryGet(rel.To, out var to); int now = f != null && to != null ? ctx.World.GetRelationship(f, to)?.Disposition ?? 0 : 0; string who = ctx.World.Char(f ?? "")?.Name ?? "they"; return want ? $"needs {who} to think {Op(rel.Op)} {rel.Value} of {(to == ctx.World.ProtagonistId ? "you" : ctx.World.Char(to ?? "")?.Name ?? "them")} (now {now})" : $"not while {who} thinks so of you"; }
            case WealthCondition wl: return want ? $"needs a house {Op(wl.Op)} {wl.Tier.Replace('_', ' ')} in wealth" : "not for a house so rich";
            case TitleCondition tt: return (want == tt.Present ? "needs the rank of " : "not for a ") + Progression.Title.StyleOf(tt.TitleId, ctx.World.Era);
            case ClaimCondition cl: return want == cl.Present ? "needs a claim to the rank of " + Progression.Title.StyleOf(cl.TitleId, ctx.World.Era) : "not while the house holds that claim";
            case AgeCondition ag: return want ? $"needs an age {Op(ag.Op)} {ag.Value}" : "not at that age";
            case FleetStrengthCondition fl: { var a = ctx.Actor(fl.Role); int have = a != null ? Systems.FleetSystem.StrengthOf(ctx.World, a.HouseId) : 0; return want ? $"needs {Op(fl.Op)} {fl.Value} hull(s) (the house has {have})" : "not with a fleet of that size"; }
            case AtWarCondition aw: return want == aw.Present ? "only in a war" : "not while at war";
            case BondCondition: return want ? "needs a tie between the two that is not there" : "not between two so tied";
            case TrackCondition tr: return want == tr.Present ? "needs a career in " + tr.Track.Replace('_', ' ') : "not for one in " + tr.Track.Replace('_', ' ');
            case EraCondition er: return want == er.Present ? "only under the " + er.Era : "not under the " + er.Era;
            case DepositCondition dp: return want == dp.Present ? "needs " + Resource.A(dp.ResourceKey) + " deposit on a world of the house" : "";
            case SphereCondition sp: return want ? $"needs standing {Op(sp.Op)} {sp.Value} with {sp.SphereKey.Replace('_', ' ')}" : "";
            case BlocCondition bl: return want == bl.Present ? "needs a seat with " + bl.Bloc.Replace('_', ' ') : "not for one of " + bl.Bloc.Replace('_', ' ');
            default: return "";   // a flag, a counter, a turn: something that happened or did not, which the story has told
        }
    }
}
