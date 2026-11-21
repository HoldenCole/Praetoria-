using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>What one lender would lend a house, and on what terms (docs/PLAYTHROUGH.md, Medium, Money: "borrow an amount you
/// choose, from a lender you can see"). <see cref="LenderId"/> is "" for the caravaneers' counting-houses.</summary>
public sealed record LoanTerms(string LenderId, string Lender, int Limit, int RatePct, string Why, string? Refusal);

/// <summary>
/// Borrowing by choice. A house may borrow from the caravaneers' counting-houses, who lend to anyone, or from any house that
/// keeps a bank. Each names a limit and a rate: the counting-houses lend on the worlds a house holds; a bank lends by its
/// size and those worlds, at a rate that follows what its head thinks of the borrower. One lender at a time: the lender a
/// house already owes will lend more, up to its limit; another wants the first paid. The standing loan the stewards of other
/// houses take (<see cref="Commands.TakeLoanCommand"/> with no lender named) is the same thing at the counting-houses' terms.
/// </summary>
public static class Lending
{
    public const string CountingHouses = "";
    /// <summary>Amounts are offered in steps of this many credits.</summary>
    public const int Step = 5;
    /// <summary>What a lender adds to its limit for each world the borrower holds, and how many worlds it counts.</summary>
    public const int PerWorld = 10, WorldsCounted = 4;
    /// <summary>What a bank will lend for each level it has.</summary>
    public const int PerBankLevel = 20;
    /// <summary>A banker who thinks this well of you takes <see cref="RegardPct"/> off the rate; this ill, adds it; at <see cref="WontAt"/> lends nothing.</summary>
    public const int GoodRegard = 20, IllRegard = -10, WontAt = -30, RegardPct = 10;
    /// <summary>What a patron takes off for a client, and what a house known for bad business pays more everywhere.</summary>
    public const int PatronPct = 10, BadNamePct = 10;
    public const int LeastRate = 5;

    /// <summary>The counting-houses' rate: what the standing loan has always cost (20 for 26 is 30 in the hundred).</summary>
    public static int BaseRate(FinanceRules f) => (f.LoanRepay - f.LoanPrincipal) * 100 / Math.Max(1, f.LoanPrincipal);
    /// <summary>What is owed on an amount borrowed at a rate: the whole of it, interest and all, is owed from the day.</summary>
    public static int Owed(int amount, int ratePct) => amount + (amount * ratePct + 99) / 100;
    /// <summary>The yearly instalment on what is owed, in the proportion the standing loan has always been paid.</summary>
    public static int Instalment(FinanceRules f, int owed) => owed <= 0 ? 0 : Math.Max(1, (owed * f.LoanInstalment + f.LoanRepay - 1) / Math.Max(1, f.LoanRepay));
    public static int Years(int owed, int instalment) => owed <= 0 || instalment <= 0 ? 0 : (owed + instalment - 1) / instalment;
    /// <summary>What a house pays this year on what it owes.</summary>
    public static int InstalmentOf(House h, FinanceRules f) => h.Debt <= 0 ? 0 : Math.Min(h.Debt, h.LoanInstalment > 0 ? h.LoanInstalment : f.LoanInstalment);

    private static int Worlds(World w, string houseId) => Math.Min(WorldsCounted, w.HoldingsOf(houseId).Count(h => h.IsPlanet));

    /// <summary>Every lender there is for this house, the counting-houses first, then the banks by what they would lend.</summary>
    public static List<LoanTerms> Lenders(World w, string borrowerId, BusinessCatalog ventures)
    {
        var list = new List<LoanTerms>(); var house = w.House(borrowerId); if (house == null) return list;
        var f = ventures.Finance; int baseRate = BaseRate(f), worlds = Worlds(w, borrowerId); bool bad = VentureSystem.BadName(w, borrowerId);
        string? Elsewhere(string lenderId) => house.Debt > 0 && house.LenderId != lenderId
            ? $"you owe {house.Debt} to {LenderName(w, house.LenderId)}: a second lender wants the first paid" : null;
        {
            int limit = f.LoanPrincipal + PerWorld * worlds, rate = baseRate + (bad ? BadNamePct : 0);
            string why = $"They lend to anyone: {f.LoanPrincipal}, and {PerWorld} more for each world held (they count {WorldsCounted} at most), at {baseRate} in the hundred"
                + (bad ? $", and {BadNamePct} more to a house known for bad business" : "") + ".";
            list.Add(new LoanTerms(CountingHouses, "the counting-houses", limit, rate, why, Elsewhere(CountingHouses)));
        }
        var me = Economy.HeadOf(w, house);
        foreach (var bank in w.Businesses.Values.Where(b => b.OwnerId != borrowerId && !World.Outsider(b.OwnerId) && ventures.Def(b.Kind)?.Lender == true)
                     .GroupBy(b => b.OwnerId).Select(g => (owner: g.Key, level: g.Max(b => b.Level))).OrderByDescending(x => x.level).ThenBy(x => x.owner, StringComparer.Ordinal))
        {
            var owner = w.House(bank.owner); var head = owner != null ? Economy.HeadOf(w, owner) : null; if (owner == null) continue;
            int regard = head != null && me != null ? w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0 : 0;
            int limit = PerBankLevel * bank.level + PerWorld * worlds, rate = baseRate; var because = new List<string>();
            if (regard >= GoodRegard) { rate -= RegardPct; because.Add($"{RegardPct} off, for {head!.Name} thinks well of you ({regard:+0;-0})"); }
            else if (regard <= IllRegard) { rate += RegardPct; because.Add($"{RegardPct} more, for {head!.Name} thinks ill of you ({regard:+0;-0})"); }
            if (house.PatronId == owner.Id) { rate -= PatronPct; because.Add($"{PatronPct} off for a client"); }
            if (bad) { rate += BadNamePct; because.Add($"{BadNamePct} more to a house known for bad business"); }
            rate = Math.Max(LeastRate, rate);
            string? no = w.AtWar(owner.Id, borrowerId) ? "you are at war with them"
                : regard <= WontAt ? $"{head?.Name ?? owner.Name} will not lend to you: they think {regard:+0;-0} of you, and {WontAt} is where a banker stops"
                : Elsewhere(owner.Id);
            string why = $"A bank of level {bank.level} lends {PerBankLevel} for each level, and {PerWorld} more for each world you hold. The rate is {baseRate} in the hundred"
                + (because.Count > 0 ? "; " + string.Join("; ", because) : "") + ".";
            list.Add(new LoanTerms(owner.Id, $"the bank of {owner.Name}", limit, rate, why, no));
        }
        return list;
    }

    public static string LenderName(World w, string lenderId) => lenderId.Length > 0 && w.House(lenderId) is { } h ? $"the bank of {h.Name}" : "the counting-houses";

    /// <summary>The most this house may still borrow of a lender: its limit, less what is already owed it.</summary>
    public static int Room(World w, House house, LoanTerms t) => Math.Max(0, (t.Limit - (house.LenderId == t.LenderId ? house.Debt : 0)) / Step * Step);
}
