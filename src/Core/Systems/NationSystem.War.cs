using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Whose war it is, and how it is fought (docs/BEYOND.md, "Whose war"; the owner's answers of 2026-11-19).
///
/// The Senate votes a war and directs nothing. It may vote peace as well, and a general may not listen. A house may make war on a
/// nation on its own account, without the Senate's leave. So a war with a nation is the Reach's (voted, or begun by the nation: every
/// house is at war with it) or one house's own (<see cref="Nation.WarOf"/>: only that house is).
///
/// And houses raid. In a war the nation did not begin, a house of the Reach sacks a world of the nation's and goes home; it takes
/// worlds only when the nation attacked first. The player chooses for their own house.</summary>
public sealed partial class NationSystem
{
    public const int SackRest = 6, ScoreSack = 2, SackUnrest = 10, PrivateRegard = 15, PrivateLeast = 3, DefyLegitimacy = 6, DefyYearly = 2, DefySway = 50, DefyYears = 6, PeaceRegard = 5;
    public const string PlayerRaidsFlag = "we_raid_beyond", PeaceObeyFlag = "peace_answer_obey", PeaceDefyFlag = "peace_answer_defy";
    public const string SackedCounter = "nation_worlds_sacked", PrivateCounter = "private_wars", PeaceVotedCounter = "foreign_peace_voted", PeaceMadeCounter = "foreign_peace_made", DefiedCounter = "foreign_peace_defied", CameHomeCounter = "defiers_came_home";
    public static string SackedKey(string holdingId) => "sacked_turn:" + holdingId;
    public static string DefiedSinceKey(string houseId) => "defied_since:" + houseId;
    private static string ComeHomeKey(Nation n) => "peace_now:" + n.Id;

    /// <summary>A house of the Reach that is this nation's enemy in the war being fought: any, in the Reach's war; one, in a house's own.</summary>
    public static bool Foe(World w, Nation n, string houseId) => w.OfTheReach(houseId) && (n.WarOf.Length == 0 || n.WarOf == houseId);
    /// <summary>The nation a house is defying the Senate's peace over, if it is.</summary>
    public static Nation? DefiedBy(World w, string houseId) => w.Nations.FirstOrDefault(n => n.AtWar && n.Defied && n.WarOf == houseId);

    // ---- raiding (the owner: "more like raiding wars; they would only conquer something if an opposing nation attacks them first") ----

    /// <summary>Whether a house sacks this nation's worlds and goes home, or takes them. Other houses raid unless the nation began the
    /// war; the player's house does as the player has set.</summary>
    public static bool RaidsOnly(World w, Nation n, string houseId) => houseId == w.Protagonist?.HouseId ? w.HasFlag(PlayerRaidsFlag) : !n.BeganIt;
    public static bool Sacked(World w, Holding h) => w.Counter(SackedKey(h.Id)) is > 0 and var t && w.Turn - t < SackRest;
    /// <summary>Whether this besieger, over this holding, is there to sack it: a world of a nation's own (beyond the rim, not one it
    /// took from the Reach), in a war, by a house that raids.</summary>
    public static bool Sacks(World w, Holding h, string besieger) =>
        h.IsPlanet && w.NationOfHouse(h.OwnerId) is { AtWar: true } n && w.OfTheReach(besieger) && w.Galaxy?.RingOf(h.SystemId) == Galaxy.Ring.Beyond && RaidsOnly(w, n, besieger);
    /// <summary>How long a sack takes: a season, and as much longer as the nation's walls make any siege.</summary>
    public static int SackTurns(World w, Holding h) => 1 + (w.NationOfHouse(h.OwnerId)?.Walls ?? 0);

    /// <summary>A world of a nation's is sacked: the raiders are paid as for a world taken, it counts in the war for two (a world taken
    /// counts three), and the world stays the nation's. It is not worth sacking again for six years.</summary>
    public static void Sack(World w, Holding h, string raider)
    {
        var n = w.NationOfHouse(h.OwnerId); if (n == null) return;
        w.WorldCounters[SackedKey(h.Id)] = Math.Max(1, w.Turn); h.Unrest = Math.Min(100, h.Unrest + SackUnrest);
        Plunder(w, raider, n, PlunderPerWorld * n.Wealth, $"out of {h.Name}");
        if (n.AtWar) { n.ScoreReach += ScoreSack; n.LastBattleTurn = w.Turn; w.WorldCounters[WinsKey(raider, n.Id)] = w.Counter(WinsKey(raider, n.Id)) + 1; }
        Bump(w, SackedCounter, n); Mark(w, raider == w.Protagonist?.HouseId ? "world_sacked_by_us" : "world_sacked", n);
        w.Log($"{w.House(raider)?.Name ?? raider} sacks {h.Name} and goes home with what it carried. The world is still {w.House(h.OwnerId)?.Name ?? h.OwnerId}'s.");
    }

    // ---- a house's own war ----

    public static string? PrivateWarRefusal(World w, NationCatalog catalog, House? house, string nationId)
    {
        var n = w.Nation(nationId); var d = catalog.Def(nationId); if (n == null || d == null) return "no such nation";
        if (house == null || !w.OfTheReach(house.Id)) return "not a house of the Reach";
        if (n.Fallen) return $"{d.Name} is no more";
        if (!n.OnMap) return $"{d.Name}'s worlds are not on the map: only a war the Senate votes reaches them";
        if (n.AtWar) return n.WarOf == house.Id ? "it is your war already" : $"the Reach is already at war with {d.Name}";
        if (n.Subdued(w.Turn)) return $"{d.Name} pays the Reach tribute: the treaty is the Senate's to break";
        if (AtWar(w) is { } other) return $"one war beyond the Reach at a time ({catalog.Def(other.Id)?.Name ?? other.Id})";
        if (!w.Fleets.Values.Any(f => f.OwnerId == house.Id && !f.IsAway && !f.Office && f.Strength >= PrivateLeast)) return $"needs a fleet of {PrivateLeast} hulls of your own";
        return null;
    }

    /// <summary>A house makes war on a nation on its own account. Nobody voted it: only that house is at war, and the Senate may yet
    /// vote that it stop.</summary>
    public static void PrivateWar(World w, NationCatalog catalog, string nationId, string houseId)
    {
        var n = w.Nation(nationId); var d = catalog.Def(nationId); var house = w.House(houseId); if (n == null || d == null || PrivateWarRefusal(w, catalog, house, nationId) != null) return;
        n.AtWar = true; n.BeganIt = false; n.WarOf = houseId; n.Defied = false; n.PeaceVoted = 0; n.WarSince = w.Turn; n.ScoreReach = 0; n.ScoreNation = 0; n.LastBattleTurn = w.Turn; n.Wars += 1;
        n.Disposition = Math.Max(-100, n.Disposition - PrivateRegard);
        w.WorldCounters[WarsCounter] = w.Counter(WarsCounter) + 1; Bump(w, PrivateCounter, n);
        w.WorldFlags.Add("foreign_war"); w.WorldFlags.Add("foreign_war:" + n.Id); Mark(w, houseId == w.Protagonist?.HouseId ? "private_war_by_us" : "private_war", n);
        w.Log($"{house!.Name} makes war on {d.Name} on its own account. The Senate has voted nothing: it is {house.Name}'s war, and nobody else's.");
    }

    // ---- the Senate's peace, and those who do not listen ----

    public static string? PeaceRefusal(World w, NationCatalog catalog, string nationId)
    {
        var n = w.Nation(nationId); var d = catalog.Def(nationId); if (n == null || d == null) return "no such nation";
        if (!n.AtWar) return $"the Reach is at peace with {d.Name}";
        if (n.Defied) return $"the Senate has voted this peace already, and {w.House(n.WarOf)?.Name ?? n.WarOf} does not listen";
        if (n.PeaceVoted > 0) return "the peace is voted: the fleets are being told";
        return null;
    }

    /// <summary>The Senate votes peace with a nation (SenateSystem.Apply). Nothing is ended yet: those in the field are told, and have a
    /// year to answer.</summary>
    public static void VotePeace(World w, NationCatalog catalog, string nationId, string proposerHouseId)
    {
        var n = w.Nation(nationId); var d = catalog.Def(nationId); if (n == null || d == null || PeaceRefusal(w, catalog, nationId) != null) return;
        n.PeaceVoted = Math.Max(1, w.Turn); Bump(w, PeaceVotedCounter, n); w.WorldFlags.Add("peace_voted");
        var mine = w.Protagonist?.HouseId; if (mine != null && Carries(w, n, d, mine)) w.WorldFlags.Add("peace_voted_on_us");
        w.Log($"The Senate votes peace with {d.Name}, on the motion of {w.House(proposerHouseId)?.Name ?? proposerHouseId}. It can order no fleet home: those in the field are told, and will answer for themselves.");
    }

    /// <summary>Whether a house is carrying this war: it is the house's own, or the house has a fleet in the nation's space.</summary>
    public static bool Carries(World w, Nation n, NationDef d, string houseId)
    {
        if (!n.AtWar || !Foe(w, n, houseId)) return false;
        if (n.WarOf == houseId) return true;
        var own = Own(d);
        return w.Fleets.Values.Any(f => f.OwnerId == houseId && !f.IsAway && (f.Moving ? own.Contains(f.DestinationId) : own.Contains(f.SystemId)));
    }

    /// <summary>The peace the Senate voted, a year on: those carrying the war have answered. If one will not listen the war is that
    /// house's own from now on, at a cost to its name every year; otherwise the peace is made. Also the yearly reckoning of a house that
    /// defies: it comes home when it is losing, when it has nothing left to fight with, or after six years. True if the war ended.
    /// Another house does not listen when its general has the name to dare it (a sway of fifty), or the war is its own and its head is
    /// ambitious, ruthless or proud; and in either case only while the war is not being lost.</summary>
    private static bool Peace(World w, Nation n, NationDef d, string? mine)
    {
        if (w.Counter(ComeHomeKey(n)) > 0) { MakePeace(w, n, d, $"{w.House(n.WarOf)?.Name ?? "The last of its fleets"} comes home"); return true; }
        if (n.Defied)
        {
            var dh = w.House(n.WarOf); int since = w.Counter(DefiedSinceKey(n.WarOf));
            bool spent = dh == null || !w.OfTheReach(dh.Id) || !w.Fleets.Values.Any(f => f.OwnerId == dh.Id && !f.IsAway && f.Strength >= 2);
            bool home = dh != null && dh.Id == mine ? w.HasFlag(PeaceObeyFlag) : n.ScoreNation > n.ScoreReach || w.Turn - since >= DefyYears;
            if (spent || home) { Bump(w, CameHomeCounter, n); MakePeace(w, n, d, spent ? $"{dh?.Name ?? "The house that fought on"} has nothing left to fight it with" : $"{dh!.Name} comes home at last"); return true; }
            dh!.Legitimacy = Math.Max(0, dh.Legitimacy - DefyYearly);
            return false;
        }
        if (n.PeaceVoted <= 0) return false;
        var carrying = w.Houses.Values.Where(h => Carries(w, n, d, h.Id)).OrderByDescending(h => h.Id == n.WarOf).ThenByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        bool asked = mine != null && carrying.Any(h => h.Id == mine);
        if (asked && !w.HasFlag(PeaceObeyFlag) && !w.HasFlag(PeaceDefyFlag) && w.Turn - n.PeaceVoted <= 1) { Mark(w, "peace_awaits_us", n); return false; }   // the player has this year to answer
        House? defier = asked && w.HasFlag(PeaceDefyFlag) ? w.House(mine!) : null;
        // another house does not listen when its general has the name to dare it and the war is going well
        defier ??= carrying.FirstOrDefault(h => h.Id != mine && n.ScoreReach >= n.ScoreNation && Defies(w, n, h));
        if (defier == null) { MakePeace(w, n, d, "The fleets come home"); return true; }
        n.WarOf = defier.Id; n.Defied = true; n.PeaceVoted = 0; w.WorldCounters[DefiedSinceKey(defier.Id)] = Math.Max(1, w.Turn);
        defier.Legitimacy = Math.Max(0, defier.Legitimacy - DefyLegitimacy);
        Bump(w, DefiedCounter, n); Mark(w, defier.Id == mine ? "peace_defied_by_us" : "peace_defied", n);
        w.Log($"{defier.Name} does not listen. The Senate has voted peace with {d.Name}, and {(CaesarSystem.GeneralOf(w, defier) is { } gg ? gg.Name : defier.Name)} fights on: it is {defier.Name}'s war now, against the Senate's word.");
        return false;
    }

    /// <summary>Whether a house other than the player's fights on. Its general has the name to dare it; or the war is the house's own
    /// and its head is the kind who does not take an order from a chamber.</summary>
    private static bool Defies(World w, Nation n, House h)
    {
        int sway = CaesarSystem.GeneralOf(w, h) is { } g ? CaesarSystem.Sway(w, g) : 0;
        if (sway >= DefySway) return true;
        if (n.WarOf != h.Id) return false;
        return sway >= CaesarSystem.RecallSway || (Economy.HeadOf(w, h) is { } hd && (hd.NatureTraits.Contains("Ambitious") || hd.NatureTraits.Contains("Ruthless") || hd.NatureTraits.Contains("Proud")));
    }

    /// <summary>A peace the Senate voted is made. Whoever is ahead keeps what they hold; no tribute is paid and no triumph is voted.</summary>
    private static void MakePeace(World w, Nation n, NationDef d, string how)
    {
        bool ahead = n.ScoreReach >= n.ScoreNation;
        w.WorldFlags.Add("foreign_peace"); Bump(w, PeaceMadeCounter, n); Mark(w, "peace_made", n); n.Disposition = Math.Min(100, n.Disposition + PeaceRegard);
        w.Log($"{how}: the Senate's peace with {d.Name} is made. {(ahead ? "Each side keeps what it holds of its own, and" : "What they hold of the Reach's is ceded, what the Reach holds of theirs is given up, and")} nobody pays tribute.");
        PeaceOnMap(w, n, d, reachWon: ahead);
        EndWar(w, n, d);
    }

    /// <summary>A house that fought on comes home (CaesarSystem.Obey, when its general lays down the command): the peace is made at the next reckoning.</summary>
    public static void CameHome(World w, string houseId)
    {
        if (DefiedBy(w, houseId) is { } n) w.WorldCounters[ComeHomeKey(n)] = 1;
    }
}
