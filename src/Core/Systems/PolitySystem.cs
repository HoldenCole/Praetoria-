using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Breaking away (docs/BREAKAWAY.md). A house that controls a region, by its own worlds and those of the houses there that
/// would follow it, may leave the Reach and found a state of its own: a republic, a dictatorship or a kingdom. What the
/// Reach does about it depends on its temper. One that has just fought a great war, or that faces a breakaway as strong as
/// itself, or one far out on the rim, lets it go. Otherwise there is a <b>war of independence</b>, and it has no fixed end:
/// the state is recognised when it has held its region long enough and the Reach has tired or been beaten, and it is brought
/// back when it has lost the region or the field. A house may declare with nothing but its own fleet and fight for as long
/// as it can. A state can also come out of the great war by partition, and out of a new throne by houses that will not kneel.
/// </summary>
public sealed class PolitySystem
{
    public const string Republic = "republic", Dictatorship = "dictatorship", Kingdom = "kingdom";
    public static readonly string[] Forms = { Republic, Dictatorship, Kingdom };
    public const int OwnShare = 25, ControlShare = 60, HoldShare = 50, LostShare = 25, WearyYears = 8, WinYears = 3, WinLead = 6, LoseLead = -8, TemperFallsEvery = 3, LeverageFallsEvery = 2, RefuseYears = 3,
        TreatyYears = 10, JoinRegard = 25, WordInfluence = 2, WordRegard = 8, GiftCredits = 12, GiftRegard = 12, PledgeYears = 10, LoyalRegard = 30;
    public const string Word = "word", Gift = "gift", ReportedFlag = "sedition_reported", WonOverFlag = "house_won_over", ReconquestFlag = "reconquest_begun", RejoinedFlag = "rejoined_the_reach";
    public static string PledgeKey(string houseId, string leaderId) => $"pledged:{houseId}:{leaderId}";
    private static string WorkedKey(string houseId, string leaderId) => $"worked:{houseId}:{leaderId}";
    // true each year while it holds
    public const string IndependentFlag = "independent", WarFlag = "war_of_independence", PossibleFlag = "breakaway_possible", RefuseFlag = "may_refuse_the_throne";
    // true for a year after the deed
    public const string DeclaredFlag = "declared_independence", LetGoFlag = "let_go_by_the_reach", WonFlag = "independence_won", LostFlag = "independence_lost",
        OtherBreaksFlag = "a_state_breaks_away", PartitionFlag = "the_reach_is_partitioned", JoinedFlag = "joined_a_state", JoinedOursFlag = "a_house_joins_our_state", BreachFlag = "treaty_broken_by_accession", StirredFlag = "camps_stirred";
    public const string Promise = "promise", Threat = "threat", LeftFlag = "left_a_state", LeftOursFlag = "a_house_leaves_our_state", UnpaidFlag = "promise_unpaid";
    public const int PromiseRegard = 15, PromiseCredits = 3, ThreatRegard = -20, ThreatOdds = 3, MarriageEases = 15, LeavesRegard = -20, NewThroneYears = 8, StirAgainst = 8;
    public static string PromisedKey(string houseId, string leaderId) => $"promised:{houseId}:{leaderId}";
    public static string CowedKey(string houseId, string leaderId) => $"cowed:{houseId}:{leaderId}";
    /// <summary>Has this house been promised a place by that one, and not yet had it?</summary>
    public static bool IsPromised(World w, House h, House leader) => w.WorldCounters.ContainsKey(PromisedKey(h.Id, leader.Id));
    /// <summary>Is this house afraid of that one: threatened inside the last ten years, and still much the weaker?</summary>
    public static bool Cowed(World w, House h, House leader) => w.WorldCounters.TryGetValue(CowedKey(h.Id, leader.Id), out int t) && w.Turn - t <= PledgeYears
        && FleetSystem.StrengthOf(w, leader.Id) >= 2 * Math.Max(1, FleetSystem.StrengthOf(w, h.Id));
    /// <summary>Are the two houses bound by a marriage: one of each, living, wed to the other?</summary>
    public static bool Bound(World w, House a, House b) => w.Marriages.Any(m => w.Char(m.A) is { Alive: true } x && w.Char(m.B) is { Alive: true } y
        && ((Of_(x) == a.Id && Of_(y) == b.Id) || (Of_(x) == b.Id && Of_(y) == a.Id)));
    private static string Of_(Character c) => c.BornHouseId.Length > 0 ? c.BornHouseId : c.HouseId;
    public const string StirKey = "camps_stirred_until"; public const int StirYears = 3, StirInfluence = 3, StirCredits = 12, AccedeRegard = 0, GoesOverRegard = 30;
    private static readonly string[] YearFlags = { IndependentFlag, WarFlag, PossibleFlag, RefuseFlag };
    private static readonly string[] DeedFlags = { DeclaredFlag, LetGoFlag, WonFlag, LostFlag, OtherBreaksFlag, PartitionFlag, ReportedFlag, WonOverFlag, ReconquestFlag, RejoinedFlag, JoinedFlag, JoinedOursFlag, BreachFlag, StirredFlag, LeftFlag, LeftOursFlag, UnpaidFlag, WarOverWordsFlag };
    public const string CivilWarEndedKey = "civil_war_ended";
    public static string OpposedKey(string houseId) => "opposed_the_throne:" + houseId;

    private readonly FleetSystem _fleets; private readonly OfficeCatalog _offices; private readonly Economy? _economy; private readonly HoldingCatalog? _holdings; private readonly BusinessCatalog? _ventures;
    public PolitySystem(FleetSystem fleets, OfficeCatalog offices, Economy? economy = null, HoldingCatalog? holdings = null, BusinessCatalog? ventures = null)
    { _fleets = fleets; _offices = offices; _economy = economy; _holdings = holdings; _ventures = ventures; }

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static bool Within(World w, string key, int years) => w.WorldCounters.TryGetValue(key, out int t) && w.Turn - t <= years;
    private static bool Real(House h) => !World.Outsider(h.Id);

    /// <summary>Is this house still of the Reach: in its Senate, under its laws, in its wars?</summary>
    public static bool InReach(World w, string houseId) => w.House(houseId) is { PolityId.Length: 0 } && !w.Foreign(houseId);
    public static Polity? Of(World w, string houseId) => w.House(houseId) is { PolityId.Length: > 0 } h ? w.Polities.FirstOrDefault(p => p.Id == h.PolityId) : null;
    public static Polity? OfRegion(World w, string regionId) => w.Polities.FirstOrDefault(p => p.Regions.Contains(regionId));

    public static string FormName(string form) => form switch { Dictatorship => "a republic under one hand", Kingdom => "a kingdom", _ => "a republic" };
    public static string RulerStyle(string form, Character? ruler) => form switch { Dictatorship => "Dictator", Kingdom => ruler?.Sex == "female" ? "Queen" : "King", _ => "First Citizen" };
    public static string NameFor(string form, string regionName)
    {
        string r = regionName.StartsWith("The ", StringComparison.Ordinal) ? regionName[4..] : regionName;
        return form switch { Dictatorship => $"the Protectorate of {r}", Kingdom => $"the Kingdom of {r}", _ => $"the Free State of {r}" };
    }

    // ---------------- who holds a region ----------------

    private static List<Holding> Worlds(World w, string regionId) =>
        w.Galaxy == null ? new() : w.Holdings.Values.Where(h => h.IsPlanet && w.Galaxy.System(h.SystemId)?.RegionId == regionId).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    /// <summary>Would this house of the region go with the one that leads it out? A client does; so does one whose head thinks well of
    /// the leader's, or that depends on the leader's house. The house that holds the capital never does.</summary>
    public static bool Joins(World w, House h, House leader)
    {
        if (h.Id == leader.Id || !Real(h) || !InReach(w, h.Id) || RegionSystem.CapitalHolder(w)?.Id == h.Id) return false;
        if (h.PatronId == leader.Id) return true;
        if (Cowed(w, h, leader)) return true;                                    // it is afraid to do otherwise
        int regard = Regard(w, h, leader);
        if (regard >= JoinRegard - (Bound(w, h, leader) ? MarriageEases : 0)) return true;   // a marriage between the houses makes it easier asked
        if (regard >= 0 && w.WorldCounters.TryGetValue(PledgeKey(h.Id, leader.Id), out int pledged) && w.Turn - pledged <= PledgeYears) return true;   // it gave its word
        return OrderSystem.Sway(w, h, leader).lean >= 2;
    }

    /// <summary>What the head of one house thinks of the head of another.</summary>
    public static int Regard(World w, House of, House toward)
    {
        var a = Systems.Economy.HeadOf(w, of); var b = Systems.Economy.HeadOf(w, toward);
        return a != null && b != null ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0;
    }

    // ---------------- working on a region's houses ----------------

    /// <summary>The other houses that hold a world in a region this house holds a world in: the ones it might bring over.</summary>
    public static List<House> Neighbours(World w, House house) =>
        RegionsOf(w, house).SelectMany(r => Worlds(w, r.Id)).Select(h => h.OwnerId).Distinct().Where(id => id != house.Id).Select(id => w.House(id)).Where(h => h != null && Real(h) && InReach(w, h.Id)).Select(h => h!).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();

    public string? WorkRefusal(World w, House? house, House? target, string how)
    {
        if (house == null || target == null || !Real(target) || house.Id == target.Id) return "no such house";
        if (how != Word && how != Gift && how != Promise && how != Threat) return "a word, a gift, a promise or a threat";
        if (!InReach(w, target.Id)) return $"{target.Name} has already left the Reach";
        if (!InReach(w, house.Id))
        {
            // a state reaches for its neighbours: its ruler may court a house of the Reach that holds worlds in the state or beside it
            var own = Of(w, house.Id); if (own == null || own.HeadHouseId != house.Id) return "the house has already left the Reach";
            if (how is Promise or Threat) return "a state courts its neighbours with words and gifts";
            if (!Courtable(w, own, target)) return $"{target.Name} holds no world in {own.Name} or beside it";
        }
        else if (!Neighbours(w, house).Any(h => h.Id == target.Id)) return $"{target.Name} holds no world in a region of yours";
        if (how == Promise && IsPromised(w, target, house)) return "you have promised them a place already";
        if (how == Threat && FleetSystem.StrengthOf(w, house.Id) < ThreatOdds * Math.Max(1, FleetSystem.StrengthOf(w, target.Id))) return $"they are not afraid of you: it takes {ThreatOdds} hulls to their one";
        if (how == Threat && Cowed(w, target, house)) return "they are afraid of you already";
        if (RegionSystem.CapitalHolder(w)?.Id == target.Id) return $"{target.Name} holds the capital, and will never leave it";
        if (w.Counter(WorkedKey(target.Id, house.Id)) == w.Turn + 1) return "you have approached them this year";
        return how == Gift && house.Treasury.Credits < GiftCredits ? $"a gift takes {GiftCredits} credits" : null;
    }

    /// <summary>A house of the Reach a state's ruler might court: one that holds a world in the state's regions, or in a region that borders them.</summary>
    public static bool Courtable(World w, Polity p, House target) => Real(target) && InReach(w, target.Id) && w.Galaxy != null && w.Galaxy.ReachRegions.Any(r =>
        (p.Regions.Contains(r.Id) || p.Regions.Any(x => Borders(w, x, r.Id))) && Worlds(w, r.Id).Any(x => x.OwnerId == target.Id));

    /// <summary>The head sounds out a house of the region: a word, or a gift. One that thinks well enough of the house afterwards gives
    /// its word to go with it. One that is the capital's creature tells the capital. True if it was won over.</summary>
    public bool Work(World w, House house, House target, string how)
    {
        w.WorldCounters[WorkedKey(target.Id, house.Id)] = w.Turn + 1; var chair = RegionSystem.CapitalHolder(w);
        if (how == Gift) house.Treasury.Credits -= GiftCredits;
        var mine = w.Protagonist?.HouseId; var th = Systems.Economy.HeadOf(w, target); var hh = Systems.Economy.HeadOf(w, house);
        // a house that is the capital's creature and has no love for this one reports the approach
        if (chair != null && Regard(w, target, chair) >= LoyalRegard && Regard(w, target, house) < 10)
        {
            if (Systems.Economy.HeadOf(w, chair) is { } ch && hh != null) { var r = w.Relationship(ch.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition - 15, -100, 100); }
            house.Legitimacy = Math.Clamp(house.Legitimacy - 2, 0, 100);
            if (house.Id == mine) Mark(w, ReportedFlag);
            w.Log($"{target.Name} hears {house.Name} out, and sends an account of the conversation to {chair.Name} the same night.");
            return false;
        }
        if (th != null && hh != null)
        {
            int gain = how == Gift ? GiftRegard + (target.Treasury.Credits < 20 ? 4 : 0) : how == Promise ? PromiseRegard : how == Threat ? ThreatRegard : WordRegard;
            var r = w.Relationship(th.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition + gain, -100, 100);
        }
        if (how == Promise) { w.WorldCounters[PromisedKey(target.Id, house.Id)] = w.Turn; w.Log($"{house.Name} promises {target.Name} a place in whatever comes after. It is a debt, and both of them know it."); }
        if (how == Threat)
        {
            w.WorldCounters[CowedKey(target.Id, house.Id)] = w.Turn;
            w.Log($"{house.Name} lets {target.Name} understand what its fleets could do. {target.Name} will go where it is told, and will not forget being told."); return false;
        }
        bool won = Regard(w, target, house) >= JoinRegard && !w.WorldCounters.ContainsKey(PledgeKey(target.Id, house.Id));
        if (Regard(w, target, house) >= JoinRegard) w.WorldCounters[PledgeKey(target.Id, house.Id)] = w.Turn;
        if (won && house.Id == mine) Mark(w, WonOverFlag);
        w.Log(won ? $"{target.Name} gives {house.Name} its word: if the region goes its own way, it goes too."
                  : how == Gift ? $"{house.Name} sends {target.Name} a gift that asks for nothing. {target.Name} thinks the better of it." : $"{house.Name} has a quiet word with {target.Name} about how things are governed from the capital.");
        return won;
    }

    // ---------------- standing alone ----------------

    private HashSet<string> ProducersOf(World w, string resource)
    {
        var set = new HashSet<string>(); var g = w.Galaxy; if (g == null || _holdings == null || _ventures == null) return set;
        foreach (var h in w.Holdings.Values)
        {
            if (g.System(h.SystemId)?.Deposit(resource) is not > 0) continue;
            if (resource == Resource.RareEarths && h.IsClaim) set.Add(h.OwnerId);
            if (h.Buildings.Any(b => _holdings.Building(b)?.RequiresDeposit == resource)) set.Add(h.OwnerId);
        }
        foreach (var b in w.Businesses.Values) if (_ventures.Def(b.Kind)?.RequiresDeposit == resource && g.System(b.SystemId)?.Deposit(resource) is > 0) set.Add(b.OwnerId);
        set.RemoveWhere(id => World.Outsider(id) || !InReach(w, id));
        return set;
    }

    /// <summary>Could the houses that would go stand by themselves? Three questions, each with its answer, and what the answers do to the
    /// Reach's temper: fuel of its own; a treasury that pays its way; and something the Reach cannot do without.</summary>
    public (List<(string name, bool ok, string says)> tests, int temper, bool leverage, bool starved) StandAlone(World w, House house, string regionId)
    {
        var tests = new List<(string, bool, string)>(); var (_, _, _, backers) = Control(w, house, regionId);
        var gone = backers.Select(b => b.Id).Append(house.Id).ToHashSet(); int temper = 0; bool leverage = false;
        var fuel = ProducersOf(w, Resource.Helium3); int ourFuel = fuel.Count(gone.Contains);
        bool hasFuel = ourFuel > 0 || _holdings == null;
        tests.Add(("Fuel of its own", hasFuel, hasFuel ? "its houses bring up their own helium-3" : "none of its houses brings up helium-3: at war the Reach closes its exchanges and the fleets ride at anchor"));
        if (!hasFuel) temper += 1;
        bool pays = true; string paysSays = "its houses pay their way";
        if (_economy != null)
        {
            int net = gone.Sum(id => _economy.Forecast(w, id, _fleets).Credits), held = gone.Sum(id => w.House(id)?.Treasury.Credits ?? 0);
            pays = net >= 0 && held >= 0; paysSays = pays ? $"its houses take in {net} credits a year more than they spend" : net < 0 ? $"its houses spend {-net} credits a year more than they take in" : "its houses are in debt";
        }
        tests.Add(("A treasury that pays its way", pays, paysSays)); if (!pays) temper += 1;
        var held2 = new List<string>(); bool other = false;
        foreach (var res in Resource.Strategic)
        {
            var p = res == Resource.Helium3 ? fuel : ProducersOf(w, res); int ours = p.Count(gone.Contains);
            if (ours == 0 || ours * 2 < p.Count) continue;
            leverage = true;
            if (res == Resource.Helium3) { held2.Add("helium-3"); temper -= 2; }       // the Reach's fleets burn it
            else { held2.Add(res == Resource.RareEarths ? "rare earths" : "isotopes"); if (!other) { temper -= 1; other = true; } }
        }
        tests.Add(("Something the Reach cannot do without", leverage, leverage ? $"half or more of the Reach's {string.Join(" and ", held2)} comes from its houses" : "nothing the Reach could not get elsewhere"));
        return (tests, temper, leverage, !hasFuel);
    }

    /// <summary>The region's worlds: how many are the house's own, how many with those who would follow, of how many; and who would follow.</summary>
    public static (int own, int with, int total, List<House> backers) Control(World w, House house, string regionId)
    {
        var worlds = Worlds(w, regionId); int own = worlds.Count(h => h.OwnerId == house.Id);
        var backers = worlds.Select(h => h.OwnerId).Distinct().Select(id => w.House(id)).Where(h => h != null && Joins(w, h, house)).Select(h => h!).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        return (own, own + worlds.Count(h => backers.Any(b => b.Id == h.OwnerId)), worlds.Count, backers);
    }

    /// <summary>Regions the house holds a world in, the most held first.</summary>
    public static List<Region> RegionsOf(World w, House house) =>
        w.Galaxy == null ? new() : w.Galaxy.ReachRegions.Where(r => Worlds(w, r.Id).Any(h => h.OwnerId == house.Id)).OrderByDescending(r => Worlds(w, r.Id).Count(h => h.OwnerId == house.Id)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();

    // ---------------- the Reach's temper ----------------

    public static bool Weary(World w) => CaesarSystem.GreatWar(w) || w.CivilWar != null || Within(w, CaesarSystem.GreatWarEndedKey, WearyYears) || Within(w, CivilWarEndedKey, WearyYears);

    /// <summary>How willing the Reach is to fight to keep this region: above nought it goes to war, at nought or below it lets it go.</summary>
    public (int temper, List<string> why) Temper(World w, House house, string regionId)
    {
        var why = new List<string>(); int t = 2; var (_, _, _, backers) = Control(w, house, regionId);
        if (Weary(w)) { t -= 2; why.Add("the Reach has just fought a great war and has no stomach for another"); }
        var gone = backers.Select(b => b.Id).Append(house.Id).ToHashSet(); var chair = RegionSystem.CapitalHolder(w);
        int ours = gone.Sum(id => FleetSystem.StrengthOf(w, id));
        int theirs = (chair != null && !gone.Contains(chair.Id) ? FleetSystem.StrengthOf(w, chair.Id) : 0)
            + w.Houses.Values.Where(h => Real(h) && InReach(w, h.Id) && !gone.Contains(h.Id) && h.Id != chair?.Id).Select(h => FleetSystem.StrengthOf(w, h.Id)).OrderByDescending(x => x).Take(3).Sum();
        if (ours * 2 >= theirs) { t -= 1; why.Add("the breakaway's fleets are a match for what would be sent"); }
        else if (ours * 4 < theirs) { t += 1; why.Add("the breakaway's fleets are few"); }
        string ring = w.Galaxy?.Region(regionId)?.Ring ?? Ring.Middle;
        if (ring == Ring.Rim) { t -= 1; why.Add("the rim is a long way to send a fleet"); }
        else if (ring == Ring.Core) { t += 1; why.Add("the core is too near the capital to let go"); }
        if (w.IsImperium) t += ThroneTemper(w, house, why);
        var alone = StandAlone(w, house, regionId); t += alone.temper;
        if (alone.leverage) why.Add("the Reach cannot do without what the region makes");
        if (alone.starved) why.Add("the region has no fuel of its own and can be starved of it");
        if (alone.tests.Count > 1 && !alone.tests[1].ok) why.Add("the region cannot pay for a war");
        return (t, why);
    }

    /// <summary>Under a throne the answer is the Emperor's own (docs/BREAKAWAY.md): a new throne cannot be seen to lose a province, a
    /// hard ruler will not, one who is not secure at home cannot spare the fleets, and one who thinks well of the house may let it go.</summary>
    public static int ThroneTemper(World w, House house, List<string> why)
    {
        var throne = ThroneSystem.Emperor(w); var emperor = throne != null ? Systems.Economy.HeadOf(w, throne) : null;
        if (throne == null || emperor == null) { why.Add("a throne does not let provinces go"); return 1; }
        int t = 0;
        if (w.WorldCounters.TryGetValue(ThroneSystem.SinceKey, out int since) && w.Turn - since <= NewThroneYears) { t += 2; why.Add($"{emperor.Name} is new on the throne and cannot be seen to lose a province"); }
        else { t += 1; why.Add("a throne does not let provinces go"); }
        if (emperor.NatureTraits.Contains("Ruthless") || emperor.NatureTraits.Contains("Cruel") || emperor.NatureTraits.Contains("Proud")) { t += 1; why.Add($"{emperor.Name} is not one to let it pass"); }
        else if (emperor.NatureTraits.Contains("Just") || emperor.NatureTraits.Contains("Honorable")) { t -= 1; why.Add($"{emperor.Name} would sooner treat than burn a region"); }
        if (throne.Legitimacy < 40) { t -= 1; why.Add("the throne is not secure enough at home to send its fleets away"); }
        int regard = Regard(w, throne, house);
        if (regard >= 30) { t -= 1; why.Add($"{emperor.Name} thinks well of the house"); } else if (regard <= -30) { t += 1; why.Add($"{emperor.Name} has no love for the house"); }
        return t;
    }

    // ---------------- declaring ----------------

    public string? DeclareRefusal(World w, House? house, string regionId, string form, bool march = false)
    {
        if (house == null || !Real(house)) return "no such house";
        if (!InReach(w, house.Id)) return "the house has already left the Reach";
        if (!Forms.Contains(form)) return "name a form of government";
        var region = w.Galaxy?.Region(regionId); if (region == null) return "no such region";
        if (OfRegion(w, regionId) != null) return $"{region.Name} has already left the Reach";
        if (RegionSystem.CapitalHolder(w)?.Id == house.Id) return "the house holds the capital: it cannot leave what it sits at the head of";
        if (w.Galaxy!.System(w.Galaxy.Capital)?.RegionId == regionId) return "the capital's own region cannot leave the Reach";
        if (w.CivilWar?.HouseId == house.Id) return "the house is marching on the capital";
        var (own, with, total, _) = Control(w, house, regionId);
        if (total == 0 || own == 0) return $"the house holds no world of {region.Name}";
        if (own * 100 < OwnShare * total) return $"the house holds {own} of the {total} worlds of {region.Name}, and must hold a quarter";
        return !march && with * 100 < ControlShare * total ? $"with the houses that would follow, {with} of the {total} worlds of {region.Name} would go: it takes three in five" : null;
    }

    // ---------------- the older rules, as states (docs/BREAKAWAY.md, "Free marches and free holds") ----------------

    public const string FreeMarchOrigin = "free march", FreeHoldOrigin = "free hold";
    public const int FreeHoldTemper = 1;
    public static bool IsFreeHold(Polity p) => p.Origin == FreeHoldOrigin;
    /// <summary>The form a house takes of its own choosing: a soldier's is one hand, an old and proud house's a crown, anyone else's a republic.</summary>
    public static string FormOf(World w, House house)
    {
        var head = Systems.Economy.HeadOf(w, house); if (head == null || house.Id == w.Protagonist?.HouseId) return Republic;
        return head.CareerTrack == "military" ? Dictatorship : head.NatureTraits.Contains("Proud") && house.Legitimacy >= 60 ? Kingdom : Republic;
    }
    /// <summary>A FREE MARCH: the dominant house of a region that has stopped believing in the capital takes the region out. It is a
    /// declaration like any other, and the Reach lets it go or comes for it, but it does not wait for three worlds in five to follow.</summary>
    public string? FreeMarchRefusal(World w, House? house, string regionId) => DeclareRefusal(w, house, regionId, Republic, march: true);
    public Polity FreeMarch(World w, House house, string regionId)
    {
        var p = Declare(w, house, regionId, FormOf(w, house), FreeMarchOrigin);
        w.WorldCounters["free_marches"] = w.Counter("free_marches") + 1; w.WorldFlags.Add("free_march:" + regionId);
        return p;
    }
    /// <summary>A FREE HOLD: a rebel world that has broken from its holder is a state from its first day. One house, one world, no
    /// region, recognised by nobody. Its old holder may buy it back or take it; if it still stands after three years the Reach
    /// stops pretending otherwise and names it in a treaty. The Senate does not go back for a single world.</summary>
    public Polity FreeHold(World w, House house, House? from, string worldName)
    {
        int n = w.Counter("free_states") + 1; w.WorldCounters["free_states"] = n;
        var p = new Polity { Id = "freehold_" + n, Name = "the Free City of " + worldName, Form = Republic, HeadHouseId = house.Id, Since = w.Turn, Temper = FreeHoldTemper, Origin = FreeHoldOrigin, WarSince = w.Turn };
        if (from != null && InReach(w, from.Id)) p.Foes.Add(from.Id);
        p.Members.Add(house.Id); house.PolityId = p.Id; w.Polities.Add(p);
        return p;
    }

    private void Leave(World w, House h, Polity p)
    {
        h.PolityId = p.Id; if (!p.Members.Contains(h.Id)) p.Members.Add(h.Id); h.Bloc = "";
        foreach (var f in w.Factions) { f.Members.Remove(h.Id); if (f.LeaderHouseId == h.Id) f.LeaderHouseId = f.Members.OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? ""; }
        w.Factions.RemoveAll(f => f.Members.Count == 0);
        OfficeSystem.Vacate(w, _offices, h.Id); w.Magistrates.RemoveAll(m => m.HouseId == h.Id);
        w.Candidacies.RemoveAll(c => w.Char(c.CharacterId)?.HouseId == h.Id);
        foreach (var r in w.Recalls.Where(r => r.HouseId == h.Id).ToList()) { w.Recalls.Remove(r); w.Char(r.CharacterId)?.Flags.Remove(CaesarSystem.RecalledChar); }
        w.WorldFlags.Remove(ElectionSystem.StandFlag(h.Id));
    }

    /// <summary>The house leads the region out. The Reach lets it go, or goes to war.</summary>
    public Polity Declare(World w, House house, string regionId, string form, string origin = "declared")
    {
        var region = w.Galaxy!.Region(regionId)!; var (_, _, _, backers) = Control(w, house, regionId); var (temper, why) = Temper(w, house, regionId);
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w);
        int n = w.Counter("polities_founded") + 1; w.WorldCounters["polities_founded"] = n;
        var alone = StandAlone(w, house, regionId);
        var p = new Polity { Id = "polity_" + n, Name = NameFor(form, region.Name), Form = form, HeadHouseId = house.Id, Since = w.Turn, Temper = temper, Origin = origin, Leverage = alone.leverage, Starved = alone.starved };
        p.Regions.Add(regionId); w.Polities.Add(p);
        Leave(w, house, p); foreach (var b in backers) Leave(w, b, p);
        foreach (var b in backers.Where(b => IsPromised(w, b, house))) { p.Promised.Add(b.Id); w.WorldCounters.Remove(PromisedKey(b.Id, house.Id)); }   // what was promised falls due
        // a house that leaves the Reach leaves the Reach's wars: the great war and a general's march are no longer its business
        foreach (var id in p.Members.ToList()) foreach (var war in w.Wars.Where(x => x.Involves(id)).ToList()) _fleets.MakePeace(w, war.HouseA, war.HouseB, terms: false);
        if (w.CivilWar is { } cw) { foreach (var id in p.Members) { cw.With.Remove(id); cw.Against.Remove(id); cw.Declared.Remove(id); } }
        if (chair != null && Economy.HeadOf(w, chair) is { } ch && Economy.HeadOf(w, house) is { } hh) { var r = w.Relationship(ch.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition - 30, -100, 100); }
        string who = Economy.HeadOf(w, house)?.Name ?? house.Name;
        w.Log($"{house.Name} DECLARES {region.Name} INDEPENDENT of {(w.IsImperium ? "the throne" : "the Senate")}: {p.Name}, {FormName(form)}, with {who} as its {RulerStyle(form, Economy.HeadOf(w, house))}."
            + (backers.Count > 0 ? $" {string.Join(", ", backers.Select(b => b.Name))} go with it." : ""));
        if (house.Id == mine) Mark(w, DeclaredFlag); else Mark(w, OtherBreaksFlag);
        if (temper <= 0)
        {
            w.Log($"The Reach does not answer with a fleet: {(why.Count > 0 ? why[0] : "it has not the will")}.");
            Recognise(w, p, letGo: true);
        }
        else SendTheFleets(w, p, house, backers, region.Name);
        return p;
    }

    /// <summary>The Reach goes to war to bring a state, or a region that has gone over to one, back.</summary>
    private void SendTheFleets(World w, Polity p, House leader, IReadOnlyList<House> others, string regionName)
    {
        var chair = RegionSystem.CapitalHolder(w); p.WarSince = w.Turn; p.Recognised = 0;
        p.Foes = w.Houses.Values.Where(h => Real(h) && InReach(w, h.Id) && h.Id != chair?.Id && FleetSystem.StrengthOf(w, h.Id) > 0)
            .OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(3).Select(h => h.Id).ToList();
        if (chair != null && InReach(w, chair.Id)) p.Foes.Insert(0, chair.Id);
        foreach (var foe in p.Foes) _fleets.DeclareWar(w, foe, leader.Id);
        for (int i = 0; i < others.Count && p.Foes.Count > 0; i++) if (others[i].Id != leader.Id) _fleets.DeclareWar(w, p.Foes[i % p.Foes.Count], others[i].Id);
        w.Log($"The Reach will not have it. {string.Join(", ", p.Foes.Select(id => w.House(id)?.Name ?? id))} are sent to bring {regionName} back.");
    }

    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }

    // ---------------- joining a state that exists (docs/BREAKAWAY.md) ----------------

    /// <summary>Do two regions touch: is there a lane from a system of one to a system of the other?</summary>
    public static bool Borders(World w, string regionA, string regionB) => w.Galaxy != null && regionA != regionB && w.Galaxy.Lanes.Any(l =>
        (w.Galaxy.System(l.A)?.RegionId, w.Galaxy.System(l.B)?.RegionId) is var (a, b) && ((a == regionA && b == regionB) || (a == regionB && b == regionA)));

    /// <summary>May this house of the Reach go over to this state? With no region named, it goes over as it stands, and must hold a
    /// world in one of the state's regions. With a region named, it takes the region with it: the region must border the state, and
    /// the house must control it as it would to declare it independent.</summary>
    public string? AccedeRefusal(World w, House? house, Polity? p, string regionId = "")
    {
        if (house == null || !Real(house) || p == null || !w.Polities.Contains(p)) return "no such state";
        if (!InReach(w, house.Id)) return "the house has already left the Reach";
        if (p.Recognised == 0 && regionId.Length > 0) return $"{p.Name} is still fighting to exist: it cannot take in a region until it is a state";
        if (RegionSystem.CapitalHolder(w)?.Id == house.Id) return "the house holds the capital: it cannot leave what it sits at the head of";
        if (w.CivilWar?.HouseId == house.Id) return "the house is marching on the capital";
        var ruler = w.House(p.HeadHouseId); if (ruler == null) return "the state has no ruler to treat with";
        if (Regard(w, ruler, house) < AccedeRegard) return $"{ruler.Name}, which rules {p.Name}, will not have you (regard {Regard(w, ruler, house)}, needs {AccedeRegard})";
        if (regionId.Length == 0)
            return p.Regions.SelectMany(r => Worlds(w, r)).Any(x => x.OwnerId == house.Id) ? null : $"the house holds no world in {p.Name}";
        var region = w.Galaxy?.Region(regionId); if (region == null) return "no such region";
        if (OfRegion(w, regionId) != null) return $"{region.Name} has already left the Reach";
        if (w.Galaxy!.System(w.Galaxy.Capital)?.RegionId == regionId) return "the capital's own region cannot leave the Reach";
        if (!p.Regions.Any(r => Borders(w, r, regionId))) return $"{region.Name} does not border {p.Name}";
        var (own, with, total, _) = Control(w, house, regionId);
        if (total == 0 || own == 0) return $"the house holds no world of {region.Name}";
        if (own * 100 < OwnShare * total) return $"the house holds {own} of the {total} worlds of {region.Name}, and must hold a quarter";
        return with * 100 < ControlShare * total ? $"with the houses that would follow, {with} of the {total} worlds of {region.Name} would go: it takes three in five" : null;
    }

    /// <summary>The house goes over to the state. Alone, the Reach loses a house and resents it. With a region, the Reach either
    /// lets it go or calls it a breach of the treaty and comes for the whole state. True if it came to war.</summary>
    public bool Accede(World w, House house, Polity p, string regionId = "")
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); var region = regionId.Length > 0 ? w.Galaxy!.Region(regionId) : null;
        var backers = region != null ? Control(w, house, regionId).backers : new List<House>(); int temper = region != null ? Temper(w, house, regionId).temper : 0;
        Leave(w, house, p); foreach (var b in backers) Leave(w, b, p);
        foreach (var h in backers.Prepend(house)) foreach (var war in w.Wars.Where(x => x.Involves(h.Id)).ToList()) _fleets.MakePeace(w, war.HouseA, war.HouseB, terms: false);
        foreach (var h in backers.Prepend(house)) p.Foes.Remove(h.Id);   // one that was sent against the state and has gone over to it is its enemy no longer
        if (w.CivilWar is { } cw) foreach (var h in backers.Prepend(house)) { cw.With.Remove(h.Id); cw.Against.Remove(h.Id); cw.Declared.Remove(h.Id); }
        if (chair != null && Systems.Economy.HeadOf(w, chair) is { } ch && Systems.Economy.HeadOf(w, house) is { } hh) { var r = w.Relationship(ch.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition - (region != null ? 30 : 20), -100, 100); }
        if (w.House(p.HeadHouseId) is { } ruler) { var a = Systems.Economy.HeadOf(w, ruler); var b = Systems.Economy.HeadOf(w, house); if (a != null && b != null && a.Id != b.Id) { var r = w.Relationship(a.Id, b.Id); r.Disposition = Math.Clamp(r.Disposition + 10, -100, 100); } }
        w.WorldCounters["polities_joined"] = w.Counter("polities_joined") + 1;
        if (house.Id == mine) Mark(w, JoinedFlag); else if (p.Members.Contains(mine ?? "")) Mark(w, JoinedOursFlag);
        if (region == null)
        {
            if (p.Recognised > 0) { w.Log($"{house.Name} goes over to {p.Name}. It has no seat in the Senate from today."); return false; }
            // a state still fighting: the house joins its war
            foreach (var foe in p.Foes) _fleets.DeclareWar(w, foe, house.Id);
            w.Log($"{house.Name} goes over to {p.Name} in the middle of its war, and is at war with the Reach from today."); return true;
        }
        p.Regions.Add(regionId);
        w.Log($"{house.Name} takes {region.Name} out of the Reach and into {p.Name}." + (backers.Count > 0 ? $" {string.Join(", ", backers.Select(b => b.Name))} go with it." : ""));
        if (temper <= 0) { w.Log("The Reach does not answer with a fleet. The treaty stands, and the maps are redrawn."); return false; }
        // the Reach calls it a breach of the treaty, and what it named in that treaty is at war with it again
        p.Temper = temper; p.TreatyUntil = 0; Mark(w, BreachFlag);
        w.Log($"The Reach calls it a breach of the treaty. {char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} is at war for its life again.");
        SendTheFleets(w, p, w.House(p.HeadHouseId) ?? house, p.Members.Select(id => w.House(id)).Where(h => h != null).Select(h => h!).ToList(), region.Name);
        return true;
    }

    // ---------------- leaving a state ----------------

    public string? DepartRefusal(World w, House? house)
    {
        var p = house != null ? Of(w, house.Id) : null;
        if (house == null || p == null) return "the house is of the Reach";
        return p.HeadHouseId == house.Id ? "the state is yours: bring it back into the Reach, or keep it" : null;
    }
    /// <summary>A house that is not its ruler leaves a state and goes back to the Reach, with its worlds. The ruler does not forgive it;
    /// the capital is glad of it.</summary>
    public void Depart(World w, House house, Polity p)
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); var ruler = w.House(p.HeadHouseId);
        foreach (var f in p.Foes.ToList()) _fleets.MakePeace(w, house.Id, f, terms: false);
        p.Members.Remove(house.Id); p.Promised.Remove(house.Id); house.PolityId = "";
        void Shift(House? of, House? toward, int by) { var a = of != null ? Systems.Economy.HeadOf(w, of) : null; var b = toward != null ? Systems.Economy.HeadOf(w, toward) : null; if (a == null || b == null || a.Id == b.Id) return; var r = w.Relationship(a.Id, b.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100); }
        Shift(ruler, house, -25); Shift(chair, house, 10);
        w.WorldCounters["polities_left"] = w.Counter("polities_left") + 1;
        if (house.Id == mine) Mark(w, LeftFlag); else if (p.Members.Contains(mine ?? "")) Mark(w, LeftOursFlag);
        w.Log($"{house.Name} leaves {p.Name} and takes its seat in the Senate again. Its worlds go with it.");
    }

    // ---------------- stirring the camps (docs/BREAKAWAY.md, what brings the great war) ----------------

    public static bool Stirring(World w) => w.Counter(StirKey) >= w.Turn && w.Counter(StirKey) > 0;
    public string? StirRefusal(World w, House? house, House? against = null)
    {
        if (house == null || !Real(house)) return "no such house";
        if (against != null && (!Real(against) || against.Id == house.Id || !InReach(w, against.Id))) return "no such house of the Reach to set them against";
        if (!InReach(w, house.Id)) return "the house has left the Reach, and its quarrels";
        if (GreatCrisisSystem.AtWar(w)) return "the war has come: there is nothing left to stir";
        if (w.HasFlag(GreatCrisisSystem.ResolvedFlag)) return "the Reach has had its war, and will not be stirred to another for years";
        if (Stirring(w)) return $"the camps are being stirred already (until year {w.Counter(StirKey)})";
        return house.Treasury.Credits < StirCredits ? $"it takes {StirCredits} credits, to the right people" : null;
    }
    /// <summary>The house sets the great houses against one another on purpose: money and words in the right places, for three years.
    /// It pushes the Reach toward its great war. One time in three the capital hears whose money it was.</summary>
    public const int CensureAt = -30;
    public static string StirredKey(string houseId, string againstId) => $"stirred_until:{houseId}:{againstId}";
    /// <summary>The house this one has been stirred against, while the talk lasts: it will move a censure of it before anything else.</summary>
    public static House? StirredAgainst(World w, string houseId)
    {
        string prefix = $"stirred_until:{houseId}:";
        return w.WorldCounters.Where(kv => kv.Value >= w.Turn && kv.Key.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => w.House(kv.Key[prefix.Length..])).FirstOrDefault(h => h != null && InReach(w, h.Id));
    }
    public bool Stir(World w, House house, House? against = null)
    {
        var mine = w.Protagonist?.HouseId; house.Treasury.Credits -= StirCredits;
        if (against != null)
        {
            // the five strongest of the rest hear worse of this one house than of any other
            var ah = Systems.Economy.HeadOf(w, against);
            foreach (var h in w.Houses.Values.Where(h => Real(h) && InReach(w, h.Id) && h.Id != house.Id && h.Id != against.Id).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(5))
                if (Systems.Economy.HeadOf(w, h) is { } hd && ah != null && hd.Id != ah.Id)
                {
                    var r = w.Relationship(hd.Id, ah.Id); r.Disposition = Math.Clamp(r.Disposition - StirAgainst, -100, 100);
                    // one that has come to think ill enough of it will say so in the chamber while the talk lasts (docs/BREAKAWAY.md)
                    if (r.Disposition <= CensureAt) w.WorldCounters[StirredKey(h.Id, against.Id)] = w.Turn + StirYears;
                }
            w.Log($"What is said in the great houses this year is said, more than anything, about {against.Name}.");
        } w.WorldCounters[StirKey] = w.Turn + StirYears; w.WorldCounters["camps_stirred"] = w.Counter("camps_stirred") + 1;
        if (house.Id == mine) Mark(w, StirredFlag);
        if (house.Id == mine && against != null) w.WorldCounters[MyStirKey(against.Id)] = w.Turn + StirYears;   // whose money it was, should it come to a war
        w.Log($"Money moves, and letters. For {StirYears} years the great houses will hear worse of one another than they did.");
        var chair = RegionSystem.CapitalHolder(w); bool heard = chair != null && chair.Id != house.Id && Hash($"stir:{house.Id}:{w.Turn}") % 3 == 0;
        if (!heard) return false;
        if (Systems.Economy.HeadOf(w, chair!) is { } ch && Systems.Economy.HeadOf(w, house) is { } hh) { var r = w.Relationship(ch.Id, hh.Id); r.Disposition = Math.Clamp(r.Disposition - 15, -100, 100); }
        house.Legitimacy = Math.Clamp(house.Legitimacy - 3, 0, 100); if (house.Id == mine) Mark(w, ReportedFlag);
        if (against != null && Systems.Economy.HeadOf(w, against) is { } victim && Systems.Economy.HeadOf(w, house) is { } me2 && victim.Id != me2.Id) { var r = w.Relationship(victim.Id, me2.Id); r.Disposition = Math.Clamp(r.Disposition - 25, -100, 100); }   // and so does the house it was aimed at
        w.Log($"{chair!.Name} learns whose money it was. {house.Name} is spoken of in the capital as a house that wants a war."); return true;
    }

    // ---------------- a war over what was said (docs/BREAKAWAY.md, "Stirring the camps") ----------------

    public const int WarOverWordsAt = -50;
    public const string WarOverWordsFlag = "war_over_words", WarOverWordsCounter = "wars_over_words";
    public static string MyStirKey(string againstId) => "my_stir_against:" + againstId;
    /// <summary>Why a house that has been stirred against another does not go to war with it, or null if it will.</summary>
    public static string? WarOverWordsRefusal(World w, House h, House? target)
    {
        if (target == null || target.Id == h.Id || !Real(h) || !InReach(w, h.Id) || !InReach(w, target.Id)) return "nobody it has been stirred against";
        if (GreatCrisisSystem.AtWar(w) || w.CivilWar != null) return "the Reach is at war with itself already";
        if (Regard(w, h, target) > WarOverWordsAt) return "it does not think ill enough of them";
        int ours = FleetSystem.StrengthOf(w, h.Id), theirs = FleetSystem.StrengthOf(w, target.Id);
        if (ours <= 0 || ours < theirs) return "it has not the hulls";
        if (w.Wars.Any(x => x.Involves(h.Id))) return "it has a war already";
        if (Bound(w, h, target)) return "the two are bound by a marriage";
        return RegionSystem.CapitalHolder(w)?.Id == target.Id ? "nobody goes to war with the capital over talk" : null;
    }
    /// <summary>A house that has been stirred against another, already thought very ill of it and is no weaker in hulls goes to war
    /// with it: what was a censure becomes a war. One such war a year at the most; the player's house is never moved for them.</summary>
    private void WarsOverWords(World w)
    {
        var mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => Real(h) && h.Id != mine).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var target = StirredAgainst(w, h.Id); if (target == null || target.Id == mine || WarOverWordsRefusal(w, h, target) != null) continue;
            _fleets.DeclareWar(w, h.Id, target.Id);
            w.WorldCounters.Remove(StirredKey(h.Id, target.Id)); w.WorldCounters[WarOverWordsCounter] = w.Counter(WarOverWordsCounter) + 1;
            w.Log($"{h.Name} goes to war with {target.Name}. Nothing has been taken from it and nothing done to it: it is over what has been said of {target.Name} in every great house these three years.");
            if (w.Counter(MyStirKey(target.Id)) >= w.Turn) Mark(w, WarOverWordsFlag);
            break;
        }
    }

    private void Peace(World w, Polity p)
    {
        foreach (var m in p.Members.ToList()) foreach (var f in p.Foes.ToList()) _fleets.MakePeace(w, m, f, terms: false);
    }

    /// <summary>The Reach accepts that the state is a state.</summary>
    public void Recognise(World w, Polity p, bool letGo = false)
    {
        var mine = w.Protagonist?.HouseId; Peace(w, p); p.Recognised = w.Turn; p.Foes.Clear(); p.TreatyUntil = w.Turn + TreatyYears; p.WarSince = 0;
        if (w.House(p.HeadHouseId) is { } head) head.Legitimacy = Math.Clamp(head.Legitimacy + 10, 0, 100);
        string counted = IsFreeHold(p) ? "free_states_recognised" : "polities_recognised"; w.WorldCounters[counted] = w.Counter(counted) + 1;
        if (!letGo) w.Log($"The Reach makes peace with {p.Name} and names it in the treaty. It is a state now, and will be on the maps.");
        if (p.Members.Contains(mine ?? "")) Mark(w, letGo ? LetGoFlag : WonFlag);
    }

    /// <summary>The state is brought back into the Reach, and those who led it out pay.</summary>
    public void Dissolve(World w, Polity p, string why)
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); Peace(w, p); w.Polities.Remove(p);
        w.Log($"{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} is no more: {why}. Its worlds are the Reach's again.");
        foreach (var id in p.Members)
        {
            var h = w.House(id); if (h == null) continue; h.PolityId = "";
            bool led = id == p.HeadHouseId; h.Legitimacy = Math.Clamp(h.Legitimacy - (led ? 15 : 6), 0, 100);
            if (led)
            {
                w.WorldCounters[CaesarSystem.EnemyKey(id)] = w.Turn + 10;
                if (h.Treasury.Credits > 0) { int fine = h.Treasury.Credits * 3 / 10; h.Treasury.Credits -= fine; if (chair != null) chair.Treasury.Credits += fine; }
            }
        }
        string ended = IsFreeHold(p) ? "free_states_ended" : "polities_dissolved"; w.WorldCounters[ended] = w.Counter(ended) + 1;
        if (p.Members.Contains(mine ?? "")) Mark(w, LostFlag);
    }

    // ---------------- a state as a neighbour ----------------

    /// <summary>Does a treaty stand between these two houses: one of the Reach, one of a state it has recognised, inside the treaty's years?</summary>
    public static Polity? TreatyBetween(World w, string a, string b)
    {
        var pa = Of(w, a); var pb = Of(w, b); if ((pa == null) == (pb == null)) return null;
        var p = pa ?? pb; return p!.Recognised > 0 && w.Turn < p.TreatyUntil ? p : null;
    }

    public string? ReconquestRefusal(World w, House? target)
    {
        var p = target != null ? Of(w, target.Id) : null;
        if (target == null || p == null) return "not a state that has left the Reach";
        if (p.HeadHouseId != target.Id) return $"{w.House(p.HeadHouseId)?.Name} rules {p.Name}: name them";
        if (IsFreeHold(p)) return "one world is its old holder's business, not the Senate's";
        if (p.Recognised == 0) return "the Reach is already at war with it";
        if (w.Turn < p.TreatyUntil) return $"the treaty with {p.Name} holds until year {p.TreatyUntil}";
        return Weary(w) ? "the Reach has no stomach for it: it has fought too lately" : null;
    }

    /// <summary>How a house of the Reach leans on going back for a state.</summary>
    public int ReconquestLean(World w, House h, House ruler, int toRuler)
    {
        var p = Of(w, ruler.Id); if (p == null) return 0; var chair = RegionSystem.CapitalHolder(w);
        int lean = toRuler >= 20 ? -2 : toRuler <= -20 ? 2 : 0;
        if (chair?.Id == h.Id) lean += 3;
        lean += FactionSystem.Of(w, h.Id)?.PartyId switch { "optimates" => 1, "sword" => 1, "factors" => -1, "populares" => -1, _ => 0 };
        int ours = p.Members.Sum(id => FleetSystem.StrengthOf(w, id)), mine = FleetSystem.StrengthOf(w, h.Id);
        if (mine > 0 && ours * 2 >= mine * 3) lean -= 1;                                  // it would be this house's hulls that went
        string ring = p.Regions.Select(r => w.Galaxy?.Region(r)?.Ring).FirstOrDefault() ?? Ring.Middle;
        if (ring == Ring.Rim) lean -= 1; else if (ring == Ring.Core) lean += 1;
        if (p.Leverage) lean -= 1;                                                       // and the Reach still buys what it makes
        return lean;
    }

    /// <summary>The Reach goes back for a state it once recognised. It is a war of independence again, on the same terms.</summary>
    public void Reconquer(World w, Polity p)
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); var ruler = w.House(p.HeadHouseId);
        p.Recognised = 0; p.TreatyUntil = 0; p.WarSince = w.Turn; p.Reconquests++; p.Temper = Math.Max(1, ruler != null && p.Regions.Count > 0 ? Temper(w, ruler, p.Regions[0]).temper : 1);
        p.Foes = w.Houses.Values.Where(h => Real(h) && InReach(w, h.Id) && h.Id != chair?.Id && FleetSystem.StrengthOf(w, h.Id) > 0)
            .OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(3).Select(h => h.Id).ToList();
        if (chair != null && InReach(w, chair.Id)) p.Foes.Insert(0, chair.Id);
        var members = p.Members.OrderByDescending(id => id == p.HeadHouseId).ThenBy(id => id, StringComparer.Ordinal).ToList();
        foreach (var foe in p.Foes) _fleets.DeclareWar(w, foe, p.HeadHouseId);
        for (int i = 1; i < members.Count && p.Foes.Count > 0; i++) _fleets.DeclareWar(w, p.Foes[i % p.Foes.Count], members[i]);
        w.WorldCounters["reconquests"] = w.Counter("reconquests") + 1;
        if (p.Members.Contains(mine ?? "")) Mark(w, ReconquestFlag);
        w.Log($"The Reach comes back for {p.Name}. {string.Join(", ", p.Foes.Select(id => w.House(id)?.Name ?? id))} are sent. The treaty is so much paper.");
    }

    public string? RejoinRefusal(World w, House? house)
    {
        var p = house != null ? Of(w, house.Id) : null;
        if (house == null || p == null) return "the house is of the Reach";
        return p.HeadHouseId != house.Id ? $"{w.House(p.HeadHouseId)?.Name} rules {p.Name}: it is theirs to say" : null;
    }

    /// <summary>The ruling house takes the state back into the Reach of its own will. Recognised, it comes back whole and is thought
    /// the better of in the capital; in the middle of its war it comes back on the Reach's terms, lighter than defeat.</summary>
    public void Rejoin(World w, Polity p)
    {
        var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w); bool atWar = p.Recognised == 0; Peace(w, p); w.Polities.Remove(p);
        foreach (var id in p.Members)
        {
            var h = w.House(id); if (h == null) continue; h.PolityId = "";
            if (atWar) h.Legitimacy = Math.Clamp(h.Legitimacy - (id == p.HeadHouseId ? 6 : 2), 0, 100);
        }
        if (chair != null && w.House(p.HeadHouseId) is { } ruler && Systems.Economy.HeadOf(w, chair) is { } ch && Systems.Economy.HeadOf(w, ruler) is { } rh)
        { var r = w.Relationship(ch.Id, rh.Id); r.Disposition = Math.Clamp(r.Disposition + (atWar ? 5 : 15), -100, 100); }
        w.WorldCounters["polities_rejoined"] = w.Counter("polities_rejoined") + 1;
        if (p.Members.Contains(mine ?? "")) Mark(w, RejoinedFlag);
        w.Log(atWar ? $"{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} lays down its arms and comes back into the Reach on the Reach's terms."
                    : $"{char.ToUpperInvariant(p.Name[0])}{p.Name[1..]} comes back into the Reach of its own will. Its houses take their seats again.");
    }

    /// <summary>Out of the great war: a side that cannot win but holds a region keeps it, and the peace says so.</summary>
    public Polity Partition(World w, House leader, string regionId, IEnumerable<House> side)
    {
        var region = w.Galaxy!.Region(regionId)!; int n = w.Counter("polities_founded") + 1; w.WorldCounters["polities_founded"] = n;
        var p = new Polity { Id = "polity_" + n, Name = NameFor(Republic, region.Name), Form = Republic, HeadHouseId = leader.Id, Since = w.Turn, Origin = "partition" };
        p.Regions.Add(regionId); w.Polities.Add(p);
        var worlds = Worlds(w, regionId);
        foreach (var h in side.Where(h => h.Id == leader.Id || worlds.Any(x => x.OwnerId == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal)) Leave(w, h, p);
        p.Recognised = w.Turn; p.TreatyUntil = w.Turn + TreatyYears; w.WorldCounters["polities_recognised"] = w.Counter("polities_recognised") + 1; Mark(w, PartitionFlag);
        w.Log($"The peace partitions the Reach. {region.Name} goes its own way as {p.Name}, {FormName(p.Form)}, under {leader.Name}: {string.Join(", ", p.Members.Select(id => w.House(id)?.Name ?? id))}.");
        return p;
    }

    /// <summary>How a war of independence stands for the state: the lead in its battles.</summary>
    public static int Standing(World w, Polity p)
    {
        int lead = 0;
        foreach (var war in w.Wars) foreach (var m in p.Members) if (war.Involves(m) && p.Foes.Contains(war.Other(m))) lead += war.Lead(m);
        return lead;
    }
    /// <summary>How much of its regions the state's own houses still hold, in hundredths.</summary>
    public static int Share(World w, Polity p)
    {
        if (p.Regions.Count == 0) return p.Members.Any(id => w.HoldingsOf(id).Any(h => h.IsPlanet)) ? 100 : 0;   // a free hold has a world and no region: it stands while it holds it
        var worlds = p.Regions.SelectMany(r => Worlds(w, r)).ToList();
        return worlds.Count == 0 ? 0 : 100 * worlds.Count(h => p.Members.Contains(h.OwnerId)) / worlds.Count;
    }
    /// <summary>The Reach's will to go on with the war: what it was when the state broke away, less one for every three years.</summary>
    public static int Will(World w, Polity p) => p.Temper - (p.WarSince > 0 ? (w.Turn - p.WarSince) / (p.Leverage ? LeverageFallsEvery : TemperFallsEvery) : 0);

    // ---------------- the year ----------------

    public void Tick(World w)
    {
        var me = w.Protagonist; var mine = me?.HouseId;
        foreach (var f in YearFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }

        foreach (var p in w.Polities.ToList())
        {
            p.Members.RemoveAll(id => w.House(id) == null);
            if (p.Members.Count == 0) { w.Polities.Remove(p); continue; }
            if (!p.Members.Contains(p.HeadHouseId))   // the ruling house is gone: the strongest that is left rules
                p.HeadHouseId = p.Members.OrderByDescending(id => FleetSystem.StrengthOf(w, id)).ThenBy(id => id, StringComparer.Ordinal).First();
            if (IsFreeHold(p) && Share(w, p) == 0) { Dissolve(w, p, "its world is taken"); continue; }   // bought back or taken, treaty or no treaty
            if (p.Recognised > 0) continue;
            if (p.Starved) foreach (var id in p.Members) w.WorldCounters["embargo:" + id] = w.Turn + 2;   // no fuel of its own: the Reach's exchanges are closed to it while the war lasts
            int years = w.Turn - p.WarSince, share = Share(w, p), lead = Standing(w, p);
            if (share < LostShare) Dissolve(w, p, p.Regions.Count == 0 ? "its world is taken" : "its houses have lost the region");
            else if (years >= 2 && lead <= LoseLead) Dissolve(w, p, "it is beaten in the field");
            else if (years >= WinYears && share >= HoldShare && (lead >= WinLead || Will(w, p) <= 0))
            {
                w.Log(lead >= WinLead ? $"{p.Name} has beaten what the Reach sent against it." : $"After {years} years the Reach has no more will to bring {p.Name} back.");
                Recognise(w, p);
            }
        }

        // What was promised is paid, for ten years from the founding: three credits a year from the ruler to each house it promised.
        // A ruler who cannot pay is thought the less of. A member that has come to think ill of the ruler leaves, unless it is afraid to.
        foreach (var p in w.Polities.OrderBy(p => p.Id, StringComparer.Ordinal).ToList())
        {
            var ruler = w.House(p.HeadHouseId); if (ruler == null) continue;
            p.Promised.RemoveAll(id => !p.Members.Contains(id)); if (w.Turn - p.Since > PledgeYears) p.Promised.Clear();
            foreach (var id in p.Promised.OrderBy(id => id, StringComparer.Ordinal).ToList())
            {
                var owed = w.House(id); if (owed == null) continue;
                if (ruler.Treasury.Credits >= PromiseCredits) { ruler.Treasury.Credits -= PromiseCredits; owed.Treasury.Credits += PromiseCredits; continue; }
                if (Systems.Economy.HeadOf(w, owed) is { } oh && Systems.Economy.HeadOf(w, ruler) is { } rh && oh.Id != rh.Id) { var r = w.Relationship(oh.Id, rh.Id); r.Disposition = Math.Clamp(r.Disposition - 8, -100, 100); }
                if (ruler.Id == mine && !w.HasFlag(UnpaidFlag)) { Mark(w, UnpaidFlag); w.Log($"{ruler.Name} cannot pay {owed.Name} what it was promised this year. {owed.Name} had been counting on it."); }
            }
            var leaver = p.Members.Where(id => id != p.HeadHouseId && id != mine).Select(id => w.House(id)).Where(h => h != null && Regard(w, h!, ruler) <= LeavesRegard && !Cowed(w, h!, ruler) && Hash($"leaves:{h!.Id}:{w.Turn}") % 3 == 0)
                .OrderBy(h => Regard(w, h!, ruler)).ThenBy(h => h!.Id, StringComparer.Ordinal).FirstOrDefault();
            if (leaver != null) Depart(w, leaver, p);
        }

        // A house of the Reach that holds worlds inside a recognised state, thinks well of its ruler and ill of the capital, goes over
        // to it of its own accord: one a year at the most (docs/BREAKAWAY.md). The player's house is never moved for them.
        var chairNow = RegionSystem.CapitalHolder(w);
        foreach (var p in w.Polities.Where(p => p.Recognised > 0).OrderBy(p => p.Id, StringComparer.Ordinal).ToList())
        {
            var ruler = w.House(p.HeadHouseId); if (ruler == null) continue;
            var goer = w.Houses.Values.Where(h => h.Id != mine && AccedeRefusal(w, h, p) == null && Regard(w, h, ruler) >= GoesOverRegard && (chairNow == null || Regard(w, h, chairNow) <= 0) && Hash($"goes_over:{h.Id}:{w.Turn}") % 4 == 0)
                .OrderByDescending(h => Regard(w, h, ruler)).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
            if (goer != null) { Accede(w, goer, p); break; }
            // and one that controls a region on the state's border, thinks very well of its ruler and ill of the capital, may bring the region
            if (w.Galaxy == null) continue;
            foreach (var r in w.Galaxy.ReachRegions.Where(r => p.Regions.Any(x => Borders(w, x, r.Id))).OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                var bringer = w.Houses.Values.Where(h => h.Id != mine && AccedeRefusal(w, h, p, r.Id) == null && Regard(w, h, ruler) >= GoesOverRegard + 10 && (chairNow == null || Regard(w, h, chairNow) <= -10) && Hash($"brings:{h.Id}:{r.Id}:{w.Turn}") % 6 == 0)
                    .OrderByDescending(h => Regard(w, h, ruler)).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
                if (bringer != null) { Accede(w, bringer, p, r.Id); goer = bringer; break; }
            }
            if (goer != null) break;
        }

        WarsOverWords(w);

        // those who fought a general who is now on the throne need not kneel: one of them that holds a region may take it out
        if (w.IsImperium)
            foreach (var h in w.Houses.Values.Where(h => Real(h) && h.Id != mine && InReach(w, h.Id) && Within(w, OpposedKey(h.Id), RefuseYears)).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal))
            {
                var region = RegionsOf(w, h).FirstOrDefault(r => DeclareRefusal(w, h, r.Id, Republic) == null);
                if (region != null && w.Counter("refusal:" + h.Id) == 0) { w.WorldCounters["refusal:" + h.Id] = w.Turn; Declare(w, h, region.Id, Republic, origin: "refused"); break; }
            }

        if (me is not { Alive: true } || mine == null || w.House(mine) is not { } my) return;
        if (Of(w, mine) is { } own) w.WorldFlags.Add(own.Recognised > 0 ? IndependentFlag : WarFlag);
        else if (RegionsOf(w, my).Any(r => DeclareRefusal(w, my, r.Id, Republic) == null))
        {
            w.WorldFlags.Add(PossibleFlag);
            if (w.IsImperium && Within(w, OpposedKey(mine), RefuseYears)) w.WorldFlags.Add(RefuseFlag);
        }
    }
}
