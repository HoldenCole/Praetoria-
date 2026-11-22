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
    public int MarketPrice(World w, Business b) => Math.Max(Salvage(b), Trading(w, b) * MarketPct / 100);
    /// <summary>Years before a buyer pays for what a venture earns and not for what it cost.</summary>
    public const int Seasoning = 5;
    /// <summary>What a buyer reckons a venture at (docs/PLAYTHROUGH.md 4): its value, but a venture founded lately has no record, and
    /// is reckoned at what it cost, rising to its value over its first <see cref="Seasoning"/> years. Without this a venture sold
    /// the year it was finished for many times its price, and could be founded again at once.</summary>
    public int Trading(World w, Business b)
    {
        int value = Value(w, b); if (b.Founded <= 0) return value;
        int held = w.Turn - b.Founded, book = _ventures.Def(b.Kind)?.Value(b.Level) ?? value;
        return held >= Seasoning || value <= book ? value : book + (value - book) * Math.Max(0, held) / Seasoning;
    }

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
        if (buyer == null || seller == null || buyerId == b.OwnerId || World.Outsider(buyerId) || b.CompanyId.Length > 0) return 0;
        if (w.AtWar(buyerId, b.OwnerId)) return 0;
        if (w.Businesses.Values.Any(o => o.OwnerId == buyerId && o.Kind == b.Kind && o.SystemId == b.SystemId)) return 0;   // one of a kind a system
        bool holder = Holder(w, b) == buyerId;
        if (Earnings(w, b) <= 0 && !holder) return 0;
        int pct = BidPct + (holder ? HolderPremium : 0) + (FactionSystem.Of(w, buyerId)?.PartyId == "factors" ? 5 : 0);
        int regard = Regard(w, buyer, seller); pct += regard >= 20 ? 5 : regard <= -20 ? -10 : 0;
        if (BadName(w, seller.Id)) pct -= BadNameBidPct;   // a house known for bad business is known to have to sell
        if (Wealth.TierIndex(Wealth.TierFor(w, buyerId)) >= 3) pct += 5;
        int bid = Trading(w, b) * pct / 100;
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
        int asked = Math.Max(Salvage(b), Trading(w, b) * pct / 100);
        // an offer made this year and answered: the answer is their price for the rest of the year
        if (w.Counter(CounterUntilKey(b.Id, buyerId)) > w.Turn && w.Counter(CounterKey(b.Id, buyerId)) is > 0 and var last) asked = Math.Min(asked, last);
        return (asked, null);
    }

    /* ---- an offer below the asking (docs/PLAYTHROUGH.md, Medium, Money: "make an offer on any venture of another house") ----
       The asking price is where an owner begins. They would take less: how much less follows what their head thinks of the
       buyer and whether they are short of coin, and a little the mood of the year, which the buyer cannot see. One offer a
       year on each venture. An offer they like is taken at the price offered. One they do not is answered with a price
       between the offer and the asking, never under the least they would take, and that is their last word for the year.
       An offer under half the asking is an insult: it is not answered, and their head thinks the worse of the buyer. */
    public const int ComesDownPct = 85, FriendDownPct = 80, IllDownPct = 95, DistressDownPct = 75, MoodSwing = 4, InsultPct = 50, InsultRegard = 3, DownFriend = 30, DownIll = -10;
    public const string OffersTakenCounter = "venture_offers_taken", OffersCounteredCounter = "venture_offers_countered";
    public static string CounterKey(string businessId, string buyerId) => $"venture_counter:{businessId}:{buyerId}";
    public static string CounterUntilKey(string businessId, string buyerId) => $"venture_counter_until:{businessId}:{buyerId}";
    /// <summary>The price they answered an offer with this year, or 0 when none has been made.</summary>
    public int LastWord(World w, Business b, string buyerId) => w.Counter(CounterUntilKey(b.Id, buyerId)) > w.Turn ? w.Counter(CounterKey(b.Id, buyerId)) : 0;
    /// <summary>The least the owner would take from this buyer this year. Not shown to the buyer.</summary>
    public int Least(World w, Business b, string buyerId)
    {
        var seller = w.House(b.OwnerId); var buyer = w.House(buyerId); var (ask, refusal) = Ask(w, b, buyerId);
        if (seller == null || buyer == null || refusal != null) return 0;
        int regard = Regard(w, seller, buyer);
        int pct = Distressed(seller) ? DistressDownPct : regard >= DownFriend ? FriendDownPct : regard <= DownIll ? IllDownPct : ComesDownPct;
        pct += (int)(Hash($"{b.Id}:{buyerId}:{w.Turn}:mood") % (2 * MoodSwing + 1)) - MoodSwing;
        return Math.Min(ask, Math.Max(MarketPrice(w, b), ask * pct / 100));   // never less than the open market would give them
    }
    /// <summary>The offer below which the owner takes it as an insult.</summary>
    public int Insult(World w, Business b, string buyerId) => Ask(w, b, buyerId).price * InsultPct / 100;
    public string? OfferRefusal(World w, Business? b, string buyerId, int price)
    {
        var buyer = w.House(buyerId); if (b == null || buyer == null) return "no such venture";
        var (ask, refusal) = Ask(w, b, buyerId); if (refusal != null) return refusal;
        if (LastWord(w, b, buyerId) is > 0 and var last) return $"they have given their last word this year: {Money.Credits(last)}";
        if (price < 1) return "offer something";
        if (price >= ask) return $"they ask {Money.Show(ask)}: that is the price, not an offer";
        return buyer.Treasury.Credits >= price ? null : $"needs {Money.Credits(price)}";
    }
    /// <summary>Make the offer. Returns what came of it, as it is told to the house that made it.</summary>
    public string Offer(World w, Business b, string buyerId, int price)
    {
        var seller = w.House(b.OwnerId)!; var buyer = w.House(buyerId)!; var (ask, _) = Ask(w, b, buyerId); int least = Least(w, b, buyerId);
        var sellerHead = HeadOfHouse(w, seller.Id); var buyerHead = HeadOfHouse(w, buyerId); string name = b.Name.StartsWith("The ", StringComparison.Ordinal) ? b.Name[4..] : b.Name;
        void LastWordIs(int n) { w.WorldCounters[CounterKey(b.Id, buyerId)] = n; w.WorldCounters[CounterUntilKey(b.Id, buyerId)] = w.Turn + 1; }
        if (price < Insult(w, b, buyerId))
        {
            LastWordIs(ask);
            if (sellerHead != null && buyerHead != null && sellerHead.Id != buyerHead.Id) { var r = w.Relationship(sellerHead.Id, buyerHead.Id); r.Disposition = Math.Clamp(r.Disposition - InsultRegard, -100, 100); }
            w.Log($"{buyer.Name} offers {Money.Credits(price)} for the {name}, which {seller.Name} asks {Money.Show(ask)} for. {seller.Name} does not answer.");
            return $"{seller.Name} takes {Money.Show(price)} for the {name} as an insult and does not answer: {sellerHead?.Name ?? "its head"} thinks {InsultRegard} the worse of you. It is {Money.Show(ask)} or nothing this year.";
        }
        if (price >= least)
        {
            LastWordIs(price); Bump(w, OffersTakenCounter);
            bool bought = BuyAt(w, b, buyerId);
            if (bought && b.OwnerId == buyerId) return $"{seller.Name} takes {Money.Credits(price)} for the {name}, {Money.Show(ask - price)} under what it asked. It is yours.";
            if (bought) return $"{seller.Name} would have taken {Money.Show(price)} for the {name}, but {w.House(b.OwnerId)?.Name} holds the world it works, matched the price and took it first. You keep your coin.";
            return $"{seller.Name} would take {Money.Show(price)} for the {name}, but the sale did not go through.";
        }
        int counter = Math.Min(ask, Math.Max(least, (price + ask + 1) / 2)); LastWordIs(counter); Bump(w, OffersCounteredCounter);
        w.Log($"{buyer.Name} offers {Money.Credits(price)} for the {name}; {seller.Name} answers {Money.Show(counter)}.");
        return $"{seller.Name} will not take {Money.Show(price)} for the {name}. It answers {Money.Show(counter)}, {Money.Show(ask - counter)} under what it asked, and that is its last word this year.";
    }

    /// <summary>The holder of the world, if it is neither party and would match the price: it has first refusal on anything sold there.</summary>
    public House? FirstRefuser(World w, Business b, string? buyerId, int price)
    {
        var holderId = Holder(w, b); if (holderId == null || holderId == b.OwnerId || holderId == buyerId) return null;
        var holder = w.House(holderId); if (holder == null || holderId == w.Protagonist?.HouseId) return null;   // the player is asked, never assumed (an offer is posted instead)
        if (holder.Treasury.Credits < price + Reserve || w.AtWar(holderId, b.OwnerId)) return null;
        if (w.Businesses.Values.Any(o => o.OwnerId == holderId && o.Kind == b.Kind && o.SystemId == b.SystemId)) return null;
        return Trading(w, b) * (BidPct + HolderPremium) / 100 >= price ? holder : null;
    }

    /// <summary>The venture changes hands for the price.</summary>
    public void Transfer(World w, Business b, House to, int price, string how)
    {
        var from = w.House(b.OwnerId)!;
        to.Treasury.Credits -= price; from.Treasury.Credits += price; Proceeds(w, b, from, price);
        b.OwnerId = to.Id; b.OfferedUntil = 0; b.HeldById = "";
        foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
        w.WorldCounters[SoldCounter] = w.Counter(SoldCounter) + 1;
        if (to.Id == w.Protagonist?.HouseId) w.WorldFlags.Add(BoughtFlag);
        w.Log($"{to.Name} {how} the {b.Name} from {from.Name} for {Money.Credits(price)}.");
    }

    /// <summary>Sold to the open market: the caravaneers pay four-fifths of value and break it up.</summary>
    public void SellToMarket(World w, Business b)
    {
        var from = w.House(b.OwnerId)!; int price = MarketPrice(w, b);
        if (FirstRefuser(w, b, null, price) is { } holder) { Transfer(w, b, holder, price, "takes up its first refusal and buys"); return; }
        from.Treasury.Credits += price; Proceeds(w, b, from, price); w.Businesses.Remove(b.Id);
        foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
        w.WorldCounters[SoldCounter] = w.Counter(SoldCounter) + 1;
        w.Log($"{from.Name} sells the {b.Name} on the open market for {Money.Credits(price)}; the caravaneers break it up.");
    }

    /// <summary>Sold to a named house at its bid — unless the world's holder matches it first.</summary>
    public bool SellTo(World w, Business b, string buyerId)
    {
        var buyer = w.House(buyerId); int price = Bid(w, buyerId, b); if (buyer == null || price <= 0) return false;
        if (FirstRefuser(w, b, buyerId, price) is { } holder) { Transfer(w, b, holder, price, "takes up its first refusal and buys"); if (b.OwnerId != buyerId && w.House(buyerId)?.Id == w.Protagonist?.HouseId) w.WorldFlags.Add(RefusedFirstFlag); return true; }
        var seller = HeadOfHouse(w, b.OwnerId); var buyerHead = HeadOfHouse(w, buyerId);
        Transfer(w, b, buyer, price, "buys");
        /* a house that is sold what it bid for remembers who sold it: the venture goes on whole in its hands, where the open market would have broken it up */
        if (seller != null && buyerHead != null && seller.Id != buyerHead.Id) { var r = w.Relationship(buyerHead.Id, seller.Id); r.Disposition = Math.Clamp(r.Disposition + PrivateSaleRegard, -100, 100); }
        return true;
    }
    /// <summary>What the head of a house that is sold a venture at its bid thinks the better of the seller for it.</summary>
    public const int PrivateSaleRegard = 4;

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
            w.Log($"{buyer.Name} offers {Money.Credits(price)} for the {b.Name}; it works a world of {w.House(mine)?.Name}, which has a year to match it.");
            return false;
        }
        if (FirstRefuser(w, b, buyerId, price) is { } holder) { Transfer(w, b, holder, price, "takes up its first refusal and buys"); if (buyerId == w.Protagonist?.HouseId) w.WorldFlags.Add(RefusedFirstFlag); return true; }
        Transfer(w, b, buyer, price, "buys"); return true;
    }

    /// <summary>A steward parts with a venture: the player is offered it first (a year to answer) when they hold the world it works or
    /// stand well with the seller and have the coin; else the best bidder takes it; else the open market.</summary>
    public void StewardSells(World w, Business b)
    {
        var seller = w.House(b.OwnerId); var mine = w.Protagonist != null ? w.House(w.Protagonist.HouseId) : null; if (seller == null || ConsentRefusal(w, b) != null) return;
        int fair = Math.Max(Salvage(b), Trading(w, b) * OfferPct / 100);
        if (mine != null && mine.Id != seller.Id && b.OfferedUntil == 0 && !w.AtWar(mine.Id, seller.Id) && Ask(w, b, mine.Id).refusal == null
            && (Holder(w, b) == mine.Id || (Regard(w, seller, mine) >= 10 && mine.Treasury.Credits >= fair)))
        {
            int n = w.Counter("offers_made") + 1; w.WorldCounters["offers_made"] = n;
            w.Offers.Add(new VentureOffer { Id = $"offer_{n}", BusinessId = b.Id, FromHouseId = seller.Id, ToHouseId = mine.Id, Price = fair, Expires = w.Turn + 1,
                Reason = Holder(w, b) == mine.Id ? "it works your world: you have first refusal" : "they would rather it went to a friend" });
            b.OfferedUntil = w.Turn + 1; w.WorldFlags.Add(OfferedFlag);
            w.Log($"{seller.Name}, short of coin, offers {mine.Name} the {b.Name} for {Money.Credits(fair)}.");
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

    // ---------------- a venture of one's own (docs/WEALTH.md, W10) ----------------

    public const int SettleRegard = 12, ConsentAt = 20, SoldOverRegard = 10, SoldFailingRegard = 4, FounderCutPct = 20;
    public const string SettledCounter = "ventures_settled", HeldIncomeCounter = "held_venture_income", FounderCutCounter = "founder_cuts", OwnMoneyPrefix = "own_money:";
    private static Character? HeadOfHouse(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;
    /// <summary>Whose own it is, if it is someone's: a living member of the house that owns it.</summary>
    public static Character? HolderOf(World w, Business b) =>
        b.HeldById.Length > 0 && b.CompanyId.Length == 0 && w.Char(b.HeldById) is { Alive: true } c && c.HouseId == b.OwnerId ? c : null;

    public string? SettleRefusal(World w, Character? head, Business? b, Character? on)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || b == null || b.OwnerId != house.Id) return "not a venture of yours";
        if (Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of the house can settle it";
        if (b.CompanyId.Length > 0) return "it is a company's";
        if (on?.Alive != true || on.HouseId != house.Id || on.Age < DynastySystem.AgeOfMajority) return "not a grown member of your house";
        var holder = HolderOf(w, b); if (holder?.Id == on.Id) return "it is theirs already";
        return holder != null && holder.Id != head.Id ? $"it is {holder.Name}'s own: it is not yours to give again" : null;
    }
    /// <summary>The head settles a venture on one of the house: it is that person's own from now on.</summary>
    public void Settle(World w, Character head, Business b, Character on)
    {
        b.HeldById = on.Id; Bump(w, SettledCounter); if (w.House(head.HouseId) is { } hs && hs.PledgedId == b.Id) hs.PledgedId = "";   // what is someone's own is no longer the house's to pledge
        if (on.Id != head.Id) { var r = w.Relationship(on.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition + SettleRegard, -100, 100); LifeSystem.Note(w, on, "venture", $"Was given the {b.Name} as their own."); }
        w.Log(on.Id == head.Id ? $"{head.Name} takes the {b.Name} as their own, apart from the house." : $"{head.Name} settles the {b.Name} on {on.Name}. It is theirs: what it earns, and the say in selling it.");
    }
    /// <summary>A venture that is someone's own is not sold, listed or given up over their head: they must think well enough of
    /// the head to agree. One the head holds themselves is theirs to sell.</summary>
    public static string? ConsentRefusal(World w, Business? b)
    {
        if (b == null || HolderOf(w, b) is not { } holder) return null; var head = HeadOfHouse(w, b.OwnerId); if (head == null || head.Id == holder.Id) return null;
        return (w.GetRelationship(holder.Id, head.Id)?.Disposition ?? 0) >= ConsentAt ? null : $"it is {holder.Name}'s own, and they will not agree to it";
    }
    /// <summary>The price of a venture that was someone's own is theirs. One that was the house's but was founded by one of its
    /// people pays the founder a part, and they mind its going (less, if it was failing).</summary>
    private void Proceeds(World w, Business b, House from, int price)
    {
        if (HolderOf(w, b) is { } holder) { from.Treasury.Credits -= price; holder.Purse += price; return; }
        if (from.Id != w.Protagonist?.HouseId || b.FounderId.Length == 0 || w.Char(b.FounderId) is not { Alive: true } founder || founder.HouseId != from.Id) return;
        var head = Economy.HeadOf(w, from); if (head == null || head.Id == founder.Id) return;
        int cut = price * FounderCutPct / 100; from.Treasury.Credits -= cut; founder.Purse += cut; Bump(w, FounderCutCounter, cut);
        var r = w.Relationship(founder.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition - (b.LosingYears > 0 ? SoldFailingRegard : SoldOverRegard), -100, 100);
        w.Log($"{founder.Name} founded the {b.Name}; {Money.Credits(cut)} of its price are theirs, and they are {(b.LosingYears > 0 ? "sorry" : "not glad")} to see it go.");
    }
    /// <summary>The year's earnings of every venture that is someone's own are paid over to them, as far as the treasury goes.
    /// One whose holder is dead or has left the house is the house's again unless a will has said otherwise.</summary>
    public void PayHolders(World w)
    {
        foreach (var b in w.Businesses.Values.Where(b => b.HeldById.Length > 0).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            var holder = HolderOf(w, b); var owner = w.House(b.OwnerId); if (holder == null || owner == null) { b.HeldById = ""; continue; }
            int pay = Math.Min(Math.Max(0, Earnings(w, b)), Math.Max(0, owner.Treasury.Credits)); if (pay <= 0) continue;
            owner.Treasury.Credits -= pay; holder.Purse += pay; Bump(w, HeldIncomeCounter, pay);
        }
    }
    /// <summary>What someone left when they died: their own ventures go to whoever is named, within the house, or back to it.</summary>
    public static void Leave(World w, Character dead, Character? to)
    {
        foreach (var b in w.Businesses.Values.Where(b => b.HeldById == dead.Id).OrderBy(b => b.Id, StringComparer.Ordinal))
            b.HeldById = to is { Alive: true } && to.HouseId == b.OwnerId ? to.Id : "";
    }

    // ---------------- claims at the peace, a company that sells, creditors who foreclose (docs/ECONOMY_MARKET.md, section 14) ----------------

    public const int SackClaimALevel = 10, ForecloseAt = -20, OverdrawnYears = 2;
    public static string OverdrawnKey(string houseId) => "overdrawn_years:" + houseId;
    public const string ClaimPrefix = "sack_claim:", ClaimsPaidCounter = "sack_claims_paid", ClaimsForfeitCounter = "sack_claims_forfeited", CompanySoldCounter = "company_ventures_sold", ForeclosedCounter = "ventures_foreclosed", ForeclosedFlag = "venture_foreclosed";
    public const string OverdrawnYearsCounter = "overdrawn_house_years", DeepOverdrawnYearsCounter = "deep_overdrawn_house_years", OverdrawnWithVentureCounter = "overdrawn_with_venture_years";
    /// <summary>What <paramref name="ownerId"/> has written down against <paramref name="takerId"/> for ventures sacked in their war.</summary>
    public static string ClaimKey(string ownerId, string takerId) => $"{ClaimPrefix}{ownerId}>{takerId}";

    /// <summary>The peace is made: each side's bill for what was sacked is paid by the other, as far as its treasury goes. A side
    /// beaten badly enough to cede a world is in no position to present a bill, and its claim is struck out.</summary>
    public static void SettleClaims(World w, War war, int beatenBy)
    {
        foreach (var (x, y) in new[] { (war.HouseA, war.HouseB), (war.HouseB, war.HouseA) })
        {
            string key = ClaimKey(x, y); int claim = w.Counter(key); if (claim <= 0) continue; w.WorldCounters.Remove(key);
            var claimant = w.House(x); var payer = w.House(y); if (claimant == null || payer == null) continue;
            bool ours = x == w.Protagonist?.HouseId || y == w.Protagonist?.HouseId;
            if (war.Lead(x) <= -beatenBy) { Bump(w, ClaimsForfeitCounter, claim); if (ours) w.Log($"{claimant.Name}'s bill of {Money.Credits(claim)} for what was sacked is struck out of the peace: the beaten do not present bills."); continue; }
            int pay = Math.Min(claim, Math.Max(0, payer.Treasury.Credits)); payer.Treasury.Credits -= pay; claimant.Treasury.Credits += pay; Bump(w, ClaimsPaidCounter, pay);
            if (ours) w.Log($"By the peace, {payer.Name} pays {claimant.Name} {Money.Credits(pay)} for the ventures sacked in the war{(pay < claim ? $", of {Money.Show(claim)} claimed" : "")}.");
        }
    }
    public const int SiegeClaimATurn = 3; public const string SiegeClaimsCounter = "siege_claims";
    /// <summary>A blockade that is lifted without the world falling has cost its holder something, and that is written down
    /// against whoever laid it, with what was sacked: paid at the peace by the same rule.</summary>
    public static void SiegeLifted(World w, Siege siege)
    {
        var world = w.Holding(siege.HoldingId); if (world == null || siege.Turns <= 0 || world.OwnerId == siege.BesiegerId || !w.AtWar(world.OwnerId, siege.BesiegerId)) return;
        int bill = SiegeClaimATurn * siege.Turns; Bump(w, ClaimKey(world.OwnerId, siege.BesiegerId), bill); Bump(w, SiegeClaimsCounter, bill);
    }

    /// <summary>A bill with no war behind it is not presented: the war ended some other way.</summary>
    private static void LapseClaims(World w)
    {
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith(ClaimPrefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToList())
        { var parts = key[ClaimPrefix.Length..].Split('>'); if (parts.Length != 2 || !w.AtWar(parts[0], parts[1])) w.WorldCounters.Remove(key); }
    }

    /// <summary>A company that cannot carry a failing venture sells it rather than close it, if the house that directs it minds
    /// its books: to the holder of the world it works, at the market's price, or to the market.</summary>
    private bool CompanySells(World w, Company co, Business b, string? mine)
    {
        var board = w.House(co.ControllerId); var chair = board != null ? Economy.HeadOf(w, board) : null;
        bool minds = (chair?.Skill("economics") ?? 0) >= 2 || board != null && FactionSystem.Of(w, board.Id)?.PartyId == "factors" || Hash($"{b.Id}:{w.Turn}:board") % 3 == 0;
        if (!minds) return false;
        int price = MarketPrice(w, b); var holder = Holder(w, b) is { } hid ? w.House(hid) : null;
        bool takes = holder != null && holder.Id != mine && holder.Id != co.ControllerId && holder.Treasury.Credits >= price + Reserve && !w.AtWar(holder.Id, co.ControllerId)
            && !w.Businesses.Values.Any(o => o.OwnerId == holder.Id && o.Kind == b.Kind && o.SystemId == b.SystemId && o.Id != b.Id);
        co.Cash += price; Bump(w, CompanySoldCounter);
        if (takes) { holder!.Treasury.Credits -= price; b.CompanyId = ""; b.OwnerId = holder.Id; b.LosingYears = 0; b.HeldById = ""; }
        else w.Businesses.Remove(b.Id);
        bool ours = mine != null && (co.HeldBy(mine) > 0 || co.ControllerId == mine || w.ProtagonistId.Length > 0 && co.HeldBy(Company.PersonKey(w.ProtagonistId)) > 0);
        if (ours) w.Log($"{co.Name} sells the {b.Name}{(takes ? $" to {holder!.Name}" : " on the open market")} for {Money.Credits(price)}: it had earned nothing for {FailsAfter} years.");
        return true;
    }

    /// <summary>A house that owes on a loan and has nothing, or whose treasury is <see cref="ForecloseAt"/> or worse, or which has
    /// been overdrawn <see cref="OverdrawnYears"/> years running, is foreclosed on: its lender takes the least of its ventures against the debt, or it is sold to pay what is owed. What is someone's own is not the house's to lose. The house has a name for bad business.</summary>
    public const int ThingAfterYears = 3;
    public const string ThingsForeclosedCounter = "things_foreclosed", WorldsForeclosedCounter = "worlds_foreclosed", PledgedCounter = "pledges_made";
    /// <summary>A thing handed down that the house's creditors can take: one of its people holds it, and it is not entailed.
    /// An entail is what keeps a thing from them.</summary>
    public static bool Takeable(World w, Heirloom t, House house) => !t.Entailed && w.Char(t.HolderId) is { Alive: true } c && c.HouseId == house.Id && Economy.HeadOf(w, house)?.Id == c.Id;

    public string? PledgeRefusal(World w, Character? head, string id)
    {
        var house = head != null ? w.House(head.HouseId) : null; if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of the house can pledge what is the house's";
        if (id.Length == 0) return house.PledgedId.Length == 0 ? "nothing is pledged" : null;
        if (house.PledgedId == id) return "it is pledged already";
        if (w.Business(id) is { } b) return b.OwnerId != house.Id ? "not a venture of yours" : b.CompanyId.Length > 0 ? "it is a company's" : HolderOf(w, b) != null ? "it is someone's own, and not the house's to pledge" : null;
        if (w.Heirlooms.FirstOrDefault(t => t.Id == id) is { } thing) return Takeable(w, thing, house) ? null : thing.Entailed ? "it is entailed: no creditor can take it" : "it is not yours to pledge";
        return "no such thing";
    }
    public void Pledge(World w, Character head, string id) { var house = w.House(head.HouseId)!; house.PledgedId = id; if (id.Length > 0) Bump(w, PledgedCounter); w.Log(id.Length == 0 ? $"{head.Name} withdraws the pledge: the creditors may take what they like." : $"{head.Name} names what the house's creditors are to take first, if it comes to that."); }

    /// <summary>With no venture left to take, the creditors take a thing handed down: the one pledged, else the least of what the
    /// head holds unentailed. Only after <see cref="ThingAfterYears"/> years overdrawn, a loan unpaid, or a treasury far under.</summary>
    private bool ForecloseThing(World w, House house, string? mine)
    {
        bool pressed = house.Debt > 0 && house.Treasury.Credits < 0 || house.Treasury.Credits <= ForecloseAt || w.Counter(OverdrawnKey(house.Id)) >= ThingAfterYears; if (!pressed) return true;   // not yet: nothing more is taken this year
        var thing = w.Heirlooms.Where(t => Takeable(w, t, house)).OrderBy(t => t.Id == house.PledgedId ? 0 : 1).ThenBy(t => t.Value).ThenBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();
        if (thing == null) return false;
        int price = thing.Value, owed = Math.Min(house.Debt, price), overdrawn = Math.Min(price - owed, Math.Max(0, -house.Treasury.Credits)); var lender = w.House(house.LenderId); var keeper = lender != null && lender.Id != house.Id ? Economy.HeadOf(w, lender) : null;
        bool takes = keeper is { Alive: true } && lender!.Treasury.Credits >= price - owed;
        house.Debt -= owed; if (house.Debt == 0) house.LenderId = ""; house.Treasury.Credits += price - owed; Bump(w, CreditorsCounter, owed + overdrawn); Bump(w, ThingsForeclosedCounter);
        w.WorldCounters[BadNameKey(house.Id)] = w.Turn + BadNameYears; if (house.PledgedId == thing.Id) house.PledgedId = "";
        if (w.Char(thing.HolderId)?.Will is { } will) will.Items.Remove(thing.Id);
        if (takes) { lender!.Treasury.Credits -= price - owed; thing.HolderId = keeper!.Id; thing.OfHouseId = lender.Id; thing.FromHouseId = ""; } else w.Heirlooms.Remove(thing);
        if (house.Id == mine) { w.WorldFlags.Add(ForeclosedFlag); w.Log($"The house owes money and has no venture left to give. {(takes ? lender!.Name + " takes" : "Its creditors sell")} {thing.Name}: {Money.Credits(owed + overdrawn)} of what is owed are paid."); }
        else if (takes && lender!.Id == mine) w.Log($"{house.Name} cannot pay what it owes you, and has no venture left. {thing.Name} is yours.");
        return true;
    }

    /// <summary>Last of all a world: only against a loan from a named house, never a house's last world or the capital. The whole
    /// loan is written off for it.</summary>
    private bool ForecloseWorld(World w, House house, string? mine)
    {
        var lender = w.House(house.LenderId); if (house.Debt <= 0 || house.Treasury.Credits >= 0 || lender == null || lender.Id == house.Id || w.AtWar(lender.Id, house.Id)) return false;
        var worlds = w.HoldingsOf(house.Id).Where(h => h.IsPlanet).ToList(); if (worlds.Count < 2) return false;
        var world = worlds.Where(h => !(w.Galaxy is { Capital.Length: > 0 } g && g.Capital == h.SystemId)).OrderBy(h => h.Population).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault(); if (world == null) return false;
        Bump(w, CreditorsCounter, house.Debt); Bump(w, WorldsForeclosedCounter); house.Debt = 0; house.LenderId = ""; w.WorldCounters[BadNameKey(house.Id)] = w.Turn + BadNameYears;
        world.FormerOwnerId = house.Id; world.OwnerId = lender.Id; world.Unrest = Math.Min(100, world.Unrest + 10); if (Economy.HeadOf(w, lender) is { } gov) world.GovernorId = gov.Id;
        if (house.Id == mine) { w.WorldFlags.Add(ForeclosedFlag); w.Log($"The house owes {lender.Name} money and has nothing else to give. {world.Name} passes to {lender.Name}, and the loan is written off."); }
        else if (lender.Id == mine) w.Log($"{house.Name} cannot pay what it owes you and has nothing else to give. {world.Name} is yours, and the loan is written off.");
        else w.Log($"{house.Name} cannot pay {lender.Name}, and {world.Name} passes to its creditor.");
        return true;
    }

    private void Foreclose(World w, string? mine)
    {
        foreach (var h in w.Houses.Values.Where(h => h.Treasury.Credits >= 0)) w.WorldCounters.Remove(OverdrawnKey(h.Id));
        foreach (var h in w.Houses.Values.Where(h => h.Treasury.Credits < 0 && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            Bump(w, OverdrawnKey(h.Id)); Bump(w, OverdrawnYearsCounter);
            if (h.Id == mine && w.Counter(OverdrawnKey(h.Id)) == OverdrawnYears - 1 && w.BusinessesOf(h.Id).Any(x => x.CompanyId.Length == 0 && HolderOf(w, x) == null)) w.Log("The house is overdrawn. If it still is next year, those it owes will take one of its ventures."); if (h.Treasury.Credits <= ForecloseAt) Bump(w, DeepOverdrawnYearsCounter); if (w.BusinessesOf(h.Id).Any(x => x.CompanyId.Length == 0)) Bump(w, OverdrawnWithVentureCounter);
        }
        foreach (var house in w.Houses.Values.Where(h => (h.Debt > 0 && h.Treasury.Credits < 0 || h.Treasury.Credits <= ForecloseAt || w.Counter(OverdrawnKey(h.Id)) >= OverdrawnYears) && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            bool Free(Business x) => x.CompanyId.Length == 0 && HolderOf(w, x) == null && _ventures.Def(x.Kind) != null;
            // what the head has pledged goes first; otherwise the least of its ventures; with none, a thing handed down; last, a world
            var b = w.Business(house.PledgedId) is { } named && named.OwnerId == house.Id && Free(named) ? named
                : w.Heirlooms.Any(t => t.Id == house.PledgedId && Takeable(w, t, house)) ? null
                : w.BusinessesOf(house.Id).Where(Free).OrderBy(x => MarketPrice(w, x)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (b == null) { if (!ForecloseThing(w, house, mine)) ForecloseWorld(w, house, mine); continue; }
            if (house.PledgedId == b.Id) house.PledgedId = "";
            int price = MarketPrice(w, b), owed = Math.Min(house.Debt, price); var lender = w.House(house.LenderId);
            bool takes = lender != null && lender.Id != house.Id && !w.AtWar(lender.Id, house.Id) && lender.Treasury.Credits >= price - owed
                && !w.Businesses.Values.Any(o => o.OwnerId == lender.Id && o.Kind == b.Kind && o.SystemId == b.SystemId);
            int overdrawn = Math.Min(price - owed, Math.Max(0, -house.Treasury.Credits));   // what is not owed on the loan goes to whoever else the house owes, and then to the house
            house.Debt -= owed; if (house.Debt == 0) house.LenderId = ""; house.Treasury.Credits += price - owed; Bump(w, CreditorsCounter, owed + overdrawn); Bump(w, ForeclosedCounter);
            w.WorldCounters[BadNameKey(house.Id)] = w.Turn + BadNameYears;
            foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
            if (takes) { lender!.Treasury.Credits -= price - owed; b.OwnerId = lender.Id; b.OfferedUntil = 0; b.LosingYears = 0; if (lender.Id == mine) w.WorldFlags.Add(BoughtFlag); }
            else w.Businesses.Remove(b.Id);
            if (house.Id == mine) { w.WorldFlags.Add(ForeclosedFlag); w.Log($"The house owes money and has none. {(takes ? lender!.Name + " takes" : "Its creditors sell")} the {b.Name} against what is owed: {Money.Credits(owed + overdrawn)} of it are paid{(price > owed + overdrawn ? $", and {Money.Show(price - owed - overdrawn)} come back" : "")}."); }
            else if (takes && lender!.Id == mine) w.Log($"{house.Name} cannot pay what it owes you. The {b.Name} is yours, against {Money.Credits(owed)} of the debt.");
        }
    }

    // ---------------- a venture can fail (docs/ECONOMY_MARKET.md, "A venture can fail") ----------------

    public const int FailsAfter = 3, WoundUpPct = 50, PropFloor = 4;
    public const string FailingFlag = "venture_failing", FailedFlag = "venture_failed", FounderFailedChar = "founded_what_failed";
    public const int BadNameYears = 5, BadNameBidPct = 10, FailMerchants = 3, FailCommons = 1, CarryReserve = 60, SackPlunder = 4, CompanyCarryReserve = 20, CompanyCloseFlowPct = 5;
    public const string SackedFlag = "venture_sacked";
    public const string CarriedByOthersCounter = "ventures_carried_by_others", SoldFailingCounter = "ventures_sold_failing", CompanyCarriedCounter = "company_ventures_carried", CompanyClosedCounter = "company_ventures_closed", SackedCounter = "ventures_sacked", CreditorsCounter = "venture_creditors_paid";
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h / 7; }
    private static void Bump(World w, string key, int by = 1) => w.WorldCounters[key] = w.Counter(key) + by;
    /// <summary>The year until which a house has a name for bad business: a venture of its own failed on it.</summary>
    public static string BadNameKey(string houseId) => "bad_business:" + houseId;
    public static bool BadName(World w, string houseId) => w.Counter(BadNameKey(houseId)) > w.Turn;
    /// <summary>The year in which a venture this person founded failed, kept so that they can be given a second start.</summary>
    public static string FailedFounderKey(string characterId) => "failed_founder:" + characterId;
    public const string FailedCounter = "ventures_failed", LossYearsCounter = "venture_loss_years", VentureYearsCounter = "venture_years", ProppedCounter = "ventures_propped";
    /// <summary>What is left when a failed venture is wound up: half its salvage. The rest goes to those it owed.</summary>
    public int WoundUp(Business b) => Salvage(b) * WoundUpPct / 100;
    /// <summary>What it costs to carry a losing venture: its losses made good twice over, and something for each level of it.</summary>
    public int PropCost(World w, Business b) => Math.Max(PropFloor, 2 * b.Level + 2 * Math.Max(0, -Earnings(w, b)));
    public string? PropRefusal(World w, House? house, Business? b)
    {
        if (house == null || b == null || b.OwnerId != house.Id) return "not a venture of yours";
        if (b.CompanyId.Length > 0) return "it is a company's, and the company carries it";
        if (b.LosingYears == 0) return "it is paying its way";
        return house.Treasury.Credits < PropCost(w, b) ? $"needs {Money.Credits(PropCost(w, b))}" : null;
    }
    /// <summary>The house puts money in: the losing years are wiped out, and it has three more to come right.</summary>
    public void Prop(World w, House house, Business b)
    {
        int cost = PropCost(w, b); house.Treasury.Credits -= cost; b.LosingYears = 0; w.WorldCounters[ProppedCounter] = w.Counter(ProppedCounter) + 1;
        w.Log($"{house.Name} puts {Money.Credits(cost)} into the {b.Name} to keep it going.");
    }
    /// <summary>The year's reckoning of every venture a house owns outright: a year in which it earned nothing or lost money counts
    /// against it, and a year of profit wipes the count out. Three such years running and it is wound up: the house gets half its
    /// salvage, and whoever of the house founded it has failed at something of their own.</summary>
    private void Reckon(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var c in w.Characters.Values) c.Flags.Remove(FounderFailedChar);
        LapseClaims(w); Foreclose(w, mine);
        foreach (var b in w.Businesses.Values.Where(b => b.CompanyId.Length == 0 && w.House(b.OwnerId) != null && _ventures.Def(b.Kind) != null).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            if (w.Turn - b.Founded < 1) continue;   // its first year is not judged
            w.WorldCounters[VentureYearsCounter] = w.Counter(VentureYearsCounter) + 1;
            if (Earnings(w, b) > 0) { b.LosingYears = 0; continue; }
            b.LosingYears++; w.WorldCounters[LossYearsCounter] = w.Counter(LossYearsCounter) + 1;
            if (b.LosingYears < FailsAfter) { if (b.LosingYears == FailsAfter - 1) { if (b.OwnerId == mine) w.WorldFlags.Add(FailingFlag); else OthersAct(w, b, mine); } continue; }
            Fail(w, b, mine);
        }
        // a company's venture is judged as a house's is; the company carries it out of its own cash once, or closes it
        foreach (var b in w.Businesses.Values.Where(b => b.CompanyId.Length > 0 && w.Company(b.CompanyId) != null && _ventures.Def(b.Kind) != null).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            if (w.Turn - b.Founded < 1) continue;
            if (Earnings(w, b) > 0) { b.LosingYears = 0; continue; }
            b.LosingYears++; if (b.LosingYears < FailsAfter) continue;
            var co = w.Company(b.CompanyId)!; int cost = PropCost(w, b);
            if (co.Cash >= cost + CompanyCarryReserve) { co.Cash -= cost; b.LosingYears = 0; Bump(w, CompanyCarriedCounter); continue; }
            if (CompanySells(w, co, b, mine)) continue;
            co.Cash += WoundUp(b); w.Businesses.Remove(b.Id); co.Flow -= co.Shares * CompanyCloseFlowPct / 100.0; Bump(w, CompanyClosedCounter);
            bool ours = mine != null && (co.HeldBy(mine) > 0 || co.ControllerId == mine || w.ProtagonistId.Length > 0 && co.HeldBy(Company.PersonKey(w.ProtagonistId)) > 0);
            if (ours) w.Log($"{co.Name} closes the {b.Name}: it had earned nothing for {FailsAfter} years. The shares will feel it.");
        }
    }

    /// <summary>Another house's venture is a year from being wound up. A head who minds the books does something about it:
    /// carries it if the house is rich enough, and otherwise sells it while it is still worth more than its salvage. The rest
    /// let it go. The player is never sold to unasked.</summary>
    private void OthersAct(World w, Business b, string? mine)
    {
        var owner = w.House(b.OwnerId); if (owner == null || World.Outsider(owner.Id)) return; var head = Economy.HeadOf(w, owner);
        bool minds = (head?.Skill("economics") ?? 0) >= 2 || FactionSystem.Of(w, owner.Id)?.PartyId == "factors" || Hash($"{b.Id}:{w.Turn}:minds") % 3 == 0;
        if (!minds) return;
        int cost = PropCost(w, b);
        if (owner.Treasury.Credits >= cost + CarryReserve) { owner.Treasury.Credits -= cost; b.LosingYears = 0; Bump(w, CarriedByOthersCounter); return; }
        Bump(w, SoldFailingCounter);
        var best = Bidders(w, b, mine).FirstOrDefault();
        if (best.house != null) { Transfer(w, b, best.house, best.bid, "buys, failing as it is,"); b.LosingYears = 0; } else SellToMarket(w, b);
    }

    /// <summary>A world is stormed: of the ventures working it that belong to houses at war with whoever took it, about half
    /// are sacked. Nothing comes back to the owner; the taker carries off a little. Called by the fleets when a siege ends.</summary>
    public static int Sack(World w, string systemId, string conquerorId)
    {
        if (systemId.Length == 0) return 0; var mine = w.Protagonist?.HouseId; int sacked = 0; var taker = w.House(conquerorId);
        foreach (var b in w.Businesses.Values.Where(b => b.SystemId == systemId && b.CompanyId.Length == 0 && b.OwnerId != conquerorId && w.AtWar(b.OwnerId, conquerorId)).OrderBy(b => b.Id, StringComparer.Ordinal).ToList())
        {
            if (Hash($"{b.Id}:{w.Turn}:sack") % 2 != 0) continue;
            var owner = w.House(b.OwnerId); w.Businesses.Remove(b.Id); foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
            if (taker != null) taker.Treasury.Credits += SackPlunder * Math.Max(1, b.Level);
            Bump(w, SackedCounter); sacked++;
            if (owner != null) Bump(w, ClaimKey(owner.Id, conquerorId), SackClaimALevel * Math.Max(1, b.Level));   // written down, to be laid before whoever makes the peace
            var founder = b.FounderId.Length > 0 ? w.Char(b.FounderId) : null; bool theirs = founder is { Alive: true } && owner != null && founder.HouseId == owner.Id;
            if (theirs) { AmbitionSystem.Setback(w, founder!, $"The {b.Name}, which they founded, was sacked when its world fell."); w.WorldCounters[FailedFounderKey(founder!.Id)] = w.Turn; }
            if (owner?.Id == mine) { w.WorldFlags.Add(SackedFlag); w.Log($"The {b.Name} is sacked as its world falls to {taker?.Name ?? "the enemy"}. Nothing of it comes back."); }
            else if (conquerorId == mine) w.Log($"Your people sack the {b.Name} of {owner?.Name}: {Money.Credits(SackPlunder * Math.Max(1, b.Level))} are carried off.");
        }
        return sacked;
    }
    private void Fail(World w, Business b, string? mine)
    {
        var owner = w.House(b.OwnerId)!; int left = WoundUp(b); w.Businesses.Remove(b.Id);
        // those the house owes are paid before the house is; the people it employed and the merchants it dealt with remember
        int owed = Math.Clamp(owner.Debt, 0, left); owner.Debt -= owed; if (owner.Debt == 0) owner.LenderId = ""; owner.Treasury.Credits += left - owed; if (owed > 0) Bump(w, CreditorsCounter, owed);
        PolicySystem.Sway(w, owner.Id, "merchants", -FailMerchants); PolicySystem.Sway(w, owner.Id, "commons", -FailCommons);
        w.WorldCounters[BadNameKey(owner.Id)] = w.Turn + BadNameYears; left -= owed;
        if (HolderOf(w, b) is { } its && left > 0) { owner.Treasury.Credits -= left; its.Purse += left; }   // what is left of a person's own venture is theirs
        foreach (var o in w.Offers.Where(o => o.BusinessId == b.Id).ToList()) w.Offers.Remove(o);
        w.WorldCounters[FailedCounter] = w.Counter(FailedCounter) + 1;
        var founder = b.FounderId.Length > 0 ? w.Char(b.FounderId) : null;
        bool theirs = founder is { Alive: true } && founder.HouseId == owner.Id;
        if (theirs) { AmbitionSystem.Setback(w, founder!, $"The {b.Name}, which they founded, failed and was wound up."); w.WorldCounters[FailedFounderKey(founder!.Id)] = w.Turn; }
        if (owner.Id == mine)
        {
            w.WorldFlags.Add(FailedFlag); if (theirs && founder!.Id != w.ProtagonistId) founder.Flags.Add(FounderFailedChar);
            w.Log($"The {b.Name} has earned nothing for {FailsAfter} years and is wound up. {Money.Credits(left)} come back to {owner.Name}{(owed > 0 ? $", after {Money.Show(owed)} to those the house owes" : "")}{(theirs ? $"; it was {founder!.Name} who founded it" : "")}.");
        }
        else if (b.SystemId.Length > 0 && w.PlanetHolder(b.SystemId) == mine) w.Log($"The {b.Name} of {owner.Name}, on a world of yours, is wound up after {FailsAfter} years without a profit.");
    }

    /// <summary>Offers lapse after their year; an offer whose venture is gone or sold goes with it. Then every venture is reckoned.</summary>
    public void Tick(World w)
    {
        w.WorldFlags.Remove(OfferedFlag); w.WorldFlags.Remove(BoughtFlag); w.WorldFlags.Remove(RefusedFirstFlag); w.WorldFlags.Remove(FailingFlag); w.WorldFlags.Remove(FailedFlag); w.WorldFlags.Remove(SackedFlag); w.WorldFlags.Remove(ForeclosedFlag);
        Reckon(w);
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
