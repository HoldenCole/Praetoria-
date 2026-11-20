using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;
using Xunit;

namespace Praetoria.Tests;

/// <summary>What five playthroughs found (docs/PLAYTHROUGH.md, "the fifteen to fix first"), held shut.</summary>
public class PlaythroughFixTests
{
    private static readonly ContentDatabase Content = ContentLoader.LoadFromDirectory(ContentLocator.FindContentDir());
    private static (World w, TurnController tc, House house, Character me, CommandContext ctx) Start(string scenario = "the_old_lion", int seed = 7)
    {
        var w = WorldBuilder.FromScenario(ScenarioLoader.LoadFromContent(ContentLocator.FindContentDir(), scenario), (ulong)seed);
        var tc = new TurnController(w, Content); var me = w.Protagonist!;
        return (w, tc, w.House(me.HouseId)!, me, new CommandContext(w, TestContent.Rng()));
    }
    private static void Hulls(World w, string houseId, int n)
    {
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == houseId).ToList()) w.Fleets.Remove(f.Id);
        if (n > 0) w.Fleets["t_" + houseId] = new Fleet { Id = "t_" + houseId, OwnerId = houseId, Name = "T", SystemId = w.Holdings.Values.First(h => h.IsPlanet).SystemId, Strength = n };
    }

    [Fact]
    public void NoRivalIsGrantedAWorldOfThePlayers_OverThePlayersHead()
    {
        // 1: a landless rival petitioned "the house with most worlds" and the player's colony, and another player's home world, went
        var (w, _, house, _, _) = Start(); foreach (var h in w.Holdings.Values.Where(h => h.IsPlanet).Take(8)) h.OwnerId = house.Id; w.HoldingsChanged();
        int mine = w.HoldingsOf(house.Id).Count(h => h.IsPlanet); Assert.True(w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id)).All(h => w.HoldingsOf(h.Id).Count(x => x.IsPlanet) <= mine));
        foreach (var rival in w.Houses.Values.Where(h => h.Id != house.Id && !World.Outsider(h.Id))) Assert.NotEqual(house.Id, PetitionFiefCommand.Grantor(w, rival.Id)?.Id);
        // the player may still be granted one by another house
        foreach (var h in w.HoldingsOf(house.Id).ToList()) h.OwnerId = w.Houses.Values.First(x => x.Id != house.Id && !World.Outsider(x.Id)).Id; w.HoldingsChanged();
        Assert.NotNull(PetitionFiefCommand.Grantor(w, house.Id)); Assert.NotEqual(house.Id, PetitionFiefCommand.Grantor(w, house.Id)!.Id);
    }

    [Fact]
    public void ATitle_IsNotClimbedByClicking()
    {
        // 3: "Seize it by force" with no ships on turn 1; three petitions in one year; First Citizen with no world
        var (w, tc, house, me, ctx) = Start(); var prog = tc.Progression!; me.CareerRank = 3; house.Title = Title.Landless; house.Legitimacy = 100;
        ctx.PoolsOf(me.Id); foreach (var h in w.HoldingsOf(house.Id).ToList()) h.OwnerId = "nobody"; w.HoldingsChanged();
        // to seize a rank takes ships of the house's own: two for each rung of the rank
        Hulls(w, house.Id, 0); Assert.Contains("hulls of the house's own", prog.SeizeRefusal(w, me.Id)); Assert.False(prog.CanSeize(w, me.Id));
        Hulls(w, house.Id, ProgressionSystem.SeizeHullsPerRank); Assert.Null(prog.SeizeRefusal(w, me.Id)); prog.Seize(w, me.Id); Assert.Equal(Title.Knight, house.Title);
        // one rung a year, by any road
        Hulls(w, house.Id, 20); Assert.Contains("one a year", prog.SeizeRefusal(w, me.Id)); Assert.Contains("one a year", prog.PetitionRefusal(w, me.Id)); Assert.False(prog.CanPetition(w, me.Id));
        w.Turn++; Assert.Null(prog.PetitionRefusal(w, me.Id)); prog.Petition(w, me.Id); Assert.Equal(Title.Baron, house.Title);
        // from Legate up the rank wants land under it, however well the house is thought of
        w.Turn++; Assert.Contains("holds a world", prog.PetitionRefusal(w, me.Id)); house.Claims.Add(Title.Count); Assert.Contains("holds a world", prog.ClaimRefusal(w, me.Id));
        w.Holdings.Values.First(h => h.IsPlanet).OwnerId = house.Id; w.HoldingsChanged(); Assert.Null(prog.PetitionRefusal(w, me.Id)); prog.Petition(w, me.Id); Assert.Equal(Title.Count, house.Title);
        w.Turn++; Assert.Contains("2 worlds", prog.PetitionRefusal(w, me.Id));
        // legitimacy is still asked for, and said
        house.Title = Title.Landless; house.Legitimacy = 3; w.Turn++; Assert.StartsWith("needs legitimacy 10", prog.PetitionRefusal(w, me.Id));
        // the screen is given the cost, the reason and the question to ask first
        var model = Praetoria.Core.Presentation.CourtProjection.Build(tc, Content); Assert.All(model.Standing!.Paths, p => { Assert.NotEmpty(p.Cost); Assert.NotEmpty(p.Confirm); if (!p.Available) Assert.False(string.IsNullOrEmpty(p.Refusal)); });
    }

    [Fact]
    public void AVentureJustFounded_SellsForWhatItCost_NotForWhatItWillEarn()
    {
        // 4: a skimmer fleet that cost 12 credits and 6 materials sold for 105 the year it was finished
        var (w, tc, _, _, _) = Start(); var vs = tc.Ventures!; var b = w.Businesses.Values.OrderByDescending(x => vs.Value(w, x)).First(x => vs.Value(w, x) > (Content.Businesses.Def(x.Kind)?.Value(x.Level) ?? 0));
        int value = vs.Value(w, b), book = Content.Businesses.Def(b.Kind)!.Value(b.Level); w.Turn = 10;
        b.Founded = 10; Assert.Equal(book, vs.Trading(w, b)); Assert.True(vs.MarketPrice(w, b) <= book);
        b.Founded = 8; Assert.InRange(vs.Trading(w, b), book + 1, value - 1);
        b.Founded = 10 - VentureSystem.Seasoning; Assert.Equal(value, vs.Trading(w, b)); Assert.Equal(Math.Max(vs.Salvage(b), value * VentureSystem.MarketPct / 100), vs.MarketPrice(w, b));
        b.Founded = 0; Assert.Equal(value, vs.Trading(w, b));   // one that was always there
    }

    [Fact]
    public void ThereIsOneHeir_AndEveryScreenReadsTheSameLine()
    {
        // 5: the header said the son, the will said the rival, and after the succession the heir was the mother who had stepped down
        var (w, _, house, me, _) = Start(); var kin = w.Characters.Values.Where(c => c.Alive && c.HouseId == house.Id && c.Id != me.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToList(); Assert.True(kin.Count >= 2);
        house.NamedHeirId = ""; var line = DynastySystem.Line(w, me); Assert.Equal(line.FirstOrDefault()?.Id, DynastySystem.FindHeir(w, me)?.Id); Assert.Equal(kin.Count, line.Count); Assert.NotEmpty(DynastySystem.HeirBy(w, me));
        foreach (var law in new[] { "primogeniture", "ultimogeniture", "seniority", "merit" }) { house.SuccessionLaw = law; Assert.Equal(DynastySystem.Line(w, me).First().Id, DynastySystem.FindHeir(w, me)!.Id); }
        house.SuccessionLaw = "seniority"; var eldest = DynastySystem.FindHeir(w, me)!;
        // someone who married in is of the house but not of its blood, and stands behind everyone who is
        eldest.BornHouseId = "another_house"; Assert.NotEqual(eldest.Id, DynastySystem.FindHeir(w, me)!.Id); Assert.Equal(eldest.Id, DynastySystem.Line(w, me).Last().Id); eldest.BornHouseId = "";
        // so does a head who has already laid the house down
        eldest.Flags.Add(LifeSystem.RetiredChar); Assert.NotEqual(eldest.Id, DynastySystem.FindHeir(w, me)!.Id); eldest.Flags.Remove(LifeSystem.RetiredChar);
        // but if nobody of the blood is left, they are better than nobody
        foreach (var k in kin) k.BornHouseId = "another_house"; Assert.NotNull(DynastySystem.FindHeir(w, me)); foreach (var k in kin) k.BornHouseId = "";
        // someone the head named stands first, whatever the law
        var last = DynastySystem.Line(w, me).Last(); house.NamedHeirId = last.Id; Assert.Equal(last.Id, DynastySystem.FindHeir(w, me)!.Id); Assert.Equal("named by you", DynastySystem.HeirBy(w, me));
    }
}
