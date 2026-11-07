using Praetoria.Core.State;

namespace Praetoria.Core.Events;

// The concrete consequence vocabulary (BuildSpec §4, GDD §15 L3). Each maps to one JSON "type".
// Crucially includes flag/gate arming and relationship writes — the levers that make a *different*
// event eligible later. Effects never invent balance numbers beyond what data specifies.

/// <summary>Set/clear a world flag. JSON: { "type": "setWorldFlag", "flag": "x", "value": true }.</summary>
public sealed class SetWorldFlagEffect : IEffect
{
    public string Flag { get; }
    public bool Value { get; }
    public SetWorldFlagEffect(string flag, bool value) { Flag = flag; Value = value; }
    public void Apply(EvalContext ctx)
    {
        if (Value) ctx.World.WorldFlags.Add(Flag);
        else ctx.World.WorldFlags.Remove(Flag);
    }
}

/// <summary>Set/clear a per-character flag (arm a personal gate). JSON: { "type": "setCharFlag", "role": "rival", "flag": "feud", "value": true }.</summary>
public sealed class SetCharFlagEffect : IEffect
{
    public string Role { get; }
    public string Flag { get; }
    public bool Value { get; }
    public SetCharFlagEffect(string role, string flag, bool value) { Role = role; Flag = flag; Value = value; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return;
        if (Value) c.Flags.Add(Flag);
        else c.Flags.Remove(Flag);
    }
}

/// <summary>Nudge directed disposition (clamped −100..100). JSON: { "type": "adjustRelationship", "from": "self", "to": "rival", "delta": -20 }.</summary>
public sealed class AdjustRelationshipEffect : IEffect
{
    public string From { get; }
    public string To { get; }
    public int Delta { get; }
    public AdjustRelationshipEffect(string from, string to, int delta) { From = from; To = to; Delta = delta; }
    public void Apply(EvalContext ctx)
    {
        if (!ctx.Binding.TryGet(From, out var f) || !ctx.Binding.TryGet(To, out var t)) return;
        var rel = ctx.World.Relationship(f, t);
        rel.Disposition = Math.Clamp(rel.Disposition + Delta, -100, 100);
    }
}

/// <summary>Forge/upgrade a formal bond. JSON: { "type": "addBond", "from": "self", "to": "ally", "bond": "sworn", "strength": 40 }.
/// Blood and sworn bonds are directed (A may swear to B without B swearing back). A <b>marriage</b> is
/// inherently mutual, so it is written on both edges — the dynasty layer (births, the <c>bond</c>
/// condition read from either side) sees it whichever spouse an event happened to bind as "from".</summary>
public sealed class AddBondEffect : IEffect
{
    public string From { get; }
    public string To { get; }
    public BondType Bond { get; }
    public int Strength { get; }
    public AddBondEffect(string from, string to, BondType bond, int strength) { From = from; To = to; Bond = bond; Strength = strength; }
    public void Apply(EvalContext ctx)
    {
        if (!ctx.Binding.TryGet(From, out var f) || !ctx.Binding.TryGet(To, out var t)) return;
        Write(ctx.World, f, t);
        if (Bond == BondType.Marriage) Write(ctx.World, t, f);
    }

    private void Write(World world, string from, string to)
    {
        var rel = world.Relationship(from, to);
        rel.Bond = Bond;
        rel.BondStrength = Math.Clamp(Strength, 0, 100);
    }
}

/// <summary>Adjust a skill. JSON: { "type": "adjustSkill", "role": "self", "skill": "tactics", "delta": 1 }.</summary>
public sealed class AdjustSkillEffect : IEffect
{
    public string Role { get; }
    public string Skill { get; }
    public int Delta { get; }
    public AdjustSkillEffect(string role, string skill, int delta) { Role = role; Skill = skill; Delta = delta; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return;
        c.Skills[Skill] = c.Skill(Skill) + Delta;
    }
}

/// <summary>Adjust stress (GDD §18, clamped 0..100). JSON: { "type": "adjustStress", "role": "self", "delta": 10 }.</summary>
public sealed class AdjustStressEffect : IEffect
{
    public string Role { get; }
    public int Delta { get; }
    public AdjustStressEffect(string role, int delta) { Role = role; Delta = delta; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return;
        c.Stress = Math.Clamp(c.Stress + Delta, 0, 100);
    }
}

/// <summary>Adjust a world counter (gate accumulator). JSON: { "type": "adjustCounter", "key": "unrest", "delta": 1 }.</summary>
public sealed class AdjustCounterEffect : IEffect
{
    public string Key { get; }
    public int Delta { get; }
    public AdjustCounterEffect(string key, int delta) { Key = key; Delta = delta; }
    public void Apply(EvalContext ctx) =>
        ctx.World.WorldCounters[Key] = ctx.World.Counter(Key) + Delta;
}

/// <summary>Grant a trait. JSON: { "type": "addTrait", "role": "self", "trait": "Cool-Headed", "kind": "aptitude" }.</summary>
public sealed class AddTraitEffect : IEffect
{
    public string Role { get; }
    public string Trait { get; }
    public string Kind { get; }
    public AddTraitEffect(string role, string trait, string kind) { Role = role; Trait = trait; Kind = kind; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return;
        if (Kind == "nature") c.NatureTraits.Add(Trait);
        else c.AptitudeTraits.Add(Trait);
    }
}

/// <summary>Advance a character's career rank by one (GDD §13), capped at the top of the track's ladder
/// when a career catalog is loaded (a Grand Admiral is not promoted again). JSON: { "type": "advanceCareer", "role": "self" }.</summary>
public sealed class AdvanceCareerEffect : IEffect
{
    public string Role { get; }
    public AdvanceCareerEffect(string role) => Role = role;
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null) return;
        int? max = ctx.Content?.Careers.MaxRank(c.CareerTrack);
        if (max == null || c.CareerRank < max) c.CareerRank += 1;
    }
}

/// <summary>Move a house-treasury resource (GDD §17). Targets the house of <c>role</c> (default
/// "self"). JSON: { "type": "adjustResource", "resource": "credits", "delta": -2, "role": "self" }.
/// Non-credit resources clamp at zero; Credits may go negative (insolvency drives consequences).</summary>
public sealed class AdjustResourceEffect : IEffect
{
    public string Role { get; }
    public string ResourceKey { get; }
    public int Delta { get; }
    public AdjustResourceEffect(string role, string resourceKey, int delta)
    {
        Role = role; ResourceKey = resourceKey; Delta = delta;
    }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c == null || !ctx.World.Houses.TryGetValue(c.HouseId, out var house)) return;
        house.Treasury.Add(ResourceKey, Delta);
        if (ResourceKey != Resource.Credits) house.Treasury.ClampNonCredit();
    }
}

/// <summary>Grant a house a claim to a title (GDD §13 Intrigue path — marriage/inheritance forges
/// the key the claim path needs). Targets the house of <c>role</c> (default "self"). JSON:
/// { "type": "grantClaim", "role": "self", "title": "count" }.</summary>
public sealed class GrantClaimEffect : IEffect
{
    public string Role { get; }
    public string TitleId { get; }
    public GrantClaimEffect(string role, string titleId) { Role = role; TitleId = titleId; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c != null && ctx.World.Houses.TryGetValue(c.HouseId, out var house))
            house.Claims.Add(TitleId);
    }
}

/// <summary>Remove a house's claim to a title (GDD §13) — the claim was pressed, sold, or extinguished.
/// Targets the house of <c>role</c> (default "self"). JSON: { "type": "revokeClaim", "role": "self", "title": "count" }.
/// Pair with <c>setTitle</c> when a claim is cashed, or a pressed claim stays pressable forever.</summary>
public sealed class RevokeClaimEffect : IEffect
{
    public string Role { get; }
    public string TitleId { get; }
    public RevokeClaimEffect(string role, string titleId) { Role = role; TitleId = titleId; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c != null && ctx.World.Houses.TryGetValue(c.HouseId, out var house))
            house.Claims.Remove(TitleId);
    }
}

/// <summary>Hand the seat to another living character while the current head still lives (GDD §14 —
/// abdication, a regency ending, a forced retirement). The role becomes the protagonist with a player-scale
/// action economy; the House and its title are untouched. Death-driven succession is the dynasty system's
/// job; this is the authored, voluntary path. JSON: { "type": "succeed", "role": "heir" }.</summary>
public sealed class SucceedEffect : IEffect
{
    public string Role { get; }
    public SucceedEffect(string role) => Role = role;
    public void Apply(EvalContext ctx)
    {
        var heir = ctx.Actor(Role);
        if (heir is not { Alive: true } || heir.Id == ctx.World.ProtagonistId) return;
        Systems.DynastySystem.HandOver(ctx.World, heir);
    }
}

/// <summary>Adjust a house's legitimacy/standing (GDD §13, clamped ≥ 0). Targets the house of
/// <c>role</c> (default "self"). JSON: { "type": "adjustLegitimacy", "role": "self", "delta": 10 }.
/// A clean marriage raises it; a scandal lowers it. The soft-lock reads it against the title each turn.</summary>
public sealed class AdjustLegitimacyEffect : IEffect
{
    public string Role { get; }
    public int Delta { get; }
    public AdjustLegitimacyEffect(string role, int delta) { Role = role; Delta = delta; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c != null && ctx.World.Houses.TryGetValue(c.HouseId, out var house))
            house.Legitimacy = Math.Max(0, house.Legitimacy + Delta);
    }
}

/// <summary>Set a house's title outright (GDD §13) — the event-driven grant / usurpation / abdication.
/// Targets the house of <c>role</c> (default "self"). JSON: { "type": "setTitle", "role": "self", "title": "count" }.
/// Atomic: it only changes the title. Legitimacy is untouched (compose adjustLegitimacy), so a usurped
/// title above the house's standing will breed soft-lock instability on the next turn.</summary>
public sealed class SetTitleEffect : IEffect
{
    public string Role { get; }
    public string TitleId { get; }
    public SetTitleEffect(string role, string titleId) { Role = role; TitleId = titleId; }
    public void Apply(EvalContext ctx)
    {
        var c = ctx.Actor(Role);
        if (c != null && ctx.World.Houses.TryGetValue(c.HouseId, out var house))
            house.Title = TitleId;
    }
}

/// <summary>Kill a character (GDD §14 — assassination, duel-to-the-death, Bloody Event). If the
/// role resolves to the protagonist, the dynasty succession fires (heir takes the seat, or the
/// dynasty ends). JSON: { "type": "kill", "role": "rival" }.</summary>
public sealed class KillEffect : IEffect
{
    public string Role { get; }
    public KillEffect(string role) => Role = role;
    public void Apply(EvalContext ctx)
    {
        if (ctx.Binding.TryGet(Role, out var id))
            Systems.DynastySystem.Die(ctx.World, id);
    }
}

/// <summary>Move self's house into a Great-Crisis bloc ("hegemon" | "alliance") — the player's side in the war of the
/// Reach (GDD §16). JSON: { "type": "joinBloc", "bloc": "alliance" }.</summary>
public sealed class JoinBlocEffect : IEffect
{
    public string Bloc { get; }
    public JoinBlocEffect(string bloc) => Bloc = bloc;
    public void Apply(EvalContext ctx)
    {
        var self = ctx.Actor(Binding.Self);
        var house = self != null ? ctx.World.House(self.HouseId) : null;
        if (house == null) return;
        house.Bloc = Bloc;
        ctx.World.Log($"{house.Name} declares for the {Bloc}.");
    }
}

/// <summary>Self's house declares war on the role's house (GDD §6). JSON: { "type": "declareWar", "role": "rival" }.</summary>
public sealed class DeclareWarEffect : IEffect
{
    public string Role { get; }
    public DeclareWarEffect(string role) => Role = role;
    public void Apply(EvalContext ctx)
    {
        var self = ctx.Actor(Binding.Self); var other = ctx.Actor(Role);
        if (self == null || other == null) return;
        new Systems.FleetSystem(ctx.Content?.Fleets ?? Systems.FleetRules.Default).DeclareWar(ctx.World, self.HouseId, other.HouseId, self.Id);
    }
}

/// <summary>Self's house and the role's house make peace. JSON: { "type": "makePeace", "role": "rival" }.</summary>
public sealed class MakePeaceEffect : IEffect
{
    public string Role { get; }
    public MakePeaceEffect(string role) => Role = role;
    public void Apply(EvalContext ctx)
    {
        var self = ctx.Actor(Binding.Self); var other = ctx.Actor(Role);
        if (self == null || other == null) return;
        new Systems.FleetSystem(ctx.Content?.Fleets ?? Systems.FleetRules.Default).MakePeace(ctx.World, self.HouseId, other.HouseId);
    }
}

/// <summary>Write a line to the history log (GDD §15 L1). JSON: { "type": "log", "text": "..." } — supports {role.name} tokens.</summary>
public sealed class LogEffect : IEffect
{
    public string Text { get; }
    public LogEffect(string text) => Text = text;
    public void Apply(EvalContext ctx) =>
        ctx.World.Log(TextRenderer.Substitute(Text, ctx));
}
