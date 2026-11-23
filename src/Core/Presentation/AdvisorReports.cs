using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

public sealed record AdvisorReport(string Seat, string? AdvisorName, string Line, bool Warning)
{
    /// <summary>The post of the house's council this seat is (HouseholdSystem.Roles), and whether the head has given it to anybody.</summary>
    public string Role { get; init; } = "";
    public bool Vacant { get; init; }
    public string? AdvisorId { get; init; }
    /// <summary>Something that wants doing now, and can be: the only thing a seat says beside the map (the owner, 2026-11-22: the
    /// council is not to be on the screen all the time). A standing worry, such as a neighbour with a larger fleet, is a
    /// <see cref="Warning"/> on the council's own page and no more.</summary>
    public bool Urgent { get; init; }
}

/// <summary>
/// The advisor briefing (GDD §3 "information is filtered", §6 one advisor per pillar). Six seats: Steward, Admiral and
/// Chancellor, each filled by whoever the head has given the post, or failing that by the ablest relative of the matching
/// service; and Spymaster, Envoy and Advocate, which speak only for what they see and stand empty until somebody is named
/// (docs/HOUSEHOLD.md, "The council"). Each speaks one line a turn about its pillar. The council is not on the screen all
/// the time: a seat is shown beside the map only when it warns. The Admiral's line is built from the house's <em>intel</em>, not the truth, so it is biased
/// exactly as the GDD wants: confident when the picture is fresh, hedged when it is stale, wrong when it is wrong.
/// </summary>
public static class AdvisorReports
{
    public static IReadOnlyList<AdvisorReport> Build(World w, ContentDatabase content)
    {
        var list = new List<AdvisorReport>();
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;
        if (p == null || house == null) return list;

        /* a seat at the council is whoever the head has given that work to (House, the Household: the Steward and the Marshal). Where
           nobody has been named, it is the ablest grown relative of that service who will serve: one who defies the head does not
           speak for the house (docs/PLAYTHROUGH.md, Easy, N40 and F73) */
        Character? Seat(string track)
        {
            string duty = track == "stewardship" ? HouseholdSystem.Steward : track == "military" ? HouseholdSystem.Marshal : HouseholdSystem.Chancellor;
            if (duty.Length > 0 && house.Duty(duty) is { } d && w.Char(d.HolderId) is { Alive: true } named) return named;
            return w.LivingCharacters().Where(c => c.HouseId == house.Id && c.CareerTrack == track && c.Id != p.Id && c.Age >= DynastySystem.AgeOfMajority && !c.Flags.Contains(FamilySystem.DefiantFlag))
                .OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
                ?? (p.CareerTrack == track ? p : null);
        }

        // Steward — the domain
        var steward = Seat("stewardship");
        var t = house.Treasury;
        int upkeepHint = w.HoldingsOf(house.Id).Count();
        string stewardLine = upkeepHint == 0 ? $"We hold no land. The house lives on what it has banked — {Money.Held(t)} credits — until a {(w.IsImperium ? "fief" : "world")} is won, granted or married into."
            : t.Credits < 0 ? $"We are insolvent — {Money.Held(t)} credits. Unrest climbs on every populated world until the ledger turns."
            : t.Credits < 5 ? $"The coffers are thin ({Money.Held(t)} credits). One bad season and we borrow."
            : w.Sieges.Values.Any(s => w.Holding(s.HoldingId)?.OwnerId == house.Id) ? "A world of ours is blockaded; its yield reaches us halved."
            : house.Debt > 0 ? $"We owe {Money.Credits(house.Debt)}{(house.LenderId.Length > 0 ? $" to the bank of {w.House(house.LenderId)?.Name}" : "")}; the instalments fall due each turn. {Money.Held(t)} credits banked."
            : w.HoldingsOf(house.Id).Any(h => h.IsPlanet && h.Prosperity < 35) ? $"{w.HoldingsOf(house.Id).Where(h => h.IsPlanet && h.Prosperity < 35).OrderBy(h => h.Prosperity).First().Name} languishes; coin invested there would lift it. {Money.Held(t)} credits banked."
            : $"The domain yields; {Money.Held(t)} credits banked, {t.Materials} materials, {t.Manpower} manpower. We stand {Wealth.RankOf(w, house.Id)}{(Wealth.RankOf(w, house.Id) == 1 ? "st" : Wealth.RankOf(w, house.Id) == 2 ? "nd" : Wealth.RankOf(w, house.Id) == 3 ? "rd" : "th")} in wealth among {w.Houses.Keys.Count(h => !World.Outsider(h))} houses. The {house.TaxPolicy} tithe stands.";
        list.Add(new AdvisorReport("Steward", steward?.Name, stewardLine, t.Credits < 5) { Role = HouseholdSystem.Steward, Vacant = house.Duty(HouseholdSystem.Steward) == null, AdvisorId = steward?.Id, Urgent = t.Credits < 0 || (upkeepHint > 0 && t.Credits < 5) });

        // Admiral — from intel, not truth (GDD §19 mixed visibility)
        var admiral = Seat("military");
        int ours = FleetSystem.StrengthOf(w, house.Id);
        var seen = GalaxyProjection.Fleets(w).Where(f => !f.IsPlayer).ToList();
        var biggest = seen.GroupBy(f => f.OwnerHouseName).Select(g => (name: g.Key, hulls: g.Sum(f => f.Strength), conf: (int)g.Average(f => f.Confidence))).OrderByDescending(x => x.hulls).FirstOrDefault();
        int laidUpHulls = w.Fleets.Values.Where(f => f.OwnerId == house.Id && f.LaidUp).Sum(f => f.Strength);
        string admiralLine = ours == 0 && laidUpHulls > 0 ? $"We have no hull in commission: {laidUpHulls} lie laid up, with no crews aboard. Until they are put back in commission any house with a squadron can dictate to us."
            : ours == 0 ? "We keep no ships. Any house with a squadron can dictate to us."
            : biggest.name == null ? $"We field {Counted.Hulls(ours)}. Of the rivals' fleets we have no reliable picture."
            : $"We field {Counted.Hulls(ours)}; {biggest.name} is reported at {(biggest.conf >= 90 ? "" : "perhaps ")}{biggest.hulls}{(biggest.conf < 60 ? " — an old report, trust it lightly" : "")}.";
        var enemies = w.EnemiesOf(house.Id).Select(e => w.House(e)?.Name ?? e).ToList();
        if (enemies.Count > 0) admiralLine += $" We are at war with {string.Join(", ", enemies)}.";
        if (w.HasFlag("fuel_short")) admiralLine += " The tanks are dry: no fleet of ours can sail until we find helium-3 — a skimmer, the exchange, or the open market.";
        else if (ours >= 2 && house.Treasury.Helium3 <= FleetSystem.FuelBillFor(w, house.Id, 3) * 2) admiralLine += $" The tanks hold {house.Treasury.Helium3} helium-3 — two turns at most. Buy or skim.";
        list.Add(new AdvisorReport("Admiral", admiral?.Name, admiralLine, enemies.Count > 0 || (biggest.name != null && biggest.hulls > ours * 2)) { Role = HouseholdSystem.Marshal, Vacant = house.Duty(HouseholdSystem.Marshal) == null, AdvisorId = admiral?.Id, Urgent = w.HasFlag("fuel_short") || (ours == 0 && enemies.Count > 0) });

        // Chancellor — standing and the realm
        var chancellor = Seat("law");
        int inst = w.Counter("title_instability"), pressure = w.Counter("coalition_pressure");
        string chanLine = w.HasFlag("motion_against_me") ? "A motion against our house is on the Senate's docket; the vote is at the session's end. Whip the benches, or answer it from the floor."
            : w.HasFlag("embargoed") ? "The Senate's embargo closes the exchanges to us. Our factors sell at half and the open market will not deal."
            : inst > 0 ? $"Our rank of {content.Titles.NameOf(house.Title, w.Era)} outstrips our standing by {inst}; {(w.IsImperium ? "the vassals grumble" : "our clients grumble")} and rivals take note."
            : pressure >= 3 ? "The houses are drawing together against us. Our rise frightens them."
            : w.Crises.Count > 0 ? $"A crisis is upon the Reach: {string.Join(", ", w.Crises.Keys.Select(k => content.Crises.FirstOrDefault(d => d.Id == k)?.Name ?? k))}. Dampers may be available."
            : p.Flags.Contains(DynastySystem.ParvenuFlag) ? "The old blood looks down on us still. Standing, not coin, will change that."
            : $"Our standing is sound at legitimacy {house.Legitimacy}. The chambers are quiet.";
        if (w.Motions.Any(m => m.Open && m.ProposerHouseId == house.Id)) chanLine += " Our own motion stands on the docket.";
        else if (w.Motions.Any(x => x.Open && x.ProposedTurn < w.Turn && !x.Votes.ContainsKey(house.Id))) chanLine += " A motion is decided this session and our vote is not yet declared.";
        else if (w.HasFlag("petition_open")) chanLine += " An estate petitions the Senate; turned away, it will remember every house.";
        else if (w.HasFlag("motion_open") && !w.HasFlag("motion_against_me")) chanLine += " A motion is before the Senate; our vote has weight.";
        list.Add(new AdvisorReport("Chancellor", chancellor?.Name, chanLine, inst > 0 || pressure >= 3 || w.Crises.Count > 0 || w.HasFlag("motion_against_me") || w.HasFlag("embargoed")) { Role = HouseholdSystem.Chancellor, Vacant = house.Duty(HouseholdSystem.Chancellor) == null, AdvisorId = chancellor?.Id, Urgent = w.HasFlag("motion_against_me") || w.HasFlag("embargoed") });

        // The three that follow speak only if somebody holds the post: nobody keeps watch, calls or pleads for a house by default.
        Character? Post(string role) => house.Duty(role) is { } pd && w.Char(pd.HolderId) is { Alive: true } named ? named : null;

        // Spymaster — what the house knows of the others, and how old it is
        var spy = Post(HouseholdSystem.Spymaster);
        int stale = seen.Count(f => f.Confidence < 50), atWarStale = seen.Count(f => f.Confidence < 50 && w.AtWar(house.Id, f.OwnerHouseId));
        string spyLine = spy == null ? (seen.Count == 0 ? "Nobody keeps watch for the house, and we have no report of any fleet but our own." : $"Nobody keeps watch for the house. Of {Counted.Fleets(seen.Count)} we have reports of, {stale} {(stale == 1 ? "is" : "are")} old.")
            : atWarStale > 0 ? $"Our reports on {Counted.Fleets(atWarStale)} of a house we are at war with are old. Watch that house, and they will be exact."
            : house.Duty(HouseholdSystem.Spymaster)!.LastReport.Length > 0 ? house.Duty(HouseholdSystem.Spymaster)!.LastReport
            : "The watch is set. The first reports come when the year turns.";
        list.Add(new AdvisorReport("Spymaster", spy?.Name, spyLine, atWarStale > 0) { Role = HouseholdSystem.Spymaster, Vacant = spy == null, AdvisorId = spy?.Id, Urgent = atWarStale > 0 && spy != null });

        // Envoy — who thinks ill of the head
        var envoy = Post(HouseholdSystem.Envoy);
        var coldest = HouseholdSystem.EnvoyChoice(w, house, p); var coldHead = coldest != null ? Economy.HeadOf(w, coldest) : null; int cold = coldHead != null ? w.GetRelationship(coldHead.Id, p.Id)?.Disposition ?? 0 : 0;
        int illWishers = w.Houses.Values.Count(h => h.Id != house.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) is { } oh && (w.GetRelationship(oh.Id, p.Id)?.Disposition ?? 0) <= -30);
        string envoyLine = coldest == null ? "There is no house to call on."
            : (cold <= -30 ? $"{coldHead!.Name} of {coldest.Name} thinks worst of you ({cold:+0;-0;0}){(illWishers > 1 ? $", and {Counted.Houses(illWishers - 1)} more think ill of you" : "")}." : $"No head of a house thinks very ill of you. The coolest is {coldHead!.Name} of {coldest.Name} ({cold:+0;-0;0}).")
              + (envoy == null ? " Nobody calls on anybody for the house." : house.Duty(HouseholdSystem.Envoy)!.LastReport.Length > 0 ? " " + house.Duty(HouseholdSystem.Envoy)!.LastReport : "");
        list.Add(new AdvisorReport("Envoy", envoy?.Name, envoyLine, cold <= -50 && envoy == null) { Role = HouseholdSystem.Envoy, Vacant = envoy == null, AdvisorId = envoy?.Id });

        // Advocate — the suits that name the house
        var advocate = Post(HouseholdSystem.Advocate);
        var suits = LadderSystem.SuitsNaming(w, house.Id); int unpleaded = suits.Count(b => w.Char(b.AdvocateId) is not { Alive: true }), stake = suits.Sum(b => LadderSystem.StakeOf(b));
        var charged = LadderSystem.ChargesOn(w, house.Id); int unanswered = charged.Count(c => LadderSystem.PleaderOf(w, c) == null);
        string chargeLine = charged.Count == 0 ? "" : $" {Counted.Of(charged.Count, "charge")} against the house's people {(charged.Count == 1 ? "waits" : "wait")} to be heard" + (unanswered == 0 ? "." : advocate == null ? $", and {unanswered} {(unanswered == 1 ? "has" : "have")} nobody to answer {(unanswered == 1 ? "it" : "them")}." : $", and {unanswered} will be taken up when the year turns.");
        string advLine = suits.Count == 0 ? (advocate == null ? "No suit names the house. Nobody is retained to plead for it." : "No suit names the house.") + chargeLine
            : $"{Counted.Of(suits.Count, "suit")} {(suits.Count == 1 ? "names" : "name")} the house, and {Money.Credits(stake)} turn on {(suits.Count == 1 ? "it" : "them")}. "
              + (unpleaded == 0 ? "Each has somebody to plead it." : advocate == null ? $"{unpleaded} {(unpleaded == 1 ? "has" : "have")} nobody to plead {(unpleaded == 1 ? "it" : "them")}: send someone, or come to terms." : $"{unpleaded} will be taken up when the year turns.") + chargeLine;
        list.Add(new AdvisorReport("Advocate", advocate?.Name, advLine, unpleaded > 0 && advocate == null) { Role = HouseholdSystem.Advocate, Vacant = advocate == null, AdvisorId = advocate?.Id, Urgent = unpleaded > 0 && advocate == null });
        return list;
    }
}
