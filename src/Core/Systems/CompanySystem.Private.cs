using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Shares a person holds in their own name (docs/WEALTH.md "A purse that holds more than credits"): bought out of
/// their purse, paying into it, theirs to leave. They are not the house's: they give it no say in the company, and nothing
/// that takes the house's holdings takes them.</summary>
public sealed partial class CompanySystem
{
    public const int PrivateCapPct = 10, InvestAt = 12, InvestKeeps = 6, InvestMost = 5;
    public const string PrivateBoughtKey = "private_shares_bought", PrivateSoldKey = "private_shares_sold", PrivateDividendsKey = "private_dividends", PrivateLeftKey = "private_shares_left";
    public const string KinInvestedFlag = "kin_invested", InvestedChar = "invested_of_their_own";

    public static bool IsPerson(string key) => key.StartsWith(Company.PersonPrefix, StringComparison.Ordinal);
    private static void Count(World w, string key, int by) => w.WorldCounters[key] = w.Counter(key) + by;

    /// <summary>What someone holds in their own name: company and shares, largest first.</summary>
    public static List<(Company Co, int Shares)> HeldPersonally(World w, string characterId)
    {
        string key = Company.PersonKey(characterId);
        return w.Companies.Where(c => c.HeldBy(key) > 0).OrderByDescending(c => c.HeldBy(key) * c.Price).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => (c, c.HeldBy(key))).ToList();
    }
    public static int PersonalWorth(World w, string characterId) => (int)w.Companies.Sum(c => c.HeldBy(Company.PersonKey(characterId)) * c.Price);
    /// <summary>What their shares are due at the next reckoning.</summary>
    public static int PersonalDividend(World w, string characterId) => w.Companies.Sum(c => c.Shares > 0 ? c.LastDividend * c.HeldBy(Company.PersonKey(characterId)) / c.Shares : 0);
    public static int PrivateCap(Company co) => Math.Max(1, co.Shares * PrivateCapPct / 100);

    public ShareQuote QuotePrivateBuy(World w, Character? who, Company? co, int n)
    {
        if (who?.Alive != true || co == null) return new ShareQuote(0, 0, 0, "no such company");
        if (n <= 0) return new ShareQuote(0, 0, 0, "nothing ordered");
        if (Barred(w, who.HouseId)) return new ShareQuote(0, 0, 0, "the Senate's embargo closes the Exchange to your house, and to you");
        int floated = co.HeldBy(Company.Public);
        if (floated < n) return new ShareQuote(0, 0, 0, floated == 0 ? "no shares on the float: its holders are not selling" : $"only {floated} on the float");
        int cap = PrivateCap(co), held = co.HeldBy(Company.PersonKey(who.Id));
        if (held + n > cap) return new ShareQuote(0, 0, 0, $"one person may hold no more than {cap} shares of it{(held > 0 ? $" (you hold {held})" : "")}");
        double cost = co.Price * (n + Rules.ImpactPct / 100.0 * (n * n / 2.0) / Math.Max(1, co.Shares));
        int credits = Math.Max(1, (int)Math.Ceiling(cost - 1e-9)); int levy = (int)Math.Ceiling(credits * LevyPct(w) / 100.0);
        return new ShareQuote(credits + levy, levy, cost / n, who.Purse < credits + levy ? $"the purse does not hold the {credits + levy} credits" : null);
    }

    public ShareQuote QuotePrivateSell(World w, Character? who, Company? co, int n)
    {
        if (who?.Alive != true || co == null) return new ShareQuote(0, 0, 0, "no such company");
        if (n <= 0) return new ShareQuote(0, 0, 0, "nothing offered");
        int held = co.HeldBy(Company.PersonKey(who.Id)); if (held < n) return new ShareQuote(0, 0, 0, $"only {held} held");
        double fetched = co.Price * Math.Max(0.3 * n, n - Rules.ImpactPct / 100.0 * (n * n / 2.0) / Math.Max(1, co.Shares)) * 0.98;
        int credits = Math.Max(0, (int)Math.Floor(fetched + 1e-9)); int levy = (int)Math.Ceiling(credits * LevyPct(w) / 100.0);
        return new ShareQuote(Math.Max(0, credits - levy), levy, fetched / n, null);
    }

    public bool BuyPrivately(World w, Character who, Company co, int n)
    {
        var q = QuotePrivateBuy(w, who, co, n); if (q.Refusal != null) return false; string key = Company.PersonKey(who.Id);
        who.Purse -= q.Credits; PayLevy(w, who.HouseId, q.Levy);
        co.Holders[Company.Public] = co.HeldBy(Company.Public) - n; co.Holders[key] = co.HeldBy(key) + n; co.Flow += n;
        w.WorldCounters[SharesTradedCounter] = w.Counter(SharesTradedCounter) + n; Count(w, PrivateBoughtKey, n);
        Recontrol(w, co);   // their voice is added to their house's, if they stand with it
        return true;
    }

    public bool SellPrivately(World w, Character who, Company co, int n)
    {
        var q = QuotePrivateSell(w, who, co, n); if (q.Refusal != null) return false; string key = Company.PersonKey(who.Id);
        who.Purse += q.Credits; PayLevy(w, who.HouseId, q.Levy);
        co.Holders[key] = co.HeldBy(key) - n; if (co.Holders[key] <= 0) co.Holders.Remove(key);
        co.Holders[Company.Public] = co.HeldBy(Company.Public) + n; co.Flow -= n;
        w.WorldCounters[SharesTradedCounter] = w.Counter(SharesTradedCounter) + n; Count(w, PrivateSoldKey, n);
        Recontrol(w, co);
        return true;
    }

    /// <summary>Does this person's holding vote with their house? The head's own does. Anyone else's does while they think no
    /// ill of the head; one who does keeps their shares and their dividends, and withholds their voice.</summary>
    public static bool VotesWithHouse(World w, Character c)
    {
        var house = w.House(c.HouseId); var head = house != null ? Economy.HeadOf(w, house) : null; if (!c.Alive || head == null) return false;
        return head.Id == c.Id || (w.GetRelationship(c.Id, head.Id)?.Disposition ?? 0) >= 0;
    }
    /// <summary>The shares a house can vote in a company: its own, and those its people hold in their own names and vote with it.
    /// A house with no share of its own has no voice for them to add to.</summary>
    public static int VotesOf(World w, Company co, string houseId)
    {
        int own = co.HeldBy(houseId); if (own <= 0) return 0;
        foreach (var kv in co.Holders) if (IsPerson(kv.Key) && kv.Value > 0 && w.Char(kv.Key[Company.PersonPrefix.Length..]) is { } c && c.HouseId == houseId && VotesWithHouse(w, c)) own += kv.Value;
        return own;
    }

    /// <summary>Pay a holder what a company owes them: a house into its treasury, a person into their purse.</summary>
    private static void PayHolder(World w, string key, int credits)
    {
        if (credits <= 0) return;
        if (IsPerson(key)) { if (w.Char(key[Company.PersonPrefix.Length..]) is { Alive: true } c) c.Purse += credits; }
        else if (w.House(key) is { } h) h.Treasury.Credits += credits;
    }

    /// <summary>The dead hold nothing. What they held goes as their will says: <paramref name="pct"/> of each holding to the
    /// one remembered, the rest to whoever comes after them.</summary>
    public static void Leave(World w, Character dead, Character? to, Character? legatee = null, int pct = 0)
    {
        string from = Company.PersonKey(dead.Id);
        foreach (var co in w.Companies.Where(c => c.HeldBy(from) > 0).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            int n = co.HeldBy(from); co.Holders.Remove(from);
            int remembered = legatee is { Alive: true } && legatee.Id != to?.Id ? n * Math.Clamp(pct, 0, 100) / 100 : 0;
            if (remembered > 0) { string k = Company.PersonKey(legatee!.Id); co.Holders[k] = co.HeldBy(k) + remembered; }
            if (to is { Alive: true }) { string k = Company.PersonKey(to.Id); co.Holders[k] = co.HeldBy(k) + n - remembered; }
            else co.Holders[Company.Public] = co.HeldBy(Company.Public) + n - remembered;
            Count(w, PrivateLeftKey, n);
            if (dead.HouseId == w.Protagonist?.HouseId) w.Log($"{dead.Name}'s {n} shares of {co.Name} go to {(to is { Alive: true } ? to.Name : "the float")}{(remembered > 0 ? $", {remembered} of them to {legatee!.Name}" : "")}.");
        }
    }

    /// <summary>Shares in a dead person's name that no will has dealt with go to the head of their house, or to the float.</summary>
    private static void SettleTheDead(World w, Company co)
    {
        foreach (var key in co.Holders.Keys.Where(IsPerson).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            var c = w.Char(key[Company.PersonPrefix.Length..]); if (c is { Alive: true }) continue;
            var house = c != null ? w.House(c.HouseId) : null; var head = house != null ? Economy.HeadOf(w, house) : null;
            if (c != null) Leave(w, c, head is { Alive: true } && head.Id != c.Id ? head : null);
            else { co.Holders[Company.Public] = co.HeldBy(Company.Public) + co.Holders[key]; co.Holders.Remove(key); }
        }
    }

    private static bool Invests(Character c) =>
        c.AptitudeTraits.Contains("Greedy") || c.Ambition == "amass_a_fortune" || c.Pursuit?.Goal == "amass_a_fortune" || c.Skill("economics") >= 3;

    /// <summary>The year's dealings of those who hold shares of their own: what the shares paid goes into each purse; one of
    /// the house in debt sells to pay it; one with a head for money and something put by buys. Called after the purses are
    /// reckoned, so that what is said of each person's year is what they did last.</summary>
    public void PrivateHolders(World w)
    {
        w.WorldFlags.Remove(KinInvestedFlag); foreach (var c in w.Characters.Values) c.Flags.Remove(InvestedChar);
        if (!Open) return;
        foreach (var co in w.Companies.OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            SettleTheDead(w, co);
            if (co.LastDividend <= 0 || co.Shares <= 0) continue;
            foreach (var kv in co.Holders.Where(kv => IsPerson(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList())
            { int due = co.LastDividend * kv.Value / co.Shares; if (due > 0) { PayHolder(w, kv.Key, due); Count(w, PrivateDividendsKey, due); } }
        }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p is not { Alive: true } || house == null) return;
        foreach (var c in WealthSystem.Members(w, house).Where(c => c.Id != p.Id))
        {
            var held = HeldPersonally(w, c.Id);
            if (c.Purse < 0 && held.Count > 0)
            {
                // sell what covers the debt, the largest holding first
                foreach (var (co, n) in held)
                {
                    if (c.Purse >= 0) break; int sell = 0; while (sell < n && c.Purse + QuotePrivateSell(w, c, co, sell).Credits < 0) sell++;
                    if (sell > 0 && SellPrivately(w, c, co, sell)) { c.LastSpend = $"Sold {sell} share{(sell == 1 ? "" : "s")} of {co.Name} to pay what they owe."; if (c.Purse > WealthSystem.DebtAt) c.Flags.Remove(WealthSystem.InDebtChar); }
                }
                continue;
            }
            if (!Invests(c) || c.Purse < InvestAt || Barred(w, house.Id)) continue;
            // the company that pays best for its price, of those with shares to be had
            var pick = w.Companies.Where(co => co.LastDividend > 0 && co.Price > 0 && co.HeldBy(Company.Public) > 0 && co.HeldBy(Company.PersonKey(c.Id)) < PrivateCap(co))
                .OrderByDescending(co => co.LastDividend / (co.Price * Math.Max(1, co.Shares))).ThenBy(co => co.Id, StringComparer.Ordinal).FirstOrDefault();
            if (pick == null) continue;
            int buy = 0; while (buy < InvestMost && QuotePrivateBuy(w, c, pick, buy + 1) is { Refusal: null } q && c.Purse - q.Credits >= InvestKeeps) buy++;
            if (buy == 0 || !BuyPrivately(w, c, pick, buy)) continue;
            c.LastSpend = $"Bought {buy} share{(buy == 1 ? "" : "s")} of {pick.Name}."; c.Flags.Add(InvestedChar); w.WorldFlags.Add(KinInvestedFlag);
            w.Log($"{c.Name} buys {buy} share{(buy == 1 ? "" : "s")} of {pick.Name} with their own money.");
        }
    }
}
