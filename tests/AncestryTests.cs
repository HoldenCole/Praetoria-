using Praetoria.Core.Creation;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Ancestry (docs/ART_DIRECTION.md, "Portraits"; the owner, 2026-11-20): a house has one, and its people are painted from it.
/// Someone who marries in keeps the ancestry of the house they were born to. A child takes after its father or its mother. Nations
/// beyond the Reach have their own.</summary>
public class AncestryTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static World Start(int seed = 7)
    { var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), (ulong)seed); NationSystem.Seed(w, Content.Nations); return w; }
    private static AncestryCatalog Cat => Content.Ancestries;

    [Fact]
    public void EveryHouse_HasAnAncestryTheCatalogKnows_AndTheNamedOnesAreAsNamed()
    {
        var w = Start();
        foreach (var h in w.Houses.Values) Assert.NotNull(Cat.Def(Lineage.OfHouse(w, Cat, h.Id)));
        Assert.Equal("mediterranean", Lineage.OfHouse(w, Cat, "castellan"));
        foreach (var (house, named) in Cat.Houses) Assert.Equal(named, Lineage.OfHouse(w, Cat, house));
        // a house of the Reach that content does not name falls to an ancestry of the Reach, and the same one every time
        string cadet = Lineage.OfHouse(w, Cat, "a_house_nobody_named"); Assert.False(Cat.Def(cadet)!.Beyond); Assert.Equal(cadet, Lineage.OfHouse(w, Cat, "a_house_nobody_named"));
    }

    [Fact]
    public void ThePeopleOfAHouse_ShareItsAncestry_AndThoseWhoMarryInKeepTheirOwn()
    {
        var w = Start(); var me = w.Protagonist!; string mine = Lineage.OfHouse(w, Cat, me.HouseId);
        Assert.Equal(mine, Lineage.Of(w, Cat, me));
        var other = w.Houses.Values.Where(h => w.OfTheReach(h.Id) && Lineage.OfHouse(w, Cat, h.Id) != mine).OrderBy(h => h.Id, StringComparer.Ordinal).First();
        var bride = new Character { Id = "test_bride", Name = "Test Bride", Sex = "female", Age = 24, HouseId = me.HouseId, BornHouseId = other.Id };
        w.Characters[bride.Id] = bride;
        Assert.Equal(Lineage.OfHouse(w, Cat, other.Id), Lineage.Of(w, Cat, bride));   // she wears her new house's name and her old house's face
        Assert.NotEqual(mine, Lineage.Of(w, Cat, bride));
    }

    [Fact]
    public void AChild_TakesAfterItsFatherOrItsMother()
    {
        var w = Start(); var me = w.Protagonist!; string mine = Lineage.Of(w, Cat, me);
        var other = w.Houses.Values.Where(h => w.OfTheReach(h.Id) && Lineage.OfHouse(w, Cat, h.Id) != mine).OrderBy(h => h.Id, StringComparer.Ordinal).First(); string hers = Lineage.OfHouse(w, Cat, other.Id);
        var mother = new Character { Id = "test_mother", Name = "Test Mother", Sex = "female", Age = 40, HouseId = me.HouseId, BornHouseId = other.Id }; w.Characters[mother.Id] = mother;
        var seen = new HashSet<string>();
        for (int i = 0; i < 24; i++)
        {
            var child = new Character { Id = "test_child_" + i, Name = "Child " + i, Sex = i % 2 == 0 ? "male" : "female", Age = 5, HouseId = me.HouseId, BornHouseId = me.HouseId, FatherId = me.Id, MotherId = mother.Id };
            w.Characters[child.Id] = child; string a = Lineage.Of(w, Cat, child);
            Assert.Contains(a, new[] { mine, hers }); Assert.Equal(Lineage.TakesAfterFather(child.Id) ? mine : hers, a); seen.Add(a);
            // and a grandchild takes after that child or its other parent, never anything else
            var grand = new Character { Id = "test_grand_" + i, Name = "Grand " + i, Sex = "male", Age = 1, HouseId = me.HouseId, FatherId = child.Id }; w.Characters[grand.Id] = grand;
            Assert.Equal(a, Lineage.Of(w, Cat, grand));
        }
        Assert.Equal(2, seen.Count);   // among two dozen children of a mixed marriage, some take after each
    }

    [Fact]
    public void AHousesOwnChoice_Wins_AndACharacterOfYourOwn_SetsIt()
    {
        var w = Start(); var house = w.House(w.Protagonist!.HouseId)!;
        house.Ancestry = "east_asian"; Assert.Equal("east_asian", Lineage.Of(w, Cat, w.Protagonist!));
        house.Ancestry = "no_such_thing"; Assert.Equal("mediterranean", Lineage.OfHouse(w, Cat, house.Id));   // nonsense is ignored
        Assert.Contains(Cat.OfTheReach, a => a.Id == "african"); Assert.DoesNotContain(Cat.OfTheReach, a => a.Id == "steppe");
        Assert.Equal("", new CustomCharacter().Ancestry);
    }

    [Fact]
    public void ANationBeyondTheReach_HasItsOwn_AndAProfileSaysWhatSomeoneIs()
    {
        var w = Start(); var khans = w.Nation("vashti")!;
        Assert.Equal("steppe", Lineage.OfHouse(w, Cat, khans.HouseId));
        var p = ProfileProjection.Build(w, Content, w.Protagonist!.Id)!; Assert.Equal("Mediterranean", p.Ancestry);
        Assert.True(Cat.Defs.Count >= 6); Assert.All(Cat.Defs, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }
}
