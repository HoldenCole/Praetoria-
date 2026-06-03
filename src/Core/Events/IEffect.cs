namespace Praetoria.Core.Events;

/// <summary>
/// A consequence — a write-back into State (BuildSpec §4 "Consequence applier", GDD §15 L3).
/// Consequences are not "+5 minerals"; they change relationships, set flags, and arm/clear
/// crisis gates. That is precisely how a choice in one event makes a *different*, unscripted
/// event eligible later (the Milestone-1 acceptance property). Authored as JSON, parsed to these.
/// </summary>
public interface IEffect
{
    void Apply(EvalContext ctx);
}
