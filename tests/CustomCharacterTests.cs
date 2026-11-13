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
        var presets = CharacterWorkshop.Presets(Content); Assert.Equal(4, presets.Count);
        foreach (var (name, blurb, spec) in presets)
        {
            Assert.True(blurb.Length > 20); var problems = CharacterWorkshop.Problems(spec, Content, Blank(spec)); Assert.True(problems.Count == 0, $"{name}: {string.Join("; ", problems)}");
            int cost = CharacterWorkshop.Cost(spec, Content).total;
            if (spec.Mode == CustomCharacter.Standard) Assert.InRange(cost, 15, WorkshopLimits.Standard.Budget); else Assert.True(cost > 220, $"{name} costs {cost}");
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
        Play(tc, 25); Assert.False(w.Houses.Values.Any(h => double.IsNaN(h.Wealth)));
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
}
