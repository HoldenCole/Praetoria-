using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

public class ShipClassAndAdmiralTests
{
    private static (ContentDatabase content, World world, FleetSystem fleets) Fresh(string scenario = "academy_crucible", ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed);
        return (content, w, new FleetSystem(content.Fleets, content.Holdings, content.ShipClasses, content.Friction));
    }

    [Fact]
    public void ShipClasses_Load_AndAFleetAuthoredByStrength_IsFrigates()
    {
        var (content, w, fleets) = Fresh();
        Assert.Equal(5, content.ShipClasses.Defs.Count);
        Assert.Equal(8, content.ShipClasses.ById("battleship")!.Attack);
        var comp = fleets.Composition(w.Fleets["fleet_corwin"]).ToList();
        Assert.Single(comp);
        Assert.Equal(("frigate", 2), (comp[0].cls.Id, comp[0].count));
        Assert.Equal("2 frigates", fleets.Describe(w.Fleets["fleet_corwin"]));
    }

    [Fact]
    public void HeavierHulls_HitHarder_AndCostMoreUpkeep()
    {
        var (content, w, fleets) = Fresh();
        var f = w.Fleets["fleet_vega"];
        f.Ships["frigate"] = 2; f.Recount();
        double frigates = fleets.Power(w, "vega", "tessaly", defending: false);
        f.Ships.Clear(); f.Ships["cruiser"] = 2; f.Recount();
        Assert.True(fleets.Power(w, "vega", "tessaly", defending: false) > frigates * 1.5);
        Assert.Equal(4, fleets.Upkeep(f).Credits);                             // 2 cruisers × 2
    }

    [Fact]
    public void Raising_RespectsTitleRank_AndSpecialization()
    {
        var (content, w, _) = Fresh();
        var ctx = new CommandContext(w, TestContent.Rng());
        var house = w.House("vega")!; house.Treasury.Materials = 100; house.Treasury.Manpower = 50; house.Treasury.Credits = 50; house.Treasury.Exotics = 5;
        Assert.Null(new RaiseFleetCommand(w.ProtagonistId, "vega_barony", content.Fleets, content.ShipClasses, "cruiser", content.Titles).Refusal(ctx));      // baron = rank 2
        Assert.Equal("needs title rank 3", new RaiseFleetCommand(w.ProtagonistId, "vega_barony", content.Fleets, content.ShipClasses, "battleship", content.Titles).Refusal(ctx));
        Assert.Equal("needs a shipyard_world", new RaiseFleetCommand(w.ProtagonistId, "vega_barony", content.Fleets, content.ShipClasses, "carrier", content.Titles).Refusal(ctx));
        house.Title = "duke";
        Assert.Null(new RaiseFleetCommand(w.ProtagonistId, "vega_barony", content.Fleets, content.ShipClasses, "battleship", content.Titles).Refusal(ctx));
    }

    [Fact]
    public void Losses_FallOnTheScreen_First()
    {
        var (content, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "corwin");
        var vega = w.Fleets["fleet_vega"]; vega.Ships["corvette"] = 4; vega.Ships["battleship"] = 1; vega.Recount();
        w.Fleets["fleet_corwin"].Strength = 1; w.Fleets["fleet_corwin"].SystemId = "tessaly";
        w.Fleets["fleet_corwin"].Readiness = 10;                                 // a token attacker that will lose and barely scratch
        fleets.ResolveBattles(w, new SplitMix64Rng(2));
        var r = fleets.LastBattles[0];
        Assert.Equal("vega", r.Winner);
        Assert.Equal(1, vega.Ships["battleship"]);                               // the capital ship is screened
        Assert.Equal(vega.Ships.Values.Sum(), vega.Strength);
    }

    [Fact]
    public void AnAdmiral_MultipliesPower_ByTheirSkill_AndUnledFleetsFightWorse()
    {
        var (content, w, fleets) = Fresh();
        var f = w.Fleets["fleet_vega"];
        double unled = fleets.Power(w, "vega", "tessaly", defending: false);
        f.AdmiralId = "vega_marcus";                                              // tactics 1, Strategist
        double led = fleets.Power(w, "vega", "tessaly", defending: false);
        Assert.True(led > unled);
        Assert.Equal(0.85, FleetSystem.AdmiralFactor(w, new Fleet()));
        w.Char("vega_marcus")!.Skills["tactics"] = 6; w.Char("vega_marcus")!.AptitudeTraits.Add("Tactician");
        Assert.True(fleets.Power(w, "vega", "tessaly", defending: false) > led);
        w.Char("vega_marcus")!.Flags.Add("wounded");
        Assert.True(FleetSystem.AdmiralFactor(w, f) < 1.0 + 6 * 0.05 + 0.10 + 0.05 + 0.0001 - 0.10 + 0.0001);
    }

    [Fact]
    public void AssignAdmiral_IsACommand_AndFlagsTheCharacter()
    {
        var (content, w, _) = Fresh();
        var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new AssignAdmiralCommand(w.ProtagonistId, "fleet_vega", "vega_marcus");
        Assert.True(cmd.CanExecute(ctx));
        new CommandExecutor().TryExecute(cmd, ctx);
        Assert.Equal("vega_marcus", w.Fleets["fleet_vega"].AdmiralId);
        Assert.Contains(FleetSystem.AdmiralFlag, w.Char("vega_marcus")!.Flags);
        Assert.False(new AssignAdmiralCommand(w.ProtagonistId, "fleet_corwin", "vega_marcus").CanExecute(ctx));   // not our fleet
    }

    [Fact]
    public void Victory_TeachesTheAdmiral_AndSeasonsTheFleet()
    {
        var (content, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "corwin");
        var vega = w.Fleets["fleet_vega"]; vega.Ships["battleship"] = 3; vega.Recount(); vega.AdmiralId = "vega_marcus";
        int skills = w.Char("vega_marcus")!.Skill("tactics") + w.Char("vega_marcus")!.Skill("gunnery");
        w.Fleets["fleet_corwin"].SystemId = "tessaly"; w.Fleets["fleet_corwin"].Readiness = 5;
        fleets.ResolveBattles(w, new SplitMix64Rng(4));
        Assert.Equal("vega", fleets.LastBattles[0].Winner);
        Assert.Equal(1, vega.Veterancy);
        Assert.Equal(skills + 1, w.Char("vega_marcus")!.Skill("tactics") + w.Char("vega_marcus")!.Skill("gunnery"));   // the weaker of the two grows
    }

    [Fact]
    public void ALosingAdmiral_IsWounded_OrDies_Deterministically()
    {
        int deaths = 0, wounds = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var (content, w, fleets) = Fresh(seed: seed);
            fleets.DeclareWar(w, "vega", "corwin");
            var corwin = w.Fleets["fleet_corwin"]; corwin.AdmiralId = "corwin_lucan"; corwin.SystemId = "tessaly"; corwin.Readiness = 5;
            w.Fleets["fleet_vega"].Ships["battleship"] = 3; w.Fleets["fleet_vega"].Recount();
            fleets.ResolveBattles(w, new SplitMix64Rng(seed));
            if (fleets.LastBattles[0].Winner != "vega") continue;
            var lucan = w.Char("corwin_lucan")!;
            if (!lucan.Alive) deaths++; else if (lucan.Flags.Contains("wounded")) wounds++;
        }
        Assert.True(wounds > 0, "most defeated admirals are wounded");
        Assert.True(deaths > 0, "some defeated admirals die (admiralDeathChance)");
    }

    [Fact]
    public void Siege_IsLongerForAFortress_AndBlockadeHalvesYield()
    {
        var (content, w, fleets) = Fresh();
        Assert.Equal(2, fleets.SiegeRequired(w, w.Holding("drake_port")!));      // trade hub: no defense
        Assert.Equal(3, fleets.SiegeRequired(w, w.Holding("sato_holdfast")!));   // fortress 0.5 → +1

        fleets.DeclareWar(w, "vega", "drake");
        w.Fleets.Remove("fleet_drake"); w.Fleets["fleet_vega"].SystemId = "drake_crossing";
        fleets.ResolveConquests(w);
        Assert.True(w.Sieges.ContainsKey("drake_port"));
        int before = w.House("drake")!.Treasury.Credits;
        new Economy(content.Holdings).Accrue(w);
        int gained = w.House("drake")!.Treasury.Credits - before;
        Assert.True(gained <= 3, $"a blockaded trade hub should yield about half (got +{gained})");
    }

    [Fact]
    public void WarScore_DrivesTribute_AndPeaceRefusal()
    {
        var (content, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "corwin");
        var war = w.Wars[0];
        war.AddScore("corwin", 6);                                                // Corwin ahead by 6
        Assert.Equal(Math.Min(w.House("vega")!.Treasury.Credits, 6 * content.Fleets.TributePerScore), fleets.Tribute(w, "vega", "corwin"));
        Assert.False(fleets.WouldAcceptPeace(w, "corwin", "vega"));              // ahead ≥5 and not weaker → refuses
        war.AddScore("vega", 6);
        Assert.True(fleets.WouldAcceptPeace(w, "corwin", "vega"));
    }

    [Fact]
    public void TitlesFollowTheLand_WhenAHouseLosesItsLastHolding()
    {
        var (content, w, fleets) = Fresh();
        fleets.DeclareWar(w, "vega", "sato");                                      // Sato: a Knight with one fortress
        w.Fleets.Remove("fleet_sato"); w.Fleets["fleet_vega"].SystemId = "sato_holdfast";
        for (int t = 0; t < 5; t++) fleets.ResolveConquests(w);
        Assert.Equal("vega", w.Holding("sato_holdfast")!.OwnerId);
        Assert.Equal("landless", w.House("sato")!.Title);
        Assert.Contains("knight", w.House("vega")!.Claims);
        Assert.Equal(3, w.Wars[0].Score("vega"));
    }

    [Fact]
    public void Honors_AreInvested_WhenTheirConditionsFirstHold()
    {
        var (content, w, _) = Fresh();
        var honors = new HonorSystem(content.Honors);
        Assert.Equal(7, honors.Defs.Count);
        honors.Apply(w, TestContent.Rng());
        Assert.Empty(honors.Held(w.Protagonist!));
        w.WorldCounters["battles_won"] = 3;
        int legit = w.House("vega")!.Legitimacy;
        honors.Apply(w, TestContent.Rng());
        Assert.Contains(w.Protagonist!.Flags, f => f == HonorSystem.Flag("burning_lance"));
        Assert.Equal(legit + 10, w.House("vega")!.Legitimacy);
        honors.Apply(w, TestContent.Rng());
        Assert.Equal(legit + 10, w.House("vega")!.Legitimacy);                    // once
    }

    [Fact]
    public void Friction_ChargesStress_ByNature_ForTheActsTheEngineKnows()
    {
        var (content, w, fleets) = Fresh();
        var marcus = w.Protagonist!;                                               // Proud
        marcus.NatureTraits.Add("Honorable");
        int before = marcus.Stress;
        new CommandExecutor().TryExecute(new DeclareWarCommand(marcus.Id, "corwin", content.Fleets), new CommandContext(w, TestContent.Rng()));
        // the command's FleetSystem has no friction table; the turn-level one does — apply directly
        Assert.Equal(8, content.Friction.Apply(w, marcus, "declare_war"));
        Assert.Equal(before + 8, marcus.Stress);
        Assert.Equal(8, content.Friction.Apply(w, w.Char("drake_sela"), "plunder"));   // Honorable Sela recoils from plunder
        Assert.Equal(0, content.Friction.Apply(w, w.Char("sato_ren"), "plunder"));     // Ambitious Ren does not
    }

    [Fact]
    public void MergeFleets_FoldsCompositionAndKeepsTheAdmiral()
    {
        var (content, w, _) = Fresh();
        var ctx = new CommandContext(w, TestContent.Rng());
        var a = w.Fleets["fleet_vega"];
        var b = new Fleet { Id = "fleet_vega_2", Name = "Second", OwnerId = "vega", SystemId = "tessaly", Strength = 2, Readiness = 50 };
        b.Ships["cruiser"] = 2; w.Fleets[b.Id] = b;
        a.AdmiralId = "vega_marcus";
        var cmd = new MergeFleetsCommand(w.ProtagonistId, "fleet_vega_2", "fleet_vega");
        Assert.True(cmd.CanExecute(ctx));
        new CommandExecutor().TryExecute(cmd, ctx);
        Assert.False(w.Fleets.ContainsKey("fleet_vega_2"));
        Assert.Equal(3, a.Strength);
        Assert.Equal(2, a.Ships["cruiser"]);
        Assert.Equal(1, a.Ships["frigate"]);
        Assert.Equal("vega_marcus", a.AdmiralId);
    }
}
