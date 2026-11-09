using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

public sealed record AdvisorReport(string Seat, string? AdvisorName, string Line, bool Warning);

/// <summary>
/// The advisor briefing (GDD §3 "information is filtered", §6 one advisor per pillar). Three seats — Steward, Admiral,
/// Chancellor — each filled by the house member on the matching career track (or vacant). Each speaks one line a
/// turn about its pillar. The Admiral's line is built from the house's <em>intel</em>, not the truth, so it is biased
/// exactly as the GDD wants: confident when the picture is fresh, hedged when it is stale, wrong when it is wrong.
/// </summary>
public static class AdvisorReports
{
    public static IReadOnlyList<AdvisorReport> Build(World w, ContentDatabase content)
    {
        var list = new List<AdvisorReport>();
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;
        if (p == null || house == null) return list;

        Character? Seat(string track) => w.LivingCharacters().Where(c => c.HouseId == house.Id && c.CareerTrack == track && c.Id != p.Id && c.Age >= DynastySystem.AgeOfMajority)
            .OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
            ?? (p.CareerTrack == track ? p : null);

        // Steward — the domain
        var steward = Seat("stewardship");
        var t = house.Treasury;
        int upkeepHint = w.HoldingsOf(house.Id).Count();
        string stewardLine = upkeepHint == 0 ? $"We hold no land. The house lives on what it has banked — {t.Credits} credits — until a fief is won, granted or married into."
            : t.Credits < 0 ? $"We are insolvent — {t.Credits} credits. Unrest climbs on every populated world until the ledger turns."
            : t.Credits < 5 ? $"The coffers are thin ({t.Credits} credits). One bad season and we borrow."
            : w.Sieges.Values.Any(s => w.Holding(s.HoldingId)?.OwnerId == house.Id) ? "A world of ours is blockaded; its yield reaches us halved."
            : house.Debt > 0 ? $"We owe {house.Debt} credits{(house.LenderId.Length > 0 ? $" to the bank of {w.House(house.LenderId)?.Name}" : "")}; the instalments fall due each turn. {t.Credits} credits banked."
            : w.HoldingsOf(house.Id).Any(h => h.IsPlanet && h.Prosperity < 35) ? $"{w.HoldingsOf(house.Id).Where(h => h.IsPlanet && h.Prosperity < 35).OrderBy(h => h.Prosperity).First().Name} languishes; coin invested there would lift it. {t.Credits} credits banked."
            : $"The domain yields; {t.Credits} credits banked, {t.Materials} materials, {t.Manpower} manpower. We stand {Wealth.RankOf(w, house.Id)}{(Wealth.RankOf(w, house.Id) == 1 ? "st" : Wealth.RankOf(w, house.Id) == 2 ? "nd" : Wealth.RankOf(w, house.Id) == 3 ? "rd" : "th")} in wealth among {w.Houses.Count} houses. The {house.TaxPolicy} tithe stands.";
        list.Add(new AdvisorReport("Steward", steward?.Name, stewardLine, t.Credits < 5));

        // Admiral — from intel, not truth (GDD §19 mixed visibility)
        var admiral = Seat("military");
        int ours = FleetSystem.StrengthOf(w, house.Id);
        var seen = GalaxyProjection.Fleets(w).Where(f => !f.IsPlayer).ToList();
        var biggest = seen.GroupBy(f => f.OwnerHouseName).Select(g => (name: g.Key, hulls: g.Sum(f => f.Strength), conf: (int)g.Average(f => f.Confidence))).OrderByDescending(x => x.hulls).FirstOrDefault();
        string admiralLine = ours == 0 ? "We keep no ships. Any house with a squadron can dictate to us."
            : biggest.name == null ? $"We field {ours} hull(s). Of the rivals' fleets we have no reliable picture."
            : $"We field {ours} hull(s); {biggest.name} is reported at {(biggest.conf >= 90 ? "" : "perhaps ")}{biggest.hulls}{(biggest.conf < 60 ? " — an old report, trust it lightly" : "")}.";
        var enemies = w.EnemiesOf(house.Id).Select(e => w.House(e)?.Name ?? e).ToList();
        if (enemies.Count > 0) admiralLine += $" We are at war with {string.Join(", ", enemies)}.";
        if (w.HasFlag("fuel_short")) admiralLine += " The tanks are dry: no fleet of ours can sail until we find helium-3 — a skimmer, the exchange, or the open market.";
        else if (ours >= 2 && house.Treasury.Helium3 <= FleetSystem.FuelBillFor(w, house.Id, 3) * 2) admiralLine += $" The tanks hold {house.Treasury.Helium3} helium-3 — two turns at most. Buy or skim.";
        list.Add(new AdvisorReport("Admiral", admiral?.Name, admiralLine, enemies.Count > 0 || (biggest.name != null && biggest.hulls > ours * 2)));

        // Chancellor — standing and the realm
        var chancellor = Seat("law");
        int inst = w.Counter("title_instability"), pressure = w.Counter("coalition_pressure");
        string chanLine = w.HasFlag("motion_against_me") ? "A motion against our house is on the Senate's docket; the vote is at the session's end. Whip the benches, or answer it from the floor."
            : w.HasFlag("embargoed") ? "The Senate's embargo closes the exchanges to us. Our factors sell at half and the open market will not deal."
            : inst > 0 ? $"Our {content.Titles.ById(house.Title)?.Name ?? house.Title} outstrips our standing by {inst}; the vassals grumble and rivals take note."
            : pressure >= 3 ? "The houses are drawing together against us. Our rise frightens them."
            : w.Crises.Count > 0 ? $"A crisis is upon the realm: {string.Join(", ", w.Crises.Keys.Select(k => content.Crises.FirstOrDefault(d => d.Id == k)?.Name ?? k))}. Dampers may be available."
            : p.Flags.Contains(DynastySystem.ParvenuFlag) ? "The old blood looks down on us still. Standing, not coin, will change that."
            : $"Our standing is sound at legitimacy {house.Legitimacy}. The chambers are quiet.";
        if (w.Motions.Any(m => m.Open && m.ProposerHouseId == house.Id)) chanLine += " Our own motion stands on the docket.";
        else if (w.Motions.Any(x => x.Open && x.ProposedTurn < w.Turn && !x.Votes.ContainsKey(house.Id))) chanLine += " A motion is decided this session and our vote is not yet declared.";
        else if (w.HasFlag("petition_open")) chanLine += " An estate petitions the Senate; turned away, it will remember every house.";
        else if (w.HasFlag("motion_open") && !w.HasFlag("motion_against_me")) chanLine += " A motion is before the Senate; our vote has weight.";
        list.Add(new AdvisorReport("Chancellor", chancellor?.Name, chanLine, inst > 0 || pressure >= 3 || w.Crises.Count > 0 || w.HasFlag("motion_against_me") || w.HasFlag("embargoed")));
        return list;
    }
}
