using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A charge against the powerful is a gamble (docs/LADDER.md, "A charge against the powerful"; docs/IDEAS_FROM_IMPERATOR.md, 2):
/// such a charge waits a year whoever brings it and is a cause to cross the line; the accused may stand trial, go into exile, or cross;
/// an acquittal costs the accuser; a conviction of one who commands forces the choice; a threat of a charge is a cause too and costs
/// the house that never makes it good; and the Senate votes an exile home.</summary>
public class ChargeAgainstThePowerfulTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static OfficeCatalog Offices => Content.Offices;

    private static (TurnController tc, World w, Character me, House mine, CommandContext ctx) Start(int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        var me = w.Protagonist!; var mine = w.House(me.HouseId)!; me.Age = Math.Min(me.Age, 45);
        w.Magistrates.Clear(); w.OfficeWork.Clear(); w.OfficeRecord.Clear(); w.Candidacies.Clear(); w.Indictments.Clear(); w.Judgments.Clear(); w.Motions.Clear(); w.Recalls.Clear();
        foreach (var s in w.Service.ToList()) ServiceSystem.End(w, s, "a test");
        mine.Treasury.Credits = 100; mine.Treasury.Influence = 9; w.PoolsFor(me.Id).Influence = 9;
        Calm(w);
        return (tc, w, me, mine, new CommandContext(w, TestContent.Rng()));
    }
    private static List<House> Others(World w, House mine) =>
        w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null && h.Id != RegionSystem.CapitalHolder(w)?.Id && w.Wars.All(x => !x.Involves(h.Id)))
            .OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    private static Character Person(World w, House h, string id, params string[] natures)
    {
        if (h.HeadId.Length == 0 && Economy.HeadOf(w, h) is { } leads) h.HeadId = leads.Id;
        var c = new Character { Id = id, Name = "Test " + id, HouseId = h.Id, Age = 40, Sex = "male", CareerTrack = "law", CareerRank = 2 }; foreach (var n in natures) c.NatureTraits.Add(n);
        w.Characters[id] = c; h.Members.Add(id); return c;
    }
    private static Character Judge(World w, House h, string nature = "Just")
    {
        var j = Person(w, h, "judge_" + h.Id, nature); w.Magistrates.Add(new OfficeHolding { OfficeId = LadderSystem.Praetor, HouseId = h.Id, CharacterId = j.Id, SinceTurn = w.Turn - 3 }); return j;
    }
    private static OfficeDeed Deed(World w, Character who)
    {
        var d = new OfficeDeed { Id = "deed_" + who.Id + "_" + w.OfficeRecord.Count, CharacterId = who.Id, HouseId = who.HouseId, RungId = "quaestor", Turn = w.Turn - 1, What = LadderSystem.TookAccounts }; w.OfficeRecord.Add(d); return d;
    }
    private static void Calm(World w)
    {
        var heads = w.Houses.Values.Select(h => Economy.HeadOf(w, h)).Where(c => c != null).Select(c => c!).ToList();
        foreach (var a in heads) foreach (var b in heads) if (a.Id != b.Id) w.Relationship(a.Id, b.Id).Disposition = 20;
    }
    private static void Hate(World w, House of, House toward) => w.Relationship(Economy.HeadOf(w, of)!.Id, Economy.HeadOf(w, toward)!.Id).Disposition = -60;
    private static Fleet Legion(World w, House h, Character c, int hulls = 10, int devotion = 80)
    {
        var at = w.HoldingsOf(h.Id).FirstOrDefault(x => x.IsPlanet)?.SystemId ?? w.Galaxy!.Systems.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
        foreach (var other in w.Fleets.Values.Where(x => x.AdmiralId == c.Id)) other.AdmiralId = "";
        var f = new Fleet { Id = "legion_" + c.Id, Name = "The Tenth", OwnerId = h.Id, SystemId = at, HomeSystemId = at, Strength = hulls, AdmiralId = c.Id, DevotedTo = c.Id, Devotion = devotion };
        w.Fleets[f.Id] = f; return f;
    }
    /// <summary>Sway enough to be one of the powerful, and to march: valour, the name the Reach gives, and rank in the service.</summary>
    private static void Name(World w, Character c) { w.WorldCounters[FleetSystem.ValourKey(c.Id)] = 5; w.WorldCounters[CaesarSystem.VictoryKey(c.Id)] = 4; c.Flags.Add(FleetSystem.AchillesFlag); c.CareerTrack = "military"; c.CareerRank = Math.Max(c.CareerRank, 3); }
    /// <summary>A year as the turn takes it: the generals, then the accused answer, then the courts sit.</summary>
    private static void Year(TurnController tc, World w) { w.Turn++; tc.Caesar.Tick(w); tc.Charges.Tick(w); LadderSystem.Tick(w, Offices, Content.Policies); }
    private static bool Do(TurnController tc, CommandContext ctx, ICommand c) => tc.Executor.TryExecute(c, ctx);
    private static string? No(CommandContext ctx, ICommand c) => c.GetType().GetMethod("Refusal")!.Invoke(c, new object[] { ctx }) as string;   // every command says why not
    /// <summary>Another house charges someone of the given house: the hater brings it at the year's courts, and it is held.</summary>
    private static Indictment Charged(TurnController tc, World w, House accuser, Character who, Character judge)
    {
        Deed(w, who); Hate(w, accuser, w.House(who.HouseId)!);
        LadderSystem.Tick(w, Offices, Content.Policies);
        return Assert.Single(w.Indictments, c => c.AccusedId == who.Id);
    }

    [Fact]
    public void Who_counts_among_the_powerful()
    {
        var (_, w, me, mine, _) = Start(); var o = Others(w, mine); var them = o[0];
        Assert.True(Praetoria.Core.Progression.Title.RankOf(mine.Title) >= ChargeSystem.PowerfulRank); Assert.True(ChargeSystem.Powerful(w, me));     // the head of a Proconsul's house
        them.Title = Praetoria.Core.Progression.Title.Baron; var head = Economy.HeadOf(w, them)!; Assert.False(ChargeSystem.Powerful(w, head));       // a Prefect's is not
        them.Title = Praetoria.Core.Progression.Title.Count; Assert.True(ChargeSystem.Powerful(w, head)); Assert.Contains("Legate", ChargeSystem.PowerfulWhy(w, head));
        var kin = Person(w, them, "kin"); Assert.False(ChargeSystem.Powerful(w, kin));                                                 // a relative of the same house is not
        Name(w, kin); Legion(w, them, kin); Assert.True(CaesarSystem.Sway(w, kin) >= ChargeSystem.PowerfulSway); Assert.True(ChargeSystem.Powerful(w, kin));   // until they have the sway
        Assert.StartsWith("a person of great sway", ChargeSystem.PowerfulWhy(w, kin));
    }

    [Fact]
    public void A_charge_against_the_powerful_waits_a_year_whoever_brings_it_and_is_a_cause_in_the_year_it_is_heard()
    {
        var (tc, w, _, mine, _) = Start(); var o = Others(w, mine); var accuser = o[0]; var them = o[1]; them.Title = Praetoria.Core.Progression.Title.Count;
        var head = Economy.HeadOf(w, them)!; head.NatureTraits.Clear(); head.NatureTraits.Add("Honorable"); var judge = Judge(w, o[2]);
        Assert.Null(CaesarSystem.Grievance(w, them));
        var c = Charged(tc, w, accuser, head, judge);
        Assert.True(c.Powerful); Assert.Equal("", c.Outcome); Assert.Equal(1, w.Counter(ChargeSystem.BroughtKey));
        Assert.Contains("charged", CaesarSystem.Grievance(w, them));                                                                  // a cause, as a recall is
        Assert.Contains(head.Name, ChargeSystem.Cause(w, them));
        Year(tc, w);                                                                                                                   // the upright answer it; the upright praetor convicts
        Assert.Equal(LadderSystem.Proved, c.Outcome); Assert.Equal(ChargeSystem.Trial, c.Answer); Assert.Equal(1, w.Counter(ChargeSystem.ConvictedKey));
        Assert.NotNull(CaesarSystem.Grievance(w, them));                                                                              // still, in the year it is heard
        w.Turn += 2; Assert.Null(ChargeSystem.Cause(w, them));
        // one of the same house who is not powerful is heard at once, as before
        var (tc2, w2, _, mine2, _) = Start(); var o2 = Others(w2, mine2); o2[1].Title = Praetoria.Core.Progression.Title.Baron; Judge(w2, o2[2]);
        var minor = Person(w2, o2[1], "minor"); Deed(w2, minor); Hate(w2, o2[0], o2[1]);
        LadderSystem.Tick(w2, Offices, Content.Policies); Assert.Empty(w2.Indictments); Assert.Equal(1, w2.Counter("prosecutions"));
    }

    [Fact]
    public void Your_charge_against_one_of_the_powerful_waits_and_an_acquittal_costs_you_and_makes_them_known()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var them = o[0]; them.Title = Praetoria.Core.Progression.Title.Count;
        var head = Economy.HeadOf(w, them)!; head.NatureTraits.Clear(); head.NatureTraits.Add("Loyal"); var d = Deed(w, head);
        var judge = Judge(w, o[1], "Loyal"); w.Relationship(Economy.HeadOf(w, o[1])!.Id, head.Id).Disposition = 50;                  // the praetor's house is a friend of theirs
        int legit = mine.Legitimacy;
        Assert.True(Do(tc, ctx, new ProsecutionCommand(me.Id, ProsecutionCommand.Prosecute, d.Id, Offices)));
        var c = Assert.Single(w.Indictments); Assert.True(c.Powerful); Assert.Equal(mine.Id, c.AccuserId); Assert.False(d.Answered);  // not heard this year
        Assert.Contains(w.History, e => e.Text.Contains(" charges " + head.Name) && e.Text.Contains("hears it at the end of next year"));   // the chronicle and the news agree
        Assert.Contains(ChargeSystem.Touching(w, mine.Id), x => x.Id == c.Id);
        Assert.Equal("there is nothing on the record to charge them with, or they still sit", No(ctx, new ProsecutionCommand(me.Id, ProsecutionCommand.Prosecute, d.Id, Offices)));   // it is under charge
        Year(tc, w);
        Assert.Equal(LadderSystem.LetGo, c.Outcome); Assert.Equal(1, w.Counter(ChargeSystem.AcquittedKey));
        Assert.Equal(legit - 1 - ChargeSystem.AcquittalLegitimacy, mine.Legitimacy);                                                   // a point for any charge let go, and three more
        Assert.Contains(w.News, n => n.Text.Contains("acquitted"));
    }

    [Fact]
    public void The_accused_may_stand_trial_and_the_answers_say_why_not()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]);
        var c = Charged(tc, w, o[0], me, judge); var a = tc.Charges;
        Assert.True(c.Powerful); Assert.Contains(w.News, n => n.Text.Contains("may stand trial"));
        var kin = w.LivingCharacters().First(x => x.HouseId == mine.Id && x.Id != me.Id && x.Age >= 18);
        Assert.Equal("only the head of a house answers a charge for it", No(ctx, new AnswerChargeCommand(kin.Id, c.Id, ChargeSystem.Trial, a)));
        Assert.Equal("no such charge is waiting", No(ctx, new AnswerChargeCommand(me.Id, "nothing", ChargeSystem.Trial, a)));
        Assert.Equal("no such answer", No(ctx, new AnswerChargeCommand(me.Id, c.Id, "flee", a)));
        Assert.Equal("no court has ordered a command laid down", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.LayDown, a)));
        foreach (var f in w.Fleets.Values.Where(f => f.AdmiralId == me.Id)) f.AdmiralId = "";
        Assert.Contains("only its general can cross", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Cross, a)));
        Assert.True(Do(tc, ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Trial, a))); Assert.Equal(ChargeSystem.Trial, c.Answer);
        Assert.Equal("you have said you will stand trial", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Trial, a)));
        // a wilful relative answers for themselves
        var (tc2, w2, me2, mine2, ctx2) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[1]);
        var wilful = Person(w2, mine2, "wilful", "Proud"); Name(w2, wilful); Legion(w2, mine2, wilful);
        var c2 = Charged(tc2, w2, o2[0], wilful, j2);
        Assert.Contains("as they think fit", No(ctx2, new AnswerChargeCommand(me2.Id, c2.Id, ChargeSystem.ExileAnswer, tc2.Charges)));
        Year(tc2, w2);                                                                                                                 // before an upright praetor the proud do not wait
        Assert.Equal(ChargeSystem.Exiled, c2.Outcome); Assert.True(ChargeSystem.Abroad(w2, wilful));
    }

    [Fact]
    public void Exile_lapses_offices_and_commands_and_keeps_you_out_of_politics_but_not_out_of_your_house()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]);
        var f = Legion(w, mine, me); w.Magistrates.Add(new OfficeHolding { OfficeId = LadderSystem.Aedile, HouseId = mine.Id, CharacterId = me.Id, SinceTurn = w.Turn });
        w.Offices.RemoveAll(x => x.OfficeId == "treasurer"); w.Offices.Add(new OfficeHolding { OfficeId = "treasurer", HouseId = mine.Id, CharacterId = me.Id, SinceTurn = w.Turn }); me.Flags.Add("office:treasurer");
        var c = Charged(tc, w, o[0], me, judge); var accuser = o[0]; int legit = mine.Legitimacy, theirs = accuser.Legitimacy, purse = me.Purse = 12;
        Assert.True(Do(tc, ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.ExileAnswer, tc.Charges)));
        Assert.Equal(ChargeSystem.Exiled, c.Outcome); Assert.True(LadderSystem.DeedOf(w, c)!.Answered); Assert.True(ChargeSystem.Abroad(w, me));
        var x = Assert.Single(w.Exiles); Assert.True(x.WasHead); Assert.Equal(accuser.Id, x.AccuserId); Assert.Equal(w.Turn, x.Since);
        if (w.Nations.Any(n => !n.Fallen && !n.AtWar)) Assert.Contains(x.NationName, ChargeSystem.WhereSays(x));
        Assert.DoesNotContain(w.Magistrates, m => m.CharacterId == me.Id); Assert.DoesNotContain(w.Offices, h => h.CharacterId == me.Id);
        Assert.Equal("", f.AdmiralId); Assert.Equal(purse, me.Purse);                                                                  // the purse goes with them
        Assert.Equal(legit - 2 * ChargeSystem.ExileLegitimacy - OfficeSystem.GrantLegitimacy, mine.Legitimacy);   // the head went abroad, and an office of the Reach was lost
        Assert.Equal(theirs + ChargeSystem.ExileAccuserLegitimacy, accuser.Legitimacy);                                                 // the house that charged them gains as for a conviction
        Assert.True(w.HasFlag(ChargeSystem.WentAbroadFlag)); Assert.Equal(me.Id, Economy.HeadOf(w, mine)!.Id);                          // still the head of the house
        Year(tc, w); Assert.Equal(ChargeSystem.Exiled, c.Outcome);                                                                    // and it is never heard
        // what an exile may not do
        string abroad = "in exile";
        Assert.StartsWith("you are in exile", LadderSystem.StandRefusal(w, Offices, me, me, LadderSystem.Quaestor));                  // your own button says "you"
        Assert.Contains(abroad, No(ctx, new AssignAdmiralCommand(me.Id, f.Id, me.Id)));
        Assert.True(CaesarSystem.Barred(w, me)); Assert.Null(OfficeSystem.Candidate(w, Offices.Def("treasurer")!, mine.Id) is { } cand && cand.Id == me.Id ? cand : null);
        var other = Person(w, o[2], "formerly"); var d2 = Deed(w, other);
        Assert.StartsWith("you are in exile", No(ctx, new ProsecutionCommand(me.Id, ProsecutionCommand.Prosecute, d2.Id, Offices)));
        Assert.StartsWith("you are in exile", LadderSystem.BringRefusal(w, me, o[2], "boundary"));
        Assert.Contains("nothing in the Senate but a return from exile", No(ctx, new ProposeMotionCommand(me.Id, "censure", o[2].Id, tc.Senate)));
        Assert.False(tc.Senate.MayPropose(w, mine.Id));
        Assert.Null(No(ctx, new ProposeMotionCommand(me.Id, ChargeSystem.HomeKind, mine.Id, tc.Senate)));                             // the house may move your return
        Assert.Contains(abroad, No(ctx, new ThreatenChargeCommand(me.Id, me.Id, Offices)));
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(x.NationName, Assert.Single(back.Exiles).NationName); Assert.True(back.Exiles[0].WasHead); Assert.Contains(ChargeSystem.ExiledChar, back.Char(me.Id)!.Flags);
        Assert.Equal(ChargeSystem.Exiled, back.Indictments.First(i => i.Id == c.Id).Outcome); Assert.True(back.Indictments.First(i => i.Id == c.Id).Powerful);
        // the return the house may move is its own exile's, not another house's
        var stranger = Person(w, o[2], "stranger"); tc.Charges.Exile(w, stranger, null);
        Assert.Null(ChargeSystem.HomeRefusal(w, o[2]));
        Assert.StartsWith("you are in exile", No(ctx, new ProposeMotionCommand(me.Id, ChargeSystem.HomeKind, o[2].Id, tc.Senate)));
        Assert.Contains("nothing in the Senate but a return from exile of its own", No(ctx, new ProposeMotionCommand(me.Id, ChargeSystem.HomeKind, o[2].Id, tc.Senate)));
        Assert.False(ChargeSystem.MayMoveHome(w, tc.Senate, mine, o[2].Id)); Assert.True(ChargeSystem.MayMoveHome(w, tc.Senate, mine, mine.Id));
    }

    [Fact]
    public void The_Senate_votes_an_exile_home_and_other_houses_move_it()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var c = Charged(tc, w, o[0], me, judge);
        tc.Charges.Exile(w, me, c);
        Assert.Equal($"nobody of {o[2].Name} is in exile", ChargeSystem.HomeRefusal(w, o[2]));
        Assert.Equal(4, ChargeSystem.HomeLean(w, mine, mine, 0)); Assert.True(ChargeSystem.HomeLean(w, o[0], mine, 0) < 0);                // the house that brought it does not want them back
        Assert.True(ChargeSystem.HomeLean(w, o[2], mine, 40) > ChargeSystem.HomeLean(w, o[2], mine, -40));
        Assert.True(Do(tc, ctx, new ProposeMotionCommand(me.Id, ChargeSystem.HomeKind, mine.Id, tc.Senate)));
        var m = Assert.Single(w.Motions, x => x.Kind == ChargeSystem.HomeKind); Assert.Contains("the exile of " + mine.Name, tc.Senate.Describe(w, m)); Assert.Equal(me.Id, ChargeSystem.ExileNamed(w, mine.Id)?.Id);
        foreach (var h in w.Houses.Values) m.Votes[h.Id] = 1;
        int legit = mine.Legitimacy; w.Turn++; tc.Senate.Tick(w);
        Assert.True(m.Passed); Assert.False(ChargeSystem.Abroad(w, me)); Assert.Empty(w.Exiles); Assert.Equal(1, w.Counter(ChargeSystem.HomeKey));
        Assert.True(mine.Legitimacy >= legit + ChargeSystem.HomeLegitimacy - 1); Assert.True(w.HasFlag(ChargeSystem.CalledHomeFlag));
        Assert.Null(LadderSystem.StandRefusal(w, Offices, me, me, LadderSystem.Quaestor) is { } why && why.Contains("exile") ? why : null);
        // another house's exile: after some years their own house moves it
        var (tc2, w2, _, mine2, _) = Start(); var o2 = Others(w2, mine2); var away = Person(w2, o2[0], "away"); tc2.Charges.Exile(w2, away, null);
        Assert.False(ChargeSystem.MoveHome(w2, tc2.Senate, mine2.Id));                                                                  // too soon
        w2.Turn += ChargeSystem.HomeAfterYears;
        Assert.Null(tc2.Senate.Door(w2, o2[0]));                                                                                         // its own house has no voice in the Senate
        Assert.False(ChargeSystem.MoveHome(w2, tc2.Senate, mine2.Id));                                                                  // and nobody is a friend of theirs
        var friend = o2[1]; friend.Title = Praetoria.Core.Progression.Title.Count; Assert.NotNull(tc2.Senate.Door(w2, friend));
        w2.Relationship(Economy.HeadOf(w2, friend)!.Id, away.Id).Disposition = ChargeSystem.HomeFriendAt;                                 // a friend at 30 moves it
        Assert.True(ChargeSystem.MoveHome(w2, tc2.Senate, mine2.Id));
        var moved = Assert.Single(w2.Motions, x => x.Kind == ChargeSystem.HomeKind && x.Open); Assert.Equal(o2[0].Id, moved.TargetId); Assert.Equal(friend.Id, moved.ProposerHouseId);
        Assert.Equal(1, w2.Counter(ChargeSystem.HomeMovedKey));
        w2.Motions.Clear(); o2[0].Title = Praetoria.Core.Progression.Title.Count; Assert.NotNull(tc2.Senate.Door(w2, o2[0]));            // with a voice of its own, its own house moves it
        Assert.True(ChargeSystem.MoveHome(w2, tc2.Senate, mine2.Id));
        moved = Assert.Single(w2.Motions, x => x.Kind == ChargeSystem.HomeKind && x.Open); Assert.Equal(o2[0].Id, moved.TargetId); Assert.Equal(o2[0].Id, moved.ProposerHouseId);
    }

    [Fact]
    public void A_conviction_of_one_who_commands_forces_the_choice_to_lay_it_down_or_cross()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var f = Legion(w, mine, me);
        var c = Charged(tc, w, o[0], me, judge);
        Year(tc, w);                                                                                                                   // you said nothing: it is heard, and proved
        Assert.Equal(LadderSystem.Proved, c.Outcome); Assert.Equal(ChargeSystem.LayDown, c.Sentence); Assert.Equal(w.Turn + 1, c.AnswerBy);
        Assert.Contains("convicted", CaesarSystem.Grievance(w, mine));
        Assert.Equal("it has been answered", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.ExileAnswer, tc.Charges)));
        Assert.True(Do(tc, ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.LayDown, tc.Charges)));
        Assert.Equal(ChargeSystem.LaidDown, c.Sentence); Assert.Equal("", f.AdmiralId); Assert.True(CaesarSystem.Barred(w, me));
        Assert.StartsWith("a court had you lay a command down", No(ctx, new AssignAdmiralCommand(me.Id, f.Id, me.Id)));
        // left unanswered, the court's officers take it, and the house pays
        var (tc2, w2, me2, mine2, _) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[1]); var f2 = Legion(w2, mine2, me2);
        var c2 = Charged(tc2, w2, o2[0], me2, j2); Year(tc2, w2); int legit = mine2.Legitimacy;
        Year(tc2, w2); Assert.Equal(ChargeSystem.LaidDown, c2.Sentence); Assert.Equal("", f2.AdmiralId); Assert.True(mine2.Legitimacy <= legit - ChargeSystem.TakenLegitimacy + 2);
        // another house's general lays it down within the year
        var (tc3, w3, _, mine3, _) = Start(); var o3 = Others(w3, mine3); var j3 = Judge(w3, o3[2]); var g = Person(w3, o3[1], "gen", "Loyal"); Name(w3, g); var f3 = Legion(w3, o3[1], g);
        var c3 = Charged(tc3, w3, o3[0], g, j3); Year(tc3, w3); Assert.Equal(ChargeSystem.LayDown, c3.Sentence);
        Year(tc3, w3); Assert.Equal(ChargeSystem.LaidDown, c3.Sentence); Assert.Equal("", f3.AdmiralId); Assert.Equal(1, w3.Counter(ChargeSystem.LaidDownKey));
    }

    [Fact]
    public void A_general_under_charge_may_cross_the_line_and_the_charge_is_not_heard()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var f = Legion(w, mine, me, hulls: 12); Name(w, me);
        Assert.Contains("no cause", tc.Caesar.CrossRefusal(w, mine));
        var c = Charged(tc, w, o[0], me, judge);
        Assert.Null(tc.Caesar.CrossRefusal(w, mine));                                                                                  // the charge is the cause
        Assert.True(Do(tc, ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Cross, tc.Charges)));
        Assert.Equal(mine.Id, w.CivilWar?.HouseId); Assert.Equal(ChargeSystem.Crossed, c.Outcome); Assert.Equal(1, w.Counter(ChargeSystem.CrossedKey));
        Assert.Contains(w.History, e => e.Text.Contains("CROSSES THE LINE") && e.Text.Contains("charged"));
        Year(tc, w); Assert.Equal(ChargeSystem.Crossed, c.Outcome);
        // another house's charge against one of a house that marches is settled the same way, whoever answers
        var (tc2, w2, _, mine2, _) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[2]); var g = Person(w2, o2[1], "gen", "Honorable");
        o2[1].Title = Praetoria.Core.Progression.Title.Count; var c2 = Charged(tc2, w2, o2[0], Economy.HeadOf(w2, o2[1])!, j2);
        w2.CivilWar = new CivilWar { HouseId = o2[1].Id, GeneralId = g.Id, Since = w2.Turn };
        w2.Turn++; tc2.Charges.Tick(w2); Assert.Equal(ChargeSystem.Crossed, c2.Outcome);
    }

    [Fact]
    public void A_threat_of_a_charge_is_a_cause_is_kept_by_the_charge_and_costs_the_house_that_never_brings_it()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var them = o[0]; Judge(w, o[1]);
        var g = Person(w, them, "gen", "Loyal"); var a = new ThreatenChargeCommand(me.Id, g.Id, Offices);
        Assert.Equal($"{g.Name} holds no command", No(ctx, a));
        var f = Legion(w, them, g);
        Assert.Equal($"there is nothing on the record against {g.Name} that a court would hear", No(ctx, a));
        Assert.Equal("name a general of another house", No(ctx, new ThreatenChargeCommand(me.Id, me.Id, Offices)));
        var d = Deed(w, g); int influence = w.PoolsFor(me.Id).Influence;
        Assert.True(Do(tc, ctx, a)); var t = Assert.Single(w.ChargeThreats);
        Assert.Equal(influence - ChargeSystem.ThreatInfluence, w.PoolsFor(me.Id).Influence); Assert.Equal(d.Id, t.DeedId);
        Assert.Contains("will be charged the day", CaesarSystem.Grievance(w, them));                                                    // a cause for their house
        Assert.Equal("your house has let one such thing be known already, and has not yet made it good", No(ctx, new ThreatenChargeCommand(me.Id, g.Id, Offices)));
        Assert.True(ChargeSystem.Threatened(w, d.Id)); w.Turn += LadderSystem.RecordYears + 2; LadderSystem.Tick(w, Offices, Content.Policies);
        Assert.Contains(w.OfficeRecord, x => x.Id == d.Id);                                                                           // a threatened act is not forgotten
        // the general lays the command down: you have until the end of the year after to bring it
        f.AdmiralId = ""; int legit = mine.Legitimacy; tc.Charges.Tick(w);
        Assert.Equal(w.Turn, t.LaidDownTurn); Assert.Null(ChargeSystem.Cause(w, them)); Assert.Contains(w.News, n => n.Text.Contains($"bring the charge by the end of year {w.Turn + ChargeSystem.ThreatGraceYears}"));
        Assert.Equal("", t.Outcome);                                                                                                   // not empty in the year it was laid down
        w.Turn++; tc.Charges.Tick(w);                                                                                                  // the end of the year you were told
        Assert.Equal(ChargeSystem.Empty, t.Outcome); Assert.Equal(legit - ChargeSystem.ThreatEmptyLegitimacy, mine.Legitimacy);
        // kept: the charge is brought the day it is laid down
        var (tc2, w2, me2, mine2, ctx2) = Start(); var o2 = Others(w2, mine2); Judge(w2, o2[1]); var g2 = Person(w2, o2[0], "gen2", "Loyal"); var f2 = Legion(w2, o2[0], g2); var d2 = Deed(w2, g2);
        Assert.True(Do(tc2, ctx2, new ThreatenChargeCommand(me2.Id, g2.Id, Offices))); f2.AdmiralId = ""; tc2.Charges.Tick(w2);
        Assert.True(Do(tc2, ctx2, new ProsecutionCommand(me2.Id, ProsecutionCommand.Prosecute, d2.Id, Offices)));
        Assert.Equal(ChargeSystem.Kept, Assert.Single(w2.ChargeThreats).Outcome); Assert.Equal(1, w2.Counter(ChargeSystem.ThreatsKeptKey));
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w2)); Assert.Equal(ChargeSystem.Kept, Assert.Single(back.ChargeThreats).Outcome);
    }

    [Fact]
    public void Other_houses_threaten_a_powerful_commander_wait_for_the_command_to_be_laid_down_and_then_charge()
    {
        var (tc, w, _, mine, _) = Start(); var o = Others(w, mine); var hater = o[0]; var them = o[1]; them.Title = Praetoria.Core.Progression.Title.Count; Judge(w, o[2]);
        var head = Economy.HeadOf(w, them)!; head.NatureTraits.Clear(); head.NatureTraits.Add("Honorable"); var f = Legion(w, them, head); Deed(w, head); Hate(w, hater, them);
        tc.Charges.Tick(w);
        var t = Assert.Single(w.ChargeThreats); Assert.Equal(hater.Id, t.AccuserId); Assert.Equal(head.Id, t.AccusedId);
        Assert.True(ChargeSystem.Waits(w, hater.Id, head.Id));
        LadderSystem.Tick(w, Offices, Content.Policies); Assert.Empty(w.Indictments);                                                 // it waits while the fleet is theirs
        f.AdmiralId = ""; w.Turn++; tc.Charges.Tick(w);
        Assert.Equal(ChargeSystem.Kept, t.Outcome); var c = Assert.Single(w.Indictments); Assert.True(c.Powerful); Assert.Equal(hater.Id, c.AccuserId);
    }

    [Fact]
    public void Everything_the_answers_say_is_plain_and_counts_and_money_go_through_their_helpers()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var c = Charged(tc, w, o[0], me, judge);
        w.Offices.RemoveAll(x => x.OfficeId == "treasurer"); w.Offices.Add(new OfficeHolding { OfficeId = "treasurer", HouseId = mine.Id, CharacterId = me.Id, SinceTurn = w.Turn }); me.Flags.Add("office:treasurer");
        foreach (var ans in new[] { ChargeSystem.Trial, ChargeSystem.ExileAnswer, ChargeSystem.Cross, ChargeSystem.LayDown })
            if (No(ctx, new AnswerChargeCommand(me.Id, c.Id, ans, tc.Charges)) is { } why) { Assert.DoesNotContain("(s)", why); Assert.DoesNotContain("!", why); Assert.DoesNotContain("—", why); }
        tc.Charges.Exile(w, me, c);
        Assert.Contains(w.History, e => e.Text.Contains("is relieved of the") && e.Text.Contains("gone into exile"));                    // an office of the Reach lapses
        foreach (var n in w.News.Concat(w.History.Select(e => new NewsItem { Text = e.Text }))) { Assert.DoesNotContain("(s)", n.Text); Assert.DoesNotContain("—", n.Text); }
        Assert.Contains("you are in exile", ChargeSystem.AbroadSays(w, me, you: true));
    }

    [Fact]
    public void A_house_that_crossed_by_the_Caesar_road_answers_nothing_before_the_Republics_courts()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var f = Legion(w, mine, me, hulls: 12); Name(w, me);
        var c = Charged(tc, w, o[0], me, judge);
        Assert.True(Do(tc, ctx, new CaesarCommand(me.Id, CaesarCommand.Cross, "", tc.Caesar)));                                       // crossed from the Caesar panel, not the courts
        Assert.Equal(mine.Id, w.CivilWar?.HouseId); Assert.Equal("", c.Outcome);
        foreach (var ans in new[] { ChargeSystem.Trial, ChargeSystem.ExileAnswer, ChargeSystem.Cross, ChargeSystem.LayDown })
            Assert.Equal("the house has crossed the line: the Republic's courts will not hear it", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ans, tc.Charges)));
        Assert.False(ChargeSystem.Abroad(w, me)); Assert.Equal(me.Id, f.AdmiralId); Assert.Equal(me.Id, w.CivilWar!.GeneralId);          // the general marches with the fleet
        w.Turn++; tc.Charges.Tick(w); Assert.Equal(ChargeSystem.Crossed, c.Outcome);                                                   // and the charge is settled at the turn of the year
    }

    [Fact]
    public void Only_the_general_crosses_and_the_refusal_names_the_general()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]);
        var small = Legion(w, mine, me, hulls: 2, devotion: 0); var kin = Person(w, mine, "marshal"); Name(w, kin); Legion(w, mine, kin, hulls: 14);
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id && x.Id != small.Id && x.AdmiralId == me.Id)) x.AdmiralId = "";
        Assert.Equal(kin.Id, CaesarSystem.GeneralOf(w, mine)?.Id); Assert.True(ChargeSystem.Commands(w, me));
        var c = Charged(tc, w, o[0], me, judge);
        Assert.Equal($"{kin.Name} is the house's general, not you: only its general can cross the line", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Cross, tc.Charges)));
        small.AdmiralId = "";
        Assert.Equal("you command none of the house's fleets: only its general can cross the line", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Cross, tc.Charges)));
    }

    [Fact]
    public void An_exile_is_given_no_fleet_asks_for_none_and_leads_no_levy()
    {
        var (tc, w, me, mine, ctx) = Start();
        var kin = Person(w, mine, "officer"); kin.Ambition = "command_a_fleet"; kin.CareerTrack = "military"; kin.Skills["tactics"] = 9; w.Relationship(kin.Id, me.Id).Disposition = 30;
        var f = w.Fleets.Values.Where(x => x.OwnerId == mine.Id).OrderBy(x => x.Id, StringComparer.Ordinal).First(); f.AdmiralId = "";
        kin.Pursuit = new Pursuit { Goal = kin.Ambition, Since = w.Turn - 2, Request = AmbitionSystem.Command, RequestArg = f.Id, RequestSince = w.Turn, LastAsked = w.Turn };
        var grant = new AmbitionCommand(me.Id, AmbitionCommand.Grant, kin.Id, tc.Ambitions);
        Assert.Null(No(ctx, grant));
        tc.Charges.Exile(w, kin, null);
        Assert.StartsWith(kin.Name + " is in exile", No(ctx, grant)); Assert.False(Do(tc, ctx, grant)); Assert.NotEqual(kin.Id, f.AdmiralId);
        // nor do they ask for one while abroad
        kin.Pursuit.Request = ""; kin.Pursuit.RequestArg = ""; kin.Pursuit.LastAsked = -10; kin.Flags.Remove(AmbitionSystem.AskingChar);
        w.Turn++; tc.Ambitions.Tick(w, tc.Executor, ctx);
        Assert.Equal("", kin.Pursuit!.Request); Assert.Contains("exile", kin.Pursuit.Note);
        // one the Senate had lay a command down is not given one either
        var barred = Person(w, mine, "barred"); barred.Ambition = "command_a_fleet"; w.WorldCounters[CaesarSystem.BarredKey(barred.Id)] = w.Turn + CaesarSystem.BarredYears;
        barred.Pursuit = new Pursuit { Goal = barred.Ambition, Since = w.Turn - 2, Request = AmbitionSystem.Command, RequestArg = f.Id, RequestSince = w.Turn, LastAsked = w.Turn };
        Assert.Equal($"the Senate had {barred.Name} lay a command down", No(ctx, new AmbitionCommand(me.Id, AmbitionCommand.Grant, barred.Id, tc.Ambitions)));
        // the levy a house sends to the holder of its world sails under somebody at home
        var w2 = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 7UL); var tc2 = new TurnController(w2, Content);
        var house = w2.House(w2.Protagonist!.HouseId)!; w2.Wars.Clear(); foreach (var n in w2.Nations) n.AtWar = false;
        var seat = w2.HoldingsOf(house.Id).First(h => h.IsPlanet); var sender = w2.House(ProvinceSystem.HolderOf(seat, 1))!; string id = ProvinceSystem.LevyPrefix + ProvinceSystem.Target(seat.Id, 1);
        var foe = w2.Houses.Values.First(h => !World.Outsider(h.Id) && h.Id != house.Id && h.Id != sender.Id && h.Id != ProvinceSystem.HolderOf(seat, 2) && ProvinceSystem.LeanUnder(w2, sender.Id, h.Id) == 0);
        var best = Person(w2, sender, "levyman"); best.Skills["tactics"] = 20; best.Skills["leadership"] = 20;                          // the one it would send
        tc2.Charges.Exile(w2, best, null);
        w2.Wars.Add(new War { HouseA = house.Id, HouseB = foe.Id, Since = w2.Turn }); ProvinceSystem.Tick(w2, Content.Holdings);
        Assert.True(w2.Fleets.ContainsKey(id)); var sent = w2.Fleets.Values.Where(x => x.LevyOf == sender.Id).ToList(); Assert.NotEmpty(sent);   // every levy the house sends
        Assert.All(sent, x => Assert.False(ChargeSystem.Abroad(w2, w2.Char(x.AdmiralId)), x.Name + " sails under an exile"));
    }

    [Fact]
    public void A_head_in_exile_petitions_for_no_rank_and_an_exile_pleads_in_no_court()
    {
        var (tc, w, _, mine, _) = Start(); var progression = tc.Progression!;
        var rising = Others(w, mine).First(h =>
        {
            h.Title = Praetoria.Core.Progression.Title.Baron; h.Legitimacy = 100;                                                     // a Prefect's house, whose next rung the Senate votes
            return progression.NextTitle(w, h.Id) is { } next && progression.Voted(next) && progression.PetitionRefusal(w, Economy.HeadOf(w, h)!.Id) == null;
        });
        var head = Economy.HeadOf(w, rising)!; Assert.Contains("petition", progression.OpenRoads(w, head.Id));
        tc.Charges.Exile(w, head, null);
        Assert.StartsWith(head.Name + " is in exile", progression.PetitionRefusal(w, head.Id)); Assert.Contains("nothing in the Senate but a return from exile", progression.PetitionRefusal(w, head.Id));
        Assert.DoesNotContain("petition", progression.OpenRoads(w, head.Id));
        // an exile pleads for nobody, and is not the house's advocate
        var (tc2, w2, me2, mine2, ctx2) = Start(); var o2 = Others(w2, mine2); var judge = Judge(w2, o2[1]);
        var sage = Person(w2, mine2, "sage"); sage.Skills["law"] = 20; sage.Skills["oratory"] = 20; var kin = Person(w2, mine2, "kin");
        var c = Charged(tc2, w2, o2[0], kin, judge);
        Assert.Equal(sage.Id, LadderSystem.Advocate(w2, mine2.Id)?.Id); Assert.Null(LadderSystem.ChargePleadRefusal(w2, me2, c, sage));
        Assert.True(Do(tc2, ctx2, new ChargeCommand(me2.Id, ChargeCommand.Plead, c.Id, Offices, sage.Id))); Assert.Equal(sage.Id, LadderSystem.PleaderOf(w2, c)?.Id);
        tc2.Charges.Exile(w2, sage, null);
        Assert.StartsWith(sage.Name + " is in exile", LadderSystem.ChargePleadRefusal(w2, me2, c, sage));
        Assert.StartsWith(sage.Name + " is in exile", No(ctx2, new ChargeCommand(me2.Id, ChargeCommand.Plead, c.Id, Offices, sage.Id)));
        Assert.NotEqual(sage.Id, LadderSystem.Advocate(w2, mine2.Id)?.Id); Assert.Null(LadderSystem.PleaderOf(w2, c));                 // whoever was sent is not heard from abroad
    }

    [Theory]
    [InlineData("the_road_abroad", ChargeSystem.WentAbroadFlag)]
    [InlineData("called_home", ChargeSystem.CalledHomeFlag)]
    [InlineData("the_charge_thrown_back", ChargeSystem.ThrownBackFlag)]
    public void Each_scene_of_a_charge_against_the_powerful_can_be_reached_and_every_answer_to_it_taken(string eventId, string flag)
    {
        foreach (string answer in Content.Events.First(e => e.Id == eventId).Choices.Select(c => c.Id))
        {
            var (_, w, _, mine, _) = Start(); mine.Treasury.Credits = 100; w.WorldFlags.Add(flag);
            var engine = new Praetoria.Core.Events.EventEngine(Content, WorldBuilder.RngFor(w));
            var found = engine.GatherEligible(w).FirstOrDefault(e => e.def.Id == eventId); Assert.True(found.def != null, eventId + " is not eligible");
            engine.Resolve(new Praetoria.Core.Events.FiredEvent(found.def!, found.binding), w, answer);
            Assert.False(w.HasFlag(flag), eventId + "/" + answer + " leaves its flag up");
        }
    }

    [Fact]
    public void The_scenes_are_raised_by_what_happens()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var c = Charged(tc, w, o[0], me, judge);
        tc.Charges.Exile(w, me, c); Assert.True(w.HasFlag(ChargeSystem.WentAbroadFlag));
        ChargeSystem.Home(w, mine, o[2]); Assert.True(w.HasFlag(ChargeSystem.CalledHomeFlag));
        w.Turn += 2; tc.Charges.Tick(w); Assert.False(w.HasFlag(ChargeSystem.WentAbroadFlag)); Assert.False(w.HasFlag(ChargeSystem.CalledHomeFlag));   // a year after the deed, gone
        var (tc2, w2, me2, mine2, _) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[1], "Loyal"); w2.Relationship(Economy.HeadOf(w2, o2[1])!.Id, me2.Id).Disposition = 50;
        var c2 = Charged(tc2, w2, o2[0], me2, j2); Year(tc2, w2); Assert.Equal(LadderSystem.LetGo, c2.Outcome); Assert.True(w2.HasFlag(ChargeSystem.ThrownBackFlag));
    }

    // ---------------- mended after the three Imperator ideas met ----------------

    /// <summary>The year's end in the turn's own order (TurnController.EndTurn): the generals and the triumphs at the gate, then the
    /// accused answer, then the courts sit.</summary>
    private static void YearEnd(TurnController tc, World w) { tc.Caesar.Tick(w); TriumphSystem.Tick(w, tc.Senate, tc.Caesar); tc.Charges.Tick(w); LadderSystem.Tick(w, Offices, Content.Policies); }

    [Fact]
    public void Coming_home_for_a_triumph_is_no_way_out_of_a_court_s_order_to_lay_the_command_down()
    {
        // convicted in command, then home for a triumph in the year the command must be laid down: the court's officers take it, and the bar is the court's
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]); var f = Legion(w, mine, me);
        var c = Charged(tc, w, o[0], me, judge);
        w.Turn++; YearEnd(tc, w);
        Assert.Equal(LadderSystem.Proved, c.Outcome); Assert.Equal(ChargeSystem.LayDown, c.Sentence); int by = c.AnswerBy;
        TriumphSystem.Voted(w, mine, me, null);
        w.Turn++; Assert.Equal(by, w.Turn);
        Assert.True(Do(tc, ctx, new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar))); Assert.True(TriumphSystem.HeldFor(w, f, me.Id));
        YearEnd(tc, w);
        Assert.Equal(ChargeSystem.LaidDown, c.Sentence); Assert.Equal(1, w.Counter(ChargeSystem.LaidDownKey));
        Assert.Equal(w.Turn + CaesarSystem.BarredYears, w.Counter(ChargeSystem.CourtBarKey(me.Id))); Assert.Equal(w.Turn + CaesarSystem.BarredYears, w.Counter(CaesarSystem.BarredKey(me.Id)));
        Assert.False(TriumphSystem.Held(w, f)); Assert.Null(TriumphSystem.AtHome(w, me.Id)); Assert.DoesNotContain(TriumphSystem.HomeChar, me.Flags);   // the fleet that waited is given up with it
        w.Turn++; YearEnd(tc, w);
        Assert.Equal("", f.AdmiralId); Assert.True(CaesarSystem.Barred(w, me));                                                          // and the year out, nothing is taken up again

        // home first, then convicted: the fleet waits for them, so they command it for the court, and laying it down gives up the fleet that waits
        var (tc2, w2, me2, mine2, ctx2) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[1]); var f2 = Legion(w2, mine2, me2);
        var c2 = Charged(tc2, w2, o2[0], me2, j2); TriumphSystem.Voted(w2, mine2, me2, null);
        w2.Turn++; Assert.True(Do(tc2, ctx2, new TriumphCommand(me2.Id, TriumphSystem.Home, tc2.Caesar)));
        YearEnd(tc2, w2);
        Assert.Equal(LadderSystem.Proved, c2.Outcome); Assert.Equal(ChargeSystem.LayDown, c2.Sentence); Assert.Equal(w2.Turn + ChargeSystem.LayDownYears, c2.AnswerBy);
        Assert.Contains(w2.News, n => n.Text.StartsWith("You are convicted while in command"));
        w2.Turn++; Assert.True(Do(tc2, ctx2, new AnswerChargeCommand(me2.Id, c2.Id, ChargeSystem.LayDown, tc2.Charges)));
        Assert.Equal(ChargeSystem.LaidDown, c2.Sentence); Assert.False(TriumphSystem.Held(w2, f2)); Assert.Equal("", f2.DevotedTo); Assert.True(w2.TriumphVotes.Single().Done);
        YearEnd(tc2, w2); Assert.Equal("", f2.AdmiralId); Assert.True(CaesarSystem.Barred(w2, me2)); Assert.Equal(w2.Turn + CaesarSystem.BarredYears, w2.Counter(ChargeSystem.CourtBarKey(me2.Id)));

        // another house's general, convicted and then voted a triumph: they come home at the year's end, before the accused are read, and the court's order holds
        var (tc3, w3, _, mine3, _) = Start(); var o3 = Others(w3, mine3); var j3 = Judge(w3, o3[2]);
        foreach (var x in w3.Fleets.Values.Where(x => x.OwnerId == o3[1].Id)) x.AdmiralId = "";
        var g = Person(w3, o3[1], "gen", "Loyal"); Name(w3, g); var f3 = Legion(w3, o3[1], g);
        var c3 = Charged(tc3, w3, o3[0], g, j3); w3.Turn++; YearEnd(tc3, w3); Assert.Equal(ChargeSystem.LayDown, c3.Sentence);
        TriumphSystem.Voted(w3, o3[1], g, null);
        w3.Turn++; YearEnd(tc3, w3);
        var t3 = w3.TriumphVotes.Single(t => t.CharacterId == g.Id); Assert.Equal(TriumphSystem.Home, t3.Choice); Assert.True(t3.Done);
        Assert.Equal(ChargeSystem.LaidDown, c3.Sentence); Assert.Equal(w3.Turn + CaesarSystem.BarredYears, w3.Counter(ChargeSystem.CourtBarKey(g.Id))); Assert.Equal(1, w3.Counter(ChargeSystem.LaidDownKey));
        w3.Turn++; YearEnd(tc3, w3); Assert.NotEqual(g.Id, f3.AdmiralId);
    }

    [Fact]
    public void Going_into_exile_while_home_for_a_triumph_ends_the_year_at_home_and_the_fleet_is_the_house_s_to_fill()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]);
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id)) x.AdmiralId = "";
        var kin = Person(w, mine, "kin", "Loyal"); Name(w, kin); var f = Legion(w, mine, kin, devotion: 90);
        var c = Charged(tc, w, o[0], kin, judge); Assert.True(c.Powerful);
        TriumphSystem.Voted(w, mine, kin, null); TriumphSystem.Tick(w, tc.Senate, tc.Caesar);                                         // a relative chooses for themselves: home
        var t = w.TriumphVotes.Single(); Assert.Equal(TriumphSystem.Home, t.Choice); Assert.True(TriumphSystem.HeldFor(w, f, kin.Id));
        Assert.True(Do(tc, ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.ExileAnswer, tc.Charges)));
        Assert.True(ChargeSystem.Abroad(w, kin));
        Assert.True(t.Done); Assert.Equal("went into exile", t.Ended); Assert.Null(TriumphSystem.AtHome(w, kin.Id));
        Assert.False(TriumphSystem.Held(w, f)); Assert.DoesNotContain(TriumphSystem.HomeChar, kin.Flags); Assert.Equal("", f.DevotedTo); Assert.Equal(0, f.Devotion);
        Assert.StartsWith(kin.Name + " is in exile", No(ctx, new AssignAdmiralCommand(me.Id, f.Id, kin.Id)));
        Assert.StartsWith(kin.Name + " is in exile", ServiceSystem.FreeRefusal(w, kin));
        Assert.Empty(TriumphSystem.HomeFor(w, mine.Id));                                                                               // the gate no longer says they are home
        w.Turn++; YearEnd(tc, w); w.Turn++; YearEnd(tc, w);
        Assert.DoesNotContain(w.News, n => n.Text.Contains("the Senate had them lay a command down")); Assert.DoesNotContain(w.History, e => e.Text.Contains("has no command to take up"));

        // should one abroad still have the year at home open, nothing holds the fleet for them, every refusal gives the exile, and the year's end says so
        var (tc2, w2, me2, mine2, ctx2) = Start(); var f2 = Legion(w2, mine2, me2);
        TriumphSystem.Voted(w2, mine2, me2, null); Assert.True(Do(tc2, ctx2, new TriumphCommand(me2.Id, TriumphSystem.Home, tc2.Caesar)));
        me2.Flags.Add(ChargeSystem.ExiledChar);
        Assert.False(TriumphSystem.Held(w2, f2));
        Assert.StartsWith("you are in exile", No(ctx2, new AssignAdmiralCommand(me2.Id, f2.Id, me2.Id)));
        Assert.StartsWith(me2.Name + " is in exile", ServiceSystem.FreeRefusal(w2, me2));
        var t2 = w2.TriumphVotes.Single(); w2.Turn = t2.HomeUntil; TriumphSystem.Tick(w2, tc2.Senate, tc2.Caesar);
        Assert.StartsWith(me2.Name + " is in exile", t2.Ended); Assert.Contains(w2.News, n => n.Text.StartsWith("The year at home is out, and you have no command to take up: you are in exile"));
        Assert.DoesNotContain(w2.News, n => n.Text.Contains("the Senate had them"));
    }

    [Fact]
    public void A_threat_is_due_by_the_end_of_the_year_after_a_triumph_lays_the_command_down_though_it_is_taken_up_again()
    {
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var them = o[0]; Judge(w, o[1]);
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == them.Id)) x.AdmiralId = "";
        var g = Person(w, them, "gen", "Loyal"); var f = Legion(w, them, g); Deed(w, g);
        Assert.True(Do(tc, ctx, new ThreatenChargeCommand(me.Id, g.Id, Offices))); var t = Assert.Single(w.ChargeThreats);
        TriumphSystem.Voted(w, them, g, null);
        YearEnd(tc, w);                                                                                                                 // home for the triumph: the command is laid down, and the charge is due
        Assert.Equal("", f.AdmiralId); Assert.Equal(w.Turn, t.LaidDownTurn); Assert.Contains(w.News, n => n.Text.Contains($"bring the charge by the end of year {w.Turn + ChargeSystem.ThreatGraceYears}"));
        w.Turn++; YearEnd(tc, w);                                                                                                       // the year out, they take it up again; the charge was never brought
        Assert.Equal(g.Id, f.AdmiralId); Assert.Equal(ChargeSystem.Empty, t.Outcome); Assert.Equal(1, w.Counter(ChargeSystem.ThreatsEmptyKey));
        Assert.Contains(w.News, n => n.Text == $"Your house said {g.Name} would be charged, and never brought it: {ChargeSystem.ThreatEmptyLegitimacy} legitimacy lost.");
        Assert.DoesNotContain("let one such thing be known already", No(ctx, new ThreatenChargeCommand(me.Id, g.Id, Offices)) ?? "");   // the word is spent: another may be said
    }

    [Fact]
    public void The_courts_panel_says_when_a_threatened_charge_is_due_and_that_the_act_is_kept_on_the_record()
    {
        // another house threatens the player's general, who comes home for a triumph; no praetor sits, so the charge is not brought at once
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var them = o[0];
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id)) x.AdmiralId = "";
        var f = Legion(w, mine, me); var d = Deed(w, me); var other = Deed(w, Person(w, mine, "clerk"));
        var t = ChargeSystem.Threaten(w, Offices, Economy.HeadOf(w, them)!, me);
        Assert.Equal($"{them.Name} has let it be known that you will be charged the day the command is laid down. While it stands it is a cause to cross the line.", ChargeSystem.ThreatSays(w, t, mine.Id));
        Assert.Equal($"kept on the record while {them.Name}'s word stands", ChargeSystem.ForgottenSays(w, d));
        Assert.Equal($"forgotten in year {other.Turn + LadderSystem.RecordYears + 1}", ChargeSystem.ForgottenSays(w, other));
        TriumphSystem.Voted(w, mine, me, null); Assert.True(Do(tc, ctx, new TriumphCommand(me.Id, TriumphSystem.Home, tc.Caesar))); tc.Charges.Tick(w);
        Assert.Equal(w.Turn, t.LaidDownTurn); Assert.Null(ChargeSystem.Cause(w, mine));
        string line = ChargeSystem.ThreatSays(w, t, mine.Id);
        Assert.Contains($"so the charge is due by the end of year {w.Turn + ChargeSystem.ThreatGraceYears}, and it is no longer a cause to cross the line", line); Assert.DoesNotContain("While it stands", line);
        Assert.Equal($"{me.Name} has laid the command down. Bring the charge by the end of year {w.Turn + ChargeSystem.ThreatGraceYears}, from the record below, or your house loses {ChargeSystem.ThreatEmptyLegitimacy} legitimacy.", ChargeSystem.ThreatSays(w, t, them.Id));
        // the screens read them
        string Razor(string name) => File.ReadAllText(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Web", "Components", name));
        Assert.Contains("ChargeSystem.ThreatSays(w, t, house.Id)", Razor("PowerfulCharges.razor")); Assert.Contains("ChargeSystem.ForgottenSays(w, d)", Razor("CourtsPanel.razor"));
    }

    [Fact]
    public void The_Senate_window_gives_an_exiled_head_the_exile_as_the_reason_and_promises_no_triumph()
    {
        var (tc, w, me, mine, ctx) = Start(); me.Cursus.Add(LadderSystem.Quaestor); Assert.True(tc.Senate.MayPropose(w, mine.Id));
        Assert.Null(CourtProjection.Build(tc, Content).Senate!.HeadAbroad);
        tc.Charges.Exile(w, me, null);
        var card = CourtProjection.Build(tc, Content).Senate!;
        Assert.False(card.MayPropose); Assert.StartsWith("you are in exile", card.HeadAbroad); Assert.EndsWith("the house may move nothing in the Senate but a return from exile of its own", card.HeadAbroad);
        Assert.StartsWith("you are in exile", No(ctx, new ProposeMotionCommand(me.Id, TriumphSystem.TriumphKind, Others(w, mine)[0].Id, tc.Senate)));   // what the line says is what the command says
        string razor = File.ReadAllText(Path.Combine(ContentLocator.FindContentDir(), "..", "src", "Web", "Components", "SenateScreen.razor"));
        Assert.Contains("@if (sen.HeadAbroad is { } abroad)", razor);                                                                  // said instead of the door and the generals' business
    }

    [Fact]
    public void A_charge_against_one_who_has_died_is_no_cause()
    {
        var (tc, w, _, mine, _) = Start(); var o = Others(w, mine); var them = o[1]; them.Title = Praetoria.Core.Progression.Title.Count; var judge = Judge(w, o[2]);
        var head = Economy.HeadOf(w, them)!; head.NatureTraits.Clear(); head.NatureTraits.Add("Honorable");
        var c = Charged(tc, w, o[0], head, judge); Assert.NotNull(ChargeSystem.Cause(w, them));
        DynastySystem.Die(w, head.Id, "of age");
        Assert.Null(ChargeSystem.Cause(w, them)); Assert.Null(CaesarSystem.Grievance(w, them) is { } gr && gr.Contains(head.Name) ? gr : null);
        // nor is a conviction of one who must lay a command down and has died since
        var (tc2, w2, _, mine2, _) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[2]); var g = Person(w2, o2[1], "gen", "Loyal"); Name(w2, g); Legion(w2, o2[1], g);
        var c2 = Charged(tc2, w2, o2[0], g, j2); Year(tc2, w2); Assert.Equal(ChargeSystem.LayDown, c2.Sentence); Assert.NotNull(ChargeSystem.Cause(w2, o2[1]));
        DynastySystem.Die(w2, g.Id, "of a fever"); Assert.Null(ChargeSystem.Cause(w2, o2[1]));
    }

    [Fact]
    public void The_answers_and_the_news_say_you_to_the_player()
    {
        // the player is the general, with too few hulls behind them: the refusal says "you"
        var (tc, w, me, mine, ctx) = Start(); var o = Others(w, mine); var judge = Judge(w, o[1]);
        foreach (var x in w.Fleets.Values.Where(x => x.OwnerId == mine.Id)) x.AdmiralId = "";
        Name(w, me); Legion(w, mine, me, hulls: 2, devotion: 90);
        var c = Charged(tc, w, o[0], me, judge);
        Assert.Contains(w.News, n => n.Text.StartsWith("You are " + ChargeSystem.PowerfulWhy(w, me) + ". Before the charge is heard you may stand trial"));
        Assert.DoesNotContain(w.News, n => n.Text.StartsWith(me.Name + " is "));
        Assert.Equal($"2 hulls would follow you against the Senate, and it takes {CaesarSystem.MarchHulls}", No(ctx, new AnswerChargeCommand(me.Id, c.Id, ChargeSystem.Cross, tc.Charges)));
        // a relative is charged, and the player is the house's general
        var (tc2, w2, me2, mine2, ctx2) = Start(); var o2 = Others(w2, mine2); var j2 = Judge(w2, o2[1]);
        foreach (var x in w2.Fleets.Values.Where(x => x.OwnerId == mine2.Id)) x.AdmiralId = "";
        Name(w2, me2); Legion(w2, mine2, me2, hulls: 14); var kin = Person(w2, mine2, "kin"); Name(w2, kin); w2.Fleets["small"] = new Fleet { Id = "small", Name = "The Small", OwnerId = mine2.Id, SystemId = w2.Fleets["legion_" + me2.Id].SystemId, Strength = 2, AdmiralId = kin.Id };
        Assert.Equal(me2.Id, CaesarSystem.GeneralOf(w2, mine2)?.Id);
        var c2 = Charged(tc2, w2, o2[0], kin, j2);
        Assert.Equal($"you are the house's general, not {kin.Name}: only its general can cross the line", No(ctx2, new AnswerChargeCommand(me2.Id, c2.Id, ChargeSystem.Cross, tc2.Charges)));
    }

    [Fact]
    public void An_exile_lives_at_the_court_of_a_nation_named_as_a_sentence_names_it()
    {
        var (tc, w, me, mine, ctx) = Start(); tc.Charges.Exile(w, me, null); var x = Assert.Single(w.Exiles);
        Assert.NotEqual("", x.NationId);
        Assert.Equal("at the court of " + ForeignGoldSystem.Name(Content.Nations, x.NationId), ChargeSystem.WhereSays(x));
        Assert.DoesNotContain("court of The ", ChargeSystem.WhereSays(x)); Assert.DoesNotContain("court of The ", ChargeSystem.AbroadSays(w, me, you: true));
    }

    [Fact]
    public void Three_sagas_of_thirty_years_never_leave_an_exile_without_a_record_or_a_charge_unanswered()
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed); var tc = new TurnController(w, Content);
            for (int t = 0; t < 30 && !w.IsDynastyDead; t++)
            {
                foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
                tc.EndTurn();
                foreach (var c in w.Characters.Values.Where(c => c.Alive && c.Flags.Contains(ChargeSystem.ExiledChar))) Assert.Contains(w.Exiles, x => x.CharacterId == c.Id);
                Assert.All(w.Indictments.Where(c => c.Powerful && c.Outcome.Length == 0), c => Assert.True(c.Turn >= w.Turn - 1, $"a charge of year {c.Turn} still waits in year {w.Turn}"));
                Assert.All(w.Fleets.Values.Where(f => f.AdmiralId.Length > 0), f => Assert.False(ChargeSystem.Abroad(w, w.Char(f.AdmiralId)), "an exile commands " + f.Name));
            }
        }
    }
}
