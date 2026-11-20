using Praetoria.Core.Data;
using Praetoria.Core.Rng;

namespace Praetoria.Core.Galaxy;

/// <summary>
/// Fills in a solar system (docs/SYSTEMS.md; the owner, 2026-11-20: "a solar system ... should be multiple planets, asteroids and
/// other things per solar system ... every system should be slightly different from each other"). The map file writes a system's
/// world and a sister or two. This adds the rest, once, when a game begins: the sun itself, a body for every deposit and a belt where
/// the system has one, moons for the world and for the giants, something far out, a comet for some, and one or two features that no
/// neighbour has. It only ever adds to the end of a system's bodies, so a body's place in the list is its name for good. What a system
/// gets follows from its id alone: the same system is the same in every game.
/// </summary>
public static class SystemBodies
{
    private static ulong Hash(string key) { ulong h = 14695981039346656037UL; foreach (var ch in key) h = (h ^ ch) * 1099511628211UL; return h; }
    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

    /// <summary>Fill in every system of the galaxy that has not been. Safe to call again: a furnished system is left alone.</summary>
    public static void Furnish(Galaxy g, HoldingCatalog cat)
    {
        foreach (var sys in g.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal)) if (!sys.Furnished) Furnish(sys, cat);
    }

    public static void Furnish(StarSystem sys, HoldingCatalog cat)
    {
        var rng = new SplitMix64Rng(Hash("bodies:" + sys.Id) | 1);
        string bare = sys.Name.StartsWith("The ", StringComparison.Ordinal) ? sys.Name[4..] : sys.Name;
        if (!sys.Bodies.Any(b => b.IsPrimary)) sys.Bodies.Insert(0, new Body { Name = sys.Name, Class = sys.PlanetClass, Size = 3, IsPrimary = true });
        string Numbered() { var used = sys.Bodies.Select(b => b.Name).ToHashSet(); foreach (var n in Numerals) { string name = $"{bare} {n}"; if (!used.Contains(name)) return name; } return $"{bare} {sys.Bodies.Count + 1}"; }
        Body Add(string name, string cls, int size, int orbits = -1) { var b = new Body { Name = name, Class = cls, Size = size, Orbits = orbits }; sys.Bodies.Add(b); return b; }
        int Planets() => sys.Bodies.Count(b => b.Orbits < 0 && !b.IsStar);

        // a body for each deposit: helium-3 in a giant's clouds, rare earths in a belt or a dead world, isotopes under rock or ice
        foreach (var res in sys.Deposits.Where(d => d.Value > 0).Select(d => d.Key).OrderBy(k => k, StringComparer.Ordinal))
        {
            string[] kinds = res == "helium3" ? new[] { "gas" } : res == "rare_earths" ? new[] { Body.Belt, "barren", "arid" } : new[] { "barren", "frozen" };
            var body = sys.Bodies.Where(b => b.Deposit.Length == 0 && kinds.Contains(b.Class)).OrderBy(b => Array.IndexOf(kinds, b.Class)).ThenBy(b => b.IsPrimary).FirstOrDefault();
            body ??= kinds[0] == Body.Belt ? Add($"The {bare} Belt", Body.Belt, 2) : Add(Numbered(), kinds[0], kinds[0] == "gas" ? 3 : 2);
            body.Deposit = res;
        }
        if (sys.Belt > 0 && !sys.Bodies.Any(b => b.Class == Body.Belt)) Add($"The {bare} Belt", Body.Belt, 2);

        // moons: for the system's own world, and for its giants
        void Moons(Body parent, int count)
        {
            int at = sys.Bodies.IndexOf(parent); bool numbered = Numerals.Any(n => parent.Name.EndsWith(" " + n, StringComparison.Ordinal));
            string stem = parent.Name.StartsWith("The ", StringComparison.Ordinal) ? parent.Name[4..] : parent.Name;
            string[] ordinals = { "Minor", "Secunda", "Tertia", "Quarta", "Quinta" };
            for (int i = 0; i < count; i++)
            {
                var taken = sys.Bodies.Select(b => b.Name).ToHashSet();   // (the map file may have named a sister-world "Minor" already)
                string name = numbered ? Enumerable.Range(0, 26).Select(k => $"{stem}-{(char)('a' + k)}").First(n => !taken.Contains(n)) : ordinals.Select(o => $"{stem} {o}").FirstOrDefault(n => !taken.Contains(n)) ?? $"{stem} Moon {i + 1}";
                Add(name, Body.Moon, 1, at);
            }
        }
        var primary = sys.Primary;
        if (primary.Class is not (Body.Belt or "gas")) Moons(primary, rng.NextInt(0, 3)); else if (primary.Class == "gas") Moons(primary, rng.NextInt(1, 3));
        foreach (var giant in sys.Bodies.Where(b => b.Class == "gas" && !b.IsPrimary).ToList()) if (sys.Bodies.Count < 9) Moons(giant, rng.NextInt(0, 3));

        // something far out, until the system has five things going round its sun; and for some a comet
        string[] outer = { "frozen", "gas", "barren", "frozen" };
        for (int guard = 0; Planets() < 4 + rng.NextInt(0, 2) && sys.Bodies.Count < 9 && guard < 6; guard++)
        {
            string cls = outer[rng.NextInt(0, outer.Length)]; if (cls == "gas" && sys.Bodies.Count(b => b.Class == "gas") >= 2) cls = "frozen";
            Add(Numbered(), cls, cls == "gas" ? 3 : rng.NextInt(1, 3));
        }
        if (rng.NextInt(0, 100) < 38) Add($"The {bare} Comet", Body.Comet, 1);
        Add($"The Sun of {bare}", Body.Star, 3);

        // what makes it unlike its neighbours: one feature, and for some a second, each on a body of a kind that can have it
        int wanted = 1 + (rng.NextInt(0, 100) < 45 ? 1 : 0); var used = new HashSet<string>();
        for (int n = 0; n < wanted && cat.Features.Count > 0; n++)
        {
            var open = sys.Bodies.Where(b => b.Feature.Length == 0 && cat.Features.Any(f => !used.Contains(f.Id) && f.Bodies.Contains(b.Class))).ToList(); if (open.Count == 0) break;
            var body = open[rng.NextInt(0, open.Count)];
            var fits = cat.Features.Where(f => !used.Contains(f.Id) && f.Bodies.Contains(body.Class)).ToList();
            int roll = rng.NextInt(0, Math.Max(1, fits.Sum(f => Math.Max(1, f.Weight)))); var pick = fits[^1];
            foreach (var f in fits) { if (roll < Math.Max(1, f.Weight)) { pick = f; break; } roll -= Math.Max(1, f.Weight); }
            body.Feature = pick.Id; used.Add(pick.Id);
        }
        sys.Furnished = true;
    }
}
