namespace Praetoria.Core.Events;

/// <summary>
/// A predicate over State (BuildSpec §4). Conditions are authored as structured JSON and parsed
/// into these objects, so event *logic* stays data-driven (GDD §15 — logic/text split, mod-ready).
/// Used for event trigger conditions, role-binding constraints, and per-choice requirements.
/// </summary>
public interface ICondition
{
    bool Evaluate(EvalContext ctx);
}
