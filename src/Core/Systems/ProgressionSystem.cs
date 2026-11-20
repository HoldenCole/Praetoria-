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

    /// <summary>The next rung a house can climb to by seizing, petitioning or claiming. The throne is not one of them: it is
    /// taken by proclamation or by a fleet over the capital (<see cref="ThroneSystem"/>), which change the era and depose
    /// whoever sat there. Without this the title could be had with none of that (docs/ROLEPLAY.md).</summary>
    public TitleDef? NextTitle(World w, string houseId)
    {
        var h = w.House(houseId); var next = h == null ? null : _titles.Next(h.Title);
        return next?.Id == Title.Emperor ? null : next;
    }

    // ---- the three rise paths (GDD §13) ----

    /// <summary>Hulls of its own a house must have, for each rung of the title it would seize: a rank is taken with ships.</summary>
    public const int SeizeHullsPerRank = 2;
    /// <summary>The rung from which a title wants land under it, and how many worlds: a Legate one, a Proconsul two, a First Citizen three.</summary>
    public const int LandedFromRank = 3;
    private static string RoseKey(string houseId) => "rose:" + houseId;
    private static int Worlds(World w, string houseId) => w.HoldingsOf(houseId).Count(h => h.IsPlanet);
    private static int OwnHulls(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Office && f.LevyOf.Length == 0).Sum(f => f.Strength);
    private static void Rose(World w, string houseId) => w.WorldCounters[RoseKey(houseId)] = w.Turn + 1;
    /// <summary>What stops any rise this year, whichever road is taken (docs/PLAYTHROUGH.md 3): there is a rung to climb, the house
    /// has not climbed one already this year, and from Legate up it holds the worlds the rank wants.</summary>
    private string? RiseRefusal(World w, Character? c, bool needsLand)
    {
        if (c?.Alive != true) return "no one to ask";
        var next = NextTitle(w, c.HouseId); if (next == null) return "there is no higher rung to climb this way";
        if (w.WorldCounters.TryGetValue(RoseKey(c.HouseId), out var at) && at == w.Turn + 1) return "the house has risen a rung this year: one a year";
        int rank = Title.RankOf(next.Id), want = rank - LandedFromRank + 1;
        if (needsLand && want > 0 && Worlds(w, c.HouseId) < want) return $"a {next.NameIn(w.Era)} holds {(want == 1 ? "a world" : want + " worlds")}: the house holds {Worlds(w, c.HouseId)}";
        return null;
    }
    /// <summary>Why a house may not seize the next title by force, or null: its head is an officer, and it has the ships.</summary>
    public string? SeizeRefusal(World w, string actorId)
    {
        var c = w.Char(actorId); if (RiseRefusal(w, c, needsLand: false) is { } no) return no;
        if (c!.CareerRank < 2) return "only an officer of some rank has the men to do it";
        var next = NextTitle(w, c.HouseId)!; int need = Title.RankOf(next.Id) * SeizeHullsPerRank, have = OwnHulls(w, c.HouseId);
        return have >= need ? null : $"needs {need} hulls of the house's own to take the rank of {next.NameIn(w.Era)}: it has {have}";
    }
    public bool CanSeize(World w, string actorId) => SeizeRefusal(w, actorId) == null;
    public string? PetitionRefusal(World w, string actorId)
    {
        var c = w.Char(actorId); if (RiseRefusal(w, c, needsLand: true) is { } no) return no;
        var next = NextTitle(w, c!.HouseId)!; var h = w.House(c.HouseId)!;
        return h.Legitimacy >= next.LegitimacyRequirement ? null : $"needs legitimacy {next.LegitimacyRequirement}: the house has {h.Legitimacy}";
    }
    public string? ClaimRefusal(World w, string actorId)
    {
        var c = w.Char(actorId); if (RiseRefusal(w, c, needsLand: true) is { } no) return no;
        var next = NextTitle(w, c!.HouseId)!;
        return w.House(c.HouseId)!.Claims.Contains(next.Id) ? null : $"the house holds no claim to the rank of {next.NameIn(w.Era)}";
    }

    public void Seize(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = NextTitle(w, h.Id)!;
        h.Title = next.Id; Rose(w, h.Id);                        // legitimacy UNCHANGED → instability
        w.WorldCounters["seizures"] = w.Counter("seizures") + 1; // every seizure spikes rival fear (§7)
        w.Log($"{c.Name} SEIZES the {(w.IsImperium ? "title" : "rank")} of {next.NameIn(w.Era)} by force.");
    }

    public bool CanPetition(World w, string actorId)
    {
        return PetitionRefusal(w, actorId) == null;   // the legitimate, once a year, with the land the rank wants
    }

    public void Petition(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = NextTitle(w, h.Id)!;
        h.Title = next.Id; Rose(w, h.Id);
        h.Legitimacy += 5;                                       // arrives clean, with standing
        w.Log($"{c.Name} is GRANTED the {(w.IsImperium ? "title" : "rank")} of {next.NameIn(w.Era)}.");
    }

    public bool CanClaimByBlood(World w, string actorId)
    {
        return ClaimRefusal(w, actorId) == null;
    }

    public void ClaimByBlood(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = NextTitle(w, h.Id)!;
        h.Title = next.Id; Rose(w, h.Id);
        h.Claims.Remove(next.Id);
        h.Legitimacy += 12;                                     // bloodline launders the claim
        w.WorldCounters["corruption"] = w.Counter("corruption") + 1;  // the scheme leaves a trail
        w.Log($"{c.Name} presses a CLAIM to the {(w.IsImperium ? "title" : "rank")} of {next.NameIn(w.Era)}.");
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
        if (t.Rank > w.Counter("peak_title_rank")) w.WorldCounters["peak_title_rank"] = t.Rank;
        w.WorldCounters["house_legitimacy"] = h.Legitimacy;

        if (instability > 0)
        {
            int grumble = Math.Min(3, 1 + instability / 20);    // rivals contest, vassals grumble (§13)
            w.WorldCounters["unrest"] = w.Counter("unrest") + grumble;
            h.Legitimacy += 1;                                  // a powder keg slowly stabilises with time
        }
    }
}
