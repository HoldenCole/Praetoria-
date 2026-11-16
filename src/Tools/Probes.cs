using System.Diagnostics;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Diagnostics;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Tools;

/// <summary>
/// Measurements with a passive player (the first available choice of every report, no orders): where a turn's time goes, whether a
/// start can found a line, and how the markets and the three orders stand after sixty years. They print numbers; they do not pass
/// or fail. docs/PROCESS.md says when to run them.
/// </summary>
internal static class Probes
{
    private static (ContentDatabase content, List<string> scenarios) Load(Options o)
    {
        var root = ContentLocator.FindContentDir(); var content = ContentLoader.LoadFromDirectory(root);
        var all = Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json").Select(Path.GetFileNameWithoutExtension).Select(s => s!).OrderBy(s => s, StringComparer.Ordinal).ToList();
        return (content, o.Scenario is "academy_crucible" or "all" ? all.Where(s => !ScenarioLoader.LoadFromContent(root, s).Hidden).ToList() : new List<string> { o.Scenario });
    }
    private static void PassiveTurn(TurnController tc)
    {
        foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
    }

    /// <summary>perf: native milliseconds a turn by system, costliest first. The browser runs the same code a few times slower.</summary>
    public static int PerfRun(Options o)
    {
        var (content, scenarios) = Load(o); int turns = Math.Max(6, Math.Min(o.Turns, 40));
        foreach (var sid in scenarios)
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), o.Seed); var tc = new TurnController(w, content);
            PassiveTurn(tc); CourtProjection.Build(tc, content); tc.EndTurn();   // warm up
            Perf.Enabled = true; Perf.Reset(); double begin = 0, end = 0, proj = 0; var sw = new Stopwatch(); int n = 0;
            for (int t = 0; t < turns && !w.IsDynastyDead; t++, n++)
            {
                sw.Restart(); PassiveTurn(tc); begin += sw.Elapsed.TotalMilliseconds;
                sw.Restart(); CourtProjection.Build(tc, content); proj += sw.Elapsed.TotalMilliseconds;
                sw.Restart(); tc.EndTurn(); end += sw.Elapsed.TotalMilliseconds;
            }
            Perf.Enabled = false; n = Math.Max(1, n);
            Console.WriteLine($"== {sid}: a turn costs {(begin + end) / n:F1} ms (open the year {begin / n:F1}, resolve it {end / n:F1}); one projection {proj / n:F1} ms; {w.Houses.Count} houses");
            foreach (var (name, ms, calls) in Perf.Report().Take(12)) Console.WriteLine($"   {name,-30} {ms / n,7:F2} ms a turn");
            sw.Restart(); string save = SaveSystem.Serialize(w); Console.WriteLine($"   {"serialise the save",-30} {sw.Elapsed.TotalMilliseconds,7:F2} ms ({save.Length / 1024} KB)");
            Perf.Reset();
        }
        return 0;
    }

    /// <summary>survive: over --seeds runs of sixty years, how often the line ends, when the head first weds and first has a child.</summary>
    public static int Survive(Options o)
    {
        var (content, scenarios) = Load(o); int seeds = Math.Max(1, o.Seeds);
        foreach (var sid in scenarios)
        {
            int dead = 0; var wed = new List<int>(); var child = new List<int>(); var ends = new List<string>();
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), seed); var tc = new TurnController(w, content);
                int wedTurn = -1, childTurn = -1; string houseName = w.Protagonist is { } first ? w.House(first.HouseId)?.Name ?? "?" : "?";
                for (int t = 0; t < 60 && !w.IsDynastyDead; t++)
                {
                    PassiveTurn(tc);
                    if (w.Protagonist is { } me && !w.InAcademy)
                    {
                        if (wedTurn < 0 && ProposeMarriageCommand.IsMarried(w, me.Id)) wedTurn = w.Turn;
                        if (childTurn < 0 && w.LivingCharacters().Any(c => c.HouseId == me.HouseId && c.Age < 16)) childTurn = w.Turn;
                    }
                    tc.EndTurn();
                }
                if (w.IsDynastyDead)
                {
                    dead++; ends.Add($"   seed {seed}, year {w.Turn}: " + string.Join(" / ", w.History.Where(h => h.Text.Contains(houseName)).TakeLast(o.Turns > 0 && o.Turns < 60 ? o.Turns : 5).Select(h => $"[{h.Turn}] " + (h.Text.Length > 130 ? h.Text[..130] + "…" : h.Text))));
                }
                if (wedTurn >= 0) wed.Add(wedTurn); if (childTurn >= 0) child.Add(childTurn);
            }
            string Mid(List<int> xs) => xs.Count == 0 ? "never" : $"year {xs.OrderBy(x => x).ElementAt(xs.Count / 2)} (in {xs.Count} of {seeds})";
            Console.WriteLine($"== {sid}: the line ended in {dead} of {seeds} · the head wed by {Mid(wed)} · a child in the house by {Mid(child)}");
            foreach (var e in ends) Console.WriteLine(e);   // how each one ended, so that a line lost can be told from a line unlucky
        }
        return 0;
    }

    /// <summary>caesar: over --seeds runs of sixty years with a passive player, how far the generals of the Reach get on the Caesar
    /// road (docs/CAESAR.md): battles won, the most victories and sway any one of them reached, triumphs, recalls, civil wars
    /// and how they ended.</summary>
    public static int Caesar(Options o)
    {
        var (content, scenarios) = Load(o); int seeds = Math.Max(1, Math.Min(o.Seeds, 10));
        foreach (var sid in scenarios)
        {
            long trustSum = 0; int lowYears = 0, highYears = 0, lowRuns = 0, taintedYears = 0;
            int thingsFc = 0, worldsFc = 0, siegeBills = 0; int plundered = 0, surrendered = 0, favGifts = 0, favOwed = 0, favCalled = 0, spoilsAtEnd = 0; int odYears = 0, odDeep = 0, odWith = 0; int clPaid = 0, clForfeit = 0, coSold = 0, foreclosed = 0; int vSettled = 0, vHeldIncome = 0, vFounderCuts = 0, vHeldAtEnd = 0; int stRetaken = 0, stReturned = 0, stRansomed = 0, stHeldAgainst = 0, kinMade = 0, mendGifts = 0; int vCarried = 0, vSoldFailing = 0, vCoCarried = 0, vCoClosed = 0, vSacked = 0, vCreditors = 0, vSecond = 0; int ownBought = 0, ownSold = 0, ownPaid = 0, ownLeft = 0, stakesPaid = 0, ownHolders = 0, ownWorth = 0; int thingsAtEnd = 0, thingsMade = 0, standardsWon = 0, weddingGifts = 0, thingsSold = 0, housesWithNew = 0, housesAtEnd = 0; int ventureYears = 0, lossYears = 0, venturesFailed = 0, venturesAtEnd = 0, officeYears = 0, officeWorse = 0, officeBetter = 0, regionYears = 0, regionWorse = 0, regionLow = 0, patrons = 0, fallings = 0, takenAlong = 0, spoken = 0, wordWars = 0, marches = 0, secessions = 0, freeStates = 0, freeRecognised = 0, freeEnded = 0, charged = 0, convicted = 0, censuresStirred = 0, heldOver = 0, chairSpoke = 0, castings = 0, partings = 0, wed = 0, cold = 0, apart = 0;
            int triumphs = 0, recalls = 0, tMoved = 0, rMoved = 0, wars = 0, ended = 0, broken = 0, maxSway = 0, maxLaurels = 0, maxDevoted = 0, battles = 0, sawFirst = 0, sawLoved = 0, sawGreat = 0, sawTroubled = 0, sawReady = 0, sawWilling = 0, troubledYears = 0, years = 0; var throne = new List<string>(); var greatWars = new List<string>(); int secondWars = 0, joined = 0, departed = 0; int founded = 0, recognised = 0, dissolved = 0;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), seed); var tc = new TurnController(w, content);
                if (w.IsImperium) continue;
                bool first = false, loved = false, great = false, troubled = false, ready = false, willing = false, lowRun = false;
                for (int t = 0; t < 60 && !w.IsDynastyDead; t++)
                {
                    PassiveTurn(tc);
                    battles += tc.Fleets.LastBattles.Count;
                    if (!w.IsImperium) { years++; if (CaesarSystem.Troubled(w)) { troubled = true; troubledYears++; } int tr = LadderSystem.Trust(w); trustSum += tr;
                        foreach (var officeId in LadderSystem.TrustOffices) { officeYears++; int apartBy = w.Counter(LadderSystem.OfficeTrustKey(officeId)); if (apartBy <= -5) officeWorse++; if (apartBy >= 5) officeBetter++; }
                        if (w.Galaxy != null) foreach (var reg in w.Galaxy.Regions) { regionYears++; int regTrust = LadderSystem.RegionTrust(w, reg.Id); if (w.Counter(LadderSystem.RegionTrustKey(reg.Id)) <= -5) regionWorse++; if (regTrust < LadderSystem.TrustLow) regionLow++; } if (tr < LadderSystem.TrustLow) { lowYears++; lowRun = true; } if (tr >= LadderSystem.TrustHigh) highYears++; if (w.Houses.Values.Any(h => LadderSystem.Tainted(w, h.Id))) taintedYears++; }
                    foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId))
                        if (CaesarSystem.GeneralOf(w, h) is { } g) { if (!w.IsImperium && h.Id != RegionSystem.CapitalHolder(w)?.Id && h.Id != w.Protagonist?.HouseId) { bool f1 = CaesarSystem.FirstRank(w, g), l1 = CaesarSystem.Loved(w, g, h); first |= f1; loved |= l1; great |= f1 && l1; if (f1 && Environment.GetEnvironmentVariable("CAESAR_STATS") != null && w.Counter("probe_seen:" + g.Id) == 0) { w.WorldCounters["probe_seen:" + g.Id] = 1; Console.WriteLine($"   FIRST y{w.Turn} {g.Name}: charisma {g.Skill("charisma")} oratory {g.Skill("oratory")} achilles {g.Flags.Contains(FleetSystem.AchillesFlag)} triumphs {CaesarSystem.Triumphs(w, g)} wins {CaesarSystem.Victories(w, g)} devoted {CaesarSystem.DevotedHulls(w, g)} legit {h.Legitimacy} amb '{g.Ambition}' troubles {CaesarSystem.Troubles(w).Count}"); } if (CaesarSystem.Ready(w, h)) { ready = true; if (tc.Caesar.WouldCross(w, h)) willing = true; } } maxSway = Math.Max(maxSway, CaesarSystem.Sway(w, g)); maxLaurels = Math.Max(maxLaurels, CaesarSystem.Laurels(w, g)); maxDevoted = Math.Max(maxDevoted, CaesarSystem.DevotedHulls(w, g)); }
                    tc.EndTurn();
                }
                if (Environment.GetEnvironmentVariable("CAESAR_STATS") != null)
                    foreach (var x in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).Select(h => (h, g: CaesarSystem.GeneralOf(w, h))).Where(x => x.g != null).OrderByDescending(x => CaesarSystem.Victories(w, x.g!)).Take(3))
                        Console.WriteLine($"   seed {seed} y{w.Turn}: {x.g!.Name} wins {CaesarSystem.Victories(w, x.g)} tactics {x.g.Skill("tactics")} charisma {x.g.Skill("charisma")} oratory {x.g.Skill("oratory")} leadership {x.g.Skill("leadership")} triumphs {CaesarSystem.Triumphs(w, x.g)} sway {CaesarSystem.Sway(w, x.g)} hulls {CaesarSystem.DevotedHulls(w, x.g)} amb '{x.g.Ambition}' legit {x.h.Legitimacy} chair {RegionSystem.CapitalHolder(w)?.Id == x.h.Id}");
                if (Environment.GetEnvironmentVariable("CAESAR_DEBUG") != null)
                    foreach (var e in w.History.Where(e => e.Text.Contains("CROSSES") || e.Text.Contains("houses declare") || e.Text.Contains("march on the capital") || e.Text.Contains("SEIZES") || e.Text.Contains("lay down the command") || e.Text.Contains("lays down the command") || e.Text.Contains("rides in triumph") || e.Text.Contains("will not vote")))
                        Console.WriteLine($"   seed {seed} y{e.Turn}: {(e.Text.Length > 230 ? e.Text[..230] : e.Text)}");
                var past = GreatCrisisSystem.Past(w); string fought = string.Join(" then ", past.Select(x => $"from y{x.began} {x.years}y {x.how}"));
                bool forgotten = past.Count > 0 && !w.HasFlag(GreatCrisisSystem.ResolvedFlag); if (past.Count > 1) secondWars++;
                charged += w.Counter("prosecutions"); convicted += w.Counter("convictions"); censuresStirred += w.Counter("censures_stirred"); wordWars += w.Counter(PolitySystem.WarOverWordsCounter); thingsFc += w.Counter(VentureSystem.ThingsForeclosedCounter); worldsFc += w.Counter(VentureSystem.WorldsForeclosedCounter); siegeBills += w.Counter(VentureSystem.SiegeClaimsCounter); plundered += w.Counter(WealthSystem.PlunderedKey); surrendered += w.Counter(WealthSystem.SurrenderedKey); favGifts += w.Counter(WealthSystem.FavourGiftsKey); favOwed += w.Counter(WealthSystem.FavoursOwedKey); favCalled += w.Counter(WealthSystem.FavoursCalledKey); spoilsAtEnd += w.Heirlooms.Count(h => WealthSystem.IsSpoil(h) && w.House(h.FromHouseId) != null); odYears += w.Counter(VentureSystem.OverdrawnYearsCounter); odDeep += w.Counter(VentureSystem.DeepOverdrawnYearsCounter); odWith += w.Counter(VentureSystem.OverdrawnWithVentureCounter); clPaid += w.Counter(VentureSystem.ClaimsPaidCounter); clForfeit += w.Counter(VentureSystem.ClaimsForfeitCounter); coSold += w.Counter(VentureSystem.CompanySoldCounter); foreclosed += w.Counter(VentureSystem.ForeclosedCounter); vSettled += w.Counter(VentureSystem.SettledCounter); vHeldIncome += w.Counter(VentureSystem.HeldIncomeCounter); vFounderCuts += w.Counter(VentureSystem.FounderCutCounter); vHeldAtEnd += w.Businesses.Values.Count(b => VentureSystem.HolderOf(w, b) != null); stRetaken += w.Counter(WealthSystem.RetakenKey); stReturned += w.Counter(WealthSystem.ReturnedKey); stRansomed += w.Counter(WealthSystem.RansomedKey); stHeldAgainst += w.Heirlooms.Count(h => h.Kind == WealthSystem.Standard && w.House(h.FromHouseId) != null); kinMade += w.Counter(WealthSystem.KinMadeKey); mendGifts += w.Counter(WealthSystem.MendKey); vCarried += w.Counter(VentureSystem.CarriedByOthersCounter); vSoldFailing += w.Counter(VentureSystem.SoldFailingCounter); vCoCarried += w.Counter(VentureSystem.CompanyCarriedCounter); vCoClosed += w.Counter(VentureSystem.CompanyClosedCounter); vSacked += w.Counter(VentureSystem.SackedCounter); vCreditors += w.Counter(VentureSystem.CreditorsCounter); vSecond += w.Counter(AmbitionSystem.SecondStartsKey); ownBought += w.Counter(CompanySystem.PrivateBoughtKey); ownSold += w.Counter(CompanySystem.PrivateSoldKey); ownPaid += w.Counter(CompanySystem.PrivateDividendsKey); ownLeft += w.Counter(CompanySystem.PrivateLeftKey); stakesPaid += w.Counter(AmbitionSystem.FounderStakesKey); ownHolders += w.LivingCharacters().Count(c => CompanySystem.HeldPersonally(w, c.Id).Count > 0); ownWorth += w.LivingCharacters().Sum(c => CompanySystem.PersonalWorth(w, c.Id)); thingsAtEnd += w.Heirlooms.Count; thingsMade += w.Counter(WealthSystem.CommissionedKey); standardsWon += w.Counter(WealthSystem.StandardsKey); weddingGifts += w.Counter(WealthSystem.WeddingGiftsKey); thingsSold += w.Counter(WealthSystem.SoldKey); housesAtEnd += w.Houses.Values.Count(h => h.Id != PirateSystem.HouseId); housesWithNew += w.Heirlooms.Where(h => h.Kind is WealthSystem.Shrine or WealthSystem.Standard or WealthSystem.Plate or WealthSystem.Orrery || h.Id.StartsWith("made_")).Select(h => h.OfHouseId).Distinct().Count(); ventureYears += w.Counter(VentureSystem.VentureYearsCounter); lossYears += w.Counter(VentureSystem.LossYearsCounter); venturesFailed += w.Counter(VentureSystem.FailedCounter); venturesAtEnd += w.Businesses.Count; patrons += w.Counter(LadderSystem.PatronsCounter); fallings += w.Counter(LadderSystem.FallingsOutCounter); takenAlong += w.Counter(LadderSystem.TakenAlongCounter); spoken += w.Counter(LadderSystem.SpokenCounter); heldOver += w.Counter(LadderSystem.HeldCounter); chairSpoke += w.Counter(LadderSystem.SpokeCounter); castings += w.Counter(LadderSystem.CastingCounter); partings += w.Counter("divorces_in_the_reach");
                string? myHouse = w.Protagonist?.HouseId;
                foreach (var m in w.Marriages) { if (w.Char(m.A)?.HouseId == myHouse || w.Char(m.B)?.HouseId == myHouse) continue; wed++; if (m.Warmth <= CourtshipSystem.ColdAt) cold++; if (m.Estranged) apart++; }
                if (GreatCrisisSystem.AtWar(w)) greatWars.Add((fought.Length > 0 ? fought + " then " : "") + $"{GreatCrisisSystem.WarYears(w)}y unfinished");
                else if (past.Count > 0) greatWars.Add(fought + (forgotten ? $" (forgotten; {tc.GreatCrisis.Nearness(w)}% toward another)" : ""));
                else greatWars.Add($"none ({tc.GreatCrisis.Nearness(w)}%{(w.Counter(GreatCrisisSystem.EasedCounter) > 0 ? ", stood down " + w.Counter(GreatCrisisSystem.EasedCounter) + "x" : "")})");
                founded += w.Counter("polities_founded"); recognised += w.Counter("polities_recognised"); dissolved += w.Counter("polities_dissolved"); joined += w.Counter("polities_joined"); departed += w.Counter("polities_left"); marches += w.Counter("free_marches"); secessions += w.Counter("secessions"); freeStates += w.Counter("free_states"); freeRecognised += w.Counter("free_states_recognised"); freeEnded += w.Counter("free_states_ended");
                if (lowRun) lowRuns++;
                if (first) sawFirst++; if (loved) sawLoved++; if (great) sawGreat++; if (troubled) sawTroubled++; if (ready) sawReady++; if (willing) sawWilling++;
                if (w.IsImperium) throne.Add($"y{w.History.FirstOrDefault(e => e.Text.Contains("THE THRONE") || e.Text.Contains("PROCLAIMS"))?.Turn}{(w.Counter("republics_ended") > 0 ? " by the road" : w.HasFlag(GreatCrisisSystem.ResolvedFlag) ? " by the great war" : "")}");
                triumphs += w.Counter("triumphs_voted"); recalls += w.Counter("recalls_voted"); tMoved += w.Counter("triumphs_moved"); rMoved += w.Counter("recalls_moved"); wars += w.Counter("civil_wars"); ended += w.Counter("republics_ended"); broken += w.Counter("caesars_broken");
            }
            Console.WriteLine($"== {sid}: battles {battles} · most victories to one name {maxLaurels} · most devoted hulls {maxDevoted} · most sway {maxSway} · triumphs {triumphs} of {tMoved} moved · recalls {recalls} of {rMoved} moved · civil wars {wars} · the Republic fell {ended} · generals broken {broken} · a throne by: {(throne.Count == 0 ? "never" : string.Join(", ", throne))} (of {seeds} runs)");
            Console.WriteLine($"   the great war, how long and how it ended: {(greatWars.Count == 0 ? "never fought" : string.Join(", ", greatWars))} · states that broke away {founded}, recognised {recognised}, brought back {dissolved}; houses that went over to one {joined}, that left one {departed}; runs with a second great war {secondWars} · free marches declared {marches}, rebel worlds that broke from their holder {secessions}: free states {freeStates}, of which taken back {freeEnded} and named in a treaty {freeRecognised}");
            if (officeYears > 0 && regionYears > 0) Console.WriteLine($"   trust told apart: an office's name stood 5 or more under the Reach's in {100 * officeWorse / officeYears}% of office-years and 5 or more over in {100 * officeBetter / officeYears}% · a region believed the magistrates 5 or more less than the Reach in {100 * regionWorse / regionYears}% of region-years, and did not believe them at all (under {LadderSystem.TrustLow}) in {100 * regionLow / regionYears}%");
            Console.WriteLine($"   plunder and favours: carried off when a world fell {plundered} · signets surrendered by a broken house {surrendered} · things still held by the house that took them {spoilsAtEnd} · gifts that asked a favour {favGifts} · favours owed {favOwed}, spent on a vote {favCalled}");
            Console.WriteLine($"   bills and creditors: paid at a peace for what was sacked {clPaid} credits, struck out {clForfeit} · sold by a company rather than closed {coSold} · foreclosed on {foreclosed}, and with no venture left a thing {thingsFc} times and a world {worldsFc} · billed for blockades that failed {siegeBills} credits · a house overdrawn in {odYears} house-years ({odDeep} of them by 20 or more; {odWith} while it owned a venture)");
            Console.WriteLine($"   a venture of one's own: held by a person at the end {vHeldAtEnd} · settled by the head {vSettled} · paid over to holders {vHeldIncome} credits · founders' parts of a sale {vFounderCuts} credits");
            Console.WriteLine($"   standards and gifts: retaken in battle {stRetaken} · bought home {stRansomed} · sent home for nothing {stReturned} · still flown by the house that took them {stHeldAgainst} · made by a relative of the player's house {kinMade} · gifts sent to mend a quarrel {mendGifts}");
            Console.WriteLine($"   a failing venture: carried by its house {vCarried} · sold before it failed {vSoldFailing} · a company's carried {vCoCarried}, closed {vCoClosed} · sacked with its world {vSacked} · paid to creditors out of a failure {vCreditors} credits · second starts {vSecond}");
            Console.WriteLine($"   shares in a person's own name: bought {ownBought} · sold {ownSold} · paid into purses {ownPaid} credits · passed on at a death {ownLeft} · people holding some at the end {ownHolders}, worth {ownWorth} · founders' stakes paid {stakesPaid} credits");
            Console.WriteLine($"   things handed down: {thingsAtEnd} at the end · made to order {thingsMade} · standards taken in battle {standardsWon} · plate sent at weddings {weddingGifts} · sold {thingsSold} · houses holding something they were not born with {housesWithNew} of {housesAtEnd}");
            if (ventureYears > 0) Console.WriteLine($"   ventures: a year without a profit in {100 * lossYears / ventureYears}% of venture-years ({lossYears} of {ventureYears}) · wound up after three such years running {venturesFailed} · standing at the end {venturesAtEnd}");
            Console.WriteLine($"   juniors and those above them: taken up by a patron {patrons} · taken along when the patron rose {takenAlong} · fell out {fallings} · spoken for and went up a step {spoken}");
            Console.WriteLine($"   the chair: motions held over by a Speaker {heldOver} · times a presiding consul spoke from the chair {chairSpoke} · tied votes settled by the casting vote {castings}");
            Console.WriteLine($"   the courts: {charged} former magistrates charged, {convicted} convicted · marriages of other houses at the end {wed}, cold {cold}, living apart {apart}, ended over the years {partings} · censures moved by a house that was stirred {censuresStirred}, wars begun over what was said {wordWars}");
            if (years > 0) Console.WriteLine($"   trust in the magistrates: {trustSum / years} on average · below {LadderSystem.TrustLow} in {lowRuns} of {seeds} runs, {100 * lowYears / years}% of years · at {LadderSystem.TrustHigh} or more in {100 * highYears / years}% of years · some house under a scandal in {100 * taintedYears / years}% of years");
            Console.WriteLine($"   gates, in how many of {seeds} runs another house's general: was of the first rank {sawFirst} · was loved {sawLoved} · was both {sawGreat} · passed all three {sawReady} · and would march {sawWilling} · the Republic was troubled in {sawTroubled} runs, {(years == 0 ? 0 : 100 * troubledYears / years)}% of its years");
        }
        return 0;
    }

    /// <summary>balance: after sixty years, where prices sat against their usual level, what the Reach made and used, the Exchange,
    /// and how the houses stand in the three orders.</summary>
    public static int Balance(Options o)
    {
        var (content, scenarios) = Load(o); int seeds = Math.Max(1, Math.Min(o.Seeds, 4));
        foreach (var sid in scenarios)
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), sid), seed); var tc = new TurnController(w, content);
                var sum = content.Market.Goods.ToDictionary(g => g.Id, _ => 0.0); int n = 0; double ixMin = 100, ixMax = 100; int crashes = 0;
                for (int t = 0; t < 60 && !w.IsDynastyDead; t++)
                {
                    PassiveTurn(tc);
                    if (w.Market != null && !w.InAcademy) { n++; foreach (var g in content.Market.Goods) if (w.Market.Good(g.Id) is { } gs) sum[g.Id] += gs.Price / g.Base; ixMin = Math.Min(ixMin, w.Market.Index); ixMax = Math.Max(ixMax, w.Market.Index); if (w.WorldFlags.Contains(CompanySystem.CrashFlag)) crashes++; }
                    tc.EndTurn();
                }
                var hs = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).ToList(); int total = Math.Max(1, hs.Sum(h => Math.Max(0, h.Wealth))); n = Math.Max(1, n);
                Console.WriteLine($"== {sid} seed {seed}, year {w.Turn}");
                Console.WriteLine("   prices against usual: " + string.Join(" · ", content.Market.Goods.Select(g => $"{g.Id} {sum[g.Id] / n:F2}")));
                if (w.Market != null) Console.WriteLine("   made / used last year: " + string.Join(" · ", content.Market.Goods.Select(g => w.Market.Good(g.Id) is { } gs ? $"{g.Id} {gs.LastProduction:F0}/{gs.LastConsumption:F0}" : g.Id)));
                Console.WriteLine($"   the Exchange: {w.Companies.Count} companies · index {ixMin:F0} to {ixMax:F0} · {crashes} crash(es) · {w.Counter(CompanySystem.SharesTradedCounter)} shares traded · {w.Counter(CompanySystem.TakeoversCounter)} takeover(s)");
                Console.WriteLine($"   wealth: mean credits {hs.Average(h => h.Treasury.Credits):F0} · the richest house holds {100 * hs.Max(h => Math.Max(0, h.Wealth)) / total}% of all wealth");
                string O(string o2) => $"{OrderSystem.Name(o2)} great {hs.Count(h => OrderSystem.Tier(h, o2) == 2)} dominant {hs.Count(h => OrderSystem.Tier(h, o2) == 3)}";
                Console.WriteLine($"   the orders: {O(OrderSystem.Senate)} · {O(OrderSystem.Arms)} · {O(OrderSystem.Commerce)} · callings {string.Join(", ", hs.GroupBy(h => h.Calling).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{(g.Key.Length > 0 ? g.Key : "none")} {g.Count()}"))} · great in two {hs.Count(h => OrderSystem.All.Count(x => OrderSystem.Tier(h, x) >= 2) >= 2)} · clients {hs.Count(h => h.PatronId.Length > 0)} · seats by the purse {hs.Count(h => h.Enrolled)}");
            }
        return 0;
    }
}
