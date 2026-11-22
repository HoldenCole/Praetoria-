using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The belt claims other houses hold in a system (docs/PLAYTHROUGH.md, Medium, Building and holding, B50). A claim is a small
/// holding of its own: it brings up what the belt holds for the house that staked it, and owes the holder of the system's world
/// nothing. The holder of that world may offer to buy it out. The price is by rule: twice what staking a claim costs, at
/// today's prices; half as much again from one who thinks ill of the buyer, a quarter less from a friend. A house does not sell
/// the last thing it holds, nor to a house it is at war with, nor to one its head thinks <see cref="WontAt"/> or worse of.
/// Bought, the claim is the buyer's; a buyer who has a claim in that belt already closes the bought one down, and the belt has
/// room for another. RNG-free.
/// </summary>
public static class ClaimSystem
{
    public const int BuyOutPct = 200, FriendPct = 150, IllPct = 300, Friend = 30, Ill = -10, WontAt = -30;
    public const string BoughtOutCounter = "claims_bought_out";

    /// <summary>The claims other houses hold in a system whose world this house holds.</summary>
    public static IEnumerable<Holding> Of(World w, string systemId, string holderId) =>
        w.PlanetHolder(systemId) != holderId ? Enumerable.Empty<Holding>()
            : w.Holdings.Values.Where(h => h.IsClaim && h.SystemId == systemId && h.OwnerId != holderId).OrderBy(h => h.Id, StringComparer.Ordinal);

    private static int Regard(World w, House of, House to)
    {
        var a = Economy.HeadOf(w, of); var b = Economy.HeadOf(w, to);
        return a != null && b != null ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0;
    }
    /// <summary>What a claim's house would take for it from this buyer.</summary>
    public static int Price(World w, BusinessCatalog ventures, Holding claim, House buyer)
    {
        var owner = w.House(claim.OwnerId); var f = ventures.Finance;
        double stake = Economy.WorthOf(w, new Resources { Credits = f.ClaimCredits, Materials = f.ClaimMaterials });
        int regard = owner != null ? Regard(w, owner, buyer) : 0;
        int pct = regard >= Friend ? FriendPct : regard <= Ill ? IllPct : BuyOutPct;
        return Math.Max(1, (int)Math.Ceiling(stake * pct / 100));
    }
    public static string? BuyOutRefusal(World w, BusinessCatalog ventures, House buyer, Holding? claim)
    {
        if (claim == null || !claim.IsClaim) return "no such claim";
        if (claim.OwnerId == buyer.Id) return "it is yours already";
        if (w.PlanetHolder(claim.SystemId) != buyer.Id) return "you do not hold the world of that system";
        var owner = w.House(claim.OwnerId); if (owner == null) return "nobody to buy it from";
        if (w.AtWar(owner.Id, buyer.Id)) return $"you are at war with {owner.Name}: it is taken, not bought";
        int regard = Regard(w, owner, buyer);
        if (regard <= WontAt) return $"{owner.Name} will not sell to you: its head thinks {regard:+0;-0} of you";
        if (w.HoldingsOf(owner.Id).Count() <= 1 && !w.BusinessesOf(owner.Id).Any()) return $"it is all {owner.Name} has: it will not sell";
        int price = Price(w, ventures, claim, buyer);
        return buyer.Treasury.Credits >= price ? null : $"needs {Money.Credits(price)}";
    }
    /// <summary>The claim changes hands for the price; a buyer with a claim in that belt already closes this one down.</summary>
    public static void BuyOut(World w, BusinessCatalog ventures, House buyer, Holding claim)
    {
        var owner = w.House(claim.OwnerId)!; int price = Price(w, ventures, claim, buyer); var sys = w.Galaxy?.System(claim.SystemId);
        buyer.Treasury.Credits -= price; owner.Treasury.Credits += price;
        bool closes = w.Holdings.Values.Any(h => h.IsClaim && h.SystemId == claim.SystemId && h.OwnerId == buyer.Id);
        if (closes) w.Holdings.Remove(claim.Id);
        else
        {
            // a claim is kept under its house's name: under the seller's it would be written over the next time the seller staked here
            w.Holdings.Remove(claim.Id); w.Sieges.Remove(claim.Id);
            claim.Id = $"claim_{buyer.Id}_{claim.SystemId}"; claim.FormerOwnerId = owner.Id; claim.OwnerId = buyer.Id; claim.GovernorId = Economy.HeadOf(w, buyer)?.Id ?? "";
            w.Holdings[claim.Id] = claim;
        }
        w.HoldingsChanged(); w.WorldCounters[BoughtOutCounter] = w.Counter(BoughtOutCounter) + 1;
        w.Log($"{buyer.Name} buys out the claim of {owner.Name} in the {(sys?.Name ?? claim.SystemId)} belt for {Money.Credits(price)}{(closes ? ", and closes it down" : "")}.");
    }
}
