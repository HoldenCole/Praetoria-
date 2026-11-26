using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
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
        Assert.Equal(legit - 2 * ChargeSystem.ExileLegitimacy - OfficeSystem.GrantLegitimacy, mine.Legitimacy);   // the head went abroad, and an office of the Reach was lost Assert.Equal(theirs + ChargeSystem.ExileAccuserLegitimacy, accuser.Legitimacy);
        Assert.True(w.HasFlag(ChargeSystem.WentAbroadFlag)); Assert.Equal(me.Id, Economy.HeadOf(w, mine)!.Id);                          // still the head of the house
        Year(tc, w); Assert.Equal(ChargeSystem.Exiled, c.Outcome);                                                                    // and it is never heard
        // what an exile may not do
        string abroad = "in exile";
        Assert.Contains(abroad, LadderSystem.StandRefusal(w, Offices, me, me, LadderSystem.Quaestor));
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
        if (tc2.Senate.Door(w2, o2[0]) != null) { Assert.True(ChargeSystem.MoveHome(w2, tc2.Senate, mine2.Id)); Assert.Contains(w2.Motions, x => x.Kind == ChargeSystem.HomeKind && x.TargetId == o2[0].Id && x.Open); }
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
        Assert.Equal(w.Turn, t.LaidDownTurn); Assert.Null(ChargeSystem.Cause(w, them)); Assert.Contains(w.News, n => n.Text.Contains("bring the charge by the end of year"));
        w.Turn++; tc.Charges.Tick(w); Assert.Equal("", t.Outcome);
        w.Turn++; tc.Charges.Tick(w); Assert.Equal(ChargeSystem.Empty, t.Outcome); Assert.Equal(legit - ChargeSystem.ThreatEmptyLegitimacy, mine.Legitimacy);
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
        foreach (var ans in new[] { ChargeSystem.Trial, ChargeSystem.ExileAnswer, ChargeSystem.Cross, ChargeSystem.LayDown })
            if (No(ctx, new AnswerChargeCommand(me.Id, c.Id, ans, tc.Charges)) is { } why) { Assert.DoesNotContain("(s)", why); Assert.DoesNotContain("!", why); Assert.DoesNotContain("—", why); }
        tc.Charges.Exile(w, me, c);
        foreach (var n in w.News.Concat(w.History.Select(e => new NewsItem { Text = e.Text }))) { Assert.DoesNotContain("(s)", n.Text); Assert.DoesNotContain("—", n.Text); }
        Assert.Contains("you are in exile", ChargeSystem.AbroadSays(w, me, you: true));
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
