using Praetoria.Core.Commands;
using Praetoria.Core.Crises;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>
/// THE Milestone-5 flagship acceptance (GDD §16): a crisis can be engineered or defused via gates;
/// a cascade escalates and a damper (earned by prior play) arrests it; an NPC house triggers a
/// crisis. Exercised against the authored <c>human_crises.json</c> through the real loader.
/// </summary>
public class CrisisTests
{
    private static (CrisisEngine engine, World world, IRng rng) Setup(int credits = 10)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        Assert.NotEmpty(content.Crises);                 // crises actually loaded
        var world = TestContent.EconomyWorld(credits);   // self + House Vega + pools + a holding
        return (new CrisisEngine(content.Crises), world, new SplitMix64Rng(1));
    }

    private static CrisisDef Def(CrisisEngine e, string id) => e.Def(id)!;

    [Fact]
    public void Crisis_IsNotCausable_UntilItsGateClears()
    {
        var (e, w, rng) = Setup();
        Assert.False(e.IsCausable(Def(e, "local_revolt"), w, rng));   // unrest 0
        w.WorldCounters["unrest"] = 5;
        Assert.True(e.IsCausable(Def(e, "local_revolt"), w, rng));    // gate cleared
    }

    [Fact]
    public void Trigger_ActivatesTheCrisis_AndAppliesItsOnTriggerWrites()
    {
        var (e, w, rng) = Setup();
        w.WorldCounters["unrest"] = 5;

        e.Trigger(Def(e, "local_revolt"), w, rng, authorId: "self");

        Assert.True(w.IsCrisisActive("local_revolt"));
        Assert.True(w.HasFlag(World.CrisisFlag("local_revolt")));
        Assert.True(w.HasFlag("realm_unstable"));                     // onTrigger write
        Assert.Equal(7, w.Counter("unrest"));                        // 5 + 2
        Assert.Equal(-2, w.Counter("legitimacy"));                   // 0 - 2
        Assert.False(w.Crisis("local_revolt")!.Organic);            // authored origin recorded
    }

    [Fact]
    public void Cascade_BrutalSuppressionOfARevolt_ClearsTheCivilWarGate()
    {
        var (e, w, rng) = Setup();
        w.WorldCounters["unrest"] = 5;

        // The civil war is impossible from a standing start...
        Assert.False(e.IsCausable(Def(e, "civil_war"), w, rng));

        e.Trigger(Def(e, "local_revolt"), w, rng);                   // unrest→7, legit→-2, realm_unstable
        Assert.False(e.IsCausable(Def(e, "civil_war"), w, rng));     // legit only -2

        var revolt = Def(e, "local_revolt");
        var crush = revolt.Dampers.First(d => d.Id == "crush_it");
        e.ApplyDamper(revolt, crush, w, rng);                        // crush: unrest→8, legit→-3

        // ...but suppressing the small crisis armed the large one — unscripted cascade.
        Assert.False(w.IsCrisisActive("local_revolt"));             // crushed (severity spent)
        Assert.True(e.IsCausable(Def(e, "civil_war"), w, rng));     // realm_unstable + legit≤-3 + unrest≥6
    }

    [Fact]
    public void Damper_IsAvailableOnly_WhenPriorPlayBankedGoodwill()
    {
        var (e, w, rng) = Setup();
        var cw = Def(e, "civil_war");
        // Stand the civil war up directly.
        w.WorldCounters["unrest"] = 8; w.WorldCounters["legitimacy"] = -3; w.WorldFlags.Add("realm_unstable");
        e.Trigger(cw, w, rng);

        Assert.Empty(e.AvailableDampers(cw, w, rng));               // goodwill 0 — no rally available
        w.WorldCounters["goodwill"] = 4;                            // a ruler who kept faith with the people
        Assert.Contains(e.AvailableDampers(cw, w, rng), d => d.Id == "rally_loyalists");
    }

    [Fact]
    public void Damper_ReducesSeverity_AndResolvesTheCrisisAtZero()
    {
        var (e, w, rng) = Setup();
        var cw = Def(e, "civil_war");
        w.WorldCounters["unrest"] = 8; w.WorldCounters["legitimacy"] = -3; w.WorldFlags.Add("realm_unstable");
        w.WorldCounters["goodwill"] = 4;
        e.Trigger(cw, w, rng);
        Assert.Equal(3, w.Crisis("civil_war")!.Severity);

        var rally = cw.Dampers.First(d => d.Id == "rally_loyalists");
        e.ApplyDamper(cw, rally, w, rng);                            // relief 3 == severity 3

        Assert.False(w.IsCrisisActive("civil_war"));               // resolved
        Assert.False(w.HasFlag(World.CrisisFlag("civil_war")));
    }

    [Fact]
    public void NpcHouse_AuthorsACausableCrisis_ThroughTheCommandBus()
    {
        var (e, w, rng) = Setup();
        w.WorldCounters["unrest"] = 5;                               // local_revolt is causable
        w.Characters["rival"] = new Character
        {
            Id = "rival", Name = "Rival", HouseId = "vega", Alive = true, Ambition = "seize_the_throne"
        };
        w.Houses["vega"].Members.Add("rival");
        w.Pools["rival"] = ActionPools.ForNpc();

        var exec = new CommandExecutor();
        new NpcAi().Act(exec, new CommandContext(w, rng), e);

        Assert.True(w.IsCrisisActive("local_revolt"));
        Assert.Contains(exec.Log, l => l.Contains("rival") && l.Contains("trigger crisis local_revolt"));
        Assert.Equal("rival", w.Crisis("local_revolt")!.AuthorId);  // NPC-authored
    }

    [Fact]
    public void TriggerCommand_IsRejected_WhenTheCrisisIsNotCausable()
    {
        var (e, w, rng) = Setup();
        // unrest 0 — local_revolt's gate is shut.
        var cmd = new TriggerCrisisCommand("self", e, Def(e, "local_revolt"));
        Assert.False(cmd.CanExecute(new CommandContext(w, rng)));
        Assert.False(new CommandExecutor().TryExecute(cmd, new CommandContext(w, rng)));
        Assert.False(w.IsCrisisActive("local_revolt"));
    }
}
