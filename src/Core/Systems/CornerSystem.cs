using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>One house's store of a good, and its share of all there is in the Reach.</summary>
public sealed record Holder(string HouseId, string Name, string Accent, int Units, int SharePct);

/// <summary>All there is of a good in the Reach: what the merchants hold, what the houses keep in their own stores, and who keeps the most.</summary>
public sealed record GoodHolding(string Good, int All, int Merchants, int Houses, IReadOnlyList<Holder> Holders)
{
    /// <summary>The house that holds the most, if any holds anything.</summary>
    public Holder? Top => Holders.Count > 0 ? Holders[0] : null;
    public int Of(string houseId) => Holders.FirstOrDefault(h => h.HouseId == houseId)?.Units ?? 0;
}

/// <summary>
/// Cornering a good (docs/ECONOMY_MARKET.md, "Cornering a good"; the owner's list: "who holds what, how much there is, and a price
/// that answers to a majority held; or a plain word that it cannot be done"). RNG-free, and the same rule for every house.
/// <list type="bullet">
/// <item>ALL THERE IS of a good is the merchants' stock and every house's store of it.</item>
/// <item>A house that holds a THIRD of it or more (and no fewer than <see cref="LeastUnits"/>) is FELT in the price; one that holds
/// HALF or more has CORNERED it. The premium is nothing at a third, half as much again at half, and double at the whole.</item>
/// <item>While a house is felt the merchants bring back half of what they would in a year; while a corner stands, a quarter: what
/// they bring is bought as it lands or priced out of reach.</item>
/// <item>A corner is heard of: the house is told, the chronicle records it, the price's cause names the holder, and the merchants
/// think the worse of the house, the year it is made and every year it stands. It lasts only while the share is held.</item>
/// </list>
/// This is not <see cref="OrderSystem"/>'s mark on a house that MAKES a quarter of the Reach's production of a strategic good.
/// </summary>
public static class CornerSystem
{
    /// <summary>A handful is not a corner: the fewest units a holding must be before the price feels it.</summary>
    public const int LeastUnits = 6;
    /// <summary>What the price gains at exactly half of all there is, and at the whole of it, in hundredths of the price.</summary>
    public const int HalfPremiumPct = 50, WholePremiumPct = 100;
    /// <summary>How much of their usual year's refill the merchants bring while a house is felt, and while a corner stands.</summary>
    public const int FeltRefillPct = 50, CorneredRefillPct = 25;
    /// <summary>What the merchants' estate thinks the worse of a house the year it corners a good, and each year the corner stands.</summary>
    public const int MerchantsOnCorner = 3, MerchantsEachYear = 1;
    /// <summary>How many of the largest holders the Exchange lists.</summary>
    public const int Shown = 3;
    public const string Estate = "merchants";

    public static bool Felt(int units, int all) => units >= LeastUnits && units * 3 >= all;
    public static bool Cornered(int units, int all) => units >= LeastUnits && units * 2 >= all;
    /// <summary>The fewest units that are a third, and that are half, of <paramref name="all"/>: never fewer than the least.</summary>
    public static int ThirdOf(int all) => Math.Max(LeastUnits, (all + 2) / 3);
    public static int HalfOf(int all) => Math.Max(LeastUnits, (all + 1) / 2);

    /// <summary>What a holding adds to the price the market aims at, as a share of it: nothing below a third; from a third to a half
    /// it rises to half as much again, slowly at first and steeply near half (the square of the way there, so a maker whose stores
    /// have crept past a third barely shows); from a half to the whole, evenly to double.</summary>
    public static double Premium(int units, int all)
    {
        if (all <= 0 || !Felt(units, all)) return 0;
        double share = Math.Min(1.0, units / (double)all), way = Math.Clamp((share - 1 / 3.0) * 6, 0, 1);
        return share < 0.5 ? HalfPremiumPct / 100.0 * way * way : HalfPremiumPct / 100.0 + (WholePremiumPct - HalfPremiumPct) / 100.0 * (share - 0.5) * 2;
    }

    /// <summary>How much of their usual refill the merchants bring this year, 0..1.</summary>
    public static double Refill(int units, int all) => Cornered(units, all) ? CorneredRefillPct / 100.0 : Felt(units, all) ? FeltRefillPct / 100.0 : 1;

    /// <summary>All there is of the good as it stands now, and every house that holds any, the largest first.</summary>
    public static GoodHolding Standing(World w, MarketCatalog cat, string good)
    {
        int merchants = cat.Good(good) != null ? (int)Math.Floor(Math.Max(0, MarketSystem.Of(w, cat).Goods[good].Stock) + 1e-9) : 0;
        var stores = w.Houses.Values.Where(h => !World.Outsider(h.Id)).Select(h => (h, n: Math.Max(0, h.Treasury.Get(good)))).Where(x => x.n > 0)
            .OrderByDescending(x => x.n).ThenBy(x => x.h.Id, StringComparer.Ordinal).ToList();
        int houses = stores.Sum(x => x.n), all = merchants + houses;
        return new GoodHolding(good, all, merchants, houses, stores.Select(x => new Holder(x.h.Id, x.h.Name, x.h.AccentColor, x.n, (int)Math.Round(100.0 * x.n / Math.Max(1, all)))).ToList());
    }

    /// <summary>The largest holders the Exchange lists: the first few, and the house asking if it holds any and is not among them.</summary>
    public static IReadOnlyList<Holder> Listed(GoodHolding s, string houseId)
    {
        var top = s.Holders.Take(Shown).ToList();
        if (top.All(h => h.HouseId != houseId) && s.Holders.FirstOrDefault(h => h.HouseId == houseId) is { } mine) top.Add(mine);
        return top;
    }

    /// <summary>May the good be bought on the exchanges at all as the law stands?</summary>
    public static bool Open(World w, TradeRules t, GoodDef d) =>
        d.Buy && (d.License.Length == 0 || (!t.Policies.IsEmpty && PolicySystem.ReachModifier(w, t.Policies, d.License) > 0));

    // ---------------------------------------------------------------- the yearly reckoning

    /// <summary>The year's reckoning of one good, called by the market before it sets the price: who holds a third or more, whether
    /// that is a corner, what it adds to the price, and the word that goes round when a corner is made or broken.</summary>
    public static void Reckon(World w, MarketCatalog cat, GoodDef d, GoodState g)
    {
        SenateActs.BreakCorner(w, cat, d, g);   // the law against forestalling, where the Senate has forbidden engrossing: the holder is made to sell first
        var s = Standing(w, cat, d.Id); var top = s.Top; var mine = w.Protagonist?.HouseId; string label = Resource.Label(d.Id);
        bool felt = top != null && Felt(top.Units, s.All), cornered = top != null && Cornered(top.Units, s.All);
        string wasHouse = g.CornerHouse; bool wasCornered = g.Cornered;
        string now = felt ? top!.HouseId : "";
        // a corner that stood last year and does not stand now, or has passed to another house, is broken
        if (wasCornered && (!cornered || now != wasHouse) && w.House(wasHouse) is { } old)
        {
            int left = s.Of(old.Id);
            w.Log($"{old.Name} no longer holds half of the Reach's {label}: the corner is broken.");
            if (old.Id == mine) w.Tell("info", $"Your house holds {left} of the {s.All} {label} in the Reach, which is no longer half: the corner is broken, and the price will come down.", "exchange:goods", d.Id);
            else w.Tell("info", $"{old.Name} no longer holds half of the Reach's {label}: the corner is broken, and the price will come down.", "exchange:goods", d.Id);
        }
        if (cornered)
        {
            var house = w.House(top!.HouseId)!; bool made = !wasCornered || now != wasHouse;
            if (made)
            {
                g.CornerSince = w.Turn;
                w.Log($"{house.Name} has cornered {label}: it holds {top.Units} of the {s.All} in the Reach.");
                if (house.Id == mine) w.Tell("good", $"Your house holds {top.Units} of the {s.All} {label} in the Reach: half or more of all there is. It is cornered. The price stands higher for as long as the house holds it, the merchants bring little, and they think the worse of the house for it.", "exchange:goods", d.Id);
                else w.Tell("warn", $"{house.Name} has cornered {label}: it holds {top.Units} of the {s.All} in the Reach. The price stands higher for as long as it does.", "exchange:goods", d.Id);
            }
            PolicySystem.Sway(w, house.Id, Estate, -(made ? MerchantsOnCorner : MerchantsEachYear));
        }
        else
        {
            g.CornerSince = -1;
            if (felt && now == mine && now != wasHouse)
                w.Tell("info", $"Your house holds {top!.Units} of the {s.All} {label} in the Reach, a third or more: the price begins to feel it. Half is {HalfOf(s.All)}.", "exchange:goods", d.Id);
        }
        g.CornerHouse = now; g.Cornered = cornered; g.CornerShare = felt ? Math.Min(1.0, top!.Units / (double)Math.Max(1, s.All)) : 0; g.CornerPremium = felt ? Math.Min(Premium(top!.Units, s.All), SenateActs.CornerCap(w)) : 0;
    }

    /// <summary>The corners in the market a house holds now, by the last reckoning.</summary>
    public static IEnumerable<string> HeldBy(World w, string houseId) =>
        w.Market == null ? Enumerable.Empty<string>() : w.Market.Goods.Values.Where(g => g.Cornered && g.CornerHouse == houseId).Select(g => g.Id).OrderBy(id => id, StringComparer.Ordinal);

    // ---------------------------------------------------------------- the plain word

    /// <summary>Who holds the good, if any house holds a third or more of it; "" if none does. As it stands now, not at the last reckoning.</summary>
    public static string Held(World w, TradeRules t, string houseId, string good)
    {
        var s = Standing(w, t.Market, good); var top = s.Top; if (top == null || !Felt(top.Units, s.All)) return "";
        bool mine = top.HouseId == houseId; double full = Premium(top.Units, s.All), cap = SenateActs.CornerCap(w); int pct = (int)Math.Round(100 * Math.Min(full, cap));
        string lift = pct > 0 ? $" The price it settles at is {pct}% higher for it." + (full > cap ? $" It would be {(int)Math.Round(100 * full)}%, and {SenateActs.LawName(w, SenateActs.CornerPremiumCap)} allows no more." : "") : "";
        if (SenateActs.CornersBroken(w) && Cornered(top.Units, s.All)) lift += $" Under {SenateActs.LawName(w, SenateActs.CornerForcedSale)} {(mine ? "your house" : "it")} will be made to sell down to {SenateActs.MayKeep(s.All)} at the year's reckoning.";
        if (Cornered(top.Units, s.All))
            return (mine ? $"Your house has cornered it: you hold {top.Units} of the {s.All} in the Reach." : $"{top.Name} has cornered it: it holds {top.Units} of the {s.All} in the Reach.") + lift
                + $" The merchants bring a quarter of what they would." + (mine ? $" Hold fewer than {HalfOf(s.All)} and the corner is broken." : "");
        return (mine ? $"You hold {top.Units} of the {s.All} in the Reach, a third or more, and the price feels it." : $"{top.Name} holds {top.Units} of the {s.All} in the Reach, a third or more, and the price feels it.") + lift
            + " The merchants bring half of what they would.";
    }

    /// <summary>What it would take the house to corner the good, in numbers, or why it cannot be done by buying; "" if the house holds
    /// the corner already.</summary>
    public static string Take(World w, TradeRules t, string houseId, string good)
    {
        var d = t.Market.Good(good); if (d == null) return "";
        var s = Standing(w, t.Market, good); var g = MarketSystem.Of(w, t.Market).Goods[good];
        if (s.All == 0) return "There is none in the Reach to corner.";
        int me = s.Of(houseId); if (Cornered(me, s.All)) return "";
        int half = HalfOf(s.All), third = ThirdOf(s.All), need = half - me; bool least = half > (s.All + 1) / 2;
        string mark = least ? $"the least that makes a corner (half of all there is, and no fewer than {LeastUnits})" : "half";
        // where this house is the one that holds a third, Held has said what it holds already
        string held = s.Top?.HouseId == houseId && Felt(me, s.All) ? "" : $"You hold {me} of the {s.All} in the Reach";
        if (!Open(w, t, d))
        {
            string shut = good == Resource.Isotopes ? "Isotopes are not sold on any open market while the law of the Arms Market keeps them off the exchanges" : $"{d.Name} cannot be bought on the exchanges";
            string kept = Largest(s, houseId);
            return (held.Length > 0 ? held + ". " : "") + $"{shut}, so a corner in them cannot be bought there: it is brought up from your own worlds, or bought out of the stores of the houses that hold them, on their pages under Trade"
                + (kept.Length > 0 ? $" ({kept})" : "") + $". {need} more is {mark}.";
        }
        int reach = me + s.Merchants, others = s.Houses - me; double made = g.LastProduction;
        if (need <= s.Merchants)
            return (held.Length > 0 ? held + ": " : "") + $"{need} more is {mark}, and the merchants hold {s.Merchants}." + (made >= 1 ? $" The Reach brings up {made:0} more a year, so a corner that is not fed is soon a smaller share." : "");
        string why = $"with all the merchants have you would hold {reach} of {s.All}, or {(int)Math.Round(100.0 * reach / s.All)}%";
        string up = made >= 1 ? $" The Reach brings up {made:0} more a year." : "";
        string close = !Felt(me, s.All) && third - me <= s.Merchants ? $" A third, from which the price begins to feel it, is {third - me} more." : "";
        string shelves = s.Merchants == 0 ? $"{Cap(mark)} is {half} and the merchants hold none" : $"{Cap(mark)} is {half} and the merchants hold only {s.Merchants}: {why}";
        // what the merchants cannot supply is in the other houses' stores, and can be bought of them (docs/WEALTH.md, "Trade between houses")
        string rest = others > 0
            ? $". The merchants cannot supply a corner: the other {need - s.Merchants} would have to be bought out of the houses' own stores, of what each will spare, on its page under Trade. The other houses keep {others}: {Largest(s, houseId)}.{up}"
            : $"{(s.Merchants == 0 ? ": it" : ". It")} cannot be cornered by buying.{up}";
        return (held.Length > 0 ? held + ". " : "") + shelves + rest + close;
    }

    /// <summary>The largest stores other than this house's, in a phrase: "House Wren holds 90 and House Marrow 60".</summary>
    public static string Largest(GoodHolding s, string houseId)
    {
        var top = s.Holders.Where(h => h.HouseId != houseId).Take(2).ToList();
        return top.Count == 0 ? "" : top.Count == 1 ? $"{top[0].Name} holds {top[0].Units}" : $"{top[0].Name} holds {top[0].Units} and {top[1].Name} {top[1].Units}";
    }

    /// <summary>The plain word on a good for one house: whether it is cornered and by whom, and what a corner would take.</summary>
    public static string Word(World w, TradeRules t, string houseId, string good)
    {
        string held = Held(w, t, houseId, good), take = Take(w, t, houseId, good);
        return held.Length == 0 ? take : take.Length == 0 ? held : held + " " + take;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
