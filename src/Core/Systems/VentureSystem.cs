using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Ventures as assets (docs/ECONOMY_MARKET.md stage B). A venture has a value — six years of its earnings at this year's prices, half its
/// book value at least — and changes hands at a price a rule sets, not a whim:
/// <list type="bullet">
/// <item>the OPEN MARKET pays four-fifths of value and the caravaneers break the venture up;</item>
/// <item>a HOUSE bids by what it is worth to it: more if it holds the world the venture works, more if it likes the seller, more if it is
/// rich, nothing if it cannot pay and keep a reserve;</item>
/// <item>a house ASKS a premium to part with one, less if it is in distress, more for a venture on its own world or its last one, and
/// will not sell to a house it hates;</item>
/// <item>the holder of the world has FIRST REFUSAL on any sale there at the agreed price.</item>
/// </list>
/// A steward in distress sells to the best bidder, offering the player first when the player holds the world or is a friend with the
/// coin. RNG-free.
/// </summary>
public sealed class VentureSystem
{
    public const int MarketPct = 80, BidPct = 85, HolderPremium = 25, AskPct = 130, DistressAskPct = 95, OwnWorldAsk = 30, LastVentureAsk = 40, Reserve = 15, OfferPct = 90;
    public const string OfferedFlag = "venture_offered", SoldCounter = "ventures_sold", BoughtFlag = "venture_bought", RefusedFirstFlag = "first_refusal_taken";

    private readonly Economy _economy; private readonly BusinessCatalog _ventures;
    public VentureSystem(Economy economy, BusinessCatalog ventures) { _economy = economy; _ventures = ventures; }
    public Economy Economy => _economy;
    public BusinessCatalog Catalog => _ventures;

    public int Value(World w, Business b) => _economy.VentureValue(w, b);
    public int Earnings(World w, Business b) => _economy.VentureEarnings(w, b);
    public int Salvage(Business b) => (_ventures.Def(b.Kind)?.Value(b.Level) ?? 0) * Economy.SalvagePct / 100;
    /// <summary>What the open market pays: four-fifths of value, never less than salvage.</summary>
    public int MarketPrice(World w, Business b) => Math.Max(Salvage(b), Value(w, b) * MarketPct / 100);

    private static int Regard(World w, House from, House to)
    {
        var a = Economy.HeadOf(w, from); var b = Economy.HeadOf(w, to);
        return a != null && b != null ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0;
    }
    /// <summary>The house holding the world the venture works; none for a venture that works no system.</summary>
    private static string? Holder(World w, Business b) => b.SystemId.Length == 0 ? null : w.PlanetHolder(b.SystemId);
    private static bool Distressed(House h) => h.Treasury.Credits < 0 || h.Debt > 0;

    /// <summary>What a house would pay for a venture, or 0 if it would not or cannot.</summary>
    public int Bid(World w, string buyerId, Business b)
    {
        var buyer = w.House(buyerId); var seller = w.House(b.OwnerId);
        if (buyer == null || seller == null || buyerId == b.OwnerId || buyerId == PirateSystem.HouseId || b.CompanyId.Length > 0) return 0;
        if (w.AtWar(buyerId, b.OwnerId)) return 0;
        if (w.Businesses.Values.Any(o => o.OwnerId == buyerId && o.Kind == b.Kind && o.SystemId == b.SystemId)) return 0;   // one of a kind a system
        bool holder = Holder(w, b) == buyerId;
        if (Earnings(w, b) <= 0 && !holder) return 0;
        int pct = BidPct + (holder ? HolderPremium : 0) + (FactionSystem.Of(w, buyerId)?.PartyId == "factors" ? 5 : 0);
        int regard = Regard(w, buyer, seller); pct += regard >= 20 ? 5 : regard <= -20 ? -10 : 0;
        if (Wealth.TierIndex(Wealth.TierFor(w, buyerId)) >= 3) pct += 5;
        int bid = Value(w, b) * pct / 100;
        return bid > 0 && buyer.Treasury.Credits >= bid + Reserve ? bid : 0;
    }

    /// <summary>The houses that would buy it, best bid first.</summary>
    public List<(House house, int bid)> Bidders(World w, Business b, string? except = null) =>
        w.Houses.Values.Where(h => h.Id != except).Select(h => (house: h, bid: Bid(w, h.Id, b))).Where(x => x.bid > 0).OrderByDescending(x => x.bid).ThenBy(x => x.house.Id, StringComparer.Ordinal).ToList();

    /// <summary>What its owner asks of a buyer, or why it will not sell.</summary>
    public (int price, string? refusal) Ask(World w, Business b, string buyerId)
    {
        var seller = w.House(b.OwnerId); var buyer = w.House(buyerId);
        if (seller == null || buyer == null) return (0, "no such venture");
        if (buyerId == b.OwnerId) return (0, "already yours");
        if (b.CompanyId.Length > 0) return (0, "held by a company: buy its shares on the Exchange");
        if (w.AtWar(buyerId, b.OwnerId)) return (0, $"{seller.Name} is at war with you");
        if (w.Businesses.Values.Any(o => o.OwnerId == buyerId && o.Kind == b.Kind && o.SystemId == b.SystemId)) return (0, "you already have one here");
        int regard = Regard(w, seller, buyer);
        if (regard <= -30) return (0, $"{seller.Name} will not sell to you at any price");
        int pct = Distressed(seller) ? DistressAskPct : AskPct;
        if (Holder(w, b) == seller.Id) pct += OwnWorldAsk;
        if (!w.HoldingsOf(seller.Id).Any(h => h.IsPlanet) && w.BusinessesOf(seller.Id).Count() == 1) pct += LastVentureAsk;   // it is all they have
        if (regard >= 30) pct -= 10;
        return (Math.Max(Salvage(b), Value(w, b) * pct / 100), null);
    }

    /// <summary>The holder of the world, if it is neither party and would match the price: it has first refusal on anything sold there.</summary>
    public House? FirstRefuser(World w, Business b, string? buyerId, int price)
    {
        var holderId = Holder(w, b); if (holderId == null || holderId == b.OwnerId || holderId == buyerId) return null;
        var holder = w.House(holderId); if (holder == null || holderId == w.Protagonist?.HouseId) return null;   // the player is asked, never assumed (an offer is posted instead)
        if (holder.Treasury.Credits < price + Reserve || w.AtWar(holderId, b.OwnerId)) return null;
        if (w.Businesses.Values.Any(o => o.OwnerId == holderId && o.Kind == b.Kind && o.SystemId == b.SystemId)) return null;
        return Value(w, b) * (BidPct + HolderPremium) / 100 >= price ? holder : null;
    }

    /// <summary>The venture changes hands for the price.</summary>
    public void Transfer(World w, Business b, House to, int price, string how)
    {
        var from = w.House(b.OwnerId)!;
        to.Treasury.Credits -= price; from.Treasury.Credits += price;
        b.OwnerId = to.Id; b.OfferedUntil = 0;
        foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
        w.WorldCounters[SoldCounter] = w.Counter(SoldCounter) + 1;
        if (to.Id == w.Protagonist?.HouseId) w.WorldFlags.Add(BoughtFlag);
        w.Log($"{to.Name} {how} the {b.Name} from {from.Name} for {price} credits.");
    }

    /// <summary>Sold to the open market: the caravaneers pay four-fifths of value and break it up.</summary>
    public void SellToMarket(World w, Business b)
    {
        var from = w.House(b.OwnerId)!; int price = MarketPrice(w, b);
        if (FirstRefuser(w, b, null, price) is { } holder) { Transfer(w, b, holder, price, "takes up its first refusal and buys"); return; }
        from.Treasury.Credits += price; w.Businesses.Remove(b.Id);
        foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
        w.WorldCounters[SoldCounter] = w.Counter(SoldCounter) + 1;
        w.Log($"{from.Name} sells the {b.Name} on the open market for {price} credits; the caravaneers break it up.");
    }

    /// <summary>Sold to a named house at its bid — unless the world's holder matches it first.</summary>
    public bool SellTo(World w, Business b, string buyerId)
    {
        var buyer = w.House(buyerId); int price = Bid(w, buyerId, b); if (buyer == null || price <= 0) return false;
        if (FirstRefuser(w, b, buyerId, price) is { } holder) { Transfer(w, b, holder, price, "takes up its first refusal and buys"); if (b.OwnerId != buyerId && w.House(buyerId)?.Id == w.Protagonist?.HouseId) w.WorldFlags.Add(RefusedFirstFlag); return true; }
        Transfer(w, b, buyer, price, "buys"); return true;
    }

    /// <summary>Bought at the owner's asking price — unless the world's holder matches it first, and then the buyer keeps their coin.</summary>
    public bool BuyAt(World w, Business b, string buyerId)
    {
        var buyer = w.House(buyerId); var (price, refusal) = Ask(w, b, buyerId); if (buyer == null || refusal != null || buyer.Treasury.Credits < price) return false;
        // a rival buying what works YOUR world: you are asked first, for a year, at the price agreed
        var mine = w.Protagonist?.HouseId;
        if (mine != null && buyerId != mine && b.OwnerId != mine && Holder(w, b) == mine && b.OfferedUntil == 0 && Ask(w, b, mine).refusal == null)
        {
            int n = w.Counter("offers_made") + 1; w.WorldCounters["offers_made"] = n;
            w.Offers.Add(new VentureOffer { Id = $"offer_{n}", BusinessId = b.Id, FromHouseId = b.OwnerId, ToHouseId = mine, Price = price, Expires = w.Turn + 1, Reason = $"{buyer.Name} has offered for it, and it works your world: you have first refusal at their price" });
            b.OfferedUntil = w.Turn + 1; w.WorldFlags.Add(OfferedFlag);
            w.Log($"{buyer.Name} offers {price} credits for the {b.Name}; it works a world of {w.House(mine)?.Name}, which has a year to match it.");
            return false;
        }
        if (FirstRefuser(w, b, buyerId, price) is { } holder) { Transfer(w, b, holder, price, "takes up its first refusal and buys"); if (buyerId == w.Protagonist?.HouseId) w.WorldFlags.Add(RefusedFirstFlag); return true; }
        Transfer(w, b, buyer, price, "buys"); return true;
    }

    /// <summary>A steward parts with a venture: the player is offered it first (a year to answer) when they hold the world it works or
    /// stand well with the seller and have the coin; else the best bidder takes it; else the open market.</summary>
    public void StewardSells(World w, Business b)
    {
        var seller = w.House(b.OwnerId); var mine = w.Protagonist != null ? w.House(w.Protagonist.HouseId) : null; if (seller == null) return;
        int fair = Math.Max(Salvage(b), Value(w, b) * OfferPct / 100);
        if (mine != null && mine.Id != seller.Id && b.OfferedUntil == 0 && !w.AtWar(mine.Id, seller.Id) && Ask(w, b, mine.Id).refusal == null
            && (Holder(w, b) == mine.Id || (Regard(w, seller, mine) >= 10 && mine.Treasury.Credits >= fair)))
        {
            int n = w.Counter("offers_made") + 1; w.WorldCounters["offers_made"] = n;
            w.Offers.Add(new VentureOffer { Id = $"offer_{n}", BusinessId = b.Id, FromHouseId = seller.Id, ToHouseId = mine.Id, Price = fair, Expires = w.Turn + 1,
                Reason = Holder(w, b) == mine.Id ? "it works your world: you have first refusal" : "they would rather it went to a friend" });
            b.OfferedUntil = w.Turn + 1; w.WorldFlags.Add(OfferedFlag);
            w.Log($"{seller.Name}, short of coin, offers {mine.Name} the {b.Name} for {fair} credits.");
            return;
        }
        var best = Bidders(w, b, except: mine?.Id).FirstOrDefault();
        if (best.house != null) Transfer(w, b, best.house, best.bid, "buys"); else SellToMarket(w, b);
    }

    /// <summary>What a venture would earn a different owner: the charter and the holder's war differ by who holds the paper.</summary>
    public int EarningsFor(World w, Business b, string ownerId) =>
        (int)Math.Round(Economy.WorthOf(w, _economy.VentureLine(w, new Business { Id = b.Id, OwnerId = ownerId, Kind = b.Kind, SystemId = b.SystemId, Level = b.Level })));

    /// <summary>The venture a rich house would most like to buy: one it can have within ten years' purchase and afford with a reserve of
    /// sixty, on its own world before anyone else's, a distressed seller's before a comfortable one's. Never the player's: theirs is not for the taking.</summary>
    public Business? Target(World w, string buyerId)
    {
        var buyer = w.House(buyerId); if (buyer == null) return null; var mine = w.Protagonist?.HouseId;
        return w.Businesses.Values.Where(b => b.OwnerId != buyerId && b.OwnerId != mine && b.CompanyId.Length == 0 && _ventures.Def(b.Kind) != null)
            .Select(b => (b, ask: Ask(w, b, buyerId), earn: EarningsFor(w, b, buyerId)))
            .Where(x => x.ask.refusal == null && x.earn > 0 && x.ask.price <= x.earn * 10 && x.ask.price + 60 <= buyer.Treasury.Credits)
            .OrderBy(x => Holder(w, x.b) == buyerId ? 0 : 1).ThenBy(x => (double)x.ask.price / x.earn).ThenBy(x => x.b.Id, StringComparer.Ordinal).Select(x => x.b).FirstOrDefault();
    }

    /// <summary>Offers lapse after their year; an offer whose venture is gone or sold goes with it.</summary>
    public void Tick(World w)
    {
        w.WorldFlags.Remove(OfferedFlag); w.WorldFlags.Remove(BoughtFlag); w.WorldFlags.Remove(RefusedFirstFlag);
        foreach (var o in w.Offers.ToList())
            if (o.Expires < w.Turn || w.Business(o.BusinessId) is not { } b || b.OwnerId != o.FromHouseId) w.Offers.Remove(o);
        if (w.Offers.Any(o => o.ToHouseId == w.Protagonist?.HouseId)) w.WorldFlags.Add(OfferedFlag);
    }

    /// <summary>What founding a venture here would earn a year at this year's prices, and how many years until it has paid for itself.</summary>
    public (int perYear, double paybackYears) Prospectus(World w, string houseId, BusinessDef def, string systemId)
    {
        var ghost = new Business { Id = "prospectus", OwnerId = houseId, Kind = def.Id, SystemId = systemId, Level = 1 };
        int perYear = (int)Math.Round(Economy.WorthOf(w, _economy.VentureLine(w, ghost))); double cost = Economy.WorthOf(w, def.Cost);
        return (perYear, perYear > 0 ? cost / perYear : double.PositiveInfinity);
    }
}
