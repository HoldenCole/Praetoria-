using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>Salaries on trial (docs/ECONOMY_REVIEW.md, "Trials"): schemes of pay for crews, officials, governors, admirals and staff,
/// off in every game that is played and switched on only to be measured.</summary>
public class PayrollTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (TurnController tc, World w, House house) Start()
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), "the_old_lion"), 5); return (new TurnController(w, Content), w, w.House(w.Protagonist!.HouseId)!);
    }

    [Fact]
    public void Nobody_is_paid_unless_a_world_asks_for_a_scheme()
    {
        var (_, w, house) = Start(); Assert.Equal("", w.Payroll); long had = house.Treasury.Total;
        PayrollSystem.Tick(w, Content.ShipClasses); Assert.Equal(had, house.Treasury.Total); Assert.Null(PayrollSystem.Of("")); Assert.Null(PayrollSystem.Of("no such scheme"));
        Assert.Equal("", SaveSystem.Deserialize(SaveSystem.Serialize(w)).Payroll);
    }

    [Fact]
    public void A_scheme_bills_a_house_for_its_crews_officials_governors_and_staff_in_single_credits()
    {
        var (_, w, house) = Start(); var light = PayrollSystem.Of("light")!; var heavy = PayrollSystem.Of("heavy")!;
        var bill = PayrollSystem.Bill(w, Content.ShipClasses, house, light); Assert.Contains(bill, l => l.what == "crews" && l.credits > 0); Assert.Contains(bill, l => l.what == "officials" && l.credits > 0);
        int lightSum = bill.Sum(l => l.credits), heavySum = PayrollSystem.Bill(w, Content.ShipClasses, house, heavy).Sum(l => l.credits);
        Assert.InRange(lightSum, 1, 300); Assert.True(heavySum > 2 * lightSum, $"{lightSum} {heavySum}");
        // crews are paid by the manpower their hulls were laid down with, and a laid-up fleet pays a third
        var fleet = w.Fleets.Values.First(f => f.OwnerId == house.Id); int crews = bill.First(l => l.what == "crews").credits;
        int manpower = fleet.Ships.Sum(kv => kv.Value * Content.ShipClasses.Resolve(kv.Key).Cost.Manpower); Assert.Equal(manpower * light.CrewPerManpower, crews);
        fleet.LaidUp = true; Assert.Equal(crews / 3, PayrollSystem.Bill(w, Content.ShipClasses, house, light).First(l => l.what == "crews").credits);
    }

    [Fact]
    public void Under_a_scheme_every_house_pays_at_the_years_end_out_of_a_treasury_kept_in_hundreds()
    {
        var (tc, w, house) = Start(); w.Payroll = "standard"; long had = house.Treasury.Total; int owed = PayrollSystem.Bill(w, Content.ShipClasses, house, PayrollSystem.Of("standard")!).Sum(l => l.credits);
        PayrollSystem.Tick(w, Content.ShipClasses); Assert.Equal(had - owed, house.Treasury.Total); Assert.Equal(owed, w.Counter(PayrollSystem.LastKey(house.Id))); Assert.InRange(house.Treasury.Coin, 0, 99);
        Assert.All(w.Houses.Values.Where(h => !World.Outsider(h.Id) && Economy.HeadOf(w, h) != null), h => Assert.True(w.WorldCounters.ContainsKey(PayrollSystem.LastKey(h.Id)), h.Id));
        // and a whole year turns with it on
        foreach (var i in tc.BeginTurn()) { var ch = tc.Offer(i).FirstOrDefault(x => x.Available); if (ch != null) tc.Resolve(i, ch.Choice.Id); }
        tc.EndTurn(); Assert.Equal("standard", SaveSystem.Deserialize(SaveSystem.Serialize(w)).Payroll);
    }
}
