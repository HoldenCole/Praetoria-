using Praetoria.Core.State;

namespace Praetoria.Core.Events;

// The concrete predicate vocabulary (BuildSpec §4). Each maps to one JSON "type".
// Kept deliberately small but expressive enough to author the Academy Crucible and prove
// gate-chaining. New predicates are added here + in ConditionParser — no engine changes.

/// <summary>Logical AND. JSON: { "type": "all", "of": [ ... ] }. Empty ⇒ true.</summary>
public sealed class AllCondition : ICondition
{
    public IReadOnlyList<ICondition> Of { get; }
    public AllCondition(IReadOnlyList<ICondition> of) => Of = of;
    public bool Evaluate(EvalContext ctx)
    {
        foreach (var c in Of) if (!c.Evaluate(ctx)) return false;
        return true;
    }
}

/// <summary>Logical OR. JSON: { "type": "any", "of": [ ... ] }. Empty ⇒ false.</summary>
public sealed class AnyCondition : ICondition
{
    public IReadOnlyList<ICondition> Of { get; }
    public AnyCondition(IReadOnlyList<ICondition> of) => Of = of;
    public bool Evaluate(EvalContext ctx)
    {
        foreach (var c in Of) if (c.Evaluate(ctx)) return true;
        return false;
    }
}

/// <summary>Negation. JSON: { "type": "not", "of": { ... } }.</summary>
public sealed class NotCondition : ICondition
{
    public ICondition Of { get; }
    public NotCondition(ICondition of) => Of = of;
    public bool Evaluate(EvalContext ctx) => !Of.Evaluate(ctx);
}

/// <summary>World flag set? JSON: { "type": "worldFlag", "flag": "x", "value": true }.</summary>
public sealed class WorldFlagCondition : ICondition
{
    public string Flag { get; }
    public bool Value { get; }
    public WorldFlagCondition(string flag, bool value) { Flag = flag; Value = value; }
    public bool Evaluate(EvalContext ctx) => ctx.World.HasFlag(Flag) == Value;
}

/// <summary>Per-character flag set? JSON: { "type": "charFlag", "role": "rival", "flag": "feud", "value": true }.</summary>
public sealed class CharFlagCondition : ICondition
{
    public string Role { get; }
    public string Flag { get; }
    public bool Value { get; }
    public CharFlagCondition(string role, string flag, bool value) { Role = role; Flag = flag; Value = value; }
    public bool Evaluate(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return false;
        return c.Flags.Contains(Flag) == Value;
    }
}

/// <summary>Directed-disposition compare. JSON: { "type": "relationship", "from": "self", "to": "rival", "op": "lt", "value": 0 }.</summary>
public sealed class RelationshipCondition : ICondition
{
    public string From { get; }
    public string To { get; }
    public CompareOp Op { get; }
    public int Value { get; }
    public RelationshipCondition(string from, string to, CompareOp op, int value) { From = from; To = to; Op = op; Value = value; }
    public bool Evaluate(EvalContext ctx)
    {
        if (!ctx.Binding.TryGet(From, out var f) || !ctx.Binding.TryGet(To, out var t)) return false;
        var rel = ctx.World.GetRelationship(f, t);
        return Comparison.Apply(Op, rel?.Disposition ?? 0, Value);
    }
}

/// <summary>Formal bond present? JSON: { "type": "bond", "from": "self", "to": "ally", "bond": "sworn", "present": true }.</summary>
public sealed class BondCondition : ICondition
{
    public string From { get; }
    public string To { get; }
    public BondType Bond { get; }
    public bool Present { get; }
    public BondCondition(string from, string to, BondType bond, bool present) { From = from; To = to; Bond = bond; Present = present; }
    public bool Evaluate(EvalContext ctx)
    {
        if (!ctx.Binding.TryGet(From, out var f) || !ctx.Binding.TryGet(To, out var t)) return false;
        var rel = ctx.World.GetRelationship(f, t);
        bool has = rel != null && rel.Bond == Bond;
        return has == Present;
    }
}

/// <summary>Skill compare. JSON: { "type": "skill", "role": "self", "skill": "tactics", "op": "gte", "value": 3 }.</summary>
public sealed class SkillCondition : ICondition
{
    public string Role { get; }
    public string Skill { get; }
    public CompareOp Op { get; }
    public int Value { get; }
    public SkillCondition(string role, string skill, CompareOp op, int value) { Role = role; Skill = skill; Op = op; Value = value; }
    public bool Evaluate(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return false;
        return Comparison.Apply(Op, c.Skill(Skill), Value);
    }
}

/// <summary>Trait present? JSON: { "type": "trait", "role": "self", "trait": "Ruthless", "kind": "nature", "present": true }.</summary>
public sealed class TraitCondition : ICondition
{
    public string Role { get; }
    public string Trait { get; }
    public string Kind { get; } // "nature" | "aptitude" | "any"
    public bool Present { get; }
    public TraitCondition(string role, string trait, string kind, bool present) { Role = role; Trait = trait; Kind = kind; Present = present; }
    public bool Evaluate(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return false;
        bool has = Kind switch
        {
            "nature" => c.NatureTraits.Contains(Trait),
            "aptitude" => c.AptitudeTraits.Contains(Trait),
            _ => c.NatureTraits.Contains(Trait) || c.AptitudeTraits.Contains(Trait)
        };
        return has == Present;
    }
}

/// <summary>Career-rank compare. JSON: { "type": "rank", "role": "rival", "op": "gt", "value": 0 }. Compares to a literal, or to another role via "vsRole".</summary>
public sealed class RankCondition : ICondition
{
    public string Role { get; }
    public CompareOp Op { get; }
    public int Value { get; }
    public string? VsRole { get; }
    public RankCondition(string role, CompareOp op, int value, string? vsRole) { Role = role; Op = op; Value = value; VsRole = vsRole; }
    public bool Evaluate(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return false;
        int target = Value;
        if (VsRole != null)
        {
            var other = ctx.Actor(VsRole);
            if (other == null) return false;
            target = other.CareerRank;
        }
        return Comparison.Apply(Op, c.CareerRank, target);
    }
}

/// <summary>Turn-counter compare. JSON: { "type": "turn", "op": "gte", "value": 3 }.</summary>
public sealed class TurnCondition : ICondition
{
    public CompareOp Op { get; }
    public int Value { get; }
    public TurnCondition(CompareOp op, int value) { Op = op; Value = value; }
    public bool Evaluate(EvalContext ctx) => Comparison.Apply(Op, ctx.World.Turn, Value);
}

/// <summary>World-counter compare. JSON: { "type": "counter", "key": "unrest", "op": "gte", "value": 2 }.</summary>
public sealed class CounterCondition : ICondition
{
    public string Key { get; }
    public CompareOp Op { get; }
    public int Value { get; }
    public CounterCondition(string key, CompareOp op, int value) { Key = key; Op = op; Value = value; }
    public bool Evaluate(EvalContext ctx) => Comparison.Apply(Op, ctx.World.Counter(Key), Value);
}

/// <summary>Has an event already fired this run? JSON: { "type": "eventFired", "event": "id", "value": true }.</summary>
public sealed class EventFiredCondition : ICondition
{
    public string EventId { get; }
    public bool Value { get; }
    public EventFiredCondition(string eventId, bool value) { EventId = eventId; Value = value; }
    public bool Evaluate(EvalContext ctx) => ctx.World.HasFlag(EventFlags.Fired(EventId)) == Value;
}

/// <summary>Always-true / always-false literal. JSON: { "type": "const", "value": true }.</summary>
public sealed class ConstCondition : ICondition
{
    public bool Value { get; }
    public ConstCondition(bool value) => Value = value;
    public bool Evaluate(EvalContext ctx) => Value;
}
