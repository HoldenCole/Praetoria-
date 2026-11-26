using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Edicts (docs/EDICTS.md; the owner: "Executive edits, laws to vote on, local laws, etc"). A law is standing and slow; an edict is a
/// decree by whoever holds an executive position, on their own authority, without a vote. It bites at once, lasts a fixed number of
/// years and lapses at that year's turn, costs the issuer influence or coin, and moves the estates of the issuer's house at once. The
/// positions it hangs on are ones that are held in a game: the head of a house over its own worlds; a consul, or the Emperor, or the
/// house that holds the capital while no consul sits, for the Reach; a sitting praetor or aedile; the holder of the emergency powers, or
/// the Emperor again. What an edict does while it lasts is written into the same modifier keys laws use and reaches the same rules
/// through one line in <see cref="PolicySystem.For"/> (a house's own worlds) and one in <see cref="PolicySystem.ReachModifier"/> (the
/// whole Reach); four keys of its own are read only from edicts (<see cref="DebtsStayed"/>, <see cref="CourtProof"/>,
/// <see cref="PriceCeiling"/>, <see cref="SenateCowed"/>). One of a kind at a time over the same worlds, two in force a person, and
/// not the same again for some years after it ends. An edict lapses early when its issuer leaves the position, unless it is one that
/// outlives them. In a Republic the Senate may strike down one that may be annulled (the motion kind <see cref="AnnulKind"/>), and the
/// issuer's house loses standing for it; under a throne or emergency powers there is no annulment. Other houses and magistrates issue
/// edicts for plain reasons, through the same command. RNG-free: what others do is decided by a hash of ids and the year.
/// </summary>
public static class EdictSystem
{
    public const string Head = "head", State = "state", Praetor = "praetor", Aedile = "aedile", Powers = "powers";
    public static readonly string[] Issuers = { Head, State, Praetor, Aedile, Powers };
    public const string DebtsStayed = "debts_stayed", CourtProof = "court_proof", PriceCeiling = "price_ceiling", SenateCowed = "senate_cowed";
    public const string AnnulKind = "annul";
    /// <summary>The most edicts one person may have in force at once.</summary>
    public const int MaxPerIssuer = 2;
    /// <summary>What an edict struck down by the Senate costs its issuer's house in standing.</summary>
    public const int AnnulledStanding = 3;
    /// <summary>A house named in proscriptions: the standing it loses, and what its head thinks of the issuer for it.</summary>
    public const int ProscribedStanding = 4, ProscribedRegard = -40;
    /// <summary>What each head whose house sent men to a requisition thinks of the issuer.</summary>
    public const int RequisitionRegard = -2;
    /// <summary>A good is short when its price is this many in the hundred of its usual price (an edict of maximum prices names it).</summary>
    public const int ShortAt = 150;
    /// <summary>Other houses: a head considers an edict one year in <see cref="HeadOneIn"/> when it has a reason, a magistrate or a
    /// holder of the Reach's executive one year in <see cref="OfficeOneIn"/>; a house moves an annulment one year in
    /// <see cref="AnnulOneIn"/>, when an edict galls it by <see cref="MoveAt"/> or more.</summary>
    public const int HeadOneIn = 4, OfficeOneIn = 2, AnnulOneIn = 2, MoveAt = 2;
    public const string IssuedKey = "edicts_issued", OthersKey = "edicts_issued_by_others", AnnulledKey = "edicts_annulled", AnnulMovedKey = "edict_annulments_moved",
        LapsedKey = "edicts_lapsed", GoneKey = "edicts_ended_with_issuer", WithdrawnKey = "edicts_withdrawn";
    private const string DangerSeenKey = "edict_danger_seen";
    public static string AgainKey(string domain, string defId) => $"edict_again:{(domain.Length == 0 ? "reach" : domain)}:{defId}";
    public static string ByKey(string issuer) => "edicts_by:" + issuer;
    public static string OfKey(string defId) => "edicts_of:" + defId;

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    private static void Bump(World w, string key, int by = 1) => w.WorldCounters[key] = w.Counter(key) + by;
    public static string Lower(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    private static string Upper(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // ---------------------------------------------------------------- positions

    /// <summary>Who may issue an edict of this kind, in words.</summary>
    public static string PositionName(string issuer) => issuer switch
    {
        Head => "the head of a house, over its own worlds",
        State => "a consul, the Emperor, or the head of the house that holds the capital while no consul sits",
        Praetor => "a sitting praetor", Aedile => "a sitting aedile",
        Powers => "the holder of the emergency powers, or the Emperor", _ => issuer
    };

    private static Character? EmperorHead(World w) => ThroneSystem.Emperor(w) is { } e ? Economy.HeadOf(w, e) : null;
    private static bool IsConsul(World w, Character c) => w.Offices.Any(o => o.CharacterId == c.Id && o.HouseId == c.HouseId && o.OfficeId.StartsWith("consul", StringComparison.Ordinal));
    private static bool AnyConsul(World w) => w.Offices.Any(o => o.OfficeId.StartsWith("consul", StringComparison.Ordinal) && w.Char(o.CharacterId) is { Alive: true });

    /// <summary>The position of the holder of the capital who speaks for the Reach while no consul sits.</summary>
    public const string CapitalSeat = "holder of the capital, while no consul sits";
    /// <summary>The position a person holds from which they may issue an edict of this kind, in words, or null if they hold none.</summary>
    public static string? PositionOf(World w, Character? c, string issuer)
    {
        if (c?.Alive != true || w.House(c.HouseId) is not { } house || World.Outsider(house.Id)) return null;
        switch (issuer)
        {
            case Head: return Economy.HeadOf(w, house)?.Id == c.Id ? "head of " + house.Name : null;
            case State:
                if (EmperorHead(w)?.Id == c.Id) return "the Emperor";
                if (w.IsImperium || !PolitySystem.InReach(w, house.Id)) return null;
                if (IsConsul(w, c)) return "consul";
                return !AnyConsul(w) && RegionSystem.CapitalHolder(w)?.Id == house.Id && Economy.HeadOf(w, house)?.Id == c.Id ? CapitalSeat : null;
            case Praetor: case Aedile:
                return !w.IsImperium && LadderSystem.Seat(w, c.Id)?.OfficeId == issuer ? issuer : null;
            case Powers:
                if (EmperorHead(w)?.Id == c.Id) return "the Emperor";
                return EmergencySystem.Holder(w)?.Id == c.Id ? "holder of the emergency powers" : null;
        }
        return null;
    }

    /// <summary>Does the person who issued an edict still hold the position it was issued from?</summary>
    public static bool StillHolds(World w, Edict e) => w.Char(e.IssuerId) is { } c && c.HouseId == e.IssuerHouseId && PositionOf(w, c, e.Issuer) != null;

    // ---------------------------------------------------------------- what is in force, read by the rules

    /// <summary>What the edicts in force on a house's own worlds add to its law (called once, from <see cref="PolicySystem.For"/>).</summary>
    public static void Into(World w, string houseId, Dictionary<string, int> into)
    {
        foreach (var e in w.Edicts) if (e.Domain == houseId) foreach (var (k, v) in e.Effects) into[k] = into.GetValueOrDefault(k) + v;
    }
    /// <summary>What the edicts in force on the whole Reach add to an effect (called once, from <see cref="PolicySystem.ReachModifier"/>,
    /// and by the rules that read the edicts' own keys).</summary>
    public static int Reach(World w, string key)
    {
        int sum = 0; foreach (var e in w.Edicts) if (e.Domain.Length == 0 && e.Effects.TryGetValue(key, out int v)) sum += v;
        return sum;
    }
    public static Edict? InForce(World w, string id) => w.Edicts.FirstOrDefault(e => e.Id == id);
    /// <summary>The moratorium on debts in force, if there is one.</summary>
    public static Edict? Stay(World w) => w.Edicts.FirstOrDefault(e => e.Domain.Length == 0 && e.Effects.GetValueOrDefault(DebtsStayed) > 0);
    /// <summary>A moratorium is in force: nothing falls due on any loan, no lender is paid interest, nobody may press or foreclose.</summary>
    public static bool DebtsAreStayed(World w) => Stay(w) != null;
    public static string? PressRefusal(World w) => Stay(w) is { } e ? $"{Lower(e.Name)} is in force until year {e.Until}: no creditor may press until it lapses" : null;
    /// <summary>What an edict on the courts adds to the forty in a hundred at which a praetor who goes by the pleading finds a charge proved.</summary>
    public static int CourtLean(World w) => Reach(w, CourtProof);
    /// <summary>A good's price on the exchanges, held under the ceiling an edict of maximum prices has set on it.</summary>
    public static double Capped(World w, GoodDef d, double price)
    {
        foreach (var e in w.Edicts) if (e.Domain.Length == 0 && e.TargetId == d.Id && e.Effects.TryGetValue(PriceCeiling, out int pct)) price = Math.Min(price, d.Base * pct / 100.0);
        return price;
    }
    /// <summary>Why a house may not move a motion while another rules by decree, or null.</summary>
    public static string? Cowed(World w, string houseId)
    {
        var e = w.Edicts.FirstOrDefault(x => x.Domain.Length == 0 && x.Effects.GetValueOrDefault(SenateCowed) > 0 && x.IssuerHouseId != houseId); if (e == null) return null;
        return $"{w.Char(e.IssuerId)?.Name ?? w.House(e.IssuerHouseId)?.Name ?? "the holder"} rules by decree until year {e.Until}: no house but theirs may move a motion";
    }
    /// <summary>Does an edict touch this house: it falls on the whole Reach, or on the house's worlds, or names it, or the house issued it.</summary>
    public static bool Touches(Edict e, string? houseId) => houseId != null && (e.Domain.Length == 0 || e.Domain == houseId || e.TargetId == houseId || e.IssuerHouseId == houseId);

    // ---------------------------------------------------------------- the short good

    /// <summary>The good the Reach is shortest of, against its usual price, with how dear it is in the hundred; null when there is no market.</summary>
    public static (GoodDef good, int pct)? Dearest(World w, MarketCatalog market)
    {
        if (w.Market == null) return null; (GoodDef good, int pct)? best = null;
        foreach (var d in market.Goods.Where(g => g.Buy && g.License.Length == 0).OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            if (!w.Market.Goods.TryGetValue(d.Id, out var g) || d.Base <= 0) continue;
            int pct = (int)Math.Round(100 * g.Price / d.Base);
            if (best == null || pct > best.Value.pct) best = (d, pct);
        }
        return best;
    }

    // ---------------------------------------------------------------- what an edict does, in words

    private static string Signed(int n) => (n > 0 ? "+" : n < 0 ? "−" : "") + Math.Abs(n);
    public static string NowSays(EdictDef d, string where)
    {
        var parts = new List<string>();
        foreach (var (k, v) in d.Now)
            parts.Add(k switch
            {
                "unrest" => $"unrest {Signed(v)} on {where}",
                "prosperity" => $"prosperity {Signed(v)} on {where}",
                "lawlessness" => $"lawlessness {Signed(v)} in {(d.Reach ? "every region" : "the regions of the house's worlds")}",
                "allegiance" => $"allegiance to the capital {Signed(v)} in {(d.Reach ? "every region" : "the regions of the house's worlds")}",
                "manpower_per_world" => $"{Signed(v)} manpower from each of {where}, for the house that holds it",
                "credits_per_world" => $"{Money.Signed(v)} credits from each of {where}, for the house that holds it",
                "legitimacy" => $"the issuer's house {(v > 0 ? "gains" : "loses")} {Math.Abs(v)} legitimacy",
                "trust" => $"the Reach's trust in its magistrates {Signed(v)}",
                "regard" => $"every other head thinks {Signed(v)} of the issuer",
                "requisition" => $"every other house of the Reach sends {v} manpower to the issuer's",
                "confiscate" => $"half the treasury of the house named, up to {Money.Credits(v)}, goes to the issuer's house; it loses {ProscribedStanding} legitimacy, and its head thinks {Signed(ProscribedRegard)} of the issuer",
                _ => $"{k.Replace('_', ' ')} {Signed(v)}"
            });
        return string.Join("; ", parts);
    }
    public static string EffectsSay(EdictDef d, string where, string? good = null) => EffectsSay(d.Effects, d.Reach, where, good);
    public static string EffectsSay(IReadOnlyDictionary<string, int> fx, bool reach, string where, string? good = null)
    {
        var parts = new List<string>();
        foreach (var (k, v) in fx)
            parts.Add(k switch
            {
                "unrest_drift" => $"unrest {Signed(v)} a year on {where}",
                "prosperity_drift" => $"prosperity settles {Math.Abs(2 * v)} {(v < 0 ? "lower" : "higher")} on {where}",
                "pop_growth" => $"{Signed(v)} people a year on each of {where}",
                "credits_per_world" => $"{Money.Signed(v)} credits a year from each of {where}",
                "manpower_per_world" => $"{Signed(v)} manpower a year from each of {where}",
                "lawlessness_drift" => $"lawlessness {Signed(v)} a year in {(reach ? "every region" : "the regions it holds")}",
                "tolls_bonus" => $"{Money.Signed(v)} credits on every toll",
                "market_price_pct" => $"{Signed(v)}% on what every house {(reach ? "of the Reach " : "")}pays the exchanges",
                "court_wage" => $"{(v < 0 ? "−" : "+")}{Money.Coin(Math.Abs(v))} a year on the wage of each place in the house's court",
                "court_loyalty" => $"the court's loyalty {Signed(v)} a year",
                DebtsStayed => "nothing falls due on any loan, no lender is paid interest, and no creditor may press or foreclose",
                CourtProof => $"a charge is found proved {Math.Abs(v)} times in a hundred {(v > 0 ? "more" : "less")} often before a praetor who goes by the pleading",
                PriceCeiling => $"{good ?? "the good the Reach is shortest of"} is bought and sold at no more than {v} in the hundred of its usual price",
                SenateCowed => "no house but the issuer's may move a motion in the Senate",
                "motion_cost" => $"{Signed(v)} influence for any other house to move a motion",
                _ => $"{k.Replace('_', ' ')} {Signed(v)}"
            });
        return string.Join("; ", parts);
    }
    /// <summary>Where an edict falls, in words.</summary>
    public static string Where(EdictDef d) => d.Reach ? "every world of the Reach" : "the house's worlds";
    /// <summary>What an edict costs, in words.</summary>
    public static string CostSays(EdictDef d) => string.Join(" and ", new[] { d.Influence > 0 ? $"{d.Influence} influence" : "", d.Credits > 0 ? Money.Credits(d.Credits) : "" }.Where(s => s.Length > 0)) is { Length: > 0 } c ? c : "nothing";
    /// <summary>The whole of it, for a button's title: the cost, what it does at once and while it lasts, how long, who minds.</summary>
    public static string Does(EdictDef d, PolicyCatalog? policies = null, string? good = null)
    {
        string now = NowSays(d, Where(d)), fx = EffectsSay(d, Where(d), good);
        string minds = policies == null || d.Opinions.Count == 0 ? "" : " The estates: " + string.Join(", ", d.Opinions.OrderByDescending(kv => Math.Abs(kv.Value)).Select(kv => $"{(policies.Estate(kv.Key)?.Name ?? kv.Key).Replace("The ", "")} {Signed(kv.Value)}")) + ".";
        return $"Costs {CostSays(d)}. " + (now.Length > 0 ? Upper(now) + ", at once. " : "") + (fx.Length > 0 ? $"For {Counted.Years(d.Years)}: {fx}." : $"It lasts {Counted.Years(d.Years)}.") + minds
            + (d.Annullable ? " The Senate may strike it down." : "") + (d.Outlives ? " It stands if you leave the position, and cannot be taken back." : "");
    }
    /// <summary>An edict in force, named as the chamber and the chronicle name it.</summary>
    public static string Title(World w, string edictId) => InForce(w, edictId) is { } e ? Title(w, e) : "an edict since ended";
    public static string Title(World w, Edict e) => $"{Lower(e.Name)}{(e.TargetId.Length > 0 && w.House(e.TargetId) is { } t ? " against " + t.Name : "")} of {w.Char(e.IssuerId)?.Name ?? w.House(e.IssuerHouseId)?.Name ?? "a magistrate"}, {e.Position}";

    // ---------------------------------------------------------------- issuing

    /// <summary>Why this person may not issue this edict now (cost aside from influence, which the command counts), or null.</summary>
    public static string? IssueRefusal(World w, ContentDatabase content, Character? actor, string defId, string targetId)
    {
        if (actor?.Alive != true || w.House(actor.HouseId) is not { } house) return "no house";
        var d = content.Edicts.Def(defId); if (d == null) return "no such edict";
        if (PositionOf(w, actor, d.Issuer) == null) return $"only {PositionName(d.Issuer)} may issue it";
        string domain = d.Reach ? "" : house.Id;
        if (!d.Reach && !w.HoldingsOf(house.Id).Any(h => h.IsPlanet)) return "the house holds no world for it to fall on";
        if (w.Edicts.FirstOrDefault(e => e.DefId == d.Id && e.Domain == domain) is { } same) return $"{Lower(d.Name)} is in force already{(d.Reach ? "" : " on the house's worlds")}, until year {same.Until}";
        if (d.Group.Length > 0 && w.Edicts.FirstOrDefault(e => e.Group == d.Group && e.Domain == domain) is { } rival) return $"{Lower(rival.Name)} stands until year {rival.Until}, and the two cannot be in force together";
        int held = w.Edicts.Count(e => e.IssuerId == actor.Id); if (held >= MaxPerIssuer) return $"you have {Counted.Of(held, "edict")} in force, and {MaxPerIssuer} is the most one person may have";
        int again = w.Counter(AgainKey(domain, d.Id)); if (again > w.Turn) return $"{Lower(d.Name)} was proclaimed lately: it may be again in year {again}";
        if (d.Target == "house")
        {
            var t = w.House(targetId); if (t == null || World.Outsider(targetId) || !w.OfTheReach(targetId)) return "name a house of the Reach";
            if (t.Id == house.Id) return "not your own house";
        }
        if (d.Target == "good")
        {
            var dear = Dearest(w, content.Market); if (dear == null) return "there is no market to set a price on";
            if (dear.Value.pct < ShortAt) return $"nothing is short: the dearest good, {dear.Value.good.Name.ToLowerInvariant()}, is at {dear.Value.pct} in the hundred of its usual price, and an edict of maximum prices wants {ShortAt}";
        }
        if (d.Credits > 0)
        {
            bool own = OwnPurse(w, actor, d);
            int have = own ? actor.Purse : house.Treasury.Credits;
            if (have < d.Credits) return own ? $"needs {Money.Credits(d.Credits)} of your own, and you have {Money.Show(Math.Max(0, have))}" : $"needs {Money.Credits(d.Credits)} and the house has {Money.Show(Math.Max(0, have))}";
        }
        return null;
    }
    /// <summary>A relative of the player pays for an edict out of their own purse, whatever its kind, as for games: the house's treasury
    /// is the head's. Everyone else pays from the house.</summary>
    private static bool OwnPurse(World w, Character actor, EdictDef d) => actor.HouseId == w.Protagonist?.HouseId && actor.Id != w.ProtagonistId;

    /// <summary>The worlds an edict falls on: the house's own, or every peopled world of the houses of the Reach.</summary>
    private static List<Holding> Worlds(World w, Edict e) =>
        w.Holdings.Values.Where(h => h.IsPlanet && !h.InRebellion && (e.Domain.Length > 0 ? h.OwnerId == e.Domain : w.OfTheReach(h.OwnerId))).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    private static List<string> Regions(World w, Edict e, List<Holding> worlds) =>
        w.Galaxy == null ? new List<string>() : e.Domain.Length == 0 ? w.Galaxy.ReachRegions.Select(r => r.Id).OrderBy(r => r, StringComparer.Ordinal).ToList()
            : worlds.Select(h => w.Galaxy.System(h.SystemId)?.RegionId ?? "").Where(r => r.Length > 0).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();
    private static void Trust(World w, int by) => w.WorldCounters[LadderSystem.TrustKey] = Math.Clamp(LadderSystem.Trust(w) + by, 0, by > 0 ? Math.Max(LadderSystem.Trust(w), LadderSystem.TrustCeiling) : 100);
    private static void Regard(World w, House of, Character whom, int by)
    {
        if (Economy.HeadOf(w, of) is { } head && head.Id != whom.Id) { var r = w.Relationship(head.Id, whom.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100); }
    }
    private static IEnumerable<House> OthersOfTheReach(World w, string houseId) => w.Houses.Values.Where(h => h.Id != houseId && w.OfTheReach(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal);

    /// <summary>The edict is proclaimed: paid for, written down, done at once, and felt by the estates of the issuer's house.</summary>
    public static Edict Issue(World w, ContentDatabase content, Character actor, EdictDef d, string targetId, string reason = "")
    {
        var house = w.House(actor.HouseId)!; string domain = d.Reach ? "" : house.Id;
        string position = PositionOf(w, actor, d.Issuer) ?? d.Issuer;
        string good = "";
        if (d.Target == "good") { var dear = Dearest(w, content.Market); targetId = dear?.good.Id ?? ""; good = dear?.good.Name.ToLowerInvariant() ?? ""; }
        if (d.Target.Length == 0) targetId = "";
        if (d.Credits > 0) { if (OwnPurse(w, actor, d)) actor.Purse -= d.Credits; else house.Treasury.Credits -= d.Credits; }
        int n = w.Counter(IssuedKey) + 1; w.WorldCounters[IssuedKey] = n;
        var e = new Edict
        {
            Id = "edict_" + n, DefId = d.Id, Name = d.Name, IssuerHouseId = house.Id, IssuerId = actor.Id, Issuer = d.Issuer, Position = position, Domain = domain, TargetId = targetId,
            Since = w.Turn, Until = w.Turn + d.Years, Outlives = d.Outlives, Annullable = d.Annullable, Group = d.Group, Again = d.Again, Effects = new Dictionary<string, int>(d.Effects), Opinions = new Dictionary<string, int>(d.Opinions)
        };
        w.Edicts.Add(e);
        Bump(w, ByKey(d.Issuer)); Bump(w, OfKey(d.Id)); string? mine = w.Protagonist?.HouseId; if (actor.Id != w.ProtagonistId) Bump(w, OthersKey);
        Now(w, e, d, actor, house);
        foreach (var (estate, by) in d.Opinions.OrderBy(kv => kv.Key, StringComparer.Ordinal)) PolicySystem.Sway(w, house.Id, estate, by);
        var target = w.House(targetId);
        string named = target != null ? $" against {target.Name}" : good.Length > 0 ? $" on {good}" : "";
        w.Log($"{actor.Name}, {position}, proclaims {Lower(d.Name)}{named}{(d.Reach ? " for the whole Reach" : " on the worlds of " + house.Name)}, until year {e.Until}.{(reason.Length > 0 ? " " + Upper(reason) + "." : "")}");
        if (mine != null && actor.Id != w.ProtagonistId)
        {
            if (target?.Id == mine) w.Tell("warn", $"{actor.Name}, {position}, has named your house in proscriptions: half its treasury is taken, and the lists stay open until year {e.Until}.", "laws", e.Id);
            else if (d.Reach) w.Tell(AnnulLean(w, w.House(mine)!, e) >= 2 ? "warn" : "info", $"{actor.Name} of {(house.Id == mine ? "your house" : house.Name)}, {position}, proclaims {Lower(d.Name)}{named} for the whole Reach, until year {e.Until}{(house.Id == mine && d.Credits > 0 ? $", and pays the {Money.Credits(d.Credits)} out of their own purse" : "")}: {EffectsSay(e.Effects, true, "every world", good.Length > 0 ? good : null)}.{(e.Annullable && AnnulRefusal(w, e.Id, mine) == null ? " The Senate may be asked to strike it down." : "")}", "laws", e.Id);
        }
        return e;
    }

    private static void Now(World w, Edict e, EdictDef d, Character actor, House house)
    {
        if (d.Now.Count == 0) return;
        var worlds = Worlds(w, e);
        foreach (var (k, v) in d.Now)
            switch (k)
            {
                case "unrest": foreach (var h in worlds) h.Unrest = Math.Clamp(h.Unrest + v, 0, 100); break;
                case "prosperity": foreach (var h in worlds) h.Prosperity = Math.Clamp(h.Prosperity + v, 0, 100); break;
                case "lawlessness": foreach (var r in Regions(w, e, worlds)) { var rs = RegionSystem.StateOf(w, r); rs.Lawlessness = Math.Clamp(rs.Lawlessness + v, 0, 100); } break;
                case "allegiance": foreach (var r in Regions(w, e, worlds)) { var rs = RegionSystem.StateOf(w, r); rs.Allegiance = Math.Clamp(rs.Allegiance + v, 0, 100); } break;
                case "manpower_per_world": foreach (var h in worlds) if (w.House(h.OwnerId) is { } o) o.Treasury.Manpower += v; break;
                case "credits_per_world": foreach (var h in worlds) if (w.House(h.OwnerId) is { } o) o.Treasury.Credits += v; break;
                case "legitimacy": LegitimacyBook.Move(w, house, v, LegitimacyBook.Laws, d.Name + ", proclaimed"); break;
                case "trust": Trust(w, v); break;
                case "regard": foreach (var h in OthersOfTheReach(w, house.Id)) Regard(w, h, actor, v); break;
                case "requisition":
                    foreach (var h in OthersOfTheReach(w, house.Id))
                    {
                        int sent = Math.Clamp(h.Treasury.Manpower, 0, v); if (sent <= 0) continue;
                        h.Treasury.Manpower -= sent; house.Treasury.Manpower += sent; Regard(w, h, actor, RequisitionRegard);
                    }
                    break;
                case "confiscate":
                    if (w.House(e.TargetId) is { } t)
                    {
                        int take = Math.Min(v, Math.Max(0, t.Treasury.Credits / 2)); t.Treasury.Credits -= take; house.Treasury.Credits += take;
                        LegitimacyBook.Move(w, t, -ProscribedStanding, LegitimacyBook.Scandal, $"Named an enemy of the state by {actor.Name}");
                        Regard(w, t, actor, ProscribedRegard);
                        w.Log($"The goods of {t.Name} are forfeit: {Money.Credits(take)} passes to {house.Name}.");
                    }
                    break;
            }
    }

    // ---------------------------------------------------------------- ending

    public const string Lapsed = "lapsed", IssuerGone = "gone", Annulled = "annulled", Withdrawn = "withdrawn";
    /// <summary>An edict ends: by its years, with its issuer, by the Senate, or taken back. It may not be issued again over the same
    /// worlds for its <see cref="EdictDef.Again"/> years.</summary>
    public static void End(World w, Edict e, string how)
    {
        if (!w.Edicts.Remove(e)) return;
        w.WorldCounters[AgainKey(e.Domain, e.DefId)] = w.Turn + e.Again;
        Bump(w, how switch { Lapsed => LapsedKey, IssuerGone => GoneKey, Annulled => AnnulledKey, _ => WithdrawnKey });
        string who = w.Char(e.IssuerId)?.Name ?? w.House(e.IssuerHouseId)?.Name ?? "the issuer";
        string line = how switch
        {
            Lapsed => $"{Upper(Lower(e.Name))} of {who} lapses: its {Counted.Years(e.Until - e.Since)} are run.",
            IssuerGone => $"{Upper(Lower(e.Name))} of {who} lapses with the position it was issued from.",
            Annulled => $"The Senate strikes down {Title(w, e)}. It is at an end, and {w.House(e.IssuerHouseId)?.Name ?? "the house"} is thought the less of.",
            _ => $"{who} takes back {Lower(e.Name)}."
        };
        w.Log(line);
        var mine = w.Protagonist?.HouseId;
        // the player is told when what ends was the house's own, fell on its worlds or named it, and when the Senate strikes one down
        bool ours = mine != null && (e.IssuerHouseId == mine || e.Domain == mine || e.TargetId == mine);
        if (how != Withdrawn && (ours || how == Annulled && Touches(e, mine))) w.Tell(how == Annulled && e.IssuerHouseId == mine ? "warn" : "info", line, "laws", e.Id);
    }

    public static string? WithdrawRefusal(World w, Character? actor, string edictId)
    {
        if (actor?.Alive != true) return "no house";
        var e = InForce(w, edictId); if (e == null) return "no such edict in force";
        if (e.IssuerId != actor.Id) return "only the one who issued it may take it back";
        return e.Outlives ? $"{Lower(e.Name)} has been given and cannot be taken back" : null;
    }

    // ---------------------------------------------------------------- the year

    /// <summary>Edicts whose years are run lapse, and those whose issuer has left the position, unless they outlive them. Called first
    /// in the year's turn, before the house's court is paid, so that an edict is read on as many paydays as it has years.</summary>
    public static void Lapse(World w)
    {
        foreach (var e in w.Edicts.OrderBy(e => e.Since).ThenBy(e => e.Id, StringComparer.Ordinal).ToList())
        {
            if (e.Until <= w.Turn) End(w, e, Lapsed);
            else if (!e.Outlives && !StillHolds(w, e)) End(w, e, IssuerGone);
        }
    }

    /// <summary>Edicts whose issuer has left the position end, unless they outlive them. Called last in the year's turn, after the
    /// elections, the seats, the powers and the deaths, so that an edict ends the same year as the position it was issued from.</summary>
    public static void EndWithIssuers(World w)
    {
        foreach (var e in w.Edicts.OrderBy(e => e.Since).ThenBy(e => e.Id, StringComparer.Ordinal).ToList())
            if (!e.Outlives && !StillHolds(w, e)) End(w, e, IssuerGone);
    }

    /// <summary>At the year's turn: edicts whose years are run lapse, and those whose issuer has left the position, unless they outlive
    /// them; then the other houses and magistrates issue what they have reason to.</summary>
    public static void Tick(World w, ContentDatabase content, CommandExecutor executor, CommandContext ctx)
    {
        Lapse(w);
        if (!content.Edicts.IsEmpty && !w.InAcademy) Others(w, content, executor, ctx);
        if (EmergencySystem.Danger(w) != null) w.WorldCounters[DangerSeenKey] = w.Turn;
    }

    /// <summary>Everyone but the player who holds a position edicts hang on, with the kinds they may issue: the heads of the other
    /// houses, the consuls, the Emperor, the holder of the capital while no consul sits, the holder of the emergency powers, and every
    /// sitting praetor and aedile. The player's relatives are among them in any of these positions: each is a person with a will of
    /// their own, decides the business of the position, and pays for an edict out of their own purse.</summary>
    public static List<(Character who, List<string> kinds)> Holders(World w)
    {
        var by = new SortedDictionary<string, List<string>>(StringComparer.Ordinal); string me = w.ProtagonistId, mine = w.Protagonist?.HouseId ?? "";
        void Add(Character? c, string kind) { if (c is not { Alive: true } || c.Id == me || PositionOf(w, c, kind) == null) return; if (!by.TryGetValue(c.Id, out var l)) by[c.Id] = l = new List<string>(); if (!l.Contains(kind)) l.Add(kind); }
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id))) Add(Economy.HeadOf(w, h), Head);
        foreach (var o in w.Offices.Where(o => o.OfficeId.StartsWith("consul", StringComparison.Ordinal))) Add(w.Char(o.CharacterId), State);
        if (RegionSystem.CapitalHolder(w) is { } cap) Add(Economy.HeadOf(w, cap), State);
        if (EmperorHead(w) is { } emp) { Add(emp, State); Add(emp, Powers); }
        Add(EmergencySystem.Holder(w), Powers);
        foreach (var m in w.Magistrates.Where(m => m.OfficeId is Praetor or Aedile)) Add(w.Char(m.CharacterId), m.OfficeId);
        return by.Select(kv => (w.Char(kv.Key)!, kv.Value.OrderBy(k => Array.IndexOf(Issuers, k)).ToList())).ToList();
    }

    private static void Others(World w, ContentDatabase content, CommandExecutor executor, CommandContext ctx)
    {
        foreach (var (who, kinds) in Holders(w))
        {
            // the Reach's business first: one edict a person a year at most
            foreach (var kind in kinds.OrderBy(k => k == Head ? 1 : 0))
            {
                if (Hash($"edict:{who.Id}:{kind}:{w.Turn}") / 7 % (uint)(kind == Head ? HeadOneIn : OfficeOneIn) != 0) continue;
                // a magistrate whose seat falls vacant at this year's election, a consul at the end of the term, the holder of the capital in
                // a year consuls are elected, and a holder of the powers in the year they are laid down decree nothing: it would lapse with them
                // at this same year's turn
                if (kind is Praetor or Aedile && LadderSystem.Due(w, content.Offices)) continue;
                if (kind == State && ElectionSystem.Due(w) && PositionOf(w, who, State) is "consul" or CapitalSeat) continue;
                if (kind == Powers && w.Emergency is { } em && em.HolderId == who.Id && (em.Due || w.Turn >= em.Until || EmergencySystem.Danger(w) == null)) continue;
                bool done = false;
                foreach (var d in content.Edicts.Defs.Where(d => d.Issuer == kind).OrderBy(d => Hash($"{d.Id}:{who.Id}:{w.Turn}")).ThenBy(d => d.Id, StringComparer.Ordinal))   // what presses first, in no fixed order
                {
                    if (Reason(w, content, d, who, out string target) is not { } why) continue;
                    var cmd = new IssueEdictCommand(who.Id, d.Id, content, target) { Reason = why };
                    if (executor.TryExecute(cmd, ctx)) { done = true; break; }
                }
                if (done) break;
            }
        }
    }

    /// <summary>The plain reason another house or magistrate would issue this edict now, in words, or null; and the house it names.</summary>
    public static string? Reason(World w, ContentDatabase content, EdictDef d, Character who, out string target)
    {
        target = ""; var house = w.House(who.HouseId); if (house == null) return null;
        var worlds = w.HoldingsOf(house.Id).Where(h => h.IsPlanet && !h.InRebellion).ToList();
        int credits = house.Treasury.Credits;
        switch (d.When)
        {
            case "unrest": return worlds.Any(h => h.Unrest >= 15) ? "its worlds are restless" : null;
            case "war": return w.Wars.Any(x => x.Involves(house.Id)) && house.Treasury.Manpower < 12 ? "it is at war and short of men" : null;
            case "red": return worlds.Count >= 3 && credits < 5 ? "it holds many worlds and has little in hand" : null;
            case "unrest_rich": return worlds.Any(h => h.Unrest >= 25) && credits >= 40 ? "its worlds are restless and it can afford to be generous" : null;
            case "pinched": return credits <= 1 && worlds.Count is >= 1 and <= 2 ? "it has next to nothing in hand" : null;
            case "unrest_coin": return worlds.Any(h => h.Unrest >= 10) && credits >= 20 ? "its worlds are restless" : null;
            case "reach_unrest":
                if (w.Holdings.Values.Count(h => h.IsPlanet && w.OfTheReach(h.OwnerId) && h.Unrest >= 20) >= 4 || worlds.Any(h => h.Unrest >= 25)) return "the worlds of the Reach are restless";
                return PolicySystem.Approval(w, house.Id, "commons") <= -20 ? "the commons have turned against the house" : null;
            case "danger": return EmergencySystem.Danger(w) is { } danger && (w.Wars.Any(x => x.Involves(house.Id)) || NationSystem.AtWar(w) != null) ? "the Reach is in danger from " + danger : null;
            case "debts":
            {
                bool owes = house.Debt > 0 || DealingsSystem.LoanOwedBy(w, house.Id) != null, lends = w.HouseLoans.Any(l => l.LenderId == house.Id && l.Owed > 0) || w.Houses.Values.Any(h => h.Debt > 0 && h.LenderId == house.Id);
                return owes && !lends ? "its own house is in debt" : null;
            }
            case "dear": return Dearest(w, content.Market) is { pct: >= 160 } dear ? $"{dear.good.Name.ToLowerInvariant()} is at {dear.pct} in the hundred of its usual price" : null;
            case "dear_some": return Dearest(w, content.Market) is { pct: >= 130 } some ? $"{some.good.Name.ToLowerInvariant()} is dear" : null;
            case "lawless":
                if (w.Galaxy is not { } g) return null;
                if (worlds.Any(h => g.System(h.SystemId)?.RegionId is { } r && RegionSystem.StateOf(w, r).Lawlessness >= 35)) return "raiders work the lanes where its worlds lie";
                return g.ReachRegions.Any(r => RegionSystem.StateOf(w, r.Id).Lawlessness >= 50) ? "raiders hold the lanes of a region of the Reach" : null;
            case "peace":
            {
                bool past = EmergencySystem.Danger(w) == null && w.Counter(DangerSeenKey) == w.Turn - 1 && w.Turn > 1;
                bool won = w.PastWars.Any(x => x.Ended >= w.Turn - 1 && x.Involves(house.Id) && x.Lead(house.Id) > 0);
                return past ? "the danger is past" : won ? "its war is won" : LadderSystem.Trust(w) < 45 && credits >= 20 ? "the Reach thinks little of its magistrates" : null;
            }
            case "upright": return who.NatureTraits.Contains("Honorable") || who.NatureTraits.Contains("Just") ? "the praetor is upright" : null;
            case "exposed": return w.OfficeRecord.Any(x => !x.Answered && x.HouseId == house.Id && w.Char(x.CharacterId)?.Alive == true) ? "someone of the praetor's house has something on the record" : null;
            case "enemy":
            {
                var worst = OthersOfTheReach(w, house.Id).Select(h => (h, d: Economy.HeadOf(w, h) is { } hh ? w.GetRelationship(hh.Id, who.Id)?.Disposition ?? 0 : 0))
                    .Where(x => x.d <= -40).OrderBy(x => x.d).ThenBy(x => x.h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (worst.h == null) return null; target = worst.h.Id; return $"{worst.h.Name} is its enemy";
            }
            case "struck_at":
                return w.Motions.Any(m => m.Open && m.TargetId == house.Id && m.Kind is "censure" or "recall" or "embargo") || w.Counter(CaesarSystem.CensuredKey(house.Id)) >= Math.Max(1, w.Turn - 2) ? "the Senate has struck at it" : null;
            case "grave": return EmergencySystem.Gravity(w) >= 3 || w.Rising != null ? "the Reach is fighting for its life" : null;
        }
        return null;
    }

    // ---------------------------------------------------------------- the Senate's check

    /// <summary>The edicts the Senate may be asked to strike down.</summary>
    public static IEnumerable<Edict> Annullable(World w) => w.Edicts.Where(e => e.Domain.Length == 0 && e.Annullable).OrderBy(e => e.Since).ThenBy(e => e.Id, StringComparer.Ordinal);

    /// <summary>Why the annulment of this edict may not be moved by this house, or null.</summary>
    public static string? AnnulRefusal(World w, string edictId, string byHouseId)
    {
        var e = InForce(w, edictId); if (e == null) return "name an edict in force";
        if (w.IsImperium) return "under a throne there is no Senate to strike down an edict";
        if (w.Emergency != null) return "while the emergency powers run the Senate does not sit in judgement on an edict";
        if (e.Domain.Length > 0) return "it falls on one house's own worlds: that is the house's business, not the Senate's";
        if (!e.Annullable) return $"{Lower(e.Name)} is not a thing the Senate may strike down";
        if (e.Until - w.Turn < 2) return $"it lapses at the turn of year {e.Until}, before the Senate could vote on it";
        if (e.IssuerHouseId == byHouseId) return e.IssuerId == w.ProtagonistId && w.Protagonist?.HouseId == byHouseId ? "it is your own edict: take it back instead" : $"it is your own house's edict, and only {w.Char(e.IssuerId)?.Name ?? "its issuer"} may take it back";
        return w.Motions.Any(m => m.Open && m.Kind == AnnulKind && m.TargetId == e.Id) ? "its annulment is already before the Senate" : null;
    }

    /// <summary>How a house leans on striking an edict down: the issuer's house against; a house the edict hurts for and one it helps
    /// against, by what it does to that house's purse, worlds, loans and courts; what the estates loyal to the house make of it; and
    /// what its head thinks of the issuer, whose friends and bench stand by it.</summary>
    public static int AnnulLean(World w, House h, Edict e)
    {
        if (h.Id == e.IssuerHouseId) return -4;
        int lean = 0; var fx = e.Effects; int Fx(string k) => fx.GetValueOrDefault(k);
        var worlds = w.HoldingsOf(h.Id).Where(x => x.IsPlanet).ToList();
        if (Fx(DebtsStayed) > 0)
        {
            bool lends = w.HouseLoans.Any(l => l.LenderId == h.Id && l.Owed > 0) || w.Houses.Values.Any(x => x.Debt > 0 && x.LenderId == h.Id);
            bool owes = h.Debt > 0 || w.HouseLoans.Any(l => l.BorrowerId == h.Id && l.Owed > 0);
            lean += lends ? 3 : owes ? -2 : 0;
        }
        if (Fx(PriceCeiling) > 0 && e.TargetId.Length > 0) lean += h.Treasury.Get(e.TargetId) >= 10 ? 2 : w.Fleets.Values.Any(f => f.OwnerId == h.Id && f.Strength > 0) ? -1 : 0;
        if (Fx("market_price_pct") < 0) lean += (worlds.Count >= 3 ? 1 : 0) - (w.Fleets.Values.Any(f => f.OwnerId == h.Id && f.Strength > 0) ? 1 : 0);
        if (Fx("unrest_drift") > 0 && worlds.Any(x => x.Unrest >= 30)) lean += 2;
        if (Fx("unrest_drift") < 0 && worlds.Any(x => x.Unrest >= 20)) lean -= 1;
        if (Fx("manpower_per_world") > 0 && Fx("unrest_drift") > 0) lean += 1;   // a requisition: its own men marched off
        if (Fx("lawlessness_drift") < 0)
        {
            if (w.Galaxy is { } g && worlds.Any(x => g.RingOf(x.SystemId) == Galaxy.Ring.Rim)) lean += 1;
            if (w.Galaxy is { } g2 && worlds.Any(x => g2.System(x.SystemId)?.RegionId is { } r && RegionSystem.StateOf(w, r).Lawlessness >= 40)) lean -= 2;
        }
        if (Fx(CourtProof) != 0)
        {
            bool exposed = w.OfficeRecord.Any(x => !x.Answered && x.HouseId == h.Id && w.Char(x.CharacterId)?.Alive == true);
            if (exposed) lean += Fx(CourtProof) > 0 ? 2 : -2;
        }
        // the estates loyal to the house: what galls them galls it
        int loyalScore = 0;
        foreach (var (estate, by) in e.Opinions) if (PolicySystem.Approval(w, h.Id, estate) >= PolicySystem.Loyal) loyalScore -= by;
        lean += Math.Clamp(loyalScore / 6, -2, 2);
        // the issuer: friends stand by it, enemies want it gone, its bench closes ranks
        var head = Economy.HeadOf(w, h); int regard = head != null ? w.GetRelationship(head.Id, e.IssuerId)?.Disposition ?? 0 : 0;
        lean += regard >= 30 ? -2 : regard <= -30 ? 2 : 0;
        if (FactionSystem.Of(w, h.Id) is { } bench && FactionSystem.Of(w, e.IssuerHouseId)?.Id == bench.Id) lean -= 1;
        return lean;
    }

    /// <summary>Another house moves to strike down an edict that galls it, one year in <see cref="AnnulOneIn"/>: the house it galls most,
    /// by <see cref="MoveAt"/> or more, that may move a motion and has none before the Senate. Never the player's house. True if moved.</summary>
    public static bool MoveAnnulment(World w, SenateSystem senate, string? mine)
    {
        if (senate.Motions.Def(AnnulKind) == null || w.Motions.Any(m => m.Open && m.Kind == AnnulKind) || !w.Edicts.Any(e => e.Domain.Length == 0 && e.Annullable)) return false;
        if (Hash($"annul:{w.Turn}") / 7 % AnnulOneIn != 0) return false;
        (House h, Edict e, int lean)? best = null;
        foreach (var e in Annullable(w).ToList())
            foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null && senate.MayPropose(w, h.Id) && !w.Motions.Any(m => m.Open && m.ProposerHouseId == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                if (AnnulRefusal(w, e.Id, h.Id) != null || SenateSystem.Stale(w, h.Id, AnnulKind, e.Id)) continue;
                int lean = AnnulLean(w, h, e); if (lean >= MoveAt && (best == null || lean > best.Value.lean)) best = (h, e, lean);
            }
        if (best is not { } b) return false;
        senate.Add(w, b.h.Id, AnnulKind, b.e.Id); Bump(w, AnnulMovedKey);
        return true;
    }

    /// <summary>The Senate has carried the annulment: the edict ends at once and its issuer's house loses standing. Carried under a
    /// throne, or while emergency powers run (the powers began after it was moved), it does nothing.</summary>
    public static void AnnulCarried(World w, string edictId)
    {
        var e = InForce(w, edictId); if (e == null) { w.Log("The edict the Senate voted to strike down had already lapsed."); return; }
        if (w.IsImperium || w.Emergency != null)
        {
            string line = $"The Senate's vote to strike down {Title(w, e)}, stands for nothing: {(w.IsImperium ? "under a throne there is no Senate to strike down an edict" : "while the emergency powers run the Senate does not sit in judgement on an edict")}.";
            w.Log(line); if (w.Protagonist?.HouseId is { } mine && Touches(e, mine)) w.Tell("info", line, "laws", e.Id);
            return;
        }
        if (w.House(e.IssuerHouseId) is { } house) LegitimacyBook.Move(w, house, -AnnulledStanding, LegitimacyBook.Senate, $"{e.Name} struck down by the Senate");
        End(w, e, Annulled);
    }
}
