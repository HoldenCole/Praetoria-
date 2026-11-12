using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The human endgame (GDD §10/§13/§16, BuildSpec M6): proclaiming or seizing the Imperium. In the fractured stars an
/// Archduke whose legitimacy meets the Emperor's bar may <b>proclaim</b> the Imperium; in any era a Duke-or-better
/// house whose hulls outnumber every rival's and who is winning the war against the sitting Emperor may <b>seize</b>
/// the throne. Either way the <see cref="World.Era"/> flips (or stays) at <c>imperium</c>, office fleets pass to the
/// new Emperor, the galaxy's fear peaks, and the <c>imperium_proclaimed</c> / <c>throne_seized</c> flags fire the
/// proclamation set-piece. A seized throne keeps the house's legitimacy where it was: the §13 powder keg.
/// </summary>
public static class ThroneSystem
{
    public const string ProclaimedFlag = "imperium_proclaimed";
    public const string SeizedFlag = "throne_seized";
    public const string DeposedFlag = "deposed";          // char flag on the heads of the house that lost the throne

    public static House? Emperor(World w) => w.Houses.Values.Where(h => h.Title == Title.Emperor).OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();

    public static string? ProclaimRefusal(World w, string houseId, TitleCatalog titles)
    {
        var h = w.House(houseId);
        if (h == null) return "no house";
        if (w.Era == "imperium" || Emperor(w) != null) return "a throne already stands — it must be seized";
        if (titles.RankOf(h.Title) < titles.RankOf(Title.Archduke)) return $"only {(w.IsImperium ? "an Archduke" : "the First Citizen")} may proclaim";
        int bar = titles.ById(Title.Emperor)?.LegitimacyRequirement ?? 95;
        if (h.Legitimacy < bar) return $"the Reach will not accept it below legitimacy {bar}";
        return CapitalGate(w, houseId, byForce: false);
    }

    /// <summary>The throne stands in the capital (GDD §19 "where the capital is"). To take it by force a fleet of yours must hold the
    /// capital's orbit; to be proclaimed, the capital must be yours or its holder must have knelt — at peace with you and well
    /// disposed. A galaxy without a capital has no gate.</summary>
    public static string? CapitalGate(World w, string houseId, bool byForce)
    {
        var g = w.Galaxy; if (g == null || g.Capital.Length == 0) return null;
        var holder = w.PlanetHolder(g.Capital); string name = g.System(g.Capital)?.Name ?? g.Capital;
        if (holder == houseId) return null;
        if (byForce) return w.Fleets.Values.Any(f => f.OwnerId == houseId && !f.Moving && f.SystemId == g.Capital && f.Strength >= 2) ? null : $"the throne stands at {name}; a fleet of yours must hold its orbit";
        if (holder == null) return null;
        if (w.AtWar(holder, houseId)) return $"{w.House(holder)?.Name ?? holder} holds {name} and is at war with you";
        var ours = Economy.HeadOf(w, w.House(houseId)!); var theirs = Economy.HeadOf(w, w.House(holder)!);
        int disp = ours != null && theirs != null ? w.GetRelationship(theirs.Id, ours.Id)?.Disposition ?? 0 : 0;
        return disp >= 20 ? null : $"{w.House(holder)?.Name ?? holder} holds {name} and has not knelt";
    }

    public static string? SeizeRefusal(World w, string houseId, TitleCatalog titles)
    {
        var h = w.House(houseId);
        if (h == null) return "no house";
        if (h.Title == Title.Emperor) return "you already hold the throne";
        if (titles.RankOf(h.Title) < titles.RankOf(Title.Duke)) return $"a {Title.StyleOf(Title.Duke, w.Era)} at least must stand behind the fleet";
        int mine = FleetSystem.StrengthOf(w, houseId);
        int strongestRival = w.Houses.Values.Where(x => x.Id != houseId).Select(x => FleetSystem.StrengthOf(w, x.Id)).DefaultIfEmpty(0).Max();
        if (mine == 0 || mine < (int)Math.Ceiling(strongestRival * 1.25)) return "your fleet must outnumber every rival's by a quarter";
        var emperor = Emperor(w);
        if (emperor != null)
        {
            var war = w.Wars.FirstOrDefault(x => x.Between(houseId, emperor.Id));
            if (war == null) return $"you must be at war with {emperor.Name}";
            if (war.Lead(houseId) < 5) return $"you must be winning the war against {emperor.Name} (score lead 5)";
        }
        return CapitalGate(w, houseId, byForce: true);
    }

    /// <summary>Take the throne. <paramref name="seized"/> = by force (legitimacy untouched, fear maxed); otherwise proclaimed.</summary>
    public static void Enthrone(World w, string houseId, bool seized, string? byCharacterId = null)
    {
        var h = w.House(houseId)!;
        var former = Emperor(w);
        if (former != null && former.Id != houseId)
        {
            former.Title = Title.Archduke;
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == former.Id)) { c.Flags.Add(DeposedFlag); w.Relationship(c.Id, w.ProtagonistId).Disposition = -100; }
            w.Wars.RemoveAll(x => x.Between(houseId, former.Id));
            FleetSystem.RefreshWarFlags(w);
        }
        h.Title = Title.Emperor;
        h.Claims.Remove(Title.Emperor);
        bool flipped = w.Era != "imperium";
        w.Era = "imperium";
        foreach (var f in w.Fleets.Values.Where(f => f.Office)) f.OwnerId = houseId;        // the office's fleets go with the office (GDD §8)
        if (seized)
        {
            w.WorldFlags.Add(SeizedFlag);
            w.WorldCounters["seizures"] = w.Counter("seizures") + 3;
            w.WorldCounters["coalition_pressure"] = Math.Max(w.Counter("coalition_pressure"), SphereSystem.MaxCoalitionPressure - 1);
        }
        else
        {
            w.WorldFlags.Add(ProclaimedFlag);
            h.Legitimacy += 5;
        }
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId != houseId))
            w.Relationship(c.Id, w.ProtagonistId).Disposition = Math.Clamp(w.Relationship(c.Id, w.ProtagonistId).Disposition - (seized ? 30 : 15), -100, 100);
        w.WorldCounters["thrones_taken"] = w.Counter("thrones_taken") + 1;
        var who = byCharacterId != null ? w.Char(byCharacterId)?.Name : null;
        w.Log(seized
            ? $"{who ?? h.Name} SEIZES THE THRONE. {(former != null ? former.Name + " is cast down. " : "")}{(flipped ? "The Republic ends; the Imperium begins, in blood." : "The Imperium has a new master.")}"
            : $"{who ?? h.Name} PROCLAIMS THE IMPERIUM, and the houses of the Reach — some gladly, some not — kneel. An era ends.");
    }
}
