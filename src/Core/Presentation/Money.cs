using System.Globalization;
using Praetoria.Core.State;

namespace Praetoria.Core;

/// <summary>
/// How money is written (docs/MONEY.md; the owner, 2026-11-22: "If you're paying a commoner 1-3 for a position, but your house total
/// is like 24 that math doesn't make sense... the costs and economy for credits needs to be rescaled").
/// <para>A house keeps its books in <b>hundreds of credits</b>. Every amount the rules reckon with (a treasury, a cost, a yield, a
/// purse, a debt, a price, a fee, a bribe, a dowry, a house's wealth) is a whole number of hundreds, as it always was: no rule changed
/// when the scale did. What changed is what a player is shown: 28 in the books is written "2,800".</para>
/// <para>Below a hundred there are single credits: <see cref="Resources.Coin"/>. A servant's wage is 1 to 3 of them a year. Only what is
/// truly small is counted in them.</para>
/// Every amount of credits the game writes for a player goes through here, so that no screen shows the books' own figure.
/// </summary>
public static class Money
{
    /// <summary>Credits in one unit of a house's books.</summary>
    public const int PerUnit = 100;
    private static string N(long credits) => credits.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>An amount from the books, as the number of credits it is: 28 is "2,800".</summary>
    public static string Show(long units) => N(units * PerUnit);
    /// <summary>The same for a reckoned figure that is not whole (an average, a worth a year): 1.25 is "125".</summary>
    public static string Show(double units) => N((long)Math.Round(units * PerUnit));
    /// <summary>With its sign, for a change: "+800", "-200", "0".</summary>
    public static string Signed(long units) => (units > 0 ? "+" : "") + Show(units);
    /// <summary>With the word: "2,800 credits".</summary>
    public static string Credits(long units) => Show(units) + " credits";
    /// <summary>For a loop over every resource: credits as credits, anything else as the count it is.</summary>
    public static string Of(string resource, long n) => resource == Resource.Credits ? Show(n) : n.ToString(CultureInfo.InvariantCulture);
    public static string SignedOf(string resource, long n) => (n > 0 ? "+" : "") + Of(resource, n);
    /// <summary>What a treasury holds, to the single credit: its hundreds and its odd coin.</summary>
    public static string Held(Resources t) => N(t.Total);
    /// <summary>What moved between two states of a treasury, a part for each resource: "−205 credits", "+3 materials". Credits are to the
    /// single credit, so a scene that paid a servant's purse says so.</summary>
    public static List<string> Changed(Resources before, Resources after)
    {
        var parts = new List<string>(); long d = after.Total - before.Total;
        if (d != 0) parts.Add((d > 0 ? "+" : "−") + N(Math.Abs(d)) + " credits");
        foreach (var k in Resource.All) { if (k == Resource.Credits) continue; int by = after.Get(k) - before.Get(k); if (by != 0) parts.Add((by > 0 ? "+" : "−") + Math.Abs(by).ToString(CultureInfo.InvariantCulture) + " " + Resource.Label(k)); }
        return parts;
    }
    /// <summary>An amount that is already in single credits (a servant's wage): "1 credit", "6 credits".</summary>
    public static string Coin(long credits) => credits == 1 ? "1 credit" : N(credits) + " credits";
}
