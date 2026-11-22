using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// What a peace between two houses may ask, by how the war stands (docs/PLAYTHROUGH.md 9: "Sue for peace" was instant, showed no
/// terms, and at five to nought paid a credit or nothing; a world could not be asked for). A war's score is two for a battle won
/// and three for a world taken. The side ahead may ask nothing, ask tribute by the lead, or, ahead by <see cref="WorldLead"/>,
/// name a world; the side behind may pay tribute or give a world to be let go. Read only, apart from <see cref="Conclude"/>.
/// </summary>
public static class HousePeace
{
    public const string White = "white", Tribute = "tribute", Cede = "world";
    /// <summary>Ahead by this, a world may be asked for at the peace. (At <see cref="FleetSystem.CessionLead"/> one is given whatever the house behind would like.)</summary>
    public const int WorldLead = 5;
    /// <summary>What a house that has nothing in its treasury pays tribute in, in order, one for one.</summary>
    private static readonly string[] InLieu = { Resource.Materials, Resource.Helium3, Resource.RareEarths, Resource.Isotopes, Resource.Exotics };

    public sealed record Term(string Kind, string HoldingId, string Label, string What, bool Accepted, string Why);

    public static int TributeOwed(World w, FleetRules rules, string asker, string other) =>
        w.Wars.FirstOrDefault(x => x.Between(asker, other)) is { } war ? Math.Abs(war.Lead(asker)) * rules.TributePerScore : 0;

    /// <summary>What <paramref name="payer"/> could actually hand over of a tribute: credits first, then goods one for one.</summary>
    public static (int credits, List<(string good, int n)> goods, int unpaid) Payable(World w, string payer, int owed)
    {
        var t = w.House(payer)?.Treasury; var goods = new List<(string, int)>(); if (t == null) return (0, goods, owed);
        int credits = Math.Clamp(t.Credits, 0, owed), left = owed - credits;
        foreach (var g in InLieu) { if (left <= 0) break; int n = Math.Clamp(t.Get(g), 0, left); if (n > 0) { goods.Add((g, n)); left -= n; } }
        return (credits, goods, left);
    }
    private static string Paid((int credits, List<(string good, int n)> goods, int unpaid) p) =>
        string.Join(", ", new[] { p.credits > 0 ? p.credits + " credits" : "" }.Concat(p.goods.Select(g => g.n + " " + Resource.Label(g.good))).Where(x => x.Length > 0));

    /// <summary>The worlds of <paramref name="loser"/> that could change hands at a peace: never the capital of the Reach, never its last.</summary>
    public static List<Holding> Cedable(World w, string loser)
    {
        var worlds = w.HoldingsOf(loser).Where(h => h.IsPlanet).ToList(); if (worlds.Count <= 1) return new();
        return worlds.Where(h => !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderByDescending(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Every peace <paramref name="asker"/> could put to <paramref name="other"/> now, with what each would do and whether it would be taken.</summary>
    public static List<Term> Terms(World w, FleetSystem fleets, FleetRules rules, string asker, string other)
    {
        var terms = new List<Term>(); var war = w.Wars.FirstOrDefault(x => x.Between(asker, other)); if (war == null) return terms;
        if (ThroneSystem.ForTheThrone(w, asker, other)) { terms.Add(new Term(White, "", "Peace", "", false, "a war for a throne is not ended by treaty: one of the two sits on it at the end")); return terms; }
        string them = w.House(other)?.Name ?? other; int lead = war.Lead(asker), mine = FleetSystem.StrengthOf(w, asker), theirs = FleetSystem.StrengthOf(w, other);
        bool suing = w.LivingCharacters().Any(c => c.HouseId == other && c.Flags.Contains(FleetSystem.SuingForPeaceFlag));
        bool listens = suing || fleets.WouldAcceptPeace(w, other, asker);
        string deaf = $"{them} smells victory ({Counted.Hulls(theirs)} to your {mine}{(war.Lead(other) > 0 ? ", and ahead by " + war.Lead(other) : "")}): it will not hear of peace yet";
        int owed = Math.Abs(lead) * rules.TributePerScore;
        if (lead >= 0)
        {
            terms.Add(new Term(White, "", lead > 0 ? "Peace, and ask nothing" : "A white peace", "Each side keeps what it holds and nothing is paid." + (lead > 0 ? " " + them + " will remember that you asked nothing." : ""), listens, listens ? "" : deaf));
            if (lead > 0)
            {
                var p = Payable(w, other, owed); string got = Paid(p);
                terms.Add(new Term(Tribute, "", $"Peace for tribute ({owed})", $"{them} owes {owed} ({lead} ahead × {rules.TributePerScore}). " + (p.unpaid == 0 ? $"It pays {got}." : got.Length > 0 ? $"It can pay {got}; the other {p.unpaid} it does not have." : "Its treasury is empty: it has nothing to pay with. Ask a world instead, or fight on."),
                    listens && got.Length > 0, !listens ? deaf : got.Length > 0 ? "" : them + " has nothing to pay with"));
            }
            var worlds = Cedable(w, other);
            bool beaten = lead >= FleetSystem.CessionLead || theirs <= mine;
            foreach (var h in worlds.Take(6))
                terms.Add(new Term(Cede, h.Id, "Peace for " + h.Name, $"{them} gives up {h.Name} ({Presentation.Census.Say(Presentation.Census.Citizens(w, h))} citizens) and the war ends.",
                    lead >= WorldLead && beaten && (listens || lead >= FleetSystem.CessionLead),
                    lead < WorldLead ? $"a world is asked for from {WorldLead} ahead; you are {lead} ahead (a battle won is 2, a world taken 3)"
                    : !beaten ? $"{them} still has the stronger fleet ({Counted.Hulls(theirs)} to your {mine}) and will not give a world while it can hope to win one back: beat its fleet, or lead by {FleetSystem.CessionLead}" : listens || lead >= FleetSystem.CessionLead ? "" : deaf));
            if (worlds.Count == 0 && lead >= WorldLead) terms.Add(new Term(Cede, "", "Peace for a world", "", false, them + " has only its last world, or only the capital: there is none it can give"));
        }
        else
        {
            var p = Payable(w, asker, owed); string got = Paid(p);
            terms.Add(new Term(White, "", "A white peace", "Each side keeps what it holds and nothing is paid.", suing, suing ? "" : $"{them} is ahead by {-lead}: it wants something for stopping"));
            terms.Add(new Term(Tribute, "", $"Peace, paying tribute ({owed})", $"You owe {owed} ({-lead} behind × {rules.TributePerScore})" + (p.unpaid == 0 ? $" and pay {got}." : got.Length > 0 ? $": you can pay {got}, and the other {p.unpaid} is let go." : ": the treasury is empty, and it is let go."),
                listens, listens ? "" : deaf));
            foreach (var h in Cedable(w, asker).OrderBy(h => h.Population).Take(4))
                terms.Add(new Term(Cede, h.Id, "Peace, giving up " + h.Name, $"You give {h.Name} ({Presentation.Census.Say(Presentation.Census.Citizens(w, h))} citizens) to {them} and the war ends. A house that smells victory will still take a world to stop.", true, ""));
        }
        return terms;
    }

    /// <summary>Make the peace on the term named. The caller has checked it would be taken.</summary>
    public static void Conclude(World w, FleetSystem fleets, FleetRules rules, string asker, string other, string kind, string holdingId)
    {
        var war = w.Wars.FirstOrDefault(x => x.Between(asker, other)); if (war == null) return;
        int lead = war.Lead(asker); string ahead = lead >= 0 ? asker : other, behind = lead >= 0 ? other : asker;
        string an = w.House(ahead)?.Name ?? ahead, bn = w.House(behind)?.Name ?? behind;
        if (kind == Tribute && lead != 0)
        {
            int owed = Math.Abs(lead) * rules.TributePerScore; var p = Payable(w, behind, owed);
            var from = w.House(behind)!.Treasury; var to = w.House(ahead)!.Treasury;
            from.Add(Resource.Credits, -p.credits); to.Add(Resource.Credits, p.credits);
            foreach (var (g, n) in p.goods) { from.Add(g, -n); to.Add(g, n); }
            string got = Paid(p);
            w.Log(got.Length > 0 ? $"By the terms of the peace, {bn} pays {an} {got} in tribute{(p.unpaid > 0 ? $" ({p.unpaid} more was owed and could not be found)" : "")}." : $"By the terms of the peace {bn} owed {an} {owed} in tribute, and had nothing to pay it with.");
        }
        else if (kind == Cede && w.Holding(holdingId) is { } h && h.OwnerId == behind) fleets.CedeWorld(w, h, behind, ahead);
        fleets.MakePeace(w, asker, other, terms: false);
        if (kind == White && lead > 0)   // mercy is remembered by the house that was spared
            if (w.House(asker) is { } ah && Economy.HeadOf(w, ah) is { } head)
                foreach (var c in w.LivingCharacters().Where(c => c.HouseId == other))
                { var r = w.Relationship(c.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition + 5, -100, 100); }
    }
}
