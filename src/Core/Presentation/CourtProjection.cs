using Praetoria.Core.Diagnostics;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

// The Godot-free model of everything Court mode shows (BuildSpec §1 Pillar 1: the UI reads from Core).
// Plain records; the Godot layer only turns them into nodes. Being in Core, it is unit-tested headless,
// which is how the Court's logic stays verified even when no Godot runtime is at hand.

public sealed record CharacterCard(string Name, string HouseName, string Accent, string TitleName, string RankName, string TrackName,
    int Age, bool Alive, IReadOnlyList<string> Nature, IReadOnlyList<string> Aptitude, string AmbitionName, int Stress, bool Broken,
    bool Regency, string? Background);
public sealed record PoolsLine(int Influence, int Treasury, int Agents);
public sealed record TreasuryLine(int Credits, int Materials, int Manpower, int Influence, int Exotics, bool Insolvent, int Helium3 = 0, int RareEarths = 0, int Isotopes = 0);
public sealed record BuildOption(string HoldingId, string BuildingId, string Label, string Tooltip, bool Available);
public sealed record HoldingCard(string Id, string Name, string SpecName, string? SystemName, int Population, int MaxPopulation, int Unrest,
    int SlotsUsed, int Slots, IReadOnlyList<string> Buildings, IReadOnlyList<BuildOption> Builds, bool CanRaiseShips, string RaiseShipsLabel,
    IReadOnlyList<ShipOption> Ships, bool Besieged, string Kind = "planet", string? GovernorName = null, IReadOnlyList<Option>? GovernorOptions = null, string Culture = "",
    int Prosperity = 50, int ProsperityTarget = 50, Option? Invest = null, int Belt = 0, int GovernorDisposition = 0, int GovernorAdministration = 0, bool GovernorAmbitious = false);
/// <summary>A venture the house owns (GDD §17 businesses) and what can be done with it.</summary>
public sealed record BusinessCard(string Id, string Name, string KindName, string? SystemName, int Level, int MaxLevel, string Yield, string? Charter, int Value, Option Expand, Option Sell,
    int Earnings = 0, int MarketPrice = 0, IReadOnlyList<VentureBid>? Bids = null, string? CompanyName = null, bool Enlarging = false);
/// <summary>A house that would buy one of your ventures, and what it would pay.</summary>
public sealed record VentureBid(string HouseId, string HouseName, string Accent, int Price, bool HoldsTheWorld);
/// <summary>Something of yours under construction (docs/ECONOMY_MARKET.md stage B).</summary>
public sealed record ProjectCard(string Id, string Name, string Kind, int TurnsLeft, int TurnsTotal, string Paid, string Remaining, string Next, bool Paused, bool Stalled, int RushCredits, string? RushRefusal, string Refund, string? CompanyName)
{
    /// <summary>A great work: it is hastened by the year, not rushed. <see cref="HastenYear"/> is what a hastened year costs, and <see cref="HastenExtra"/> how much of that buys only the speed.</summary>
    public bool Great { get; init; }
    public bool Hastened { get; init; }
    /// <summary>It was to be hastened last year and the treasury could not stand it: it went at its own pace.</summary>
    public bool HastenMissed { get; init; }
    public string HastenYear { get; init; } = "";
    public string HastenExtra { get; init; } = "";
}
/// <summary>Another house's venture you could buy, at its asking price, and what it would earn you.</summary>
public sealed record ForSaleCard(string BusinessId, string Name, string KindName, string OwnerId, string OwnerName, string OwnerAccent, string? SystemName, int Level, int PerYear, int Ask, string? Refusal, string Payback, bool Distressed, bool OnYourWorld);
/// <summary>A venture offered to you, until the year is out.</summary>
public sealed record OfferCard(string Id, string BusinessName, string KindName, string FromName, string? SystemName, int Price, int PerYear, string Reason, string? Refusal);
/// <summary>The house's assets beyond land: projects under way, ventures for sale in the Reach, offers on the table.</summary>
public sealed record AssetsCard(IReadOnlyList<ProjectCard> Projects, IReadOnlyList<ForSaleCard> ForSale, IReadOnlyList<OfferCard> Offers);
/// <summary>A venture the house could found: a kind in a system, with the reason it cannot (null = it can).</summary>
public sealed record VentureOption(string DefId, string Name, string Description, string SystemId, string SystemName, string Cost, string? Refusal, int PerYear = 0, string Payback = "", int BuildTurns = 0, string First = "", string Sector = "")
{
    /// <summary>What its earnings swing on, where they do: a war, the peace, a price that is spiking. Empty for a steady trade.</summary>
    public string Swing { get; init; } = "";
}
/// <summary>A belt the house could stake a claim in.</summary>
public sealed record ClaimOption(string SystemId, string SystemName, int Belt, int Staked, string? Refusal);
/// <summary>A world of the house's that broke away and may be bought back.</summary>
public sealed record ReclaimOption(string HoldingId, string HoldingName, string FreeHouseName, int Price, string? Refusal);
/// <summary>The capital: who holds it, and what it gives.</summary>
public sealed record CapitalCard(string SystemName, string? HolderId, string? HolderName, bool Mine);
/// <summary>One house's stance on a motion: declared or predicted, with its weight and whether the player may still whip it.</summary>
public sealed record StanceLine(string HouseId, string HouseName, string Accent, int Weight, int Stance, bool Declared, string? LobbyRefusal, string? BribeRefusal, string? Faction = null, bool FactionLeader = false)
{ /// <summary>What this house's vote would cost to buy (docs/PLAYTHROUGH.md 13).</summary>
  public int BribePrice { get; init; } }
/// <summary>A Senate faction: its name, leader, benches, platform and record, and what the player may do about it.</summary>
public sealed record FactionCard(string Id, string Name, string LeaderHouseId, string LeaderName, string LeaderAccent, IReadOnlyList<(string Id, string Name, string Accent)> Members, int Weight, bool Mine, bool Lead, string Platform, int Record, int Since, string? JoinRefusal,
    string Description = "", string Courts = "", string LawPlatform = "", bool Party = false);
/// <summary>A motion on the docket or in the history.</summary>
public sealed record MotionCard(string Id, string KindId, string KindName, string ProposerName, string ProposerAccent, string? TargetName, string Text, int ProposedTurn, bool Open, bool Passed,
    int ForWeight, int AgainstWeight, int MyStance, bool Mine, bool AgainstMe, IReadOnlyList<StanceLine> Stances, bool Petition = false, string? VetoRefusal = null, bool Vetoed = false, string? VetoedBy = null)
{ /// <summary>Why the count came in far from where it had stood (docs/PLAYTHROUGH.md 13), or "".</summary>
  public string Swing { get; init; } = ""; }
/// <summary>A motion the player could move, with its targets.</summary>
public sealed record MotionProposal(string KindId, string KindName, string Description, int Influence, string TargetType, IReadOnlyList<(string Id, string Name)> Targets, string? Refusal, bool NeedsOffice = false, IReadOnlyList<(string Id, string Name)>? Offices = null,
    bool NeedsLaw = false, IReadOnlyList<ActLawChoice>? Laws = null);
/// <summary>A Senate-scoped law an Act may set: its options (the one in force marked), each with its refusal were it moved now.</summary>
public sealed record ActLawChoice(string Id, string Name, string Description, string InForce, IReadOnlyList<(string Id, string Name, string Effects, string? Refusal, string Opinions)> Options, string Category = "");
/// <summary>A law of the whole Reach as it stands: who set it and to what.</summary>
public sealed record ReachLawLine(string Id, string Name, string Category, string OptionName, string Effects, string SetBy, bool Act, bool Mine, int CooldownLeft);
/// <summary>An office of the Reach: who holds it, what it confers, and whether the player has a candidate for it.</summary>
public sealed record OfficeCard(string Id, string Name, string Description, string? HolderHouseId, string? HolderHouse, string? HolderName, string? HolderAccent, int Since, bool Mine, string? MyCandidate, int Hulls, string Track, int MinRank, bool Elected = false);
/// <summary>The consulship (docs/REPUBLIC.md): the sitting consuls, the next election, the nominees as they stand and the player's candidacy.</summary>
public sealed record ElectionCard(int NextIn, int TermTurns, IReadOnlyList<(string Party, string PartyAccent, string HouseId, string HouseName, string Accent, int Tally)> Nominees, bool Standing, string? StandRefusal, bool AmConsul, bool CanVeto);
/// <summary>The Senate as a place of action (GDD §6.1): the docket, the history, what the player may move, and their weight in the chamber.</summary>
public sealed record SenateCard(IReadOnlyList<MotionCard> Docket, IReadOnlyList<MotionCard> History, IReadOnlyList<MotionProposal> Proposals, int MyWeight, int TotalWeight, bool MayPropose, bool Embargoed, int EmbargoLifts, IReadOnlyList<OfficeCard>? Offices = null,
    IReadOnlyList<FactionCard>? Factions = null, string? FoundRefusal = null, string? LeaveRefusal = null, int Followers = 0, ElectionCard? Election = null,
    string? HeadAbroad = null);   // why the house may move nothing but its own exile's return, said to the player: its head is in exile (docs/LADDER.md, "Exile")

/// <summary>One option of a law: in force or not, what it does, what the estates think of it, and why it cannot be chosen.</summary>
public sealed record LawOptionCard(string Id, string Name, bool InForce, string Effects, IReadOnlyList<(string Estate, string Name, int Opinion)> Opinions, string? Refusal);
/// <summary>A law: its scope (held or not), its options, its cooldown.</summary>
public sealed record LawCard(string Id, string Name, string Category, string Description, string Scope, string ScopeName, bool Held, int Cost, int CooldownLeft, IReadOnlyList<LawOptionCard> Options);
/// <summary>An estate's regard for the house: approval, where the laws would take it, and what its mood does.</summary>
public sealed record EstateCard(string Id, string Name, string Description, int Approval, int Target, string Mood, string Effects, string LawsSay, string? Petition = null, int HostileIn = 0, string? Wants = null);
/// <summary>The chancery: laws by category and the six estates.</summary>
/// <param name="Bound">The player's house is of the Reach, and its law binds it; false for a house that has broken away (docs/BREAKAWAY.md).</param>
public sealed record ChanceryCard(IReadOnlyList<LawCard> Laws, IReadOnlyList<EstateCard> Estates, int InfluencePool, IReadOnlyList<ReachLawLine>? Reach = null, bool Bound = true);
/// <summary>One house on the wealth table.</summary>
public sealed record WealthLine(string HouseId, string Name, string Accent, int Wealth, string Tier, bool Mine);
/// <summary>The house ledger (GDD §17): every per-turn line, the net, wealth and its rank, the loan.</summary>
public sealed record LedgerCard(IReadOnlyList<LedgerLine> Lines, Resources Net, int Wealth, int Rank, int Houses, string Tier, int Debt, string? LenderName, int LoanPrincipal, int LoanRepay, int LoanInstalment,
    Option Loan, Option Repay, IReadOnlyList<WealthLine> Table);
public sealed record PathOption(string Id, string Label, string Tooltip, bool Available)
{
    /// <summary>What it costs, in words; why it is refused, when it is; and what the player is asked before it is done.</summary>
    public string Cost { get; init; } = ""; public string? Refusal { get; init; } public string Confirm { get; init; } = "";
}
public sealed record StandingCard(int Legitimacy, int Instability, IReadOnlyList<string> Claims, string? NextTitle, int NextRequirement,
    IReadOnlyList<PathOption> Paths, string Spheres, int Threat, IReadOnlyList<string> Honors, string SuccessionLaw, IReadOnlyList<Option> LawOptions,
    string TaxPolicy, IReadOnlyList<Option> TaxOptions, IReadOnlyList<Option> ExchangeOptions, ThroneCard Throne)
{
    /// <summary>What the next rung asks and what the house has of it, one line for each thing; the same in a sentence; and what the
    /// house's legitimacy is made of (docs/TITLES.md, "Rungs that are earned" and "What legitimacy is made of").</summary>
    public IReadOnlyList<RungNeed> Needs { get; init; } = Array.Empty<RungNeed>();
    public string Earned { get; init; } = "";
    public LegitimacyCard? Made { get; init; }
}
public sealed record FleetLine(string Id, string Name, string Where, int Strength, int Readiness, bool Moving, string? Destination, int Eta, bool Office,
    string Composition, string? AdmiralName, int Veterancy, IReadOnlyList<Option> AdmiralOptions, IReadOnlyList<Option> MergeOptions, Option Refit,
    string Stance = "line", string Loadout = "guns", int Loyalty = 70, int Supply = 100, string? AdmiralId = null, string AdmiralStyle = "none", int Cap = 0,
    IReadOnlyList<Option>? StanceOptions = null, IReadOnlyList<Option>? LoadoutOptions = null, IReadOnlyList<Option>? StyleOptions = null, int Valour = 0, bool Achilles = false,
    string Away = "", string AwayName = "", string AwayPhase = "", int AwayTurns = 0, int Plunder = 0, int ExpeditionWins = 0, Option? Expedition = null, Option? Recall = null,
    string? CaptainId = null, string? CaptainName = null, string CaptainSays = "", IReadOnlyList<Option>? CaptainOptions = null, Option? RelieveCaptain = null, string Standing = "");
/// <summary>A nation beyond the Reach as the Reach sees it (docs/FOREIGN_NATIONS.md): its strength and regard, the war if there is one, its tribute if it pays.</summary>
public sealed record NationCard(string Id, string Name, string Temper, string Description, string Holds, int Strength, int FullStrength, int Disposition, bool AtWar, int WarSince, int ScoreReach, int ScoreNation,
    int Threshold, int Tribute, int Wealth, int Distance, int SubduedUntil, int Wars, bool RaidedLately, int Expeditions, Option War,
    string Ruler = "", string RulerNature = "", string Commander = "", int CommanderSkill = 0, string Marches = "", string Guard = "", int Nearest = 0,
    bool OnMap = false, bool Fallen = false, string Capital = "", string Worlds = "", string Fleets = "", string Occupied = "", string Lost = "",
    int HoldYears = 3, string Manner = "", string Ransom = "", string Watch = "", string WhoseWar = "", IReadOnlyList<Option>? Orders = null);
/// <summary>A battle about to be joined at the turn's end: our fleets share a system with an enemy's.</summary>
public sealed record BattleBrief(string SystemId, string SystemName, string EnemyHouseId, string EnemyName, int OurHulls, int EnemyHulls, int Confidence, string OurStance, string TheirStance, int Edge, double OurPower, double TheirPower);
/// <summary>The campaign of the house, and the enemy worlds it could name (docs/MILITARY.md).</summary>
public sealed record CampaignCard(string? TargetId, string? TargetName, int TurnsLeft, IReadOnlyList<Option> Targets, int Cost);
/// <summary>A good on the Reach's market as the house sees it (docs/ECONOMY_MARKET.md): the price, what this house would pay and get for
/// one unit, the merchants' stock, the last years' prices, why it moved, and what the house holds and makes of it.</summary>
public sealed record GoodLine(string Id, string Name, double Price, double Base, double BuyUnit, double SellUnit, int Stock, int ChangePct, IReadOnlyList<double> History, string Cause,
    int Held, int NetPerTurn, bool CanBuy, bool CanSell, string? BuyRefusal, string? SellRefusal, int Deficit)
{
    /// <summary>All there is of the good in the Reach: the merchants' stock and every house's store (docs/ECONOMY_MARKET.md, "Cornering a good").</summary>
    public int All { get; init; }
    /// <summary>Who holds the most: the first few, and this house if it holds any.</summary>
    public IReadOnlyList<HolderLine> Holders { get; init; } = Array.Empty<HolderLine>();
    /// <summary>The house that holds a third or more of all there is now, if one does; whether that is half (a corner); its share; what it adds to the price the market aims at.</summary>
    public string CornerHouseId { get; init; } = "";
    public string CornerHouseName { get; init; } = "";
    public bool Cornered { get; init; }
    public bool CornerMine { get; init; }
    public int CornerSharePct { get; init; }
    public int PremiumPct { get; init; }
    /// <summary>The plain word: who holds it, if a house holds a third or more; and what a corner would take this house, or why it cannot be bought.</summary>
    public string CornerHeld { get; init; } = "";
    public string CornerTake { get; init; } = "";
}
/// <summary>One house's store of a good and its share of all there is.</summary>
public sealed record HolderLine(string HouseId, string Name, string Accent, int Units, int SharePct, bool Mine);
/// <summary>The markets panel: every good, whether the house trades at the narrow spread, and whether an embargo closes them.</summary>
/// <summary>A house's public stake in a company: a tenth or more.</summary>
public sealed record StakeLine(string HouseId, string HouseName, string Accent, int Pct);
/// <summary>One listed company as the Exchange shows it (docs/ECONOMY_MARKET.md stage C).</summary>
public sealed record CompanyLine(string Id, string Name, string Sector, string SectorName, double Price, int ChangePct, IReadOnlyList<double> History, int Earnings, int Dividend, int YieldPct,
    int Shares, int Float, int Held, int HeldValue, int Cash, int PayoutPct, string ControllerId, string ControllerName, string ControllerAccent, bool Directed, bool Founded, bool Chartered,
    string Cause, IReadOnlyList<StakeLine> Stakes, IReadOnlyList<string> Ventures, string Description, bool GoldenShare, int OutlookPct, int CapValue, string MonopolyName);
/// <summary>The Exchange: the index, confidence, what moved the market, every listed company, and the house's paper.</summary>
public sealed record ExchangeCard(double Index, IReadOnlyList<double> IndexHistory, int IndexChangePct, int ConfidencePct, IReadOnlyList<string> Movers, IReadOnlyList<CompanyLine> Companies,
    int PaperValue, int DividendsDue, int LevyPct, IReadOnlyList<BondLine> Bonds, BondOffer? BondOffer, string? SteadyRefusal, bool IsTreasurer, bool IsEmperor = false, bool Proscribing = false);
/// <summary>A war bond the house holds (stage D).</summary>
public sealed record BondLine(string Id, string NationName, int Face, int YieldPct, int Bought);
/// <summary>The bond the Reach is selling this year, if it is at war abroad.</summary>
public sealed record BondOffer(string NationId, string NationName, int Face, int YieldPct, int Held, int Cap, string? Refusal);
public sealed record MarketCard(IReadOnlyList<GoodLine> Goods, bool OwnMarket, bool Embargoed, int SpreadPct);
public sealed record MilitaryCard(IReadOnlyList<BattleBrief> Brief, CampaignCard Campaign, string MyStyle, IReadOnlyList<Option> MyStyleOptions, int MyValour, bool MyAchilles, int MyCap, IReadOnlyList<NationCard>? Nations = null);
/// <summary>A generic actionable option: an id the UI turns into a command, a label, and why it is disabled (null = available).</summary>
public sealed record Option(string Id, string Label, string? Refusal);
public sealed record ShipOption(string HoldingId, string ClassId, string Label, string? Refusal);
public sealed record HouseholdLine(string Id, string Name, int Age, string Sex, bool Hidden, bool Married, IReadOnlyList<Option> Actions);
public sealed record SiegeLine(string HoldingName, string BesiegerName, int Turns, int Required, bool Ours);
public sealed record ThroneCard(string? ProclaimRefusal, string? SeizeRefusal, bool HoldsThrone, string? EmperorName);
public sealed record GreatCrisisCard(string Name, string? Stage, int Clock, int? NextAt, string Intensity, string Bloc, bool Resolved, string? HegemonName);
/// <summary>A war with a house. <c>Terms</c> is every peace that could be put to it now, each with what it would do and whether it
/// would be taken (docs/PLAYTHROUGH.md 9).</summary>
public sealed record WarLine(string EnemyHouseId, string EnemyName, int Since, int EnemyHulls, bool PeaceAvailable, string PeaceTooltip, bool TheyAreSuing)
{ public IReadOnlyList<HousePeace.Term> Terms { get; init; } = Array.Empty<HousePeace.Term>(); public int Lead { get; init; } public bool CanPay { get; init; } }
public sealed record BattleLine(string Text);
/// <summary>A choice as offered: <paramref name="Reason"/> is null when available, else why not ("requirements not met" — a trait,
/// skill or rank gate — or "cannot afford: 2 influence").</summary>
/// <summary>One answer to a report. <c>Stakes</c> is what it costs and gives the house in words, <c>Warn</c> what it would overdraw
/// (docs/PLAYTHROUGH.md 8: a choice is not made blind).</summary>
public sealed record ChoiceCard(string Id, string Label, string Cost, bool Available, string? Reason = null, string Stakes = "", string? Warn = null)
{ /// <summary>The same stakes as items, one to a chip (docs/VISUAL_SYSTEM.md 6): the year's pools it spends, then what it does to the
  /// house and the player, each with its icon, signed figure, before and after, and the person it is about.</summary>
  public IReadOnlyList<StakeItem> Items { get; init; } = Array.Empty<StakeItem>();
  /// <summary>Its requirements in words and whether each holds now, for the choice's tooltip.</summary>
  public IReadOnlyList<StakeNeed> Needs { get; init; } = Array.Empty<StakeNeed>(); }
public sealed record BriefingCard(BriefingItem Item, string Title, string Tier, bool Setpiece, string Body, bool Resolved, IReadOnlyList<ChoiceCard> Choices)
{ /// <summary>The words of the answer that was given, once it has been.</summary>
  public string Answer { get; init; } = "";
  /// <summary>Which occasion this report is of: the battle, the ruling and its sum, the office, the world (docs/PLAYTHROUGH.md, reports).</summary>
  public string Facts { get; init; } = ""; }
public sealed record MemberLine(string Name, string Detail, bool Alive, int Disposition, string Bond);
public sealed record ConnectionLine(string Id, string Name, string HouseName, string Detail, int Disposition, string Bond, IReadOnlyList<string> Flags, bool Hostile,
    IReadOnlyList<Option> Actions);
public sealed record DamperOption(string CrisisId, string DamperId, string Label, string Tooltip, bool Available);
public sealed record CrisisLine(string Id, string Name, int Severity, int Since, string? Author, IReadOnlyList<DamperOption> Dampers);
public sealed record CrisisClock(string Counters, IReadOnlyList<CrisisLine> Active, IReadOnlyList<string> Causable);
public sealed record CourtModel(int Turn, string Era, string Mode, int Generation, bool DynastyDead, bool Quiet,
    CharacterCard Character, PoolsLine Pools, TreasuryLine Treasury, IReadOnlyList<HoldingCard> Holdings, StandingCard? Standing,
    IReadOnlyList<FleetLine> Fleets, IReadOnlyList<WarLine> Wars, IReadOnlyList<BriefingCard> Briefing, string HouseName, string HouseAccent,
    IReadOnlyList<MemberLine> Members, IReadOnlyList<ConnectionLine> Connections, CrisisClock? Crises, IReadOnlyList<string> Chronicle,
    IReadOnlyList<HouseholdLine> Household, IReadOnlyList<SiegeLine> Sieges, string? EpilogueText,
    IReadOnlyList<AdvisorReport> Advisors, GreatCrisisCard? GreatCrisis,
    LedgerCard? Ledger = null, IReadOnlyList<BusinessCard>? Businesses = null, IReadOnlyList<VentureOption>? Ventures = null, IReadOnlyList<ClaimOption>? Claims = null,
    IReadOnlyList<ReclaimOption>? Reclaims = null, CapitalCard? Capital = null, Option? Petition = null, IReadOnlyList<Option>? Procure = null, IReadOnlyList<string>? Deposits = null, SenateCard? Senate = null, ChanceryCard? Chancery = null, MilitaryCard? Military = null, MarketCard? Market = null, AssetsCard? Assets = null, ExchangeCard? Exchange = null);

public static class CourtProjection
{
    private static readonly IRng NoRng = new SplitMix64Rng(0);
    public const int ChronicleLines = 10;

    public static CourtModel Build(TurnController tc, ContentDatabase content)
    {
        // nothing changes while a projection is built, so pure questions are answered once (ReadCache)
        bool opened = !ReadCache.Active; if (opened) ReadCache.Open();
        try { return BuildNow(tc, content); } finally { if (opened) ReadCache.Close(); }
    }
    private static CourtModel BuildNow(TurnController tc, ContentDatabase content)
    {
        using var perf_ = Perf.Time("proj:Build");
        var w = tc.World;
        var p = w.Protagonist ?? throw new InvalidOperationException("No protagonist.");
        var house = w.House(p.HouseId) ?? throw new InvalidOperationException($"House '{p.HouseId}' missing.");
        var ctx = new CommandContext(w, NoRng);

        var character = new CharacterCard(
            p.Name, house.Name, house.AccentColor,
            content.Titles.NameOf(house.Title, w.Era),
            content.Careers.RankName(p.CareerTrack, p.CareerRank, w.Era) ?? $"rank {p.CareerRank}",
            content.Careers.ById(p.CareerTrack)?.Name ?? p.CareerTrack,
            p.Age, p.Alive,
            p.NatureTraits.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            p.AptitudeTraits.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            content.Traits.Ambitions.FirstOrDefault(a => a.Id == p.Ambition)?.Name ?? p.Ambition,
            p.Stress, p.Flags.Contains(DynastySystem.BrokenFlag), w.HasFlag(DynastySystem.RegencyFlag),
            p.Flags.FirstOrDefault(f => f.StartsWith("background:")) is { } bg ? content.Backgrounds.ById(bg["background:".Length..])?.Name : null);

        var pools = tc.PlayerPools;
        var t = house.Treasury;

        var holdings = new List<HoldingCard>();
        foreach (var h in w.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var spec = content.Holdings.Spec(h.Specialization);
            var builds = new List<BuildOption>();
            foreach (var b in content.Holdings.Buildings.OrderBy(b => b.Megastructure).ThenBy(b => b.Id, StringComparer.Ordinal))
            {
                if (!b.FitsSpecialization(h.Specialization) || h.Buildings.Contains(b.Id)) continue;
                if (b.RequiresDeposit != null && (w.Galaxy?.System(h.SystemId)?.Deposit(b.RequiresDeposit) ?? 0) == 0) continue;   // nothing here to work
                var cmd = new BuildCommand(p.Id, h.Id, b.Id, content.Holdings, content.Titles);
                // the prospectus: what it would net a year at this year's prices, and how long until it has paid for itself
                double perYear = Economy.WorthOf(w, b.Yield) * (b.RequiresDeposit != null ? Math.Max(1, w.Galaxy?.System(h.SystemId)?.Deposit(b.RequiresDeposit) ?? 1) : 1) - Economy.WorthOf(w, b.Upkeep); double cost = Economy.WorthOf(w, b.Cost);
                string payback = perYear > 0.5 ? $"pays for itself in {Counted.Years((long)(Math.Ceiling(cost / perYear)))}" : "does not pay in coin";
                string years = b.BuildTurns > 1 ? $" · {b.BuildTurns} years, {Bundle(ProjectSystem.Split(b.Cost, b.BuildTurns).First)} a year" : b.BuildTurns == 1 ? " · a year" : "";
                builds.Add(new BuildOption(h.Id, b.Id, $"{(b.Megastructure ? "Raise" : "Build")} {b.Name}  ({Bundle(b.Cost)}{years})",
                    (b.Id == Economy.GranaryId ? $"unrest eases {Economy.GranaryCalm} more each year" : b.Id == "supply_depot" ? "your fleets are in supply within 2 lanes of here, not 1" : $"yield {Bundle(b.Yield)}") + (b.Upkeep.IsEmpty() ? "" : $" · upkeep {Bundle(b.Upkeep)}") + $" · worth {Money.Show(perYear)} credits a year now · {payback}" + (b.MinTitleRank > 0 ? $" · needs the rank of {Progression.Title.StyleOf(Progression.Title.OfRank(b.MinTitleRank), w.Era)}" : "") + (cmd.Refusal(ctx) is { } why ? $" · {why}" : ""), cmd.CanExecute(ctx)));
            }
            bool canRaise = new RaiseFleetCommand(p.Id, h.Id, content.Fleets, content.ShipClasses, null, content.Titles).CanExecute(ctx);
            var ships = new List<ShipOption>();
            foreach (var cls in content.ShipClasses.Defs)
            {
                var rc = new RaiseFleetCommand(p.Id, h.Id, content.Fleets, content.ShipClasses, cls.Id, content.Titles);
                ships.Add(new ShipOption(h.Id, cls.Id, $"Lay down a {cls.Name.ToLowerInvariant()}  ({Bundle(cls.Cost)}) · atk {cls.Attack} / def {cls.Defense}" + (cls.Upkeep.IsEmpty() ? "" : $" · keep {Bundle(cls.Upkeep)} a year") + $" · {content.Fleets.FuelHullsPerUnit} hulls burn 1 helium-3 a year · a new squadron starts at readiness 60", rc.Refusal(ctx)));
            }
            holdings.Add(new HoldingCard(h.Id, h.Name, spec?.Name ?? h.Specialization, w.Galaxy?.System(h.SystemId)?.Name,
                h.Population, spec is { MaxPopulation: > 0 } ? spec.MaxPopulation + (h.IsPlanet && SystemWorks.Any(w) ? SystemWorks.Population(w, content.Holdings, h.SystemId) : 0) : 0, h.Unrest, SystemWorks.WorldSlotsUsed(w, content.Holdings, h), SystemWorks.WorldSlots(w, content.Holdings, h),   // one count, the system panel's: what stands, what is rising and the works on the world (docs/PLAYTHROUGH.md, buildings)
                h.Buildings.Select(b => content.Holdings.Building(b)?.Name ?? b).ToList(), builds,
                canRaise, $"Lay down a hull at {h.Name}  ({Bundle(content.Fleets.ShipCost)})", ships, w.Sieges.ContainsKey(h.Id),
                h.Kind, w.Char(h.GovernorId) is { } gov ? (gov.Alive ? gov.Name : $"nobody: {gov.Name} is dead, and the chair is empty") : null,   // a dead governor is not left in the chair on the screen
                w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.Id != h.GovernorId).OrderByDescending(c => c.Skill("administration")).ThenBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => new Option(c.Id, $"Name {c.Name} governor of {h.Name}  (1 influence · administration {c.Skill("administration")})", new AppointGovernorCommand(p.Id, h.Id, c.Id).Refusal(ctx))).ToList(),
                h.Culture,
                h.Prosperity, tc.Economy?.ProsperityTarget(w, h) ?? 50,
                new Option("invest", $"Invest in {h.Name}  ({Money.Credits(content.Businesses.Finance.InvestCredits)} → +{content.Businesses.Finance.InvestProsperity} prosperity: the world's yields are scaled by its prosperity, and it drifts back toward what its buildings and governor support by 5 a year)", new InvestCommand(p.Id, h.Id, content.Businesses).Refusal(ctx)),
                w.Galaxy?.System(h.SystemId)?.Belt ?? 0,
                h.GovernorId.Length > 0 && h.GovernorId != p.Id ? w.GetRelationship(h.GovernorId, p.Id)?.Disposition ?? 0 : 100, w.Char(h.GovernorId)?.Skill("administration") ?? 0, w.Char(h.GovernorId)?.NatureTraits.Contains("Ambitious") == true));
        }

        // the ledger, the ventures and the belts (GDD §17)
        LedgerCard? ledger = null; var businessCards = new List<BusinessCard>(); var ventureOptions = new List<VentureOption>(); var claimOptions = new List<ClaimOption>();
        if (tc.Economy is { } eco)
        {
            var bc = content.Businesses; var f = bc.Finance;
            var lg = eco.Compute(w, house.Id, tc.Fleets);
            if (house.Wealth == 0 && w.Houses.Values.All(h => h.Wealth == 0)) eco.Reckon(w);
            var table = Wealth.Ranked(w).Select(x => new WealthLine(x.house.Id, x.house.Name, x.house.AccentColor, x.wealth, Wealth.TierFor(w, x.house.Id), x.house.Id == house.Id)).ToList();
            var lender = house.LenderId.Length > 0 ? w.House(house.LenderId)?.Name : null;
            var (standLent, standOwes) = SenateActs.StandingLoan(w, f, house.Id); int standEach = Math.Min(standOwes, f.LoanInstalment);   // the standing loan as the Usury Law leaves it
            ledger = new LedgerCard(lg.Lines, lg.Net, house.Wealth, Wealth.RankOf(w, house.Id), w.Houses.Keys.Count(h => !World.Outsider(h)), Wealth.TierFor(w, house.Id), house.Debt, lender, standLent, standOwes, house.Debt > 0 ? Lending.InstalmentOf(w, house, f) : standEach,
                new Option("loan", $"Borrow {Money.Credits(standLent)} (owe {Money.Show(standOwes)}, {Money.Show(standEach)} a year)", new TakeLoanCommand(p.Id, bc).Refusal(ctx)),
                new Option("repay", $"Clear the debt ({Money.Credits(house.Debt)})", new RepayLoanCommand(p.Id).Refusal(ctx)), table);
            var vs = tc.Ventures;
            foreach (var b in w.BusinessesOf(house.Id))
            {
                var def = bc.Def(b.Kind); if (def == null) continue;
                var delta = eco.VentureLine(w, b);
                var holder = w.PlanetHolder(b.SystemId); int value = eco.VentureValue(w, b);
                var bids = vs == null || b.CompanyId.Length > 0 ? new List<VentureBid>() : vs.Bidders(w, b).Take(3).Select(x => new VentureBid(x.house.Id, x.house.Name, x.house.AccentColor, x.bid, w.PlanetHolder(b.SystemId) == x.house.Id)).ToList();
                bool enlarging = ProjectSystem.Underway(w, house.Id, Project.Expansion, b.Kind, b.Id);
                string expandCost = def.BuildTurns > 1 ? $"{Bundle(def.ExpandCost)} over {def.BuildTurns} years" : Bundle(def.ExpandCost);
                businessCards.Add(new BusinessCard(b.Id, b.Name, def.Name, w.Galaxy?.System(b.SystemId)?.Name, b.Level, def.MaxLevel, delta.IsEmpty() ? "—" : Bundle(delta),
                    holder != null && holder != house.Id && def.Charter > 0 ? $"{Money.Credits(def.Charter * b.Level)} a year to {w.House(holder)?.Name}" : null, value,
                    new Option("expand", b.Level >= def.MaxLevel ? "At its height" : enlarging ? "Being enlarged" : $"Expand to level {b.Level + 1}  ({expandCost})", new ExpandBusinessCommand(p.Id, b.Id, bc).Refusal(ctx)),
                    new Option("sell", $"Sell on the open market for {Money.Credits(vs != null ? vs.MarketPrice(w, b) : value)}", vs == null ? "no market for ventures" : new SellVentureCommand(p.Id, b.Id, "", vs).Refusal(ctx)),
                    Earnings: eco.VentureEarnings(w, b), MarketPrice: vs != null ? vs.MarketPrice(w, b) : value, Bids: bids, CompanyName: b.CompanyId.Length > 0 ? w.Company(b.CompanyId)?.Name : null, Enlarging: enlarging));
            }
            if (w.Galaxy != null)
            {
                var own = w.HoldingsOf(house.Id).Select(h => h.SystemId).ToHashSet();
                foreach (var def in bc.Defs)
                    foreach (var sys in w.Galaxy.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
                    {
                        var refusal = new FoundBusinessCommand(p.Id, def.Id, sys.Id, bc, content.Holdings, content.Titles).Refusal(ctx);
                        if (refusal != null && !own.Contains(sys.Id)) continue;
                        var (perYear, payback) = vs != null ? vs.Prospectus(w, house.Id, def, sys.Id) : (0, double.PositiveInfinity);
                        ventureOptions.Add(new VentureOption(def.Id, def.Name, def.Description, sys.Id, sys.Name, Bundle(def.Cost), refusal, perYear, double.IsInfinity(payback) ? "never, at these prices" : $"{Counted.Years((long)(Math.Ceiling(payback)))}",
                            def.BuildTurns, Bundle(ProjectSystem.Split(def.Cost, def.BuildTurns).First), def.Sector) { Swing = SwingOf(def) });
                    }
                foreach (var sys in w.Galaxy.Systems.Values.Where(s => s.Belt > 0).OrderBy(s => s.Id, StringComparer.Ordinal))
                    claimOptions.Add(new ClaimOption(sys.Id, sys.Name, sys.Belt, w.Holdings.Values.Count(h => h.IsClaim && h.SystemId == sys.Id), new StakeClaimCommand(p.Id, sys.Id, content.Holdings, bc).Refusal(ctx)));
            }
        }

        StandingCard? standing = null;
        if (tc.Progression is { } prog)
        {
            var next = prog.NextTitle(w, house.Id);
            var paths = new List<PathOption>();
            if (next != null)
            {
                string rung = next.NameIn(w.Era);
                string? Why(string? rule, bool afford, string cost) => rule ?? (afford ? null : "needs " + cost);
                bool sz = new SeizeTitleCommand(p.Id, prog).CanExecute(ctx), pt = new PetitionTitleCommand(p.Id, prog).CanExecute(ctx), cl = new ClaimTitleCommand(p.Id, prog).CanExecute(ctx);
                paths.Add(new PathOption("seize", "Seize it by force", "By arms: the house's own ships behind it. Legitimacy does not rise with the rank, and every house fears you the more.", sz)
                { Cost = "2 treasury actions, 1 agent", Refusal = sz ? null : Why(prog.SeizeRefusal(w, p.Id), false, "2 treasury actions and 1 agent this year"), Confirm = $"The house takes the rank of {rung} by force. Its legitimacy stays where it is, so the rank stands above what the house is thought to deserve, and every house fears it the more. One rung a year." });
                bool voted = prog.Voted(next);
                paths.Add(new PathOption("petition", voted || !w.IsImperium ? "Petition the Senate" : "Petition the throne", voted ? "By merit: the petition goes before the Senate, which votes on it at the end of next year. The house rises only if it is carried. It gives no legitimacy." : "By merit: granted to a house thought to deserve it. It gives no legitimacy.", pt)
                {
                    Cost = "2 influence of this year's", Refusal = pt ? null : Why(prog.PetitionRefusal(w, p.Id), false, "2 influence of this year's"),
                    Confirm = voted ? $"The house's petition for the rank of {rung} goes before the Senate. It is voted at the end of next year like any motion: the houses may be lobbied until then, and the house rises only if it is carried. Thrown out, the house may ask again in {Counted.Years(ProgressionSystem.AskAgainYears - 1)} after the vote. Its legitimacy stays as it is either way."
                        : $"The house is granted the rank of {rung}. Its legitimacy stays as it is. One rung a year."
                });
                paths.Add(new PathOption("claim", "Press a claim", "By a claim the house holds: it launders the rise, and leaves a trail.", cl)
                { Cost = "1 influence of this year's, 2 agents", Refusal = cl ? null : Why(prog.ClaimRefusal(w, p.Id), false, "1 influence of this year's and 2 agents"), Confirm = $"The house presses its claim and takes the rank of {rung} (+12 legitimacy). The claim is spent, and the way it was pressed leaves a trail. One rung a year." });
            }
            var honors = new HonorSystem(content.Honors).Held(p).Select(h => h.Name).ToList();
            var laws = SetSuccessionLawCommand.Laws.Select(l => new Option(l, l, new SetSuccessionLawCommand(p.Id, l).CanExecute(ctx) ? null : (house.SuccessionLaw == l ? "current law" : "no influence"))).ToList();
            var taxes = content.Holdings.TaxPolicies.Select(t => new Option(t.Id, $"{t.Name} ({Money.Signed(t.CreditsBonus)} credits/holding, unrest {t.UnrestDrift:+#;-#;0})", new SetTaxPolicyCommand(p.Id, t.Id, content.Holdings).CanExecute(ctx) ? null : "current")).ToList();
            var exchange = content.Holdings.Rates.Select(r => new Option($"{r.Give}>{r.Get}", $"{Money.Of(r.Give, r.GiveAmount)} {Resource.Label(r.Give)} → {Money.Of(r.Get, ExchangeCommand.Received(w, house.Id, r, content.Holdings, 1))} {Resource.Label(r.Get)}" + (ExchangeCommand.HasExchange(w, house.Id, content.Holdings) ? "" : " (half: the house has no exchange of its own)"),
                new ExchangeCommand(p.Id, r, content.Holdings).CanExecute(ctx) ? null : $"needs {Money.Of(r.Give, r.GiveAmount)} {Resource.Label(r.Give)} (the house has {Money.Of(r.Give, house.Treasury.Get(r.Give))})")).ToList();
            var emperor = ThroneSystem.Emperor(w);
            var throne = new ThroneCard(new ProclaimImperiumCommand(p.Id, content.Titles).Refusal(ctx), new SeizeThroneCommand(p.Id, content.Titles, content.Friction).Refusal(ctx),
                house.Title == "emperor", emperor?.Name);
            standing = new StandingCard(house.Legitimacy, w.Counter("title_instability"),
                house.Claims.OrderBy(c => c, StringComparer.Ordinal).ToList(), next?.NameIn(w.Era), next?.LegitimacyRequirement ?? 0, paths,
                string.Join(" · ", house.SphereInfluence.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")),
                w.Counter("threat"), honors, house.SuccessionLaw, laws, house.TaxPolicy, taxes, exchange, throne)
            { Needs = LegitimacyProjection.Needs(w, house, prog), Earned = LegitimacyProjection.Says(w, house, prog), Made = LegitimacyProjection.Of(w, house) };
        }

        var fleetSys = tc.Fleets; var abroad = NationSystem.AtWar(w);
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal)
            .Select(f => new FleetLine(f.Id, f.Name, f.IsAway ? "beyond the Reach" : w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId, f.Strength, f.Readiness, f.Moving,
                f.Moving ? w.Galaxy?.System(f.DestinationId)?.Name ?? f.DestinationId : null, f.Eta, f.Office,
                fleetSys.Describe(f), f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId)?.Name : null, f.Veterancy,
                w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority).OrderByDescending(c => c.Skill("tactics")).ThenBy(c => c.Id, StringComparer.Ordinal)   // no child is offered a fleet
                    .Select(c => new Option(c.Id, $"Give {f.Name} to {c.Name} (tactics {c.Skill("tactics")}, {c.Age}){(w.Fleets.Values.FirstOrDefault(x => x.Id != f.Id && x.AdmiralId == c.Id) is { } has ? $", who shifts flag from {has.Name} and leaves it without an admiral" : "")}{(w.Fleets.Values.FirstOrDefault(x => x.CaptainId == c.Id) is { } ship ? $", who leaves the captaincy of {ship.Name}" : "")} · 1 agent", new AssignAdmiralCommand(p.Id, f.Id, c.Id).Refusal(ctx))).ToList(),
                w.Fleets.Values.Where(o => o.Id != f.Id && o.OwnerId == house.Id).OrderBy(o => o.Id, StringComparer.Ordinal)
                    .Select(o => new Option(o.Id, $"Fold {o.Name} into {f.Name} · {((FleetSystem.AdmiralOf(w, f) ?? FleetSystem.AdmiralOf(w, o)) is { } keeps ? keeps.Name + " commands" : "no admiral")}", new MergeFleetsCommand(p.Id, o.Id, f.Id).Refusal(ctx))).ToList(),
                new Option("refit", $"Refit {f.Name}  ({RefitFleetCommand.MaterialsPerHull * f.Strength} materials → readiness 100)", new RefitFleetCommand(p.Id, f.Id).Refusal(ctx)),
                StanceOptions: FleetSystem.Stances.Select(st => new Option(st, st, new SetStanceCommand(p.Id, f.Id, st).Refusal(ctx))).ToList(),
                LoadoutOptions: FleetSystem.Loadouts.Select(l => new Option(l, l, new SetLoadoutCommand(p.Id, f.Id, l).Refusal(ctx))).ToList(),
                Stance: f.Stance, Loadout: f.Loadout, Loyalty: f.Loyalty, Supply: f.Supply, AdmiralId: f.AdmiralId.Length > 0 ? f.AdmiralId : null,
                AdmiralStyle: f.AdmiralId.Length > 0 && w.Char(f.AdmiralId) is { } adm ? SetCommandStyleCommand.StyleOf(adm) : "none", Cap: f.AdmiralId.Length > 0 ? fleetSys.CommandCap(w.Char(f.AdmiralId)) : 0,
                StyleOptions: f.AdmiralId.Length > 0 ? new[] { "front", "flag", "none" }.Select(st => new Option(st, st, new SetCommandStyleCommand(p.Id, f.AdmiralId, st).Refusal(ctx))).ToList() : null,
                Valour: f.AdmiralId.Length > 0 ? w.Counter(FleetSystem.ValourKey(f.AdmiralId)) : 0, Achilles: f.AdmiralId.Length > 0 && (w.Char(f.AdmiralId)?.Flags.Contains(FleetSystem.AchillesFlag) ?? false),
                Away: f.Away, AwayName: f.IsAway ? content.Nations.Def(f.Away)?.Name ?? f.Away : "", AwayPhase: f.AwayPhase, AwayTurns: f.AwayTurns, Plunder: f.Plunder, ExpeditionWins: f.ExpeditionWins,
                Expedition: abroad != null && !f.IsAway && !NationSystem.OnMap(w, abroad.Id) ? new Option(abroad.Id, $"Send against {content.Nations.Def(abroad.Id)?.Name ?? abroad.Id}", new SendExpeditionCommand(p.Id, f.Id, abroad.Id, content.Fleets, content.Nations).Refusal(ctx)) : null,
                Recall: f.IsAway ? new Option(f.Id, "Recall", new RecallExpeditionCommand(p.Id, f.Id, content.Nations).Refusal(ctx)) : null,
                // the captain under the admiral, the officers of the service who could be one, and where the fleet stands as a whole (docs/MILITARY.md, "Commanding a navy")
                CaptainId: FleetSystem.CaptainOf(w, f)?.Id, CaptainName: FleetSystem.CaptainOf(w, f)?.Name,
                CaptainSays: FleetSystem.CaptainOf(w, f) is { } cap ? $"{content.Careers.RankName(cap.CareerTrack, cap.CareerRank, w.Era) ?? "officer"} · discipline {cap.Skill("discipline")}, gunnery {cap.Skill("gunnery")}: readiness +{FleetSystem.CaptainDrills(w, f)} a year at anchor, {FleetSystem.CaptainHolds(w, f)} less lost a year under way"
                    + (FleetSystem.Commander(w, f) == cap ? " · commands the fleet" : $" · +{(FleetSystem.CaptainFactor(w, f) - 1) * 100:0}% in a fight by gunnery") : "",
                CaptainOptions: w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.CareerTrack == "military").OrderByDescending(c => c.CareerRank).ThenByDescending(c => c.Skill("discipline")).ThenBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => new Option(c.Id, $"Make {c.Name} captain ({content.Careers.RankName(c.CareerTrack, c.CareerRank, w.Era) ?? "officer"}, {c.Age} · discipline {c.Skill("discipline")}, gunnery {c.Skill("gunnery")}){(w.Fleets.Values.FirstOrDefault(x => x.Id != f.Id && x.CaptainId == c.Id) is { } leaves ? $", who leaves {leaves.Name} without a captain" : "")} · 1 agent", new AppointCaptainCommand(p.Id, f.Id, c.Id).Refusal(ctx))).ToList(),
                RelieveCaptain: new Option("relieve", "Relieve the captain", new RelieveCaptainCommand(p.Id, f.Id).Refusal(ctx)),
                Standing: fleetSys.Stands(w, f))).ToList();

        var wars = new List<WarLine>();
        foreach (var enemy in w.EnemiesOf(house.Id))
        {
            var war = w.Wars.FirstOrDefault(x => x.Between(house.Id, enemy)); if (war == null) continue;   // a war with a nation is the Reach's, and is shown with the nations
            var peace = new SueForPeaceCommand(p.Id, enemy, content.Fleets);
            bool can = peace.CanExecute(ctx);
            bool refuses = !tc.Fleets.WouldAcceptPeace(w, enemy, house.Id);
            bool suing = w.LivingCharacters().Any(c => c.HouseId == enemy && c.Flags.Contains(FleetSystem.SuingForPeaceFlag));
            wars.Add(new WarLine(enemy, w.House(enemy)?.Name ?? enemy, war.Since, FleetSystem.StrengthOf(w, enemy), can,
                can ? "" : refuses ? "They smell victory: their hulls far outnumber yours." : "No Influence left this turn.", suing)
                { Terms = HousePeace.Terms(w, tc.Fleets, content.Fleets, house.Id, enemy), Lead = war.Lead(house.Id), CanPay = pools.Get(Pool.Influence) >= content.Fleets.PeaceCost });
        }

        var briefing = new List<BriefingCard>();
        foreach (var item in tc.Briefing)
        {
            var fired = item.Fired;
            var text = content.Text(fired.Def.Id);
            var ectx = new EvalContext(w, fired.Binding, NoRng, content);
            var choices = item.Resolved ? new List<ChoiceCard>() : tc.Offer(item).Select(oc =>
            {
                string cost = oc.Choice.Cost.Count == 0 ? "" : string.Join(", ", oc.Choice.Cost.Select(c => $"{c.Value} {c.Key}"));
                string? reason = oc.Available ? null
                    : !tc.Engine.IsChoiceAvailable(fired, w, oc.Choice.Id) ? ChoiceStakes.Unmet(oc.Choice, ectx)
                    : "this year's " + string.Join(" and ", oc.Choice.Cost.Where(c => pools.Get(c.Key) < c.Value).Select(c => $"{c.Key} is spent ({pools.Get(c.Key)} left of the {c.Value} it takes)"));
                // the stakes as items (docs/VISUAL_SYSTEM.md 6): what the year's pools give up first, then what the choice does
                var items = oc.Choice.Cost.Where(c => c.Value != 0).Select(c => new StakeItem("pool:" + c.Key, ChoiceStakes.PoolIcon(c.Key), -c.Value, Counted.Figure(-c.Value),
                    (c.Key == Pool.Treasury ? Counted.TreasuryActions(c.Value) : c.Key == Pool.Agents ? Counted.Agents(c.Value) : $"{c.Value} {c.Key}") + " this year", false, pools.Get(c.Key).ToString(), Counted.Figure(pools.Get(c.Key) - c.Value))).Concat(ChoiceStakes.Items(oc.Choice, ectx, p)).ToList();
                return new ChoiceCard(oc.Choice.Id,
                    text != null && text.Choices.TryGetValue(oc.Choice.Id, out var ct) ? TextRenderer.Substitute(ct, ectx) : oc.Choice.Id,
                    cost, oc.Available, reason, ChoiceStakes.Of(oc.Choice, ectx, p), ChoiceStakes.Overdraws(oc.Choice, ectx, p))
                    { Items = items, Needs = ChoiceStakes.Needs(oc.Choice, ectx) };
            }).ToList();
            briefing.Add(new BriefingCard(item, TextRenderer.Substitute(text?.Title ?? fired.Def.Id, ectx), fired.Def.Tier.ToString().ToUpperInvariant(),
                fired.Def.Tier == EventTier.Setpiece, string.IsNullOrWhiteSpace(text?.Body) ? "" : TextRenderer.Substitute(text!.Body, ectx), item.Resolved, choices)
                { Facts = ReportFacts.For(w, fired.Def, content), Answer = item.Resolved && item.ChosenId.Length > 0 ? (text != null && text.Choices.TryGetValue(item.ChosenId, out var answered) ? TextRenderer.Substitute(answered, ectx) : item.ChosenId) : "" });
        }

        var members = house.Members.Select(w.Char).Where(c => c != null && c!.Id != p.Id).Select(c => c!)
            .Select(c => { var rel = w.GetRelationship(c.Id, p.Id); return new MemberLine(c.Name,
                c.Alive ? $"{content.Careers.RankName(c.CareerTrack, c.CareerRank, w.Era) ?? $"rank {c.CareerRank}"}, {c.Age}" : "dead",
                c.Alive, rel?.Disposition ?? 0, BondName(rel)); }).ToList();

        var suitors = w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.Sex != "" && !ProposeMarriageCommand.IsMarried(w, c.Id))
                      .OrderByDescending(c => c.Id == p.Id).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var suitor = suitors.FirstOrDefault();
        var connections = w.LivingCharacters().Where(c => c.HouseId != house.Id).OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c =>
            {
                var rel = w.GetRelationship(c.Id, p.Id); int d = rel?.Disposition ?? 0;
                var actions = new List<Option>();
                // no match and no knife is offered against a child (GDD §14): the court does not list what the house would not do
                if (c.Age >= DynastySystem.AgeOfMajority)
                {
                    // a choice of who (docs/PLAYTHROUGH.md, Medium, Family, F11): each of the house who could be matched with them, yourself first
                    var could = suitors.Where(s => s.Sex != c.Sex).Take(4).ToList(); if (could.Count == 0 && suitor != null) could.Add(suitor);
                    foreach (var s in could)
                    {
                        var pm = new ProposeMarriageCommand(p.Id, s.Id, c.Id, content.Titles);
                        actions.Add(new Option("marry:" + s.Id, $"Propose {(s.Id == p.Id ? "yourself" : s.Name)} ⚭ {c.Name} by treaty  (2 influence)", pm.Refusal(ctx)));
                    }
                    var kill = new AssassinateCommand(p.Id, c.Id, content.Friction);
                    actions.Add(new Option("assassinate", $"Arrange an accident  (2 agents · {AssassinateCommand.Odds(w, p, c):P0})", kill.CanExecute(ctx) ? null : "unreachable, or no agents"));
                }
                return new ConnectionLine(c.Id, c.Name, w.House(c.HouseId)?.Name ?? c.HouseId, content.Careers.RankName(c.CareerTrack, c.CareerRank, w.Era) ?? (c.Age < DynastySystem.AgeOfMajority ? $"child, {c.Age}" : $"{c.Age}, untitled"), d, BondName(rel),
                    c.Flags.Where(f => !f.StartsWith("background:")).OrderBy(f => f, StringComparer.Ordinal).ToList(), d < -20, actions);
            }).ToList();

        var household = w.Characters.Values.Where(c => c.Alive && c.HouseId == house.Id && c.Id != p.Id).OrderBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c =>
            {
                var acts = new List<Option>();
                if (c.Flags.Contains(FamilySystem.DefiantFlag)) acts.Add(new Option("defiant", $"{c.Name} is defiant; answer it in the briefing", "informational"));
                if (c.Age < DynastySystem.AgeOfMajority)
                    acts.Add(c.Flags.Contains(DynastySystem.HiddenFlag)
                        ? new Option("recall", $"Recall {c.Name} {(w.IsImperium ? "to court" : "home")}", new RecallCommand(p.Id, c.Id).CanExecute(ctx) ? null : "")
                        : new Option("foster", $"Foster {c.Name} away, hidden  (1 agent)", new FosterCommand(p.Id, c.Id).CanExecute(ctx) ? null : "no agents"));
                return new HouseholdLine(c.Id, c.Name, c.Age, c.Sex, c.Flags.Contains(DynastySystem.HiddenFlag), ProposeMarriageCommand.IsMarried(w, c.Id), acts);
            }).ToList();

        /* the sieges that are the house's business: its own worlds invested, and the ones its own fleets are laying. Strangers' sieges are the map's to show. */
        var sieges = w.Sieges.Values.Where(s => s.BesiegerId == house.Id || w.Holding(s.HoldingId)?.OwnerId == house.Id).OrderBy(s => s.HoldingId, StringComparer.Ordinal)
            .Select(s => new SiegeLine(w.Holding(s.HoldingId)?.Name ?? s.HoldingId, w.House(s.BesiegerId)?.Name ?? s.BesiegerId, s.Turns, s.Required, w.Holding(s.HoldingId)?.OwnerId == house.Id)).ToList();
        // the story is told when the line ends, or while the player's own house sits a throne it took: not because some other house took one (docs/THRONE.md)
        string? epilogue = w.IsDynastyDead || (w.HasFlag(ThroneSystem.ProclaimedFlag) || w.HasFlag(ThroneSystem.SeizedFlag)) && ThroneSystem.Emperor(w)?.Id == house.Id ? Epilogue.Generate(w, content) : null;
        var advisors = AdvisorReports.Build(w, content);
        GreatCrisisCard? great = null;
        if (content.GreatCrisis is { } gc)
        {
            var stage = tc.GreatCrisis.CurrentStage(w);
            int idx = w.WorldCounters.TryGetValue(GreatCrisisSystem.StageCounter, out var si) ? si : -1;
            var hegemon = w.Houses.Values.FirstOrDefault(h => h.Bloc == GreatCrisisSystem.HegemonBloc);
            great = new GreatCrisisCard(gc.Name, stage?.Id, w.Counter(GreatCrisisSystem.ClockCounter),
                idx + 1 < gc.Stages.Count ? gc.Stages[idx + 1].At : null, w.CrisisIntensity, house.Bloc, w.HasFlag(GreatCrisisSystem.ResolvedFlag), hegemon?.Name);
        }

        CrisisClock? clock = null;
        if (tc.Crises is { } crises)
        {
            var active = new List<CrisisLine>();
            foreach (var a in w.Crises.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                var def = crises.Def(a.Id);
                var dampers = new List<DamperOption>();
                if (def != null)
                    foreach (var d in def.Dampers)
                    {
                        bool can = new ApplyDamperCommand(p.Id, crises, def, d).CanExecute(ctx);
                        dampers.Add(new DamperOption(def.Id, d.Id, $"{d.Name} (−{d.Relief})" + (d.Cost.Count > 0 ? $"  [{string.Join(", ", d.Cost.Select(c => $"{c.Value} {c.Key}"))}]" : ""),
                            can ? $"Takes {d.Relief} from its severity. It does this: {CrisisFacts.Does(d.Effects)}." : !ctx.PoolsOf(p.Id).CanAfford(d.Cost) ? $"You cannot pay for it this year: it costs {string.Join(", ", d.Cost.Select(c => $"{c.Value} {c.Key}"))}." : "Closed to you now: what it needs does not hold. The lines under the crisis say what.", can));
                    }
                active.Add(new CrisisLine(a.Id, def?.Name ?? a.Id, a.Severity, a.TurnStarted, a.Organic ? null : w.Char(a.AuthorId)?.Name ?? a.AuthorId, dampers));
            }
            clock = new CrisisClock(
                $"unrest {w.Counter("unrest")} · corruption {w.Counter("corruption")} · goodwill {w.Counter("goodwill")} · coalition pressure {w.Counter("coalition_pressure")} · incursions {w.Counter(FleetSystem.IncursionsCounter)} · battles {w.Counter(FleetSystem.BattlesCounter)} · conquests {w.Counter(FleetSystem.ConquestsCounter)}",
                active, crises.Causable(w, NoRng).Select(c => c.Name).ToList());
        }

        var chronicle = w.History.Skip(Math.Max(0, w.History.Count - ChronicleLines)).Reverse().Select(h => $"T{h.Turn}  {h.Text}").ToList();

        return new CourtModel(w.Turn, w.Era, w.Mode, w.Counter("generation") + 1, w.IsDynastyDead, tc.Briefing.Count == 0,
            character, new PoolsLine(pools.Influence, pools.Treasury, pools.Agents),
            new TreasuryLine(t.Credits, t.Materials, t.Manpower, t.Influence, t.Exotics, t.Credits < 0, t.Helium3, t.RareEarths, t.Isotopes),
            holdings, standing, fleets, wars, briefing, house.Name, house.AccentColor, members, connections, clock, chronicle, household, sieges, epilogue, advisors, great,
            ledger, businessCards, ventureOptions, claimOptions,
            w.Holdings.Values.Where(h => h.FormerOwnerId == house.Id && w.House(h.OwnerId)?.Bloc == RegionSystem.FreeholdsBloc).OrderBy(h => h.Id, StringComparer.Ordinal)
                .Select(h => new ReclaimOption(h.Id, h.Name, w.House(h.OwnerId)!.Name, ReclaimFreeholdCommand.Price(w, h, house.Id), new ReclaimFreeholdCommand(p.Id, h.Id).Refusal(ctx))).ToList(),
            w.Galaxy is { Capital.Length: > 0 } cg ? new CapitalCard(cg.System(cg.Capital)?.Name ?? cg.Capital, RegionSystem.CapitalHolder(w)?.Id, RegionSystem.CapitalHolder(w)?.Name, RegionSystem.CapitalHolder(w)?.Id == house.Id) : null,
            new Option("petition_fief", $"Petition {PetitionFiefCommand.Grantor(w, house.Id)?.Name ?? (w.IsImperium ? "the court" : "the chair")} for a {(w.IsImperium ? "fief" : "prefecture")}  (3 influence)", new PetitionFiefCommand(p.Id).Refusal(ctx)),
            Resource.Strategic.Where(r => r != Resource.Isotopes).Select(r => { var bc = new BuyGoodCommand(p.Id, r, 3, content.Trade); var q = bc.Quote(w); return new Option("procure_" + r, $"Buy 3 {Resource.Label(r)} on the market  ({(q.Credits > 0 ? Money.Show(q.Credits) : "—")} credits)", q.Refusal); }).ToList(),
            Economy.Deposits(w, house.Id).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            BuildSenate(w, p, house, tc.Senate, ctx, tc.Fleets),
            BuildChancery(w, p, house, content.Policies, ctx), Military: BuildMilitary(w, p, house, tc.Fleets, content.Fleets, ctx, content.Nations, tc.Senate), Market: BuildMarket(w, p, house, content, tc), Assets: BuildAssets(w, p, house, content, tc, ctx), Exchange: BuildExchange(w, p, house, content, tc));
    }

    private static string BondName(Relationship? rel) => rel?.Bond is { } b && b != BondType.None ? b.ToString().ToLowerInvariant() : "";

    private static string HastenYear(Project x, int pct, bool extraOnly)
    {
        var (due, extra) = ProjectSystem.HastenDue(x, pct); if (extraOnly) return Bundle(extra);
        var all = due.Clone(); foreach (var r in Resource.All) all.Add(r, extra.Get(r)); return Bundle(all);
    }
    /// <summary>A venture whose earnings are a bet says what on (docs/UPSTART.md, U2): the figure in the list is this year's, and a
    /// warehouse or a shipyard that shows nothing this year is not broken, it is waiting.</summary>
    public static string SwingOf(BusinessDef def)
    {
        var parts = new List<string>();
        if (def.WarCredits != 0) parts.Add($"{Money.Signed(def.WarCredits)} a level for each war being fought");
        if (def.PeaceCredits > 0) parts.Add($"{Money.Signed(def.PeaceCredits)} a level while there is no war");
        if (def.SpikeCredits > 0) parts.Add($"{Money.Signed(def.SpikeCredits)} a level for each good whose price is spiking");
        return string.Join(" · ", parts);
    }
    public static string Bundle(Resources r)
    {
        var parts = Resource.All.Where(k => r.Get(k) != 0).Select(k => $"{Money.Of(k, r.Get(k))} {Resource.Label(k)}").ToList();
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    /// <summary>The docket and the history with every house's stance, and the motions the player could move with their legal targets.</summary>
    private static SenateCard BuildSenate(World w, Character p, House house, SenateSystem senate, CommandContext ctx, FleetSystem? fleets = null)
    {
        using var perf_ = Perf.Time("proj:BuildSenate");
        MotionCard Card(Motion m)
        {
            var def = senate.Motions.Def(m.Kind); var proposer = w.House(m.ProposerHouseId);
            string? targetName = def?.Target == "house" ? w.House(m.TargetId)?.Name : def?.Target == "region" ? w.Galaxy?.Regions.FirstOrDefault(r => r.Id == m.TargetId)?.Name : def?.Target == "edict" ? EdictSystem.Title(w, m.TargetId) : null;
            var stances = new List<StanceLine>();
            foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != house.Id).OrderByDescending(h => senate.Weight(w, h.Id)).ThenBy(h => h.Id, StringComparer.Ordinal))
                stances.Add(new StanceLine(h.Id, h.Name, h.AccentColor, senate.Weight(w, h.Id), senate.Stance(w, m, h.Id), m.Votes.ContainsKey(h.Id),
                    m.Open ? new LobbyCommand(p.Id, m.Id, h.Id, 1).Refusal(ctx) : "decided", m.Open ? new LobbyCommand(p.Id, m.Id, h.Id, 1, bribe: true).Refusal(ctx) : "decided",
                    FactionSystem.Of(w, h.Id)?.Name, FactionSystem.Leads(w, h.Id)) { BribePrice = SenateSystem.BribeCost(w, h, p) });
            var (f, a) = m.Open ? senate.Tally(w, m) : (m.ForWeight, m.AgainstWeight);
            bool againstMe = m.TargetId == house.Id && def != null && (def.LegitimacyTarget < 0 || def.EmbargoTurns > 0);
            return new MotionCard(m.Id, m.Kind, def?.Name ?? m.Kind, senate.ProposerName(w, m), SenateSystem.IsPetition(m) ? SenateSystem.EstateAccent : proposer?.AccentColor ?? "#888", targetName, senate.Describe(w, m) + (senate.Ground(w, m) is { Length: > 0 } ground ? " " + ground : "") + (m.Open ? ActToYou(w, house, senate, m, fleets) : ""), m.ProposedTurn, m.Open, m.Passed,
                f, a, m.Votes.GetValueOrDefault(house.Id), m.ProposerHouseId == house.Id, againstMe, stances, SenateSystem.IsPetition(m),
                m.Open ? new VetoCommand(p.Id, m.Id, senate.Offices, senate).Refusal(ctx) : "decided", m.Vetoed, m.Vetoed ? w.House(m.VetoedBy)?.Name ?? m.VetoedBy : null) { Swing = m.Swing };
        }
        var docket = SenateSystem.Docket(w).Select(Card).ToList();
        var history = w.Motions.Where(m => m.Resolved).OrderByDescending(m => m.ResolvedTurn).ThenBy(m => m.Id, StringComparer.Ordinal).Take(8).Select(Card).ToList();
        var houses = w.Houses.Values.Where(h => !World.Outsider(h.Id)).OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => (Id: h.Id, Name: h.Name)).ToList();
        var regions = w.Galaxy?.ReachRegions.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => (Id: r.Id, Name: r.Name)).ToList() ?? new List<(string Id, string Name)>();
        var proposals = senate.Motions.Defs.Where(d => d.Id != "petition" && !d.Rank).Select(d =>
        {
            List<(string Id, string Name)> targets = d.Target == "house" ? houses.Where(x => (x.Id != house.Id || d.TargetSelfAllowed) && (!d.TargetMustBeAtWar || w.Wars.Any(x2 => x2.Involves(x.Id)))).ToList() : d.Target == "region" ? regions
                : d.Target == "nation" ? senate.Nations.Defs.Where(nd => w.Nation(nd.Id) is { } ns && (d.Interest == "foreign_peace" ? NationSystem.PeaceRefusal(w, senate.Nations, nd.Id) == null : (!ns.AtWar || ns.WarOf.Length > 0) && !ns.Fallen && !ns.Subdued(w.Turn))).OrderBy(nd => nd.Name, StringComparer.Ordinal).Select(nd => (Id: nd.Id, Name: nd.Name)).ToList()
                : d.Target == "edict" ? EdictSystem.Annullable(w).Select(e => (Id: e.Id, Name: EdictSystem.Title(w, e))).ToList() : new List<(string Id, string Name)>();   // the edicts the Senate may strike down (docs/EDICTS.md)
            var commissionable = senate.Offices.Defs.Where(o => !o.Elected).ToList();
            if (d.Office) targets = targets.Where(x => commissionable.Any(o => OfficeSystem.Candidate(w, o, x.Id) != null)).ToList();   // only houses with someone of the rank
            string firstOffice = d.Office && commissionable.Count > 0 ? commissionable[0].Id : "";
            var probe = new ProposeMotionCommand(p.Id, d.Id, targets.Count > 0 ? targets[0].Id : "", senate, firstOffice).Refusal(ctx);
            if (probe != null && targets.Count > 1 && (probe == "not against yourself" || probe == "already before the Senate" || probe.StartsWith("no one of") || probe.EndsWith("holds it already"))) probe = new ProposeMotionCommand(p.Id, d.Id, targets[1].Id, senate, firstOffice).Refusal(ctx);
            int cost = new ProposeMotionCommand(p.Id, d.Id, "", senate).Cost(w, house.Id, d);
            List<ActLawChoice>? laws = null;
            if (d.Law)
            {
                laws = senate.Policies.Laws.Where(l => l.Scope == "senate").OrderBy(l => l.Category, StringComparer.Ordinal).ThenBy(l => l.Name, StringComparer.Ordinal).Select(l =>
                {
                    string inForce = (PolicySystem.ReachOption(w, senate.Policies, l) ?? l.DefaultOption).Id;
                    var now = l.Option(inForce) ?? l.DefaultOption;   // what each option would do to the player's own house, in figures (docs/SENATE.md, "The Senate's law, enlarged")
                    return new ActLawChoice(l.Id, l.Name, l.Description, inForce, l.Options.Select(o => (o.Id, o.Name, Effects(o.Effects) + (o.Id != inForce && SenateActs.ToHouse(w, house, now, o, senate.Offices, fleets) is { Length: > 0 } you ? ". " + you.TrimEnd('.') : ""), new ProposeMotionCommand(p.Id, d.Id, "", senate, lawId: l.Id, optionId: o.Id).Refusal(ctx),
                        string.Join(", ", o.Opinions.OrderByDescending(kv => Math.Abs(kv.Value)).Select(kv => $"{(senate.Policies.Estate(kv.Key)?.Name ?? kv.Key).Replace("The ", "")} {(kv.Value > 0 ? "+" : kv.Value < 0 ? "−" : "")}{Math.Abs(kv.Value)}")))).ToList(), l.Category);
                }).ToList();
                var firstOpen = laws.SelectMany(l => l.Options).FirstOrDefault(o => o.Refusal == null); probe = firstOpen.Refusal ?? laws.SelectMany(l => l.Options).Select(o => o.Refusal).FirstOrDefault(r => r != "already the law of the Reach") ?? probe;
                if (laws.SelectMany(l => l.Options).Any(o => o.Refusal == null)) probe = null;
            }
            return new MotionProposal(d.Id, d.Name, d.Description.Replace("{proposer} moves", "You move").Replace("{proposer}", "You").Replace("{target}", d.Target == "house" ? "a house" : d.Target == "region" ? "a region" : d.Target == "nation" ? "a nation beyond the Reach" : d.Target == "edict" ? "an edict in force" : "").Replace("the office of {office}", "an office of the Reach").Replace("{office}", "an office of the Reach").Replace("{law}", "a law of the Reach").Replace("{option}", "so set"), cost, d.Target, targets, probe, d.Office,
                d.Office ? commissionable.Select(o => (Id: o.Id, Name: o.Name)).ToList() : null, d.Law, laws);
        }).ToList();
        int total = w.Houses.Values.Where(h => !World.Outsider(h.Id)).Sum(h => senate.Weight(w, h.Id));
        var offices = senate.Offices.Defs.Select(o =>
        {
            var h = OfficeSystem.Holding(w, o.Id); var hh = h != null ? w.House(h.HouseId) : null; var who = h != null ? w.Char(h.CharacterId) : null;
            int hulls = o.CommandsFleets ? w.Fleets.Values.Where(f => f.Office).Sum(f => f.Strength) : 0;
            return new OfficeCard(o.Id, o.Name, o.Description, hh?.Id, hh?.Name, who?.Name, hh?.AccentColor, h?.SinceTurn ?? 0, hh?.Id == house.Id, OfficeSystem.Candidate(w, o, house.Id)?.Name, hulls, o.Track, o.MinRank, o.Elected);
        }).ToList();
        var factions = w.Factions.OrderByDescending(f => f.Members.Sum(h => senate.Weight(w, h))).ThenBy(f => f.Id, StringComparer.Ordinal).Select(f =>
        {
            var lh = w.House(f.LeaderHouseId);
            string platform = string.Join(" · ", f.Platform.Where(kv => kv.Value != 0).OrderByDescending(kv => Math.Abs(kv.Value)).Take(3).Select(kv => $"{(kv.Value > 0 ? "for" : "against")} {InProse(senate.Motions.Def(kv.Key)?.Name ?? kv.Key)}"));
            return new FactionCard(f.Id, f.Name, f.LeaderHouseId, lh?.Name ?? f.LeaderHouseId, f.Accent.Length > 0 ? f.Accent : lh?.AccentColor ?? "#888",
                f.Members.Select(h => (Id: h, Name: w.House(h)?.Name ?? h, Accent: w.House(h)?.AccentColor ?? "#888")).ToList(), f.Members.Sum(h => senate.Weight(w, h)),
                f.Members.Contains(house.Id), f.LeaderHouseId == house.Id, platform, f.Record, f.FormedTurn, new JoinFactionCommand(p.Id, f.Id).Refusal(ctx),
                f.Description, string.Join(", ", f.Estates.Select(e => senate.Policies.Estate(e)?.Name ?? e)), string.Join(" · ", f.Laws.Select(kv => $"{InProse(senate.Policies.Law(kv.Key)?.Name ?? kv.Key)}: {InProse(senate.Policies.Law(kv.Key)?.Option(kv.Value)?.Name ?? kv.Value)}")), f.PartyId.Length > 0);
        }).ToList();
        ElectionCard? election = null;
        if (ElectionSystem.ConsulOffices(senate.Offices).Any())
        {
            Func<string, int> wt = id => senate.Weight(w, id);
            var noms = ElectionSystem.Nominees(w, senate.Offices, wt); var tally = ElectionSystem.Tally(w, senate.Offices, wt, noms);
            election = new ElectionCard(ElectionSystem.CountIn(w), ElectionSystem.ConsulOffices(senate.Offices).First().TermTurns,
                noms.Select(n => { var f = n.partyId != null ? w.Factions.FirstOrDefault(x => x.Id == n.partyId) : null; var nh = w.House(n.houseId); return (f?.Name ?? "independent", f?.Accent ?? "#C9D1DC", n.houseId, nh?.Name ?? n.houseId, nh?.AccentColor ?? "#888", tally.GetValueOrDefault(n.houseId)); }).OrderByDescending(x => x.Item6).ToList(),
                w.HasFlag(ElectionSystem.StandFlag(house.Id)), new StandForConsulCommand(p.Id, senate.Offices).Refusal(ctx), ElectionSystem.IsConsul(w, senate.Offices, house.Id), ElectionSystem.MayVeto(w, senate.Offices, house.Id));
        }
        return new SenateCard(docket, history, proposals, senate.Weight(w, house.Id), total, senate.MayPropose(w, house.Id), SenateSystem.Embargoed(w, house.Id), Math.Max(0, w.Counter("embargo:" + house.Id) - w.Turn), offices,
            factions, new FoundFactionCommand(p.Id).Refusal(ctx), new LeaveFactionCommand(p.Id).Refusal(ctx), FoundFactionCommand.Followers(w, house.Id).Count, election,
            ChargeSystem.HeadAbroadSays(w, house, p.Id));
    }

    /// <summary>Battles about to be joined, the campaign, and the protagonist's own command style (docs/MILITARY.md).</summary>
    private static AssetsCard BuildAssets(World w, Character p, House house, ContentDatabase content, TurnController tc, CommandContext ctx)
    {
        using var perf_ = Perf.Time("proj:BuildAssets");
        var bc = content.Businesses; var vs = tc.Ventures;
        var projects = w.ProjectsOf(house.Id).OrderBy(x => x.Id, StringComparer.Ordinal).Select(x =>
        {
            var rush = new RushProjectCommand(p.Id, x.Id);
            return new ProjectCard(x.Id, x.Name, x.Kind, x.TurnsLeft, x.TurnsTotal, Bundle(x.Paid), Bundle(ProjectSystem.Remaining(x)), x.TurnsLeft > 1 ? Bundle(ProjectSystem.NextInstalment(x)) : "nothing more", x.Paused, x.Stalled,
                ProjectSystem.RushPremium(w, x), rush.Refusal(ctx), Bundle(ProjectSystem.Refund(x)), x.CompanyId.Length > 0 ? w.Company(x.CompanyId)?.Name : null)
            { Great = x.Great, Hastened = x.Hastened, HastenMissed = x.Hastened && x.HastenMissedTurn > 0 && x.HastenMissedTurn >= w.Turn - 1, HastenYear = x.Great ? HastenYear(x, content.Holdings.Rules.HastenPct, false) : "", HastenExtra = x.Great ? HastenYear(x, content.Holdings.Rules.HastenPct, true) : "" };
        }).ToList();
        var forSale = new List<ForSaleCard>(); var offers = new List<OfferCard>();
        if (vs != null)
        {
            foreach (var b in w.Businesses.Values.Where(b => b.OwnerId != house.Id && b.CompanyId.Length == 0 && bc.Def(b.Kind) != null).OrderBy(b => b.Id, StringComparer.Ordinal))
            {
                var owner = w.House(b.OwnerId); if (owner == null) continue;
                var (ask, refusal) = vs.Ask(w, b, house.Id);
                // what it would earn YOU: charters and the world's holder differ by owner
                var ghost = new Business { Id = b.Id, OwnerId = house.Id, Kind = b.Kind, SystemId = b.SystemId, Level = b.Level };
                int perYear = (int)Math.Round(Economy.WorthOf(w, vs.Economy.VentureLine(w, ghost)));
                bool distressed = owner.Treasury.Credits < 0 || owner.Debt > 0;
                forSale.Add(new ForSaleCard(b.Id, b.Name, bc.Def(b.Kind)!.Name, owner.Id, owner.Name, owner.AccentColor, w.Galaxy?.System(b.SystemId)?.Name, b.Level, perYear, ask,
                    refusal ?? (house.Treasury.Credits < ask ? $"needs {Money.Credits(ask)}" : null), ask > 0 && perYear > 0 ? $"{Counted.Years((long)(Math.Ceiling((double)ask / perYear)))}" : "never", distressed, w.PlanetHolder(b.SystemId) == house.Id));
            }
            forSale = forSale.OrderBy(x => x.Refusal != null && !x.Refusal.StartsWith("needs ") ? 1 : 0).ThenByDescending(x => x.Distressed).ThenBy(x => x.PerYear > 0 ? (double)x.Ask / x.PerYear : 999).ThenBy(x => x.BusinessId, StringComparer.Ordinal).Take(14).ToList();
            foreach (var o in w.Offers.Where(o => o.ToHouseId == house.Id).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var b = w.Business(o.BusinessId); if (b == null) continue;
                var ghost = new Business { Id = b.Id, OwnerId = house.Id, Kind = b.Kind, SystemId = b.SystemId, Level = b.Level };
                offers.Add(new OfferCard(o.Id, b.Name, bc.Def(b.Kind)?.Name ?? b.Kind, w.House(o.FromHouseId)?.Name ?? o.FromHouseId, w.Galaxy?.System(b.SystemId)?.Name, o.Price, (int)Math.Round(Economy.WorthOf(w, vs.Economy.VentureLine(w, ghost))), o.Reason,
                    new AcceptVentureOfferCommand(p.Id, o.Id, vs).Refusal(ctx)));
            }
        }
        return new AssetsCard(projects, forSale, offers);
    }

    private static ExchangeCard? BuildExchange(World w, Character p, House house, ContentDatabase content, TurnController tc)
    {
        using var perf_ = Perf.Time("proj:BuildExchange");
        var cs = tc.Companies; if (cs == null || !cs.Open || w.Market == null) return null; var m = w.Market; var r = cs.Rules;
        var lines = new List<CompanyLine>();
        foreach (var co in w.Companies.OrderByDescending(c => c.HeldBy(house.Id) > 0 || c.ControllerId == house.Id ? 1 : 0).ThenByDescending(c => CompanySystem.Cap(c)).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            var ctl = w.House(co.ControllerId); var h = co.History; int back = Math.Min(5, h.Count - 1);
            int change = back > 0 && h[^(back + 1)] > 0 ? (int)Math.Round((co.Price / h[^(back + 1)] - 1) * 100) : 0;
            var stakes = co.Holders.Where(kv => kv.Key != Company.Public && kv.Value * 100 >= co.Shares * r.PublicStakePct && w.House(kv.Key) != null).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new StakeLine(kv.Key, w.House(kv.Key)!.Name, w.House(kv.Key)!.AccentColor, kv.Value * 100 / co.Shares)).ToList();
            var def = content.Exchange.Charter(co.CharterId); int cap = CompanySystem.Cap(co);
            lines.Add(new CompanyLine(co.Id, co.Name, co.Sector, content.Exchange.Sector(co.Sector)?.Name ?? co.Sector, co.Price, change, h.ToList(), co.LastEarnings, co.LastDividend,
                cap > 0 ? (int)Math.Round(co.LastDividend * 100.0 / cap) : 0, co.Shares, co.HeldBy(Company.Public), co.HeldBy(house.Id), CompanySystem.Value(co, house.Id), co.Cash, co.PayoutPct,
                co.ControllerId, ctl?.Name ?? (co.Chartered ? (w.IsImperium ? "the throne's board" : "the Senate's board") : "its board"), ctl?.AccentColor ?? "#9aa4b5", co.ControllerId == house.Id && !co.Chartered,
                co.FounderId == house.Id, co.Chartered, co.Cause, stakes, CompanySystem.VenturesOf(w, co).Select(b => b.Name).ToList(), def?.Description ?? "",
                !co.Chartered && !co.GoldenShareUsed && co.FounderId.Length > 0 && co.ControllerId == co.FounderId, (int)Math.Round((cs.Outlook(w, co.Sector) - 1) * 100), cap,
                co.MonopolyOf.Length > 0 ? w.House(co.MonopolyOf)?.Name ?? "" : ""));
        }
        int due = w.Companies.Sum(co => co.Shares > 0 ? co.LastDividend * co.HeldBy(house.Id) / co.Shares : 0);
        var ih = m.IndexHistory; int ib = Math.Min(5, ih.Count - 1); int ichg = ib > 0 && ih[^(ib + 1)] > 0 ? (int)Math.Round((m.Index / ih[^(ib + 1)] - 1) * 100) : 0;
        int levy = content.Policies.IsEmpty ? 0 : Math.Max(0, PolicySystem.ReachModifier(w, content.Policies, "exchange_levy_pct"));
        var bonds = w.Bonds.Where(b => b.HolderId == house.Id).OrderBy(b => b.Id, StringComparer.Ordinal).Select(b => new BondLine(b.Id, content.Nations.Def(b.NationId)?.Name ?? b.NationId, b.Face, b.YieldPct, b.Bought)).ToList();
        BondOffer? offer = NationSystem.AtWar(w) is { } war ? new BondOffer(war.Id, content.Nations.Def(war.Id)?.Name ?? war.Id, r.BondFace, cs.BondYield(w), w.Bonds.Count(b => b.HolderId == house.Id && b.NationId == war.Id), r.BondCap, cs.BondRefusal(w, house.Id)) : null;
        bool treasurer = OfficeSystem.Holds(w, house.Id, CompanySystem.TreasurerOffice), emperor = w.IsImperium && ThroneSystem.Emperor(w)?.Id == house.Id;
        return new ExchangeCard(m.Index, ih.ToList(), ichg, (int)Math.Round(m.Confidence * 100), m.Movers.ToList(), lines, CompanySystem.PaperOf(w, house.Id), due, levy,
            bonds, offer, treasurer ? cs.SteadyRefusal(w, house.Id) : null, treasurer, emperor, emperor && PolicySystem.Option(w, content.Policies, house.Id, "imperial_edict")?.Id == "proscriptions");
    }

    private static MarketCard? BuildMarket(World w, Character p, House house, ContentDatabase content, TurnController tc)
    {
        using var perf_ = Perf.Time("proj:BuildMarket");
        var t = content.Trade; if (t.Market.IsEmpty) return null;
        var m = MarketSystem.Of(w, t.Market); var net = tc.Economy?.Forecast(w, house.Id, tc.Fleets);
        var goods = t.Market.Goods.Select(d =>
        {
            var g = m.Goods[d.Id]; var buy = MarketSystem.QuoteBuy(w, t, house.Id, d.Id, 1); var sell = MarketSystem.QuoteSell(w, t, house.Id, d.Id, 1);
            double then = g.History.Count > 5 ? g.History[^6] : g.History.Count > 0 ? g.History[0] : g.Price;
            var standing = CornerSystem.Standing(w, t.Market, d.Id); var top = standing.Top; bool felt = top != null && CornerSystem.Felt(top.Units, standing.All);
            return new GoodLine(d.Id, d.Name, g.Price, d.Base, MarketSystem.BuyPrice(w, t, house.Id, d.Id), MarketSystem.SellPrice(w, t, house.Id, d.Id), (int)Math.Floor(g.Stock),
                then <= 0 ? 0 : (int)Math.Round((g.Price - then) / then * 100), g.History.ToList(), g.Cause, house.Treasury.Get(d.Id), net?.Get(d.Id) ?? 0, d.Buy, d.Sell,
                buy.Refusal != null && !buy.Refusal.StartsWith("needs ") ? buy.Refusal : null, sell.Refusal != null && !sell.Refusal.StartsWith("you hold only") ? sell.Refusal : null, (int)Math.Round(g.Deficit * 100))
            {
                All = standing.All, Holders = CornerSystem.Listed(standing, house.Id).Select(h => new HolderLine(h.HouseId, h.Name, h.Accent, h.Units, h.SharePct, h.HouseId == house.Id)).ToList(),
                CornerHouseId = felt ? top!.HouseId : "", CornerHouseName = felt ? top!.Name : "", Cornered = felt && CornerSystem.Cornered(top!.Units, standing.All), CornerMine = felt && top!.HouseId == house.Id,
                CornerSharePct = felt ? top!.SharePct : 0, PremiumPct = felt ? (int)Math.Round(100 * Math.Min(CornerSystem.Premium(top!.Units, standing.All), SenateActs.CornerCap(w, top!.HouseId))) : 0,
                CornerHeld = CornerSystem.Held(w, t, house.Id, d.Id), CornerTake = CornerSystem.Take(w, t, house.Id, d.Id),
            };
        }).ToList();
        bool own = MarketSystem.HasMarket(w, house.Id, t.Holdings);
        return new MarketCard(goods, own, SenateSystem.Embargoed(w, house.Id), own ? t.Market.Rules.SpreadPct : t.Market.Rules.CaravanSpreadPct);
    }

    /// <summary>One of the great rules this nation: what that changes, in a line.</summary>
    private static string GreatLine(Nation n, NationDef d) => !NationSystem.Great(n) ? ""
        : $"{NationSystem.Styled(d, n.Ruler, true)} has made it one host: it begins wars itself, three years after the acclamation and every {NationSystem.GreatRest} after a war; its host stays in the field and takes world after world, {NationSystem.GreatReach} lanes deeper; it musters {NationSystem.Cap(n, d)} hulls, half as many again. ";

    /// <summary>What one of the great would make of this nation, and how often one comes in this game.</summary>
    private static string GreatHint(Nation n, NationDef d) => d.GreatOneIn <= 0 || d.GreatTitle.Length == 0 || NationSystem.Great(n) ? ""
        : $" Under a {d.GreatTitle} it would begin its own wars and come to conquer: one is raised at about one accession in {Math.Max(2, NationSystem.ByBoldness(n, d.GreatOneIn))}, or acclaimed when a warlike ruler beats the Reach.";
    /// <summary>A treaty that stands between the Reach and this nation: a client's, or the tribute the Reach pays it.</summary>
    private static string TreatyLine(World w, Nation n, NationDef d) =>
        n.Vassal ? $" A client of the Reach{(w.House(n.PatronId) is { } patron ? $", under {patron.Name}" : "")}: it pays {Money.Credits(d.Tribute)} a year to the chair{(w.House(n.PatronId) != null ? $" and {Money.Show(NationSystem.ClientShare(d))} to its patron" : "")}, raids nobody and wars with nobody, until a warlike ruler with its fleets rebuilt throws the treaty off."
        : n.PaidUntil > w.Turn ? $" The Reach pays it {Money.Credits(d.Tribute)} a year until T{n.PaidUntil}: the price of a war lost." : "";

    private static MilitaryCard BuildMilitary(World w, Character p, House house, FleetSystem fs, FleetRules rules, CommandContext ctx, NationCatalog nations, SenateSystem senate)
    {
        using var perf_ = Perf.Time("proj:BuildMilitary");
        var abroad = nations.Defs.Where(d => w.Nation(d.Id) != null).OrderByDescending(d => w.Nation(d.Id)!.AtWar).ThenByDescending(d => d.Strength).ThenBy(d => d.Name, StringComparer.Ordinal).Select(d =>
        {
            var n = w.Nation(d.Id)!;
            NationSystem.People(w, n, d); var guard = NationSystem.Guard(w, d);
            var marches = NationSystem.Marches(w, d); int nearest = w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.IsAway && f.SystemId.Length > 0).Select(f => NationSystem.OutTurns(w, d, f.SystemId)).DefaultIfEmpty(NationSystem.OutTurns(w, d, "")).Min();
            return new NationCard(d.Id, d.Name, d.Temper, d.Description, d.Holds, n.Strength, NationSystem.Cap(n, d), n.Disposition, n.AtWar, n.WarSince, n.ScoreReach, n.ScoreNation, NationSystem.SuesAt(n, d), d.Tribute, d.Wealth, marches.Count > 0 ? d.Depth : d.Distance,
                n.Subdued(w.Turn) ? n.SubduedUntil : 0, n.Wars, NationSystem.Raided(w, d.Id, 8), NationSystem.Expeditions(w, d.Id).Count(),
                new Option(d.Id, "Move for war", senate.Motions.Def("foreign_war") == null ? "no such motion" : new ProposeMotionCommand(p.Id, "foreign_war", d.Id, senate).Refusal(ctx)),
                NationSystem.Styled(d, n.Ruler, true), n.Ruler?.Nature ?? "", NationSystem.Styled(d, n.Commander, false), n.Commander?.Skill ?? 0,
                string.Join(" and ", marches.Select(m => w.Galaxy?.System(m)?.Name ?? m)), guard != null ? $"{guard.Name} of {w.House(guard.OwnerId)?.Name}" : "", nearest,
                // a nation on the map (docs/BEYOND.md): where its ruler sits, what it holds, where its fleets are, what it has of the Reach's and the Reach of its
                n.OnMap, n.Fallen, n.OnMap ? w.Galaxy?.System(n.CapitalSystemId)?.Name ?? "" : "",
                n.OnMap ? string.Join(", ", w.HoldingsOf(n.HouseId).Where(h => h.IsPlanet && w.Galaxy?.RingOf(h.SystemId) == Galaxy.Ring.Beyond).OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => h.Name)) : "",
                n.OnMap ? string.Join(" · ", w.Fleets.Values.Where(f => f.OwnerId == n.HouseId).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal).Select(f => $"{f.Name}, {f.Strength} hulls {(f.Moving ? $"bound for {w.Galaxy?.System(f.DestinationId)?.Name ?? f.DestinationId}" : $"at {w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId}")}")) : "",
                n.OnMap ? string.Join(", ", w.HoldingsOf(n.HouseId).Where(h => h.IsPlanet && w.Galaxy?.RingOf(h.SystemId) != Galaxy.Ring.Beyond).OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => h.FormerOwnerId.Length > 0 ? $"{h.Name} (of {w.House(h.FormerOwnerId)?.Name ?? h.FormerOwnerId})" : $"{h.Name} (ceded)")) : "",
                n.OnMap ? string.Join(", ", NationSystem.Worlds(w, d).Select(x => w.Holding(x.Id)).Where(h => h != null && h.OwnerId != n.HouseId).Select(h => $"{h!.Name} ({w.House(h.OwnerId)?.Name ?? h.OwnerId}{(w.Counter(NationSystem.UnheldKey(h.Id)) > 0 ? $", {w.Counter(NationSystem.UnheldKey(h.Id))} of {d.HoldYears} years ungarrisoned" : "")})")) : "",
                d.HoldYears,
                // the manner of a nation on the map: what kind of neighbour it is, in a line
                !n.OnMap || n.Fallen ? "" : GreatLine(n, d) + (NationSystem.Buys(d) ? $"Would rather buy than fight. It sues at {NationSystem.SuesAt(n, d)}, half the usual tally; hires a hull a year while attacked; buys back the worlds it lost when it loses a war, for {Money.Credits(NationSystem.RansomPerWealth * d.Wealth)} each; pays tribute only while watched; and buys off a war nobody presses.{(NationSystem.ComesOut(n, d) ? " Its ruler is warlike: its squadron will come through the march." : " Its squadron stays at home.")} A fleet of its beaten pays {Money.Credits(d.Wealth)}, a world taken {Money.Show(NationSystem.PlunderPerWorld * d.Wealth)}."
                    : d.Strikes ? $"Begins wars itself: when the Reach is divided and it has no love for it, or to take back a world of its own. {(d.Hosts > 1 ? $"{d.Hosts} fleets come through its marches" : "Its fleet comes through its march")}{(d.Campaigns ? " and stay in the field, taking world after world" : "")}. What it takes it keeps until it is beaten; its own worlds do not rise, it comes back for them. A fleet of its beaten pays {Money.Credits(d.Wealth)}, a world taken {Money.Show(NationSystem.PlunderPerWorld * d.Wealth)}."
                    : d.Sorties == "never" ? $"Fights only when invaded: its fleets never leave home. Hull for hull they fight at {NationSystem.MettleOf(d)} in a hundred of a fleet of the Reach's{(d.Walls > 0 ? $", and its worlds take {d.Walls} years longer to reduce" : "")}. {(d.Yields.Count > 0 ? $"Each of its worlds gives the house that holds it {string.Join(" and ", d.Yields.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{Money.Of(k.Key, k.Value)} {k.Key}"))} a year. " : "")}A fleet of its beaten pays {Money.Credits(d.Wealth)}, a world taken {Money.Show(NationSystem.PlunderPerWorld * d.Wealth)}."
                    : d.Temper == "decaying" ? $"An empire in decay: it strikes a hull from its lists every ten years, replaces none, and does not come back for a world it loses. Its fleets fight at {NationSystem.MettleOf(d)} in a hundred of a fleet of the Reach's. {(n.Treasure > 0 ? $"Its capital holds {Money.Credits(n.Treasure)}{(n.Regalia.Length > 0 ? $" and {n.Regalia}" : "")} for whoever takes it first. " : "Its vaults have been opened. ")}{(NationSystem.ComesOut(n, d) ? "Its ruler is warlike: its fleet will come through the march. " : "")}A fleet of its beaten pays {Money.Credits(d.Wealth)}, a world taken {Money.Show(NationSystem.PlunderPerWorld * d.Wealth)}."
                    : $"{(NationSystem.Great(n) ? "" : NationSystem.ComesOut(n, d) ? "Its host comes through the march in a war, takes one world, and goes home with it. " : "Its host stays at home. ")}A fleet of its beaten pays {Money.Credits(d.Wealth)}, a world taken or sacked {Money.Show(NationSystem.PlunderPerWorld * d.Wealth)}.") + GreatHint(n, d) + TreatyLine(w, n, d),
                n.OnMap ? string.Join(", ", NationSystem.Worlds(w, d).Where(x => w.Counter(NationSystem.RansomKey(x.Id)) > 0).Select(x => $"{Money.Credits(NationSystem.RansomPerWealth * d.Wealth)} for {x.Name}, until T{w.Counter(NationSystem.RansomKey(x.Id)) + NationSystem.RansomWaits}")) : "",
                n.OnMap && NationSystem.Buys(d) && n.Subdued(w.Turn) ? (NationSystem.Watched(w, d) ? "watched: the tribute comes" : $"not watched: {w.Counter(NationSystem.UnwatchedKeyOf(n))} of {NationSystem.UnwatchedYears} years, then it stops paying") : "",
                // whose war it is (docs/BEYOND.md, "Whose war"), and what the player may do about it without waiting on anyone
                !n.AtWar ? "" : (n.Defied ? $"{w.House(n.WarOf)?.Name ?? n.WarOf}'s war, against a peace the Senate voted: it costs that house {NationSystem.DefyYearlyCost(NationSystem.Leeway(w, w.House(n.WarOf)))} standing a year"
                    : n.PeaceVoted > 0 ? "The Senate has voted peace: those in the field are answering"
                    : n.WarOf.Length > 0 ? $"{w.House(n.WarOf)?.Name ?? n.WarOf}'s own war: the Senate voted nothing, and no other house is at war"
                    : n.BeganIt ? "The Reach's war: they began it, and every house may take what it can" : "The Reach's war, voted by the Senate: other houses raid and take nothing") + ". " + NationSystem.PeaceNow(w, n, d, house.Id),
                new List<Option>
                {
                    new Option("peace", "Move for peace", senate.Motions.Def("foreign_peace") == null ? "no such motion" : new ProposeMotionCommand(p.Id, "foreign_peace", d.Id, senate).Refusal(ctx)),
                    new Option("own_war", "Make war on your own account", new RaidNationCommand(p.Id, d.Id, nations).Refusal(ctx)),
                    new Option("obey", "Come home: keep the Senate's peace", !n.AtWar || !(n.PeaceVoted > 0 || n.Defied) ? "no peace is voted" : new AnswerPeaceCommand(p.Id, true).Refusal(ctx)),
                    new Option("defy", $"Fight on, against the Senate's word ({NationSystem.DefyCost(NationSystem.Leeway(w, house))} standing now, {NationSystem.DefyYearlyCost(NationSystem.Leeway(w, house))} a year: your commander is {NationSystem.LeewayName(NationSystem.Leeway(w, house))})", !n.AtWar || n.PeaceVoted <= 0 ? "no peace is voted" : new AnswerPeaceCommand(p.Id, false).Refusal(ctx)),
                    new Option("terms_tribute", "Make your own peace: they pay tribute", n.WarOf != house.Id ? "not your war" : new NegotiateCommand(p.Id, d.Id, "tribute", nations).Refusal(ctx)),
                    new Option("terms_territory", "Make your own peace: keep what you hold", n.WarOf != house.Id ? "not your war" : new NegotiateCommand(p.Id, d.Id, "territory", nations).Refusal(ctx)),
                    new Option("terms_white", "Make your own peace: a white peace", n.WarOf != house.Id ? "not your war" : new NegotiateCommand(p.Id, d.Id, "white", nations).Refusal(ctx)),
                    new Option("terms_ceded", "Make your own peace: they keep what they hold", n.WarOf != house.Id ? "not your war" : NationSystem.WarScore(n, d) >= 0 ? "not offered" : new NegotiateCommand(p.Id, d.Id, "ceded", nations).Refusal(ctx)),   // conceding is offered only when the war is going against you
                    new Option("terms_paid", "Make your own peace: pay them tribute", n.WarOf != house.Id ? "not your war" : NationSystem.WarScore(n, d) >= 0 ? "not offered" : new NegotiateCommand(p.Id, d.Id, "paid", nations).Refusal(ctx)),
                    new Option("client", w.HasFlag(NationSystem.PlayerClientsFlag) ? "Beaten decisively, you would make it a client: say you would keep what you take" : "Beaten decisively, you would keep what you take: say you would make it a client", !n.OnMap ? "not on the map" : null),
                    new Option("raid", w.HasFlag(NationSystem.PlayerRaidsFlag) ? "Order your fleets to take and hold (now they raid)" : "Order your fleets to raid (now they take and hold)", !n.OnMap ? "not on the map" : null),
                });
        }).ToList();
        var brief = new List<BattleBrief>();
        if (w.Galaxy != null)
            foreach (var sys in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving).Select(f => f.SystemId).Distinct().OrderBy(x => x, StringComparer.Ordinal))
                foreach (var enemy in w.Fleets.Values.Where(e => !e.Moving && e.SystemId == sys && e.OwnerId != house.Id && w.AtWar(house.Id, e.OwnerId)).Select(e => e.OwnerId).Distinct().OrderBy(x => x, StringComparer.Ordinal))
                {
                    string? owner = FleetSystem.TerritoryOwner(w, sys);
                    double ours = fs.Power(w, house.Id, sys, defending: owner == house.Id), theirs = fs.Power(w, enemy, sys, defending: owner == enemy);
                    string os = FleetSystem.StanceAt(w, house.Id, sys), ts = FleetSystem.StanceAt(w, enemy, sys);
                    int conf = w.Intel.Values.Where(i => w.Fleets.TryGetValue(i.FleetId, out var ef) && ef.OwnerId == enemy && ef.SystemId == sys).Select(i => i.Confidence).DefaultIfEmpty(50).Max();
                    brief.Add(new BattleBrief(sys, w.Galaxy.System(sys)?.Name ?? sys, enemy, w.House(enemy)?.Name ?? enemy, FleetSystem.StrengthAt(w, house.Id, sys), FleetSystem.StrengthAt(w, enemy, sys), conf, os, ts, FleetSystem.StanceEdgeOf(os, ts), ours, theirs));
                }
        string? target = FleetSystem.CampaignTarget(w, house.Id);
        var targets = w.EnemiesOf(house.Id).SelectMany(e => w.HoldingsOf(e).Where(h => h.IsPlanet && h.SystemId.Length > 0).Select(h => (h, e))).OrderBy(x => x.h.Name, StringComparer.Ordinal)
            .Select(x => new Option(x.h.SystemId, $"{x.h.Name} ({w.House(x.e)?.Name ?? x.e})", new DeclareCampaignCommand(p.Id, x.h.SystemId, rules).Refusal(ctx))).ToList();
        var campaign = new CampaignCard(target, target != null ? w.Galaxy?.System(target)?.Name ?? target : null, target != null ? Math.Max(0, w.Counter(FleetSystem.CampaignUntilKey(house.Id)) - w.Turn) : 0, targets, rules.CampaignCost);
        var myStyle = new[] { "front", "flag", "none" }.Select(st => new Option(st, st, new SetCommandStyleCommand(p.Id, p.Id, st).Refusal(ctx))).ToList();
        return new MilitaryCard(brief, campaign, SetCommandStyleCommand.StyleOf(p), myStyle, w.Counter(FleetSystem.ValourKey(p.Id)), p.Flags.Contains(FleetSystem.AchillesFlag), fs.CommandCap(p), abroad);
    }

    private static string Prose(string key) => key switch
    {
        "credits_per_world" => "credits a world", "materials_per_world" => "materials a world", "manpower_per_world" => "manpower a world", "unrest_drift" => "unrest a turn",
        "prosperity_drift" => "prosperity drift", "pop_growth" => "population growth", "lawlessness_drift" => "lawlessness a year", "allegiance_drift" => "allegiance a turn",
        "venture_yield_pct" => "% venture coin", "charter_pct" => "% charters", "market_price_pct" => "% market prices", "fleet_upkeep_pct" => "% fleet upkeep", "readiness" => "readiness a turn",
        "senate_weight" => "Senate weight", "legitimacy_per_5" => "legitimacy every fifth turn", "motion_cost" => "influence per motion (others)", "tolls_bonus" => "credits a toll", _ => key.Replace('_', ' ')
    };
    /// <summary>What a motion does if it carries, from its own numbers: so that a form says more than its name (docs/PLAYTHROUGH.md,
    /// the Senate: "what each motion does, in a line").</summary>
    public static string MotionDoes(MotionDef d)
    {
        var parts = new List<string>();
        if (d.LegitimacyTarget != 0) parts.Add($"the house named {(d.LegitimacyTarget > 0 ? "gains" : "loses")} {Math.Abs(d.LegitimacyTarget)} legitimacy");
        if (d.LegitimacyProposer != 0) parts.Add($"the mover {(d.LegitimacyProposer > 0 ? "gains" : "loses")} {Math.Abs(d.LegitimacyProposer)} legitimacy");
        if (d.LevyCredits != 0) parts.Add($"every house pays {Money.Credits(d.LevyCredits)} to the chair");
        if (d.EmbargoTurns > 0) parts.Add($"the exchanges are closed to the house named for {d.EmbargoTurns} years");
        if (d.LawlessnessRegion != 0) parts.Add($"the region's lawlessness {(d.LawlessnessRegion < 0 ? "falls" : "rises")} by {Math.Abs(d.LawlessnessRegion)}");
        if (d.AllegianceRegion != 0) parts.Add($"the region's allegiance {(d.AllegianceRegion > 0 ? "rises" : "falls")} by {Math.Abs(d.AllegianceRegion)}");
        if (d.ProsperityRegion != 0) parts.Add($"its worlds' prosperity {(d.ProsperityRegion > 0 ? "rises" : "falls")} by {Math.Abs(d.ProsperityRegion)}");
        if (d.ReliefCost > 0) parts.Add($"the chair pays {Money.Credits(d.ReliefCost)} for it");
        if (d.Peace) parts.Add("the war with the house named is ended by the Senate's word");
        if (d.Breakup) parts.Add("the companies of the house named are broken up and their holders paid out");
        if (d.Rescue) parts.Add("the chair's purse steadies the Exchange");
        if (d.Interest == TriumphSystem.TriumphKind) parts.Add("the general named must come home and lay the command down for a year to have it, or may decline it and keep the fleet");
        if (d.Interest == TriumphSystem.LeaveKind) parts.Add("those of the house named who command away from the capital may stand for office at the next election without coming home");
        if (d.Interest == EdictSystem.AnnulKind) parts.Add($"the edict named ends at once, and the house that issued it loses {EdictSystem.AnnulledStanding} legitimacy");
        return parts.Count == 0 ? "" : "If it carries: " + string.Join("; ", parts) + ".";
    }

    public static string Effects(IReadOnlyDictionary<string, int> fx) => fx.Count == 0 ? "changes no yield and no cost: what it moves is how the estates think of you" : string.Join(" · ", fx.Select(kv => SenateActs.Words(kv.Key, kv.Value) ?? $"{(InCredits(kv.Key) ? Money.Signed(kv.Value) : (kv.Value > 0 ? "+" : "") + kv.Value)} {Prose(kv.Key)}"));
    /// <summary>What an Act before the chamber would do to the player's own house, in figures, for the docket's card.</summary>
    private static string ActToYou(World w, House house, SenateSystem senate, Motion m, FleetSystem? fleets)
    {
        if (m.LawId.Length == 0 || senate.Policies.Law(m.LawId) is not { } law || law.Option(m.LawOption) is not { } next) return "";
        var cur = PolicySystem.ReachOption(w, senate.Policies, law) ?? law.DefaultOption; if (cur.Id == next.Id) return "";
        return SenateActs.ToHouse(w, house, cur, next, senate.Offices, fleets) is { Length: > 0 } you ? " " + you : "";
    }
    /// <summary>The effects of a law or an estate's mood that are an amount of credits, and so are written as credits are (docs/MONEY.md).
    /// A court's wage ("court_wage") is not one of them: it is in single credits.</summary>
    private static bool InCredits(string key) => key is "credits_per_world" or "tolls_bonus" or "informers_cost" or "envoy_cost" or "counsel_cost";

    /// <summary>Laws by category with every option's refusal, and the estates with their drift and mood (PolicySystem).</summary>
    private static ChanceryCard? BuildChancery(World w, Character p, House house, PolicyCatalog cat, CommandContext ctx)
    {
        using var perf_ = Perf.Time("proj:BuildChancery");
        if (cat.IsEmpty) return null; bool bound = w.OfTheReach(house.Id);   // a house that has broken away is bound only by its own laws
        var laws = cat.Laws.Select(l =>
        {
            string inForce = l.Reach ? (PolicySystem.ReachOption(w, cat, l) ?? l.DefaultOption).Id : PolicySystem.OptionId(w, cat, house.Id, l.Id);
            var options = l.Options.Select(o => new LawOptionCard(o.Id, o.Name, o.Id == inForce, Effects(o.Effects),
                o.Opinions.OrderByDescending(kv => Math.Abs(kv.Value)).Select(kv => (kv.Key, cat.Estate(kv.Key)?.Name ?? kv.Key, kv.Value)).ToList(),
                new EnactLawCommand(p.Id, l.Id, o.Id, cat).Refusal(ctx))).ToList();
            return new LawCard(l.Id, l.Name, l.Category, l.Description, l.Scope, PolicySystem.ScopeName(l.Scope), PolicySystem.HoldsScope(w, house.Id, l.Scope), l.Cost, Math.Max(0, w.Counter($"law_cd:{house.Id}:{l.Id}") - w.Turn), options);
        }).OrderByDescending(l => l.Held).ThenBy(l => l.Category, StringComparer.Ordinal).ThenBy(l => l.Name, StringComparer.Ordinal).ToList();
        var estates = cat.Estates.Select(e =>
        {
            int a = w.Counter("estate:" + e.Id); int target = Math.Clamp(PolicySystem.LawOpinion(w, cat, house.Id, e.Id) + w.Counter("estate_offset:" + e.Id), -100, 100);
            string mood = a >= PolicySystem.Loyal ? "loyal" : a <= PolicySystem.Hostile ? "hostile" : a >= 10 ? "warm" : a <= -10 ? "cool" : "indifferent";
            string fx = a >= PolicySystem.Loyal ? Effects(e.Loyal) : a <= PolicySystem.Hostile ? Effects(e.Hostile) : $"loyal would give {Effects(e.Loyal)}; hostile would cost {Effects(e.Hostile)}";
            var says = cat.Laws.Where(l => !l.Reach && PolicySystem.HoldsScope(w, house.Id, l.Scope)).Select(l => (l, o: PolicySystem.Option(w, cat, house.Id, l.Id))).Where(x => x.o != null && x.o.Opinions.GetValueOrDefault(e.Id) != 0)
                .Select(x => $"{x.o!.Name.ToLowerInvariant()} {(x.o.Opinions[e.Id] > 0 ? "+" : "")}{x.o.Opinions[e.Id]}").ToList();
            var reachSays = cat.Laws.Where(l => l.Reach).Select(l => (l, o: PolicySystem.ReachOption(w, cat, l))).Where(x => x.o != null && x.o.Opinions.GetValueOrDefault(e.Id) != 0)
                .Select(x => $"the Reach's {x.o!.Name.ToLowerInvariant()} {(x.o.Opinions[e.Id] > 0 ? "+" : "")}{x.o.Opinions[e.Id]}").ToList();
            if (!bound) { if (reachSays.Count > 0) says.Add("the law of the Reach counts for nothing: your house has broken away, and is bound only by its own laws"); }
            else
            {
                says.AddRange(reachSays);
                if (PolicySystem.ReachOpinion(w, cat, e.Id) is var said && said != 0) { int counts = PolicySystem.ReachCounts(said); says.Add($"the Reach's law in all {(said > 0 ? "+" : "")}{said}, counted at half and at most {PolicySystem.ReachCap}: {(counts > 0 ? "+" : "")}{counts}"); }
            }
            says.AddRange(LocalLawSystem.Says(w, cat, house.Id, e.Id));   // the law of single worlds, by the share of the house's worlds that keep it
            var pet = w.Motions.FirstOrDefault(m => m.Open && SenateSystem.PetitionEstate(m) == e.Id);
            string? petition = pet != null ? $"petitioning the Senate: that {cat.Law(pet.LawId)?.Name.ToLowerInvariant()} be {cat.Law(pet.LawId)?.Option(pet.LawOption)?.Name.ToLowerInvariant()}" : null;
            int hostileIn = w.Houses.Values.Count(h => w.OfTheReach(h.Id) && PolicySystem.Approval(w, h.Id, e.Id) <= PolicySystem.Hostile);   // as SenateSystem.Petition counts them: the houses of the Reach
            // what would warm them most: the single option, among the laws in the player's gift, that raises their opinion furthest over what is in force
            string? wants = null; int bestGain = 0;
            foreach (var l in cat.Laws.Where(l => !l.Reach && PolicySystem.HoldsScope(w, house.Id, l.Scope)))
            {
                var cur = PolicySystem.Option(w, cat, house.Id, l.Id); if (cur == null) continue;
                foreach (var o in l.Options) { int gain = o.Opinions.GetValueOrDefault(e.Id) - cur.Opinions.GetValueOrDefault(e.Id); if (gain > bestGain) { bestGain = gain; wants = $"{l.Name.ToLowerInvariant()} → {o.Name.ToLowerInvariant()} (+{gain}, {l.Cost} influence{(PolicySystem.OnCooldown(w, house.Id, l.Id) ? ", on cooldown" : "")})"; } }
            }
            return new EstateCard(e.Id, e.Name, e.Description, a, target, mood, fx, says.Count == 0 ? "no law moves them" : string.Join(" · ", says), petition, hostileIn, wants);
        }).ToList();
        var reach = cat.Laws.Where(l => l.Reach).OrderBy(l => l.Category, StringComparer.Ordinal).ThenBy(l => l.Name, StringComparer.Ordinal).Select(l =>
        {
            var o = PolicySystem.ReachOption(w, cat, l); bool act = l.Scope == "senate";
            string? holder = l.Scope == "chair" ? RegionSystem.CapitalHolder(w)?.Id : l.Scope == "emperor" ? ThroneSystem.Emperor(w)?.Id : l.Scope.StartsWith("office:") ? OfficeSystem.HolderHouse(w, l.Scope["office:".Length..]) : null;
            string setBy = act ? (w.Laws.TryGetValue(PolicySystem.SenateHolder, out var set) && set.ContainsKey(l.Id) ? "by an Act of the Senate" : "the custom, no Act yet carried")
                : holder == null ? $"no one holds {PolicySystem.ScopeName(l.Scope)}" : holder == house.Id ? "set by you, as " + PolicySystem.ScopeName(l.Scope) : $"set by {w.House(holder)?.Name ?? holder}, as {PolicySystem.ScopeName(l.Scope)}";
            return new ReachLawLine(l.Id, l.Name, l.Category, (o ?? l.DefaultOption).Name, Effects((o ?? l.DefaultOption).Effects), setBy, act, holder == house.Id, act ? Math.Max(0, w.Counter($"law_cd:{PolicySystem.SenateHolder}:{l.Id}") - w.Turn) : 0);
        }).ToList();
        return new ChanceryCard(laws, estates, w.PoolsFor(p.Id).Influence, reach, bound);
    }

    /// <summary>A name in the middle of a sentence: lower case, but the Reach keeps its capital ("the levy of the Reach").</summary>
    private static string InProse(string name) => System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"\breach\b", "Reach");
}
