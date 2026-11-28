using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The year's moments (docs/VISUAL_SYSTEM.md, "The year's moments"): the biggest moments of a life in the player's line, read
/// from what the year recorded, each with who is in it, a kind, one line and its painting; at most three of them a card, a death in
/// the main line first; the year in brief with its worst news first; and the funeral that names whom it buries.</summary>
public class YearMomentsTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());

    /// <summary>The Old Lion in its first year: Aurel, head at 68; Helene his wife; Corvin their son and heir, wed to Ysolde; their
    /// children Tamsin and Pell; Bastien, of the blood with no parents written down.</summary>
    private static (World w, TurnController tc, House house, Character me) Start(ulong seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), seed);
        var tc = new TurnController(w, Content); tc.BeginTurn();
        var me = w.Protagonist!; return (w, tc, w.House(me.HouseId)!, me);
    }
    /// <summary>The year's records stand, and the next year opens: the moments are read for the year just ended.</summary>
    private static void NextYear(World w) { w.Turn++; YearInBrief.Mark(w); }
    private static Character Of(World w, string id) => w.Char(id)!;
    private static House Foe(World w, House mine) => w.Houses.Values.Where(h => h.Id != mine.Id && !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null).OrderBy(h => h.Id, StringComparer.Ordinal).First();
    private static Character Baby(World w, House house, string id, string name, string sex, Character father, Character mother)
    {
        var c = new Character { Id = id, Name = name, HouseId = house.Id, Age = 0, Sex = sex, FatherId = father.Id, MotherId = mother.Id };
        w.Characters[c.Id] = c; house.Members.Add(c.Id); return c;
    }
    /// <summary>The rules for anything a player reads: no em dash, never "(s)", the true minus for a loss.</summary>
    private static void Plain(Moment m)
    {
        foreach (var s in new[] { m.Kicker, m.Title, m.Text, m.Button, m.Line }.Concat(m.Lead.Concat(m.Around).Select(f => f.Says)))
        {
            Assert.DoesNotContain("—", s); Assert.DoesNotContain("(s)", s); Assert.DoesNotMatch(@"\(-\d", s);
        }
        Assert.Contains(m.Scene, SceneArt.Scenes);
        Assert.True(m.Staged || m.Button.Split(' ').Length <= 3, m.Button);   // the seat passing keeps the succession screen's own plate
        Assert.True((m.Kicker + " year XII").Split(' ').Length <= 4, "a kicker in capitals is four words at most with its year: " + m.Kicker);
    }

    [Fact]
    public void The_death_of_the_son_and_heir_is_a_moment_with_the_dead_in_the_large_plate_and_the_family_around()
    {
        var (w, _, house, me) = Start();
        var corvin = Of(w, "corvin_castellan");
        Assert.Equal(corvin.Id, DynastySystem.FindHeir(w, me)!.Id);
        DynastySystem.Die(w, corvin.Id, "in battle");
        Assert.True(w.Deaths.Single(d => d.PersonId == corvin.Id).Heir);   // written down while he still stood in the line
        NextYear(w);
        var m = Assert.Single(YearMoments.For(w, Content), x => x.Kind == YearMoments.Death);
        Assert.Equal("Your son and heir is dead", m.Title);
        Assert.Equal(YearMoments.HeirDies, m.Weight);
        Assert.Equal("funeral", m.Scene);
        Assert.Equal(corvin.Id, Assert.Single(m.Lead).Id);
        Assert.False(Of(w, m.Lead[0].Id).Alive);   // the client draws him in grey with the ribbon
        Assert.Equal("your son · died at 36", m.Lead[0].Says);
        Assert.StartsWith("Corvin Castellan died at 36, in battle. He was your heir, and ", m.Text);
        Assert.Contains(m.Around, f => f.Id == me.Id && f.Says == "you");
        Assert.Contains(m.Around, f => f.Id == "ysolde_castellan" && f.Says == "your son's wife");
        Assert.Contains(m.Around, f => f.Id == "helene_castellan" && f.Says == "your wife");
        Assert.True(m.Around.Count <= 4);
        Assert.Equal("Mourn him", m.Button);
        Plain(m);
    }

    [Fact]
    public void A_wifes_death_says_whose_and_a_grandsons_death_stays_a_line()
    {
        var (w, _, house, me) = Start();
        DynastySystem.Die(w, "helene_castellan");
        DynastySystem.Die(w, "pell_castellan");   // a grandson, not the heir: not of the main line the moments are of
        NextYear(w);
        var moments = YearMoments.For(w, Content);
        var m = Assert.Single(moments, x => x.Kind == YearMoments.Death);
        Assert.Equal("Your wife is dead", m.Title); Assert.Equal(YearMoments.SpouseDies, m.Weight);
        Assert.Equal("helene_castellan", m.Lead[0].Id); Assert.StartsWith("your wife · died at 61", m.Lead[0].Says);
        Assert.Equal("Mourn her", m.Button);
        Assert.Contains(m.Around, f => f.Id == "corvin_castellan" && f.Says == "your son");
        // both deaths are lines of the year in brief, the wife's graver than the grandson's
        var brief = YearInBrief.For(w, Content);
        var wife = Assert.Single(brief, l => l.Text.StartsWith("Helene Castellan died at"));
        var grandson = Assert.Single(brief, l => l.Text.StartsWith("Pell Castellan died at"));
        Assert.Equal(YearMoments.SpouseDies, wife.Grave); Assert.Equal(YearInBrief.GraveDeath, grandson.Grave);
        Plain(m);
    }

    [Fact]
    public void A_brother_the_start_wrote_down_only_as_of_the_blood_is_mourned_as_a_sibling()
    {
        var (w, _, house, me) = Start();
        DynastySystem.Die(w, "bastien_castellan");   // the Lion's younger brother: a blood bond, and no parent written down
        NextYear(w);
        var m = Assert.Single(YearMoments.For(w, Content), x => x.Kind == YearMoments.Death);
        Assert.Equal("Bastien Castellan is dead", m.Title); Assert.Equal(YearMoments.SiblingDies, m.Weight);
        Assert.StartsWith("of your blood · died at", m.Lead[0].Says);
        Plain(m);
    }

    [Fact]
    public void A_birth_in_the_main_line_is_a_moment_with_the_newborn_and_the_parents()
    {
        var (w, _, house, me) = Start();
        var corvin = Of(w, "corvin_castellan"); var ysolde = Of(w, "ysolde_castellan"); var helene = Of(w, "helene_castellan");
        var grand = Baby(w, house, "t_livia", "Livia Castellan", "female", corvin, ysolde);
        var own = Baby(w, house, "t_marcus", "Marcus Castellan", "male", me, helene);
        NextYear(w);
        var births = YearMoments.For(w, Content).Where(x => x.Kind == YearMoments.Birth).ToList();
        var g = Assert.Single(births, x => x.Lead[0].Id == grand.Id);
        Assert.Equal("A granddaughter is born", g.Title); Assert.Equal(YearMoments.GrandchildBorn, g.Weight); Assert.Equal("family", g.Scene);
        Assert.Equal("your granddaughter · newborn", g.Lead[0].Says);
        Assert.Equal(new[] { corvin.Id, ysolde.Id }, g.Around.Select(f => f.Id).ToArray());
        Assert.Equal(new[] { "your son", "your son's wife" }, g.Around.Select(f => f.Says).ToArray());
        Assert.StartsWith("Livia Castellan is born to Corvin Castellan and Ysolde Castellan", g.Text);
        Assert.Equal("Welcome her", g.Button);
        var s = Assert.Single(births, x => x.Lead[0].Id == own.Id);
        Assert.Equal("A son is born", s.Title); Assert.Equal(YearMoments.ChildBorn, s.Weight);
        Assert.Equal("Marcus Castellan is born to you and Helene Castellan, and stands second in the line.", s.Text);   // after his elder brother, before the grandchildren
        Assert.Contains(s.Around, f => f.Id == me.Id && f.Says == "you");
        Plain(g); Plain(s);
    }

    [Fact]
    public void A_daughters_wedding_and_her_coming_of_age_are_moments_of_the_house()
    {
        var (w, _, house, me) = Start();
        var helene = Of(w, "helene_castellan"); var foe = Foe(w, house);
        var daughter = new Character { Id = "t_aurelia", Name = "Aurelia Castellan", HouseId = house.Id, Age = 22, Sex = "female", FatherId = me.Id, MotherId = helene.Id };
        var younger = new Character { Id = "t_vesna", Name = "Vesna Castellan", HouseId = house.Id, Age = DynastySystem.AgeOfMajority, Sex = "female", FatherId = me.Id, MotherId = helene.Id, CareerTrack = "law" };
        foreach (var c in new[] { daughter, younger }) { w.Characters[c.Id] = c; house.Members.Add(c.Id); }
        var groom = w.LivingCharacters().Where(c => c.HouseId != house.Id && !World.Outsider(c.HouseId) && c.Sex == "male" && c.Age >= 18 && w.MarriageOf(c.Id) == null).OrderBy(c => c.Id, StringComparer.Ordinal).First();
        foe = w.House(groom.HouseId)!;
        CourtshipSystem.Wed(w, daughter, groom, CourtshipSystem.Love);
        NextYear(w);
        var moments = YearMoments.For(w, Content);
        var wed = Assert.Single(moments, x => x.Kind == YearMoments.Marriage);
        Assert.Equal("Your daughter is married", wed.Title); Assert.Equal("feast", wed.Scene);
        Assert.Equal(new[] { daughter.Id, groom.Id }, wed.Lead.Select(f => f.Id).ToArray());   // the couple
        Assert.Equal("your daughter", wed.Lead[0].Says);
        Assert.StartsWith($"Aurelia Castellan weds {groom.Name} of {foe.Name}, for love", wed.Text);
        var age = Assert.Single(moments, x => x.Kind == YearMoments.OfAge);
        Assert.Equal("Your daughter comes of age", age.Title); Assert.Equal(younger.Id, age.Lead[0].Id);
        Assert.Equal("Vesna Castellan is sixteen and of age today, and is to go into the Courts.", age.Text);
        // the coming of age had no line in the year in brief: it has one now
        Assert.Contains(YearInBrief.For(w, Content), l => l.PersonId == younger.Id && l.Text.StartsWith("Vesna Castellan came of age") && l.Kind == YearInBrief.Family);
        Plain(wed); Plain(age);
    }

    [Fact]
    public void A_war_declared_a_siege_laid_a_rank_lost_and_a_suit_brought_are_moments_of_the_house()
    {
        var (w, tc, house, me) = Start();
        var foe = Foe(w, house); var foeHead = Economy.HeadOf(w, foe)!;
        tc.Fleets.DeclareWar(w, foe.Id, house.Id);
        var world = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        w.Sieges[world.Id] = new Siege { HoldingId = world.Id, BesiegerId = foe.Id, Turns = 1, Required = 4 };
        var judge = w.LivingCharacters().First(c => c.HouseId != house.Id && c.HouseId != foe.Id && c.Age >= 30);
        w.OfficeWork.Add(new OfficeBusiness { Id = "t_suit", Kind = LadderSystem.Case, Held = true, HouseA = foe.Id, HouseB = house.Id, PlaintiffId = foe.Id, RightId = foe.Id, Turn = w.Turn, Stake = 12, HolderId = judge.Id, Matter = "a boundary" });
        string rankWas = house.Title; Assert.True(Praetoria.Core.Progression.Title.RankOf(rankWas) > 0);
        house.Title = Praetoria.Core.Progression.Title.OfRank(Praetoria.Core.Progression.Title.RankOf(rankWas) - 1);
        NextYear(w);
        var moments = YearMoments.For(w, Content);

        var war = Assert.Single(moments, x => x.Kind == YearMoments.War);
        Assert.Equal($"{foe.Name} declares war", war.Title); Assert.Equal(foeHead.Id, war.Lead[0].Id); Assert.Equal(foe.Id, war.House);
        Assert.Contains("has declared war on your house, with ", war.Text); Assert.Equal("battle", war.Scene);
        var siege = Assert.Single(moments, x => x.Kind == YearMoments.Siege);
        Assert.Equal($"{world.Name} is under siege", siege.Title); Assert.EndsWith("the world falls in 3 years.", siege.Text);
        var rank = Assert.Single(moments, x => x.Kind == YearMoments.RankLost);
        Assert.StartsWith($"Your house held the rank of {Content.Titles.NameOf(rankWas, w.Era)} as the year opened", rank.Text);
        var suit = Assert.Single(moments, x => x.Kind == YearMoments.Suit);
        string matter = LadderSystem.MatterOf(w.OfficeWork.Single(b => b.Id == "t_suit"));
        Assert.Equal($"{foe.Name} sues your house", suit.Title); Assert.Contains(matter, suit.Text); Assert.Contains(judge.Name + ", praetor", suit.Text);
        Assert.Contains(suit.Around, f => f.Id == judge.Id);
        foreach (var m in new[] { war, siege, rank, suit }) { Assert.True(m.Bad); Plain(m); }

        // each is a line of the year in brief, grave by its weight: the siege, the rank and the suit had none before
        var brief = YearInBrief.For(w, Content);
        Assert.Contains(brief, l => l.PersonId == foe.Id && l.Kind == YearInBrief.War && l.Grave == YearMoments.WarDeclared);
        Assert.Contains(brief, l => l.PersonId == world.Id && l.Text.Contains("laid siege to " + world.Name) && l.Grave == YearMoments.Besieged);
        Assert.Contains(brief, l => l.Kind == YearInBrief.Senate && l.Text.StartsWith("Your house fell from the rank of") && l.Grave == YearMoments.RankFalls);
        Assert.Contains(brief, l => l.Kind == YearInBrief.Courts && l.Text.Contains("brought a suit against your house over " + matter) && l.Grave == YearMoments.Sued);
    }

    [Fact]
    public void A_battle_of_the_house_is_a_card_with_its_hulls_and_the_score_and_the_brief_says_it_once()
    {
        // docs/STEAM_REVIEW.md, item 13: a battle report card when the score moves
        var (w, tc, house, me) = Start();
        var foe = Foe(w, house);
        tc.Fleets.DeclareWar(w, foe.Id, house.Id);
        var war = w.Wars.Single(x => x.Between(house.Id, foe.Id)); war.AddScore(foe.Id, 2);
        var sys = w.Galaxy!.Systems.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList(); string at = sys[0].Name;
        w.Battles.Add(new BattleRecord { Turn = w.Turn + 1, SystemId = sys[0].Id, Place = at, AttackerId = foe.Id, DefenderId = house.Id, WinnerId = foe.Id,
            AttackerHulls = 9, DefenderHulls = 5, AttackerLost = 2, DefenderLost = 4 });
        NextYear(w);
        var lost = Assert.Single(YearMoments.For(w, Content), x => x.Kind == YearMoments.Battle);
        Assert.Equal($"Defeat at {at}", lost.Title); Assert.True(lost.Bad); Assert.Equal(YearMoments.BattleLost, lost.Weight);
        Assert.Contains($"Your 5 hulls met 9 of {foe.Name} at {at}, and were beaten. You lost 4, they lost 2.", lost.Text);
        Assert.Contains("The war stands 0 to 2.", lost.Text); Assert.Equal("battle", lost.Scene); Plain(lost);
        var brief = YearInBrief.For(w, Content);
        Assert.Single(brief, l => l.Text.StartsWith($"The battle of {at}"));   // the card's line is the brief's own, made grave
        Assert.Contains(brief, l => l.Text.StartsWith($"The battle of {at}") && l.Grave == YearMoments.BattleLost);

        // two battles the next year, both won: one card for them, good news
        w.Battles.Add(new BattleRecord { Turn = w.Turn + 1, SystemId = sys[1].Id, Place = sys[1].Name, AttackerId = house.Id, DefenderId = foe.Id, WinnerId = house.Id, AttackerHulls = 6, DefenderHulls = 3, AttackerLost = 1, DefenderLost = 3 });
        w.Battles.Add(new BattleRecord { Turn = w.Turn + 1, SystemId = sys[2].Id, Place = sys[2].Name, AttackerId = house.Id, DefenderId = foe.Id, WinnerId = house.Id, AttackerHulls = 4, DefenderHulls = 2, AttackerLost = 0, DefenderLost = 2 });
        NextYear(w);
        var won = Assert.Single(YearMoments.For(w, Content), x => x.Kind == YearMoments.Battle);
        Assert.Equal("Two battles, both won", won.Title); Assert.False(won.Bad); Assert.Equal(YearMoments.BattleWon, won.Weight);
        Assert.Contains(": both won. You lost 1 hull and sank 5.", won.Text); Plain(won);
    }

    [Fact]
    public void At_most_three_moments_are_cards_and_a_death_in_the_main_line_comes_first()
    {
        var (w, tc, house, me) = Start();
        var foe = Foe(w, house);
        var corvin = Of(w, "corvin_castellan"); var ysolde = Of(w, "ysolde_castellan");
        Baby(w, house, "t_livia", "Livia Castellan", "female", corvin, ysolde);
        tc.Fleets.DeclareWar(w, foe.Id, house.Id);
        var world = w.HoldingsOf(house.Id).First(h => h.IsPlanet);
        w.Sieges[world.Id] = new Siege { HoldingId = world.Id, BesiegerId = foe.Id, Turns = 1, Required = 3 };
        DynastySystem.Die(w, "helene_castellan");
        NextYear(w);
        var all = YearMoments.For(w, Content); var cards = YearMoments.Cards(w, Content);
        Assert.True(all.Count >= 4);
        Assert.Equal(YearMoments.MaxCards, cards.Count);
        Assert.Equal(YearMoments.Death, cards[0].Kind);
        Assert.Equal(all.OrderByDescending(m => m.Weight).Select(m => m.Weight), all.Select(m => m.Weight));   // the heaviest first
        Assert.Equal(all.Take(3).Select(m => m.Key), cards.Select(m => m.Key));
        // the rest stay lines in the year in brief: every moment has one
        var brief = YearInBrief.For(w, Content);
        foreach (var m in all) Assert.Contains(brief, l => l.PersonId == m.Subject && l.Kind == m.Section && l.Tone == m.Tone);
    }

    [Fact]
    public void The_worst_news_stands_first_and_a_warning_about_someone_since_dead_is_dropped()
    {
        var (w, _, house, me) = Start();
        var corvin = Of(w, "corvin_castellan"); var ysolde = Of(w, "ysolde_castellan");
        w.Tell("warn", $"{corvin.Name} is ill. A physician can be sent for from your own purse", "dynasty", corvin.Id);
        w.Tell("warn", $"{ysolde.Name} is ill. A physician can be sent for from your own purse", "dynasty", ysolde.Id);
        DynastySystem.Die(w, corvin.Id);
        NextYear(w);
        var brief = YearInBrief.For(w, Content);
        Assert.DoesNotContain(brief, l => l.Text.StartsWith(corvin.Name + " is ill"));   // he died that year: the warning is stale
        Assert.Contains(brief, l => l.Text.StartsWith(ysolde.Name + " is ill"));         // she lives: it stands
        var worst = YearInBrief.Worst(brief);
        Assert.NotEmpty(worst); Assert.True(worst.Count <= YearInBrief.WorstCount);
        Assert.StartsWith(corvin.Name + " died at", worst[0].Text);
        Assert.Equal(YearMoments.HeirDies, worst[0].Grave);
        Assert.All(worst, l => Assert.Equal("warn", l.Tone));
        Assert.Equal(worst.OrderByDescending(l => l.Grave).Select(l => l.Grave), worst.Select(l => l.Grave));
    }

    [Fact]
    public void The_head_who_dies_is_the_seat_passing_and_the_succession_screen_stages_it()
    {
        var (w, _, house, me) = Start();
        DynastySystem.Die(w, me.Id);
        Assert.False(w.IsDynastyDead);
        NextYear(w);
        var cards = YearMoments.Cards(w, Content);
        var s = cards[0];
        Assert.Equal(YearMoments.Succession, s.Kind); Assert.True(s.Staged); Assert.Equal(me.Id, s.Lead[0].Id);
        Assert.Equal(w.ProtagonistId, s.Around[0].Id);
        // the summary says the new head is you, and he who held it is "he"
        var line = YearInBrief.For(w, Content).First();
        Assert.StartsWith("Aurel Castellan, head of the house, died at 68. You are head now, and what he had begun", line.Text);
        Assert.Equal(YearMoments.HeadDies, line.Grave);
    }

    [Fact]
    public void The_first_year_has_no_moments_for_it_has_no_year_before_it()
    {
        var (w, _, _, _) = Start();
        DynastySystem.Die(w, "helene_castellan");
        Assert.Empty(YearMoments.For(w, Content));
    }

    [Fact]
    public void Every_moment_has_a_painting_the_client_holds()
    {
        foreach (var kind in YearMoments.Kinds) Assert.Contains(SceneArt.ForMoment(kind), SceneArt.Scenes);
        Assert.Equal("funeral", SceneArt.ForMoment(YearMoments.Death)); Assert.Equal("family", SceneArt.ForMoment(YearMoments.Birth)); Assert.Equal("feast", SceneArt.ForMoment(YearMoments.Marriage));
    }

    [Fact]
    public void The_funeral_names_whom_it_buries_draws_no_dice_and_comes_once()
    {
        var (w, _, house, me) = Start();
        var corvin = Of(w, "corvin_castellan");
        DynastySystem.Die(w, corvin.Id, "in battle");
        Assert.True(w.HasFlag(LifeSystem.FuneralFlag));
        var def = Content.Event("a_death_in_the_house")!;
        var rng = WorldBuilder.RngFor(w); ulong before = rng.State;
        var binding = Binder.TryBind(def, w, rng);
        Assert.NotNull(binding);
        Assert.True(binding!.TryGet("dead", out var dead)); Assert.Equal(corvin.Id, dead);
        Assert.Equal(before, rng.State);   // the dead are bound from the funerals, not by the dice
        var ctx = new EvalContext(w, binding, rng, Content);
        Assert.StartsWith("Corvin Castellan is dead, and the house must bury its own.", TextRenderer.Substitute(Content.Text(def.Id)!.Body, ctx));
        Assert.Equal("As Corvin Castellan would have wanted.", TextRenderer.Substitute(Content.Text(def.Id)!.Choices["as_the_dead_would_have_wanted"], ctx));
        // what was said over him is the report's facts
        string facts = ReportFacts.For(w, def, Content, binding);
        Assert.StartsWith("What was said at the graveside: Corvin Castellan", facts);
        // the engine finds it with him in it, and once the house has buried him it is not raised for him again
        var engine = new EventEngine(Content, WorldBuilder.RngFor(w));
        Assert.Contains(engine.GatherEligible(w), e => e.def.Id == def.Id && e.binding.TryGet("dead", out var d) && d == corvin.Id);
        corvin.Flags.Add("mourned");
        Assert.Null(Binder.TryBind(def, w, WorldBuilder.RngFor(w)));
        // a death of long ago is not buried again: only the last two years' funerals bind
        corvin.Flags.Remove("mourned"); w.Turn += 3;
        Assert.Null(Binder.TryBind(def, w, WorldBuilder.RngFor(w)));
    }

    [Fact]
    public void A_played_year_opens_its_moments_from_what_it_recorded()
    {
        // the whole turn, as the client plays it: a death in the year, the year's turn, and the cards as the next opens
        var (w, tc, house, me) = Start();
        DynastySystem.Die(w, "helene_castellan");
        tc.EndTurn(); if (w.IsDynastyDead) return; tc.BeginTurn();
        var moments = YearMoments.For(w, Content);
        if (w.ProtagonistId == me.Id) Assert.Contains(moments, m => m.Kind == YearMoments.Death && m.Lead[0].Id == "helene_castellan" && m.Title == "Your wife is dead");
        Assert.All(moments, Plain);
        Assert.True(YearMoments.Cards(w, Content).Count <= YearMoments.MaxCards);
        Assert.Contains(YearInBrief.Worst(YearInBrief.For(w, Content)), l => l.PersonId == "helene_castellan" || l.Grave >= YearMoments.SpouseDies);
    }
}
