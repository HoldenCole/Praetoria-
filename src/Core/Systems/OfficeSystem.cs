using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The offices of the Reach (GDD §8 "the gap between office-power and house-power"). An office is the state's: granted by a Commission
/// motion (<see cref="SenateSystem"/>) to a house whose member has the rank on the office's track, held until the Senate commissions
/// another, the holder dies without a qualified kinsman, the house falls, or — for the Admiralty — the holder's house makes war on the
/// chair (treason). The Emperor holds every office by the throne. The Admiralty commands the <b>office fleets</b> (<see cref="Fleet.Office"/>):
/// they sail under the holder's colours and fight its wars, but the chair pays their upkeep and the state's bunkers their fuel; stripped,
/// they return to the chair. Flags for the player: <c>holds_office:&lt;id&gt;</c>, <c>office_granted</c>, <c>office_lost</c>,
/// <c>office_vacant:&lt;id&gt;</c>, <c>treason</c>.
/// </summary>
public static class OfficeSystem
{
    public const int TreasonLegitimacy = 6, GrantLegitimacy = 2;
    public static readonly string[] TurnFlags = { "office_granted", "office_lost", "treason" };

    public static OfficeHolding? Holding(World w, string officeId) => w.Offices.FirstOrDefault(o => o.OfficeId == officeId);
    public static string? HolderHouse(World w, string officeId) => Holding(w, officeId)?.HouseId;
    public static bool Holds(World w, string houseId, string officeId) => HolderHouse(w, officeId) == houseId;
    public static IEnumerable<OfficeHolding> HeldBy(World w, string houseId) => w.Offices.Where(o => o.HouseId == houseId);

    /// <summary>The best-qualified living member of a house for an office: the office's track, at least its rank; highest rank, then id. Null if none.</summary>
    public static Character? Candidate(World w, OfficeDef def, string houseId) =>
        w.LivingCharacters().Where(c => c.HouseId == houseId && c.CareerTrack == def.Track && c.CareerRank >= def.MinRank)
            .OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The house the office fleets belong to when no Admiral holds them: the chair, else no one.</summary>
    public static string? FleetsDefaultOwner(World w) => RegionSystem.CapitalHolder(w)?.Id;

    public static void Grant(World w, OfficeDef def, string houseId, Character who)
    {
        var mine = w.Protagonist?.HouseId;
        var old = Holding(w, def.Id);
        if (old != null) Strip(w, def, "the Senate commissions another", false);
        w.Offices.Add(new OfficeHolding { OfficeId = def.Id, HouseId = houseId, CharacterId = who.Id, SinceTurn = w.Turn });
        var house = w.House(houseId); if (house != null) house.Legitimacy = Math.Min(100, house.Legitimacy + GrantLegitimacy);
        who.Flags.Add("office:" + def.Id);
        if (def.CommandsFleets) foreach (var f in w.Fleets.Values.Where(f => f.Office)) f.OwnerId = houseId;
        w.WorldCounters["offices_granted"] = w.Counter("offices_granted") + 1;
        if (houseId == mine) w.WorldFlags.Add("office_granted");
        w.Log($"{who.Name} is commissioned {def.Name}{(def.CommandsFleets ? "; the office fleets sail under the colours of " + (house?.Name ?? houseId) : "")}.");
    }

    /// <summary>Vacate an office. The fleets go back to the chair; the holder's house loses the grant's legitimacy, and more for treason.</summary>
    public static void Strip(World w, OfficeDef def, string reason, bool treason)
    {
        var mine = w.Protagonist?.HouseId; var h = Holding(w, def.Id); if (h == null) return;
        w.Offices.Remove(h);
        var house = w.House(h.HouseId); var who = w.Char(h.CharacterId); who?.Flags.Remove("office:" + def.Id);
        if (house != null) house.Legitimacy = Math.Max(0, house.Legitimacy - (treason ? TreasonLegitimacy : GrantLegitimacy));
        if (def.CommandsFleets) { var back = FleetsDefaultOwner(w); foreach (var f in w.Fleets.Values.Where(f => f.Office && f.OwnerId == h.HouseId)) f.OwnerId = back ?? f.OwnerId; }
        if (h.HouseId == mine) { w.WorldFlags.Add("office_lost"); if (treason) w.WorldFlags.Add("treason"); }
        w.Log(treason
            ? $"{house?.Name ?? h.HouseId} turns the Reach's guns on the chair: {who?.Name ?? "its admiral"} is stripped of the {def.Name} for treason, and the office fleets return to the chair."
            : $"{who?.Name ?? "The holder"} is relieved of the {def.Name} — {reason}.");
    }

    /// <summary>The Emperor holds every office by the throne (GDD §8).</summary>
    public static void TakeAll(World w, OfficeCatalog offices, string houseId)
    {
        var house = w.House(houseId); var head = house != null ? Economy.HeadOf(w, house) : null; if (head == null) return;
        foreach (var def in offices.Defs)
        {
            var h = Holding(w, def.Id); if (h?.HouseId == houseId) continue;
            if (h != null) { w.Offices.Remove(h); w.Char(h.CharacterId)?.Flags.Remove("office:" + def.Id); }
            w.Offices.Add(new OfficeHolding { OfficeId = def.Id, HouseId = houseId, CharacterId = head.Id, SinceTurn = w.Turn });
            head.Flags.Add("office:" + def.Id);
        }
    }

    /// <summary>A fallen house's offices are vacated.</summary>
    public static void Vacate(World w, OfficeCatalog offices, string houseId)
    {
        foreach (var h in w.Offices.Where(o => o.HouseId == houseId).ToList()) { var def = offices.Def(h.OfficeId); if (def != null) Strip(w, def, "the house is no more", false); else w.Offices.Remove(h); }
    }

    /// <summary>Each turn: a dead holder's office passes within the house or falls vacant, treason is punished, stipends are paid, flags are written.</summary>
    public static void Tick(World w, OfficeCatalog offices)
    {
        if (offices.IsEmpty) return;
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w);   // turn flags are cleared by SenateSystem.Tick before motions resolve
        if (w.Era == "imperium" && ThroneSystem.Emperor(w) is { } emperor) TakeAll(w, offices, emperor.Id);   // the Emperor holds every office by the throne
        foreach (var def in offices.Defs)
        {
            var h = Holding(w, def.Id); if (h == null) continue;
            var house = w.House(h.HouseId); var who = w.Char(h.CharacterId);
            if (house == null) { w.Offices.Remove(h); continue; }
            if (who is not { Alive: true })
            {
                var next = Candidate(w, def, house.Id);
                if (next != null) { h.CharacterId = next.Id; next.Flags.Add("office:" + def.Id); w.Log($"{next.Name} takes up the {def.Name} for {house.Name}."); }
                else { Strip(w, def, "no one of the house has the rank to hold it", false); continue; }
            }
            if (def.CommandsFleets && chair != null && chair.Id != house.Id && w.AtWar(house.Id, chair.Id)) { Strip(w, def, "treason", true); continue; }
            if (def.Stipend > 0 && chair != null && chair.Id != house.Id) { int paid = Math.Min(def.Stipend, Math.Max(0, chair.Treasury.Credits)); chair.Treasury.Credits -= paid; house.Treasury.Credits += paid; }
            if (def.CommandsFleets) foreach (var f in w.Fleets.Values.Where(f => f.Office && f.OwnerId != house.Id)) f.OwnerId = house.Id;   // the Admiral's colours on every office hull
        }
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("holds_office:") || f.StartsWith("office_vacant:")).ToList()) w.WorldFlags.Remove(f);
        foreach (var def in offices.Defs)
        {
            var h = Holding(w, def.Id);
            if (h == null) w.WorldFlags.Add("office_vacant:" + def.Id);
            else if (h.HouseId == mine) w.WorldFlags.Add("holds_office:" + def.Id);
        }
    }

    /// <summary>Sphere influence an office confers on a house.</summary>
    public static int SphereBonus(World w, OfficeCatalog offices, string houseId, string sphereId) =>
        HeldBy(w, houseId).Sum(h => offices.Def(h.OfficeId) is { } d && d.Sphere == sphereId ? d.SphereBonus : 0);
}
