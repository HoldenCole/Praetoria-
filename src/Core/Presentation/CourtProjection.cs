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
public sealed record TreasuryLine(int Credits, int Materials, int Manpower, int Influence, int Exotics, bool Insolvent);
public sealed record BuildOption(string HoldingId, string BuildingId, string Label, string Tooltip, bool Available);
public sealed record HoldingCard(string Id, string Name, string SpecName, string? SystemName, int Population, int MaxPopulation, int Unrest,
    int SlotsUsed, int Slots, IReadOnlyList<string> Buildings, IReadOnlyList<BuildOption> Builds, bool CanRaiseShips, string RaiseShipsLabel,
    IReadOnlyList<ShipOption> Ships, bool Besieged, string Kind = "planet", string? GovernorName = null, IReadOnlyList<Option>? GovernorOptions = null, string Culture = "",
    int Prosperity = 50, int ProsperityTarget = 50, Option? Invest = null, int Belt = 0, int GovernorDisposition = 0, int GovernorAdministration = 0, bool GovernorAmbitious = false);
/// <summary>A venture the house owns (GDD §17 businesses) and what can be done with it.</summary>
public sealed record BusinessCard(string Id, string Name, string KindName, string? SystemName, int Level, int MaxLevel, string Yield, string? Charter, int Value, Option Expand, Option Sell);
/// <summary>A venture the house could found: a kind in a system, with the reason it cannot (null = it can).</summary>
public sealed record VentureOption(string DefId, string Name, string Description, string SystemId, string SystemName, string Cost, string? Refusal);
/// <summary>A belt the house could stake a claim in.</summary>
public sealed record ClaimOption(string SystemId, string SystemName, int Belt, int Staked, string? Refusal);
/// <summary>A world of the house's that broke away and may be bought back.</summary>
public sealed record ReclaimOption(string HoldingId, string HoldingName, string FreeHouseName, int Price, string? Refusal);
/// <summary>The capital: who holds it, and what it gives.</summary>
public sealed record CapitalCard(string SystemName, string? HolderId, string? HolderName, bool Mine);
/// <summary>One house on the wealth table.</summary>
public sealed record WealthLine(string HouseId, string Name, string Accent, int Wealth, string Tier, bool Mine);
/// <summary>The house ledger (GDD §17): every per-turn line, the net, wealth and its rank, the loan.</summary>
public sealed record LedgerCard(IReadOnlyList<LedgerLine> Lines, Resources Net, int Wealth, int Rank, int Houses, string Tier, int Debt, string? LenderName, int LoanPrincipal, int LoanRepay, int LoanInstalment,
    Option Loan, Option Repay, IReadOnlyList<WealthLine> Table);
public sealed record PathOption(string Id, string Label, string Tooltip, bool Available);
public sealed record StandingCard(int Legitimacy, int Instability, IReadOnlyList<string> Claims, string? NextTitle, int NextRequirement,
    IReadOnlyList<PathOption> Paths, string Spheres, int Threat, IReadOnlyList<string> Honors, string SuccessionLaw, IReadOnlyList<Option> LawOptions,
    string TaxPolicy, IReadOnlyList<Option> TaxOptions, IReadOnlyList<Option> ExchangeOptions, ThroneCard Throne);
public sealed record FleetLine(string Id, string Name, string Where, int Strength, int Readiness, bool Moving, string? Destination, int Eta, bool Office,
    string Composition, string? AdmiralName, int Veterancy, IReadOnlyList<Option> AdmiralOptions, IReadOnlyList<Option> MergeOptions, Option Refit);
/// <summary>A generic actionable option: an id the UI turns into a command, a label, and why it is disabled (null = available).</summary>
public sealed record Option(string Id, string Label, string? Refusal);
public sealed record ShipOption(string HoldingId, string ClassId, string Label, string? Refusal);
public sealed record HouseholdLine(string Id, string Name, int Age, string Sex, bool Hidden, bool Married, IReadOnlyList<Option> Actions);
public sealed record SiegeLine(string HoldingName, string BesiegerName, int Turns, int Required, bool Ours);
public sealed record ThroneCard(string? ProclaimRefusal, string? SeizeRefusal, bool HoldsThrone, string? EmperorName);
public sealed record GreatCrisisCard(string Name, string? Stage, int Clock, int? NextAt, string Intensity, string Bloc, bool Resolved, string? HegemonName);
public sealed record WarLine(string EnemyHouseId, string EnemyName, int Since, int EnemyHulls, bool PeaceAvailable, string PeaceTooltip, bool TheyAreSuing);
public sealed record BattleLine(string Text);
/// <summary>A choice as offered: <paramref name="Reason"/> is null when available, else why not ("requirements not met" — a trait,
/// skill or rank gate — or "cannot afford: 2 influence").</summary>
public sealed record ChoiceCard(string Id, string Label, string Cost, bool Available, string? Reason = null);
public sealed record BriefingCard(BriefingItem Item, string Title, string Tier, bool Setpiece, string Body, bool Resolved, IReadOnlyList<ChoiceCard> Choices);
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
    IReadOnlyList<ReclaimOption>? Reclaims = null, CapitalCard? Capital = null, Option? Petition = null);

public static class CourtProjection
{
    private static readonly IRng NoRng = new SplitMix64Rng(0);
    public const int ChronicleLines = 10;

    public static CourtModel Build(TurnController tc, ContentDatabase content)
    {
        var w = tc.World;
        var p = w.Protagonist ?? throw new InvalidOperationException("No protagonist.");
        var house = w.House(p.HouseId) ?? throw new InvalidOperationException($"House '{p.HouseId}' missing.");
        var ctx = new CommandContext(w, NoRng);

        var character = new CharacterCard(
            p.Name, house.Name, house.AccentColor,
            content.Titles.ById(house.Title)?.Name ?? house.Title,
            content.Careers.RankName(p.CareerTrack, p.CareerRank) ?? $"rank {p.CareerRank}",
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
                var cmd = new BuildCommand(p.Id, h.Id, b.Id, content.Holdings, content.Titles);
                builds.Add(new BuildOption(h.Id, b.Id, $"{(b.Megastructure ? "Raise" : "Build")} {b.Name}  ({Bundle(b.Cost)})",
                    $"yield {Bundle(b.Yield)}" + (b.Upkeep.IsEmpty() ? "" : $" · upkeep {Bundle(b.Upkeep)}") + (b.MinTitleRank > 0 ? $" · needs title rank {b.MinTitleRank}" : ""), cmd.CanExecute(ctx)));
            }
            bool canRaise = new RaiseFleetCommand(p.Id, h.Id, content.Fleets, content.ShipClasses, null, content.Titles).CanExecute(ctx);
            var ships = new List<ShipOption>();
            foreach (var cls in content.ShipClasses.Defs)
            {
                var rc = new RaiseFleetCommand(p.Id, h.Id, content.Fleets, content.ShipClasses, cls.Id, content.Titles);
                ships.Add(new ShipOption(h.Id, cls.Id, $"Lay down a {cls.Name.ToLowerInvariant()}  ({Bundle(cls.Cost)}) · atk {cls.Attack} / def {cls.Defense}", rc.Refusal(ctx)));
            }
            holdings.Add(new HoldingCard(h.Id, h.Name, spec?.Name ?? h.Specialization, w.Galaxy?.System(h.SystemId)?.Name,
                h.Population, spec?.MaxPopulation ?? 0, h.Unrest, h.Buildings.Count, spec?.Slots ?? 0,
                h.Buildings.Select(b => content.Holdings.Building(b)?.Name ?? b).ToList(), builds,
                canRaise, $"Lay down a hull at {h.Name}  ({Bundle(content.Fleets.ShipCost)})", ships, w.Sieges.ContainsKey(h.Id),
                h.Kind, h.GovernorId.Length > 0 ? w.Char(h.GovernorId)?.Name : null,
                w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority).OrderBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => new Option(c.Id, $"Name {c.Name} governor of {h.Name}  (1 influence · administration {c.Skill("administration")})", new AppointGovernorCommand(p.Id, h.Id, c.Id).Refusal(ctx))).ToList(),
                h.Culture,
                h.Prosperity, tc.Economy?.ProsperityTarget(w, h) ?? 50,
                new Option("invest", $"Invest in {h.Name}  ({content.Businesses.Finance.InvestCredits} credits → +{content.Businesses.Finance.InvestProsperity} prosperity)", new InvestCommand(p.Id, h.Id, content.Businesses).Refusal(ctx)),
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
            var table = Wealth.Ranked(w).Select(x => new WealthLine(x.house.Id, x.house.Name, x.house.AccentColor, x.wealth, Wealth.Tier(x.wealth), x.house.Id == house.Id)).ToList();
            var lender = house.LenderId.Length > 0 ? w.House(house.LenderId)?.Name : null;
            ledger = new LedgerCard(lg.Lines, lg.Net, house.Wealth, Wealth.RankOf(w, house.Id), w.Houses.Count, Wealth.Tier(house.Wealth), house.Debt, lender, f.LoanPrincipal, f.LoanRepay, f.LoanInstalment,
                new Option("loan", $"Borrow {f.LoanPrincipal} credits (owe {f.LoanRepay}, {f.LoanInstalment} a turn)", new TakeLoanCommand(p.Id, bc).Refusal(ctx)),
                new Option("repay", $"Clear the debt ({house.Debt} credits)", new RepayLoanCommand(p.Id).Refusal(ctx)), table);
            foreach (var b in w.BusinessesOf(house.Id))
            {
                var def = bc.Def(b.Kind); if (def == null) continue;
                var line = lg.Lines.FirstOrDefault(l => l.Source == b.Name && l.Kind == "venture");
                var holder = w.PlanetHolder(b.SystemId);
                businessCards.Add(new BusinessCard(b.Id, b.Name, def.Name, w.Galaxy?.System(b.SystemId)?.Name, b.Level, def.MaxLevel, line != null ? Bundle(line.Delta) : "—",
                    holder != null && holder != house.Id && def.Charter > 0 ? $"{def.Charter * b.Level} credits a turn to {w.House(holder)?.Name}" : null, def.Value(b.Level),
                    new Option("expand", b.Level >= def.MaxLevel ? "At its height" : $"Expand to level {b.Level + 1}  ({Bundle(def.ExpandCost)})", new ExpandBusinessCommand(p.Id, b.Id, bc).Refusal(ctx)),
                    new Option("sell", $"Sell for {SellBusinessCommand.Price(def, b)} credits", new SellBusinessCommand(p.Id, b.Id, bc).Refusal(ctx))));
            }
            if (w.Galaxy != null)
            {
                var own = w.HoldingsOf(house.Id).Select(h => h.SystemId).ToHashSet();
                foreach (var def in bc.Defs)
                    foreach (var sys in w.Galaxy.Systems.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
                    {
                        var refusal = new FoundBusinessCommand(p.Id, def.Id, sys.Id, bc, content.Holdings, content.Titles).Refusal(ctx);
                        if (refusal == null || own.Contains(sys.Id)) ventureOptions.Add(new VentureOption(def.Id, def.Name, def.Description, sys.Id, sys.Name, Bundle(def.Cost), refusal));
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
                paths.Add(new PathOption("seize", "Seize it by force", "Military — ignores legitimacy; spikes the galaxy's fear", new SeizeTitleCommand(p.Id, prog).CanExecute(ctx)));
                paths.Add(new PathOption("petition", "Petition the throne", "Merit — granted only what you're legitimate for; arrives clean", new PetitionTitleCommand(p.Id, prog).CanExecute(ctx)));
                paths.Add(new PathOption("claim", "Press a claim", "Intrigue — needs a claim; launders legitimacy, leaves a trail", new ClaimTitleCommand(p.Id, prog).CanExecute(ctx)));
            }
            var honors = new HonorSystem(content.Honors).Held(p).Select(h => h.Name).ToList();
            var laws = SetSuccessionLawCommand.Laws.Select(l => new Option(l, l, new SetSuccessionLawCommand(p.Id, l).CanExecute(ctx) ? null : (house.SuccessionLaw == l ? "current law" : "no influence"))).ToList();
            var taxes = content.Holdings.TaxPolicies.Select(t => new Option(t.Id, $"{t.Name} ({t.CreditsBonus:+#;-#;0} credits/holding, unrest {t.UnrestDrift:+#;-#;0})", new SetTaxPolicyCommand(p.Id, t.Id, content.Holdings).CanExecute(ctx) ? null : "current")).ToList();
            var exchange = content.Holdings.Rates.Select(r => new Option($"{r.Give}>{r.Get}", $"{r.GiveAmount} {r.Give} → {r.GetAmount} {r.Get}",
                new ExchangeCommand(p.Id, r, content.Holdings).CanExecute(ctx) ? null : "cannot afford")).ToList();
            var emperor = ThroneSystem.Emperor(w);
            var throne = new ThroneCard(new ProclaimImperiumCommand(p.Id, content.Titles).Refusal(ctx), new SeizeThroneCommand(p.Id, content.Titles, content.Friction).Refusal(ctx),
                house.Title == "emperor", emperor?.Name);
            standing = new StandingCard(house.Legitimacy, w.Counter("title_instability"),
                house.Claims.OrderBy(c => c, StringComparer.Ordinal).ToList(), next?.Name, next?.LegitimacyRequirement ?? 0, paths,
                string.Join(" · ", house.SphereInfluence.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")),
                w.Counter("threat"), honors, house.SuccessionLaw, laws, house.TaxPolicy, taxes, exchange, throne);
        }

        var fleetSys = tc.Fleets;
        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal)
            .Select(f => new FleetLine(f.Id, f.Name, w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId, f.Strength, f.Readiness, f.Moving,
                f.Moving ? w.Galaxy?.System(f.DestinationId)?.Name ?? f.DestinationId : null, f.Eta, f.Office,
                fleetSys.Describe(f), f.AdmiralId.Length > 0 ? w.Char(f.AdmiralId)?.Name : null, f.Veterancy,
                w.LivingCharacters().Where(c => c.HouseId == house.Id).OrderBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => new Option(c.Id, $"Give {f.Name} to {c.Name} (tactics {c.Skill("tactics")})", new AssignAdmiralCommand(p.Id, f.Id, c.Id).CanExecute(ctx) ? null : "already in command, or no agents")).ToList(),
                w.Fleets.Values.Where(o => o.Id != f.Id && o.OwnerId == house.Id).OrderBy(o => o.Id, StringComparer.Ordinal)
                    .Select(o => new Option(o.Id, $"Fold {o.Name} into {f.Name}", new MergeFleetsCommand(p.Id, o.Id, f.Id).CanExecute(ctx) ? null : "not in the same system")).ToList(),
                new Option("refit", $"Refit {f.Name}  ({RefitFleetCommand.MaterialsPerHull * f.Strength} materials → readiness 100)", new RefitFleetCommand(p.Id, f.Id).CanExecute(ctx) ? null : (f.Readiness >= 100 ? "already at full readiness" : "only at a system of your own, with the materials")))).ToList();

        var wars = new List<WarLine>();
        foreach (var enemy in w.EnemiesOf(house.Id))
        {
            var war = w.Wars.First(x => x.Between(house.Id, enemy));
            var peace = new SueForPeaceCommand(p.Id, enemy, content.Fleets);
            bool can = peace.CanExecute(ctx);
            bool refuses = !tc.Fleets.WouldAcceptPeace(w, enemy, house.Id);
            bool suing = w.LivingCharacters().Any(c => c.HouseId == enemy && c.Flags.Contains(FleetSystem.SuingForPeaceFlag));
            wars.Add(new WarLine(enemy, w.House(enemy)?.Name ?? enemy, war.Since, FleetSystem.StrengthOf(w, enemy), can,
                can ? "" : refuses ? "They smell victory — their hulls far outnumber yours." : "No Influence left this turn.", suing));
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
                    : !tc.Engine.IsChoiceAvailable(fired, w, oc.Choice.Id) ? "requirements not met"
                    : $"cannot afford: {cost}";
                return new ChoiceCard(oc.Choice.Id,
                    text != null && text.Choices.TryGetValue(oc.Choice.Id, out var ct) ? TextRenderer.Substitute(ct, ectx) : oc.Choice.Id,
                    cost, oc.Available, reason);
            }).ToList();
            briefing.Add(new BriefingCard(item, TextRenderer.Substitute(text?.Title ?? fired.Def.Id, ectx), fired.Def.Tier.ToString().ToUpperInvariant(),
                fired.Def.Tier == EventTier.Setpiece, string.IsNullOrWhiteSpace(text?.Body) ? "" : TextRenderer.Substitute(text!.Body, ectx), item.Resolved, choices));
        }

        var members = house.Members.Select(w.Char).Where(c => c != null && c!.Id != p.Id).Select(c => c!)
            .Select(c => { var rel = w.GetRelationship(c.Id, p.Id); return new MemberLine(c.Name,
                c.Alive ? $"{content.Careers.RankName(c.CareerTrack, c.CareerRank) ?? $"rank {c.CareerRank}"}, {c.Age}" : "dead",
                c.Alive, rel?.Disposition ?? 0, BondName(rel)); }).ToList();

        var suitor = w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Age >= DynastySystem.AgeOfMajority && c.Sex != "" && !ProposeMarriageCommand.IsMarried(w, c.Id))
                      .OrderByDescending(c => c.Id == p.Id).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        var connections = w.LivingCharacters().Where(c => c.HouseId != house.Id).OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c =>
            {
                var rel = w.GetRelationship(c.Id, p.Id); int d = rel?.Disposition ?? 0;
                var actions = new List<Option>();
                // no match and no knife is offered against a child (GDD §14): the court does not list what the house would not do
                if (c.Age >= DynastySystem.AgeOfMajority)
                {
                    if (suitor != null)
                    {
                        var pm = new ProposeMarriageCommand(p.Id, suitor.Id, c.Id, content.Titles);
                        actions.Add(new Option("marry:" + suitor.Id, $"Propose {suitor.Name} ⚭ {c.Name}  (2 influence)", pm.Refusal(ctx)));
                    }
                    var kill = new AssassinateCommand(p.Id, c.Id, content.Friction);
                    actions.Add(new Option("assassinate", $"Arrange an accident  (2 agents · {AssassinateCommand.Odds(w, p, c):P0})", kill.CanExecute(ctx) ? null : "unreachable, or no agents"));
                }
                return new ConnectionLine(c.Id, c.Name, w.House(c.HouseId)?.Name ?? c.HouseId, content.Careers.RankName(c.CareerTrack, c.CareerRank) ?? (c.Age < DynastySystem.AgeOfMajority ? $"child, {c.Age}" : $"{c.Age}, untitled"), d, BondName(rel),
                    c.Flags.Where(f => !f.StartsWith("background:")).OrderBy(f => f, StringComparer.Ordinal).ToList(), d < -20, actions);
            }).ToList();

        var household = w.Characters.Values.Where(c => c.Alive && c.HouseId == house.Id && c.Id != p.Id).OrderBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c =>
            {
                var acts = new List<Option>();
                if (c.Flags.Contains(FamilySystem.DefiantFlag)) acts.Add(new Option("defiant", $"{c.Name} is defiant — answer it in the briefing", "informational"));
                if (c.Age < DynastySystem.AgeOfMajority)
                    acts.Add(c.Flags.Contains(DynastySystem.HiddenFlag)
                        ? new Option("recall", $"Recall {c.Name} to court", new RecallCommand(p.Id, c.Id).CanExecute(ctx) ? null : "")
                        : new Option("foster", $"Foster {c.Name} away, hidden  (1 agent)", new FosterCommand(p.Id, c.Id).CanExecute(ctx) ? null : "no agents"));
                return new HouseholdLine(c.Id, c.Name, c.Age, c.Sex, c.Flags.Contains(DynastySystem.HiddenFlag), ProposeMarriageCommand.IsMarried(w, c.Id), acts);
            }).ToList();

        var sieges = w.Sieges.Values.OrderBy(s => s.HoldingId, StringComparer.Ordinal)
            .Select(s => new SiegeLine(w.Holding(s.HoldingId)?.Name ?? s.HoldingId, w.House(s.BesiegerId)?.Name ?? s.BesiegerId, s.Turns, s.Required, w.Holding(s.HoldingId)?.OwnerId == house.Id)).ToList();
        string? epilogue = w.IsDynastyDead || w.HasFlag(ThroneSystem.ProclaimedFlag) || w.HasFlag(ThroneSystem.SeizedFlag) ? Epilogue.Generate(w, content) : null;
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
                            can ? "" : "Not available — earned by prior play, or unaffordable.", can));
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
            new TreasuryLine(t.Credits, t.Materials, t.Manpower, t.Influence, t.Exotics, t.Credits < 0),
            holdings, standing, fleets, wars, briefing, house.Name, house.AccentColor, members, connections, clock, chronicle, household, sieges, epilogue, advisors, great,
            ledger, businessCards, ventureOptions, claimOptions,
            w.Holdings.Values.Where(h => h.FormerOwnerId == house.Id && w.House(h.OwnerId)?.Bloc == RegionSystem.FreeholdsBloc).OrderBy(h => h.Id, StringComparer.Ordinal)
                .Select(h => new ReclaimOption(h.Id, h.Name, w.House(h.OwnerId)!.Name, ReclaimFreeholdCommand.Price(w, h, house.Id), new ReclaimFreeholdCommand(p.Id, h.Id).Refusal(ctx))).ToList(),
            w.Galaxy is { Capital.Length: > 0 } cg ? new CapitalCard(cg.System(cg.Capital)?.Name ?? cg.Capital, RegionSystem.CapitalHolder(w)?.Id, RegionSystem.CapitalHolder(w)?.Name, RegionSystem.CapitalHolder(w)?.Id == house.Id) : null,
            new Option("petition_fief", "Petition for a fief  (3 influence)", new PetitionFiefCommand(p.Id).Refusal(ctx)));
    }

    private static string BondName(Relationship? rel) => rel?.Bond is { } b && b != BondType.None ? b.ToString().ToLowerInvariant() : "";

    public static string Bundle(Resources r)
    {
        var parts = Resource.All.Where(k => r.Get(k) != 0).Select(k => $"{r.Get(k)} {k}").ToList();
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }
}
