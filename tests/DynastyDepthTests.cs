using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

public class DynastyDepthTests
{
    private static (ContentDatabase content, World world) Fresh(string scenario = "the_imperial_court", ulong seed = 1)
    {
        var root = ContentLocator.FindContentDir();
        var content = ContentLoader.LoadFromDirectory(root);
        return (content, WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(root, scenario), seed));
    }

    [Fact]
    public void Children_AreNamedFromTheHousesCulture()
    {
        var (content, w) = Fresh();                                   // Cassian + Elara Thorne, married
        var dyn = new DynastySystem { Content = content };
        var rng = new SplitMix64Rng(3);
        for (int t = 0; t < 12; t++) dyn.Tick(w, rng);
        var child = w.Characters.Values.FirstOrDefault(c => c.Id.StartsWith("scion_"));
        Assert.NotNull(child);
        Assert.EndsWith(" Thorne", child!.Name);
        Assert.DoesNotContain("Scion", child.Name);
        Assert.Contains(child.Name.Split(' ')[0], child.Sex == "male" ? new[] { "Aldous", "Cassian", "Darius", "Eamon", "Felix", "Gaius", "Hadrian", "Idris", "Julian", "Kael", "Lucan", "Marcus", "Nero", "Orrin", "Pell", "Quintus", "Rowan", "Severin", "Tiberius", "Ulric", "Varro", "Wendel" }
                                                                        : new[] { "Adria", "Brisa", "Cassia", "Delia", "Elara", "Fenna", "Galla", "Helia", "Ione", "Junia", "Kessa", "Livia", "Mira", "Nessa", "Octavia", "Petra", "Quilla", "Rhea", "Sela", "Thessaly", "Una", "Vera" });
    }

    [Fact]
    public void AChild_ComesOfAge_WithInheritedTraits_ATrack_AndAnAmbition()
    {
        var (content, w) = Fresh();
        var dyn = new DynastySystem { Content = content };
        var child = new Character { Id = "scion_x", Name = "Test Thorne", HouseId = "thorne", Age = 15, Sex = "male", MotherId = "elara_thorne", FatherId = "cassian_thorne" };
        child.Flags.Add(DynastySystem.HiddenFlag);
        w.Characters[child.Id] = child; w.House("thorne")!.Members.Add(child.Id);

        dyn.Tick(w, new SplitMix64Rng(5));

        Assert.Equal(16, child.Age);
        Assert.Single(child.NatureTraits);
        Assert.Contains(child.NatureTraits.Single(), new[] { "Ambitious", "Honorable", "Loyal" });       // from a parent
        Assert.Contains(child.CareerTrack, new[] { "military", "stewardship", "law" });   // parents differ → drawn from the catalog
        Assert.True(child.Skills.Count >= 2);
        Assert.False(string.IsNullOrEmpty(child.Ambition));
        Assert.DoesNotContain(DynastySystem.HiddenFlag, child.Flags); // home from fostering
        Assert.Contains(DynastySystem.ComeOfAgeFlag, child.Flags);
    }

    [Fact]
    public void SuccessionLaws_PickDifferentHeirs()
    {
        var w = new World { ProtagonistId = "king" };
        w.Houses["h"] = new House { Id = "h", Name = "h" };
        void Add(string id, int age, int skill = 0, string father = "") { w.Characters[id] = new Character { Id = id, Name = id, HouseId = "h", Age = age, FatherId = father, Skills = new() { ["law"] = skill } }; w.Houses["h"].Members.Add(id); }
        Add("king", 70); Add("uncle", 65, 1); Add("elder", 30, 2, "king"); Add("younger", 20, 9, "king");
        var king = w.Char("king")!;
        Assert.Equal("elder", DynastySystem.FindHeir(w, king)!.Id);
        w.Houses["h"].SuccessionLaw = "ultimogeniture"; Assert.Equal("younger", DynastySystem.FindHeir(w, king)!.Id);
        w.Houses["h"].SuccessionLaw = "seniority"; Assert.Equal("uncle", DynastySystem.FindHeir(w, king)!.Id);
        w.Houses["h"].SuccessionLaw = "merit"; Assert.Equal("younger", DynastySystem.FindHeir(w, king)!.Id);
    }

    [Fact]
    public void SetSuccessionLaw_IsACommand_WithAPrice()
    {
        var (content, w) = Fresh();
        var ctx = new CommandContext(w, TestContent.Rng());
        int legit = w.House("thorne")!.Legitimacy;
        Assert.True(new CommandExecutor().TryExecute(new SetSuccessionLawCommand(w.ProtagonistId, "merit"), ctx));
        Assert.Equal("merit", w.House("thorne")!.SuccessionLaw);
        Assert.Equal(legit - 2, w.House("thorne")!.Legitimacy);
        Assert.False(new SetSuccessionLawCommand(w.ProtagonistId, "merit").CanExecute(ctx));
    }

    [Fact]
    public void FosteredKin_CannotBeBoundByEvents_ButStillInherit()
    {
        var (content, w) = Fresh();
        var child = new Character { Id = "scion_y", Name = "Y Thorne", HouseId = "thorne", Age = 8, Sex = "female", FatherId = "cassian_thorne" };
        w.Characters[child.Id] = child; w.House("thorne")!.Members.Add(child.Id);
        var ctx = new CommandContext(w, TestContent.Rng());
        Assert.True(new CommandExecutor().TryExecute(new FosterCommand(w.ProtagonistId, child.Id), ctx));
        Assert.Contains(DynastySystem.HiddenFlag, child.Flags);

        // a role any living character could fill will never pick the hidden child
        var def = new Praetoria.Core.Events.EventDef { Id = "any", Roles = new[] { new Praetoria.Core.Events.RoleDef { Name = "someone" } } };
        for (int i = 0; i < 30; i++)
        {
            var b = Praetoria.Core.Events.Binder.TryBind(def, w, new SplitMix64Rng((ulong)i));
            Assert.NotNull(b); Assert.NotEqual(child.Id, b!.Resolve("someone"));
        }
        Assert.False(new AssassinateCommand("marius_valmont", child.Id).CanExecute(ctx));   // unreachable
        Assert.Equal(child.Id, DynastySystem.FindHeir(w, w.Protagonist!)!.Id);              // blood child beats the wife
        Assert.True(new RecallCommand(w.ProtagonistId, child.Id).CanExecute(ctx));
    }

    [Fact]
    public void Regent_IsAnAdultOfTheHouse_AndIsFlagged()
    {
        var (content, w) = Fresh();
        var child = new Character { Id = "scion_z", Name = "Z Thorne", HouseId = "thorne", Age = 9, Sex = "male", FatherId = "cassian_thorne" };
        w.Characters[child.Id] = child; w.House("thorne")!.Members.Add(child.Id);
        DynastySystem.Die(w, "cassian_thorne");
        Assert.Equal("scion_z", w.ProtagonistId);
        new DynastySystem { Content = content }.Tick(w, TestContent.Rng());
        Assert.True(w.HasFlag(DynastySystem.RegencyFlag));
        Assert.Contains(DynastySystem.RegentFlag, w.Char("elara_thorne")!.Flags);
    }

    [Fact]
    public void ResentmentOfTheRisen_CoolsTheGrandees_UntilStandingCatchesUp()
    {
        var (content, w) = Fresh();                                   // Thorne 14 vs Aurelian 95, Valmont 70
        var dyn = new DynastySystem { Content = content };
        int before = w.Relationship("marius_valmont", "cassian_thorne").Disposition;
        dyn.Tick(w, TestContent.Rng());
        Assert.Equal(before - 1, w.GetRelationship("marius_valmont", "cassian_thorne")!.Disposition);
        Assert.Contains(DynastySystem.ParvenuFlag, w.Protagonist!.Flags);
        w.House("thorne")!.Legitimacy = 90;
        dyn.Tick(w, TestContent.Rng());
        Assert.DoesNotContain(DynastySystem.ParvenuFlag, w.Protagonist!.Flags);
    }

    [Fact]
    public void Marriage_IsProposed_AndADynasticMatchYieldsAClaim()
    {
        var (content, w) = Fresh("academy_crucible");                 // Marcus (vega, baron) ↔ Sela (drake, baron); Ren (sato, knight)
        var ctx = new CommandContext(w, TestContent.Rng());
        var cmd = new ProposeMarriageCommand(w.ProtagonistId, "vega_marcus", "drake_sela", content.Titles);
        Assert.Null(cmd.Refusal(ctx));
        Assert.True(new CommandExecutor().TryExecute(cmd, ctx));
        Assert.NotNull(DynastySystem.Spouse(w, w.Char("vega_marcus")!));
        Assert.Contains("wed", w.Char("drake_sela")!.Flags);
        Assert.Equal("already wed", new ProposeMarriageCommand(w.ProtagonistId, "vega_marcus", "sato_ren", content.Titles).Refusal(ctx));

        var (_, w2) = Fresh("academy_crucible");
        var ctx2 = new CommandContext(w2, TestContent.Rng());
        w2.Char("corwin_lucan")!.Sex = "female";                     // a Count's heir
        Assert.True(new CommandExecutor().TryExecute(new ProposeMarriageCommand(w2.ProtagonistId, "vega_marcus", "corwin_lucan", content.Titles), ctx2));
        Assert.Contains("count", w2.House("vega")!.Claims);           // marrying up forges a claim (GDD §13 Intrigue)
    }

    [Fact]
    public void Assassination_IsARoll_WithATrail_OnFailure()
    {
        int killed = 0, exposed = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var (content, w) = Fresh("academy_crucible", seed);
            var ctx = new CommandContext(w, new SplitMix64Rng(seed));
            Assert.True(new CommandExecutor().TryExecute(new AssassinateCommand(w.ProtagonistId, "corwin_lucan", content.Friction), ctx));
            if (!w.Char("corwin_lucan")!.Alive) killed++;
            else { exposed++; Assert.True(w.HasFlag("plot_exposed")); Assert.True(w.HasFlag("assassin:vega")); Assert.Contains("feud", w.Char("corwin_lucan")!.Flags); }
        }
        Assert.True(killed > 0 && exposed > 0, $"killed {killed}, exposed {exposed}");
    }

    [Fact]
    public void AtWar_ARuthlessHouse_HuntsHeirs_AndNpcsMarry()
    {
        var (content, w) = Fresh("the_fallen_house");                 // Drusus Harrow: Cruel; Voss has Mira + Senna
        var fleets = new FleetSystem(content.Fleets);
        fleets.DeclareWar(w, "harrow", "voss");
        var exec = new CommandExecutor();
        for (int t = 0; t < 4; t++)                                   // a campaign, not a dice roll: agents must be saved up (GDD §14)
        {
            foreach (var p in w.Pools.Values) p.Regenerate();
            new NpcAi { Titles = content.Titles }.Act(exec, new CommandContext(w, TestContent.Rng()), null, null, content.Fleets);
        }
        Assert.Contains(exec.Log, l => l.Contains("harrow_drusus: arrange an accident"));
        Assert.DoesNotContain(exec.Log, l => l.Contains("harrow_drusus: propose"));   // no courting across a war

        var (content2, w2) = Fresh("academy_crucible");
        var exec2 = new CommandExecutor();
        for (int t = 0; t < 3; t++) { foreach (var p in w2.Pools.Values) p.Regenerate(); new NpcAi { Titles = content2.Titles }.Act(exec2, new CommandContext(w2, TestContent.Rng()), null, null, content2.Fleets); }
        Assert.Contains(exec2.Log, l => l.Contains("propose"));
    }

    [Fact]
    public void HouseholdEvents_BindTheRegent_AndClearTheirFlags()
    {
        var (content, w) = Fresh();
        var child = new Character { Id = "scion_q", Name = "Q Thorne", HouseId = "thorne", Age = 9, Sex = "male", FatherId = "cassian_thorne" };
        w.Characters[child.Id] = child; w.House("thorne")!.Members.Add(child.Id);
        DynastySystem.Die(w, "cassian_thorne");
        new DynastySystem { Content = content }.Tick(w, TestContent.Rng());
        var engine = new Praetoria.Core.Events.EventEngine(content, WorldBuilder.RngFor(w));
        var ev = engine.GatherEligible(w).First(e => e.def.Id == "the_regents_hand");
        Assert.Equal("elara_thorne", ev.binding.Resolve("regent"));
        engine.Resolve(new Praetoria.Core.Events.FiredEvent(ev.def, ev.binding), w, "learn_at_their_elbow");
        Assert.Equal(1, w.Protagonist!.Skill("administration"));
    }
}
