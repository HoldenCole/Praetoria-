using Praetoria.Core.Creation;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>A character of the player's own making (docs/CHARACTER.md): priced, checked by mode, and built into a world that plays.</summary>
public class CustomCharacterTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Blank(CustomCharacter c, ulong seed = 3) => WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), CharacterWorkshop.BaseFor(c)), seed);
    private static (TurnController tc, World w, House house) Build(CustomCharacter c, ulong seed = 3)
    {
        var w = Blank(c, seed); CharacterWorkshop.Apply(w, Content, c); var tc = new TurnController(w, Content); CharacterWorkshop.Finish(tc, c);
        return (tc, w, w.House(w.Protagonist!.HouseId)!);
    }
    private static void Play(TurnController tc, int turns)
    {
        for (int t = 0; t < turns && !tc.World.IsDynastyDead; t++)
        {
            foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
            tc.EndTurn();
        }
    }
    private static CustomCharacter Preset(string name) => CharacterWorkshop.Presets(Content).Single(p => p.name == name).spec.Clone();

    [Fact]
    public void The_presets_are_legal_and_three_of_them_fit_the_standard_budget()
    {
        var presets = CharacterWorkshop.Presets(Content); Assert.Equal(5, presets.Count);
        foreach (var (name, blurb, spec) in presets)
        {
            Assert.True(blurb.Length > 20); var problems = CharacterWorkshop.Problems(spec, Content, Blank(spec)); Assert.True(problems.Count == 0, $"{name}: {string.Join("; ", problems)}");
            int cost = CharacterWorkshop.Cost(spec, Content).total;
            if (spec.Mode == CustomCharacter.Standard) Assert.InRange(cost, name == "The Upstart" ? 5 : 15, WorkshopLimits.Standard.Budget); else Assert.True(cost > 220, $"{name} costs {cost}");
        }
        Assert.Equal("absurd", CharacterWorkshop.Rating(CharacterWorkshop.Cost(Preset("The Colossus"), Content).total));
        Assert.Equal("modest", CharacterWorkshop.Rating(10)); Assert.Equal("able", CharacterWorkshop.Rating(60)); Assert.Equal("formidable", CharacterWorkshop.Rating(61)); Assert.Equal("overpowered", CharacterWorkshop.Rating(200));
    }

    [Fact]
    public void A_character_is_built_as_written_and_the_world_plays()
    {
        var spec = Preset("The New Man"); spec.Face = new FaceSpec { Skin = 2, Hair = 1, Beard = 1, Scar = true };
        var (tc, w, house) = Build(spec); var p = w.Protagonist!;
        Assert.Equal("Marcus Crane", p.Name); Assert.Equal("Crane", house.Name); Assert.Equal(24, p.Age); Assert.Equal("stewardship", p.CareerTrack); Assert.Equal(3, p.Skill("economics"));
        Assert.Contains("Ambitious", p.NatureTraits); Assert.Contains("Administrator", p.AptitudeTraits); Assert.Equal("#2EC27E", house.AccentColor); Assert.Equal("new", house.Origin);
        Assert.Equal(45, house.Treasury.Credits); Assert.Equal("lucent", house.PatronId); Assert.Single(w.BusinessesOf(house.Id)); Assert.Empty(w.HoldingsOf(house.Id));
        Assert.Equal("factors", FactionSystem.Of(w, house.Id)?.PartyId); Assert.Contains(CharacterWorkshop.CustomFlag, w.WorldFlags); Assert.DoesNotContain(CharacterWorkshop.FreeFlag, w.WorldFlags);
        Assert.Equal(CharacterWorkshop.Cost(spec, Content).total, w.Counter(CharacterWorkshop.PowerCounter)); Assert.Equal("skin=2,hair=1,style=-1,beard=1,scar=1", w.Cosmetics["look:" + p.Id]);
        Assert.Equal(3, house.Orders.Count);   // measured in the three orders like any house
        Play(tc, 12); Assert.True(w.Turn >= 12 || w.IsDynastyDead);
    }

    [Fact]
    public void Standard_refuses_what_free_allows()
    {
        var c = Preset("The Heir"); var w = Blank(c);
        c.Skills["law"] = 5; Assert.Contains(CharacterWorkshop.Problems(c, Content, w), x => x.Contains("law may be 0 to 4"));
        c.Skills["law"] = 2; c.Treasury["credits"] = 150; c.Hulls = 8; c.Worlds.Add("sorrow"); Assert.Contains(CharacterWorkshop.Problems(c, Content, w), x => x.Contains("over the standard budget"));
        c = Preset("The Heir"); c.Worlds.Add(w.Galaxy!.Capital); Assert.Contains(CharacterWorkshop.Problems(c, Content, w), x => x.Contains("capital"));
        string onlyWorld = w.Houses.Values.Where(h => h.Id != "custom" && w.HoldingsOf(h.Id).Count(x => x.IsPlanet) == 1).Select(h => w.HoldingsOf(h.Id).First(x => x.IsPlanet).SystemId).First(s => s != w.Galaxy.Capital);
        c = Preset("The Heir"); c.Worlds.Add(onlyWorld); Assert.Contains(CharacterWorkshop.Problems(c, Content, w), x => x.Contains("only world"));
        Assert.Throws<ContentException>(() => CharacterWorkshop.Apply(Blank(c), Content, c));
        c.Mode = CustomCharacter.Free; c.Worlds.Add(w.Galaxy.Capital); c.Skills["law"] = 9; c.Title = "duke"; c.Legitimacy = 95;
        Assert.Empty(CharacterWorkshop.Problems(c, Content, w));
        c.Title = "emperor"; Assert.Contains(CharacterWorkshop.Problems(c, Content, w), x => x.Contains("throne"));
        c.Title = "duke"; c.Skills["swordplay"] = 2; c.Nature.Add("Tall"); c.Party = "greens";
        var bad = CharacterWorkshop.Problems(c, Content, w); Assert.Contains(bad, x => x.Contains("swordplay")); Assert.Contains(bad, x => x.Contains("Tall")); Assert.Contains(bad, x => x.Contains("greens"));
    }

    [Fact]
    public void A_world_taken_at_the_start_is_the_houses_and_its_old_holder_goes_home()
    {
        var c = Preset("The Colossus"); var blank = Blank(c);
        var victim = blank.Houses.Values.Where(h => h.Id != "custom" && h.Id != PirateSystem.HouseId && blank.HoldingsOf(h.Id).Count(x => x.IsPlanet) >= 1 && blank.Fleets.Values.Any(f => f.OwnerId == h.Id && !f.Office))
            .OrderBy(h => h.Id, StringComparer.Ordinal).First();
        string sys = blank.Fleets.Values.Where(f => f.OwnerId == victim.Id && !f.Office).OrderBy(f => f.Id, StringComparer.Ordinal).First().SystemId;
        if (!c.Worlds.Contains(sys)) c.Worlds.Add(sys);
        var (tc, w, house) = Build(c);
        Assert.Equal(house.Id, w.PlanetHolder(sys)); Assert.DoesNotContain(w.Fleets.Values, f => f.OwnerId == victim.Id && f.SystemId == sys && !f.Office);
        Assert.Equal(c.Worlds.Count, w.HoldingsOf(house.Id).Count(h => h.IsPlanet)); Assert.All(w.HoldingsOf(house.Id), h => Assert.Equal(w.ProtagonistId, h.GovernorId));
        var fleet = Assert.Single(w.Fleets.Values.Where(f => f.OwnerId == house.Id)); Assert.Equal(24, fleet.Strength); Assert.Equal(6, fleet.Ships["cruiser"]);
        Assert.Equal("House Magna", house.Name); Assert.Equal(1200, house.Treasury.Credits); Assert.Contains(CharacterWorkshop.FreeFlag, w.WorldFlags);
        Assert.Equal(2, w.BusinessesOf(house.Id).Count()); Assert.True(OrderSystem.Tier(house, OrderSystem.Arms) >= 2);
        Play(tc, 25); Assert.DoesNotContain(w.Houses.Values, h => double.IsNaN(h.Wealth));
    }

    [Fact]
    public void The_family_is_real_kin()
    {
        var c = Preset("The Colossus"); var (tc, w, house) = Build(c); var p = w.Protagonist!;
        var spouse = DynastySystem.Spouse(w, p); Assert.NotNull(spouse); Assert.Equal("male", spouse!.Sex); Assert.Contains("wed", p.Flags); Assert.Equal(house.Id, spouse.HouseId);
        var children = w.LivingCharacters().Where(k => k.MotherId == p.Id).ToList(); Assert.Equal(2, children.Count); Assert.All(children, k => { Assert.Equal(spouse.Id, k.FatherId); Assert.Equal(house.Id, k.HouseId); Assert.EndsWith("Magna", k.Name); });
        var brother = w.LivingCharacters().Single(k => k.Id == "custom_sibling_1"); Assert.True(DynastySystem.AreKin(w, p, brother)); Assert.True(DynastySystem.AreKin(w, children[0], brother));
        Assert.False(w.Char("custom_father")!.Alive); Assert.Equal(w.LivingCharacters().Count(k => k.HouseId == house.Id), house.Members.Count(m => w.Char(m)?.Alive == true));
        Assert.Equal(new[] { 5, 8 }, children.Select(k => k.Age).OrderBy(a => a).ToArray());
        var names = w.LivingCharacters().Where(k => k.HouseId == house.Id).Select(k => k.Name.Split(' ')[0]).ToList(); Assert.Equal(names.Count, names.Distinct().Count());
        var friend = Economy.HeadOf(w, w.House("aurelian")!)!; Assert.True(w.GetRelationship(friend.Id, p.Id)!.Disposition >= 40);
        var bad = Preset("The Heir"); bad.Age = 20; bad.Children.Add(new KinSpec { Age = 9 }); Assert.Contains(CharacterWorkshop.Problems(bad, Content), x => x.Contains("too old for a parent"));
    }

    [Fact]
    public void A_custom_character_may_begin_at_the_academy_in_either_era()
    {
        foreach (bool imperium in new[] { false, true })
        {
            var c = Preset("The Heir"); c.Academy = true; c.Imperium = imperium; c.Age = 40; c.Rank = 3;
            var (tc, w, house) = Build(c); Assert.True(w.InAcademy); Assert.Equal(16, w.Protagonist!.Age); Assert.Equal(0, w.Protagonist.CareerRank); Assert.Equal(imperium, w.IsImperium);
            Play(tc, 8); Assert.False(w.InAcademy); Assert.True(w.Turn >= 1);
        }
        var wed = Preset("The Heir"); wed.Academy = true; wed.Spouse = new KinSpec { Age = 20 }; Assert.Contains(CharacterWorkshop.Problems(wed, Content), x => x.Contains("cadet"));
        var court = Preset("The Heir"); court.Imperium = true; var built = Build(court); Assert.NotNull(ThroneSystem.Emperor(built.w)); Assert.NotEqual(built.house.Id, ThroneSystem.Emperor(built.w)!.Id);
        court.Seat = true; Assert.Contains(CharacterWorkshop.Problems(court, Content), x => x.Contains("Republic"));
    }

    [Fact]
    public void A_character_saves_to_a_file_and_loads_again_and_the_dice_make_legal_ones()
    {
        var c = Preset("The Colossus"); c.Face.Skin = 3; var again = CustomCharacter.FromJson(c.ToJson());
        Assert.Equal(c.ToJson(), again.ToJson()); Assert.Equal(c.Worlds, again.Worlds); Assert.Equal(6, again.Skills["tactics"]); Assert.Equal(3, again.Face.Skin); Assert.Equal(2, again.Children.Count);
        Assert.Throws<System.Text.Json.JsonException>(() => CustomCharacter.FromJson("not a character"));
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var std = CharacterWorkshop.Random(Content, seed, CustomCharacter.Standard); var w = Blank(std);
            var problems = CharacterWorkshop.Problems(std, Content, w); Assert.True(problems.Count == 0, $"standard seed {seed}: {string.Join("; ", problems)}");
            var free = CharacterWorkshop.Random(Content, seed, CustomCharacter.Free, w); problems = CharacterWorkshop.Problems(free, Content, w); Assert.True(problems.Count == 0, $"free seed {seed}: {string.Join("; ", problems)}");
        }
        Assert.Equal(CharacterWorkshop.Random(Content, 7, CustomCharacter.Standard).ToJson(), CharacterWorkshop.Random(Content, 7, CustomCharacter.Standard).ToJson());
    }

    [Fact]
    public void Extreme_characters_play_thirty_years_without_breaking_the_world()
    {
        var nothing = new CustomCharacter { Name = "Nemo Null", Treasury = new(), Legitimacy = 0, Rank = 0 };   // no coin, no kin, no skill, no land
        var everything = Preset("The Colossus"); everything.Skills = CharacterWorkshop.Skills.ToDictionary(s => s, _ => 10); everything.Hulls = 80; everything.Treasury["credits"] = 5000;
        var old = Preset("The Heir"); old.Age = 80; old.Siblings.Clear();
        var crowd = Preset("The Heir"); crowd.Mode = CustomCharacter.Free; crowd.Age = 50; crowd.Spouse = new KinSpec { Age = 45 }; for (int i = 0; i < 8; i++) crowd.Children.Add(new KinSpec { Sex = i % 2 == 0 ? "male" : "female", Age = 2 + i * 4 });
        foreach (var c in new[] { nothing, everything, old, crowd })
        {
            Assert.Empty(CharacterWorkshop.Problems(c, Content, Blank(c)));
            var (tc, w, house) = Build(c, 5); Play(tc, 30);
            Assert.All(w.Companies, co => Assert.Equal(co.Shares, co.Holders.Values.Sum()));
            Assert.All(w.Houses.Values, h => Assert.InRange(h.Legitimacy, 0, 100));
            Assert.True(w.Market == null || w.Market.Goods.Values.All(gs => gs.Price > 0 && !double.IsNaN(gs.Price)));
        }
    }

    [Fact]
    public void The_saga_says_how_its_founder_was_made()
    {
        var fair = Build(Preset("The Heir")); Assert.Contains("within the standard budget", Praetoria.Core.Presentation.Epilogue.Generate(fair.w, Content));
        var colossus = Build(Preset("The Colossus")); Assert.Contains("without limits", Praetoria.Core.Presentation.Epilogue.Generate(colossus.w, Content));
        var written = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_circling_heir"), 1);
        Assert.DoesNotContain("own hand", Praetoria.Core.Presentation.Epilogue.Generate(written, Content));
    }

    /* docs/PLAYTHROUGH.md, Medium, A first game: "The creator: a map for choosing a world, with what it yields and whose space it is
       in" (N11: fifty names in a flat list, no yield, and a disabled world gave no reason). */
    [Fact]
    public void Every_world_of_the_map_says_what_it_is_whose_it_is_what_it_brings_and_why_it_may_not_be_taken()
    {
        var c = Preset("The Heir"); c.Mode = CustomCharacter.Standard; var blank = Blank(c);
        var sites = CharacterWorkshop.Sites(c, Content, blank);
        Assert.Equal(blank.Galaxy!.Systems.Count, sites.Count);
        Assert.All(sites, s => { Assert.False(string.IsNullOrWhiteSpace(s.Name)); Assert.False(string.IsNullOrWhiteSpace(s.Kind)); Assert.False(string.IsNullOrWhiteSpace(s.Region)); Assert.True(s.Lanes >= 1, s.Name + " has no lane"); });
        // a held world names its holder and is the kind of world that holding is; an unclaimed one is what it would become
        var held = sites.First(s => s.HolderId.Length > 0); Assert.Equal(blank.House(held.HolderId)!.Name, held.HolderName);
        Assert.Equal(Content.Holdings.Spec(blank.PlanetOf(held.SystemId)!.Specialization)!.Name, held.Kind);
        var free = sites.First(s => s.HolderId.Length == 0 && s.Ring != Praetoria.Core.Galaxy.Ring.Beyond);
        Assert.Equal(Content.Holdings.Spec(CharacterWorkshop.SpecFor(Content, blank.Galaxy.System(free.SystemId)!.PlanetClass))!.Name, free.Kind);
        Assert.Null(free.Refusal);
        // what it brings is the kind's yield less its upkeep, and some world brings something
        var kind = Content.Holdings.Spec(blank.PlanetOf(held.SystemId)!.Specialization)!;
        Assert.Equal(kind.BaseYield.Credits - kind.Upkeep.Credits, held.Yield.Credits);
        Assert.Contains(sites, s => s.Yield.Credits > 0);
        // the reasons a standard start may not take a world are the ones the whole character is held to
        var capital = sites.Single(s => s.SystemId == blank.Galaxy.Capital); Assert.Contains("capital", capital.Refusal);
        var only = sites.First(s => s.HolderId.Length > 0 && s.SystemId != blank.Galaxy.Capital && blank.HoldingsOf(s.HolderId).Count(h => h.IsPlanet) < 2 && s.Ring != Praetoria.Core.Galaxy.Ring.Beyond);
        Assert.Contains("only world", only.Refusal);
        foreach (var s in sites)
        {
            var trial = c.Clone(); trial.Worlds.Clear(); trial.Worlds.Add(s.SystemId); trial.Ventures.Clear();
            bool refusedWhole = CharacterWorkshop.Problems(trial, Content, blank).Any(x => x.Contains(s.Name) || x.Contains("capital") || x.Contains("beyond") || x.Contains("nation"));
            if (s.Refusal == null) Assert.False(refusedWhole, s.Name + " is said to be free and is refused"); 
        }
        // a free start may take any of them
        var any = c.Clone(); any.Mode = CustomCharacter.Free;
        Assert.All(CharacterWorkshop.Sites(any, Content, blank), s => Assert.Null(s.Refusal));
        // a world beside a nation's space says so
        Assert.All(sites.Where(s => s.BesideANation), s => Assert.NotEqual(Praetoria.Core.Galaxy.Ring.Beyond, s.Ring));
    }

    /* docs/PLAYTHROUGH.md, Medium, A first game: "a house for the spouse" (N13: "wed" gave only a name and an age). */
    [Fact]
    public void A_spouse_may_come_from_a_named_house_and_keeps_its_name_as_one_married_in_play_does()
    {
        var plain = Preset("The Colossus"); plain.Mode = CustomCharacter.Free; plain.FriendId = ""; plain.RivalId = "";
        var c = plain.Clone(); c.Spouse!.HouseId = "aurelian"; c.Spouse.Name = "";
        Assert.Empty(CharacterWorkshop.Problems(c, Content, Blank(c)));
        Assert.Equal(CharacterWorkshop.Cost(plain, Content).total + CharacterWorkshop.SpouseHouseCost, CharacterWorkshop.Cost(c, Content).total);

        var (tc, w, house) = Build(c); var p = w.Protagonist!; var theirs = w.House("aurelian")!;
        var spouse = DynastySystem.Spouse(w, p)!;
        Assert.Equal(theirs.Id, spouse.HouseId); Assert.Contains(spouse.Id, theirs.Members); Assert.DoesNotContain(spouse.Id, house.Members);
        Assert.EndsWith(theirs.Name.Replace("House ", ""), spouse.Name);
        // the children are the head's own house's, with both parents
        var children = w.LivingCharacters().Where(k => k.MotherId == p.Id || k.FatherId == p.Id).ToList(); Assert.NotEmpty(children);
        Assert.All(children, k => { Assert.Equal(house.Id, k.HouseId); Assert.True(k.FatherId == spouse.Id || k.MotherId == spouse.Id); });
        // the two heads think well of each other from the first day, and no better than a named friend would
        var head = Economy.HeadOf(w, theirs)!;
        Assert.Equal(CharacterWorkshop.SpouseHouseRegard, w.GetRelationship(head.Id, p.Id)!.Disposition);
        Assert.Equal(CharacterWorkshop.SpouseHouseRegardBack, w.GetRelationship(p.Id, head.Id)!.Disposition);
        // without a house the spouse is of the head's own, as before
        var (_, w0, house0) = Build(plain); Assert.Equal(house0.Id, DynastySystem.Spouse(w0, w0.Protagonist!)!.HouseId);
        // a house that is not there, or the head's own, is refused; and the world still plays
        var bad = c.Clone(); bad.Spouse!.HouseId = "no_such_house"; Assert.Contains(CharacterWorkshop.Problems(bad, Content, Blank(bad)), x => x.Contains("no such house"));
        var own = c.Clone(); own.Spouse!.HouseId = CharacterWorkshop.HouseId; Assert.Contains(CharacterWorkshop.Problems(own, Content, Blank(own)), x => x.Contains("no such house"));
        Play(tc, 5); Assert.False(w.IsDynastyDead);
        // the choice is kept when a character is saved to a file
        Assert.Equal("aurelian", CustomCharacter.FromJson(c.ToJson()).Spouse!.HouseId);
    }

    // ---------------- where they come from, and the family each of them (docs/CHARACTER.md; docs/UPSTART.md) ----------------

    private static CustomCharacter Plain() => new() { Name = "Tam Corvane", HouseName = "Corvane", Age = 30, Track = "stewardship", Rank = 1, Legitimacy = 10, Skills = { ["administration"] = 1 } };

    [Fact]
    public void A_background_moves_the_person_and_the_house_and_is_priced()
    {
        var bare = Plain(); var born = Plain(); born.Background = "clerks_child"; var bg = Content.Backgrounds.ById("clerks_child")!;
        Assert.Empty(CharacterWorkshop.Problems(born, Content, Blank(born)));
        Assert.Equal(CharacterWorkshop.Cost(bare, Content).total + CharacterWorkshop.BackgroundCost(bg), CharacterWorkshop.Cost(born, Content).total);
        Assert.Contains(CharacterWorkshop.Cost(born, Content).lines, l => l.what.Contains(bg.Name));
        var (_, w, house) = Build(born); var p = w.Protagonist!;
        Assert.Equal(10 + bg.Legitimacy, house.Legitimacy); Assert.Equal(20 + bg.Treasury.Credits, house.Treasury.Credits);
        Assert.Contains("Administrator", p.AptitudeTraits); Assert.Equal(2, p.Skill("administration")); Assert.Equal(1, p.Skill("law")); Assert.Contains("background:clerks_child", p.Flags);
        // a low birth gives points back and a high one costs them; an unknown one is refused
        Assert.True(CharacterWorkshop.BackgroundCost(Content.Backgrounds.ById("rim_born")!) <= CharacterWorkshop.BackgroundCost(Content.Backgrounds.ById("old_blood")!));
        Assert.True(CharacterWorkshop.BackgroundCost(Content.Backgrounds.ById("old_blood")!) > 0);
        born.Background = "a_prince"; Assert.Contains(CharacterWorkshop.Problems(born, Content, Blank(born)), x => x.Contains("no such background"));
        // and it survives the file
        born.Background = "rim_born"; Assert.Equal("rim_born", CustomCharacter.FromJson(born.ToJson()).Background);
    }

    [Fact]
    public void A_father_and_a_mother_may_be_living_and_of_the_house()
    {
        var c = Plain(); int before = CharacterWorkshop.Cost(c, Content).total;
        c.Mother = new KinSpec { Name = "Hesta", Age = 55, Track = "law", Rank = 2, Nature = "Just", Aptitude = "Orator" }; c.Siblings.Add(new KinSpec { Sex = "female", Age = 24 });
        Assert.Empty(CharacterWorkshop.Problems(c, Content, Blank(c)));
        Assert.Equal(before + CharacterWorkshop.ParentCost + 2 * CharacterWorkshop.KinRankCost + CharacterWorkshop.KinAptitudeCost + 1, CharacterWorkshop.Cost(c, Content).total);
        var (tc, w, house) = Build(c); var p = w.Protagonist!; var mother = w.Char("custom_mother")!; var father = w.Char("custom_father")!; var sister = w.Char("custom_sibling_1")!;
        Assert.True(mother.Alive); Assert.False(father.Alive); Assert.Equal("Hesta Corvane", mother.Name); Assert.Equal(house.Id, mother.HouseId); Assert.Contains(mother.Id, house.Members);
        Assert.Equal("law", mother.CareerTrack); Assert.Equal(2, mother.CareerRank); Assert.Contains("Just", mother.NatureTraits); Assert.Contains("Orator", mother.AptitudeTraits);
        Assert.Equal(mother.Id, p.MotherId); Assert.Equal(mother.Id, sister.MotherId); Assert.Equal(BondType.Blood, w.Relationship(mother.Id, p.Id).Bond);
        Assert.Equal(p.Id, Economy.HeadOf(w, house)!.Id);   // the player is the head whoever else is living
        Play(tc, 3); Assert.False(w.IsDynastyDead); Assert.Equal(house.Id, w.Protagonist!.HouseId);
        // a parent is a parent's age
        c.Mother.Age = c.Age + 5; Assert.Contains(CharacterWorkshop.Problems(c, Content, Blank(c)), x => x.Contains("not the age of a parent"));
    }

    [Fact]
    public void Each_relative_of_age_has_a_service_a_step_a_nature_and_a_gift()
    {
        var c = Plain(); c.Age = 44;
        c.Spouse = new KinSpec { Age = 40, Track = "military", Rank = 2, Nature = "Honorable" };
        c.Children.Add(new KinSpec { Sex = "female", Age = 21, Track = "law", Rank = 1, Aptitude = "Orator" }); c.Children.Add(new KinSpec { Sex = "male", Age = 9 });
        Assert.Empty(CharacterWorkshop.Problems(c, Content, Blank(c)));
        var (_, w, _) = Build(c); var spouse = w.Char("custom_spouse")!; var daughter = w.Char("custom_child_1")!; var son = w.Char("custom_child_2")!;
        Assert.Equal("military", spouse.CareerTrack); Assert.Equal(2, spouse.CareerRank); Assert.Contains("Honorable", spouse.NatureTraits); Assert.Single(spouse.NatureTraits);
        Assert.Equal("law", daughter.CareerTrack); Assert.Equal(1, daughter.CareerRank); Assert.Contains("Orator", daughter.AptitudeTraits);
        Assert.Equal(0, son.CareerRank); Assert.Empty(son.NatureTraits);
        // one left alone is as before: the head's service, from the bottom, a nature by the dice
        var d = Plain(); d.Siblings.Add(new KinSpec { Sex = "male", Age = 27 }); var (_, w2, _) = Build(d); var brother = w2.Char("custom_sibling_1")!;
        Assert.Equal("stewardship", brother.CareerTrack); Assert.Equal(0, brother.CareerRank); Assert.Single(brother.NatureTraits);
        // the rules: nobody has a step before twenty, a standard start stops at two steps, and the words must be real ones
        c.Children[0].Age = 18; Assert.Contains(CharacterWorkshop.Problems(c, Content, Blank(c)), x => x.Contains("before " + CharacterWorkshop.KinRankAge));
        c.Children[0].Age = 21; c.Spouse.Rank = 4; Assert.Contains(CharacterWorkshop.Problems(c, Content, Blank(c)), x => x.Contains("steps up a service"));
        c.Mode = CustomCharacter.Free; Assert.Empty(CharacterWorkshop.Problems(c, Content, Blank(c)));
        c.Spouse.Nature = "Sleepy"; Assert.Contains(CharacterWorkshop.Problems(c, Content, Blank(c)), x => x.Contains("no such nature"));
    }

    [Fact]
    public void The_upstart_begins_with_nothing_and_is_not_held_to_a_senators_year()
    {
        var c = Preset("The Upstart"); Assert.Equal("modest", CharacterWorkshop.Rating(CharacterWorkshop.Cost(c, Content).total));
        // a seed on which no report hands the house a rank, and so a seat, in its first years: about half the seeds are, and which they
        // are moves with any change to the reports (the binder spends the shared generator on every report it tries: seed 4 was one until
        // the other houses began to trade among themselves, seed 5 until the vote promises closed the whip to houses with no motion), so
        // the seeds are played in turn until one is, and every check below holds in each that is played
        TurnController tc = null!; World w = null!; House house = null!; Character p = null!; int legitimacy = 0;
        foreach (ulong seed in new ulong[] { 5, 4, 3, 6 })
        {
            (tc, w, house) = Build(c, seed); p = w.Protagonist!;
            Assert.Equal("landless", house.Title); Assert.Empty(w.HoldingsOf(house.Id)); Assert.Equal("", house.PatronId); Assert.False(house.Enrolled); Assert.False(OrderSystem.Seated(w, house));
            Assert.True(w.Char("custom_mother")!.Alive); Assert.Contains("background:clerks_child", p.Flags);
            // nobody of the house is called by its surname
            Assert.DoesNotContain(w.Characters.Values.Where(x => x.HouseId == house.Id), x => x.Name.Split(' ')[0] == "Corvane");
            legitimacy = house.Legitimacy;
            Play(tc, 5);
            if (house.Title == "landless" && !house.Enrolled) break;
        }
        Assert.Equal("landless", house.Title);
        Assert.DoesNotContain(w.OfficeWork, b => b.Kind == LadderSystem.SeatKind && b.HolderId == p.Id);                    // no seat, no senator's year
        Assert.Contains(w.OfficeWork, b => b.Kind == LadderSystem.Ledgers && b.HolderId == p.Id);                              // the year is the clerk's
        Assert.DoesNotContain(w.History, e => e.Text.Contains("has not been seen in the Senate"));
        Assert.NotNull(LadderSystem.ChangeCommitteeRefusal(w, p, LadderSystem.Committees.First(x => x != house.Committee)));
        // the rank it cannot yet ask for says why
        var why = new Praetoria.Core.Commands.PetitionTitleCommand(p.Id, tc.Progression!).Refusal(new Praetoria.Core.Commands.CommandContext(w, WorldBuilder.RngFor(w)));
        if (house.Legitimacy < 10) Assert.Contains("legitimacy", why);
        // a seat by the census brings the senator's year with it
        house.Enrolled = true; Assert.True(OrderSystem.Seated(w, house)); Play(tc, 1);
        Assert.Contains(w.OfficeWork, b => b.Kind == LadderSystem.SeatKind && b.HolderId == w.ProtagonistId);
        Assert.True(house.Legitimacy >= legitimacy - 2, $"standing fell from {legitimacy} to {house.Legitimacy} in six quiet years");
    }

    [Fact]
    public void One_who_does_the_years_work_at_the_bottom_goes_up_a_step()
    {
        var c = Preset("The Upstart"); var (tc, w, house) = Build(c); var p = w.Protagonist!; Assert.Equal(0, p.CareerRank);
        var ctx = new Praetoria.Core.Commands.CommandContext(w, WorldBuilder.RngFor(w)); int years = 0;
        for (int t = 0; t < LadderSystem.ServedYears + 3 && p.CareerRank == 0; t++)
        {
            foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available && !x.Choice.Id.Contains("pad")); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
            if (w.OfficeWork.Any(b => b.Kind == LadderSystem.Ledgers && b.HolderId == p.Id && b.Choice.Length == 0)
                && tc.Executor.TryExecute(new Praetoria.Core.Commands.DutyCommand(p.Id, LadderSystem.Ledgers, LadderSystem.Diligent, Content.Offices, Content.Policies), ctx)) years++;
            tc.EndTurn();
        }
        Assert.True(p.CareerRank >= 1, $"still at the bottom after {years} years' work");
        Assert.True(years <= LadderSystem.ServedYears);   // the years did it, or somebody's word did it sooner
    }
}
