using Praetoria.Core.Crises;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The gate / cascade / damper crisis system (GDD §16) — the strategic mid-game heart. Crises are
/// NOT random events: each is a <em>gated, buildable state</em>. While any gate is un-cleared it
/// cannot fire; once every gate holds it is <b>causable</b> and may onset organically (a seeded
/// roll over ripe conditions) or be <b>authored</b> by an actor (player or NPC) through a command.
/// A crisis's <c>onTrigger</c> effects write world state, which can clear <em>other</em> crises'
/// gates — so the small→galaxy-spanning escalation arc <b>cascades</b> emergently rather than on a
/// scripted clock. <b>Dampers</b> arrest it, but each unlocks only when accumulated state from prior
/// play permits (high goodwill/legitimacy) — "the escalation you face is partly the bill for how you
/// played." Gates reuse the condition vocabulary; triggers/dampers reuse the effect vocabulary, so
/// the whole system is data and invents no new mechanism. Deterministic given State + RNG.
/// </summary>
public sealed class CrisisEngine
{
    private readonly IReadOnlyList<CrisisDef> _defs;
    private readonly Dictionary<string, CrisisDef> _byId;

    public CrisisEngine(IReadOnlyList<CrisisDef> defs)
    {
        _defs = defs;
        _byId = defs.ToDictionary(d => d.Id);
    }

    public IReadOnlyList<CrisisDef> Defs => _defs;
    public CrisisDef? Def(string id) => _byId.TryGetValue(id, out var d) ? d : null;
    public bool IsEmpty => _defs.Count == 0;

    private static EvalContext Ctx(World w, IRng rng) => new(w, new Binding(w.ProtagonistId), rng);

    /// <summary>True if every gate holds and the crisis isn't already active (unless repeatable).</summary>
    public bool IsCausable(CrisisDef def, World w, IRng rng)
    {
        if (w.IsCrisisActive(def.Id) && !def.Repeatable) return false;
        return Binder.AllHold(def.Gates, Ctx(w, rng));
    }

    /// <summary>Every crisis whose gates are currently cleared — the "causable" pool (id order).</summary>
    public List<CrisisDef> Causable(World w, IRng rng)
    {
        var list = new List<CrisisDef>();
        foreach (var def in _defs.OrderBy(d => d.Id, StringComparer.Ordinal))
            if (IsCausable(def, w, rng)) list.Add(def);
        return list;
    }

    /// <summary>Onset a crisis (authored if <paramref name="authorId"/> set, else organic). If it is
    /// already active and repeatable, the re-trigger escalates severity. Applies the onTrigger
    /// effects, which may clear other gates and cascade.</summary>
    public void Trigger(CrisisDef def, World w, IRng rng, string authorId = "")
    {
        if (w.Crises.TryGetValue(def.Id, out var active))
            active.Severity += def.Severity;     // a cascade re-feeding an active crisis escalates it
        else
            w.Crises[def.Id] = new ActiveCrisis
            {
                Id = def.Id, Tier = def.Tier, Severity = def.Severity,
                TurnStarted = w.Turn, AuthorId = authorId
            };

        w.WorldFlags.Add(World.CrisisFlag(def.Id));
        foreach (var eff in def.OnTrigger) eff.Apply(Ctx(w, rng));

        string who = string.IsNullOrEmpty(authorId) ? "organically" : $"by {authorId}";
        w.Log($"Crisis '{def.Name}' ({def.Tier}) erupts {who}.");
    }

    /// <summary>Dampers whose availability conditions currently hold (GDD §16 — scaled by prior play).</summary>
    public List<DamperDef> AvailableDampers(CrisisDef def, World w, IRng rng)
    {
        var list = new List<DamperDef>();
        foreach (var d in def.Dampers)
            if (Binder.AllHold(d.Availability, Ctx(w, rng))) list.Add(d);
        return list;
    }

    /// <summary>Apply a damper: run its effects, cut severity by its relief, resolve the crisis at &lt;= 0.</summary>
    public void ApplyDamper(CrisisDef def, DamperDef damper, World w, IRng rng)
    {
        foreach (var eff in damper.Effects) eff.Apply(Ctx(w, rng));
        if (w.Crises.TryGetValue(def.Id, out var active))
        {
            active.Severity -= damper.Relief;
            if (active.Severity <= 0) Resolve(def, w);
        }
    }

    /// <summary>End an active crisis (its severity is spent) and clear its active flag.</summary>
    public void Resolve(CrisisDef def, World w)
    {
        if (w.Crises.Remove(def.Id))
        {
            w.WorldFlags.Remove(World.CrisisFlag(def.Id));
            w.Log($"Crisis '{def.Name}' subsides.");
        }
    }

    /// <summary>A seeded weighted roll over the causable pool, with an <paramref name="abstainWeight"/>
    /// so ripe conditions don't fire instantly every turn (organic onset is a roll, GDD §16). Returns
    /// the crisis to onset, or null to abstain this turn.</summary>
    public CrisisDef? RollOrganic(World w, IRng rng, double abstainWeight = 3.0)
    {
        var pool = Causable(w, rng);
        if (pool.Count == 0) return null;

        double total = abstainWeight;
        foreach (var d in pool) total += Math.Max(0.0001, d.Weight);

        double roll = rng.NextDouble() * total - abstainWeight;
        if (roll < 0) return null;               // abstained
        foreach (var d in pool)
        {
            roll -= Math.Max(0.0001, d.Weight);
            if (roll < 0) return d;
        }
        return null;
    }
}
