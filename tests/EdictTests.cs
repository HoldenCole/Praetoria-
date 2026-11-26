using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Edicts (docs/EDICTS.md): what the holder of an executive position decrees on their own authority. Each is issued from its own
/// position and no other, bites at once, shows while it lasts in a number a rule computes, lapses with its years or with its issuer, is
/// limited, may be struck down by the Senate except under a throne or emergency powers, survives a save, and is issued by others.</summary>
public class EdictTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static EdictCatalog Cat => Content.Edicts;
    private static (TurnController tc, World w, Character me, House mine) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!;
        Clear(w, mine);
        // every estate indifferent to every house, so that what an edict moves at once does not turn one loyal or hostile and blur a number
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id))) foreach (var e in Content.Policies.Estates) w.WorldCounters[PolicySystem.ApprovalKey(w, h.Id, e.Id)] = 0;
        foreach (var e in Content.Policies.Estates) w.WorldCounters["estate_offset:" + e.Id] = 0;
        return (tc, w, me, mine);
    }
    /// <summary>Nobody of the player's house sits in an office of the ladder or a consulship, and nothing is in force.</summary>
    private static void Clear(World w, House mine)
    {
        w.Magistrates.RemoveAll(m => m.HouseId == mine.Id); w.Offices.RemoveAll(o => o.OfficeId.StartsWith("consul", StringComparison.Ordinal)); w.Emergency = null; w.Edicts.Clear();
        foreach (var k in w.WorldCounters.Keys.Where(k => k.StartsWith("edict_again:")).ToList()) w.WorldCounters.Remove(k);
    }
    private static bool Do(World w, ICommand cmd) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, TestContent.Rng()));
    private static string? No(World w, ICommand cmd) => cmd switch { IssueEdictCommand i => i.Refusal(new CommandContext(w, TestContent.Rng())), WithdrawEdictCommand x => x.Refusal(new CommandContext(w, TestContent.Rng())), _ => null };
    private static IssueEdictCommand Issue(Character c, string id, string target = "") => new(c.Id, id, Content, target);
    private static void Rich(World w, Character c) { w.PoolsFor(c.Id).Influence = 9; w.House(c.HouseId)!.Treasury.Credits = 300; c.Purse = 300; }
    private static Character Kin(World w, House mine, Character me) => w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id && c.Age >= 18).OrderBy(c => c.Id, StringComparer.Ordinal).First();
    private static House Other(World w, House mine, int skip = 0) => w.Houses.Values.Where(h => h.Id != mine.Id && w.OfTheReach(h.Id) && Economy.HeadOf(w, h) != null && w.HoldingsOf(h.Id).Any(x => x.IsPlanet)).OrderBy(h => h.Id, StringComparer.Ordinal).Skip(skip).First();
    private static void Seat(World w, Character c, string rung) { w.Magistrates.RemoveAll(m => m.CharacterId == c.Id); w.Magistrates.Add(new OfficeHolding { OfficeId = rung, HouseId = c.HouseId, CharacterId = c.Id, SinceTurn = w.Turn }); }
    private static void Consul(World w, House h, Character c) { c.Age = Math.Max(c.Age, 45); OfficeSystem.Grant(w, ElectionSystem.ConsulOffices(Content.Offices).First(), h.Id, c); }
    private static void Powers(World w, House h, Character c) => w.Emergency = new EmergencyPowers { HolderId = c.Id, HouseId = h.Id, Cause = "a test", Since = w.Turn, Until = w.Turn + 3 };
    private static void Short(World w, string good = "helium3") { var d = Content.Market.Good(good)!; foreach (var g in w.Market!.Goods.Values) g.Price = Content.Market.Good(g.Id)!.Base; w.Market.Goods[good].Price = d.Base * 2.5; }
    private static List<Holding> Worlds(World w, House h) => w.HoldingsOf(h.Id).Where(x => x.IsPlanet && !x.InRebellion).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
    private static List<Holding> Reach(World w) => w.Holdings.Values.Where(x => x.IsPlanet && !x.InRebellion && w.OfTheReach(x.OwnerId)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
    private static Edict InForce(World w, string def) => w.Edicts.Single(e => e.DefId == def);
    private static World Copy(World w) => SaveSystem.Deserialize(SaveSystem.Serialize(w));
    private static bool Plain(string s) => !s.Contains('—') && !s.Contains('!') && !s.Contains("(s)");

    [Fact]
    public void There_are_about_sixteen_edicts_across_every_position_each_modelled_on_a_real_one_and_each_with_a_reason()
    {
        Assert.InRange(Cat.Defs.Count, 16, 20);
        foreach (var kind in EdictSystem.Issuers) Assert.Contains(Cat.Defs, d => d.Issuer == kind);
        var (tc, w, me, mine) = Start();
        foreach (var d in Cat.Defs)
        {
            Assert.True(d.Model.Length > 10, d.Id + " says what it is modelled on"); Assert.True(d.Description.Length > 40, d.Id);
            Assert.True(Plain(d.Name) && Plain(d.Description) && Plain(d.Model), d.Id + " is written plainly");
            Assert.True(d.Years >= 1 && d.Again >= 1, d.Id); Assert.True(d.Influence > 0, d.Id + " costs something");
            Assert.True(d.Opinions.Values.Any(v => v < 0) && d.Opinions.Values.Any(v => v > 0), d.Id + ": somebody minds and somebody is pleased");
            Assert.True(d.Now.Count + d.Effects.Count > 0, d.Id + " does something");
            if (!d.Reach) Assert.False(d.Annullable, d.Id + ": the Senate does not rule a house's own worlds");
            if (d.Issuer == EdictSystem.Powers) Assert.False(d.Annullable, d.Id + ": there is no annulment under the powers");
            // a reason that some state of the world answers: Reason does not fall through to nothing for a word it does not know
            Assert.Contains(d.When, new[] { "unrest", "war", "red", "unrest_rich", "pinched", "unrest_coin", "reach_unrest", "danger", "debts", "dear", "dear_some", "lawless", "peace", "upright", "exposed", "enemy", "struck_at", "grave" });
            Assert.True(EdictSystem.Does(d, Content.Policies).StartsWith("Costs ", StringComparison.Ordinal)); Assert.True(Plain(EdictSystem.Does(d, Content.Policies)), d.Id);
        }
        // every lasting effect is one a rule reads where the edict puts it: a house's own law for its own worlds, the Reach's for the Reach
        var own = new HashSet<string> { "credits_per_world", "materials_per_world", "manpower_per_world", "unrest_drift", "prosperity_drift", "pop_growth", "lawlessness_drift", "allegiance_drift", "venture_yield_pct", "charter_pct", "market_price_pct", "fleet_upkeep_pct", "readiness", "senate_weight", "legitimacy_per_5", "court_wage", "court_loyalty" };
        var reach = new HashSet<string> { "unrest_drift", "lawlessness_drift", "market_price_pct", "tolls_bonus", "motion_cost", "exchange_levy_pct", "manpower_per_world", "venture_yield_pct", EdictSystem.DebtsStayed, EdictSystem.CourtProof, EdictSystem.PriceCeiling, EdictSystem.SenateCowed };
        foreach (var d in Cat.Defs) foreach (var k in d.Effects.Keys) Assert.True((d.Reach ? reach : own).Contains(k), $"{d.Id}: {k} is read by no rule {(d.Reach ? "for the whole Reach" : "for a house's own worlds")}");
        var motion = Content.Motions.Def(EdictSystem.AnnulKind)!; Assert.Equal("edict", motion.Target); Assert.Equal("Annulment of an edict", motion.Name);
    }

    [Fact]
    public void Every_edict_is_issued_from_its_own_position_and_no_other()
    {
        var (tc, w, me, mine) = Start(); var kin = Kin(w, mine, me); Rich(w, me); Rich(w, kin); Short(w);
        var target = Other(w, mine).Id;
        string Only(EdictDef d) => $"only {EdictSystem.PositionName(d.Issuer)} may issue it";
        void Holds(Character c, params string[] kinds)
        {
            foreach (var d in Cat.Defs)
            {
                var no = No(w, Issue(c, d.Id, d.Target == "house" ? target : ""));
                if (kinds.Contains(d.Issuer)) Assert.True(no != Only(d), $"{c.Name} holds {d.Issuer} and is refused {d.Id}: {no}");
                else Assert.Equal(Only(d), no);
            }
        }
        Holds(kin);                                          // a relative who holds nothing may decree nothing
        Holds(me, EdictSystem.Head);                         // the head, over the house's own worlds
        Seat(w, kin, LadderSystem.Praetor); Holds(kin, EdictSystem.Praetor); Assert.Equal("praetor", EdictSystem.PositionOf(w, kin, EdictSystem.Praetor));
        Seat(w, kin, LadderSystem.Aedile); Holds(kin, EdictSystem.Aedile);
        w.Magistrates.RemoveAll(m => m.CharacterId == kin.Id);
        Consul(w, mine, kin); Holds(kin, EdictSystem.State); Assert.Equal("consul", EdictSystem.PositionOf(w, kin, EdictSystem.State));
        Powers(w, mine, kin); Holds(kin, EdictSystem.State, EdictSystem.Powers); w.Emergency = null; w.Offices.RemoveAll(o => o.CharacterId == kin.Id);
        // the house that holds the capital speaks for the Reach while no consul sits, and not once one does
        var chair = RegionSystem.CapitalHolder(w)!; var chairHead = Economy.HeadOf(w, chair)!; Rich(w, chairHead);
        Assert.Equal("holder of the capital, while no consul sits", EdictSystem.PositionOf(w, chairHead, EdictSystem.State));
        Consul(w, Other(w, mine, 1), Economy.HeadOf(w, Other(w, mine, 1))!); Assert.Null(EdictSystem.PositionOf(w, chairHead, EdictSystem.State)); w.Offices.Clear();
        // under a throne the Emperor decrees for the Reach and with the powers, and there is no ladder
        Seat(w, kin, LadderSystem.Praetor); mine.Title = Praetoria.Core.Progression.Title.Emperor; w.Era = World.EraImperium;
        Holds(me, EdictSystem.Head, EdictSystem.State, EdictSystem.Powers); Holds(kin);
        Assert.Equal("the Emperor", EdictSystem.PositionOf(w, me, EdictSystem.Powers));

        // and each is issued from that position: one at a time, in a fresh Reach
        foreach (var d in Cat.Defs)
        {
            var (_, w2, me2, mine2) = Start(); var kin2 = Kin(w2, mine2, me2); Rich(w2, me2); Rich(w2, kin2); Short(w2);
            Character who = me2;
            if (d.Issuer is EdictSystem.Praetor or EdictSystem.Aedile) { Seat(w2, kin2, d.Issuer); who = kin2; }
            if (d.Issuer == EdictSystem.State) Consul(w2, mine2, me2);
            if (d.Issuer == EdictSystem.Powers) { Consul(w2, mine2, me2); Powers(w2, mine2, me2); }
            if (d.When == "exposed") { }   // a reason is the other houses' business: the player needs none
            Assert.True(Do(w2, Issue(who, d.Id, d.Target == "house" ? Other(w2, mine2).Id : "")), $"{d.Id}: {No(w2, Issue(who, d.Id, d.Target == "house" ? Other(w2, mine2).Id : ""))}");
            var e = InForce(w2, d.Id); Assert.Equal(d.Issuer, e.Issuer); Assert.Equal(who.Id, e.IssuerId); Assert.Equal(d.Reach ? "" : mine2.Id, e.Domain); Assert.Equal(w2.Turn + d.Years, e.Until);
            Assert.Contains(w2.History, h => h.Text.Contains("proclaims " + EdictSystem.Lower(d.Name)));
        }
    }

    [Fact]
    public void What_an_edict_does_at_once()
    {
        // an amnesty: the house's own worlds settle, nobody else's
        var (tc, w, me, mine) = Start(); Rich(w, me); var other = Other(w, mine);
        foreach (var h in w.Holdings.Values) h.Unrest = 30;
        int commons = PolicySystem.Approval(w, mine.Id, "commons");
        Assert.True(Do(w, Issue(me, "amnesty")));
        Assert.All(Worlds(w, mine), h => Assert.Equal(20, h.Unrest)); Assert.All(Worlds(w, other), h => Assert.Equal(30, h.Unrest));
        Assert.Equal(commons + 6, PolicySystem.Approval(w, mine.Id, "commons"));   // what the estates make of it, at once
        Assert.Equal(7, w.PoolsFor(me.Id).Influence);                              // and what it cost
        // a muster: men from every world of the house, now
        int men = mine.Treasury.Manpower; Assert.True(Do(w, Issue(me, "muster"))); Assert.Equal(men + 3 * Worlds(w, mine).Count, mine.Treasury.Manpower);
        // a forced loan: four hundred credits a world, today
        (tc, w, me, mine) = Start(); Rich(w, me); int coin = mine.Treasury.Credits; Assert.True(Do(w, Issue(me, "forced_loan"))); Assert.Equal(coin + 4 * Worlds(w, mine).Count, mine.Treasury.Credits);
        // games: paid for by the house, the worlds quieter, the house better thought of
        (tc, w, me, mine) = Start(); Rich(w, me); foreach (var h in w.Holdings.Values) h.Unrest = 30; mine.Legitimacy = 50; coin = mine.Treasury.Credits;
        Assert.True(Do(w, Issue(me, "games"))); Assert.Equal(coin - 6, mine.Treasury.Credits); Assert.Equal(51, mine.Legitimacy); Assert.All(Worlds(w, mine), h => Assert.Equal(25, h.Unrest));
        // a corn dole, by a consul: every world of the Reach, at the consul's house's cost
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); foreach (var h in w.Holdings.Values) h.Unrest = 30; coin = mine.Treasury.Credits;
        Assert.True(Do(w, Issue(me, "corn_dole"))); Assert.Equal(coin - 10, mine.Treasury.Credits); Assert.All(Reach(w), h => Assert.Equal(25, h.Unrest));
        // the lanes closed: every region of the Reach is the less lawless at once
        var law = w.Galaxy!.ReachRegions.ToDictionary(r => r.Id, r => RegionSystem.StateOf(w, r.Id).Lawlessness = 40);
        Assert.True(Do(w, Issue(me, "close_lanes"))); Assert.All(w.Galaxy.ReachRegions, r => Assert.Equal(32, RegionSystem.StateOf(w, r.Id).Lawlessness));
        // a requisition: every other house sends a man to the consul's standards, and its head thinks the less of the consul for it
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); other = Other(w, mine); var senders = w.Houses.Values.Where(h => h.Id != mine.Id && w.OfTheReach(h.Id) && h.Treasury.Manpower > 0).ToList();
        men = mine.Treasury.Manpower; var head = Economy.HeadOf(w, other)!; int regard = w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0; int theirs = other.Treasury.Manpower;
        Assert.True(Do(w, Issue(me, "requisition"))); Assert.Equal(men + senders.Count, mine.Treasury.Manpower); Assert.Equal(theirs - 1, other.Treasury.Manpower);
        Assert.Equal(regard + EdictSystem.RequisitionRegard, w.GetRelationship(head.Id, me.Id)!.Disposition);
        // a thanksgiving: every region a little closer to the capital, the magistrates a little better thought of
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); foreach (var r in w.Galaxy!.ReachRegions) RegionSystem.StateOf(w, r.Id).Allegiance = 50; w.WorldCounters[LadderSystem.TrustKey] = 50;
        Assert.True(Do(w, Issue(me, "thanksgiving"))); Assert.All(w.Galaxy.ReachRegions, r => Assert.Equal(54, RegionSystem.StateOf(w, r.Id).Allegiance)); Assert.Equal(52, LadderSystem.Trust(w));
        // the praetor's edicts and the aedile's move the Reach's trust in its magistrates; the aedile of the player's house pays from the purse
        (tc, w, me, mine) = Start(); var kin = Kin(w, mine, me); Rich(w, kin); Seat(w, kin, LadderSystem.Praetor); w.WorldCounters[LadderSystem.TrustKey] = 50;
        Assert.True(Do(w, Issue(kin, "strict_letter"))); Assert.Equal(52, LadderSystem.Trust(w));
        Seat(w, kin, LadderSystem.Aedile); int purse = kin.Purse; coin = mine.Treasury.Credits; Assert.True(Do(w, Issue(kin, "market_edict"))); Assert.Equal(53, LadderSystem.Trust(w)); Assert.Equal(purse - 3, kin.Purse); Assert.Equal(coin, mine.Treasury.Credits);
        // proscriptions: half the house named, up to three thousand credits, to the holder's house; its standing and its head's regard fall
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); Powers(w, mine, me); other = Other(w, mine); other.Treasury.Credits = 100; other.Legitimacy = 50; mine.Legitimacy = 50;
        head = Economy.HeadOf(w, other)!; coin = mine.Treasury.Credits; var bystander = Economy.HeadOf(w, Other(w, mine, 1))!;
        w.Relationship(head.Id, me.Id).Disposition = 0; w.Relationship(bystander.Id, me.Id).Disposition = 0;
        Assert.True(Do(w, Issue(me, "proscription", other.Id))); Assert.Equal(70, other.Treasury.Credits); Assert.Equal(coin + 30, mine.Treasury.Credits);
        Assert.Equal(50 - EdictSystem.ProscribedStanding, other.Legitimacy); Assert.Equal(47, mine.Legitimacy);
        Assert.Equal(EdictSystem.ProscribedRegard - 8, w.GetRelationship(head.Id, me.Id)!.Disposition); Assert.Equal(-8, w.GetRelationship(bystander.Id, me.Id)!.Disposition);
        // a levy in mass: two more men from every world of the Reach, for the house that holds it
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); Powers(w, mine, me); var before = w.Houses.Values.ToDictionary(h => h.Id, h => h.Treasury.Manpower);
        Assert.True(Do(w, Issue(me, "levy_in_mass")));
        foreach (var h in w.Houses.Values.Where(h => w.OfTheReach(h.Id))) Assert.Equal(before[h.Id] + 2 * Worlds(w, h).Count, h.Treasury.Manpower);
    }

    [Fact]
    public void Each_lasting_effect_shows_in_a_number_a_rule_computes()
    {
        var (tc, w, me, mine) = Start(); var eco = tc.Economy!; var other = Other(w, mine); Rich(w, me);
        var myWorld = Worlds(w, mine)[0]; var theirWorld = Worlds(w, other)[0];
        int Unrest(Holding h) => eco.UnrestWhy(w, h).Sum(x => x.by);
        int Credits(House h) => eco.Compute(w, h.Id, tc.Fleets).Net.Credits;
        int Manpower(House h) => eco.Compute(w, h.Id, tc.Fleets).Net.Manpower;
        // an amnesty: a point less unrest a year on the house's worlds, and none less on another's
        int mineU = Unrest(myWorld), theirU = Unrest(theirWorld);
        Assert.True(Do(w, Issue(me, "amnesty"))); Assert.Equal(mineU - 1, Unrest(myWorld)); Assert.Equal(theirU, Unrest(theirWorld));
        // a forced loan is paid back: a hundred credits a world a year
        int coin = Credits(mine); Assert.True(Do(w, Issue(me, "forced_loan"))); Assert.Equal(coin - Worlds(w, mine).Count, Credits(mine));
        // a muster: the worlds' prosperity settles four lower, and they are a point the more restless
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); myWorld = Worlds(w, mine)[0];
        int target = eco.ProsperityTarget(w, myWorld); mineU = Unrest(myWorld);
        Assert.True(Do(w, Issue(me, "muster"))); Assert.Equal(Math.Max(0, target - 4), eco.ProsperityTarget(w, myWorld)); Assert.Equal(mineU + 1, Unrest(myWorld));
        // a sumptuary edict: a hundred credits a world saved, and the house's court paid less and the less content
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); mine.Court.Clear(); mine.Court.Add(new Retainer { Id = "r_test", Role = CourtSystem.Positions[0], Name = "Test", Wage = 30, Loyalty = 40, Since = w.Turn });
        var plain = Copy(w); coin = Credits(mine);
        Assert.True(Do(w, Issue(me, "sumptuary"))); Assert.Equal(coin + Worlds(w, mine).Count, Credits(mine));
        CourtSystem.Tick(plain, Content.Policies); CourtSystem.Tick(w, Content.Policies);
        var pm = plain.House(mine.Id)!; Assert.Equal(100L * pm.Treasury.Credits + pm.Treasury.Coin + 1, 100L * mine.Treasury.Credits + mine.Treasury.Coin);   // a credit less in wages
        Assert.Equal(pm.Court[0].Loyalty - 2, mine.Court[0].Loyalty);
        // a remission of the tithe: two hundred credits a world less, and the worlds fill faster
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); coin = Credits(mine); var room = Worlds(w, mine).First(h => h.Population > 0 && Content.Holdings.Spec(h.Specialization) is { } s && (s.MaxPopulation == 0 || h.Population < s.MaxPopulation - 5));
        plain = Copy(w); Assert.True(Do(w, Issue(me, "jubilee"))); Assert.Equal(coin - 2 * Worlds(w, mine).Count, Credits(mine));
        foreach (var h in w.Holdings.Values) { h.Unrest = 0; plain.Holding(h.Id)!.Unrest = 0; }   // set aside what it did at once
        eco.Accrue(plain); eco.Accrue(w); Assert.Equal(plain.Holding(room.Id)!.Population + 1, w.Holding(room.Id)!.Population);
        // a corn dole and a thanksgiving: every world of the Reach a point the quieter a year
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); Consul(w, mine, me); other = Other(w, mine); theirWorld = Worlds(w, other)[0]; theirU = Unrest(theirWorld);
        Assert.True(Do(w, Issue(me, "corn_dole"))); Assert.Equal(theirU - 1, Unrest(theirWorld));
        Assert.True(Do(w, Issue(me, "thanksgiving"))); Assert.Equal(theirU - 2, Unrest(theirWorld));
        // a requisition: a man more a year from every world of every house, and a point more unrest
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); Consul(w, mine, me); other = Other(w, mine); int theirMen = Manpower(other); theirU = Unrest(Worlds(w, other)[0]);
        Assert.True(Do(w, Issue(me, "requisition"))); Assert.InRange(Manpower(other) - theirMen, 1, Worlds(w, other).Count); Assert.Equal(theirU + 1, Unrest(Worlds(w, other)[0]));
        // the lanes closed: lawlessness falls two more a year in every region, and a toll brings in a hundred credits less
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); Consul(w, mine, me);
        var tolled = w.Houses.Values.FirstOrDefault(h => eco.Compute(w, h.Id, tc.Fleets).Lines.Any(l => l.Kind == "tolls")); Assert.NotNull(tolled);
        int Tolls() => eco.Compute(w, tolled!.Id, tc.Fleets).Lines.Where(l => l.Kind == "tolls").Sum(l => l.Delta.Credits); int tolls = Tolls(), gates = eco.Compute(w, tolled!.Id, tc.Fleets).Lines.Count(l => l.Kind == "tolls");
        plain = Copy(w); foreach (var r in w.Galaxy!.ReachRegions) { RegionSystem.StateOf(w, r.Id).Lawlessness = 50; RegionSystem.StateOf(plain, r.Id).Lawlessness = 50; }
        Assert.True(Do(w, Issue(me, "close_lanes"))); Assert.Equal(tolls - gates, Tolls());
        foreach (var r in w.Galaxy.ReachRegions) RegionSystem.StateOf(w, r.Id).Lawlessness = 50;   // set aside what it did at once
        tc.Regions.Tick(plain); tc.Regions.Tick(w);
        Assert.All(w.Galaxy.ReachRegions, r => Assert.Equal(Math.Max(0, RegionSystem.StateOf(plain, r.Id).Lawlessness - 2), RegionSystem.StateOf(w, r.Id).Lawlessness));
        // the aedile's edict: everything on the exchanges eight in the hundred cheaper to buy, for every house
        (tc, w, me, mine) = Start(); var kin = Kin(w, mine, me); Rich(w, kin); Seat(w, kin, LadderSystem.Aedile); other = Other(w, mine);
        double buy = MarketSystem.BuyPrice(w, Content.Trade, other.Id, "materials"), own = PolicySystem.Modifier(w, Content.Policies, other.Id, "market_price_pct") + PolicySystem.ReachModifier(w, Content.Policies, "market_price_pct");
        Assert.True(Do(w, Issue(kin, "market_edict")));
        Assert.Equal(buy * (1 + (own - 8) / 100.0) / (1 + own / 100.0), MarketSystem.BuyPrice(w, Content.Trade, other.Id, "materials"), 6);
        // an edict of maximum prices: the short good is bought and sold at no more than its ceiling; the others are not touched
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); Short(w, "helium3"); other = Other(w, mine);
        double he = MarketSystem.BuyPrice(w, Content.Trade, other.Id, "helium3"), heSell = MarketSystem.SellPrice(w, Content.Trade, other.Id, "helium3"), mat = MarketSystem.BuyPrice(w, Content.Trade, other.Id, "materials");
        Assert.True(Do(w, Issue(me, "price_edict"))); Assert.Equal("helium3", InForce(w, "price_edict").TargetId);
        Assert.Equal(he * 1.25 / 2.5, MarketSystem.BuyPrice(w, Content.Trade, other.Id, "helium3"), 6); Assert.Equal(heSell * 1.25 / 2.5, MarketSystem.SellPrice(w, Content.Trade, other.Id, "helium3"), 6);
        Assert.Equal(mat, MarketSystem.BuyPrice(w, Content.Trade, other.Id, "materials"), 9);
        // and between houses: the market's price a house names is held under the ceiling, and so is what a seller asks of a buyer its head dislikes
        var heDef = Content.Market.Good("helium3")!; double ceiling = heDef.Base * 125 / 100.0; var seller = Other(w, mine, 1);
        w.Relationship(Economy.HeadOf(w, seller)!.Id, me.Id).Disposition = -30;
        Assert.Equal(ceiling, tc.HouseTrade.Mid(w, "helium3"), 6); Assert.Equal(ceiling, tc.HouseTrade.Asks(w, seller, mine, "helium3"), 6);
        Assert.True(tc.HouseTrade.Pays(w, mine, seller, "helium3") <= ceiling);
        Assert.Equal(w.Market!.Goods["materials"].Price, tc.HouseTrade.Mid(w, "materials"), 9);
        EdictSystem.End(w, InForce(w, "price_edict"), EdictSystem.Withdrawn); Assert.Equal(heDef.Base * 2.5, tc.HouseTrade.Mid(w, "helium3"), 6);
        // rule by decree: no house but the holder's may move a motion, and the others bring none of their own
        (tc, w, me, mine) = Start(); other = Other(w, mine); var holder = Economy.HeadOf(w, other)!; Rich(w, holder); Consul(w, other, holder); Powers(w, other, holder); w.Motions.Clear();
        Assert.True(Do(w, Issue(holder, "rule_by_decree")));
        Assert.Contains("rules by decree", new ProposeMotionCommand(me.Id, "levy", "", tc.Senate).Refusal(new CommandContext(w, TestContent.Rng())));
        for (int y = 0; y < 3; y++) { w.Turn++; tc.Senate.Tick(w); }
        Assert.DoesNotContain(w.Motions, m => !SenateSystem.IsPetition(m));
        // proscriptions and a levy in mass: unrest on every world while they run
        (tc, w, me, mine) = Start(); eco = tc.Economy!; Rich(w, me); Consul(w, mine, me); Powers(w, mine, me); other = Other(w, mine); theirWorld = Worlds(w, other)[0]; theirU = Unrest(theirWorld);
        Assert.True(Do(w, Issue(me, "proscription", Other(w, mine, 1).Id))); Assert.Equal(theirU + 1, Unrest(theirWorld));
        Assert.True(Do(w, Issue(me, "levy_in_mass"))); Assert.Equal(theirU + 3, Unrest(theirWorld));
    }

    [Fact]
    public void The_praetors_edicts_move_how_often_a_charge_is_found_proved()
    {
        var (tc, w, me, mine) = Start(); var accused = Other(w, mine); var accuser = Other(w, mine, 1); var bench = Other(w, mine, 2);
        var who = Economy.HeadOf(w, accused)!;
        var judge = new Character { Id = "judge_test", Name = "Test Judge", HouseId = bench.Id, Alive = true, Age = 50 }; w.Characters[judge.Id] = judge;
        foreach (var h in new[] { accused, accuser }) if (Economy.HeadOf(w, bench) is { } bh && Economy.HeadOf(w, h) is { } hh) w.Relationship(bh.Id, hh.Id).Disposition = 0;
        int plead = LadderSystem.Craft(LadderSystem.Advocate(w, accuser.Id)), defence = Math.Max(0, (plead * 6 - 10) / 4);   // so that the plain chance is near half
        int Proved() => Enumerable.Range(0, 300).Count(i => LadderSystem.Verdict(w, new OfficeDeed { Id = $"deed_test_{i}", CharacterId = who.Id, HouseId = accused.Id, RungId = LadderSystem.Quaestor, What = LadderSystem.TookAccounts }, accuser.Id, judge, defence).guilty);
        int plain = Proved();
        var kin = Kin(w, mine, me); Rich(w, kin); Seat(w, kin, LadderSystem.Praetor);
        Assert.True(Do(w, Issue(kin, "strict_letter"))); int strict = Proved();
        Assert.Contains("cannot be in force together", No(w, Issue(kin, "equity")));
        EdictSystem.End(w, InForce(w, "strict_letter"), EdictSystem.Withdrawn); w.WorldCounters.Remove(EdictSystem.AgainKey("", "equity"));
        Assert.True(Do(w, Issue(kin, "equity"))); int equity = Proved();
        Assert.InRange(strict - plain, 25, 65); Assert.InRange(plain - equity, 25, 65);   // fifteen in a hundred, either way, of three hundred charges
    }

    [Fact]
    public void A_moratorium_stops_every_instalment_and_every_interest_and_no_creditor_may_press()
    {
        var (tc, w, me, mine) = Start(); var eco = tc.Economy!; Rich(w, me); Consul(w, mine, me);
        var lender = Other(w, mine); var borrower = Other(w, mine, 1); lender.Treasury.Credits = 100; borrower.Treasury.Credits = 0;
        var loan = tc.Dealings.Lend(w, lender, borrower, 10, 20); borrower.Treasury.Credits = 0; loan.Missed = 0;
        // and a house that owes a bank: the instalment, and the bank's interest
        var bank = Other(w, mine, 2); mine.Debt = 20; mine.LenderId = bank.Id; mine.LoanInstalment = 5;
        Assert.Contains(eco.Compute(w, mine.Id, tc.Fleets).Lines, l => l.Kind == "loan"); Assert.Contains(eco.Compute(w, bank.Id, tc.Fleets).Lines, l => l.Source == "interest on loans");
        borrower.Treasury.Credits = 50; Assert.Equal(loan.Instalment, DealingsSystem.Due(w, loan)); borrower.Treasury.Credits = 0;
        var plain = Copy(w);
        Assert.True(Do(w, Issue(me, "moratorium"))); Assert.True(EdictSystem.DebtsAreStayed(w));
        borrower.Treasury.Credits = 50; Assert.Equal(0, DealingsSystem.Due(w, loan)); borrower.Treasury.Credits = 0;
        Assert.Equal(0, Lending.InstalmentOf(w, mine, Content.Businesses.Finance));
        Assert.DoesNotContain(eco.Compute(w, mine.Id, tc.Fleets).Lines, l => l.Kind == "loan"); Assert.DoesNotContain(eco.Compute(w, bank.Id, tc.Fleets).Lines, l => l.Source == "interest on loans");
        // and the steward says so: nothing falls due while it stands
        var stewardSays = Praetoria.Core.Presentation.AdvisorReports.Build(w, Content).Single(r => r.Seat == "Steward").Line;
        Assert.Contains($"nothing falls due while a moratorium on debts stands, until year {InForce(w, "moratorium").Until}", stewardSays); Assert.DoesNotContain("instalments fall due", stewardSays);
        // a year: without it the borrower would have missed a payment and the bank been paid its interest; under it, nothing moves
        int owed = loan.Owed, debt = mine.Debt; var plainBank = plain.House(bank.Id)!; int bankWas = plainBank.Treasury.Credits;
        eco.Accrue(plain); eco.Accrue(w);
        var plainLoan = plain.HouseLoans.Single(l => l.LenderId == lender.Id && l.BorrowerId == borrower.Id);
        Assert.Equal(1, plainLoan.Missed); Assert.Equal(0, loan.Missed); Assert.Equal(owed, loan.Owed); Assert.Equal(debt, mine.Debt); Assert.True(plain.House(mine.Id)!.Debt < debt);
        // no creditor may press while it lasts, and the others' creditors do not
        loan.Missed = 1; var mineLent = tc.Dealings.Lend(w, mine, Other(w, mine, 3), 5, 10); mineLent.Missed = 1;
        Assert.Equal($"a moratorium on debts is in force until year {InForce(w, "moratorium").Until}: no creditor may press until it lapses", tc.Dealings.PressRefusal(w, me, Other(w, mine, 3)));
        tc.Dealings.Tick(w); Assert.False(loan.Pressed);
        // when it lapses the instalments fall due again
        EdictSystem.End(w, InForce(w, "moratorium"), EdictSystem.Lapsed); Assert.False(EdictSystem.DebtsAreStayed(w));
        borrower.Treasury.Credits = 50; Assert.Equal(loan.Instalment, DealingsSystem.Due(w, loan)); Assert.Equal(5, Lending.InstalmentOf(w, mine, Content.Businesses.Finance));
    }

    [Fact]
    public void An_edict_lapses_with_its_years_and_may_not_be_issued_again_for_a_while()
    {
        var (tc, w, me, mine) = Start(); Rich(w, me); int t = w.Turn;
        Assert.True(Do(w, Issue(me, "amnesty"))); var e = InForce(w, "amnesty"); Assert.Equal(t + 3, e.Until);
        var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        w.Turn = t + 2; EdictSystem.Tick(w, Content, ex, ctx); Assert.Contains(w.Edicts, x => x.Id == e.Id);
        w.Turn = t + 3; EdictSystem.Tick(w, Content, ex, ctx); Assert.DoesNotContain(w.Edicts, x => x.Id == e.Id);
        Assert.Contains(w.History, h => h.Text.Contains("An amnesty of " + me.Name + " lapses"));
        Assert.Contains(w.News, n => n.Text.Contains("lapses") && n.Target == "laws");
        Assert.True(w.Counter(EdictSystem.LapsedKey) >= 1);
        Rich(w, me); Assert.Equal($"an amnesty was proclaimed lately: it may be again in year {t + 3 + 8}", No(w, Issue(me, "amnesty")));
        w.Turn = t + 11; Assert.Null(No(w, Issue(me, "amnesty")));
    }

    [Fact]
    public void An_edict_lapses_early_when_its_issuer_leaves_the_position_unless_it_outlives_them()
    {
        var (tc, w, me, mine) = Start(); var kin = Kin(w, mine, me); Rich(w, kin); Seat(w, kin, LadderSystem.Praetor);
        var ctx = new CommandContext(w, TestContent.Rng()); var ex = new CommandExecutor();
        Assert.True(Do(w, Issue(kin, "strict_letter")));
        w.Magistrates.RemoveAll(m => m.CharacterId == kin.Id); EdictSystem.Tick(w, Content, ex, ctx);
        Assert.DoesNotContain(w.Edicts, e => e.DefId == "strict_letter"); Assert.True(w.Counter(EdictSystem.GoneKey) >= 1);
        Assert.Contains(w.History, h => h.Text.Contains("lapses with the position it was issued from"));
        // another house's head proclaims an amnesty (which outlives them) and a muster (which does not), and dies
        var other = Other(w, mine); var head = Economy.HeadOf(w, other)!; Rich(w, head);
        Assert.True(Do(w, Issue(head, "amnesty"))); Assert.True(Do(w, Issue(head, "muster")));
        head.Alive = false; Assert.NotEqual(head.Id, Economy.HeadOf(w, other)?.Id);
        EdictSystem.Tick(w, Content, ex, ctx);
        Assert.Contains(w.Edicts, e => e.DefId == "amnesty" && e.Domain == other.Id); Assert.DoesNotContain(w.Edicts, e => e.DefId == "muster" && e.Domain == other.Id);
        // a consul's edict ends with the consulship
        (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); ctx = new CommandContext(w, TestContent.Rng());
        Assert.True(Do(w, Issue(me, "close_lanes"))); w.Offices.Clear(); EdictSystem.Tick(w, Content, ex, ctx); Assert.DoesNotContain(w.Edicts, e => e.IssuerId == me.Id);
        // and it ends in the same year's turn as the position: powers laid down at the turn of the year take their edicts with them,
        // though the year's edicts were looked at before the powers ended
        (tc, w, me, mine) = Start(); other = Other(w, mine); head = Economy.HeadOf(w, other)!; Rich(w, head); Consul(w, other, head); Powers(w, other, head); w.Emergency!.Until = w.Turn;
        Assert.True(Do(w, Issue(head, "levy_in_mass"))); var levy = InForce(w, "levy_in_mass"); int gone = w.Counter(EdictSystem.GoneKey);
        tc.EndTurn();
        Assert.NotEqual(head.Id, EmergencySystem.Holder(w)?.Id); Assert.DoesNotContain(w.Edicts, e => e.Id == levy.Id || e.Issuer == EdictSystem.Powers && e.IssuerId == head.Id);
        Assert.True(w.Counter(EdictSystem.GoneKey) > gone); Assert.Contains(w.History, h => h.Text.Contains("A levy in mass of " + head.Name + " lapses with the position it was issued from"));
    }

    [Fact]
    public void The_limits_one_of_a_kind_two_a_person_none_lately_ended_and_the_cost()
    {
        var (tc, w, me, mine) = Start(); Rich(w, me); var other = Other(w, mine); var head = Economy.HeadOf(w, other)!; Rich(w, head);
        Assert.True(Do(w, Issue(me, "amnesty")));
        Assert.Equal($"an amnesty is in force already on the house's worlds, until year {w.Turn + 3}", No(w, Issue(me, "amnesty")));
        Assert.True(Do(w, Issue(head, "amnesty")));   // one of a kind over the same worlds: another house's are its own
        Assert.True(Do(w, Issue(me, "muster")));
        Assert.Equal($"you have 2 edicts in force, and {EdictSystem.MaxPerIssuer} is the most one person may have", No(w, Issue(me, "games")));
        // taken back: what has been given cannot be, and only by the one who issued it
        Assert.Equal("an amnesty has been given and cannot be taken back", No(w, new WithdrawEdictCommand(me.Id, w.Edicts.Single(e => e.DefId == "amnesty" && e.Domain == mine.Id).Id)));
        var muster = w.Edicts.Single(e => e.DefId == "muster");
        Assert.Equal("only the one who issued it may take it back", No(w, new WithdrawEdictCommand(head.Id, muster.Id)));
        Assert.True(Do(w, new WithdrawEdictCommand(me.Id, muster.Id))); Assert.DoesNotContain(w.Edicts, e => e.Id == muster.Id); Assert.Equal(1, w.Counter(EdictSystem.WithdrawnKey));
        Assert.Contains("was proclaimed lately", No(w, Issue(me, "muster")));
        // what it costs
        w.PoolsFor(me.Id).Influence = 0; Assert.Equal("needs 1 influence this year (0 left)", No(w, Issue(me, "games")));
        w.PoolsFor(me.Id).Influence = 9; mine.Treasury.Credits = 2; Assert.Equal("needs 600 credits and the house has 200", No(w, Issue(me, "games")));
        var kin = Kin(w, mine, me); Rich(w, kin); Seat(w, kin, LadderSystem.Aedile); kin.Purse = 1; Assert.Equal("needs 300 credits of your own, and you have 100", No(w, Issue(kin, "market_edict")));
        // a proscription names a house of the Reach, not one's own
        Assert.Equal($"only {EdictSystem.PositionName(EdictSystem.Powers)} may issue it", No(w, Issue(me, "proscription")));
        Rich(w, me); Consul(w, mine, me); Powers(w, mine, me); Assert.Equal("name a house of the Reach", No(w, Issue(me, "proscription"))); Assert.Equal("not your own house", No(w, Issue(me, "proscription", mine.Id)));
        foreach (var g in w.Market!.Goods.Values) g.Price = Content.Market.Good(g.Id)!.Base;
        Assert.StartsWith("nothing is short", No(w, Issue(me, "price_edict")));
        // a house with no world has nothing for its own edicts to fall on
        Rich(w, me); Assert.Null(No(w, Issue(me, "games")));
        foreach (var h in w.HoldingsOf(mine.Id).Where(x => x.IsPlanet).ToList()) h.OwnerId = other.Id;
        w.HoldingsChanged(); Assert.Equal("the house holds no world for it to fall on", No(w, Issue(me, "games")));
    }

    [Fact]
    public void The_Senate_strikes_down_an_edict_it_carries_an_annulment_of_and_lets_stand_one_it_refuses()
    {
        foreach (bool carried in new[] { true, false })
        {
            var (tc, w, me, mine) = Start(); var other = Other(w, mine); var consul = Economy.HeadOf(w, other)!; Rich(w, consul); Rich(w, me); Consul(w, other, consul); w.Motions.Clear();
            Assert.True(Do(w, Issue(consul, "requisition"))); var e = InForce(w, "requisition"); other.Legitimacy = 50;
            var move = new ProposeMotionCommand(me.Id, EdictSystem.AnnulKind, e.Id, tc.Senate);
            Assert.Null(move.Refusal(new CommandContext(w, TestContent.Rng())));
            Assert.True(new CommandExecutor().TryExecute(move, new CommandContext(w, TestContent.Rng())));
            var m = SenateSystem.Docket(w).Single(); Assert.Equal(e.Id, m.TargetId); Assert.Contains("strike down a requisition of men for the state of " + consul.Name, tc.Senate.Describe(w, m));
            Assert.Equal("its annulment is already before the Senate", EdictSystem.AnnulRefusal(w, e.Id, Other(w, mine, 1).Id));
            foreach (var h in w.Houses.Values) m.Votes[h.Id] = carried ? 1 : -1;
            w.Turn++; tc.Senate.Tick(w);
            Assert.True(m.Resolved); Assert.Equal(carried, m.Passed);
            if (carried)
            {
                Assert.DoesNotContain(w.Edicts, x => x.Id == e.Id); Assert.Equal(50 - EdictSystem.AnnulledStanding, other.Legitimacy); Assert.Equal(1, w.Counter(EdictSystem.AnnulledKey));
                Assert.Contains(w.History, h => h.Text.Contains("The Senate strikes down a requisition"));
                Assert.Contains($"may be again in year {w.Turn + e.Again}", No(w, Issue(consul, "requisition")));
            }
            else { Assert.Contains(w.Edicts, x => x.Id == e.Id); Assert.Equal(50, other.Legitimacy); }
        }
    }

    [Fact]
    public void Who_leans_for_an_annulment_and_who_moves_one()
    {
        var (tc, w, me, mine) = Start(); var issuerHouse = Other(w, mine); var consul = Economy.HeadOf(w, issuerHouse)!; Rich(w, consul); Consul(w, issuerHouse, consul);
        var voices = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != issuerHouse.Id && w.OfTheReach(h.Id) && Economy.HeadOf(w, h) != null && tc.Senate.MayPropose(w, h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var lender = voices[0]; var borrower = voices[1]; var stranger = voices[2];
        foreach (var h in w.Houses.Values) if (Economy.HeadOf(w, h) is { } hh && hh.Id != consul.Id) w.Relationship(hh.Id, consul.Id).Disposition = 0;
        Assert.True(Do(w, Issue(consul, "moratorium"))); var e = InForce(w, "moratorium");
        Assert.Equal(-4, EdictSystem.AnnulLean(w, issuerHouse, e));   // the issuer's own house
        int lenderWas = EdictSystem.AnnulLean(w, lender, e), borrowerWas = EdictSystem.AnnulLean(w, borrower, e), neither = EdictSystem.AnnulLean(w, stranger, e);
        lender.Treasury.Credits = 100; borrower.Treasury.Credits = 0; tc.Dealings.Lend(w, lender, borrower, 10, 20);
        int lends = EdictSystem.AnnulLean(w, lender, e), owes = EdictSystem.AnnulLean(w, borrower, e);
        Assert.Equal(lenderWas + 3, lends); Assert.Equal(borrowerWas - 2, owes);
        // what its head thinks of the issuer: a friend stands by the edict, an enemy wants it gone
        var sh = Economy.HeadOf(w, stranger)!; w.Relationship(sh.Id, consul.Id).Disposition = 40; int friend = EdictSystem.AnnulLean(w, stranger, e);
        w.Relationship(sh.Id, consul.Id).Disposition = -40; int enemy = EdictSystem.AnnulLean(w, stranger, e); Assert.Equal(friend + 4, enemy);
        // the estates loyal to a house: merchants who are loyal to it gall at a moratorium
        w.Relationship(sh.Id, consul.Id).Disposition = 0; w.WorldCounters[PolicySystem.ApprovalKey(w, stranger.Id, "merchants")] = 40; Assert.Equal(neither + 2, EdictSystem.AnnulLean(w, stranger, e));
        // the lean is the Senate's own: it is what the chamber counts
        var m = new Motion { Id = "m_test", Kind = EdictSystem.AnnulKind, ProposerHouseId = stranger.Id, TargetId = e.Id, ProposedTurn = w.Turn };
        Assert.Contains(tc.Senate.Why(w, m, lender.Id), x => x.why.StartsWith("what the edict does to it") && x.by == lends);
        // another house the edict galls moves it, in some year
        w.Relationship(sh.Id, consul.Id).Disposition = 0; w.WorldCounters[PolicySystem.ApprovalKey(w, stranger.Id, "merchants")] = 0;
        w.Motions.Clear(); bool moved = false;
        for (int y = 0; y < 8 && !moved; y++) { w.Turn++; e.Until = w.Turn + 2; moved = EdictSystem.MoveAnnulment(w, tc.Senate, mine.Id); }
        Assert.True(moved); var mv = w.Motions.Single(x => x.Kind == EdictSystem.AnnulKind); Assert.Equal(e.Id, mv.TargetId); Assert.NotEqual(mine.Id, mv.ProposerHouseId); Assert.True(EdictSystem.AnnulLean(w, w.House(mv.ProposerHouseId)!, e) >= EdictSystem.MoveAt);
    }

    [Fact]
    public void There_is_no_annulment_under_a_throne_or_while_emergency_powers_run_nor_of_what_cannot_be_annulled()
    {
        var (tc, w, me, mine) = Start(); var other = Other(w, mine); var consul = Economy.HeadOf(w, other)!; Rich(w, consul); Rich(w, me); Consul(w, other, consul);
        Assert.True(Do(w, Issue(consul, "close_lanes"))); var e = InForce(w, "close_lanes");
        Assert.Null(EdictSystem.AnnulRefusal(w, e.Id, mine.Id));
        Assert.Equal($"it is your own house's edict, and only {consul.Name} may take it back", EdictSystem.AnnulRefusal(w, e.Id, other.Id));
        Powers(w, other, consul); Assert.Equal("while the emergency powers run the Senate does not sit in judgement on an edict", EdictSystem.AnnulRefusal(w, e.Id, mine.Id));
        Assert.Equal("while the emergency powers run the Senate does not sit in judgement on an edict", new ProposeMotionCommand(me.Id, EdictSystem.AnnulKind, e.Id, tc.Senate).Refusal(new CommandContext(w, TestContent.Rng())));
        w.Emergency = null; other.Title = Praetoria.Core.Progression.Title.Emperor; w.Era = World.EraImperium;
        Assert.Equal("under a throne there is no Senate to strike down an edict", EdictSystem.AnnulRefusal(w, e.Id, mine.Id));
        Assert.Empty(EdictSystem.Annullable(w).Where(x => EdictSystem.AnnulRefusal(w, x.Id, mine.Id) == null));
        // what may not be annulled: one house's own edict, an edict of the powers, and one that lapses before the Senate could vote
        (tc, w, me, mine) = Start(); Rich(w, me); Assert.True(Do(w, Issue(me, "amnesty")));
        Assert.Equal("it falls on one house's own worlds: that is the house's business, not the Senate's", EdictSystem.AnnulRefusal(w, InForce(w, "amnesty").Id, Other(w, mine).Id));
        Consul(w, mine, me); Powers(w, mine, me); Assert.True(Do(w, Issue(me, "levy_in_mass"))); w.Emergency = null;
        Assert.Equal("a levy in mass is not a thing the Senate may strike down", EdictSystem.AnnulRefusal(w, InForce(w, "levy_in_mass").Id, Other(w, mine).Id));
        w.Edicts.RemoveAll(x => x.DefId == "levy_in_mass"); Assert.True(Do(w, Issue(me, "corn_dole")));
        var dole = InForce(w, "corn_dole");
        Assert.Equal("it is your own edict: take it back instead", EdictSystem.AnnulRefusal(w, dole.Id, mine.Id)); Assert.Null(No(w, new WithdrawEdictCommand(me.Id, dole.Id)));
        w.Turn = dole.Until - 1;
        Assert.Equal($"it lapses at the turn of year {dole.Until}, before the Senate could vote on it", EdictSystem.AnnulRefusal(w, dole.Id, Other(w, mine).Id));
        // an annulment moved before the powers began and carried while they run strikes nothing down, and the chronicle says why
        (tc, w, me, mine) = Start(); other = Other(w, mine); consul = Economy.HeadOf(w, other)!; Rich(w, consul); Rich(w, me); Consul(w, other, consul); w.Motions.Clear();
        Assert.True(Do(w, Issue(consul, "close_lanes"))); e = InForce(w, "close_lanes"); other.Legitimacy = 50;
        Assert.True(Do(w, new ProposeMotionCommand(me.Id, EdictSystem.AnnulKind, e.Id, tc.Senate)));
        var m = SenateSystem.Docket(w).Single(); foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        Powers(w, other, consul); w.Turn++; tc.Senate.Tick(w);
        Assert.True(m.Resolved && m.Passed); Assert.Contains(w.Edicts, x => x.Id == e.Id); Assert.Equal(50, other.Legitimacy); Assert.Equal(0, w.Counter(EdictSystem.AnnulledKey));
        var why = w.History.Last(h => h.Text.Contains("stands for nothing")).Text;
        Assert.Contains("while the emergency powers run the Senate does not sit in judgement on an edict", why); Assert.True(Plain(why));
    }

    [Fact]
    public void The_player_is_told_of_a_Reach_wide_edict_and_of_one_that_names_the_house()
    {
        var (tc, w, me, mine) = Start(); var other = Other(w, mine); var head = Economy.HeadOf(w, other)!; Rich(w, head); int before = w.NewsSeq;
        Assert.True(Do(w, Issue(head, "amnesty")));   // its own worlds: the chronicle has it, the player is not told
        Assert.DoesNotContain(w.News, n => n.Seq > before);
        Consul(w, other, head); Assert.True(Do(w, Issue(head, "close_lanes")));
        var told = w.News.Last(); Assert.Equal("laws", told.Target); Assert.Contains("proclaims the lanes closed to raiders for the whole Reach", told.Text);
        Powers(w, other, head); w.PoolsFor(head.Id).Influence = 9; w.Edicts.RemoveAll(e => e.IssuerId == head.Id);
        Assert.True(Do(w, Issue(head, "proscription", mine.Id)));
        Assert.Equal("warn", w.News.Last().Tone); Assert.Contains("has named your house in proscriptions", w.News.Last().Text);
    }

    [Fact]
    public void Edicts_in_force_survive_a_save()
    {
        var (tc, w, me, mine) = Start(); Rich(w, me); Consul(w, mine, me); Short(w);
        Assert.True(Do(w, Issue(me, "forced_loan"))); Assert.True(Do(w, Issue(me, "price_edict")));
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.Edicts.Count, back.Edicts.Count);
        foreach (var (a, b) in w.Edicts.Zip(back.Edicts))
        {
            Assert.Equal(a.Id, b.Id); Assert.Equal(a.DefId, b.DefId); Assert.Equal(a.IssuerId, b.IssuerId); Assert.Equal(a.Domain, b.Domain); Assert.Equal(a.TargetId, b.TargetId);
            Assert.Equal(a.Until, b.Until); Assert.Equal(a.Outlives, b.Outlives); Assert.Equal(a.Effects, b.Effects); Assert.Equal(a.Opinions, b.Opinions); Assert.Equal(a.Position, b.Position);
        }
        Assert.Equal(PolicySystem.Modifier(w, Content.Policies, mine.Id, "credits_per_world"), PolicySystem.Modifier(back, Content.Policies, mine.Id, "credits_per_world"));
        Assert.Equal(MarketSystem.BuyPrice(w, Content.Trade, mine.Id, "helium3"), MarketSystem.BuyPrice(back, Content.Trade, mine.Id, "helium3"), 9);
        Assert.Equal(w.Counter(EdictSystem.IssuedKey), back.Counter(EdictSystem.IssuedKey));
    }

    [Fact]
    public void Other_houses_and_magistrates_issue_edicts_over_some_years_from_more_than_one_position()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 1); var tc = new TurnController(w, Content);
        for (int y = 0; y < 20; y++) { foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); } tc.EndTurn(); }
        int others = w.Counter(EdictSystem.OthersKey);
        Assert.True(others >= 10, $"other houses and magistrates issued {others} edicts in twenty years");
        Assert.True(EdictSystem.Issuers.Count(k => w.Counter(EdictSystem.ByKey(k)) > 0) >= 3, "from three positions at least");
        Assert.True(Content.Edicts.Defs.Count(d => w.Counter(EdictSystem.OfKey(d.Id)) > 0) >= 5, "of five kinds at least");
        Assert.Contains(w.News, n => n.Target == "laws" && n.Text.Contains("for the whole Reach"));
        Assert.Contains(w.History, h => h.Text.Contains(", proclaims "));
        // and it replays the same
        var w2 = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 1); var tc2 = new TurnController(w2, Content);
        for (int y = 0; y < 20; y++) { foreach (var i in tc2.BeginTurn()) { var c = tc2.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc2.Resolve(i, c.Choice.Id); } tc2.EndTurn(); }
        Assert.Equal(w.Counter(EdictSystem.IssuedKey), w2.Counter(EdictSystem.IssuedKey)); Assert.Equal(w.Edicts.Select(e => e.Id + e.DefId), w2.Edicts.Select(e => e.Id + e.DefId));
    }

    [Fact]
    public void Every_reason_another_house_issues_an_edict_for_can_be_met()
    {
        // for each edict, a world in which its reason holds for someone in its position: so that none is only ever the player's
        foreach (var d in Cat.Defs)
        {
            var (tc, w, me, mine) = Start(); var other = Other(w, mine); var head = Economy.HeadOf(w, other)!; Rich(w, head);
            Character who = head;
            if (d.Issuer is EdictSystem.Praetor or EdictSystem.Aedile) { var m = w.Magistrates.FirstOrDefault(x => x.OfficeId == d.Issuer && x.HouseId != mine.Id); if (m != null) who = w.Char(m.CharacterId)!; else Seat(w, head, d.Issuer); Rich(w, who); }
            if (d.Issuer == EdictSystem.State) Consul(w, other, head);
            if (d.Issuer == EdictSystem.Powers) { Consul(w, other, head); Powers(w, other, head); }
            var house = w.House(who.HouseId)!;
            switch (d.When)
            {
                case "unrest": case "unrest_coin": foreach (var h in Worlds(w, house)) h.Unrest = 30; house.Treasury.Credits = 50; break;
                case "unrest_rich": foreach (var h in Worlds(w, house)) h.Unrest = 30; house.Treasury.Credits = 50; break;
                case "reach_unrest": foreach (var h in Reach(w)) h.Unrest = 30; break;
                case "war": w.Wars.Add(new War { HouseA = house.Id, HouseB = mine.Id, Since = w.Turn }); house.Treasury.Manpower = 0; break;
                case "red": while (Worlds(w, house).Count < 3) w.Holdings.Values.First(x => x.IsPlanet && x.OwnerId != house.Id && x.OwnerId != mine.Id && w.OfTheReach(x.OwnerId)).OwnerId = house.Id; w.HoldingsChanged(); house.Treasury.Credits = 2; break;
                case "pinched": foreach (var h in Worlds(w, house).Skip(1)) h.OwnerId = mine.Id; w.HoldingsChanged(); house.Treasury.Credits = 0; break;
                case "danger": w.Nations[0].AtWar = true; break;
                case "debts": house.Debt = 20; house.LenderId = mine.Id; house.LoanInstalment = 5; break;
                case "dear": case "dear_some": Short(w); break;
                case "lawless": foreach (var r in w.Galaxy!.ReachRegions) RegionSystem.StateOf(w, r.Id).Lawlessness = 60; break;
                case "peace": w.WorldCounters[LadderSystem.TrustKey] = 30; house.Treasury.Credits = 50; break;
                case "upright": who.NatureTraits.Add("Just"); break;
                case "exposed": w.OfficeRecord.Add(new OfficeDeed { Id = "deed_reason", CharacterId = who.Id, HouseId = house.Id, RungId = LadderSystem.Quaestor, What = LadderSystem.TookAccounts }); break;
                case "enemy": w.Relationship(Economy.HeadOf(w, mine)!.Id, who.Id).Disposition = -60; break;
                case "struck_at": w.WorldCounters[CaesarSystem.CensuredKey(house.Id)] = w.Turn; break;
                case "grave": w.Rising = new ThroneRising(); break;
            }
            Assert.True(EdictSystem.Reason(w, Content, d, who, out var target) != null, $"{d.Id}: the reason '{d.When}' never holds");
            if (d.Target == "house") Assert.Equal(mine.Id, target);
            Rich(w, who); if (d.When is "red" or "pinched") house.Treasury.Credits = d.When == "red" ? 2 : 0;
            var cmd = new IssueEdictCommand(who.Id, d.Id, Content, target);
            Assert.True(Do(w, cmd), $"{d.Id}: {No(w, new IssueEdictCommand(who.Id, d.Id, Content, target))}");
        }
    }

    [Fact]
    public void A_relative_of_the_player_issues_edicts_of_their_own_and_pays_for_each_out_of_their_own_purse()
    {
        // a relative who holds the consulship is a person with a will of their own: among those who issue edicts, as the others are
        var (tc, w, me, mine) = Start(); var kin = Kin(w, mine, me); Rich(w, kin); Rich(w, me); Consul(w, mine, kin);
        Assert.Contains(EdictSystem.Holders(w), x => x.who.Id == kin.Id && x.kinds.Contains(EdictSystem.State));
        // and pays for a corn dole out of their own purse: the house's treasury is the head's
        int coin = mine.Treasury.Credits, purse = kin.Purse, before = w.NewsSeq;
        Assert.True(Do(w, Issue(kin, "corn_dole"))); var dole = InForce(w, "corn_dole");
        Assert.Equal(coin, mine.Treasury.Credits); Assert.Equal(purse - 10, kin.Purse);
        // the player is told, and told whose purse it came out of
        var told = w.News.Last(n => n.Seq > before);
        Assert.Equal("laws", told.Target); Assert.Contains($"{kin.Name} of your house, consul, proclaims a corn dole for the whole Reach", told.Text);
        Assert.Contains("and pays the 1,000 credits out of their own purse", told.Text); Assert.True(Plain(told.Text));
        // a relative short of coin is refused for want of their own, however full the house's treasury
        mine.Treasury.Credits = 300; kin.Purse = 1; Assert.Equal("needs 800 credits of your own, and you have 100", No(w, Issue(kin, "close_lanes")));
        // and only that relative may take the edict back: the Senate's refusal does not send the player to a button there is not
        Assert.Equal($"it is your own house's edict, and only {kin.Name} may take it back", EdictSystem.AnnulRefusal(w, dole.Id, mine.Id));
        Assert.Equal("only the one who issued it may take it back", No(w, new WithdrawEdictCommand(me.Id, dole.Id)));
    }

    [Fact]
    public void An_edict_is_read_on_as_many_paydays_of_the_court_as_it_has_years()
    {
        var (tc, w, me, mine) = Start(); Rich(w, me); mine.Court.Clear();
        mine.Court.Add(new Retainer { Id = "r_test", Role = CourtSystem.Positions[0], Name = "Test", Wage = 30, Loyalty = 10, Since = w.Turn });
        Assert.True(Do(w, Issue(me, "sumptuary"))); var e = InForce(w, "sumptuary"); Assert.Equal(w.Turn + 3, e.Until);
        var moved = new List<int>();
        while (w.Turn <= e.Until) { int was = mine.Court.Single(r => r.Id == "r_test").Loyalty; tc.EndTurn(); moved.Add(mine.Court.Single(r => r.Id == "r_test").Loyalty - was); tc.BeginTurn(); }
        // three years, three paydays at two less loyalty; in the year's turn when its years are run, it has lapsed before the court is paid
        Assert.Equal(new[] { CourtSystem.PaidLoyalty - 2, CourtSystem.PaidLoyalty - 2, CourtSystem.PaidLoyalty - 2, CourtSystem.PaidLoyalty }, moved);
        Assert.DoesNotContain(w.Edicts, x => x.Id == e.Id);
    }

    [Fact]
    public void A_Reach_wide_edict_falls_only_on_the_houses_of_the_Reach()
    {
        var (tc, w, me, mine) = Start(); var eco = tc.Economy!; Rich(w, me); Consul(w, mine, me);
        var gone = Other(w, mine); var stays = Other(w, mine, 1); gone.PolityId = "gone";   // it has broken away
        int Unrest(Holding h) => eco.UnrestWhy(w, h).Sum(x => x.by);
        int Manpower(House h) => eco.Compute(w, h.Id, tc.Fleets).Net.Manpower;
        var goneWorld = Worlds(w, gone)[0]; var staysWorld = Worlds(w, stays)[0];
        int goneU = Unrest(goneWorld), staysU = Unrest(staysWorld), goneMen = Manpower(gone), goneHas = gone.Treasury.Manpower;
        // a requisition: the house that has broken away sends no man at once, and none of its worlds feels it while it lasts
        Assert.True(Do(w, Issue(me, "requisition")));
        Assert.Equal(goneHas, gone.Treasury.Manpower); Assert.Equal(staysU + 1, Unrest(staysWorld));
        Assert.Equal(goneU, Unrest(goneWorld)); Assert.Equal(goneMen, Manpower(gone));
    }
}
