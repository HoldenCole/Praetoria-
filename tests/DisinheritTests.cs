using Praetoria.Core;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Struck from the line, cast out, and the undutiful will (docs/HOUSEHOLD.md; docs/IDEAS_FROM_MODS.md, entry 3): the head may
/// strike a grown relative of the blood out of the line or cast them out of the house, at a price in legitimacy and the kin's regard
/// unless the record holds cause, and undo either at the same price; the one struck strikes back through the family's own rules; a
/// child struck out with no cause may sue at the head's death; other houses do it for plain causes.</summary>
public class DisinheritTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    private static Character Add(World w, House h, string id, string name, int age, string sex = "male", Character? parent = null, params string[] natures)
    {
        var c = new Character { Id = id, Name = name, HouseId = h.Id, Age = age, Sex = sex, CareerTrack = "law", CareerRank = 1, Ambition = "amass_a_fortune" };
        if (parent != null) { if (parent.Sex == "female") c.MotherId = parent.Id; else c.FatherId = parent.Id; }
        foreach (var n in natures) c.NatureTraits.Add(n);
        w.Characters[id] = c; h.Members.Add(id); return c;
    }
    private static void Wed(World w, Character a, Character b) { w.Relationship(a.Id, b.Id).Bond = BondType.Marriage; w.Relationship(b.Id, a.Id).Bond = BondType.Marriage; }
    private static int Regard(World w, Character from, Character to) => w.GetRelationship(from.Id, to.Id)?.Disposition ?? 0;

    /// <summary>A small world: the player's House Varro (a head, three grown children, a grandchild), two other houses, and an upright
    /// praetor of a third.</summary>
    private static (World w, House h, Character head, Character eldest, Character second, Character third, CommandContext ctx) Small()
    {
        var w = new World { ProtagonistId = "head", Turn = 10 };
        var h = new House { Id = "h", Name = "House Varro", Legitimacy = 50 }; w.Houses[h.Id] = h;
        var o = new House { Id = "o", Name = "House Oriel", Legitimacy = 40 }; w.Houses[o.Id] = o;
        var b = new House { Id = "b", Name = "House Bench", Legitimacy = 40 }; w.Houses[b.Id] = b;
        var head = Add(w, h, "head", "Marcus Varro", 60);
        var eldest = Add(w, h, "eldest", "Corvin Varro", 34, "male", head);
        var second = Add(w, h, "second", "Livia Varro", 30, "female", head);
        var third = Add(w, h, "third", "Tiber Varro", 26, "male", head);
        Add(w, h, "grandchild", "Pia Varro", 6, "female", eldest);
        Add(w, o, "oriel", "Sabina Oriel", 50, "female");
        var judge = Add(w, b, "judge", "Quintus Bench", 50, "male", null, "Just"); judge.Ambition = "";
        w.Magistrates.Add(new OfficeHolding { OfficeId = LadderSystem.Praetor, HouseId = b.Id, CharacterId = judge.Id, SinceTurn = 1 });
        LegitimacyBook.Open(w);
        return (w, h, head, eldest, second, third, new CommandContext(w, TestContent.Rng()));
    }
    private static string? Try(CommandContext ctx, string actor, string what, string who) => new DisownCommand(actor, what, who).Refusal(ctx);
    private static void Do(CommandContext ctx, string actor, string what, string who) { var c = new DisownCommand(actor, what, who); Assert.Null(c.Refusal(ctx)); c.Execute(ctx); }

    [Fact]
    public void Striking_out_is_refused_for_anyone_but_a_grown_relative_of_the_blood_who_is_not_the_named_heir()
    {
        var (w, h, head, eldest, second, third, ctx) = Small();
        Assert.Equal("only the head of a house decides who stands in its line", Try(ctx, eldest.Id, DisownCommand.Strike, second.Id));
        Assert.Equal("you cannot strike yourself from your own line", Try(ctx, head.Id, DisownCommand.Strike, head.Id));
        Assert.Contains("is a child: only someone grown can be struck from the line", Try(ctx, head.Id, DisownCommand.Strike, "grandchild"));
        Assert.Equal("they are not of your house", Try(ctx, head.Id, DisownCommand.Strike, "oriel"));
        var ward = Add(w, h, "ward", "Aulus Oriel", 20); ward.BornHouseId = "o";
        Assert.Contains("is not of the blood of the house", Try(ctx, head.Id, DisownCommand.Strike, ward.Id));
        Assert.Contains("is not of the blood of the house", Try(ctx, head.Id, DisownCommand.CastOut, ward.Id));
        h.NamedHeirId = third.Id;
        Assert.Equal($"{third.Name} is the heir you named: name another, or leave it to the law, first", Try(ctx, head.Id, DisownCommand.Strike, third.Id));
        Assert.Equal($"{third.Name} is the heir you named: name another, or leave it to the law, first", Try(ctx, head.Id, DisownCommand.CastOut, third.Id));
        h.NamedHeirId = "";
        h.Legitimacy = 2;
        Assert.Equal("striking them out would cost 3 legitimacy and the house has 2", Try(ctx, head.Id, DisownCommand.Strike, eldest.Id));
        Assert.Equal("casting them out would cost 5 legitimacy and the house has 2", Try(ctx, head.Id, DisownCommand.CastOut, eldest.Id));
        h.Legitimacy = 50;
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id);
        Assert.Equal($"{eldest.Name} is struck from the line already", Try(ctx, head.Id, DisownCommand.Strike, eldest.Id));
        Assert.Equal($"{second.Name} stands in the line", Try(ctx, head.Id, DisownCommand.Restore, second.Id));
        Assert.Equal($"{second.Name} was not cast out of your house", Try(ctx, head.Id, DisownCommand.TakeBack, second.Id));
        Assert.Equal("no such thing", Try(ctx, head.Id, "disown", second.Id));
        // the last who could follow cannot be struck or cast out
        Do(ctx, head.Id, DisownCommand.Strike, second.Id); w.Characters["grandchild"].Alive = false; ward.Alive = false;
        Assert.Equal($"there is nobody else to stand in the line: struck out, {third.Name} would still be your heir", Try(ctx, head.Id, DisownCommand.Strike, third.Id));
        foreach (var id in new[] { eldest.Id, second.Id }) { w.Characters[id].Alive = false; }
        Assert.Equal($"{third.Name} is the only one who could follow you: cast out, the house would have nobody", Try(ctx, head.Id, DisownCommand.CastOut, third.Id));
    }

    [Fact]
    public void Struck_out_with_no_cause_costs_legitimacy_and_the_kins_regard_and_they_stand_last_in_the_line()
    {
        var (w, h, head, eldest, second, third, ctx) = Small();
        Assert.Equal(eldest.Id, DynastySystem.FindHeir(w, head)!.Id);
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id);
        Assert.Equal(47, h.Legitimacy); Assert.Equal(-3, h.LegitimacyRecord.Totals[LegitimacyBook.Family]);
        Assert.Contains(h.LegitimacyRecord.Changes, x => x.Line.Contains("struck from the line with no cause"));
        Assert.Equal(-DisownSystem.StrikeKin, Regard(w, second, head)); Assert.Equal(-DisownSystem.StrikeKin, Regard(w, third, head));
        Assert.Equal(-DisownSystem.StrikeSelf, Regard(w, eldest, head));
        Assert.True(DisownSystem.IsStruck(eldest)); Assert.Equal(eldest.Id, h.Members.First(id => id == eldest.Id));   // still of the house
        var line = DynastySystem.Line(w, head); Assert.Equal(second.Id, line[0].Id); Assert.Equal(eldest.Id, line[^1].Id);
        Assert.Equal("grandchild", line[^2].Id);   // their own child stands in the line still
        // the record
        var d = Assert.Single(w.Disownings); Assert.Equal(DisownSystem.Struck, d.Kind); Assert.Equal("", d.Cause); Assert.Equal(3, d.Legitimacy); Assert.Equal(5, d.KinRegard); Assert.True(d.Stands);
        // the grievance: they want their place back, which makes them wilful, and nobody can name them heir
        Assert.Equal("seize_the_throne", eldest.Ambition); Assert.Equal("amass_a_fortune", d.WasAmbition); Assert.True(CourtshipSystem.Wilful(eldest));
        Assert.Equal($"{eldest.Name} is struck from the line: restore them to it first", AmbitionSystem.NameHeirRefusal(w, head, eldest));
        Assert.Contains(second.Name, DisownSystem.StrikeSays(w, second));
        Assert.Contains("no cause on the record", DisownSystem.StrikeSays(w, second));
        Assert.True(w.HasFlag(DisownSystem.StruckFlag));
        Assert.Contains(eldest.Life!.Deeds, x => x.Text.Contains("struck from the line") && x.Kind == "failure");
    }

    [Fact]
    public void With_cause_on_the_record_it_costs_nothing_but_their_own_regard()
    {
        // each thing the record holds is a cause
        var (w, h, head, eldest, _, _, _) = Small();
        Assert.Null(DisownSystem.Cause(w, eldest));
        eldest.Flags.Add(FamilySystem.DefiantFlag); Assert.Equal("have grown defiant of the head", DisownSystem.Cause(w, eldest)); eldest.Flags.Remove(FamilySystem.DefiantFlag);
        w.WorldCounters[DisownSystem.DefiedKey(eldest.Id)] = 4; Assert.Equal("defied the head in year 4", DisownSystem.Cause(w, eldest));
        w.Turn = 4 + DisownSystem.CauseYears + 1; Assert.Null(DisownSystem.Cause(w, eldest)); w.Turn = 10;   // it does not count for ever
        w.WorldCounters.Remove(DisownSystem.DefiedKey(eldest.Id));
        eldest.Flags.Add(CourtshipSystem.ElopedChar); Assert.Equal("married without the head's leave", DisownSystem.Cause(w, eldest)); eldest.Flags.Remove(CourtshipSystem.ElopedChar);
        w.WorldCounters[DisownSystem.WedKey(eldest.Id)] = 8; Assert.Equal("married without the head's leave in year 8", DisownSystem.Cause(w, eldest)); w.WorldCounters.Remove(DisownSystem.WedKey(eldest.Id));
        eldest.Flags.Add(FamilySystem.DefectedFlag); Assert.Equal("once left the house for a rival", DisownSystem.Cause(w, eldest)); eldest.Flags.Remove(FamilySystem.DefectedFlag);
        eldest.Flags.Add(FeudSystem.RefusedFieldChar); Assert.Contains("did not come", DisownSystem.Cause(w, eldest)); eldest.Flags.Remove(FeudSystem.RefusedFieldChar);
        w.WorldCounters[LadderSystem.BarredKey(eldest.Id)] = 9 + LadderSystem.BarYears; Assert.Equal("were put out of office for what they did in it, in year 9", DisownSystem.Cause(w, eldest));
        // a failure is not a cause: losing an election is no shame on the house
        w.WorldCounters.Remove(LadderSystem.BarredKey(eldest.Id)); AmbitionSystem.Setback(w, eldest, "Stood for quaestor and was not chosen."); Assert.Null(DisownSystem.Cause(w, eldest));

        var (w2, h2, head2, eldest2, second2, _, ctx2) = Small(); eldest2.Flags.Add(FamilySystem.DefiantFlag);
        Assert.Contains("The record holds cause", DisownSystem.StrikeSays(w2, eldest2));
        Do(ctx2, head2.Id, DisownCommand.Strike, eldest2.Id);
        Assert.Equal(50, h2.Legitimacy); Assert.Equal(0, Regard(w2, second2, head2)); Assert.Equal(-DisownSystem.StrikeSelfWithCause, Regard(w2, eldest2, head2));
        var d = Assert.Single(w2.Disownings); Assert.Equal("have grown defiant of the head", d.Cause); Assert.Equal(0, d.Legitimacy);
        // and undoing it costs the nothing it cost
        Do(ctx2, head2.Id, DisownCommand.Restore, eldest2.Id); Assert.Equal(50, h2.Legitimacy); Assert.Equal(0, Regard(w2, second2, head2));
    }

    [Fact]
    public void Restored_to_the_line_at_the_price_it_cost_and_they_want_what_they_wanted()
    {
        var (w, h, head, eldest, second, _, ctx) = Small();
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id);
        Assert.Contains("3 legitimacy", DisownSystem.RestoreSays(w, eldest));
        h.Legitimacy = 2; Assert.Equal("restoring them, at the price striking them out cost, would cost 3 legitimacy and the house has 2", Try(ctx, head.Id, DisownCommand.Restore, eldest.Id)); h.Legitimacy = 47;
        Assert.Equal("only the head of a house decides who stands in its line", Try(ctx, second.Id, DisownCommand.Restore, eldest.Id));
        Do(ctx, head.Id, DisownCommand.Restore, eldest.Id);
        Assert.Equal(44, h.Legitimacy); Assert.Equal(-2 * DisownSystem.StrikeKin, Regard(w, second, head));
        Assert.Equal(-DisownSystem.StrikeSelf + DisownSystem.RestoreSelf, Regard(w, eldest, head));
        Assert.False(DisownSystem.IsStruck(eldest)); Assert.Equal(eldest.Id, DynastySystem.FindHeir(w, head)!.Id); Assert.Equal("amass_a_fortune", eldest.Ambition);
        var d = Assert.Single(w.Disownings); Assert.False(d.Stands); Assert.Equal("restored", d.Ended);
        Assert.Equal(1, w.Counter(DisownSystem.RestoredKey));
    }

    [Fact]
    public void A_loyal_relative_takes_it_quietly()
    {
        var (w, _, head, eldest, _, _, ctx) = Small(); eldest.NatureTraits.Add("Loyal");
        Assert.Contains("takes it quietly", DisownSystem.StrikeSays(w, eldest));
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id);
        Assert.Equal("amass_a_fortune", eldest.Ambition); Assert.False(CourtshipSystem.Wilful(eldest));
        // nor does anyone of the house move when the family has no wills of its own
        var (w2, _, head2, eldest2, _, _, ctx2) = Small(); w2.FamilyAutonomy = 0;
        Do(ctx2, head2.Id, DisownCommand.Strike, eldest2.Id); Assert.Equal("amass_a_fortune", eldest2.Ambition);
    }

    [Fact]
    public void One_struck_out_strikes_back_through_the_familys_own_rules()
    {
        var (w, h, head, eldest, _, _, ctx) = Small(); var rival = w.Char("oriel")!; w.Relationship(rival.Id, head.Id).Disposition = -40;
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id);   // they think -25 of the head now, and want the seat
        new FamilySystem().Tick(w);
        Assert.Contains(FamilySystem.DefiantFlag, eldest.Flags);   // wilful and ill-disposed: they grow defiant
        Assert.True(Regard(w, eldest, rival) > 0);   // and warm to the house that thinks worst of the head
        DisownSystem.Tick(w); Assert.Equal(w.Turn, w.Counter(DisownSystem.DefiedKey(eldest.Id)));   // the defiance goes on the record
        // low enough, the family's own rule takes them to the rival's house
        w.Relationship(eldest.Id, head.Id).Disposition = -70; new FamilySystem().Tick(w);
        Assert.Equal(rival.HouseId, eldest.HouseId); Assert.Contains(FamilySystem.DefectedFlag, eldest.Flags);
        DisownSystem.Tick(w);
        Assert.False(DisownSystem.IsStruck(eldest)); Assert.Equal("left", w.Disownings.First(d => d.HouseId == h.Id).Ended);   // struck from a line they are no longer of
    }

    [Fact]
    public void Cast_out_they_go_to_a_spouses_house_a_patrons_a_parents_or_alone_and_take_their_purse()
    {
        // to a husband or wife's house
        var (w, h, head, eldest, second, _, ctx) = Small(); var oriel = w.Char("oriel")!; Wed(w, eldest, oriel); eldest.Purse = 12;
        var fleet = new Fleet { Id = "f", Name = "The Varro Guard", OwnerId = h.Id, AdmiralId = eldest.Id }; w.Fleets[fleet.Id] = fleet;
        var sword = new Heirloom { Id = "sword", Name = "the Varro sword", Kind = "sword", HolderId = eldest.Id, OfHouseId = h.Id, Value = 10 }; var seal = new Heirloom { Id = "seal", Name = "the Varro seal", Kind = "signet", HolderId = eldest.Id, OfHouseId = h.Id, Value = 10, Entailed = true };
        w.Heirlooms.Add(sword); w.Heirlooms.Add(seal); h.Duties.Add(new Duty { Role = "steward", HolderId = eldest.Id });
        Assert.Contains("They go to House Oriel, their wife's house", DisownSystem.CastOutSays(w, eldest));
        Do(ctx, head.Id, DisownCommand.CastOut, eldest.Id);
        Assert.Equal("o", eldest.HouseId); Assert.DoesNotContain(eldest.Id, h.Members); Assert.Contains(eldest.Id, w.House("o")!.Members);
        Assert.Equal(h.Id, eldest.BornHouseId); Assert.True(HouseholdSystem.IsFound(eldest));   // living there as one taken in
        Assert.Equal(12, eldest.Purse); Assert.Equal(eldest.Id, sword.HolderId); Assert.Equal(head.Id, seal.HolderId);   // the purse and their own things go with them; the entailed stays
        Assert.Equal("", fleet.AdmiralId); Assert.Empty(h.Duties);
        Assert.Equal(45, h.Legitimacy); Assert.Equal(-DisownSystem.CastKin, Regard(w, second, head)); Assert.Equal(-DisownSystem.CastSelf, Regard(w, eldest, head));
        Assert.Equal("outshine_all_rivals", eldest.Ambition); Assert.Contains(DisownSystem.CastOutChar, eldest.Flags);
        var d = Assert.Single(w.Disownings); Assert.Equal(DisownSystem.BySpouse, d.WentHow); Assert.Equal("o", d.WentTo);
        Assert.DoesNotContain(DynastySystem.Line(w, head), c => c.Id == eldest.Id);
        Assert.True(w.HasFlag(DisownSystem.CastFlag));

        // to a patron's
        var (w2, h2, head2, eldest2, _, _, ctx2) = Small(); eldest2.PatronId = "judge";
        Do(ctx2, head2.Id, DisownCommand.CastOut, eldest2.Id); Assert.Equal("b", eldest2.HouseId); Assert.Equal(DisownSystem.ByPatron, w2.Disownings[0].WentHow);

        // to a parent's people
        var (w3, h3, head3, eldest3, _, _, ctx3) = Small(); var mother = Add(w3, w3.House("o")!, "mother", "Aemilia Oriel", 58, "female"); eldest3.MotherId = mother.Id;
        Do(ctx3, head3.Id, DisownCommand.CastOut, eldest3.Id); Assert.Equal("o", eldest3.HouseId); Assert.Equal(DisownSystem.ByParent, w3.Disownings[0].WentHow);

        // alone: the first of a new name, whose treasury is their purse
        var (w4, h4, head4, eldest4, _, _, ctx4) = Small(); eldest4.Purse = 9;
        Assert.Contains("found a name of their own", DisownSystem.CastOutSays(w4, eldest4));
        Do(ctx4, head4.Id, DisownCommand.CastOut, eldest4.Id);
        var mine = w4.House(eldest4.HouseId)!; Assert.Equal("House Corvin", mine.Name); Assert.Equal("landless", mine.Title); Assert.Equal(9, mine.Treasury.Credits); Assert.Equal(0, eldest4.Purse);
        Assert.Equal(eldest4.Id, mine.HeadId); Assert.Equal("", eldest4.BornHouseId); Assert.Equal(DisownSystem.Alone, w4.Disownings[0].WentHow); Assert.Equal(1, w4.Counter(DisownSystem.NewNamesKey));
        HouseSystem.Tick(w4); Assert.NotNull(w4.House(mine.Id));   // a name with someone in it stays on the rolls
        // one struck out first and then cast out: the striking ends, the casting out stands
        var (w5, _, head5, eldest5, _, _, ctx5) = Small();
        Do(ctx5, head5.Id, DisownCommand.Strike, eldest5.Id); Do(ctx5, head5.Id, DisownCommand.CastOut, eldest5.Id);
        Assert.False(DisownSystem.IsStruck(eldest5)); Assert.Equal("cast_out", w5.Disownings[0].Ended); Assert.True(w5.Disownings[1].Stands);
    }

    [Fact]
    public void Taken_back_at_the_price_it_cost_if_they_will_come()
    {
        var (w, h, head, eldest, second, _, ctx) = Small(); eldest.Purse = 9;
        Do(ctx, head.Id, DisownCommand.CastOut, eldest.Id); var founded = w.House(eldest.HouseId)!;
        w.Relationship(eldest.Id, head.Id).Disposition = -61;
        Assert.Equal($"{eldest.Name} will not come back: they think -61 of you, and it would take -60 or better", Try(ctx, head.Id, DisownCommand.TakeBack, eldest.Id));
        w.Relationship(eldest.Id, head.Id).Disposition = -30;
        var son = Add(w, founded, "newson", "Gaius Corvin", 2, "male", eldest);
        Assert.Equal($"{eldest.Name} leads {founded.Name} now, and will not leave its people", Try(ctx, head.Id, DisownCommand.TakeBack, eldest.Id));
        son.Alive = false;
        Assert.Contains("5 legitimacy", DisownSystem.TakeBackSays(w, eldest));
        Do(ctx, head.Id, DisownCommand.TakeBack, eldest.Id);
        Assert.Equal(h.Id, eldest.HouseId); Assert.Contains(eldest.Id, h.Members); Assert.DoesNotContain(eldest.Id, founded.Members); Assert.Equal("", eldest.BornHouseId);
        Assert.Equal(9, eldest.Purse); Assert.Equal(0, founded.Treasury.Credits);   // the name was only them: what it had is theirs again
        Assert.Equal(40, h.Legitimacy); Assert.Equal(-2 * DisownSystem.CastKin, Regard(w, second, head)); Assert.Equal(-30 + DisownSystem.TakeBackSelf, Regard(w, eldest, head));
        Assert.Equal("amass_a_fortune", eldest.Ambition); Assert.DoesNotContain(DisownSystem.CastOutChar, eldest.Flags); Assert.Equal("taken_back", w.Disownings[0].Ended);
        Assert.Contains(eldest.Id, DynastySystem.Line(w, head).Select(c => c.Id));
        HouseSystem.Tick(w); Assert.Null(w.House(founded.Id));   // and the empty name passes out of the rolls
    }

    /// <summary>The head with a purse and an heirloom strikes the eldest out with no cause, and dies.</summary>
    private static (World w, House h, Character heir, Character child, WillSuit suit) Will(string judgeNature = "Just")
    {
        var (w, h, head, eldest, second, _, ctx) = Small(); var judge = w.Char("judge")!; judge.NatureTraits.Clear(); if (judgeNature.Length > 0) judge.NatureTraits.Add(judgeNature);
        head.Purse = 40; second.Purse = 5;
        w.Heirlooms.Add(new Heirloom { Id = "library", Name = "the Varro library", Kind = "library", HolderId = head.Id, OfHouseId = h.Id, Value = 30 });
        w.Heirlooms.Add(new Heirloom { Id = "seal", Name = "the Varro seal", Kind = "signet", HolderId = head.Id, OfHouseId = h.Id, Value = 50, Entailed = true });
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id);
        DynastySystem.Die(w, head.Id, "of age");
        Assert.Equal(second.Id, w.ProtagonistId);
        return (w, h, second, eldest, Assert.Single(w.WillSuits));
    }
    private static void NextYear(World w) { w.Turn++; DisownSystem.Tick(w); }

    [Fact]
    public void At_the_heads_death_a_child_struck_out_with_no_cause_sues_that_the_will_was_undutiful_and_an_upright_praetor_finds_for_them()
    {
        var (w, h, heir, child, s) = Will();
        Assert.Equal(child.Id, s.PlaintiffId); Assert.Equal("judge", s.JudgeId); Assert.Equal(10, s.Share); Assert.Equal(new[] { "library" }, s.Items);   // a quarter of 4,000 credits, and the library; the seal is entailed
        Assert.True(w.HasFlag(DisownSystem.SuitFlag)); Assert.Contains(w.News, n => n.Text.Contains("undutiful") && n.Target == "senate:ladder");
        Assert.Contains("1,000 credits", DisownSystem.AsksSays(w, s)); Assert.Contains("the Varro library", DisownSystem.AsksSays(w, s));
        // it is on the docket with the house's other suits
        var line = Assert.Single(Docket.Of(w, Content.Offices, h.Id)); Assert.Equal(Docket.Will, line.Kind); Assert.Equal(s.Id, line.Id); Assert.Contains("undutiful", line.Title); Assert.Equal(heir.Name, line.Pleader);
        Assert.Equal("at the end of next year", line.Heard);
        Assert.Equal((true, "upright, and will go by the record, which shows no cause"), DisownSystem.Verdict(w, s, w.Char("judge")!));
        DisownSystem.Tick(w); Assert.Equal("", s.Outcome);   // not heard the year it is brought
        int purse = heir.Purse, legitimacy = h.Legitimacy, regard = Regard(w, child, heir);
        NextYear(w);
        Assert.Equal(DisownSystem.Undutiful, s.Outcome); Assert.Equal(purse - 10, heir.Purse); Assert.Equal(10, child.Purse);
        Assert.Equal(child.Id, w.Heirlooms.First(i => i.Id == "library").HolderId); Assert.Equal(heir.Id, w.Heirlooms.First(i => i.Id == "seal").HolderId);
        Assert.False(DisownSystem.IsStruck(child)); Assert.Equal("found_undutiful", w.Disownings[0].Ended); Assert.Equal("amass_a_fortune", child.Ambition);   // set aside
        Assert.Equal(legitimacy - DisownSystem.UndutifulLegitimacy, h.Legitimacy); Assert.Equal(regard + DisownSystem.WonRegard, Regard(w, child, heir));
        Assert.True(w.HasFlag(DisownSystem.UndutifulFlag)); Assert.Equal(1, w.Counter(DisownSystem.SuitsWonKey));
        Assert.Empty(Docket.Of(w, Content.Offices, h.Id)); Assert.Equal(s.Id, Assert.Single(DisownSystem.DecidedOn(w, h.Id)).Id);
        w.Turn += DisownSystem.KeptYears + 1; DisownSystem.Tick(w); Assert.Empty(w.WillSuits);   // kept a while for the screen, then gone
    }

    [Fact]
    public void A_praetor_bought_by_the_house_or_its_friend_lets_the_will_stand_and_the_rest_go_by_who_pleads()
    {
        var (w, h, heir, child, s) = Will("Ruthless"); var judge = w.Char("judge")!;
        Assert.False(DisownSystem.Verdict(w, s, judge).undutiful);   // for sale, and the house has more than the child
        child.Purse = 500; Assert.True(DisownSystem.Verdict(w, s, judge).undutiful); child.Purse = 0;
        judge.NatureTraits.Clear();
        w.WorldFlags.Add($"bought:{judge.Id}:{h.Id}"); Assert.Equal((false, "bought by the house, and will find for it"), DisownSystem.Verdict(w, s, judge)); w.WorldFlags.Remove($"bought:{judge.Id}:{h.Id}");
        w.Relationship(judge.Id, heir.Id).Disposition = 40; Assert.Equal((false, "a friend of the house, and will find for it"), DisownSystem.Verdict(w, s, judge)); w.Relationship(judge.Id, heir.Id).Disposition = 0;
        Assert.StartsWith("neither upright", DisownSystem.Verdict(w, s, judge).why);
        child.Skills["law"] = 0; child.Skills["oratory"] = 0;
        Assert.Equal(DisownSystem.PleadBase - 4 * DisownSystem.PleadAgainst, DisownSystem.Chance(w, s, 4)); Assert.Equal(95, DisownSystem.Chance(w, s, -20)); Assert.Equal(5, DisownSystem.Chance(w, s, 40));
        // a will that stands
        w.Relationship(judge.Id, heir.Id).Disposition = 40; int regard = Regard(w, child, heir), purse = heir.Purse;
        NextYear(w);
        Assert.Equal(DisownSystem.Upheld, s.Outcome); Assert.Equal(purse, heir.Purse); Assert.True(DisownSystem.IsStruck(child)); Assert.Equal(regard - DisownSystem.LostRegard, Regard(w, child, heir));
        Assert.True(w.HasFlag(DisownSystem.UpheldFlag)); Assert.Contains("let the will stand", s.How);
    }

    [Fact]
    public void The_suit_can_be_settled_out_of_court_or_answered_by_someone_sent()
    {
        var (w, h, heir, child, s) = Will(""); var ctx = new CommandContext(w, TestContent.Rng()); w.PoolsFor(heir.Id).Influence = 5;
        var third = w.Char("third")!; third.Skills["law"] = 4; third.Skills["oratory"] = 3;
        // answering it
        Assert.Equal("they are the one who brings it", new WillSuitCommand(heir.Id, WillSuitCommand.Plead, s.Id, child.Id).Refusal(ctx));
        Assert.Equal("there is no one of age in the house to send", new WillSuitCommand(heir.Id, WillSuitCommand.Plead, s.Id, "grandchild").Refusal(ctx));
        Assert.Equal("only the head of a house sends its pleader", new WillSuitCommand(third.Id, WillSuitCommand.Plead, s.Id, third.Id).Refusal(ctx));
        Assert.Equal("no such suit is waiting", new WillSuitCommand(heir.Id, WillSuitCommand.Plead, "none", third.Id).Refusal(ctx));
        Assert.Contains("in a hundred that the will is found undutiful", DisownSystem.PleadSays(w, s, third));
        var plead = new WillSuitCommand(heir.Id, WillSuitCommand.Plead, s.Id, third.Id); Assert.Null(plead.Refusal(ctx)); plead.Execute(ctx);
        Assert.Equal(third.Id, s.PleaderId); Assert.Equal(4, w.PoolsFor(heir.Id).Influence); Assert.Equal(third.Id, DisownSystem.AnswersFor(w, s)!.Id); Assert.Equal(7, DisownSystem.DefenceOf(w, s));
        Assert.Equal("they are already to answer it", new WillSuitCommand(heir.Id, WillSuitCommand.Plead, s.Id, third.Id).Refusal(ctx));
        Assert.Equal(third.Name, Assert.Single(Docket.Of(w, Content.Offices, h.Id)).Pleader);
        w.PoolsFor(heir.Id).Influence = 0; Assert.NotNull(new WillSuitCommand(heir.Id, WillSuitCommand.Plead, s.Id, heir.Id).Refusal(ctx));
        // coming to terms
        heir.Purse = 3; h.Treasury.Credits = 4;
        Assert.Equal("terms are the 1,000 credits the suit asks, and your purse and the treasury hold 700 credits", new WillSuitCommand(heir.Id, WillSuitCommand.Settle, s.Id).Refusal(ctx));
        h.Treasury.Credits = 20; int regard = Regard(w, child, heir);
        var settle = new WillSuitCommand(heir.Id, WillSuitCommand.Settle, s.Id); Assert.Null(settle.Refusal(ctx)); settle.Execute(ctx);
        Assert.Equal(DisownSystem.Settled, s.Outcome); Assert.Equal(0, heir.Purse); Assert.Equal(13, h.Treasury.Credits); Assert.Equal(10, child.Purse);
        Assert.Equal(child.Id, w.Heirlooms.First(i => i.Id == "library").HolderId); Assert.True(DisownSystem.IsStruck(child));   // the striking stands
        Assert.Equal(regard + DisownSystem.SettledRegard, Regard(w, child, heir));
        Assert.Equal("no such suit is waiting", new WillSuitCommand(heir.Id, WillSuitCommand.Settle, s.Id).Refusal(ctx));
        NextYear(w); Assert.Equal(DisownSystem.Settled, s.Outcome);   // it is not heard as well
    }

    [Fact]
    public void A_child_struck_out_with_cause_or_a_loyal_and_fond_one_does_not_sue_and_one_with_no_praetor_to_hear_it_cannot()
    {
        var (w, _, head, eldest, second, _, ctx) = Small(); eldest.Flags.Add(FeudSystem.RefusedFieldChar);
        Do(ctx, head.Id, DisownCommand.Strike, eldest.Id); DynastySystem.Die(w, head.Id); Assert.Empty(w.WillSuits);
        var (w2, _, head2, eldest2, second2, _, ctx2) = Small(); eldest2.NatureTraits.Add("Loyal"); w2.Relationship(eldest2.Id, second2.Id).Disposition = 60;   // 40 once passed over for them
        Do(ctx2, head2.Id, DisownCommand.Strike, eldest2.Id); DynastySystem.Die(w2, head2.Id); Assert.Empty(w2.WillSuits);
        var (w3, _, head3, eldest3, _, _, ctx3) = Small(); w3.Magistrates.Clear();
        Do(ctx3, head3.Id, DisownCommand.Strike, eldest3.Id); DynastySystem.Die(w3, head3.Id); Assert.Empty(w3.WillSuits);
        Assert.Contains(w3.History, x => x.Text.Contains("no praetor of another house sits to hear it"));
        // a head who lays the house down in life leaves no will to sue over
        var (w4, _, head4, eldest4, second4, _, ctx4) = Small();
        Do(ctx4, head4.Id, DisownCommand.Strike, eldest4.Id); DynastySystem.HandOver(w4, second4); Assert.Empty(w4.WillSuits);
        // a suit whose child dies before it is heard lapses
        var (w5, h5, _, child5, s5) = Will(); child5.Alive = false; NextYear(w5); Assert.Equal(DisownSystem.Lapsed, s5.Outcome);
        // and one whose praetor has gone, with no other of neither house, lapses too
        var (w6, _, _, _, s6) = Will(); w6.Magistrates.Clear(); NextYear(w6); Assert.Equal(DisownSystem.Lapsed, s6.Outcome); Assert.Contains("no praetor", s6.How);
    }

    [Fact]
    public void Other_houses_strike_out_or_cast_out_a_relative_for_plain_causes_once_and_by_the_same_rule_every_time()
    {
        World Build()
        {
            var (w, _, _, _, _, _, _) = Small();
            for (int i = 0; i < 8; i++)
            {
                var house = new House { Id = "n" + i, Name = "House N" + i, Legitimacy = 30 }; w.Houses[house.Id] = house;
                var hd = Add(w, house, "nhead" + i, "Head N" + i, 60, "male", null, i % 2 == 0 ? "Proud" : "Diligent"); house.HeadId = hd.Id;
                var kin = Add(w, house, "nkin" + i, "Kin N" + i, 30, "male", hd); Add(w, house, "nother" + i, "Other N" + i, 28, "female", hd);
                w.WorldCounters[LadderSystem.BarredKey(kin.Id)] = w.Turn + LadderSystem.BarYears;   // put out of office this year
            }
            return w;
        }
        var w = Build(); DisownSystem.Tick(w);
        var acts = w.Disownings.ToList(); Assert.NotEmpty(acts); Assert.True(acts.Count < 8);   // sparingly: some let it lie
        foreach (var d in acts)
        {
            var head = w.Char(d.HeadId)!; var kin = w.Char(d.PersonId)!; Assert.StartsWith("nkin", kin.Id);
            Assert.Equal(head.NatureTraits.Contains("Proud") ? DisownSystem.CastOut : DisownSystem.Struck, d.Kind);
            Assert.Equal("were put out of office for what they did in it, in year 10", d.Cause); Assert.Equal(0, d.Legitimacy);
            if (d.Kind == DisownSystem.Struck) { Assert.True(DisownSystem.IsStruck(kin)); Assert.Equal("seize_the_throne", kin.Ambition); }
            else Assert.NotEqual(d.HouseId, kin.HouseId);
        }
        Assert.Contains(acts, d => d.Kind == DisownSystem.CastOut); Assert.Contains(acts, d => d.Kind == DisownSystem.Struck);
        Assert.Equal(acts.Count, w.Counter(DisownSystem.StruckByOthersKey) + w.Counter(DisownSystem.CastByOthersKey));
        // once: the next years do nothing more for the same cause, and those let lie stay let lie
        for (int y = 0; y < 4; y++) NextYear(w); Assert.Equal(acts.Count, w.Disownings.Count);
        // the same world gives the same acts
        var again = Build(); DisownSystem.Tick(again);
        Assert.Equal(acts.Select(d => d.Id + d.Kind), again.Disownings.Select(d => d.Id + d.Kind));
        // one struck from the line leads only when nobody else is left (docs/HOUSEHOLD.md, "Who leads another house")
        var struck = acts.First(d => d.Kind == DisownSystem.Struck); var their = w.House(struck.HouseId)!;
        w.Char(struck.HeadId)!.Alive = false; Assert.NotEqual(struck.PersonId, HeadSystem.Eldest(w, their)!.Id);
        foreach (var c in w.Characters.Values.Where(c => c.HouseId == their.Id && c.Id != struck.PersonId)) c.Alive = false;
        Assert.Equal(struck.PersonId, HeadSystem.Eldest(w, their)!.Id);
        // a head of another house does not cast one of theirs into the player's house unless it is to a husband or wife there
        var (w2, h2, _, _, _, _, _) = Small(); var nh = new House { Id = "n", Name = "House N", Legitimacy = 30 }; w2.Houses[nh.Id] = nh;
        var hd2 = Add(w2, nh, "nhead", "Head N", 60, "male", null, "Proud"); var kin2 = Add(w2, nh, "nkin", "Kin N", 30, "male"); kin2.MotherId = "second";
        Assert.Equal((null, DisownSystem.Alone), DisownSystem.WhereTo(w2, nh, kin2));
        Wed(w2, kin2, w2.Char("third")!); Assert.Equal(h2.Id, DisownSystem.WhereTo(w2, nh, kin2).to?.Id);
    }

    [Fact]
    public void A_cause_that_passes_with_the_year_is_written_down_where_it_lasts()
    {
        var (w, _, _, eldest, second, third, _) = Small();
        eldest.Flags.Add(CourtshipSystem.ElopedChar); second.Flags.Add(FeudSystem.RefusedFieldChar); third.Flags.Add(FamilySystem.DefectedFlag);
        DisownSystem.Tick(w);
        Assert.Equal(10, w.Counter(DisownSystem.WedKey(eldest.Id))); Assert.Equal(10, w.Counter(DisownSystem.ShamedKey(second.Id))); Assert.Equal(10, w.Counter(DisownSystem.DefectedKey(third.Id)));
        eldest.Flags.Remove(CourtshipSystem.ElopedChar); w.Turn = 14; DisownSystem.Tick(w);
        Assert.Equal("married without the head's leave in year 10", DisownSystem.Cause(w, eldest)); Assert.Equal(10, w.Counter(DisownSystem.WedKey(eldest.Id)));
        Assert.Null(DisownSystem.PlainCause(w, second));   // stale for another house's head to act on
    }

    [Fact]
    public void What_is_struck_out_cast_out_and_sued_over_survives_a_save()
    {
        var (w, h, heir, child, s) = Will(); var ctx = new CommandContext(w, TestContent.Rng());
        Do(ctx, heir.Id, DisownCommand.CastOut, "third");
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w));
        Assert.Equal(w.Disownings.Count, back.Disownings.Count); Assert.Equal(w.Disownings.Select(d => (d.Id, d.Kind, d.Cause, d.Legitimacy, d.KinRegard, d.WentTo, d.WentHow, d.UndoneTurn, d.WasAmbition)), back.Disownings.Select(d => (d.Id, d.Kind, d.Cause, d.Legitimacy, d.KinRegard, d.WentTo, d.WentHow, d.UndoneTurn, d.WasAmbition)));
        var bs = Assert.Single(back.WillSuits); Assert.Equal((s.Id, s.PlaintiffId, s.Share, s.JudgeId, s.DeceasedName), (bs.Id, bs.PlaintiffId, bs.Share, bs.JudgeId, bs.DeceasedName)); Assert.Equal(s.Items, bs.Items);
        Assert.True(DisownSystem.IsStruck(back.Char(child.Id)!)); Assert.Contains(DisownSystem.CastOutChar, back.Char("third")!.Flags);
    }

    [Fact]
    public void A_head_who_strikes_out_and_casts_out_whenever_they_may_plays_on_and_the_line_holds()
    {
        foreach (ulong seed in new ulong[] { 3, 4 })
        {
            var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed); var tc = new TurnController(w, Content);
            var ctx = new CommandContext(w, TestContent.Rng(seed)); int done = 0;
            for (int y = 0; y < 20 && !w.IsDynastyDead; y++)
            {
                foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
                var p = w.Protagonist!; var house = w.House(p.HouseId)!; house.Legitimacy = Math.Max(house.Legitimacy, 20);
                var people = w.Characters.Values.Where(c => c.Alive && c.Id != p.Id && (c.HouseId == house.Id || DisownSystem.Standing(w, c, DisownSystem.CastOut)?.HouseId == house.Id)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
                string[] verbs = { DisownCommand.Strike, DisownCommand.CastOut, DisownCommand.Restore, DisownCommand.TakeBack };
                foreach (var who in people.Skip(y % 3).Take(2))
                    foreach (var v in verbs.Skip(y % 4).Concat(verbs.Take(y % 4)))
                    {
                        var cmd = new DisownCommand(p.Id, v, who.Id); if (cmd.Refusal(ctx) != null) continue;
                        cmd.Execute(ctx); done++; break;
                    }
                foreach (var s in DisownSystem.SuitsOn(w, house.Id).ToList()) { var settle = new WillSuitCommand(p.Id, WillSuitCommand.Settle, s.Id); if (y % 2 == 0 && settle.Refusal(ctx) == null) settle.Execute(ctx); }
                tc.EndTurn();
                // the line is the house's own, with those struck out last, and nobody struck out is of another house
                var line = DynastySystem.Line(w, w.Protagonist!);
                Assert.All(line, c => Assert.Equal(w.Protagonist!.HouseId, c.HouseId));
                Assert.True(line.SkipWhile(c => !DisownSystem.IsStruck(c)).All(DisownSystem.IsStruck), "one struck out stands before someone who is not");
                string mine = w.Protagonist!.HouseId;
                foreach (var d in w.Disownings.Where(d => d.Stands && d.Kind == DisownSystem.Struck && d.HouseId == mine)) Assert.Equal(d.HouseId, w.Char(d.PersonId)!.HouseId);
                foreach (var d in w.Disownings.Where(d => d.Stands && d.Kind == DisownSystem.CastOut && d.HouseId == mine && w.Char(d.PersonId)!.Alive))
                { var c = w.Char(d.PersonId)!; Assert.NotEqual(mine, c.HouseId); Assert.DoesNotContain(c.Id, w.House(mine)!.Members); }
            }
            Assert.True(done > 0, "the head never struck anyone out");
            var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)); Assert.Equal(w.Disownings.Count, back.Disownings.Count);
        }
    }
}
