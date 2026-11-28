using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Foreign gold in the Senate (docs/BEYOND.md, "Foreign gold in the Senate"; docs/IDEAS_FROM_IMPERATOR.md, 9; GAPS N11). A nation that
/// would rather buy than fight (the Free Cities: <see cref="NationDef.SenateGold"/>) keeps a finite purse, filled each year from its wealth.
/// When a motion of war on it, or on a nation it favours (<see cref="NationDef.Favours"/>), is on the docket and would carry or nearly,
/// it sends senators the gift that asks a favour that houses already send one another: a sum to vote against the war, or to stay away.
/// Sparingly, it first offers the house that moved the war a sum to withdraw it. A head who takes it has the sum in their own purse and
/// it goes on the record (<see cref="OfficeDeed"/>), where a rival may charge it before a praetor at once; refusing costs nothing;
/// exposing it to the chamber mends the Reach's trust and the house's standing, sets the chamber the more for the war, and the nation
/// thinks the less of the Reach. Other houses answer by nature: the upright expose it, those for sale by nature and those short of
/// money take it, the rest refuse. The player is asked on the Senate window. Under a throne nothing is sent: the record that would hold it
/// is not kept there (<see cref="LadderSystem.Active"/>), and the Cities treat with the throne. RNG-free.
/// </summary>
public static class ForeignGoldSystem
{
    public const string Take = "take", Refuse = "refuse", Expose = "expose";
    public static readonly string[] Answers = { Take, Refuse, Expose };
    public const string Against = "against", Away = "away", Withdraw = "withdraw";
    public const string Taken = "taken", Refused = "refused", Exposed = "exposed", Lapsed = "lapsed";
    /// <summary>The kind of act on the record that the courts know it by.</summary>
    public const string Deed = "foreign_gold";
    /// <summary>The purse fills by the nation's wealth each year (the Cities' 9: 900 credits), up to its <see cref="NationDef.SenateGold"/> years of
    /// wealth (the Cities' 12: 10,800 credits); it opens with <see cref="StartYears"/> years in it.</summary>
    public const int StartYears = 5;
    /// <summary>The sums, in the books' hundreds: a base and so much for each seat the house weighs in the Senate.</summary>
    public const int AgainstBase = 20, AgainstPerSeat = 2, AwayBase = 12, AwayPerSeat = 1, WithdrawBase = 40, WithdrawPerSeat = 3;
    /// <summary>A house that leans for the war by more than this is asked only to stay away.</summary>
    public const int AwayAbove = 2;
    /// <summary>The most senators the Cities write to on one motion, besides the player and the mover.</summary>
    public const int MostReached = 4;
    /// <summary>They do not trouble with a war that fails by a fifth of the seats that vote, or more.</summary>
    public const int HopelessPart = 5;
    /// <summary>A war is bought off before the vote at most once in this many years.</summary>
    public const int BuyOffEvery = 15;
    /// <summary>What exposing it does: the Reach's trust, the house's legitimacy, what the nation thinks of the Reach, and the chamber's anger (a lean for the war, every house, on that motion).</summary>
    public const int ExposeTrust = 2, ExposeLegitimacy = 3, ExposeRegard = 10, ExposeAnger = 1;
    /// <summary>A mover who takes the sum and withdraws loses a step of standing in the Senate, as a motion thrown out does.</summary>
    public const int WithdrawStanding = 1;
    /// <summary>One who took it and voted for the war all the same: the nation thinks the less of the Reach, and sends that house nothing for some years.</summary>
    public const int BrokenRegard = 5, BrokenYears = 10;
    /// <summary>A head who thinks this well of the player tells them of gold sent to their own house.</summary>
    public const int FriendTells = 30;
    public const int KeptYears = 20;
    public const string OfferedFlag = "foreign_gold_offered";
    public const string OffersKey = "gold_offers", TakenKey = "gold_taken", RefusedKey = "gold_refused", ExposedKey = "gold_exposed", LapsedKey = "gold_lapsed",
        TurnedKey = "gold_turned", BoughtOffKey = "gold_bought_off", WithdrawOffersKey = "gold_withdraw_offers", MotionsKey = "gold_motions", BrokenKey = "gold_broken", SpentKey = "gold_spent";
    public static string BrokenKeyFor(string houseId) => "gold_shut:" + houseId;

    // ---------------- who pays, and the purse ----------------

    /// <summary>The nation that would pay to stop a war on this one: itself, if it keeps a purse for the Senate, or one that keeps a
    /// purse and favours it. Not a nation fallen or a client of the Reach.</summary>
    public static Nation? Payer(World w, NationCatalog catalog, string warOnId)
    {
        foreach (var d in catalog.Defs.Where(d => d.SenateGold > 0).OrderBy(d => d.Id, StringComparer.Ordinal))
            if ((d.Id == warOnId || d.Favours.Contains(warOnId)) && w.Nation(d.Id) is { Fallen: false, Vassal: false } n) return n;
        return null;
    }
    public static int Cap(NationDef d) => d.Wealth * d.SenateGold;

    /// <summary>Each year the purse of a nation that buys votes fills by its wealth, to its limit; not while it is at war (the money goes on hulls) or pays tribute.</summary>
    private static void Fill(World w, NationCatalog catalog)
    {
        foreach (var d in catalog.Defs.Where(d => d.SenateGold > 0).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (w.Nation(d.Id) is not { } n || n.Fallen) continue;
            if (!n.PurseOpened) { n.PurseOpened = true; n.Purse = Math.Min(Cap(d), d.Wealth * StartYears); }
            else if (!n.AtWar && !n.Subdued(w.Turn)) n.Purse = Math.Min(Cap(d), n.Purse + d.Wealth);
        }
    }

    /// <summary>The sum the nation sends for one thing, by the seats of the house it is sent to.</summary>
    public static int SumFor(string ask, int seats) => ask switch
    {
        Against => AgainstBase + AgainstPerSeat * seats,
        Away => AwayBase + AwayPerSeat * seats,
        _ => WithdrawBase + WithdrawPerSeat * seats
    };

    /// <summary>The motions of war the gold is about: a war on the nation that pays, or one it favours.</summary>
    public static bool IsWarMotion(SenateSystem senate, Motion m) => senate.Motions.Def(m.Kind) is { Target: "nation" } def && def.Interest == "foreign_war";

    // ---------------- the year ----------------

    /// <summary>After the Senate has sat: what was sent about motions now counted is settled, the purses fill, and the nations write to the
    /// senators about any war on the docket that is theirs to stop. Not under a throne, where the record is not kept (<see cref="LadderSystem.Active"/>):
    /// there nothing is written, and whatever still waits on an answer goes home.</summary>
    public static void Tick(World w, NationCatalog catalog, SenateSystem senate, OfficeCatalog offices)
    {
        if (catalog.IsEmpty || !catalog.Defs.Any(d => d.SenateGold > 0)) return;
        string? mine = w.Protagonist?.HouseId;
        SettleCounted(w, senate, mine);
        w.GoldOffers.RemoveAll(o => o.Settled && w.Turn - o.Turn > KeptYears);
        Fill(w, catalog);
        if (!LadderSystem.Active(w, offices)) { foreach (var o in w.GoldOffers.Where(o => o.Answer.Length == 0 && !o.Settled).ToList()) Settle(w, o, turned: false); }
        else foreach (var m in SenateSystem.Docket(w).Where(m => IsWarMotion(senate, m) && !w.GoldOffers.Any(o => o.MotionId == m.Id)).ToList())
            if (Payer(w, catalog, m.TargetId) is { } payer) Write(w, catalog, senate, m, payer, mine);
        w.WorldFlags.Remove(OfferedFlag);
        if (mine != null && Waiting(w, mine).Any()) w.WorldFlags.Add(OfferedFlag);
    }

    /// <summary>What waits on the player's answer.</summary>
    public static IEnumerable<GoldOffer> Waiting(World w, string houseId) => w.GoldOffers.Where(o => o.HouseId == houseId && o.Answer.Length == 0 && !o.Settled);
    public static GoldOffer? WaitingOn(World w, string motionId, string houseId) => Waiting(w, houseId).FirstOrDefault(o => o.MotionId == motionId);

    /// <summary>The nation writes to the senators about one war motion: the mover first, if the war would carry and it has not bought a war
    /// off lately; then the heaviest who lean for it or would stay away, until the count as it reckons it turns, and the player's house
    /// whenever it is not already against the war; but to no senator at all if every one of them taking it would not turn the count.</summary>
    private static void Write(World w, NationCatalog catalog, SenateSystem senate, Motion m, Nation payer, string? mine)
    {
        var (f, a) = senate.Tally(w, m);
        if ((a - f) * HopelessPart >= Math.Max(1, f + a)) return;   // it fails as the benches lean, and by enough: nothing to buy
        string warOn = m.TargetId; int sent = w.GoldOffers.Count;
        // the mover, sparingly: a sum to withdraw the motion before it is voted
        if (f > a && (payer.BoughtOffTurn == 0 || w.Turn - payer.BoughtOffTurn >= BuyOffEvery) && w.House(m.ProposerHouseId) is { } mover && w.OfTheReach(mover.Id)
            && Economy.HeadOf(w, mover) is { Alive: true } moverHead && !Shut(w, mover.Id))
        {
            int sum = SumFor(Withdraw, senate.Weight(w, mover.Id));
            if (payer.Purse >= sum)
            {
                var o = Send(w, catalog, payer, warOn, m, mover, moverHead, Withdraw, sum, 1, senate.Weight(w, mover.Id)); Count(w, WithdrawOffersKey); Count(w, MotionsKey);
                if (mover.Id == mine) Told(w, o);
                else { Answer(w, o, ByNature(moverHead, mover)); if (o.Answer is Taken or Exposed) return; }   // withdrawn; or the factor is known, and writes to nobody else
            }
        }
        // the senators: the heaviest who lean for it or would stay away, until the count as they reckon it turns, and the player's house;
        // and nobody, if all of them together could not turn it (the purse is kept for a war that can be stopped)
        var plan = new List<(House h, Character head, int stance, int seats, string ask, int sum)>(); int left = payer.Purse, reached = 0;
        foreach (var h in w.Houses.Values.Where(h => h.Id != m.ProposerHouseId && w.OfTheReach(h.Id) && PolitySystem.InReach(w, h.Id) && !Shut(w, h.Id))
                     .OrderByDescending(h => senate.Weight(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            bool theirs = h.Id == mine; if (!theirs && (reached >= MostReached || f < a || m.Votes.ContainsKey(h.Id))) continue;   // a house that has given its word on the vote is not written to
            if (theirs && DealingsSystem.PledgeOn(w, h.Id, m.Id) != null) continue;   // nor the player's, whose vote on it is pledged or sold: taking it would break that word
            if (Economy.HeadOf(w, h) is not { Alive: true } head) continue;
            int stance = senate.Stance(w, m, h.Id); if (stance < 0) continue;
            int seats = senate.Weight(w, h.Id); int lean = senate.Lean(w, m, h.Id);
            string ask = stance > 0 && lean > AwayAbove ? Away : Against; int sum = SumFor(ask, seats);
            if (left < sum) continue;
            plan.Add((h, head, stance, seats, ask, sum)); left -= sum; if (!theirs) reached++;
            if (stance > 0) f -= seats; if (ask == Against) a += seats;   // as they reckon it, before anyone has answered
        }
        if (f >= a || plan.Count == 0) return;
        bool playerAsked = false; if (w.GoldOffers.Count == sent) Count(w, MotionsKey);
        foreach (var (h, head, stance, seats, ask, sum) in plan)
        {
            var o = Send(w, catalog, payer, warOn, m, h, head, ask, sum, stance, seats);
            if (h.Id == mine) { playerAsked = true; Told(w, o); } else Answer(w, o, ByNature(head, h));
        }
        if (playerAsked || reached > 0) w.Log($"Letters from {Name(catalog, payer.Id)} have reached {(reached + (playerAsked ? 1 : 0) == 1 ? "a senator" : "senators")} about the war on {Name(catalog, warOn)}. What was in them is not said.");
    }

    private static GoldOffer Send(World w, NationCatalog catalog, Nation payer, string warOn, Motion m, House h, Character head, string ask, int sum, int before, int seats)
    {
        payer.Purse -= sum;   // set aside until it is answered
        var o = new GoldOffer { Id = $"gold_{w.Turn}_{m.Id}_{h.Id}", NationId = payer.Id, WarOnId = warOn, NationName = Name(catalog, payer.Id), WarOnName = Name(catalog, warOn), MotionId = m.Id, HouseId = h.Id, CharacterId = head.Id, Ask = ask, Sum = sum, Turn = w.Turn, Before = Math.Sign(before), Seats = seats };
        w.GoldOffers.Add(o); Count(w, OffersKey); return o;
    }

    private static void Told(World w, GoldOffer o) =>
        w.Tell("warn", $"A factor of {o.NationName} has called with {Money.Credits(o.Sum)} for you, {AskSays(o)}. Nothing is written down. You may take it, refuse it, or tell the Senate.", "senate:chamber", o.HouseId);

    /// <summary>How the head of another house answers, by nature: the upright tell the chamber; one for sale by nature (the natures a praetor
    /// is bought by, and the greedy), or whose house is in want (under 500 credits, when a lender charges it twenty in the hundred), takes it;
    /// anyone else refuses it, and says nothing.</summary>
    public static string ByNature(Character head, House house)
    {
        if (head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just")) return Expose;
        if (ForSale(head) || InWant(house)) return Take;
        return Refuse;
    }
    public static bool InWant(House house) => house.Treasury.Credits < DealingsSystem.PinchedAt;
    public static bool ForSale(Character c) => c.NatureTraits.Contains("Ruthless") || c.NatureTraits.Contains("Cruel") || c.AptitudeTraits.Contains("Greedy") || c.Ambition == "amass_a_fortune";

    // ---------------- an answer ----------------

    /// <summary>Why the player may not give this answer to this offer, or null.</summary>
    public static string? AnswerRefusal(World w, Character? me, GoldOffer? o, string answer)
    {
        var house = me != null ? w.House(me.HouseId) : null;
        if (me?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != me.Id) return "only the head of the house answers for it";
        if (o == null) return "nothing was sent";
        if (o.HouseId != house.Id) return "it was not sent to you";
        if (o.Answer.Length > 0 || o.Settled) return "it has been answered";
        if (w.IsImperium) return "a throne stands now, and the Cities treat with it, not with the benches";
        if (!Answers.Contains(answer)) return "take it, refuse it, or tell the Senate";
        if (w.Motions.FirstOrDefault(x => x.Id == o.MotionId) is not { Open: true }) return "the vote it was sent for is over";
        // the house's vote on it is pledged or sold another way: taking the gold would turn it, and break that word
        if (answer == Take && o.Ask != Withdraw && DealingsSystem.PledgeOn(w, house.Id, o.MotionId) is { } p && p.Side != (o.Ask == Against ? -1 : 0))
            return $"your vote on it is {(p.Paid ? "bought by" : "pledged to")} {w.House(p.ToHouseId)?.Name ?? "another house"}, {(p.Side > 0 ? "for" : "against")} it: take this and you break that word (its head would think {-DealingsSystem.PledgeBrokenRegard:+0;-0} of you)";
        return null;
    }

    /// <summary>The answer is given, by the player or by another house's head. It is the house's head now who answers, and whose it is: the one
    /// first asked may have died or yielded the seat since (an offer to the player waits a year, and a seat can pass in the same year's end).</summary>
    public static void Answer(World w, GoldOffer o, string answer)
    {
        var house = w.House(o.HouseId); var head = house != null && Economy.HeadOf(w, house) is { Alive: true } now ? now : null; var payer = w.Nation(o.NationId); var m = w.Motions.FirstOrDefault(x => x.Id == o.MotionId);
        if (house == null || head == null || payer == null) { o.Answer = Lapsed; o.Settled = true; if (payer != null) payer.Purse += o.Sum; return; }
        o.CharacterId = head.Id;
        bool ours = house.Id == w.Protagonist?.HouseId; string nation = o.NationName, warOn = o.WarOnName;
        if (answer == Take)
        {
            o.Answer = Taken; head.Purse += o.Sum; Count(w, TakenKey); w.WorldCounters[SpentKey] = w.Counter(SpentKey) + o.Sum;
            w.OfficeRecord.Add(new OfficeDeed { Id = "deed_" + o.Id, CharacterId = head.Id, HouseId = house.Id, RungId = "", Turn = w.Turn, What = Deed, AgainstId = "", Answered = false });
            LifeSystem.Note(w, head, "office", $"Took {Money.Credits(o.Sum)} from {nation} {AskSays(o)}.");
            if (m != null)
            {
                if (o.Ask == Withdraw) { Withdrawn(w, m, o); return; }
                m.Bought.TryAdd(house.Id, 0);   // a bench does not follow a leader who was paid, whoever paid him (SenateSystem.LeaderStance)
                if (ours) m.Votes[house.Id] = o.Ask == Against ? -1 : 0;
            }
            if (ours) { w.Log($"{head.Name} takes what {nation} sent. It is not spoken of."); VoteMarks.Wanted(w, house.Id); }   // the vote is held by the gold: no report asks it of the house again
            // the reminder before the count of what the gold binds the house to, in the words the button gave (TakeSays)
            if (ours && m != null) w.Tell("warn", $"You took what {nation} sent: your house is declared {(o.Ask == Against ? "against the war on " + warOn : "to stay away from the vote on the war on " + warOn)}. Vote for the war after all and they send your house nothing for {Counted.Years(BrokenYears)}.", "senate:chamber", house.Id);
            return;
        }
        payer.Purse += o.Sum;
        if (answer == Expose)
        {
            o.Answer = Exposed; Count(w, ExposedKey);
            LadderSystem.GoldShown(w, ExposeTrust); LegitimacyBook.Move(w, house, ExposeLegitimacy, LegitimacyBook.Senate, $"Told the Senate of the gold {nation} sent");
            payer.Disposition = Math.Clamp(payer.Disposition - ExposeRegard, -100, 100);
            LifeSystem.Note(w, head, "office", $"Told the Senate what {nation} had sent to buy a vote on the war on {warOn}.");
            w.Log($"{head.Name} of {house.Name} stands in the Senate and says what {nation} sent, and for what. The chamber is angry, and thinks the less of {nation} for it.");
            if (!ours && w.Protagonist != null) w.Tell("info", $"{head.Name} of {house.Name} has told the Senate of the gold {nation} sent about the war on {warOn}. The chamber leans the more for the war.", "senate:chamber", house.Id);
            return;
        }
        o.Answer = Refused; Count(w, RefusedKey);
        if (ours) w.Log($"{head.Name} sends the factor of {nation} away with what they brought.");
    }

    /// <summary>The mover took the sum: the motion is taken back before it is voted.</summary>
    private static void Withdrawn(World w, Motion m, GoldOffer o)
    {
        DealingsSystem.Struck(w, m);   // a word given on it is void, and a favour owed its mover waits (docs/WEALTH.md)
        w.Motions.Remove(m); Count(w, BoughtOffKey);
        if (w.Nation(o.NationId) is { } payer) payer.BoughtOffTurn = w.Turn;
        string key = "senate_standing:" + m.ProposerHouseId; w.WorldCounters[key] = Math.Max(0, w.Counter(key) - WithdrawStanding);
        foreach (var x in w.GoldOffers.Where(x => x.MotionId == m.Id)) Settle(w, x, turned: true);
        if (!SenateSystem.Docket(w).Any()) w.WorldFlags.Remove("motion_open");
        w.Log($"{w.House(m.ProposerHouseId)?.Name ?? "Its mover"} withdraws the motion of war on {o.WarOnName} before it is voted. No reason is given.");
    }

    // ---------------- the count ----------------

    /// <summary>A motion the gold was sent about has been counted (or vetoed, or is gone): what was waiting lapses, a promise broken is
    /// noticed, and the war vote is marked turned if it failed and would have carried had those who took the gold voted as they leaned.</summary>
    private static void SettleCounted(World w, SenateSystem senate, string? mine)
    {
        foreach (var group in w.GoldOffers.Where(o => !o.Settled).GroupBy(o => o.MotionId).ToList())
        {
            var m = w.Motions.FirstOrDefault(x => x.Id == group.Key); if (m is { Open: true }) continue;
            int f = m?.ForWeight ?? 0, a = m?.AgainstWeight ?? 0;
            foreach (var o in group.Where(o => o.Answer == Taken && o.Ask != Withdraw))
            {
                if (m == null || !m.Votes.TryGetValue(o.HouseId, out int v)) continue;   // it did not vote at all: nothing to say of it
                // vetoed before the count: no vote was cast, so none was broken, whatever was declared
                if (m.Vetoed) { if (o.HouseId == mine) w.Tell("info", $"The war on {o.WarOnName} was vetoed before the count, and no vote was cast: nothing is asked of your house for the gold.", "senate:chamber"); continue; }
                bool kept = v <= 0;   // only a vote for the war breaks it, as TakeSays tells it: one paid to vote against that stayed away did not vote for the war
                if (!kept)
                {
                    o.Broken = true; Count(w, BrokenKey); w.WorldCounters[BrokenKeyFor(o.HouseId)] = w.Turn + BrokenYears;
                    if (w.Nation(o.NationId) is { } payer) payer.Disposition = Math.Clamp(payer.Disposition - BrokenRegard, -100, 100);
                    if (o.HouseId == mine) w.Tell("warn", $"You took the gold from beyond the Reach and voted for the war all the same. They send your house nothing for {Counted.Years(BrokenYears)}, and the gold is still on the record.", "senate:chamber");
                    continue;
                }
                if (v > 0) f -= o.Seats; else if (v < 0) a -= o.Seats;
                if (o.Before > 0) f += o.Seats; else if (o.Before < 0) a += o.Seats;
            }
            bool turned = m != null && m.Resolved && !m.Passed && !m.Vetoed && f > a && group.Any(o => o.Answer == Taken && !o.Broken);
            if (turned) { Count(w, TurnedKey); w.Log($"The war is thrown out, and it is said in the lobbies that the vote was not all it seemed."); }
            foreach (var o in group) Settle(w, o, turned && o.Answer == Taken && !o.Broken);
        }
    }

    private static void Settle(World w, GoldOffer o, bool turned)
    {
        if (o.Settled) return;
        if (o.Answer.Length == 0) { o.Answer = Lapsed; Count(w, LapsedKey); if (w.Nation(o.NationId) is { } payer) payer.Purse += o.Sum; }
        o.Settled = true; o.Turned = turned;
    }

    // ---------------- the vote ----------------

    /// <summary>What the gold does to a house's lean on a war motion (SenateSystem.LeanNow): a house that took it to vote against votes
    /// against, one that took it to stay away stays away; and once gold has been shown to the chamber, every house leans the more for the war.
    /// <paramref name="lean"/> is its lean before this. The player's house votes as the player declares.</summary>
    public static int Lean(World w, Motion m, string houseId, int lean, out string why)
    {
        why = ""; if (w.GoldOffers.Count == 0) return 0;
        int by = 0;
        foreach (var o in w.GoldOffers) if (o.MotionId == m.Id && o.Answer == Exposed) { by = ExposeAnger; why = "gold from beyond the Reach was shown to the chamber"; break; }
        if (houseId == w.Protagonist?.HouseId) return by;
        foreach (var o in w.GoldOffers)
        {
            if (o.MotionId != m.Id || o.HouseId != houseId || o.Answer != Taken || o.Ask == Withdraw) continue;
            int now = lean + by;
            int to = o.Ask == Against ? Math.Min(0, -1 - now) : -now;
            why = o.Ask == Against ? "it has taken gold to vote against the war" : "it has taken gold to stay away";
            return by + to;
        }
        return by;
    }

    // ---------------- what is known, and what is said ----------------

    /// <summary>The senators the player knows the nation reached on this motion: their own house (a sum to withdraw the motion they moved,
    /// too), any who told the chamber, any who took it (the roll call gives it as the reason for their vote, and the courts list it on the
    /// record), and those whose head thinks well enough of the player to tell them. One who refused it and said nothing is not known.</summary>
    public static List<GoldOffer> Known(World w, string motionId, string? houseId)
    {
        var me = w.Protagonist;
        return w.GoldOffers.Where(o => o.MotionId == motionId && (o.HouseId == houseId || o.Answer is Exposed or Taken
                || (me != null && w.Char(o.CharacterId) is { Alive: true } c && (w.GetRelationship(c.Id, me.Id)?.Disposition ?? 0) >= FriendTells)))
            .OrderBy(o => o.HouseId == houseId ? 0 : 1).ThenBy(o => o.HouseId, StringComparer.Ordinal).ToList();
    }

    /// <summary>The line on the motion of what is known (<see cref="Known"/>): "As far as you know, the Free Cities of Thessar have reached
    /// 2 senators on this motion: your own house; House Aurelian, which took it."</summary>
    public static string KnownSays(World w, List<GoldOffer> known, string payer, string? mine)
    {
        if (known.Count == 0) return $"No senator is known to have had gold from {payer} on this motion. They write to senators the year a war on them, or on a nation they favour, is moved, if it would carry or nearly and their purse can pay what they would offer.";
        static string Did(GoldOffer o) => o.Answer switch { Exposed => "told the chamber", Taken => "took it", Refused => "refused it", _ => "" };
        var names = known.Select(o => o.HouseId == mine ? "your own house" + (o.Ask == Withdraw ? ", to withdraw it" : string.Empty)
            : (w.House(o.HouseId)?.Name ?? o.HouseId) + (o.Ask == Withdraw ? ", which moved it" + (Did(o).Length > 0 ? " and " + Did(o) : string.Empty) : Did(o).Length > 0 ? ", which " + Did(o) : string.Empty));
        return $"As far as you know, {payer} have reached {Counted.Of(known.Count, "senator")} on this motion: {string.Join("; ", names)}. There may be others: one who refused it and said nothing is not known.";
    }

    /// <summary>What the sum was for, in words: "to vote against the war on the Hollow Crown".</summary>
    /// <summary>What taking the sum does, for the button's title: the purse, the vote it asks, the record, and (unless it buys the motion off,
    /// which leaves no vote to break it at) what voting for the war after all would cost.</summary>
    public static string TakeSays(GoldOffer o) =>
        $"{Money.Credits(o.Sum)} into your own purse. {(o.Ask == Withdraw ? "Your motion is withdrawn at once." : o.Ask == Against ? "Your house is declared against the war." : "Your house is declared to stay away.")} It goes on the record: any house may charge you with it before a praetor, at once and for {Counted.Years(LadderSystem.RecordYears)}."
        + (o.Ask == Withdraw ? "" : $" Vote for the war after all and they send your house nothing for {Counted.Years(BrokenYears)}.");
    public static string AskSays(GoldOffer o) => o.Ask switch
    {
        Against => $"to vote against the war on {o.WarOnName}",
        Away => $"to stay away from the vote on the war on {o.WarOnName}",
        _ => $"to withdraw the motion of war on {o.WarOnName}"
    };

    /// <summary>What the record says of it, for the courts: "took 3,800 credits from the Free Cities of Thessar to vote against the war on the Hollow Crown in year 34".</summary>
    public static string DeedSays(World w, OfficeDeed d)
    {
        var o = w.GoldOffers.FirstOrDefault(x => "deed_" + x.Id == d.Id);
        return o == null ? $"took gold from beyond the Reach for a vote in the Senate in year {d.Turn}" : $"took {Money.Credits(o.Sum)} from {o.NationName} {AskSays(o)} in year {d.Turn}";
    }

    /// <summary>The house took the gold and voted against what it was for: nothing more is sent to it for some years.</summary>
    public static bool Shut(World w, string houseId) => w.Counter(BrokenKeyFor(houseId)) > w.Turn;

    /// <summary>A nation's name as it is written in the middle of a sentence: "the Free Cities of Thessar".</summary>
    public static string Name(NationCatalog catalog, string id) => catalog.Def(id)?.Name is { } n ? (n.StartsWith("The ", StringComparison.Ordinal) ? "the " + n[4..] : n) : id;
    private static void Count(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
}
