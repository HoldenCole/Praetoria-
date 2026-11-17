using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Blood between houses (docs/FEUD.md; docs/GAPS.md R4): a killing binds the dead person's house, the debt is the house's and
/// outlives the people, and it ends by blood, a duel, a price, or being let lie. Other houses end theirs by their head's nature.</summary>
public class FeudTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private sealed class Fixed : IRng { private readonly double _d; public Fixed(double d) => _d = d; public ulong State => 0; public double NextDouble() => _d; public int NextInt(int lo, int hi) => hi <= lo ? lo : lo + (int)(_d * (hi - lo)); }

    private static (TurnController tc, World w, Character me, House mine, House theirs, Character head) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn(); var me = w.Protagonist!;
        var theirs = w.Houses.Values.Where(h => h.Id != me.HouseId && h.Id != PirateSystem.HouseId && w.LivingCharacters().Count(c => c.HouseId == h.Id && c.Age >= 18) >= 2).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var head = Economy.HeadOf(w, theirs)!; head.NatureTraits.Clear(); head.CareerRank = Math.Max(head.CareerRank, 5); theirs.Treasury.Credits = 200;   // the head stays the head whoever is added
        return (tc, w, me, w.House(me.HouseId)!, theirs, head);
    }
    private static bool Do(World w, ICommand cmd, double dice = 0.0) => new CommandExecutor().TryExecute(cmd, new CommandContext(w, new Fixed(dice)));
    private static Character Add(World w, House h, string id, int age = 30, string track = "", int rank = 0, params string[] natures)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = age, Sex = "male", CareerTrack = track, CareerRank = rank, Health = 90 };
        foreach (var n in natures) c.NatureTraits.Add(n); w.Characters[id] = c; h.Members.Add(id); return c;
    }
    private static bool Eligible(World w, string eventId)
    {
        var me = w.Protagonist!; w.House(me.HouseId)!.Treasury.Credits = Math.Max(100, w.House(me.HouseId)!.Treasury.Credits); w.PoolsFor(me.Id).Influence = 9;
        return new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == eventId);
    }

    [Fact]
    public void AKilling_BindsTheDeadPersonsHouse_AndTheDebtIsTheHousesNotThePersons()
    {
        var (_, w, me, mine, theirs, head) = Start(); var victim = Add(w, theirs, "v", rank: 2); var watcher = Add(w, theirs, "watcher");
        int before = w.GetRelationship(watcher.Id, me.Id)?.Disposition ?? 0;
        var f = FeudSystem.Blood(w, victim, mine.Id, me.Id, "hand")!; DynastySystem.Die(w, victim.Id);
        Assert.True(f.Open); Assert.Equal(theirs.Id, f.WrongedId); Assert.Equal(mine.Id, f.OwingId); Assert.Equal(me.Id, f.BlamedId);
        Assert.Equal(FeudSystem.PriceBase + 2 * FeudSystem.PricePerRank, f.Price);
        Assert.Equal(before + FeudSystem.KinRegard, w.GetRelationship(watcher.Id, me.Id)!.Disposition);
        Assert.True(w.HasFlag(FeudSystem.WeOweFlag)); Assert.False(w.HasFlag(FeudSystem.OwedToUsFlag)); Assert.Contains(FeudSystem.BlamedChar, me.Flags);
        Assert.True(FeudSystem.Between(w, mine.Id, theirs.Id)); Assert.True(FeudSystem.Between(w, theirs.Id, mine.Id));
        Assert.Equal(-FeudSystem.LeanBy, FeudSystem.Lean(w, theirs.Id, mine.Id)); Assert.Equal(0, FeudSystem.Lean(w, theirs.Id, theirs.Id));
        Assert.True(Eligible(w, "blood_on_our_hands"));
        // it is held against the one who did it while they live, and against the head of their house after
        Assert.Equal(me.Id, FeudSystem.Blamed(w, f)!.Id);
        var other = new BloodFeud { Id = "x", WrongedId = mine.Id, OwingId = theirs.Id, BlamedId = "nobody_alive", VictimName = "someone", Price = 20 }; w.Feuds.Add(other);
        Assert.Equal(head.Id, FeudSystem.Blamed(w, other)!.Id);
        // no house owes itself blood, and the killing of the head costs more
        w.Feuds.Remove(other);
        Assert.Null(FeudSystem.Blood(w, watcher, theirs.Id, head.Id, "hand"));
        var theirHead = FeudSystem.Blood(w, head, mine.Id, me.Id, "hand")!; Assert.Equal(FeudSystem.PriceBase + head.CareerRank * FeudSystem.PricePerRank + FeudSystem.PriceForAHead, theirHead.Price);
    }

    [Fact]
    public void BloodForBlood_TheRightBloodEndsIt_AndTheWrongBloodTurnsItRound()
    {
        var (_, w, me, mine, theirs, head) = Start(); var killer = Add(w, theirs, "killer"); var bystander = Add(w, theirs, "bystander"); var dead = Add(w, mine, "mine_dead");
        var f = FeudSystem.Blood(w, dead, theirs.Id, killer.Id, "hand")!; DynastySystem.Die(w, dead.Id);
        Assert.True(w.HasFlag(FeudSystem.OwedToUsFlag)); Assert.True(Eligible(w, "blood_between_houses"));
        // the wrong blood: the debt is answered, and they hold one against us in their turn
        var back = FeudSystem.Blood(w, bystander, mine.Id, me.Id, "knife"); DynastySystem.Die(w, bystander.Id);
        Assert.Equal("blood", f.Ended); Assert.NotNull(back); Assert.Equal(theirs.Id, back!.WrongedId); Assert.Equal(1, w.Counter(FeudSystem.WrongBloodKey));
        Assert.False(w.HasFlag(FeudSystem.OwedToUsFlag)); Assert.True(w.HasFlag(FeudSystem.WeOweFlag));
        // the right blood: they kill the one they hold to blame, and it is over
        var kin = Add(w, mine, "blamed_kin"); back.BlamedId = kin.Id;
        Assert.Null(FeudSystem.Blood(w, kin, theirs.Id, head.Id, "knife")); DynastySystem.Die(w, kin.Id);
        Assert.Equal("blood", back.Ended); Assert.Empty(FeudSystem.Open(w)); Assert.False(w.HasFlag(FeudSystem.WeOweFlag)); Assert.Equal(2, w.Counter(FeudSystem.BloodKey));
    }

    [Fact]
    public void AKnife_IsLaidAtTheRightDoor_WhenTheHouseAlreadyHasCause()
    {
        var (_, w, me, mine, theirs, head) = Start(); var target = Add(w, theirs, "target"); me.Skills["intrigue"] = 9; w.PoolsFor(me.Id).Agents = 9;
        w.Relationship(head.Id, me.Id).Disposition = FeudSystem.SuspectAt;
        Assert.True(FeudSystem.Traced(w, target, me));
        Assert.True(Do(w, new AssassinateCommand(me.Id, target.Id), dice: 0.0)); Assert.False(target.Alive);
        var f = Assert.Single(FeudSystem.Open(w)); Assert.Equal("knife", f.How); Assert.Equal(me.Id, f.BlamedId); Assert.Equal(1, w.Counter(FeudSystem.BegunKey));
        // a house with no cause to suspect finds the trail one time in three
        w.Feuds.Clear(); w.Relationship(head.Id, me.Id).Disposition = 0; int traced = 0;
        for (int i = 0; i < 60; i++) { var t = new Character { Id = "t" + i, HouseId = theirs.Id }; if (FeudSystem.Traced(w, t, me)) traced++; }
        Assert.InRange(traced, 8, 32);
    }

    [Fact]
    public void ADeathInBattle_IsWar_UnlessItWasTheHeadOrTheHeadsNearBlood()
    {
        var (_, w, me, mine, theirs, head) = Start(); var cousin = Add(w, theirs, "cousin"); var child = Add(w, theirs, "child"); child.FatherId = head.Id; child.CareerRank = 0;
        Assert.True(FeudSystem.BattleDeathBinds(w, head)); Assert.True(FeudSystem.BattleDeathBinds(w, child)); Assert.False(FeudSystem.BattleDeathBinds(w, cousin));
        var fleets = new FleetSystem(Content.Fleets, Content.Holdings); child.CareerTrack = "military";
        fleets.AdmiralOutcomes(w, new Fixed(0.0), new List<Character> { me }, new List<Character> { child }, mine.Id);
        Assert.False(child.Alive); var f = Assert.Single(FeudSystem.Open(w)); Assert.Equal("battle", f.How); Assert.Equal(mine.Id, f.OwingId); Assert.Equal(me.Id, f.BlamedId);
    }

    [Fact]
    public void ADuel_EndsIt_WhoeverFalls_AndADeathInItIsNobodysToAvenge()
    {
        var (_, w, me, mine, theirs, head) = Start(); var dead = Add(w, mine, "mine_dead"); var killer = Add(w, theirs, "killer", track: "military", rank: 1);
        var f = FeudSystem.Blood(w, dead, theirs.Id, killer.Id, "hand")!; DynastySystem.Die(w, dead.Id);
        me.Skills["tactics"] = 8; me.Health = 90; me.Age = 30;
        Assert.True(FeudSystem.DuelOdds(me, killer) > 50); Assert.InRange(FeudSystem.DuelOdds(killer, me), FeudSystem.DuelFloor, 49);
        Assert.Null(FeudSystem.ActRefusal(w, me, f, FeudCommand.Challenge));
        Assert.True(Do(w, new FeudCommand(me.Id, f.Id, FeudCommand.Challenge), dice: 0.0));   // the dice at their lowest: the challenger wins and the loser dies
        Assert.Equal("duel", f.Ended); Assert.False(killer.Alive); Assert.Empty(FeudSystem.Open(w)); Assert.Equal(1, w.Counter(FeudSystem.DuelDeathsKey));
        Assert.Contains(FeudSystem.WonDuelChar, me.Flags); Assert.True(w.HasFlag(FeudSystem.DuelFoughtFlag)); Assert.True(Eligible(w, "after_the_field"));
        // a kinsman sent as champion, and a loser who yields
        var dead2 = Add(w, mine, "mine_dead2"); var killer2 = Add(w, theirs, "killer2", track: "military", rank: 3); killer2.Skills["tactics"] = 9;
        var f2 = FeudSystem.Blood(w, dead2, theirs.Id, killer2.Id, "hand")!; DynastySystem.Die(w, dead2.Id);
        var clerk = Add(w, mine, "clerk"); var soldier = Add(w, mine, "soldier", track: "military", rank: 1);
        Assert.Contains("will not fight", FeudSystem.ActRefusal(w, me, f2, FeudCommand.Challenge, clerk));
        Assert.True(Do(w, new FeudCommand(me.Id, f2.Id, FeudCommand.Challenge, soldier.Id), dice: 0.99));   // the dice at their highest: the champion loses and lives
        Assert.Equal("duel", f2.Ended); Assert.True(soldier.Alive && killer2.Alive); Assert.Contains(LifeSystem.WoundedChar, soldier.Flags); Assert.Empty(FeudSystem.Open(w));
    }

    [Fact]
    public void TheOneCalledOut_MayNotCome_AndItCostsTheirHouse()
    {
        var (_, w, me, mine, theirs, head) = Start(); var dead = Add(w, mine, "mine_dead"); var clerk = Add(w, theirs, "clerk", track: "law", rank: 2);
        var f = FeudSystem.Blood(w, dead, theirs.Id, clerk.Id, "hand")!; int standing = theirs.Legitimacy;
        Assert.False(FeudSystem.TakesTheField(clerk));
        Assert.True(Do(w, new FeudCommand(me.Id, f.Id, FeudCommand.Challenge)));
        Assert.True(f.Open && f.Refused); Assert.Equal(standing - FeudSystem.FieldRefusedStanding, theirs.Legitimacy); Assert.Contains(FeudSystem.RefusedFieldChar, clerk.Flags);
        Assert.Equal("one thing a year is done about a death", FeudSystem.ActRefusal(w, me, f, FeudCommand.LetLie));
        Assert.Equal("they have refused once already", new Func<string?>(() => { f.ActedTurn = 0; return FeudSystem.ActRefusal(w, me, f, FeudCommand.Demand); })());
    }

    [Fact]
    public void APrice_MayBeAskedOrOffered_AndTakingItCosts()
    {
        var (_, w, me, mine, theirs, head) = Start(); var dead = Add(w, mine, "mine_dead"); var son = Add(w, mine, "son"); son.FatherId = me.Id; dead.FatherId = me.Id; w.Relationship(son.Id, me.Id).Disposition = 20;
        var f = FeudSystem.Blood(w, dead, theirs.Id, head.Id, "hand")!; int mineBefore = mine.Treasury.Credits, theirsBefore = theirs.Treasury.Credits, standing = theirs.Legitimacy;
        Assert.True(FeudSystem.WouldPay(w, f));
        Assert.True(Do(w, new FeudCommand(me.Id, f.Id, FeudCommand.Demand)));
        Assert.Equal("price", f.Ended); Assert.Equal(mineBefore + f.Price, mine.Treasury.Credits); Assert.Equal(theirsBefore - f.Price, theirs.Treasury.Credits);
        Assert.Equal(standing - FeudSystem.PriceAdmits, theirs.Legitimacy); Assert.Equal(20 + FeudSystem.LetLieKin / 2, w.GetRelationship(son.Id, me.Id)!.Disposition);
        // a house that cannot pay refuses, and has refused
        var dead2 = Add(w, mine, "mine_dead2"); var f2 = FeudSystem.Blood(w, dead2, theirs.Id, head.Id, "hand")!; theirs.Treasury.Credits = 3;
        Assert.True(Do(w, new FeudCommand(me.Id, f2.Id, FeudCommand.Demand))); Assert.True(f2.Open && f2.Refused);
        // owing: the offer is taken by most heads and never by a vengeful one, and it is made once
        var victim = Add(w, theirs, "their_dead"); var owe = FeudSystem.Blood(w, victim, mine.Id, me.Id, "hand")!; mine.Treasury.Credits = 500;
        head.NatureTraits.Add("Vengeful"); Assert.False(FeudSystem.TakesAPrice(w, owe));
        Assert.True(Do(w, new FeudCommand(me.Id, owe.Id, FeudCommand.Offer))); Assert.True(owe.Open && owe.Offered); Assert.Equal(500, mine.Treasury.Credits);
        Assert.Equal("you have offered once already", new Func<string?>(() => { owe.ActedTurn = 0; return FeudSystem.ActRefusal(w, me, owe, FeudCommand.Offer); })());
        head.NatureTraits.Clear(); owe.Offered = false;
        Assert.True(Do(w, new FeudCommand(me.Id, owe.Id, FeudCommand.Offer))); Assert.Equal("price", owe.Ended); Assert.Equal(500 - owe.Price, mine.Treasury.Credits);
    }

    [Fact]
    public void TheDeadUnanswered_CostTheHouse_UntilItIsLetLie()
    {
        var (_, w, me, mine, theirs, head) = Start(); var dead = Add(w, mine, "mine_dead"); var f = FeudSystem.Blood(w, dead, theirs.Id, head.Id, "hand")!; f.Offered = true;
        mine.Legitimacy = 50;
        for (int y = 0; y < FeudSystem.GraceYears; y++) { w.Turn++; FeudSystem.Tick(w); }
        Assert.Equal(50, mine.Legitimacy); Assert.False(w.HasFlag(FeudSystem.UnansweredFlag));
        w.Turn++; FeudSystem.Tick(w); Assert.Equal(49, mine.Legitimacy); Assert.True(w.HasFlag(FeudSystem.UnansweredFlag));
        for (int y = 0; y < 10 && f.Open; y++) { w.Turn++; FeudSystem.Tick(w); }
        Assert.Equal("let_lie", f.Ended); Assert.Equal(50 - FeudSystem.ShameCap, mine.Legitimacy); Assert.Equal(FeudSystem.LetLieYears, f.Unanswered); Assert.False(w.HasFlag(FeudSystem.OwedToUsFlag));
        // said aloud, it costs at once what is left to lose, the near kin take it hard, and the other house had not expected it
        var dead2 = Add(w, mine, "mine_dead2"); dead2.FatherId = me.Id; var brother = Add(w, mine, "brother"); brother.FatherId = me.Id; w.Relationship(brother.Id, me.Id).Disposition = 30;
        var f2 = FeudSystem.Blood(w, dead2, theirs.Id, head.Id, "hand")!; int regard = w.GetRelationship(head.Id, me.Id)?.Disposition ?? 0; mine.Legitimacy = 50;
        Assert.True(Do(w, new FeudCommand(me.Id, f2.Id, FeudCommand.LetLie)));
        Assert.Equal("let_lie", f2.Ended); Assert.Equal(50 - FeudSystem.LetLieStanding, mine.Legitimacy); Assert.Equal(30 + FeudSystem.LetLieKin, w.GetRelationship(brother.Id, me.Id)!.Disposition);
        Assert.Equal(regard + FeudSystem.LetLieThanks, w.GetRelationship(head.Id, me.Id)!.Disposition); Assert.Equal(2, w.Counter(FeudSystem.LetLieKey));
    }

    [Fact]
    public void OtherHouses_AnswerByTheirHeadsNature()
    {
        // two other houses: the honourable send someone to the field, the cruel a knife, the rest ask the price
        string How(string nature, bool richOwer = true, bool fighters = true)
        {
            var (_, w, me, mine, theirs, head) = Start(); var third = w.Houses.Values.Where(h => h.Id != mine.Id && h.Id != theirs.Id && h.Id != PirateSystem.HouseId && w.LivingCharacters().Any(c => c.HouseId == h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).First();
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == theirs.Id).ToList()) { c.CareerTrack = "law"; c.NatureTraits.Clear(); }
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == third.Id).ToList()) { c.NatureTraits.Clear(); }
            if (nature.Length > 0) head.NatureTraits.Add(nature);
            if (fighters) Add(w, theirs, "champion", track: "military", rank: 3).Skills["tactics"] = 9; else head.Age = 70;   // too old to go, and nobody to send
            var blamed = Add(w, third, "blamed", track: "military", rank: 1); var victim = Add(w, theirs, "victim"); third.Treasury.Credits = richOwer ? 500 : 0;
            var f = FeudSystem.Blood(w, victim, third.Id, blamed.Id, "hand")!; DynastySystem.Die(w, victim.Id);
            for (int y = 0; y < 30 && f.Open; y++) { w.Turn++; FeudSystem.Tick(w); }
            return f.Ended + (f.Refused ? "+refused" : "");
        }
        Assert.Equal("duel", How("Honorable"));
        Assert.Equal("price", How(""));
        Assert.Equal("price", How("Honorable", fighters: false));   // an honourable head with nobody to send asks the price instead
        Assert.Contains(How("Cruel"), new[] { "blood", "let_lie+refused", "let_lie" });
        Assert.Contains(How("", richOwer: false), new[] { "duel+refused", "blood+refused", "let_lie+refused" });   // refused the price: then the field or a knife
    }

    [Fact]
    public void WhenTheyComeForThePlayer_ThePlayerIsAsked_AndNeverKnifed()
    {
        // an honourable house calls the player out; the answer is given through the event's choice or the panel
        var (_, w, me, mine, theirs, head) = Start(); foreach (var c in w.LivingCharacters().Where(c => c.HouseId == theirs.Id).ToList()) c.NatureTraits.Clear();
        head.NatureTraits.Add("Honorable"); var champion = Add(w, theirs, "champion", track: "military", rank: 3); var victim = Add(w, theirs, "victim");
        var f = FeudSystem.Blood(w, victim, mine.Id, me.Id, "hand")!; DynastySystem.Die(w, victim.Id);
        for (int y = 0; y < 12 && f.Asked.Length == 0; y++) { w.Turn++; FeudSystem.Tick(w); }
        Assert.Equal("duel", f.Asked); Assert.Equal(champion.Id, f.AskerId); Assert.True(w.HasFlag(FeudSystem.CalledOutFlag)); Assert.True(Eligible(w, "called_out")); Assert.True(me.Alive);
        Assert.Equal("they are waiting on your answer", FeudSystem.ActRefusal(w, me, f, FeudCommand.Offer));
        int standing = mine.Legitimacy; w.WorldFlags.Add(FeudSystem.DeclineFlag); w.Turn++; FeudSystem.Tick(w);
        Assert.True(f.Open && f.Refused); Assert.Equal(standing - FeudSystem.FieldRefusedStanding, mine.Legitimacy); Assert.Contains(FeudSystem.RefusedFieldChar, me.Flags);
        // nobody is called out twice: what comes next is a knife, and never for the player's own person
        Assert.Equal("they would not come the first time, and will not now", new Func<string?>(() => { var mineDead = new BloodFeud { Id = "y", WrongedId = mine.Id, OwingId = theirs.Id, BlamedId = head.Id, FieldRefused = true, Refused = true, VictimName = "x" }; w.Feuds.Add(mineDead); var r = FeudSystem.ActRefusal(w, me, mineDead, FeudCommand.Challenge); w.Feuds.Remove(mineDead); return r; })());
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == mine.Id && c.Id != me.Id).ToList()) c.Alive = false;
        for (int y = 0; y < 5; y++) { w.Turn++; FeudSystem.Tick(w); Assert.Equal("", f.Asked); }
        Assert.True(me.Alive);

        // called out and going: the player takes the field from the panel, and it ends there whoever falls
        var (_, wd, med, mined, theirsd, headd) = Start(); foreach (var c in wd.LivingCharacters().Where(c => c.HouseId == theirsd.Id).ToList()) c.NatureTraits.Clear();
        headd.NatureTraits.Add("Proud"); Add(wd, theirsd, "champion", track: "military", rank: 3); var vd = Add(wd, theirsd, "victim");
        var fd = FeudSystem.Blood(wd, vd, mined.Id, med.Id, "hand")!; DynastySystem.Die(wd, vd.Id);
        for (int y = 0; y < 12 && fd.Asked.Length == 0; y++) { wd.Turn++; FeudSystem.Tick(wd); }
        Assert.Equal("duel", fd.Asked); med.Health = 90; med.Skills["tactics"] = 9;
        Assert.True(Do(wd, new FeudCommand(med.Id, fd.Id, FeudCommand.Fight), dice: 0.99)); Assert.Equal("duel", fd.Ended); Assert.True(med.Alive);

        // a house that asks the price: paid through the event, and unanswered for two years it is a refusal
        var (_, w2, me2, mine2, theirs2, head2) = Start(); foreach (var c in w2.LivingCharacters().Where(c => c.HouseId == theirs2.Id).ToList()) { c.NatureTraits.Clear(); c.CareerTrack = "law"; }
        var v2 = Add(w2, theirs2, "victim"); var f2 = FeudSystem.Blood(w2, v2, mine2.Id, me2.Id, "hand")!; DynastySystem.Die(w2, v2.Id); mine2.Treasury.Credits = 300;
        for (int y = 0; y < 12 && f2.Asked.Length == 0; y++) { w2.Turn++; FeudSystem.Tick(w2); }
        Assert.Equal("price", f2.Asked); Assert.True(Eligible(w2, "the_price_asked"));
        w2.WorldFlags.Add(FeudSystem.PayFlag); w2.Turn++; FeudSystem.Tick(w2); Assert.Equal("price", f2.Ended); Assert.Equal(300 - f2.Price, mine2.Treasury.Credits);

        // a cruel house sends a knife, and it is never for the player's own person
        var (_, w3, me3, mine3, theirs3, head3) = Start(); foreach (var c in w3.LivingCharacters().Where(c => c.HouseId == theirs3.Id).ToList()) c.NatureTraits.Clear();
        head3.NatureTraits.Add("Cruel"); head3.Skills["intrigue"] = 9; foreach (var c in w3.LivingCharacters().Where(c => c.HouseId == mine3.Id && c.Id != me3.Id).ToList()) c.Alive = false;
        var v3 = Add(w3, theirs3, "victim"); var f3 = FeudSystem.Blood(w3, v3, mine3.Id, me3.Id, "hand")!; DynastySystem.Die(w3, v3.Id);
        for (int y = 0; y < 7; y++) { w3.Turn++; FeudSystem.Tick(w3); }
        Assert.True(me3.Alive); Assert.True(f3.Open);
        var kin = Add(w3, mine3, "kin");
        for (int y = 0; y < 12 && kin.Alive && f3.Open; y++) { w3.Turn++; FeudSystem.Tick(w3); }
        Assert.True(me3.Alive); Assert.True(!kin.Alive || !f3.Open);
        if (!kin.Alive) { Assert.Equal("blood", f3.Ended); Assert.Contains(FeudSystem.Open(w3), o => o.WrongedId == mine3.Id && o.VictimId == kin.Id); }   // the wrong blood: now it is ours to answer
    }

    [Fact]
    public void AHouseThatOwesThePlayer_MayOfferThePrice_AndBloodStopsAMatch()
    {
        var (_, w, me, mine, theirs, head) = Start(); head.NatureTraits.Add("Just"); var dead = Add(w, mine, "mine_dead");
        var f = FeudSystem.Blood(w, dead, theirs.Id, head.Id, "hand")!; DynastySystem.Die(w, dead.Id);
        for (int y = 0; y < 7 && f.Asked.Length == 0; y++) { w.Turn++; FeudSystem.Tick(w); }
        Assert.Equal("offer", f.Asked); Assert.True(f.Offered); Assert.True(Eligible(w, "the_price_offered"));
        Assert.Null(FeudSystem.ActRefusal(w, me, f, FeudCommand.Take)); Assert.Equal("nobody has asked you for a price", FeudSystem.ActRefusal(w, me, f, FeudCommand.Pay));
        Assert.True(Do(w, new FeudCommand(me.Id, f.Id, FeudCommand.Spurn))); Assert.True(f.Open); Assert.Equal("", f.Asked);
        // and while it stands nobody of the one house courts anybody of the other
        var suitor = Add(w, mine, "suitor", age: 24); var bride = Add(w, theirs, "bride", age: 22); bride.Sex = "female";
        Assert.Equal("there is blood between your houses", CourtshipSystem.Bar(w, suitor, bride));
        // a house with nobody left owes nothing and is owed nothing
        foreach (var c in w.LivingCharacters().Where(c => c.HouseId == theirs.Id).ToList()) c.Alive = false;
        w.Turn++; FeudSystem.Tick(w); Assert.Equal("extinct", f.Ended);
    }

    [Fact]
    public void AnEventsKilling_NamesWhoseHandItWas()
    {
        var kill = Content.Events.SelectMany(e => e.Choices.Select(c => (e, c))).SelectMany(x => x.c.Effects.OfType<KillEffect>().Select(k => (x.e.Id, k))).ToList();
        Assert.True(kill.Count >= 4); Assert.All(kill, k => Assert.NotEqual("", k.k.By));
        Assert.Contains(kill, k => k.Id == "the_marked_man" && k.k.By == "self");   // a cadet killed at the Institute is remembered by their house
    }
}
