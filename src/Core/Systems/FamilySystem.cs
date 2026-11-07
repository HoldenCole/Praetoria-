using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Family autonomy (GDD §18 "people, not puppets"): the player's own kin have ambitions and loyalties of their own.
/// Each turn an adult member who is ambitious or arrogant and ill-disposed toward the head becomes <c>defiant</c>
/// (content answers it); a defiant member whose disposition sinks further courts the house's rivals, and one who
/// despises the head may <em>leave</em> — taking their name to a rival house, which gains a claim on yours. Loyal
/// kin never defy. The intensity is the §20 "family autonomy" knob (0 = deferential).
/// </summary>
public sealed class FamilySystem
{
    public const string DefiantFlag = "defiant";
    public const string DefectedFlag = "defected";
    public const int DefyBelow = -20;
    public const int DefectBelow = -60;

    public void Tick(World w)
    {
        var p = w.Protagonist;
        var house = p != null ? w.House(p.HouseId) : null;
        if (p is not { Alive: true } || house == null || w.FamilyAutonomy <= 0) return;

        foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != p.Id && c.Age >= DynastySystem.AgeOfMajority).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            if (kin.NatureTraits.Contains("Loyal")) { kin.Flags.Remove(DefiantFlag); continue; }
            var rel = w.Relationship(kin.Id, p.Id);
            bool willful = kin.NatureTraits.Contains("Ambitious") || kin.NatureTraits.Contains("Arrogant") || kin.NatureTraits.Contains("Proud") || kin.Ambition == "seize_the_throne" || kin.Ambition == "outshine_all_rivals";
            int threshold = (int)Math.Round(DefyBelow / w.FamilyAutonomy);

            if (willful && rel.Disposition < threshold)
            {
                if (kin.Flags.Add(DefiantFlag)) w.Log($"{kin.Name} grows defiant of {p.Name}'s rule of the house.");
                // courting the rivals: a defiant kinsman warms toward whoever dislikes the head most
                var rival = w.LivingCharacters().Where(c => c.HouseId != house.Id).OrderBy(c => w.GetRelationship(c.Id, p.Id)?.Disposition ?? 0).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (rival != null) { var r = w.Relationship(kin.Id, rival.Id); r.Disposition = Math.Clamp(r.Disposition + 5, -100, 100); }
                rel.Disposition = Math.Clamp(rel.Disposition - 2, -100, 100);

                if (rel.Disposition < (int)Math.Round(DefectBelow / w.FamilyAutonomy) && rival != null && !kin.Flags.Contains(DynastySystem.HiddenFlag))
                    Defect(w, kin, rival, house);
            }
            else if (rel.Disposition >= 0) kin.Flags.Remove(DefiantFlag);
        }
    }

    /// <summary>The kinsman leaves for a rival house, carrying your blood — and so a claim on your title — with them.</summary>
    private static void Defect(World w, Character kin, Character patron, House from)
    {
        var to = w.House(patron.HouseId);
        if (to == null) return;
        from.Members.Remove(kin.Id);
        to.Members.Add(kin.Id);
        kin.HouseId = to.Id;
        kin.Flags.Remove(DefiantFlag);
        kin.Flags.Add(DefectedFlag);
        kin.Flags.Add("feud");
        if (from.Title != "landless") to.Claims.Add(from.Title);
        w.WorldCounters["defections"] = w.Counter("defections") + 1;
        w.WorldFlags.Add("kin_defected");
        w.Log($"{kin.Name} abandons {from.Name} for {to.Name}, and takes {from.Name}'s blood — and a claim to its {from.Title} rank — with them.");
    }
}
