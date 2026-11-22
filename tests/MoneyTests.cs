using System.Text.RegularExpressions;
using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Money (docs/MONEY.md): a house's books are in hundreds of credits, shown as credits, with single credits below a hundred.</summary>
public class MoneyTests
{
    [Fact]
    public void The_books_figure_is_written_as_the_credits_it_is()
    {
        Assert.Equal(100, Money.PerUnit);
        Assert.Equal("2,800", Money.Show(28)); Assert.Equal("0", Money.Show(0)); Assert.Equal("-400", Money.Show(-4)); Assert.Equal("1,234,500", Money.Show(12345));
        Assert.Equal("125", Money.Show(1.25)); Assert.Equal("70", Money.Show(0.7));
        Assert.Equal("+800", Money.Signed(8)); Assert.Equal("-200", Money.Signed(-2)); Assert.Equal("0", Money.Signed(0));
        Assert.Equal("2,800 credits", Money.Credits(28)); Assert.Equal("100 credits", Money.Credits(1));
        Assert.Equal("300", Money.Of(Resource.Credits, 3)); Assert.Equal("3", Money.Of(Resource.Materials, 3)); Assert.Equal("+300", Money.SignedOf(Resource.Credits, 3)); Assert.Equal("-3", Money.SignedOf(Resource.Manpower, -3));
        Assert.Equal("1 credit", Money.Coin(1)); Assert.Equal("14 credits", Money.Coin(14));
    }

    [Fact]
    public void Single_credits_break_a_hundred_and_make_one()
    {
        var t = new Resources { Credits = 28 }; Assert.Equal(2800, t.Total); Assert.Equal("2,800", Money.Held(t));
        t.AddCoin(-6); Assert.Equal(27, t.Credits); Assert.Equal(94, t.Coin); Assert.Equal(2794, t.Total); Assert.Equal("2,794", Money.Held(t));
        t.AddCoin(-94); Assert.Equal(27, t.Credits); Assert.Equal(0, t.Coin);
        t.AddCoin(250); Assert.Equal(29, t.Credits); Assert.Equal(50, t.Coin);
        // below nought the coin still runs 0 to 99, and the total is right
        var poor = new Resources { Credits = 0 }; poor.AddCoin(-5); Assert.Equal(-1, poor.Credits); Assert.Equal(95, poor.Coin); Assert.Equal(-5, poor.Total);
        // the coin is not one of the resources a rule walks, and it is kept by a copy and by a save
        Assert.DoesNotContain(Resource.Coin, Resource.All); Assert.Equal(94, new Resources { Credits = 3, Coin = 94 }.Clone().Coin);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 5); var house = w.House(w.Protagonist!.HouseId)!; house.Treasury.AddCoin(-7);
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(w)).House(house.Id)!.Treasury; Assert.Equal(house.Treasury.Credits, back.Credits); Assert.Equal(93, back.Coin); Assert.Equal(house.Treasury.Total, back.Total);
    }

    [Fact]
    public void A_shortfall_is_said_in_credits()
    {
        var t = new Resources { Credits = 2, Materials = 1 }; var cost = new Resources { Credits = 5, Materials = 4 };
        Assert.Equal("300 credits and 3 materials short", t.Short(cost));
    }
}
