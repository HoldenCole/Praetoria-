using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The consulship (docs/REPUBLIC.md, stage 2). Every <see cref="Every"/> turns the Senate elects the two consuls — the elected offices of
/// <c>content/senate/offices.json</c>. Each party nominates its weightiest member with a qualified candidate who was not consul last term;
/// the player may <b>stand</b> (StandForConsulCommand) and runs as their party's nominee, or alone. Every house's weight goes to its party's
/// nominee; a house with no party votes for the nominee its head likes best. The two highest tallies take the senior and junior chairs. A
/// consul may <b>veto</b> one open motion a term (VetoCommand; rival consuls veto what strikes at their house or their party). Flags:
/// <c>election_near</c>, <c>election_held</c>, <c>consul_elected</c>, <c>consul_lost</c>, <c>vetoed_mine</c>; counters <c>elections</c>,
/// <c>consul_term:&lt;house&gt;</c> (the turn last elected), <c>veto_used:&lt;house&gt;</c> (the turn of the last veto).
/// </summary>
public static class ElectionSystem
{
    public const int Every = 10, StandInfluence = 2;
    public static readonly string[] TurnFlags = { "election_held", "consul_elected", "consul_lost", "vetoed_mine", CourtFlag };
    public static string StandFlag(string houseId) => "stands_for_consul:" + houseId;
    public const string OwedKey = "election_owed";
    /// <summary>Every tenth year, or the year after emergency powers under which one fell due are laid down. Never while they run.</summary>
    public static bool Due(World w)
    {
        if (w.Turn <= 0 || w.Era == "imperium") return false;
        if (w.Emergency is { } e) { if (w.Turn % Every == 0) e.SkippedElection = true; return false; }
        return w.Turn % Every == 0 || w.Counter(OwedKey) > 0;
    }
    /// <summary>For a screen, in the year being played: years until the consuls are elected. Nought in the year it is held at the end of.</summary>
    public static int CountIn(World w) => (Every - w.Turn % Every) % Every;
    public static int NextIn(World w) => w.Turn % Every == 0 ? Every : Every - w.Turn % Every;
    public static IEnumerable<OfficeDef> ConsulOffices(OfficeCatalog offices) => offices.Defs.Where(d => d.Elected).OrderByDescending(d => d.Weight).ThenBy(d => d.Id, StringComparer.Ordinal);
    public static bool IsConsul(World w, OfficeCatalog offices, string houseId) => ConsulOffices(offices).Any(d => OfficeSystem.Holds(w, houseId, d.Id));
    /// <summary>Eligible: a qualified member for the office, and not elected at the last election.</summary>
    public static bool Eligible(World w, OfficeCatalog offices, string houseId) =>
        !CaesarSystem.Enemy(w, houseId) && PolitySystem.InReach(w, houseId) && (!World.Outsider(houseId) && w.House(houseId) != null && ConsulOffices(offices).FirstOrDefault() is { } d && OfficeSystem.Candidate(w, d, houseId) != null && w.Turn - w.Counter("consul_term:" + houseId) >= Every * 2 - 1 || (w.Counter("consul_term:" + houseId) == 0 && !World.Outsider(houseId) && w.House(houseId) != null && ConsulOffices(offices).FirstOrDefault() is { } d2 && OfficeSystem.Candidate(w, d2, houseId) != null));

    /// <summary>The nominees as they stand: one a party (the weightiest eligible member, or the player if standing), plus the player alone if unaffiliated and standing.</summary>
    public static List<(string houseId, string? partyId)> Nominees(World w, OfficeCatalog offices, Func<string, int> weight)
    {
        var list = new List<(string, string?)>(); var mine = w.Protagonist?.HouseId;
        bool mineStands = mine != null && w.HasFlag(StandFlag(mine)) && Eligible(w, offices, mine);
        foreach (var f in w.Factions.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            // the player's house, standing, is its bench's nominee only if the bench will have it (see BenchContest); otherwise the
            // weightiest eligible member is, as when the player does not stand. A house that has pledged its seats to the player's,
            // standing, does not stand against it, on its own bench or any other (AskAbove: "they are counted for you")
            bool mineHere = mineStands && f.Members.Contains(mine!);
            string? pick = mineHere && BenchContest(w, offices, weight, f) is { won: true } ? mine
                : f.Members.Where(h => Eligible(w, offices, h) && h != mine && !(mineStands && PledgedTo(w, h) == mine)).OrderByDescending(weight).ThenBy(h => h, StringComparer.Ordinal).FirstOrDefault();
            if (pick != null) list.Add((pick, f.Id));
        }
        if (mine != null && w.HasFlag(StandFlag(mine)) && Eligible(w, offices, mine) && !list.Any(x => x.Item1 == mine)) list.Add((mine, null));
        return list;
    }

    // ---------------- the nomination inside a bench (docs/PLAYTHROUGH.md, Medium, The Senate, P87) ----------------

    /// <summary>How the bench stands between the player's house, which has declared, and the member it would otherwise put forward.
    /// A bench's leader is its nominee when they stand. Otherwise each other member backs whichever of the two its head thinks
    /// the better of (the weightier, where it thinks the same of both), with its seats; each of the two has its own. The player's
    /// house is the nominee if it has at least as many seats of the bench behind it. Null where there is no contest: the player's
    /// house is not of the bench, or nobody else of it could stand.</summary>
    public static (bool won, string rivalId, int mine, int rival, List<string> forMine, List<string> forRival)? BenchContest(World w, OfficeCatalog offices, Func<string, int> weight, Faction f)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null || !f.Members.Contains(mine)) return null;
        // (a member who has pledged its seats to the player's house does not stand against it, and backs it)
        string? rival = f.Members.Where(h => h != mine && Eligible(w, offices, h) && PledgedTo(w, h) != mine).OrderByDescending(weight).ThenBy(h => h, StringComparer.Ordinal).FirstOrDefault();
        if (rival == null) return (true, "", weight(mine), 0, new(), new());
        var forMine = new List<string>(); var forRival = new List<string>(); int a = weight(mine), b = weight(rival);
        if (f.LeaderHouseId == mine) return (true, rival, a, b, forMine, forRival);
        var myHead = w.House(mine) is { } mh ? Economy.HeadOf(w, mh) : null; var rivalHead = w.House(rival) is { } rh ? Economy.HeadOf(w, rh) : null;
        foreach (var m in f.Members.Where(m => m != mine && m != rival).OrderBy(m => m, StringComparer.Ordinal))
        {
            var head = w.House(m) is { } hm ? Economy.HeadOf(w, hm) : null; if (head == null) continue;
            int toMine = myHead != null ? w.GetRelationship(head.Id, myHead.Id)?.Disposition ?? 0 : 0, toRival = rivalHead != null ? w.GetRelationship(head.Id, rivalHead.Id)?.Disposition ?? 0 : 0;
            bool mineIt = PledgedTo(w, m) == mine || toMine > toRival || (toMine == toRival && weight(mine) >= weight(rival));
            if (mineIt) { forMine.Add(m); a += weight(m); } else { forRival.Add(m); b += weight(m); }
        }
        return (a >= b, rival, a, b, forMine, forRival);
    }

    // ---------------- a pledge of votes (docs/PLAYTHROUGH.md, Medium, The Senate, P59) ----------------
    // A house that is not standing may pledge its seats to a nominee: they are counted for that nominee, whatever its bench or its
    // patron would have had. The nominee's head thinks the better of the house at once, and if the nominee is elected with the
    // pledge kept, owes it a favour. Pledging away from the house's own bench is seen by the bench's nominee. A pledge may be
    // taken back before the count, at a cost with the one it was given to.

    public const string PledgePrefix = "consul_pledge:";
    public const int PledgeRegard = 8, UnpledgeRegard = 10, BenchSlight = 5;
    /// <summary>The nominee a house has pledged its seats to, or null.</summary>
    public static string? PledgedTo(World w, string houseId)
    {
        string lead = PledgePrefix + houseId + ">";
        foreach (var f in w.WorldFlags) if (f.StartsWith(lead, StringComparison.Ordinal)) return f[lead.Length..];
        return null;
    }
    public static string? PledgeRefusal(World w, OfficeCatalog offices, Func<string, int> weight, House? by, string toId)
    {
        if (by == null || World.Outsider(by.Id)) return "no house";
        string? now = PledgedTo(w, by.Id);
        if (toId.Length == 0) return now != null ? null : "you have pledged your seats to nobody";
        if (w.HasFlag(StandFlag(by.Id))) return "you are standing yourself: your seats are your own";
        var to = w.House(toId); if (to == null || toId == by.Id) return "no such nominee";
        if (!Nominees(w, offices, weight).Any(n => n.houseId == toId)) return $"{to.Name} is not standing";
        if (now == toId) return "your seats are pledged to them already";
        if (now != null) return $"your seats are pledged to {w.House(now)?.Name ?? "another"}: take that back first";
        return w.AtWar(by.Id, toId) ? "you are at war with them" : null;
    }
    private static void Think(World w, string ofHouseId, string aboutHouseId, int by)
    {
        var a = w.House(ofHouseId) is { } ha ? Economy.HeadOf(w, ha) : null; var b = w.House(aboutHouseId) is { } hb ? Economy.HeadOf(w, hb) : null;
        if (a == null || b == null || a.Id == b.Id) return; var r = w.Relationship(a.Id, b.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100);
    }
    /// <summary>The nominee of a house's own bench, if it has one and it is another house.</summary>
    public static string? BenchNominee(World w, OfficeCatalog offices, Func<string, int> weight, string houseId) =>
        FactionSystem.Of(w, houseId) is { } bench && Nominees(w, offices, weight).FirstOrDefault(n => n.partyId == bench.Id).houseId is { } pick && pick != houseId ? pick : null;
    /// <summary>What a pledge to them would do, said before it is given.</summary>
    public static string PledgeSays(World w, OfficeCatalog offices, Func<string, int> weight, House by, string toId)
    {
        var to = w.House(toId); string? own = BenchNominee(w, offices, weight, by.Id);
        return $"Your {Counted.Seats(weight(by.Id))} {Counted.Are(weight(by.Id))} counted for {to?.Name}, whatever your bench or your patron would have had. Its head thinks {PledgeRegard} the better of you now, and owes you a favour if it is elected with your pledge kept."
            + (own != null && own != toId ? $" {w.House(own)?.Name}, whom your bench puts forward, thinks {BenchSlight} the worse of you for it." : "")
            + $" Taken back before the count, its head thinks {UnpledgeRegard} the worse of you.";
    }
    /// <summary>Pledge the house's seats to a nominee, or (empty) take the pledge back.</summary>
    public static void Pledge(World w, OfficeCatalog offices, Func<string, int> weight, House by, string toId)
    {
        string? now = PledgedTo(w, by.Id);
        if (toId.Length == 0)
        {
            if (now == null) return; w.WorldFlags.Remove(PledgePrefix + by.Id + ">" + now); Think(w, now, by.Id, -UnpledgeRegard);
            w.Log($"{by.Name} takes back the seats it had pledged to {w.House(now)?.Name}."); return;
        }
        string? own = BenchNominee(w, offices, weight, by.Id);
        w.WorldFlags.Add(PledgePrefix + by.Id + ">" + toId); Think(w, toId, by.Id, PledgeRegard);
        if (own != null && own != toId) Think(w, own, by.Id, -BenchSlight);
        w.WorldCounters["consul_pledges"] = w.Counter("consul_pledges") + 1;
        w.Log($"{by.Name} pledges its seats to {w.House(toId)?.Name} for the consulship.");
    }

    /// <summary>Each house's weight to its party's nominee; an unaffiliated house to the nominee its head likes best (ties by weight).</summary>
    public static Dictionary<string, int> Tally(World w, OfficeCatalog offices, Func<string, int> weight, List<(string houseId, string? partyId)> nominees)
    {
        var tally = nominees.ToDictionary(n => n.houseId, _ => 0);
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id)))
        {
            var party = FactionSystem.Of(w, h.Id); string? vote = null;
            if (nominees.Any(n => n.houseId == h.Id)) vote = h.Id;
            else if (PledgedTo(w, h.Id) is { } pledged && nominees.Any(n => n.houseId == pledged)) vote = pledged;   // a pledge of its seats, kept
            else if (h.PatronId.Length > 0 && nominees.Any(n => n.houseId == h.PatronId)) vote = h.PatronId;   // a client votes for the patron, whatever the party says
            else if (party != null) vote = nominees.FirstOrDefault(n => n.partyId == party.Id).houseId;
            if (vote == null && nominees.Count > 0)
            {
                var head = Economy.HeadOf(w, h);
                vote = nominees.OrderByDescending(n => head != null && w.House(n.houseId) is { } nh && Economy.HeadOf(w, nh) is { } nhead ? w.GetRelationship(head.Id, nhead.Id)?.Disposition ?? 0 : 0).ThenByDescending(n => weight(n.houseId)).ThenBy(n => n.houseId, StringComparer.Ordinal).First().houseId;
            }
            if (vote != null) tally[vote] = tally.GetValueOrDefault(vote) + weight(h.Id);
        }
        foreach (var n in nominees) tally[n.houseId] = tally.GetValueOrDefault(n.houseId) + w.Counter(OrderSystem.CanvassKey(n.houseId)) - LadderSystem.TaintCost(w, n.houseId, "consul") + LifeSystem.TieVotes(w, n.houseId) + (w.Protagonist is { Alive: true } me && me.HouseId == n.houseId ? LadderSystem.AttendanceVotes(w, me) : 0);   // coin spent on the voters in the nominee's name; and what a scandal in office costs the house (docs/LADDER.md)
        return tally;
    }

    public static void Hold(World w, OfficeCatalog offices, Func<string, int> weight)
    {
        var consuls = ConsulOffices(offices).ToList(); if (consuls.Count == 0) return;
        var mine = w.Protagonist?.HouseId; w.WorldCounters.Remove(OwedKey);
        TermEnds(w, offices);   // a new term: what the last consuls promised the player lapses with their chairs, and the player is told
        var nominees = Nominees(w, offices, weight); var tally = Tally(w, offices, weight, nominees);
        var winners = tally.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(consuls.Count).ToList();
        foreach (var d in consuls) if (OfficeSystem.Holding(w, d.Id) is { } old) { if (old.HouseId == mine) w.WorldFlags.Add("consul_lost"); OfficeSystem.Strip(w, d, "the term ends", false); }
        for (int i = 0; i < winners.Count; i++)
        {
            var who = OfficeSystem.Candidate(w, consuls[i], winners[i].Key); if (who == null) continue;
            OfficeSystem.Grant(w, consuls[i], winners[i].Key, who);
            w.WorldCounters["consul_term:" + winners[i].Key] = w.Turn;
            if (winners[i].Key == mine) { w.WorldFlags.Add("consul_elected"); w.WorldFlags.Remove("consul_lost"); }
        }
        // a pledge kept to one who was elected is a favour owed
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith(PledgePrefix, StringComparison.Ordinal)).ToList())
        {
            var parts = f[PledgePrefix.Length..].Split('>'); w.WorldFlags.Remove(f);
            if (parts.Length == 2 && winners.Any(x => x.Key == parts[1]) && w.House(parts[0]) != null && !nominees.Any(n => n.houseId == parts[0])) { WealthSystem.OweFor(w, parts[1], parts[0]); if (parts[0] == mine) w.Tell("good", $"{w.House(parts[1])?.Name} is elected with the seats you pledged, and owes your house a favour.", "senate"); }
            // the one the player pledged to no longer stood at the count: the seats went as the bench or the patron had them, and the pledge lapsed
            else if (parts.Length == 2 && parts[0] == mine && !nominees.Any(n => n.houseId == parts[1]) && w.House(parts[1]) is { } dropped)
                w.Tell("info", $"{dropped.Name} was not standing when the consuls were elected, so the seats you pledged it went as your bench or your patron had them, and it owes you nothing for them.", "senate", dropped.Id);
        }
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("stands_for_consul:")).ToList()) w.WorldFlags.Remove(f);
        OrderSystem.SweepCanvass(w);
        w.WorldCounters["elections"] = w.Counter("elections") + 1; w.WorldFlags.Add("election_held"); w.WorldCounters["mark:election_held"] = w.Turn;   // marked, so the year's reports of it come in its year (docs/events/votes.md)
        w.Log($"The Senate elects its consuls: {string.Join(", ", winners.Select((kv, i) => $"{w.House(kv.Key)?.Name ?? kv.Key} ({kv.Value}) as {consuls[Math.Min(i, consuls.Count - 1)].Name}"))}.{(nominees.Count > winners.Count ? " Also standing: " + string.Join(", ", tally.Where(kv => winners.All(x => x.Key != kv.Key)).OrderByDescending(kv => kv.Value).Select(kv => $"{w.House(kv.Key)?.Name ?? kv.Key} ({kv.Value})")) + "." : "")}");
    }

    // ---------------- what a consul promises the player (docs/events/votes.md, "Promises", the office group) ----------------
    // Each is a mark on the consul as a person, kept while they hold the chair in this term: a consul at whose table you were seen
    // spares one motion of yours the veto they would have struck it with; a colleague who divided the consulship's business with you
    // neither vetoes your motions nor speaks against them from the chair; the other consul you courted after a veto strikes, once, a
    // motion against your house. Each lapses, with a notice, when the term ends, when the consul leaves the chair, or when the two
    // houses are at war. RNG-free.

    /// <summary>A consul at whose table the player was seen at the start of the term (the_consul_elects_feast): once in the term they
    /// let a motion of the player's house stand that they would have struck with the veto.</summary>
    public const string DinedChar = "dined_with_you";
    /// <summary>The player's colleague in the consulship, who divided its business with the player (your_colleague_the_consul): for the
    /// term they neither veto a motion of the player's house nor speak against it from the chair, until the player strikes theirs.</summary>
    public const string DividedChar = "divided_the_year";
    /// <summary>The other consul, courted after a veto struck the player's motion (the_veto_falls): once in the term they strike a
    /// motion against the player's house with their own veto.</summary>
    public const string FriendChar = "a_friend_in_the_chair";
    /// <summary>The consul who did not strike the player's motion, of a third house, with a veto unspent this term; and the world flag
    /// that opens the choice to court them. Both until the next session. The choice sets <see cref="CourtedFlag"/>.</summary>
    public const string OtherConsulChar = "the_other_consul", CourtFlag = "other_consul_to_court", CourtedFlag = "courted_the_other_consul";
    /// <summary>What a colleague thinks the worse of the player for striking their business after dividing it: a word broken, as a pledge broken is.</summary>
    public const int DivisionBroken = DealingsSystem.PledgeBrokenRegard;
    private static readonly string[] Promises = { DinedChar, DividedChar, FriendChar };

    /// <summary>Whether this person holds a consul's chair now.</summary>
    private static bool InTheChair(World w, OfficeCatalog offices, Character c) => c.Alive && ConsulOffices(offices).Any(d => OfficeSystem.Holding(w, d.Id) is { } h && h.CharacterId == c.Id);
    private static string Promised(string flag) => flag switch
    {
        DinedChar => "the motion they would have spared you for coming to their table",
        DividedChar => "the division of the consulship's business you made with them",
        _ => "the veto they would have used for you"
    };
    /// <summary>Before the session: a consul's promise lapses if they no longer hold the chair, or if their house and the player's are at war.</summary>
    public static void Lapses(World w, OfficeCatalog offices)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null) return;
        foreach (var c in w.Characters.Values.Where(c => Promises.Any(c.Flags.Contains)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            string? why = !InTheChair(w, offices, c) ? $"{c.Name} no longer holds the chair" : w.AtWar(c.HouseId, mine) ? $"{w.House(c.HouseId)?.Name ?? c.Name} is at war with your house" : null;
            if (why == null) continue;
            foreach (var f in Promises.Where(c.Flags.Remove)) w.Tell("info", $"{why}, and {Promised(f)} goes with it.", "senate:chamber", c.HouseId);
        }
    }
    /// <summary>At the election: what the last term's consuls promised the player lapses with the term, and the player is told.</summary>
    private static void TermEnds(World w, OfficeCatalog offices)
    {
        foreach (var c in w.Characters.Values.Where(c => Promises.Any(c.Flags.Contains)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
            foreach (var f in Promises.Where(c.Flags.Remove)) if (c.Alive) w.Tell("info", $"{c.Name}'s term as consul is over, and {Promised(f)} ends with it.", "senate", c.HouseId);
    }
    /// <summary>The choice to court the other consul was made after the veto (it sets <see cref="CourtedFlag"/>): the one marked as the
    /// other consul is the player's friend in the chair. Then the marks go. Before the session's flags are cleared.</summary>
    public static void Courted(World w)
    {
        bool courted = w.WorldFlags.Remove(CourtedFlag);
        foreach (var c in w.Characters.Values.Where(c => c.Flags.Remove(OtherConsulChar)).ToList()) if (courted && c.Alive) c.Flags.Add(FriendChar);
    }
    /// <summary>The player struck a motion of the colleague who divided the consulship's business with them, with the veto or from the
    /// chair: the division is broken, and the colleague is free to strike the player's.</summary>
    public static void BreakDivision(World w, Motion m, string byHouseId)
    {
        var mine = w.Protagonist; if (mine == null || byHouseId != mine.HouseId || m.ProposerHouseId == mine.HouseId) return;
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == m.ProposerHouseId && c.Flags.Remove(DividedChar)).ToList())
        {
            var r = w.Relationship(c.Id, mine.Id); r.Disposition = Math.Clamp(r.Disposition - DivisionBroken, -100, 100);
            w.Tell("warn", $"You struck the business of {c.Name}, and the division of the consulship you made with them is broken: they think {-DivisionBroken:+0;-0} of you, and are free to strike yours.", "senate:chamber", c.HouseId);
            w.Log($"{c.Name} sees their business struck by their colleague, and holds the division of the consulship broken.");
        }
    }
    /// <summary>The colleague who divided the consulship's business with the player, if they have moved this motion.</summary>
    public static Character? Divided(World w, Motion m) => w.Protagonist?.HouseId is { } mine && m.ProposerHouseId != mine ? w.LivingCharacters().FirstOrDefault(c => c.HouseId == m.ProposerHouseId && c.Flags.Contains(DividedChar)) : null;

    /// <summary>A consul's veto: the motion is struck before the vote. Once a term.</summary>
    public static bool MayVeto(World w, OfficeCatalog offices, string houseId) => EmergencySystem.MayVeto(w, houseId) ??
        ConsulOffices(offices).Any(d => d.Veto && OfficeSystem.Holds(w, houseId, d.Id)) && !VetoSpent(w, offices, houseId);
    private static bool VetoSpent(World w, OfficeCatalog offices, string houseId) => w.Counter("veto_used:" + houseId) == TermStart(w, offices, houseId);
    /// <summary>A term's mark: the turn the chair was taken plus one, so a veto is remembered against the term it was spent in.</summary>
    public static int TermStart(World w, OfficeCatalog offices, string houseId) => ConsulOffices(offices).Select(d => OfficeSystem.Holding(w, d.Id)).FirstOrDefault(h => h?.HouseId == houseId)?.SinceTurn + 1 ?? int.MaxValue;
    public static void Veto(World w, OfficeCatalog offices, string houseId, Motion m)
    {
        m.Resolved = true; m.Passed = false; m.ResolvedTurn = w.Turn; m.Vetoed = true; m.VetoedBy = houseId;
        DealingsSystem.Struck(w, m);   // a word given on it is void, and a favour owed its mover waits (docs/WEALTH.md)
        BreakDivision(w, m, houseId);   // the player struck the business of a colleague who had divided it with them
        if (m.ProposerHouseId == w.Protagonist?.HouseId) OtherConsul(w, offices, houseId);
        if (w.Emergency != null) { EmergencySystem.Vetoed(w, houseId); w.WorldCounters["vetoes"] = w.Counter("vetoes") + 1; if (m.ProposerHouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("vetoed_mine"); return; }
        w.WorldCounters["veto_used:" + houseId] = TermStart(w, offices, houseId); w.WorldCounters["vetoes"] = w.Counter("vetoes") + 1;
        if (m.ProposerHouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("vetoed_mine");
    }
    /// <summary>The player's motion was struck: the consul of a third house with a veto unspent this term is the other consul, who may be
    /// courted (the_veto_falls). Under emergency powers their veto is set aside, and would be used for the player once they are laid down.</summary>
    private static void OtherConsul(World w, OfficeCatalog offices, string byHouseId)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var d in ConsulOffices(offices).Where(d => d.Veto))
            if (OfficeSystem.Holding(w, d.Id) is { } h && h.HouseId != byHouseId && h.HouseId != mine && !VetoSpent(w, offices, h.HouseId) && w.Char(h.CharacterId) is { Alive: true } c && !c.Flags.Contains(FriendChar))
            { c.Flags.Add(OtherConsulChar); w.WorldFlags.Add(CourtFlag); }
    }

    /// <summary>Rival consuls veto what strikes at them: a motion against their house, their party's leader, or an Act that undoes their
    /// party's line. Before that, a consul who is the player's friend in the chair strikes a motion against the player's house; a consul
    /// who dined the player spares the player's motion once; a colleague who divided the business with the player leaves theirs alone.</summary>
    public static void RivalVetoes(World w, OfficeCatalog offices, SenateSystem senate)
    {
        var mine = w.Protagonist?.HouseId; Lapses(w, offices);
        foreach (var d in ConsulOffices(offices).Where(d => d.Veto))
        {
            var h = OfficeSystem.Holding(w, d.Id); if (h == null || h.HouseId == mine || !MayVeto(w, offices, h.HouseId)) continue;
            var consul = w.Char(h.CharacterId); var party = FactionSystem.Of(w, h.HouseId);
            var due = SenateSystem.Docket(w).Where(m => m.ProposedTurn < w.Turn && m.ProposerHouseId != h.HouseId).OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
            // a friend in one chair: the first motion before the Senate that strikes at the player's house is struck for it, and the promise is spent
            if (consul != null && consul.Flags.Contains(FriendChar) && due.FirstOrDefault(m => senate.Motions.Def(m.Kind) is { } fd && LadderSystem.Hostile(fd) && m.TargetId == mine) is { } foe)
            {
                consul.Flags.Remove(FriendChar); Veto(w, offices, h.HouseId, foe);
                w.Log($"{consul.Name}, as {d.Name}, vetoes: {senate.Describe(w, foe)} A friend in one chair is worth a veto in the other.");
                w.Tell("good", $"{consul.Name}, as {d.Name}, strikes the motion against your house with their veto, as a friend in the chair: {senate.Describe(w, foe)}", "senate:chamber", h.HouseId);
                continue;
            }
            foreach (var m in due)
            {
                var def = senate.Motions.Def(m.Kind); if (def == null) continue;
                bool against = def.Target == "house" && (def.LegitimacyTarget < 0 || def.EmbargoTurns > 0) && (m.TargetId == h.HouseId || (party != null && m.TargetId == party.LeaderHouseId));
                bool undoes = def.Law && party != null && party.Laws.TryGetValue(m.LawId, out var line) && line != m.LawOption && senate.Policies.Law(m.LawId) is { } ld && (PolicySystem.ReachOption(w, senate.Policies, ld) ?? ld.DefaultOption).Id == line;
                if (!against && !undoes) continue;
                if (m.ProposerHouseId == mine && consul != null && consul.Flags.Contains(DividedChar)) continue;   // the player's business, by the division: left alone, and nothing spent
                if (m.ProposerHouseId == mine && consul != null && consul.Flags.Remove(DinedChar))
                {
                    w.Log($"{consul.Name}, as {d.Name}, lets {w.House(mine!)?.Name ?? "your house"}'s motion stand unstruck. They remember who came to their table.");
                    w.Tell("good", $"{consul.Name}, as {d.Name}, lets your motion stand that they would have struck with the veto: {senate.Describe(w, m)} They remember who came to their table.", "senate:chamber", h.HouseId);
                    continue;
                }
                Veto(w, offices, h.HouseId, m);
                w.Log($"{w.House(h.HouseId)?.Name ?? h.HouseId}, as {d.Name}, vetoes: {senate.Describe(w, m)}");
                break;
            }
        }
    }
}
