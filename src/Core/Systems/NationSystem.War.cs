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
    public const int SackRest = 6, ScoreSack = 2, SackUnrest = 10, PrivateRegard = 15, PrivateLeast = 3, DefySway = 50, DefyYears = 6, PeaceRegard = 5;
    public const int PaidYears = 10, ClientBreakOneIn = 12, ClientBreakDivided = 3;
    public const string PlayerClientsFlag = "we_make_clients";
    public const string ClientCounter = "nations_made_clients", ClientLostCounter = "clients_broke_away", PaidCounter = "tribute_paid_to_nations", PeaceKindCounter = "peace_kind";
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
            Defiance(w, dh!, yearly: true);
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
        Defiance(w, defier, yearly: false);
        Bump(w, DefiedCounter, n); Mark(w, defier.Id == mine ? "peace_defied_by_us" : "peace_defied", n);
        w.Log($"{defier.Name} does not listen. The Senate has voted peace with {d.Name}, and {(CaesarSystem.GeneralOf(w, defier) is { } gg ? gg.Name : defier.Name)} fights on: it is {defier.Name}'s war now, against the Senate's word.");
        return false;
    }

    /// <summary>Whether a house other than the player's fights on. Its general has the name to dare it; or the war is the house's own
    /// and its head is the kind who does not take an order from a chamber.</summary>
    private static bool Defies(World w, Nation n, House h)
    {
        int leeway = Leeway(w, h);
        if (leeway >= 2) return true;                    // a name the Senate will think twice about
        if (n.WarOf != h.Id) return false;
        return leeway >= 1 || (Economy.HeadOf(w, h) is { } hd && (hd.NatureTraits.Contains("Ambitious") || hd.NatureTraits.Contains("Ruthless") || hd.NatureTraits.Contains("Proud")));
    }

    // ---- what not listening costs, by who it is that does not listen (the owner: "if you are a well-known general and you are very
    // good, then you probably have more leeway to defy the Senate and they are less likely to recall you. If you are not well known ...
    // it probably costs you more and your chance of success is a lot lower") ----

    public const int LeewayKnown = 25, LeewayNamed = 45, LeewayGreat = 65, LeewaySkill = 7, CrewsWaver = 4;
    public static readonly int[] DefyAtOnce = { 8, 6, 4, 2 }, DefyAYear = { 3, 2, 1, 0 }, RecallLeanFor = { 4, 2, 0, -2 };
    /// <summary>How much the Senate will take from this house's general, 0 to 3. It is his sway on the Caesar road (battles won, crews
    /// that follow him, triumphs, rank): 25 is a known name, 45 a name, 65 a great one. A commander of real skill counts one step
    /// better than his name. A house with nobody of its own in command has none.</summary>
    public static int Leeway(World w, House? h)
    {
        var g = CaesarSystem.GeneralOf(w, h); if (g == null) return 0;
        int sway = CaesarSystem.Sway(w, g); int step = sway >= LeewayGreat ? 3 : sway >= LeewayNamed ? 2 : sway >= LeewayKnown ? 1 : 0;
        return Math.Min(3, step + (g.Skill("tactics") >= LeewaySkill ? 1 : 0));
    }
    public static string LeewayName(int leeway) => leeway switch { 3 => "a great name", 2 => "a name the Senate knows", 1 => "a known commander", _ => "nobody the Senate has heard of" };
    public static int DefyCost(int leeway) => DefyAtOnce[Math.Clamp(leeway, 0, 3)];
    public static int DefyYearlyCost(int leeway) => DefyAYear[Math.Clamp(leeway, 0, 3)];
    /// <summary>How the benches lean on recalling a general who fights on against a peace: hard against a nobody, not at all against a
    /// name, and the other way for a great one.</summary>
    public static int RecallLean(World w, House h) => RecallLeanFor[Math.Clamp(Leeway(w, h), 0, 3)];

    /// <summary>The price of not listening, at once or for another year: the house's standing, and its crews, who did not sign on to
    /// fight the Senate for a man nobody has heard of. Crews devoted to their commander do not waver.</summary>
    private static void Defiance(World w, House h, bool yearly)
    {
        int leeway = Leeway(w, h);
        h.Legitimacy = Math.Max(0, h.Legitimacy - (yearly ? DefyYearlyCost(leeway) : DefyCost(leeway)));
        int waver = (3 - leeway) * CrewsWaver / (yearly ? 2 : 1);
        if (waver > 0) foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == h.Id && !f.Office && f.Devotion < CaesarSystem.DevotedAt)) f.Loyalty = Math.Max(0, f.Loyalty - waver);
    }

    /// <summary>A peace the Senate voted is made. Whoever is ahead keeps what they hold; no tribute is paid and no triumph is voted.</summary>
    private static void MakePeace(World w, Nation n, NationDef d, string how)
    {
        w.WorldFlags.Add("foreign_peace"); Bump(w, PeaceMadeCounter, n); Mark(w, "peace_made", n); n.Disposition = Math.Min(100, n.Disposition + PeaceRegard);
        w.Log($"{how}: the Senate's peace with {d.Name} is made.");
        Unforced(w, n, d);
        EndWar(w, n, d);
    }

    // ---- kinds of peace (the owner: "it could be a peace where you pay tribute, or where you keep the territory, or even where they
    // become a vassal state. It really depends on the war and what is achieved in the war") ----

    /// <summary>Whether the nation holds a world of the Reach's that it took in this war.</summary>
    public static bool Holds(World w, Nation n) => n.OnMap && w.Holdings.Values.Any(x => x.IsPlanet && x.OwnerId == n.HouseId && x.FormerOwnerId.Length > 0 && w.OfTheReach(x.FormerOwnerId));
    public static int ClientShare(NationDef d) => Math.Max(1, d.Tribute / 2);

    /// <summary>Whether a war won ends with the nation made a client. It must be beaten decisively: its capital in the Reach's hands,
    /// or the tally at twice what makes it sue, or three in four of its hulls gone. And the house that won it must want a client: other
    /// houses always do (they raid, and hold nothing); the player's does when the player has said so, and otherwise keeps what it took.</summary>
    public static bool Client(World w, Nation n, NationDef d, House? top, string? mine)
    {
        if (!n.OnMap || n.Fallen || n.Vassal) return false;
        // the player's house keeps what it took unless the player has said otherwise: when the war is its to win, or it holds one of the nation's worlds
        if (mine != null && !w.HasFlag(PlayerClientsFlag) && (top?.Id == mine || d.Worlds.Any(x => w.Holding(x.Id)?.OwnerId == mine))) return false;
        return Decisive(w, n, d);
    }
    public static bool Decisive(World w, Nation n, NationDef d) =>
        n.OnMap && ((w.PlanetHolder(n.CapitalSystemId) is { } seat && w.OfTheReach(seat)) || n.ScoreReach - n.ScoreNation >= 2 * SuesAt(n, d) || n.Strength * 4 <= Full(n, d));

    // ---- negotiated or won (the owner: "Peace can be negotiated or won. Depends on war score") ----

    public const int TermsTerritory = 25, TermsTribute = 60, TermsYears = 10, TermsPaidYears = 5, ParleyAfter = 4;
    public const string NegotiatedCounter = "peaces_negotiated", ParleyCounter = "own_wars_negotiated";
    /// <summary>The terms a peace can be negotiated on, worst for the Reach first.</summary>
    public static readonly string[] Terms = { "paid", "ceded", "white", "territory", "tribute" };

    /// <summary>The war score, from a hundred against the Reach to a hundred for it: how far the tally is toward the lead that ends the
    /// war. At a hundred they sue and the peace is won; at a hundred against, the Reach is beaten. Between, a peace is negotiated.</summary>
    public static int WarScore(Nation n, NationDef d)
    {
        int lead = n.ScoreReach - n.ScoreNation;
        return Math.Clamp(lead >= 0 ? lead * 100 / Math.Max(1, SuesAt(n, d)) : lead * 100 / Math.Max(1, d.Threshold), -100, 100);
    }
    /// <summary>How much more a ruler wants before agreeing: one of the great a quarter of the scale, a warlike ruler fifteen points.</summary>
    public static int Stubborn(Nation n) => Great(n) ? 25 : n.Ruler?.Nature == "warlike" ? 15 : 0;
    /// <summary>The war score at which the nation agrees to these terms.</summary>
    public static int Needs(Nation n, string terms) => terms switch
    {
        "tribute" => TermsTribute + Stubborn(n),             // they pay for ten years, and the Reach keeps what it holds
        "territory" => TermsTerritory + Stubborn(n),         // the Reach keeps what it holds
        "white" => 1 - TermsTerritory + Stubborn(n),         // everything goes home
        "ceded" => 1 - TermsTribute + Stubborn(n),           // they keep what they hold
        _ => -100                                            // they keep what they hold, and are paid for five years
    };
    /// <summary>The best terms the nation will agree to at this war score. A war that gutters out is not negotiated: it gets no tribute either way.</summary>
    public static string Best(Nation n, NationDef d, bool negotiated = true)
    {
        int score = WarScore(n, d); string best = Terms.Last(t => Needs(n, t) <= score);
        return negotiated ? best : best == "tribute" ? "territory" : best == "paid" ? "ceded" : best;
    }
    public static string TermsName(string terms) => terms switch
    {
        "tribute" => $"they pay tribute for {TermsYears} years and the Reach keeps what it holds", "territory" => "the Reach keeps what it holds, and has its own back",
        "white" => "a white peace: everything goes home", "ceded" => "they keep what they hold", _ => $"they keep what they hold and are paid tribute for {TermsPaidYears} years"
    };

    /// <summary>A peace is made on these terms. <paramref name="with"/>: the house whose own war it was, which is paid or pays instead of the chair.</summary>
    private static void Settle(World w, Nation n, NationDef d, string terms, string with)
    {
        n.LastPeace = terms; n.TreatyWith = with; Bump(w, PeaceKindCounter + ":" + terms, n);
        string who = with.Length > 0 ? w.House(with)?.Name ?? "the house whose war it was" : "the Reach";
        switch (terms)
        {
            case "tribute": n.SubduedUntil = w.Turn + TermsYears; w.Log($"Terms with {d.Name}: it pays {who} {d.Tribute} credits a year for {TermsYears} years; each house keeps what it holds of its worlds, and what it holds of the Reach's goes home."); PeaceOnMap(w, n, d, reachWon: true); break;
            case "territory": w.Log($"Terms with {d.Name}: each house keeps what it holds of its worlds, and what it holds of the Reach's goes home. Nobody pays tribute."); PeaceOnMap(w, n, d, reachWon: true); break;
            case "white": Mark(w, "white_peace", n); w.Log($"A white peace with {d.Name}: every world goes back to whoever had it before the war, and nobody pays anybody."); PeaceOnMap(w, n, d, reachWon: true, allBack: true); break;
            case "ceded": w.Log($"Terms with {d.Name}: what it holds of the Reach's is ceded, and what the Reach holds of its worlds is given up. Nobody pays tribute."); PeaceOnMap(w, n, d, reachWon: false); break;
            default: n.PaidUntil = w.Turn + TermsPaidYears; Mark(w, "reach_pays_tribute", n); w.Log($"Terms with {d.Name}: what it holds of the Reach's is ceded, and {who} pays it {d.Tribute} credits a year for {TermsPaidYears} years."); PeaceOnMap(w, n, d, reachWon: false); break;
        }
    }

    /// <summary>A peace nobody forced: the Senate voted it and it is negotiated, on the best terms the war score allows; or the war
    /// guttered out, and each side keeps what the score gives it with no tribute either way.</summary>
    private static void Unforced(World w, Nation n, NationDef d, bool negotiated = true)
    {
        if (negotiated) Bump(w, NegotiatedCounter, n);
        Settle(w, n, d, Best(n, d, negotiated), n.WarOf);
    }

    public static string? NegotiateRefusal(World w, NationCatalog catalog, House? house, string nationId, string terms) => NegotiateRefusal(w, w.Nation(nationId), catalog.Def(nationId), house, terms);
    private static string? NegotiateRefusal(World w, Nation? n, NationDef? d, House? house, string terms)
    {
        if (n == null || d == null) return "no such nation";
        if (house == null || !n.AtWar) return $"you are not at war with {d.Name}";
        if (n.WarOf != house.Id) return n.WarOf.Length > 0 ? "it is another house's war" : "it is the Reach's war: its peace is the Senate's to vote";
        if (!Terms.Contains(terms)) return "no such terms";
        int score = WarScore(n, d), needs = Needs(n, terms);
        return score >= needs ? null : $"they will not hear of it: the war score is {score:+0;-0;0}, and that wants {needs:+0;-0;0}";
    }

    /// <summary>A house makes its own peace in its own war, on terms the war score allows. No vote: it was never the Senate's war. If
    /// the Senate had voted peace and the house fought on, this ends that too.</summary>
    public static void Negotiate(World w, NationCatalog catalog, string nationId, string houseId, string terms) => Negotiate(w, w.Nation(nationId), catalog.Def(nationId), houseId, terms);
    private static void Negotiate(World w, Nation? n, NationDef? d, string houseId, string terms)
    {
        var house = w.House(houseId); if (n == null || d == null || NegotiateRefusal(w, n, d, house, terms) != null) return;
        Bump(w, NegotiatedCounter, n); Bump(w, ParleyCounter, n); Mark(w, houseId == w.Protagonist?.HouseId ? "peace_negotiated_by_us" : "peace_negotiated", n); w.WorldFlags.Add("foreign_peace");
        n.Disposition = Math.Min(100, n.Disposition + PeaceRegard);
        w.Log($"{house!.Name} makes its own peace with {d.Name}.");
        Settle(w, n, d, terms, houseId); EndWar(w, n, d);
    }

    /// <summary>Another house, in a war of its own, makes its own peace: at once when tribute is on the table or the war is going
    /// against it; after four years, on whatever the score allows. True if the war ended.</summary>
    private static bool Parley(World w, Nation n, NationDef d, string? mine)
    {
        if (n.WarOf.Length == 0 || n.WarOf == mine || n.PeaceVoted > 0) return false;
        string best = Best(n, d); int score = WarScore(n, d);
        if (best != "tribute" && score > -TermsTerritory && w.Turn - n.WarSince < ParleyAfter) return false;
        Negotiate(w, n, d, n.WarOf, best); return !n.AtWar;
    }

    /// <summary>The war score and what it buys, in a line or two: for the nation's card.</summary>
    public static string PeaceNow(World w, Nation n, NationDef d, string? mine)
    {
        if (!n.AtWar) return "";
        int score = WarScore(n, d); string best = Best(n, d); int sues = SuesAt(n, d);
        string next = Terms.SkipWhile(t => t != best).Skip(1).FirstOrDefault() is { } better ? $" At {Needs(n, better):+0;-0;0}: {TermsName(better)}." : "";
        string stubborn = Stubborn(n) > 0 ? $" ({(Great(n) ? "One of the great" : "A warlike ruler")} asks {Stubborn(n)} points more.)" : "";
        string win = n.OnMap && Decisive(w, n, d) ? "it can be made a client" : $"it sues, and pays tribute for {SubduedTurns} years{(n.OnMap ? ", or is made a client if it is beaten decisively" : "")}";
        string lose = n.OnMap ? (Holds(w, n) ? "the war is lost and what it holds is ceded" : $"the war is lost and the Reach pays it tribute for {PaidYears} years") : "the chair buys peace";
        return $"War score {score:+0;-0;0} (the tally is {n.ScoreReach} to {n.ScoreNation}; {sues} ahead wins it, {d.Threshold} behind loses it). Negotiated now: {TermsName(best)}.{next}{stubborn} Won at +100: {win}. Lost at -100: {lose}.";
    }

    /// <summary>A client's year: it pays the chair, and half as much again to its patron. It throws the treaty off under one of the
    /// great at once; under a warlike ruler with its fleets rebuilt, one year in twelve, or one in three while the Reach is divided.</summary>
    private static void Clientage(World w, Nation n, NationDef d)
    {
        var chair = RegionSystem.CapitalHolder(w); var patron = n.PatronId.Length > 0 && w.OfTheReach(n.PatronId) ? w.House(n.PatronId) : null;
        bool rebuilt = n.Strength * 100 >= Full(n, d) * StrikeFit;
        bool breaks = Great(n) || (n.Ruler?.Nature == "warlike" && rebuilt && Hash($"client:{n.Salt}:{n.Id}:{w.Turn}") / 7 % (uint)(Divided(w) != null ? ClientBreakDivided : ByBoldness(n, ClientBreakOneIn)) == 0);
        if (breaks)
        {
            n.Vassal = false; n.PatronId = ""; n.SubduedUntil = 0; n.Disposition = Math.Max(-100, n.Disposition - 20); Bump(w, ClientLostCounter, n); Mark(w, "client_breaks", n);
            w.Log($"{d.Name} throws off the treaty: {Styled(d, n.Ruler, true)} sends the Reach's envoys home, and pays nobody. {(patron != null ? $"{patron.Name} has lost a client." : "")}");
            return;
        }
        if (chair != null) { chair.Treasury.Credits += d.Tribute; w.WorldCounters[TributeCounter] = w.Counter(TributeCounter) + d.Tribute; }
        if (patron != null) { patron.Treasury.Credits += ClientShare(d); w.WorldCounters[TributeCounter] = w.Counter(TributeCounter) + ClientShare(d); }
    }

    /// <summary>A house that fought on comes home (CaesarSystem.Obey, when its general lays down the command): the peace is made at the next reckoning.</summary>
    public static void CameHome(World w, string houseId)
    {
        if (DefiedBy(w, houseId) is { } n) w.WorldCounters[ComeHomeKey(n)] = 1;
    }
}
