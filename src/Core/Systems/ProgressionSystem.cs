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
/// <item><b>Merit — be granted it.</b> Capped by the soft-lock (only what the house is legitimate for is
/// granted). Up to Prefect it is granted for the asking; from Legate up the rung must be earned: the house
/// has the land or the seats in the Senate, and the Senate votes on the petition (docs/TITLES.md, "Rungs
/// that are earned"). It gives no legitimacy: rank follows standing and does not make it.</item>
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

    /// <summary>The chamber, when the world has one: its count of a house's seats, and the docket a petition for a rung above
    /// Prefect goes onto (docs/TITLES.md, "Rungs that are earned"). Null in a world built without a Senate: a petition is then
    /// granted as it always was, and seats count for nothing.</summary>
    public SenateSystem? Senate { get; set; }

    /// <summary>Hulls of its own a house must have, for each rung of the title it would seize: a rank is taken with ships.</summary>
    public const int SeizeHullsPerRank = 2;
    /// <summary>The rung from which a title must be earned: a Legate, a Proconsul and a First Citizen want land or seats under
    /// them (one, two and three worlds; or 8, 12 and 16 seats), and a petition for one of them is voted by the Senate.</summary>
    public const int LandedFromRank = 3;
    /// <summary>Seats in the Senate that stand in place of land: so many for a Legate, and so many more for each rung above.</summary>
    public const int SeatsForLegate = 8, SeatsPerRung = 4;
    /// <summary>The motion a petition for a rung above Prefect is (content/senate/motions.json).</summary>
    public const string PetitionKind = "elevation";
    /// <summary>The years a house waits before it asks the Senate for the same rung again, counted from the year it asked; and
    /// how often a rival that could petition does: one year in so many.</summary>
    public const int AskAgainYears = 3, RivalPetitionOneIn = 4;
    public static int WorldsFor(int rank) => Math.Max(0, rank - LandedFromRank + 1);
    public static int SeatsFor(int rank) => rank < LandedFromRank ? 0 : SeatsForLegate + (rank - LandedFromRank) * SeatsPerRung;
    private static string RoseKey(string houseId) => "rose:" + houseId;
    public static int Worlds(World w, string houseId) => w.HoldingsOf(houseId).Count(h => h.IsPlanet);
    /// <summary>A house's seats in the Senate, as the chamber and the table of houses count them (<see cref="SenateSystem.Weight"/>):
    /// its influence there, its rank, the offices it holds, what its order and the laws add. Nought where there is no Senate.</summary>
    public int Seats(World w, string houseId) => Senate?.Weight(w, houseId) ?? 0;
    private static int OwnHulls(World w, string houseId) => w.Fleets.Values.Where(f => f.OwnerId == houseId && !f.Office && f.LevyOf.Length == 0).Sum(f => f.Strength);
    private static void Rose(World w, string houseId) => w.WorldCounters[RoseKey(houseId)] = w.Turn + 1;

    /// <summary>What a rung asks to stand on, and what the house has of it: worlds, or seats in the Senate. Either is enough.</summary>
    public sealed record Footing(int Rank, int WorldsWanted, int WorldsHeld, int SeatsWanted, int SeatsHeld, bool SeatsCount)
    {
        /// <summary>The rung asks for something: a Legate or better.</summary>
        public bool Asked => WorldsWanted > 0;
        public bool ByLand => WorldsHeld >= WorldsWanted;
        public bool BySeats => SeatsCount && SeatsHeld >= SeatsWanted;
        public bool Met => !Asked || ByLand || BySeats;
    }
    public Footing FootingFor(World w, string houseId, TitleDef next)
    {
        int rank = Title.RankOf(next.Id);
        return new Footing(rank, WorldsFor(rank), Worlds(w, houseId), SeatsFor(rank), Seats(w, houseId), Senate != null);
    }
    /// <summary>Whether a petition for this rung goes before the Senate: a rung from Legate up, in a world that has a chamber.</summary>
    public bool Voted(TitleDef next) => Title.RankOf(next.Id) >= LandedFromRank && Senate?.Motions.Def(PetitionKind) != null;
    /// <summary>The house's petition for a rank, while it is before the Senate.</summary>
    public static Motion? PetitionOf(World w, string houseId) => w.Motions.FirstOrDefault(m => m.Open && m.Kind == PetitionKind && m.TargetId == houseId);
    private static string AskedKey(string houseId, string titleId) => $"motion_cd:{houseId}:{PetitionKind}:{houseId}:{titleId}";

    /// <summary>What stops any rise this year, whichever road is taken (docs/PLAYTHROUGH.md 3): there is a rung to climb, the house
    /// has not climbed one already this year, and from Legate up it has what the rank stands on: the worlds, or the seats.</summary>
    private string? RiseRefusal(World w, Character? c, bool needsFooting)
    {
        if (c?.Alive != true) return "no one to ask";
        var next = NextTitle(w, c.HouseId); if (next == null) return "there is no higher rung to climb this way";
        if (w.WorldCounters.TryGetValue(RoseKey(c.HouseId), out var at) && at == w.Turn + 1) return "the house has risen a rung this year: one a year";
        var f = FootingFor(w, c.HouseId, next);
        if (needsFooting && !f.Met)
            return $"a {next.NameIn(w.Era)} holds {(f.WorldsWanted == 1 ? "a world" : f.WorldsWanted + " worlds")}"
                + (f.SeatsCount ? $" or has {Counted.Seats(f.SeatsWanted)} in the Senate: the house holds {f.WorldsHeld} and has {Counted.Seats(f.SeatsHeld)}" : $": the house holds {f.WorldsHeld}");
        return null;
    }
    /// <summary>Why a house may not seize the next title by force, or null: its head is an officer, and it has the ships.</summary>
    public string? SeizeRefusal(World w, string actorId)
    {
        var c = w.Char(actorId); if (RiseRefusal(w, c, needsFooting: false) is { } no) return no;
        if (c!.CareerRank < 2) return "only an officer of some rank has the men to do it";
        var next = NextTitle(w, c.HouseId)!; int need = Title.RankOf(next.Id) * SeizeHullsPerRank, have = OwnHulls(w, c.HouseId);
        return have >= need ? null : $"needs {need} hulls of the house's own to take the rank of {next.NameIn(w.Era)}: it has {have}";
    }
    public bool CanSeize(World w, string actorId) => SeizeRefusal(w, actorId) == null;
    /// <summary>The roads by which a house could take the next rung this year, as things stand, with nothing counted but the rule:
    /// "petition" (the standing is there), "claim" (a claim in hand), "seize" (the ships). Empty where there is no rung or none
    /// is open. What each costs in influence or agents is the command's, not this (docs/PLAYTHROUGH.md, Medium, The Senate: "an
    /// alert when you qualify").</summary>
    public List<string> OpenRoads(World w, string actorId)
    {
        var roads = new List<string>();
        if (PetitionRefusal(w, actorId) == null) roads.Add("petition");
        if (ClaimRefusal(w, actorId) == null) roads.Add("claim");
        if (SeizeRefusal(w, actorId) == null) roads.Add("seize");
        return roads;
    }
    /// <summary>Why a house may not petition for the next rung, or null. Up to Prefect: the standing. From Legate up: the
    /// standing, the land or the seats, and room for the petition before the Senate, which votes on it.</summary>
    public string? PetitionRefusal(World w, string actorId)
    {
        var c = w.Char(actorId); if (RiseRefusal(w, c, needsFooting: true) is { } no) return no;
        var next = NextTitle(w, c!.HouseId)!; var h = w.House(c.HouseId)!;
        if (h.Legitimacy < next.LegitimacyRequirement) return $"needs legitimacy {next.LegitimacyRequirement}: the house has {h.Legitimacy}";
        if (!Voted(next)) return null;
        if (ChargeSystem.HeadAbroadSays(w, h, actorId) is { } abroad) return abroad;   // a head in exile (docs/LADDER.md, "A charge against the powerful")
        if (!PolitySystem.InReach(w, h.Id)) return "a house that has left the Reach does not petition its Senate";
        if (PetitionOf(w, h.Id) is { } asked) return $"the house's petition is before the Senate already: it is voted at the end of {(asked.ProposedTurn < w.Turn ? "this" : "next")} year";
        if (w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id)) return "the house has a motion before the Senate already: one at a time";
        if (SenateSystem.Docket(w).Count() >= SenateSystem.DocketSize) return "the docket is full this session: the Senate hears two matters at a time";
        int wait = w.Counter(AskedKey(h.Id, next.Id)) - w.Turn;
        return wait > 0 ? $"the Senate has heard the house on this lately: it may ask again in {Counted.Years(wait)}" : null;
    }
    public string? ClaimRefusal(World w, string actorId)
    {
        var c = w.Char(actorId); if (RiseRefusal(w, c, needsFooting: true) is { } no) return no;
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
        return PetitionRefusal(w, actorId) == null;   // the legitimate, once a year, with what the rank stands on
    }

    /// <summary>Petition for the next rung. Up to Prefect it is granted. From Legate up it goes before the Senate as a motion
    /// (returned), voted at the end of next year like any other; the house rises only if it is carried (<see cref="Carried"/>).
    /// A granted petition gives no legitimacy: rank follows standing and does not make it (docs/PLAYTHROUGH.md S58).</summary>
    public Motion? Petition(World w, string actorId)
    {
        var c = w.Char(actorId)!;
        var h = w.House(c.HouseId)!;
        var next = NextTitle(w, h.Id)!;
        if (Voted(next))
        {
            var m = Senate!.Add(w, h.Id, PetitionKind, h.Id, titleId: next.Id); m.Votes[h.Id] = 1;
            w.WorldCounters[AskedKey(h.Id, next.Id)] = w.Turn + AskAgainYears;   // turned away, it may ask again sooner than the chamber's ten years
            if (w.Protagonist != null && h.Id != w.Protagonist.HouseId)
                w.Tell("info", $"{h.Name} petitions the Senate for the rank of {next.NameIn(w.Era)}. The chamber votes at the year's end, and your house has a vote", "senate:chamber", h.Id);
            return m;
        }
        h.Title = next.Id; Rose(w, h.Id);
        w.Log($"{c.Name} is GRANTED the {(w.IsImperium ? "title" : "rank")} of {next.NameIn(w.Era)}.");
        return null;
    }

    /// <summary>The Senate has carried a house's petition: it rises, if it is still where it was when it asked. A house that has
    /// since risen another way, or no longer has the land or the seats, has leave that lapses.</summary>
    public bool Carried(World w, Motion m)
    {
        var h = w.House(m.TargetId); if (h == null) return false;
        var next = NextTitle(w, h.Id); string asked = _titles.ById(m.TitleId)?.NameIn(w.Era) ?? m.TitleId; bool mine = h.Id == w.Protagonist?.HouseId;
        string? lapse = next == null || next.Id != m.TitleId ? "the house is not on the rung it asked from"
            : !FootingFor(w, h.Id, next).Met ? "the house no longer has the land or the seats the rank stands on" : null;
        if (lapse != null)
        {
            w.Log($"The Senate's leave for {h.Name} to take the rank of {asked} lapses: {lapse}.");
            if (mine) w.Tell("warn", $"The Senate carried your petition for the rank of {asked}, but its leave lapses: {lapse}", "senate:ladder");
            return false;
        }
        h.Title = next!.Id; Rose(w, h.Id);
        w.Log($"{h.Name} is GRANTED the {(w.IsImperium ? "title" : "rank")} of {asked} by vote of the Senate, {m.ForWeight} to {m.AgainstWeight}.");
        if (mine) w.Tell("good", $"The Senate has carried your petition, {m.ForWeight} to {m.AgainstWeight}: your house holds the rank of {asked}", "senate:ladder");
        return true;
    }

    /// <summary>The Senate has thrown a house's petition out. It keeps its rank, and may ask again when the wait is over.</summary>
    public void Refused(World w, Motion m)
    {
        var h = w.House(m.TargetId); if (h == null || h.Id != w.Protagonist?.HouseId) return;
        string asked = _titles.ById(m.TitleId)?.NameIn(w.Era) ?? m.TitleId; int wait = Math.Max(1, w.Counter(AskedKey(h.Id, m.TitleId)) - w.Turn);
        w.Tell("warn", $"The Senate has thrown out your petition for the rank of {asked}, {m.AgainstWeight} to {m.ForWeight}. The house may ask again in {Counted.Years(wait)}", "senate:ladder");
    }

    /// <summary>Whether a rival house petitions this year (the other houses rise by the same road as the player's): it could,
    /// about one year in <see cref="RivalPetitionOneIn"/>, and where the Senate must vote, only when no other house's petition
    /// for a rank is before it. The command is the player's own.</summary>
    public bool RivalWouldPetition(World w, House house, Character head)
    {
        if (World.Outsider(house.Id) || !PolitySystem.InReach(w, house.Id) || NextTitle(w, house.Id) is not { } next) return false;
        uint hash = 2166136261; foreach (var ch in house.Id + ":rise:" + w.Turn) hash = (hash ^ ch) * 16777619;
        if (hash % RivalPetitionOneIn != 0) return false;
        if (Voted(next) && w.Motions.Any(m => m.Open && m.Kind == PetitionKind)) return false;
        return PetitionRefusal(w, head.Id) == null;
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
        LegitimacyBook.Move(w, h, 12, LegitimacyBook.Titles, "A claim pressed to the rank of " + next.NameIn(w.Era));   // bloodline launders the claim
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
            LegitimacyBook.Move(w, h, 1, LegitimacyBook.Titles, "A rank held above the house's standing, settling with time");   // a powder keg slowly stabilises with time
        }
    }
}
