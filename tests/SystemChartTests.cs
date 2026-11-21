using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>The close map of a solar system (docs/SYSTEMS.md, "The close map"): where each thing is, and that it is the same wherever it is asked.</summary>
public class SystemChartTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (World w, TurnController tc, House house, Character me) Start(int seed = 7, string scenario = "the_old_lion")
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!;
        return (w, tc, w.House(me.HouseId)!, me);
    }
    private static double Dist(double x, double y) => Math.Sqrt(x * x + y * y);
    private static double Apart(double a, double b) { double d = Math.Abs(a - b) % (2 * Math.PI); return Math.Min(d, 2 * Math.PI - d); }

    [Fact]
    public void Every_system_has_a_chart_with_all_that_goes_round_its_sun_each_on_its_own_orbit()
    {
        var (w, tc, _, _) = Start(); tc.BeginTurn();
        Assert.Null(SystemCharts.For(w, "no_such_system"));
        foreach (var sys in w.Galaxy!.Systems.Values)
        {
            var chart = SystemCharts.For(w, sys.Id, Content.Holdings)!;
            // everything but the sun and open space is on it, once, under its own place in the system's list
            var drawn = sys.Bodies.Select((b, i) => (b, i)).Where(x => !x.b.IsStar && !x.b.IsSpace).Select(x => x.i).ToList();
            Assert.Equal(drawn.OrderBy(i => i), chart.Bodies.Select(b => b.Index).OrderBy(i => i));
            Assert.All(chart.Bodies, b => { Assert.Equal(sys.Bodies[b.Index].Name, b.Name); Assert.Equal(sys.Bodies[b.Index].Class, b.Class); });
            Assert.Contains(chart.Bodies, b => b.Primary);
            // what goes round the sun: each further out than the last, by a step at least, the near kinds of world first
            var round = chart.Bodies.Where(b => b.Parent < 0 && b.Class != Body.Comet).OrderBy(b => b.Rank).ToList();
            for (int k = 1; k < round.Count; k++)
            {
                Assert.True(round[k].Orbit >= round[k - 1].Orbit + SystemCharts.Step - 0.01, $"{sys.Name}: {round[k].Name} is not a step beyond {round[k - 1].Name}");
                Assert.True(SystemCharts.Zone(round[k].Class) >= SystemCharts.Zone(round[k - 1].Class), $"{sys.Name}: {round[k].Name} lies inside a warmer world");
                Assert.True(round[k].Period > round[k - 1].Period);
            }
            // each is where its orbit and its angle put it, and inside the lanes
            Assert.All(round, b => { Assert.InRange(Dist(b.X, b.Y), b.Orbit - 0.1, b.Orbit + 0.1); Assert.InRange(b.X, b.Orbit * Math.Cos(b.Angle) - 0.1, b.Orbit * Math.Cos(b.Angle) + 0.1); Assert.True(b.Orbit < chart.Edge - 20); });
            // a moon is beside its world: nearer to it than its world's orbit is to the next one's
            foreach (var moon in chart.Bodies.Where(b => b.Parent >= 0))
            {
                var world = chart.Body(moon.Parent)!; Assert.InRange(Dist(moon.X - world.X, moon.Y - world.Y), moon.Orbit - 0.1, moon.Orbit + 0.1);
                var others = round.Where(o => o.Index != world.Index).ToList();
                if (others.Count > 0) Assert.True(moon.Orbit < others.Min(o => Math.Abs(o.Orbit - world.Orbit)), $"{sys.Name}: {moon.Name} reaches another world's orbit");
            }
            // a comet keeps inside the lanes and comes nearer the sun than its mean distance
            foreach (var comet in chart.Bodies.Where(b => b.Class == Body.Comet)) { Assert.True(Dist(comet.X, comet.Y) <= chart.Edge); Assert.True(comet.Eccentricity > 0.5); }
        }
    }

    [Fact]
    public void A_chart_is_the_same_in_every_game_and_a_year_moves_each_thing_round_the_near_ones_fastest()
    {
        var (w, tc, _, _) = Start(7); tc.BeginTurn(); var (w2, tc2, _, _) = Start(99, "the_new_man"); tc2.BeginTurn();
        string id = w.Galaxy!.Systems.Values.First(s => s.Bodies.Count(b => b.Orbits < 0 && !b.IsStar && !b.IsSpace && b.Class != Body.Comet) >= 3 && w2.Galaxy!.System(s.Id) != null).Id;
        var a = SystemCharts.For(w, id)!; var b = SystemCharts.For(w2, id)!;
        Assert.Equal(w.Turn, w2.Turn);
        // the same sun, the same worlds in the same places, the same lanes out: whichever game it is
        Assert.Equal(a.Bodies.Select(x => (x.Index, x.Name, x.Orbit, x.Angle, x.X, x.Y)), b.Bodies.Select(x => (x.Index, x.Name, x.Orbit, x.Angle, x.X, x.Y)));
        Assert.Equal(a.Exits.Select(x => (x.SystemId, x.Angle)), b.Exits.Select(x => (x.SystemId, x.Angle)));
        Assert.Equal(a.SunTint, b.SunTint); Assert.Equal(a.Edge, b.Edge);
        // asked twice it is the same chart
        var again = SystemCharts.For(w, id)!; Assert.Equal(a.Bodies, again.Bodies); Assert.Equal(a.Exits, again.Exits);
        // a year on: every orbit as it was, every world moved along it, and the nearer the further (a year is a larger part of a short one)
        tc.EndTurn(); tc.BeginTurn(); var next = SystemCharts.For(w, id)!;
        Assert.Equal(a.Year + 1, next.Year);
        var round = a.Bodies.Where(x => x.Parent < 0 && x.Class != Body.Comet).OrderBy(x => x.Rank).ToList();
        double lastMove = double.MaxValue;
        foreach (var was in round)
        {
            var now = next.Body(was.Index)!; Assert.Equal(was.Orbit, now.Orbit);
            double moved = now.Angle - was.Angle; Assert.InRange(moved, 2 * Math.PI / was.Period - 0.001, 2 * Math.PI / was.Period + 0.001);
            Assert.True(moved < lastMove); lastMove = moved;
            Assert.NotEqual((was.X, was.Y), (now.X, now.Y));
        }
        Assert.Equal(3, SystemCharts.PeriodOf(0)); Assert.True(SystemCharts.PeriodOf(5) > 20);
    }

    [Fact]
    public void The_lanes_leave_toward_the_neighbours_they_lead_to()
    {
        var (w, tc, _, _) = Start(); tc.BeginTurn(); var g = w.Galaxy!;
        foreach (var sys in g.Systems.Values)
        {
            var chart = SystemCharts.For(w, sys.Id)!; var neighbours = g.Neighbors(sys.Id).ToList();
            Assert.Equal(neighbours.OrderBy(n => n), chart.Exits.Select(e => e.SystemId).OrderBy(n => n));
            foreach (var e in chart.Exits)
            {
                var n = g.System(e.SystemId)!; double toward = Math.Atan2(-(n.Y - sys.Y), n.X - sys.X);
                // at the edge of the system, in the direction the neighbour lies on the galaxy map, give or take the room made between two lanes
                Assert.InRange(Dist(e.X, e.Y), chart.Edge - 0.1, chart.Edge + 0.1);
                Assert.True(Apart(toward, e.Angle) <= SystemCharts.ExitGap * 1.6, $"{sys.Name} → {n.Name}: drawn {e.Angle:F2}, lies {toward:F2}");
                Assert.Equal(n.Name, e.Name); Assert.Equal(w.PlanetHolder(n.Id) ?? "", e.HolderId); Assert.Equal(g.RingOf(n.Id), e.Ring);
            }
            // no two lanes are drawn one on top of another
            var angles = chart.Exits.Select(e => e.Angle).OrderBy(x => x).ToList();
            for (int k = 0; k < angles.Count; k++) for (int j = k + 1; j < angles.Count; j++) Assert.True(Apart(angles[k], angles[j]) >= SystemCharts.ExitGap - 0.02, $"{sys.Name}: two lanes {Apart(angles[k], angles[j]):F2} apart");
        }
        // the rule that spreads them, by itself
        Assert.Equal(new[] { 1.0, 2.0 }, SystemCharts.Spread(new[] { 1.0, 2.0 }, 0.3));
        var close = SystemCharts.Spread(new[] { 1.0, 1.05, 1.1 }, 0.3);
        Assert.True(close[1] - close[0] >= 0.299 && close[2] - close[1] >= 0.299); Assert.Equal(1.05, close[1], 2);
        var many = SystemCharts.Spread(Enumerable.Range(0, 30).Select(k => k * 0.01).ToList(), 0.3); Assert.Equal(30, many.Distinct().Count());
    }

    [Fact]
    public void Who_is_in_a_system_is_on_its_chart_where_they_lie()
    {
        var (w, tc, house, me) = Start(); tc.BeginTurn(); var g = w.Galaxy!;
        var mine = w.Fleets.Values.First(f => f.OwnerId == house.Id && !f.Office); string home = mine.SystemId;
        Assert.Equal(house.Id, w.PlanetHolder(home));
        var chart = SystemCharts.For(w, home, Content.Holdings)!; var world = chart.Bodies.Single(b => b.Primary);
        Assert.True(chart.IsPlayer); Assert.Equal(house.Id, chart.HolderId); Assert.Equal(house.Name, chart.HolderName);
        // the holder's fleet lies off the system's world, outside its moons
        var f = chart.Fleets.Single(x => x.FleetId == mine.Id);
        Assert.True(f.IsPlayer); Assert.Equal(world.Index, f.AtBody); Assert.Equal(mine.Strength, f.Strength); Assert.False(f.Moving); Assert.False(f.LaidUp);
        double off = Dist(f.X - world.X, f.Y - world.Y); Assert.InRange(off, SystemCharts.MoonOrbit, SystemCharts.Step);
        foreach (var moon in chart.Bodies.Where(b => b.Parent == world.Index)) Assert.True(off > moon.Orbit);
        // laid up, it says so
        mine.LaidUp = true; Assert.True(SystemCharts.For(w, home)!.Fleets.Single(x => x.FleetId == mine.Id).LaidUp); mine.LaidUp = false;
        // ordered away, it is drawn at the lane it is making for: the first on its road
        string far = g.Systems.Keys.Where(s => g.Hops(home, s) >= 2).OrderBy(s => s, StringComparer.Ordinal).First();
        var ctx = new CommandContext(w, WorldBuilder.RngFor(w)); var pools = ctx.PoolsOf(me.Id); pools.Treasury = 6;
        Assert.True(tc.Executor.TryExecute(new MoveFleetCommand(me.Id, mine.Id, far, Content.Fleets), ctx));
        var away = SystemCharts.For(w, home)!.Fleets.Single(x => x.FleetId == mine.Id); var chartNow = SystemCharts.For(w, home)!;
        Assert.True(away.Moving); Assert.Equal(-1, away.AtBody); Assert.Equal(g.Path(home, far)[1], away.ExitTo);
        var gate = chartNow.Exits.Single(e => e.SystemId == away.ExitTo); Assert.True(Dist(away.X - gate.X, away.Y - gate.Y) < chartNow.Edge * 0.15);
        // another house's fleet standing here is out in the system, not off the world
        var theirs = w.Fleets.Values.First(x => x.OwnerId != house.Id && !x.Office && !x.Moving && x.OwnerId != PirateSystem.HouseId);
        theirs.SystemId = home; w.Intel[theirs.Id] = new FleetIntel { SystemId = home, Strength = theirs.Strength, Confidence = 80, TurnObserved = w.Turn };
        var guest = SystemCharts.For(w, home)!.Fleets.Single(x => x.FleetId == theirs.Id);
        Assert.False(guest.IsPlayer); Assert.Equal(-1, guest.AtBody); Assert.Equal(80, guest.Confidence); Assert.True(Dist(guest.X, guest.Y) > chartNow.Edge * 0.6);
        // a fleet nobody has reported is not on it
        w.Intel.Remove(theirs.Id); Assert.DoesNotContain(SystemCharts.For(w, home)!.Fleets, x => x.FleetId == theirs.Id);

        // a station lies off the world, and a claim is in the belt
        w.Holdings["zz_station"] = new Holding { Id = "zz_station", OwnerId = house.Id, Name = "The Mole", SystemId = home, Kind = Holding.Station, Specialization = Content.Holdings.Specs.First().Id }; w.HoldingsChanged();
        var post = SystemCharts.For(w, home)!.Posts.Single(p => p.HoldingId == "zz_station"); Assert.Equal(world.Index, post.AtBody); Assert.Equal(Holding.Station, post.Kind); Assert.Equal(house.Name, post.OwnerName);
        var belted = g.Systems.Values.First(s => s.Bodies.Any(b => b.Class == Body.Belt));
        w.Holdings["zz_claim"] = new Holding { Id = "zz_claim", OwnerId = house.Id, Name = "A Claim", SystemId = belted.Id, Kind = Holding.Claim, Specialization = StakeClaimCommand.Spec }; w.HoldingsChanged();
        var bchart = SystemCharts.For(w, belted.Id)!; var claim = bchart.Posts.Single(p => p.HoldingId == "zz_claim"); var belt = bchart.Bodies.Single(b => b.Class == Body.Belt && b.Index == claim.AtBody);
        Assert.Equal(Holding.Claim, claim.Kind); Assert.InRange(Dist(claim.X, claim.Y), belt.Orbit - 0.1, belt.Orbit + 0.1);

        // what is built on a body is counted on it, and a thing rising is counted as rising
        var sys = g.System(home)!; int moonOrWorld = world.Index;
        // the system's own world counts what stands on the holding itself as built there, with what is built round it
        int onHolding = w.PlanetOf(home)!.Buildings.Count; Assert.True(onHolding > 0);
        Assert.Equal(onHolding, chart.Bodies.Single(b => b.Index == moonOrWorld).Works);
        Assert.All(chart.Bodies.Where(b => !b.Primary), b => Assert.Equal(SystemWorks.In(w, home).Count(x => x.Body == b.Index), b.Works));
        var def = Content.Holdings.Works.First(d => !d.IsMegastructure); var great = Content.Holdings.Works.First(d => d.IsMegastructure);
        w.BodyWorks.Add(new BodyWork { Id = "zz_w", SystemId = home, Body = moonOrWorld, DefId = def.Id, BuiltBy = house.Id });
        var built = SystemCharts.For(w, home, Content.Holdings)!.Bodies.Single(b => b.Index == moonOrWorld); Assert.Equal(onHolding + 1, built.Works); Assert.False(built.GreatWork);
        w.BodyWorks.Add(new BodyWork { Id = "zz_g", SystemId = home, Body = moonOrWorld, DefId = great.Id, BuiltBy = house.Id });
        Assert.True(SystemCharts.For(w, home, Content.Holdings)!.Bodies.Single(b => b.Index == moonOrWorld).GreatWork);
        w.Projects.Add(new Project { Id = "zz_p", OwnerId = house.Id, Kind = Project.Work, DefId = def.Id, TargetId = SystemWorks.Target(home, moonOrWorld), Name = "x" });
        Assert.Equal(1, SystemCharts.For(w, home)!.Bodies.Single(b => b.Index == moonOrWorld).Rising);
        int sun = sys.Bodies.FindIndex(b => b.IsStar); Assert.False(chart.SunWorks);
        w.BodyWorks.Add(new BodyWork { Id = "zz_s", SystemId = home, Body = sun, DefId = great.Id }); Assert.True(SystemCharts.For(w, home)!.SunWorks);
    }
}
