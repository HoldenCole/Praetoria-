using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The progression system (GDD §13): the title ladder and the legitimacy soft-lock. Title and career
/// are independent currencies — the mid-game is converting what you have into what you lack, and the
/// three rise paths are the three exchange rates:
/// <list type="bullet">
/// <item><b>Military — take it.</b> Needs martial power; ignores legitimacy (so it breeds instability)
/// and spikes the galaxy's fear (the §7 <c>seizures</c> threat input).</item>
/// <item><b>Merit — be granted it.</b> Capped by the soft-lock (the throne grants only what you're
/// legitimate for); arrives clean, with standing.</item>
/// <item><b>Intrigue — claim it.</b> Needs a claim (marriage/inheritance); launders legitimacy through
/// bloodline, but the scheme leaves a trail (<c>corruption</c>).</item>
/// </list>
/// The soft-lock is a <em>modifier, not a gate</em> (§13): holding a title above its legitimacy
/// requirement is allowed but each turn breeds instability — vassals grumble (<c>unrest</c>), feeding
/// the §16 crisis web — until your standing slowly catches up. Deterministic and RNG-free.
/// </summary>
public sealed class ProgressionSystem
{
    private readonly TitleCatalog _titles;

    public ProgressionSystem(TitleCatalog titles) => _titles = titles;

    public bool IsEmpty => _titles.IsEmpty;

    /// <summary>How far the protagonist house's title outstrips its legitimacy (0 if legitimate).</summary>
    public int Instability(World w, string houseId)
    {
        var h = w.House(houseId);
        var t = h != null ? _titles.ById(h.Title) : null;
        return t == null ? 0 : Math.Max(0, t.LegitimacyRequirement - h!.Legitimacy);
    }

    public TitleDef? NextTitle(World w, string houseId)
    {
        var h = w.House(houseId);
        return h == null ? null : _titles.Next(h.Title);
    }

    // ---- the three rise paths (GDD §13) ----

    public bool CanSeize(World w, string actorId)
    {
        var c = w.Char(actorId);
        if (c?.Alive != true || NextTitle(w, c.HouseId) == null) return false;
        return c.CareerRank >= 2;   // a real officer with a fleet/army behind the claim
    }

    public void Seize(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = _titles.Next(h.Title)!;
        h.Title = next.Id;                                       // legitimacy UNCHANGED → instability
        w.WorldCounters["seizures"] = w.Counter("seizures") + 1; // every seizure spikes rival fear (§7)
        w.Log($"{c.Name} SEIZES the title of {next.Name} by force.");
    }

    public bool CanPetition(World w, string actorId)
    {
        var c = w.Char(actorId);
        var next = c?.Alive == true ? NextTitle(w, c.HouseId) : null;
        if (next == null) return false;
        return w.House(c!.HouseId)!.Legitimacy >= next.LegitimacyRequirement;  // throne grants only the legitimate
    }

    public void Petition(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = _titles.Next(h.Title)!;
        h.Title = next.Id;
        h.Legitimacy += 5;                                       // arrives clean, with standing
        w.Log($"{c.Name} is GRANTED the title of {next.Name}.");
    }

    public bool CanClaimByBlood(World w, string actorId)
    {
        var c = w.Char(actorId);
        var next = c?.Alive == true ? NextTitle(w, c.HouseId) : null;
        if (next == null) return false;
        return w.House(c!.HouseId)!.Claims.Contains(next.Id);
    }

    public void ClaimByBlood(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = _titles.Next(h.Title)!;
        h.Title = next.Id;
        h.Claims.Remove(next.Id);
        h.Legitimacy += 12;                                     // bloodline launders the claim
        w.WorldCounters["corruption"] = w.Counter("corruption") + 1;  // the scheme leaves a trail
        w.Log($"{c.Name} presses a CLAIM to the title of {next.Name}.");
    }

    /// <summary>Per-turn soft-lock (GDD §13). For the protagonist house, surface title rank /
    /// legitimacy / instability into counters (so events and crisis gates read them), and while a
    /// title is held above its legitimacy, make vassals grumble (unrest) and slowly legitimise the
    /// holder. Call once per turn.</summary>
    public void Apply(World w)
    {
        var prot = w.Protagonist;
        var h = prot != null ? w.House(prot.HouseId) : null;
        var t = h != null ? _titles.ById(h.Title) : null;
        if (h == null || t == null) return;

        int instability = Math.Max(0, t.LegitimacyRequirement - h.Legitimacy);
        w.WorldCounters["title_instability"] = instability;
        w.WorldCounters["title_rank"] = t.Rank;
        w.WorldCounters["house_legitimacy"] = h.Legitimacy;

        if (instability > 0)
        {
            int grumble = Math.Min(3, 1 + instability / 20);    // rivals contest, vassals grumble (§13)
            w.WorldCounters["unrest"] = w.Counter("unrest") + grumble;
            h.Legitimacy += 1;                                  // a powder keg slowly stabilises with time
        }
    }
}
