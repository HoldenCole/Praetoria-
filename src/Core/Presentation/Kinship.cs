using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>
/// A word for how someone stands to the head: "your brother", "your niece", "married in" (docs/PLAYTHROUGH.md, the family: rows
/// and profiles did not say whose child each was, and a niece was taken for a daughter). Read only.
/// </summary>
public static class Kinship
{
    private static bool F(Character c) => c.Sex == "female";
    private static IEnumerable<string> Parents(Character c) => new[] { c.MotherId, c.FatherId }.Where(x => x.Length > 0);
    private static bool ChildOf(Character child, Character parent) => child.MotherId == parent.Id || child.FatherId == parent.Id;
    private static bool Siblings(Character a, Character b) => a.Id != b.Id && Parents(a).Intersect(Parents(b)).Any();

    /// <summary>How <paramref name="other"/> stands to <paramref name="me"/>, or "" when nothing ties them by blood or marriage.</summary>
    public static string Of(World w, Character me, Character other)
    {
        if (me.Id == other.Id) return "you";
        if (w.MarriageOf(me.Id) is { } m && m.Has(other.Id)) return F(other) ? "your wife" : "your husband";
        if (ChildOf(other, me)) return F(other) ? "your daughter" : "your son";
        if (ChildOf(me, other)) return F(other) ? "your mother" : "your father";
        if (Siblings(me, other)) return F(other) ? "your sister" : "your brother";
        var theirParents = Parents(other).Select(w.Char).Where(p => p != null).Select(p => p!).ToList(); var myParents = Parents(me).Select(w.Char).Where(p => p != null).Select(p => p!).ToList();
        if (theirParents.Any(p => ChildOf(p, me))) return F(other) ? "your granddaughter" : "your grandson";
        if (myParents.Any(p => ChildOf(p, other))) return F(other) ? "your grandmother" : "your grandfather";
        if (theirParents.Any(p => Siblings(p, me))) return F(other) ? "your niece" : "your nephew";
        if (myParents.Any(p => Siblings(p, other))) return F(other) ? "your aunt" : "your uncle";
        if (theirParents.Any(tp => myParents.Any(mp => Siblings(tp, mp)))) return "your cousin";
        // by marriage: the spouse of a child or a sibling, and a spouse's kin
        if (w.MarriageOf(other.Id) is { } om && w.Char(om.A == other.Id ? om.B : om.A) is { } theirSpouse)
        {
            if (ChildOf(theirSpouse, me)) return F(other) ? "your son's wife" : "your daughter's husband";
            if (Siblings(theirSpouse, me)) return F(other) ? "your brother's wife" : "your sister's husband";
        }
        if (w.MarriageOf(me.Id) is { } mm && w.Char(mm.A == me.Id ? mm.B : mm.A) is { } mine)
        {
            if (ChildOf(other, mine)) return F(other) ? "your stepdaughter" : "your stepson";
            if (ChildOf(mine, other)) return F(other) ? "your spouse's mother" : "your spouse's father";
            if (Siblings(mine, other)) return F(other) ? "your spouse's sister" : "your spouse's brother";
        }
        return other.AdoptedById == me.Id ? "adopted by you" : other.HouseId == me.HouseId && other.BornHouseId.Length > 0 && other.BornHouseId != other.HouseId ? "married into the house" : "";
    }

    /// <summary>How <paramref name="other"/> stands to the player, as words a sentence can carry where the name would stand ({role.rel} in
    /// prose): "your son and heir", "your daughter's husband", "your cousin". Someone of the player's house or blood with no nearer word is
    /// "your kinsman" or "your kinswoman"; someone who is no kin at all is named, so the sentence still says who.</summary>
    public static string Said(World w, Character other)
    {
        var me = w.Protagonist;
        if (me == null) return other.Name;
        if (me.Id == other.Id) return "you";
        string word = Of(w, me, other);
        if (word == "adopted by you") word = F(other) ? "your adopted daughter" : "your adopted son";
        else if (word == "married into the house") word = F(other) ? "your kinswoman by marriage" : "your kinsman by marriage";
        else if (word.Length == 0)
        {
            bool blood = w.GetRelationship(me.Id, other.Id)?.Bond == BondType.Blood || w.GetRelationship(other.Id, me.Id)?.Bond == BondType.Blood;
            if (other.HouseId != me.HouseId && !blood) return other.Name;
            word = F(other) ? "your kinswoman" : "your kinsman";
        }
        return Systems.DynastySystem.FindHeir(w, me)?.Id == other.Id ? word + " and heir" : word;
    }

    /// <summary>"child of X and Y", from whoever of the two is known.</summary>
    public static string ChildOfSays(World w, Character c)
    {
        var names = new[] { c.FatherId, c.MotherId }.Select(w.Char).Where(p => p != null).Select(p => p!.Name).ToList();
        return names.Count == 0 ? "" : (F(c) ? "daughter of " : "son of ") + string.Join(" and ", names);
    }
}
