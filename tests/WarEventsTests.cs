using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The war reports (docs/events/war.md): each is eligible when its moment is set, every choice applies, the threads carry
/// from one report to the next, and the marks the fleets raise for them (who was in the battle, the prisoner, the fleet bearing down,
/// the peace, the sacker) are set and struck out when they should be.</summary>
public class WarEventsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private sealed class Fixed : IRng { private readonly double _d; public Fixed(double d) => _d = d; public ulong State => 0; public double NextDouble() => _d; public int NextInt(int lo, int hi) => hi <= lo ? lo : lo + (int)(_d * (hi - lo)); }
    private const string Mine = "voss", Theirs = "harrow";

    /// <summary>The Fallen House in year ten, with a head who can open every gated choice: money, influence, the traits and skills the
    /// gates ask for, and a fleet of eight hulls.</summary>
    private static World Base()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_fallen_house"), 7); w.Turn = 10;
        var me = w.Protagonist!; var h = w.House(me.HouseId)!;
        h.Treasury.Credits = 100; h.Treasury.Set(Resource.Influence, 9); h.Treasury.Set(Resource.Materials, 20);
        me.NatureTraits.Remove("Proud"); me.NatureTraits.Add("Ambitious"); me.NatureTraits.Add("Arrogant"); me.AptitudeTraits.Add("Schemer");
        me.Skills["oratory"] = 3; me.Skills["intrigue"] = 3; me.CareerTrack = "military"; me.CareerRank = 3;
        w.Fleets["fleet_voss_home"].Strength = 8;
        return w;
    }
    private static Character Add(World w, string houseId, string id, int age = 30, string track = "military", int rank = 3)
    {
        var c = new Character { Id = id, Name = "Test " + id, HouseId = houseId, Age = age, Sex = "female", CareerTrack = track, CareerRank = rank, Health = 90, Alive = true };
        w.Characters[id] = c; w.House(houseId)!.Members.Add(id); return c;
    }
    private static void War(World w) => w.Wars.Add(new War { HouseA = Mine, HouseB = Theirs, Since = 1 });

    private static (EventDef def, Binding binding) Find(World w, string id)
    {
        var all = new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w);
        var hit = all.Where(e => e.def.Id == id).ToList();
        Assert.True(hit.Count == 1, $"{id} not eligible; eligible were: {string.Join(", ", all.Select(e => e.def.Id))}");
        return hit[0];
    }
    private static bool IsEligible(World w, string id) => new EventEngine(Content, WorldBuilder.RngFor(w)).GatherEligible(w).Any(e => e.def.Id == id);

    /// <summary>For each choice of the report: a fresh world, the report eligible in it, the choice open, and taken without error with
    /// a line written to the chronicle.</summary>
    private static void EveryChoice(string id, Func<World> build, Action<World, Binding>? tune = null, Action<World, Binding, string>? after = null)
    {
        var def = Content.Events.Single(e => e.Id == id);
        Assert.True(def.Choices.Count is >= 2 and <= 4, $"{id} has {def.Choices.Count} choices");
        foreach (var c in def.Choices)
        {
            var w = build(); var (d, b) = Find(w, id); tune?.Invoke(w, b);
            var engine = new EventEngine(Content, WorldBuilder.RngFor(w)); var fired = new FiredEvent(d, b);
            Assert.True(engine.IsChoiceAvailable(fired, w, c.Id), $"{id}/{c.Id} is not open");
            int before = w.History.Count;
            engine.Resolve(fired, w, c.Id);
            Assert.True(w.History.Count > before, $"{id}/{c.Id} wrote nothing to the chronicle");
            after?.Invoke(w, b, c.Id);
        }
    }
    private static World Officer(string moment, params string[] flags)
    {
        var w = Base(); var o = Add(w, Mine, "voss_officer"); foreach (var f in flags) o.Flags.Add(f); w.WorldFlags.Add(moment); return w;
    }

    // ---------------- the reports ----------------

    [Fact]
    public void OutOfTheLine_NamesTheOfficerWhoBrokeIt_AndEachAnswerLeavesThemOwedOrWronged()
    {
        Assert.False(IsEligible(Officer("battle_lost", FleetSystem.InTheBattleChar, FleetSystem.InALostBattleChar), "out_of_the_line"));
        Assert.False(IsEligible(Officer("battle_won"), "out_of_the_line"));                                  // nobody of the house was in the line
        Assert.False(IsEligible(Officer("battle_won", FleetSystem.InTheBattleChar, FleetSystem.InALostBattleChar), "out_of_the_line"));   // they stood in the year's other battle, the one lost
        EveryChoice("out_of_the_line", () => Officer("battle_won", FleetSystem.InTheBattleChar, FleetSystem.InAWonBattleChar), after: (w, b, choice) =>
        {
            var o = w.Char(b.Resolve("officer"))!; Assert.Equal("voss_officer", o.Id);
            Assert.Contains(choice is "name_them" or "promote_them" ? "officer_owed" : "officer_wronged", o.Flags);
            if (choice == "promote_them") Assert.Equal(4, o.CareerRank);
        });
    }

    [Fact]
    public void TheOfficerWhoRan_IsALostBattle_AndAskingWhyEndsTheThread()
    {
        Assert.False(IsEligible(Officer("battle_lost", FleetSystem.InTheBattleChar, FleetSystem.InAWonBattleChar), "ran_from_the_battle"));   // they stood in the battle won
        EveryChoice("ran_from_the_battle", () => Officer("battle_lost", FleetSystem.InTheBattleChar, FleetSystem.InALostBattleChar), after: (w, b, choice) =>
        {
            var o = w.Char(b.Resolve("officer"))!;
            if (choice == "ask_them_why") { Assert.DoesNotContain("officer_owed", o.Flags); Assert.DoesNotContain("officer_wronged", o.Flags); }
            else Assert.Contains(choice == "strip_them" ? "officer_wronged" : "officer_owed", o.Flags);
        });
    }

    [Fact]
    public void AtThePeace_TheOfficerOwedIsHonoured_AndTheOfficerWrongedWritesTheirOwnDispatch()
    {
        Assert.False(IsEligible(Officer("battle_won", "officer_owed"), "a_laurel_at_the_peace"));             // not before the peace
        EveryChoice("a_laurel_at_the_peace", () => Officer(FleetSystem.OurPeaceFlag, "officer_owed"), after: (w, b, choice) =>
        {
            var o = w.Char(b.Resolve("officer"))!; Assert.DoesNotContain("officer_owed", o.Flags);
            if (choice == "name_them_to_the_senate") Assert.Equal(BondType.Sworn, w.GetRelationship(o.Id, w.ProtagonistId)!.Bond);
        });
        EveryChoice("the_other_dispatch", () => Officer(FleetSystem.OurPeaceFlag, "officer_wronged"), after: (w, b, _) => Assert.DoesNotContain("officer_wronged", w.Char(b.Resolve("officer"))!.Flags));
        var warm = Officer(FleetSystem.OurPeaceFlag, "officer_wronged"); warm.Relationship("voss_officer", warm.ProtagonistId).Disposition = 40;
        Assert.False(IsEligible(warm, "the_other_dispatch"));                                               // a grudge they have let go writes nothing
        var mended = Officer(FleetSystem.OurPeaceFlag, "officer_wronged"); mended.Relationship("voss_officer", mended.ProtagonistId).Disposition = 15;
        Assert.True(IsEligible(mended, "the_other_dispatch"));                                              // a little mended since, they still publish
        // a war with a nation ends without a peace of the house's own: its officers are answered at that end too
        Assert.True(IsEligible(Officer(NationSystem.WarOverFlag, "officer_owed"), "a_laurel_at_the_peace"));
        Assert.True(IsEligible(Officer(NationSystem.WarOverFlag, "officer_wronged"), "the_other_dispatch"));
    }

    [Fact]
    public void AYoungOneOfTheHouse_AsksForAPlaceInTheFleet_AndIsBloodedInTheNextBattle()
    {
        World Asks() { var w = Base(); w.WorldFlags.Add("at_war_any"); return w; }
        EveryChoice("a_place_in_the_fleet", Asks, after: (w, b, choice) =>
        {
            var kin = w.Char(b.Resolve("kin"))!; Assert.InRange(kin.Age, 16, 24); Assert.Equal(Mine, kin.HouseId);
            Assert.Contains(choice is "send_them" or "take_them_on_your_staff" ? "at_the_front" : "kept_from_war", kin.Flags);
        });
        var peace = Base(); Assert.False(IsEligible(peace, "a_place_in_the_fleet"));                       // not in peace time

        World Blooded() { var w = Base(); Add(w, Mine, "voss_cadet", age: 18, rank: 1).Flags.Add("at_the_front"); w.WorldFlags.Add("battle_lost"); return w; }
        var old = Blooded(); old.Char("voss_cadet")!.Age = 34; Assert.False(IsEligible(old, "blooded_in_battle"));   // not "their first battle" at thirty-four
        EveryChoice("blooded_in_battle", Blooded, after: (w, b, choice) =>
        {
            var kin = w.Char(b.Resolve("kin"))!; Assert.DoesNotContain("at_the_front", kin.Flags);
            if (choice == "a_step_up") Assert.Equal(2, kin.CareerRank);
            if (choice == "bring_them_home") Assert.Contains("kept_from_war", kin.Flags);
        });
    }

    [Fact]
    public void TheYoungOne_ComesHomeAtThePeace_AndEveryAnswerEndsTheThread()
    {
        World Home() { var w = Base(); Add(w, Mine, "voss_cadet", age: 19, rank: 1).Flags.Add("at_the_front"); w.WorldFlags.Add(FleetSystem.OurPeaceFlag); return w; }
        EveryChoice("kin_home_from_the_war", Home, after: (w, b, choice) =>
        {
            var kin = w.Char(b.Resolve("kin"))!; Assert.Equal("voss_cadet", kin.Id); Assert.DoesNotContain("at_the_front", kin.Flags);
            if (choice == "a_place_in_the_service") Assert.Equal(2, kin.CareerRank);
        });
        var war = Home(); war.WorldFlags.Remove(FleetSystem.OurPeaceFlag); Assert.False(IsEligible(war, "kin_home_from_the_war"));   // not while the war lasts
        var nation = Home(); nation.WorldFlags.Remove(FleetSystem.OurPeaceFlag); nation.WorldFlags.Add(NationSystem.WarOverFlag); Assert.True(IsEligible(nation, "kin_home_from_the_war"));
    }

    [Fact]
    public void DesertersFromTheFleet_AreAnsweredOneOfFourWays()
    {
        World Deserted() { var w = Base(); w.WorldFlags.Add("fleet_deserts"); me(w).NatureTraits.Add("Ruthless"); return w; }
        static Character me(World w) => w.Protagonist!;
        EveryChoice("deserters_from_the_fleet", Deserted);
        Assert.False(IsEligible(Base(), "deserters_from_the_fleet"));
    }

    [Fact]
    public void AChoicesChips_SayWhatItDoesToTheEstatesTheMagistratesAndWhoIsKilled()
    {
        var w = Base(); w.WorldFlags.Add(FleetSystem.OurPeaceFlag); w.House(Mine)!.Treasury.Credits = 6;
        var (d, b) = Find(w, "who_pays_for_the_war"); var ctx = new EvalContext(w, b, WorldBuilder.RngFor(w), Content);
        string tax = Praetoria.Core.Presentation.ChoiceStakes.Of(d.Choices.Single(c => c.Id == "a_war_tax"), ctx, w.Protagonist!);
        Assert.Contains("the Commons −4", tax); Assert.Contains("+600 credits", tax);
        string levy = Praetoria.Core.Presentation.ChoiceStakes.Of(d.Choices.Single(c => c.Id == "a_levy_on_cargoes"), ctx, w.Protagonist!);
        Assert.Contains("the Merchants −4", levy);
        var trust = new Choice { Id = "t", Effects = new IEffect[] { new AdjustCounterEffect(LadderSystem.TrustKey, -2), new AdjustCounterEffect("goodwill", 3) } };
        string said = Praetoria.Core.Presentation.ChoiceStakes.Of(trust, ctx, w.Protagonist!);
        Assert.Contains("trust in the magistrates −2", said); Assert.DoesNotContain("goodwill", said);   // a counter the player does not watch says nothing
        var kill = new Choice { Id = "k", Effects = new IEffect[] { new KillEffect("rival", "self") } };
        var kctx = new EvalContext(w, new Binding(w.ProtagonistId).With("rival", "harrow_drusus"), WorldBuilder.RngFor(w), Content);
        Assert.Contains(w.Char("harrow_drusus")!.Name + " is killed", Praetoria.Core.Presentation.ChoiceStakes.Of(kill, kctx, w.Protagonist!));
    }

    [Fact]
    public void APrisonerTakenInBattle_IsRansomedExchangedParoledOrParaded()
    {
        World Taken() { var w = Base(); War(w); Add(w, Theirs, "harrow_captain", rank: 2).Flags.Add(FleetSystem.PrisonerChar); w.WorldFlags.Add("battle_won"); return w; }
        EveryChoice("taken_in_battle", Taken, after: (w, b, choice) =>
        {
            var p = w.Char(b.Resolve("prisoner"))!;
            Assert.Equal(choice == "parade_them", FleetSystem.Captive(p));
            Assert.Equal(choice == "parole_them", p.Flags.Contains(FleetSystem.ParoleChar));
            if (choice == "parade_them") Assert.Contains(FleetSystem.HeldChar, p.Flags);
            if (choice == "ransom_them") Assert.Equal(14 - 4, w.House(Theirs)!.Treasury.Credits);
        });
        var held = Taken(); held.Char("harrow_captain")!.Flags.Add(FleetSystem.HeldChar);
        Assert.False(IsEligible(held, "taken_in_battle"));                                                 // the house has answered for them already
    }

    [Fact]
    public void AParoleIsBroken_WhenTheirHouseGivesThemAFleetInTheSameWar()
    {
        World Broken() { var w = Base(); War(w); var p = Add(w, Theirs, "harrow_paroled", rank: 3); p.Flags.Add(FleetSystem.ParoleChar); w.Fleets["fleet_harrow_main"].AdmiralId = p.Id; FleetSystem.RefreshAdmiralFlags(w); return w; }
        EveryChoice("the_parole_broken", Broken, after: (w, b, _) => Assert.DoesNotContain(FleetSystem.ParoleChar, w.Char(b.Resolve("parolee"))!.Flags));
        var kept = Broken(); kept.Fleets["fleet_harrow_main"].AdmiralId = "harrow_drusus"; FleetSystem.RefreshAdmiralFlags(kept);
        Assert.False(IsEligible(kept, "the_parole_broken"));                                               // a parole kept is no report
    }

    [Fact]
    public void AParleyBeforeTheGuns_CanEndTheWar_WhenTheWarsScoreAllowsAWhitePeace()
    {
        World Parley() { var w = Base(); War(w); w.Char("harrow_drusus")!.Flags.Add(FleetSystem.BearsDownChar); w.WorldFlags.Add(FleetSystem.BearsDownFlag); return w; }
        EveryChoice("a_parley_before_the_guns", Parley, after: (w, _, choice) =>
        {
            Assert.Equal(choice != "offer_a_white_peace", w.AtWar(Mine, Theirs));
            Assert.Equal(choice == "count_their_ships", w.Char("harrow_drusus")!.Flags.Contains(FleetSystem.ShipsCountedChar));
            if (choice == "offer_a_white_peace") Assert.True(w.HasFlag(FleetSystem.OurPeaceFlag));      // and the peace is news, for the reports of it
        });
        var strong = Parley(); strong.Fleets["fleet_harrow_main"].Strength = 60;                             // they smell victory: no white peace on offer
        var (d, b) = Find(strong, "a_parley_before_the_guns");
        Assert.False(new EventEngine(Content, WorldBuilder.RngFor(strong)).IsChoiceAvailable(new FiredEvent(d, b), strong, "offer_a_white_peace"));
    }

    [Fact]
    public void PlunderOnTheExchange_CanBuyAStakeInAnotherHousesRaids_ThatComesBackAtTheNextPeaceBeyondTheRim()
    {
        World Spoils() { var w = Base(); w.Char("harrow_drusus")!.Flags.Add(NationSystem.SackerChar); w.WorldFlags.Add("world_sacked"); return w; }
        EveryChoice("plunder_on_the_exchange", Spoils, after: (w, b, choice) =>
        {
            var s = w.Char(b.Resolve("sacker"))!; Assert.DoesNotContain(NationSystem.SackerChar, s.Flags);
            Assert.Equal(choice == "put_money_into_the_next_raid", s.Flags.Contains("war_partner"));
        });
        World Scandal() { var w = Base(); w.Char("harrow_drusus")!.Flags.Add("war_partner"); w.WorldFlags.Add(NationSystem.WarOverFlag); return w; }
        EveryChoice("a_scandal_in_the_accounts", Scandal, after: (w, b, _) => Assert.DoesNotContain("war_partner", w.Char(b.Resolve("partner"))!.Flags));
        var atWar = Spoils(); War(atWar); Assert.False(IsEligible(atWar, "plunder_on_the_exchange"));         // not from a house you are at war with
    }

    [Fact]
    public void ThePeace_PaysTheFleetOff_AndAsksWhoPaysWhenTheTreasuryIsLow()
    {
        World Peace() { var w = Base(); w.WorldFlags.Add(FleetSystem.OurPeaceFlag); return w; }
        EveryChoice("the_fleet_paid_off", Peace);
        var stillAtWar = Peace(); stillAtWar.WorldFlags.Add("at_war_any"); Assert.False(IsEligible(stillAtWar, "the_fleet_paid_off"));
        World Poor() { var w = Peace(); w.House(Mine)!.Treasury.Credits = 6; return w; }
        EveryChoice("who_pays_for_the_war", Poor, after: (w, _, choice) =>
        {
            if (choice == "sell_the_war_stores") { Assert.Equal(16, w.House(Mine)!.Treasury.Materials); Assert.True(w.House(Mine)!.Treasury.Credits > 6); }
        });
        Assert.False(IsEligible(Peace(), "who_pays_for_the_war"));                                           // a full treasury asks nobody
    }

    [Fact]
    public void EveryWarReport_HasAnUngatedChoice_AndALineForTheChronicle()
    {
        var mine = Content.Events.Where(e => new[] { "out_of_the_line", "ran_from_the_battle", "a_laurel_at_the_peace", "the_other_dispatch", "a_place_in_the_fleet", "blooded_in_battle",
            "kin_home_from_the_war", "taken_in_battle", "the_parole_broken", "deserters_from_the_fleet", "a_parley_before_the_guns", "plunder_on_the_exchange", "a_scandal_in_the_accounts",
            "the_fleet_paid_off", "who_pays_for_the_war" }.Contains(e.Id)).ToList();
        Assert.Equal(15, mine.Count);
        foreach (var e in mine)
        {
            Assert.Contains(e.Choices, c => c.Requirements.Count == 0);
            Assert.All(e.Choices, c => Assert.Contains(c.Effects, x => x is LogEffect));
            var text = Content.Text(e.Id)!; Assert.DoesNotContain("—", text.Body + text.Title + string.Concat(text.Choices.Values));
        }
    }

    // ---------------- the marks (Core) ----------------

    private static (World w, FleetSystem fs) BattleAtGreywater()
    {
        var w = Base(); var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction);
        var officer = Add(w, Mine, "voss_officer"); w.Fleets["fleet_voss_home"].AdmiralId = officer.Id; w.Fleets["fleet_voss_home"].Strength = 12;
        var captain = Add(w, Theirs, "harrow_captain", rank: 2); var theirs = w.Fleets["fleet_harrow_main"];
        theirs.AdmiralId = "harrow_drusus"; theirs.CaptainId = captain.Id; theirs.Strength = 6; theirs.SystemId = "greywater";
        fs.DeclareWar(w, Mine, Theirs); FleetSystem.RefreshAdmiralFlags(w);
        return (w, fs);
    }

    [Fact]
    public void ABattleTheHouseWins_MarksItsOfficers_TakesACaptainOfTheBeatenLine_WhoGoesHomeAtThePeace()
    {
        var (w, fs) = BattleAtGreywater();
        fs.ResolveBattles(w, new Fixed(0.99));                                   // the holder wins the day and nobody dies
        var r = Assert.Single(fs.LastBattles); Assert.Equal(Mine, r.Winner);
        var officer = w.Char("voss_officer")!; var captain = w.Char("harrow_captain")!; var head = w.Char("harrow_drusus")!;
        Assert.Contains(FleetSystem.InTheBattleChar, officer.Flags); Assert.DoesNotContain(FleetSystem.InTheBattleChar, w.Protagonist!.Flags);
        Assert.Contains(FleetSystem.InAWonBattleChar, officer.Flags); Assert.DoesNotContain(FleetSystem.InALostBattleChar, officer.Flags);
        Assert.DoesNotContain(FleetSystem.InTheBattleChar, captain.Flags);       // only the house's own are marked
        Assert.True(FleetSystem.Captive(captain)); Assert.False(FleetSystem.Captive(head));   // a captain, and never the head of the house
        Assert.DoesNotContain(w.Fleets.Values, f => f.CaptainId == captain.Id);
        var any = w.Fleets.Values.First(f => f.OwnerId == Theirs);
        Assert.Contains("prisoner", FleetSystem.CaptainRefusal(w, any, captain)!);
        Assert.Contains("prisoner", ServiceSystem.FreeRefusal(w, captain)!);
        Assert.True(IsEligible(w, "taken_in_battle")); Assert.True(IsEligible(w, "out_of_the_line"));

        fs.Tick(w);                                                              // a year on: who was in the line is struck out; the prisoner is held while the war lasts
        Assert.DoesNotContain(FleetSystem.InTheBattleChar, officer.Flags); Assert.True(FleetSystem.Captive(captain));

        fs.MakePeace(w, Mine, Theirs);
        Assert.True(w.HasFlag(FleetSystem.OurPeaceFlag)); Assert.Equal(w.Turn, w.Counter("mark:" + FleetSystem.OurPeaceFlag));
        fs.Tick(w);                                                              // the war is over: the prisoner goes home, and says so
        Assert.False(FleetSystem.Captive(captain)); Assert.Contains(w.History, h => h.Text.Contains("goes home to") && h.Text.Contains(captain.Name));
        Assert.True(w.HasFlag(FleetSystem.OurPeaceFlag));                        // the peace is news for the year it is made and two more briefings
        w.Turn += 2; fs.Tick(w); Assert.True(w.HasFlag(FleetSystem.OurPeaceFlag));   // a peace made after a briefing is still read at the next one
        w.Turn += 1; fs.Tick(w);
        Assert.False(w.HasFlag(FleetSystem.OurPeaceFlag));
    }

    [Fact]
    public void ABattleTheHouseLoses_MarksItsOfficers_AndTakesNobody()
    {
        var (w, fs) = BattleAtGreywater(); w.Fleets["fleet_harrow_main"].Strength = 14;
        fs.ResolveBattles(w, new Fixed(0.3));                                    // the attacker carries the day, and the beaten admiral lives (the death roll is under 0.12)
        Assert.Equal(Theirs, Assert.Single(fs.LastBattles).Winner);
        Assert.Contains(FleetSystem.InTheBattleChar, w.Char("voss_officer")!.Flags);
        Assert.Contains(FleetSystem.InALostBattleChar, w.Char("voss_officer")!.Flags); Assert.DoesNotContain(FleetSystem.InAWonBattleChar, w.Char("voss_officer")!.Flags);
        Assert.DoesNotContain(w.Characters.Values, c => FleetSystem.Captive(c));
        Assert.Equal("harrow_captain", w.Fleets.Values.First(f => f.OwnerId == Theirs).CaptainId);
    }

    [Fact]
    public void WithNoCaptainToTake_AnAdmiralWhoseFleetWentDownIsTaken_AndOneWhoseFleetSurvivedIsNot()
    {
        var (w, fs) = BattleAtGreywater(); var adm = Add(w, Theirs, "harrow_admiral", rank: 5); var theirs = w.Fleets["fleet_harrow_main"];
        theirs.AdmiralId = adm.Id; theirs.CaptainId = ""; theirs.Strength = 2; theirs.Stance = "press";   // two hulls pressing home: both are lost
        FleetSystem.RefreshAdmiralFlags(w);
        fs.ResolveBattles(w, new Fixed(0.99));
        Assert.False(w.Fleets.ContainsKey("fleet_harrow_main")); Assert.True(adm.Alive); Assert.True(FleetSystem.Captive(adm));
        Assert.DoesNotContain(FleetSystem.AdmiralFlag, adm.Flags);                // a prisoner commands nothing, so no report reads them as an admiral
        var (w2, fs2) = BattleAtGreywater(); var adm2 = Add(w2, Theirs, "harrow_admiral", rank: 5); var t2 = w2.Fleets["fleet_harrow_main"];
        t2.AdmiralId = adm2.Id; t2.CaptainId = ""; t2.Strength = 8; FleetSystem.RefreshAdmiralFlags(w2);
        fs2.ResolveBattles(w2, new Fixed(0.99));
        Assert.True(w2.Fleets.ContainsKey("fleet_harrow_main")); Assert.False(FleetSystem.Captive(adm2));   // the fleet got away, and its admiral with it
    }

    [Fact]
    public void ABattleLost_MayWoundARelativeAtTheFront_AndABattleWonDoesNot()
    {
        (World w, FleetSystem fs, List<Character> kin) Front(int theirStrength)
        {
            var (w, fs) = BattleAtGreywater(); w.Fleets["fleet_harrow_main"].Strength = theirStrength; var kin = new List<Character>();
            for (int i = 0; i < 12; i++) { var k = Add(w, Mine, "voss_front_" + i, age: 19, rank: 1); k.Flags.Add(FleetSystem.AtTheFrontChar); kin.Add(k); }
            return (w, fs, kin);
        }
        var (lost, lfs, lk) = Front(14); lfs.ResolveBattles(lost, new Fixed(0.3)); Assert.Equal(Theirs, Assert.Single(lfs.LastBattles).Winner);
        int hurt = lk.Count(k => k.Flags.Contains(LifeSystem.WoundedChar));
        Assert.InRange(hurt, 1, 11);                                             // one in three, by hash: some, and not all
        var (won, wfs, wk) = Front(6); wfs.ResolveBattles(won, new Fixed(0.99)); Assert.Equal(Mine, Assert.Single(wfs.LastBattles).Winner);
        Assert.DoesNotContain(wk, k => k.Flags.Contains(LifeSystem.WoundedChar));
    }

    [Fact]
    public void TheWarsPeople_AreStruckOut_OnceTheHouseIsAtPeaceAndThePeaceIsOldNews()
    {
        var (w, fs) = BattleAtGreywater(); var o = w.Char("voss_officer")!; o.Flags.Add("officer_owed");
        var cadet = Add(w, Mine, "voss_cadet", age: 19, rank: 1); cadet.Flags.Add(FleetSystem.AtTheFrontChar);
        fs.Tick(w); Assert.Contains("officer_owed", o.Flags); Assert.Contains(FleetSystem.AtTheFrontChar, cadet.Flags);   // the war goes on
        fs.MakePeace(w, Mine, Theirs);
        for (int y = 0; y < 3; y++) { fs.Tick(w); Assert.Contains("officer_owed", o.Flags); Assert.Contains(FleetSystem.AtTheFrontChar, cadet.Flags); w.Turn++; }   // the peace's reports have their years
        fs.Tick(w); Assert.False(w.HasFlag(FleetSystem.OurPeaceFlag));
        Assert.DoesNotContain("officer_owed", o.Flags); Assert.DoesNotContain(FleetSystem.AtTheFrontChar, cadet.Flags);
    }

    [Fact]
    public void ShipsCountedAtAParley_FightTheWorse_TheYearTheyArrive()
    {
        var (w, fs) = BattleAtGreywater(); var drusus = w.Char("harrow_drusus")!; var theirs = w.Fleets["fleet_harrow_main"];
        drusus.Flags.Add(FleetSystem.ShipsCountedChar);
        fs.Tick(w); Assert.Contains(FleetSystem.ShipsKnownChar, drusus.Flags); Assert.DoesNotContain(FleetSystem.ShipsCountedChar, drusus.Flags);
        double known = fs.DepthFactor(w, theirs); drusus.Flags.Remove(FleetSystem.ShipsKnownChar); double plain = fs.DepthFactor(w, theirs); drusus.Flags.Add(FleetSystem.ShipsKnownChar);
        Assert.Equal(plain * FleetSystem.CountedPower, known, 6);
        w.Turn++; fs.Tick(w); Assert.DoesNotContain(FleetSystem.ShipsKnownChar, drusus.Flags);   // the year after, their order of battle has changed
    }

    [Fact]
    public void AParole_IsStruckOutWithTheWar()
    {
        var (w, fs) = BattleAtGreywater(); var p = Add(w, Theirs, "harrow_paroled"); p.Flags.Add(FleetSystem.ParoleChar);
        fs.Tick(w); Assert.Contains(FleetSystem.ParoleChar, p.Flags);
        fs.MakePeace(w, Mine, Theirs); fs.Tick(w); Assert.DoesNotContain(FleetSystem.ParoleChar, p.Flags);
    }

    [Fact]
    public void AnEnemyFleetAYearOut_BearsDownOnTheHouse_ForTheYear()
    {
        var w = Base(); var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); fs.DeclareWar(w, Mine, Theirs);
        var theirs = w.Fleets["fleet_harrow_main"]; theirs.AdmiralId = "harrow_drusus"; theirs.DestinationId = "greywater"; theirs.Eta = 2; theirs.Voyage = 2;
        fs.Tick(w);
        Assert.True(w.HasFlag(FleetSystem.BearsDownFlag)); Assert.Equal(w.Turn, w.Counter("mark:" + FleetSystem.BearsDownFlag));
        Assert.Contains(FleetSystem.BearsDownChar, w.Char("harrow_drusus")!.Flags);
        Assert.True(IsEligible(w, "a_parley_before_the_guns"));
        w.Turn++; fs.Tick(w);                                                    // it has arrived: no longer coming, and the battle is the fleets' business
        Assert.False(w.HasFlag(FleetSystem.BearsDownFlag)); Assert.DoesNotContain(FleetSystem.BearsDownChar, w.Char("harrow_drusus")!.Flags);
        // a fleet of a house at peace with yours bears down on nobody
        var calm = Base(); var cfs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction);
        var c = calm.Fleets["fleet_harrow_main"]; c.AdmiralId = "harrow_drusus"; c.DestinationId = "greywater"; c.Eta = 2; c.Voyage = 2;
        cfs.Tick(calm); Assert.False(calm.HasFlag(FleetSystem.BearsDownFlag));
    }

    [Fact]
    public void AnotherHouseThatSacksAWorldBeyondTheRim_HasItsHeadMarked_ForTheYear()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations); w.Turn = 3;
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }
        var mine = w.House(w.Protagonist!.HouseId)!; var other = w.Houses.Values.Where(h => w.OfTheReach(h.Id) && h.Id != mine.Id && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var head = Economy.HeadOf(w, other)!; var camp = w.Holding("long_grass_camp")!;
        NationSystem.DeclareWar(w, Content.Nations, "vashti", mine.Id);
        NationSystem.Sack(w, camp, other.Id);
        Assert.Contains(NationSystem.SackerChar, head.Flags); Assert.True(w.HasFlag("world_sacked"));
        NationSystem.Sack(w, camp, mine.Id);                                     // the player's own sack is The Sack's, and marks nobody
        Assert.DoesNotContain(NationSystem.SackerChar, w.Protagonist!.Flags);
        w.Turn++; ns.Tick(w);
        Assert.False(w.HasFlag("world_sacked")); Assert.DoesNotContain(NationSystem.SackerChar, head.Flags);
    }

    [Fact]
    public void TheEndOfAWarWithANation_IsMarkedForTheYear_HoweverItEnds()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7);
        var fs = new FleetSystem(Content.Fleets, Content.Holdings, Content.ShipClasses, Content.Friction); var ns = new NationSystem(Content.Nations, fs);
        NationSystem.Seed(w, Content.Nations); w.Turn = 3;
        foreach (var x in w.Nations) { x.Disposition = 50; if (x.Ruler != null) { x.Ruler.Nature = "mild"; x.Ruler.Age = 30; } }
        var n = w.Nation("vashti")!; NationSystem.DeclareWar(w, Content.Nations, "vashti", w.Protagonist!.HouseId);
        for (int y = 0; y < 40 && n.AtWar; y++) { w.Turn++; ns.Tick(w); Assert.Equal(!n.AtWar, w.HasFlag(NationSystem.WarOverFlag)); }   // a war nobody fights gutters out
        Assert.False(n.AtWar); Assert.True(w.HasFlag(NationSystem.WarOverFlag + ":vashti"));
        w.Turn++; ns.Tick(w); Assert.True(w.HasFlag(NationSystem.WarOverFlag));    // still news at the next briefing: a war that ends after one is read at the next
        w.Turn++; ns.Tick(w); Assert.False(w.HasFlag(NationSystem.WarOverFlag));
    }

    [Fact]
    public void APrisonerOfWar_StepsUpToNoPostOfTheirHouse()
    {
        string Steps(bool prisoner)
        {
            var w = Base(); w.FamilyAutonomy = 1; var f = w.Fleets["fleet_voss_home"]; f.AdmiralId = "";
            var best = Add(w, Mine, "voss_best", rank: 5); best.Skills["tactics"] = 9; best.Skills["gunnery"] = 9; best.Skills["leadership"] = 9;
            if (prisoner) best.Flags.Add(FleetSystem.PrisonerChar);
            var fam = new FamilySystem(); fam.Tick(w); w.Turn++; fam.Tick(w); return f.AdmiralId;
        }
        Assert.NotEqual("voss_best", Steps(prisoner: true));
        Assert.Equal("voss_best", Steps(prisoner: false));                       // free, the best of the house takes up the empty command
    }

    [Fact]
    public void APrisonerOfWar_LeadsNoLevy()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7UL); _ = new TurnController(w, Content);
        var house = w.House(w.Protagonist!.HouseId)!; w.Wars.Clear(); foreach (var n in w.Nations) n.AtWar = false;
        var seat = w.HoldingsOf(house.Id).First(h => h.IsPlanet); var sender = w.House(ProvinceSystem.HolderOf(seat, 1))!; string id = ProvinceSystem.LevyPrefix + ProvinceSystem.Target(seat.Id, 1);
        var foe = w.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && h.Id != sender.Id && h.Id != ProvinceSystem.HolderOf(seat, 2) && ProvinceSystem.LeanUnder(w, sender.Id, h.Id) == 0);
        var best = Add(w, sender.Id, "levyman"); best.Skills["tactics"] = 20; best.Skills["leadership"] = 20; best.Flags.Add(FleetSystem.PrisonerChar);   // the one it would send, held by an enemy
        w.Wars.Add(new War { HouseA = house.Id, HouseB = foe.Id, Since = w.Turn }); ProvinceSystem.Tick(w, Content.Holdings);
        Assert.True(w.Fleets.ContainsKey(id)); var sent = w.Fleets.Values.Where(x => x.LevyOf == sender.Id).ToList(); Assert.NotEmpty(sent);
        Assert.All(sent, x => Assert.NotEqual(best.Id, x.AdmiralId));
    }

    [Fact]
    public void AHouse_DoesNotMakeAPrisonerOfWarItsAdmiral()
    {
        var w = Base(); var theirs = w.Fleets["fleet_harrow_main"]; theirs.AdmiralId = "";
        var best = Add(w, Theirs, "harrow_best", rank: 5); best.Skills["tactics"] = 9; best.Skills["gunnery"] = 9; best.Flags.Add(FleetSystem.PrisonerChar);
        new NpcAi().Act(new CommandExecutor(), new CommandContext(w, TestContent.Rng()), null, null, Content.Fleets);
        Assert.NotEqual(best.Id, theirs.AdmiralId);
        best.Flags.Remove(FleetSystem.PrisonerChar); theirs.AdmiralId = "";
        new NpcAi().Act(new CommandExecutor(), new CommandContext(w, TestContent.Rng()), null, null, Content.Fleets);
        Assert.Equal(best.Id, theirs.AdmiralId);                                 // free, the best mind of the house has the fleet
    }

    // ---------------- the threads, end to end ----------------

    [Fact]
    public void TheThreads_CarryFromTheFirstReportToTheSecond()
    {
        void Take(World w, string id, string choice) { var (d, b) = Find(w, id); new EventEngine(Content, WorldBuilder.RngFor(w)).Resolve(new FiredEvent(d, b), w, choice); }
        // the officer: named in the dispatch, honoured at the peace
        var w = Officer("battle_won", FleetSystem.InTheBattleChar, FleetSystem.InAWonBattleChar); Take(w, "out_of_the_line", "name_them");
        w.WorldFlags.Remove("battle_won"); w.WorldFlags.Add(FleetSystem.OurPeaceFlag); Assert.True(IsEligible(w, "a_laurel_at_the_peace")); Assert.False(IsEligible(w, "the_other_dispatch"));
        // ...or robbed of the credit, and writes their own account
        w = Officer("battle_won", FleetSystem.InTheBattleChar, FleetSystem.InAWonBattleChar); Take(w, "out_of_the_line", "take_the_credit");
        w.WorldFlags.Remove("battle_won"); w.WorldFlags.Add(FleetSystem.OurPeaceFlag); Assert.True(IsEligible(w, "the_other_dispatch")); Assert.False(IsEligible(w, "a_laurel_at_the_peace"));
        // the young one: sent, and blooded
        w = Base(); w.WorldFlags.Add("at_war_any"); Take(w, "a_place_in_the_fleet", "send_them");
        w.WorldFlags.Add("battle_won"); Assert.True(IsEligible(w, "blooded_in_battle"));
        w.WorldFlags.Remove("battle_won"); w.WorldFlags.Add(FleetSystem.OurPeaceFlag); Assert.False(IsEligible(w, "kin_home_from_the_war"));   // not while another war lasts
        w.WorldFlags.Remove("at_war_any"); Assert.True(IsEligible(w, "kin_home_from_the_war"));                                        // and home at the peace
        // the prisoner: paroled, and the parole broken when their house gives them a fleet
        w = Base(); War(w); var p = Add(w, Theirs, "harrow_captain", rank: 2); p.Flags.Add(FleetSystem.PrisonerChar); w.WorldFlags.Add("battle_won");
        Take(w, "taken_in_battle", "parole_them"); Assert.False(IsEligible(w, "the_parole_broken"));
        w.Fleets["fleet_harrow_main"].AdmiralId = p.Id; FleetSystem.RefreshAdmiralFlags(w); Assert.True(IsEligible(w, "the_parole_broken"));
        // the spoils: a stake, and the scandal at the peace beyond the rim
        w = Base(); w.Char("harrow_drusus")!.Flags.Add(NationSystem.SackerChar); w.WorldFlags.Add("world_sacked"); Take(w, "plunder_on_the_exchange", "put_money_into_the_next_raid");
        Assert.False(IsEligible(w, "a_scandal_in_the_accounts")); w.WorldFlags.Add(NationSystem.WarOverFlag); Assert.True(IsEligible(w, "a_scandal_in_the_accounts"));
    }
}
