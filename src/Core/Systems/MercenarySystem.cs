using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Mercenary service (the owner, 2026-11-21: "you can have forces act as mercenaries in war, but I think that should come with a
/// [hurt] to reputation"; manpower itself is never sold). A house hires one of its fleets out to a house that is at war. The fleet
/// sails under the employer's colours and at the employer's orders, is paid for and fuelled by the employer, and fights the
/// employer's battles: hulls it loses are lost. Its own admiral stays with it. The house that sent it is paid by the hull each year,
/// is thought the less of for selling its sword, and is disliked by those the sword is turned on. It comes home when the
/// employer's wars are over, when the term is up, when the pay is not found, or when it is called back, which breaks the contract.
/// State: <see cref="Fleet.HiredFrom"/>, <see cref="Fleet.HireTurn"/>. RNG-free.
/// </summary>
public static class MercenarySystem
{
    /// <summary>Paid to the sending house each year for every hull still in the company.</summary>
    public const int CreditsPerHull = 2;
    /// <summary>The longest a company serves before it comes home of itself.</summary>
    public const int TermYears = 5;
    /// <summary>What hiring a fleet out costs the house's legitimacy, each time it is done.</summary>
    public const int Legitimacy = 3;
    /// <summary>What the heads of the houses it is turned on think the worse of the head who sent it; what the employer's head thinks the better.</summary>
    public const int FoeRegard = 15, EmployerRegard = 8;
    /// <summary>Calling a company back before its term: the employer's head thinks this much the worse, and the house is thought the less of.</summary>
    public const int BreakRegard = 15, BreakLegitimacy = 1;
    /// <summary>An employer whose head thinks worse of the sender than this will not have its ships.</summary>
    public const int TakesAt = -20;

    private const string OutPrefix = "hired_out:";
    private static string OutKey(string homeId, Fleet f) => $"{OutPrefix}{homeId}:{f.Id}:{f.Name}";

    public static IEnumerable<Fleet> HiredOutBy(World w, string houseId) => w.Fleets.Values.Where(f => f.HiredFrom == houseId).OrderBy(f => f.Id, StringComparer.Ordinal);
    public static int PayOf(Fleet f) => f.Strength * CreditsPerHull;
    public static int YearsLeft(World w, Fleet f) => Math.Max(0, f.HireTurn + TermYears - w.Turn);

    /// <summary>The houses a fleet of <paramref name="houseId"/> could be hired to now: houses of the Reach at war, with which it is not at war itself.</summary>
    public static List<House> Employers(World w, string houseId) =>
        w.Houses.Values.Where(h => h.Id != houseId && !World.Outsider(h.Id) && w.EnemiesOf(h.Id).Any() && !w.AtWar(houseId, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    public static string? Refusal(World w, Character? actor, string fleetId, string employerId)
    {
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(fleetId, out var f) || f.OwnerId != actor.HouseId) return "not your fleet";
        if (SenateActs.CompaniesForbidden(w)) return $"{SenateActs.LawName(w, SenateActs.CompanyBan)} forbids it: no fleet is hired out while it stands";
        if (f.Office) return "it is held by office and is the Reach's: it is not yours to hire out";
        if (f.LevyOf.Length > 0) return "a levy is its own house's, not yours to hire out";
        if (f.IsAway) return "it is beyond the Reach"; if (f.Moving) return "it is under way: a company is hired where it lies";
        var employer = w.House(employerId); if (employer == null || World.Outsider(employerId) || employerId == actor.HouseId) return "no such house";
        if (w.AtWar(actor.HouseId, employerId)) return $"you are at war with {employer.Name}";
        var foes = w.EnemiesOf(employerId).Distinct().ToList(); if (foes.Count == 0) return $"{employer.Name} is at war with no one: it has no use for a hired sword";
        if (w.Wars.Any(x => x.Involves(actor.HouseId))) return "your house is at war itself: its ships are wanted at home";
        var head = Economy.HeadOf(w, employer); int regard = head != null ? w.GetRelationship(head.Id, actor.Id)?.Disposition ?? 0 : 0;
        if (regard < TakesAt) return $"{head?.Name ?? "its head"} thinks too ill of you to put your ships in {employer.Name}'s line (regard {regard}; {TakesAt} or better would do)";
        if (SenateActs.CompanyFee(w) is > 0 and var fee && w.House(actor.HouseId) is { } sender && RegionSystem.CapitalHolder(w)?.Id != sender.Id && sender.Treasury.Credits < fee)
            return $"{SenateActs.LawName(w, SenateActs.CompanyLicence)} asks {Money.Credits(fee)} for the licence, paid to the chair, and the house has {Money.Show(Math.Max(0, sender.Treasury.Credits))}";
        int pay = PayOf(f); return employer.Treasury.Credits >= pay ? null : $"{employer.Name} cannot find the first year's pay ({Money.Credits(pay)}; it has {Money.Show(Math.Max(0, employer.Treasury.Credits))})";
    }

    /// <summary>What hiring it out would do, said before it is done.</summary>
    public static string Says(World w, Fleet f, House employer)
    {
        var foes = w.EnemiesOf(employer.Id).Distinct().Select(e => w.House(e)?.Name ?? e).ToList();
        return $"{f.Name} ({Counted.Hulls(f.Strength)}) sails under {employer.Name}'s colours and at its orders for up to {TermYears} years, against {string.Join(" and ", foes)}. {employer.Name} pays you {Money.Credits(CreditsPerHull)} a hull each year ({Money.Show(PayOf(f))} now) and keeps and fuels the ships. "
            + $"Hulls lost in its battles are lost to you. Your house is thought the less of for selling its sword (legitimacy −{ShameOf(w)}), and the head of each house it is turned on thinks {FoeRegard} the worse of you; {employer.Name}'s head thinks {EmployerRegard} the better. "
            + "It comes home when that house's wars are over, the term is up or the pay is not found. Calling it back sooner breaks the contract."
            + (SenateActs.CompanyFee(w) is > 0 and var fee ? $" Under {SenateActs.LawName(w, SenateActs.CompanyLicence)} you pay the chair {Money.Credits(fee)} for the licence, and a licensed company costs less standing than an unlicensed one did." : "");
    }

    public static void Hire(World w, Character actor, Fleet f, House employer)
    {
        var home = w.House(actor.HouseId)!; string where = w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId;
        f.HiredFrom = home.Id; f.LevyOf = home.Id; f.HireTurn = w.Turn; f.OwnerId = employer.Id; f.Errand = ""; f.Drilling = false; w.Intel.Remove(f.Id);
        LegitimacyBook.Move(w, home, -ShameOf(w), LegitimacyBook.War, "A company of the house hired out");
        if (SenateActs.CompanyFee(w) is > 0 and var fee && RegionSystem.CapitalHolder(w) is { } chair && chair.Id != home.Id)
        {
            home.Treasury.Credits -= fee; chair.Treasury.Credits += fee; w.WorldCounters["company_licences"] = w.Counter("company_licences") + 1;
            w.Log($"{home.Name} pays {chair.Name}, which holds the capital, {Money.Credits(fee)} for the licence {SenateActs.LawName(w, SenateActs.CompanyLicence)} asks.");
        }
        foreach (var foe in w.EnemiesOf(employer.Id).Distinct().ToList())
            if (w.House(foe) is { } fh && Economy.HeadOf(w, fh) is { } foeHead) { var r = w.Relationship(foeHead.Id, actor.Id); r.Disposition = Math.Clamp(r.Disposition - FoeRegard, -100, 100); }
        if (Economy.HeadOf(w, employer) is { } eh) { var r = w.Relationship(eh.Id, actor.Id); r.Disposition = Math.Clamp(r.Disposition + EmployerRegard, -100, 100); }
        Pay(w, f, employer, home);
        w.WorldCounters[OutKey(home.Id, f)] = w.Turn;   // so that a company destroyed in service can be told of
        w.WorldCounters["companies_hired"] = w.Counter("companies_hired") + 1;
        w.Log($"{home.Name} hires {f.Name} ({Counted.Hulls(f.Strength)}) out to {employer.Name} at {where}. It sails under {employer.Name}'s colours now.");
        FleetSystem.RefreshAdmiralFlags(w);
    }

    /// <summary>What hiring a fleet out costs the house in standing as the law stands: less under licence.</summary>
    public static int ShameOf(World w) => SenateActs.CompanyFee(w) > 0 ? Math.Min(Legitimacy, SenateActs.LicensedLegitimacy) : Legitimacy;

    private static bool Pay(World w, Fleet f, House employer, House home)
    {
        int due = PayOf(f), paid = Math.Clamp(employer.Treasury.Credits, 0, due);
        employer.Treasury.Credits -= paid; home.Treasury.Credits += paid;
        return paid >= due;
    }

    /// <summary>The year's reckoning for every company out on hire: the pay, and whether it comes home.</summary>
    public static void Tick(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        // a company that is no longer anywhere was destroyed in its employer's war: the house that sent it is told
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(OutPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            var parts = key[OutPrefix.Length..].Split(':', 3); if (parts.Length < 3) { w.WorldCounters.Remove(key); continue; }
            if (w.Fleets.TryGetValue(parts[1], out var still) && still.HiredFrom == parts[0]) continue;
            w.WorldCounters.Remove(key);
            if (!w.Fleets.ContainsKey(parts[1]))
            {
                w.Log($"{parts[2]}, hired out by {w.House(parts[0])?.Name ?? parts[0]}, is lost in another house's war.");
                if (parts[0] == mine) w.Tell("warn", $"{parts[2]}, which you hired out, has been destroyed in its employer's war. Nothing of it comes home", "military", parts[0]);
            }
        }
        foreach (var f in w.Fleets.Values.Where(f => f.HiredFrom.Length > 0).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            var home = w.House(f.HiredFrom); var employer = w.House(f.OwnerId);
            if (home == null) { f.HiredFrom = ""; f.LevyOf = ""; continue; }   // the house that sent it is gone: it is the employer's now
            if (employer == null) { ComeHome(w, f, "the house that hired it is no more"); continue; }
            if (f.IsAway) continue;   // it went beyond the Reach with its employer: the reckoning waits for it
            if (SenateActs.CompaniesForbidden(w)) { ComeHome(w, f, $"{SenateActs.LawName(w, SenateActs.CompanyBan)} forbids hired companies"); continue; }
            if (w.AtWar(home.Id, employer.Id)) { ComeHome(w, f, $"{home.Name} and {employer.Name} are at war"); continue; }
            if (!w.EnemiesOf(employer.Id).Any()) { ComeHome(w, f, $"{employer.Name}'s wars are over"); continue; }
            if (w.Turn >= f.HireTurn + TermYears) { ComeHome(w, f, "its term is up"); continue; }
            if (w.Turn <= f.HireTurn) continue;   // the first year was paid when it was hired
            if (!Pay(w, f, employer, home)) { ComeHome(w, f, $"{employer.Name} could not find its pay"); continue; }
            if (home.Id == mine) w.Tell("good", $"{employer.Name} pays {Money.Credits(PayOf(f))} for {f.Name} ({Counted.Hulls(f.Strength)}), {Counted.Years(YearsLeft(w, f))} of its term left", "military", employer.Id);
        }
    }

    /// <summary>The company goes back to the house that sent it, and sails for its nearest port.</summary>
    public static void ComeHome(World w, Fleet f, string why)
    {
        var home = w.House(f.HiredFrom); string employerName = w.House(f.OwnerId)?.Name ?? "its employer"; if (home == null) return;
        f.OwnerId = home.Id; f.HiredFrom = ""; f.LevyOf = ""; f.Errand = "";
        if (f.AdmiralId.Length > 0 && w.Char(f.AdmiralId)?.HouseId != home.Id) f.AdmiralId = "";   // an officer the employer put over it stays with the employer
        var port = f.SystemId.Length == 0 || w.Galaxy == null ? null : w.Holdings.Values.Where(h => h.OwnerId == home.Id && h.SystemId.Length > 0 && h.SystemId != f.SystemId)
            .Select(h => (s: h.SystemId, hops: w.Galaxy.Hops(f.SystemId, h.SystemId))).Where(x => x.hops > 0).OrderBy(x => x.hops).ThenBy(x => x.s, StringComparer.Ordinal).Select(x => (string?)x.s).FirstOrDefault();
        bool there = w.Holdings.Values.Any(h => h.OwnerId == home.Id && h.SystemId == f.SystemId);
        if (!there && port != null && !f.Moving) { f.DestinationId = port; f.Eta = w.Galaxy!.Hops(f.SystemId, port); f.Voyage = f.Eta; }
        w.Log($"{f.Name} leaves {employerName}'s service ({why}) and is {home.Name}'s again.");
        if (home.Id == w.Protagonist?.HouseId) w.Tell("info", $"{f.Name} has left {employerName}'s service: {why}. It is yours again, with {Counted.Hulls(f.Strength)}{(f.Moving ? $", and is {Counted.Years(f.Eta)} from home" : "")}", "military", f.OwnerId);
        FleetSystem.RefreshAdmiralFlags(w);
    }

    /// <summary>Why a company cannot be called back, or null.</summary>
    public static string? RecallRefusal(World w, Character? actor, string fleetId)
    {
        if (actor?.Alive != true) return "no one to give the order";
        if (!w.Fleets.TryGetValue(fleetId, out var f) || f.HiredFrom != actor.HouseId) return "not a company of yours out on hire";
        return f.IsAway ? "it is beyond the Reach with its employer: it cannot be reached" : null;
    }

    public static void Recall(World w, Character actor, Fleet f)
    {
        var employer = w.House(f.OwnerId); var home = w.House(actor.HouseId)!;
        if (employer != null && Economy.HeadOf(w, employer) is { } eh) { var r = w.Relationship(eh.Id, actor.Id); r.Disposition = Math.Clamp(r.Disposition - BreakRegard, -100, 100); }
        LegitimacyBook.Move(w, home, -BreakLegitimacy, LegitimacyBook.War, "A hired company called back, and the contract broken");
        ComeHome(w, f, "you called it back before its term, and the contract is broken");
    }
}
