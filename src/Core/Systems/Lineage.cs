using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What a person looks like by blood (docs/ART_DIRECTION.md, "Portraits"; the owner: "certain houses will have certain
/// genetics ... unless they were brought into the house"). A house has an ancestry: the one it was given, else the one content names
/// for it, else its nation's, else one that falls to it by its id. A person with a parent on record takes after the father or the
/// mother, by the dice of their own id; a person with none takes the ancestry of the house they were born to, so someone who marries
/// in keeps their own. Nothing here is stored but a house's choice, so it holds for every house however it came to be.</summary>
public static class Lineage
{
    public static string OfHouse(World w, AncestryCatalog cat, string houseId)
    {
        var h = w.House(houseId);
        if (h != null && h.Ancestry.Length > 0 && cat.Def(h.Ancestry) != null) return h.Ancestry;
        if (cat.Houses.TryGetValue(houseId, out var named) && cat.Def(named) != null) return named;
        if (w.NationOfHouse(houseId) is { } nation && cat.Nations.TryGetValue(nation.Id, out var theirs) && cat.Def(theirs) != null) return theirs;
        var pool = cat.OfTheReach.Where(d => d.Weight > 0).ToList(); if (pool.Count == 0) pool = cat.Defs.ToList(); if (pool.Count == 0) return "";
        long roll = Hash("ancestry|" + houseId) % pool.Sum(d => (long)Math.Max(1, d.Weight));
        foreach (var d in pool) { roll -= Math.Max(1, d.Weight); if (roll < 0) return d.Id; }
        return pool[^1].Id;
    }

    public static string Of(World w, AncestryCatalog cat, Character c) => Of(w, cat, c, 0);
    private static string Of(World w, AncestryCatalog cat, Character c, int depth)
    {
        if (depth < 12)
        {
            var father = c.FatherId.Length > 0 ? w.Char(c.FatherId) : null; var mother = c.MotherId.Length > 0 ? w.Char(c.MotherId) : null;
            var parent = father != null && mother != null ? (TakesAfterFather(c.Id) ? father : mother) : father ?? mother;
            if (parent != null) return Of(w, cat, parent, depth + 1);
        }
        return OfHouse(w, cat, c.BornHouseId.Length > 0 && w.House(c.BornHouseId) != null ? c.BornHouseId : c.HouseId);
    }
    /// <summary>Whether a child of two known parents takes after the father (else the mother): the dice of the child's own id.</summary>
    public static bool TakesAfterFather(string characterId) => Hash("kin|" + characterId) % 2 == 0;

    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) { h ^= ch; h *= 16777619; } h ^= h >> 15; h *= 2246822519; h ^= h >> 13; return h; }
}
