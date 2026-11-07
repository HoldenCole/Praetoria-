using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

public class GreatCrisisTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario = "academy_crucible", ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }

    [Fact]
    public void TheArchetype_Loads_WithOrderedStages()
    {
        var (content, _) = Fresh();
        Assert.NotNull(content.GreatCrisis);
        Assert.Equal("war_of_the_reach", content.GreatCrisis!.Id);
        Assert.Equal(5, content.GreatCrisis.Stages.Count);
        Assert.Equal("blocs", content.GreatCrisis.Stages[1].Behavior);
        Assert.Equal(2.0, content.GreatCrisis.IntensityOf("dominant"));
        Assert.Equal(0, content.GreatCrisis.IntensityOf("off"));
    }

    [Fact]
    public void TheClock_WaitsForTheStartTurn_AndIntensityOffFreezesIt()
    {
        var (content, w) = Fresh();
        var sys = new GreatCrisisSystem(content.GreatCrisis, new FleetSystem(content.Fleets));
        w.Turn = 5; sys.Tick(w, TestContent.Rng());
        Assert.Equal(0, w.Counter(GreatCrisisSystem.ClockCounter));
        w.Turn = 10; sys.Tick(w, TestContent.Rng());
        Assert.True(w.Counter(GreatCrisisSystem.ClockCounter) >= 10);
        Assert.True(w.HasFlag(GreatCrisisSystem.StageFlag("rumblings")));
        var (_, w2) = Fresh(); w2.CrisisIntensity = "off"; w2.Turn = 30;
        sys.Tick(w2, TestContent.Rng());
        Assert.Equal(0, w2.Counter(GreatCrisisSystem.ClockCounter));
    }

    [Fact]
    public void Turmoil_HurriesTheClock()
    {
        var (content, w) = Fresh();
        var fleets = new FleetSystem(content.Fleets);
        var sys = new GreatCrisisSystem(content.GreatCrisis, fleets);
        w.Turn = 10; sys.Tick(w, TestContent.Rng()); int calm = w.Counter(GreatCrisisSystem.ClockCounter);
        var (_, w2) = Fresh(); w2.Turn = 10; fleets.DeclareWar(w2, "corwin", "drake"); w2.WorldCounters["coalition_pressure"] = 4;
        sys.Tick(w2, TestContent.Rng());
        Assert.True(w2.Counter(GreatCrisisSystem.ClockCounter) > calm);
    }

    [Fact]
    public void Blocs_SortByDispositionTowardTheHegemon_AndBlocWarFollows()
    {
        var (content, w) = Fresh();                                   // Corwin: 2 hulls → hegemon
        var fleets = new FleetSystem(content.Fleets);
        var sys = new GreatCrisisSystem(content.GreatCrisis, fleets);
        w.Relationship("drake_sela", "corwin_lucan").Disposition = 30;   // Drake likes Lucan → hegemon bloc
        sys.FormBlocs(w);
        Assert.Equal(GreatCrisisSystem.HegemonBloc, w.House("corwin")!.Bloc);
        Assert.Equal(GreatCrisisSystem.HegemonBloc, w.House("drake")!.Bloc);
        Assert.Equal(GreatCrisisSystem.AllianceBloc, w.House("vega")!.Bloc);    // Marcus −5 toward Lucan
        Assert.Equal(GreatCrisisSystem.AllianceBloc, w.House("sato")!.Bloc);
        sys.BlocWar(w);
        Assert.True(w.AtWar("corwin", "vega")); Assert.True(w.AtWar("drake", "sato")); Assert.False(w.AtWar("corwin", "drake"));
    }

    [Fact]
    public void Resolution_EndsTheWars_AndBirthsAnImperium()
    {
        var (content, w) = Fresh();
        var fleets = new FleetSystem(content.Fleets);
        var sys = new GreatCrisisSystem(content.GreatCrisis, fleets);
        sys.FormBlocs(w); sys.BlocWar(w);
        w.Fleets["fleet_corwin"].Strength = 8;                        // the hegemon bloc wins on hulls
        sys.Resolve(w);
        Assert.Empty(w.Wars);
        Assert.True(w.HasFlag(GreatCrisisSystem.ResolvedFlag));
        Assert.Equal("imperium", w.Era);
        Assert.Equal("emperor", w.House("corwin")!.Title);
        Assert.True(w.HasFlag("great_crisis:player_lost"));
        Assert.All(w.Houses.Values, h => Assert.Equal("", h.Bloc));
    }

    [Fact]
    public void TheWholeArc_RunsThroughTheTurnLoop_AndResolves()
    {
        var (content, w) = Fresh("the_imperial_court");
        w.CrisisIntensity = "dominant";
        var tc = new TurnController(w, content);
        for (int t = 0; t < 60 && !w.HasFlag(GreatCrisisSystem.ResolvedFlag); t++)
        {
            foreach (var i in tc.BeginTurn()) { var c = tc.Offer(i).FirstOrDefault(x => x.Available); if (c != null) tc.Resolve(i, c.Choice.Id); }
            tc.EndTurn();
        }
        Assert.True(w.HasFlag(GreatCrisisSystem.StageFlag("war")), "the war stage should have been reached");
        Assert.True(w.HasFlag(GreatCrisisSystem.ResolvedFlag), "a dominant-intensity crisis should resolve within 60 turns");
        Assert.Contains(w.History, h => h.Text.Contains("THE WAR OF THE REACH"));
    }

    [Fact]
    public void StageEvents_FireOnTheirFlags_AndTheBlocsScene_LetsThePlayerChooseASide()
    {
        var (content, w) = Fresh();
        var fleets = new FleetSystem(content.Fleets);
        var sys = new GreatCrisisSystem(content.GreatCrisis, fleets);
        sys.FormBlocs(w);
        w.WorldFlags.Add(GreatCrisisSystem.StageFlag("alliances"));
        var engine = new EventEngine(content, WorldBuilder.RngFor(w));
        var ev = engine.GatherEligible(w).First(e => e.def.Id == "the_blocs_form");
        Assert.Equal("corwin_lucan", ev.binding.Resolve("hegemon"));
        Assert.Equal(GreatCrisisSystem.AllianceBloc, w.House("vega")!.Bloc);
        engine.Resolve(new FiredEvent(ev.def, ev.binding), w, "stand_with_the_hegemon");
        Assert.Equal(GreatCrisisSystem.HegemonBloc, w.House("vega")!.Bloc);
    }
}
