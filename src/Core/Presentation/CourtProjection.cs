using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

// The Godot-free model of everything Court mode shows (BuildSpec §1 Pillar 1: the UI reads from Core).
// Plain records; the Godot layer only turns them into nodes. Being in Core, it is unit-tested headless,
// which is how the Court's logic stays verified even when no Godot runtime is at hand.

public sealed record CharacterCard(string Name, string HouseName, string Accent, string TitleName, string RankName, string TrackName,
    int Age, bool Alive, IReadOnlyList<string> Nature, IReadOnlyList<string> Aptitude, string AmbitionName, int Stress, bool Broken,
    bool Regency, string? Background);
public sealed record PoolsLine(int Influence, int Treasury, int Agents);
public sealed record TreasuryLine(int Credits, int Materials, int Manpower, int Influence, int Exotics, bool Insolvent);
public sealed record BuildOption(string HoldingId, string BuildingId, string Label, string Tooltip, bool Available);
public sealed record HoldingCard(string Id, string Name, string SpecName, string? SystemName, int Population, int MaxPopulation, int Unrest,
    int SlotsUsed, int Slots, IReadOnlyList<string> Buildings, IReadOnlyList<BuildOption> Builds, bool CanRaiseShips, string RaiseShipsLabel);
public sealed record PathOption(string Id, string Label, string Tooltip, bool Available);
public sealed record StandingCard(int Legitimacy, int Instability, IReadOnlyList<string> Claims, string? NextTitle, int NextRequirement,
    IReadOnlyList<PathOption> Paths, string Spheres, int Threat);
public sealed record FleetLine(string Id, string Name, string Where, int Strength, int Readiness, bool Moving, string? Destination, int Eta, bool Office);
public sealed record ChoiceCard(string Id, string Label, string Cost, bool Available);
public sealed record BriefingCard(BriefingItem Item, string Title, string Tier, bool Setpiece, string Body, bool Resolved, IReadOnlyList<ChoiceCard> Choices);
public sealed record MemberLine(string Name, string Detail, bool Alive, int Disposition, string Bond);
public sealed record ConnectionLine(string Name, string HouseName, string Detail, int Disposition, string Bond, IReadOnlyList<string> Flags, bool Hostile);
public sealed record DamperOption(string CrisisId, string DamperId, string Label, string Tooltip, bool Available);
public sealed record CrisisLine(string Id, string Name, int Severity, int Since, string? Author, IReadOnlyList<DamperOption> Dampers);
public sealed record CrisisClock(string Counters, IReadOnlyList<CrisisLine> Active, IReadOnlyList<string> Causable);
public sealed record CourtModel(int Turn, string Era, string Mode, int Generation, bool DynastyDead, bool Quiet,
    CharacterCard Character, PoolsLine Pools, TreasuryLine Treasury, IReadOnlyList<HoldingCard> Holdings, StandingCard? Standing,
    IReadOnlyList<FleetLine> Fleets, IReadOnlyList<BriefingCard> Briefing, string HouseName, string HouseAccent,
    IReadOnlyList<MemberLine> Members, IReadOnlyList<ConnectionLine> Connections, CrisisClock? Crises, IReadOnlyList<string> Chronicle);

public static class CourtProjection
{
    private static readonly IRng NoRng = new SplitMix64Rng(0);
    public const int ChronicleLines = 10;

    public static CourtModel Build(TurnController tc, ContentDatabase content)
    {
        var w = tc.World;
        var p = w.Protagonist ?? throw new InvalidOperationException("No protagonist.");
        var house = w.House(p.HouseId) ?? throw new InvalidOperationException($"House '{p.HouseId}' missing.");
        var ctx = new CommandContext(w, NoRng);

        var character = new CharacterCard(
            p.Name, house.Name, house.AccentColor,
            content.Titles.ById(house.Title)?.Name ?? house.Title,
            content.Careers.RankName(p.CareerTrack, p.CareerRank) ?? $"rank {p.CareerRank}",
            content.Careers.ById(p.CareerTrack)?.Name ?? p.CareerTrack,
            p.Age, p.Alive,
            p.NatureTraits.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            p.AptitudeTraits.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            content.Traits.Ambitions.FirstOrDefault(a => a.Id == p.Ambition)?.Name ?? p.Ambition,
            p.Stress, p.Flags.Contains(DynastySystem.BrokenFlag), w.HasFlag(DynastySystem.RegencyFlag),
            p.Flags.FirstOrDefault(f => f.StartsWith("background:")) is { } bg ? content.Backgrounds.ById(bg["background:".Length..])?.Name : null);

        var pools = tc.PlayerPools;
        var t = house.Treasury;

        var holdings = new List<HoldingCard>();
        foreach (var h in w.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var spec = content.Holdings.Spec(h.Specialization);
            var builds = new List<BuildOption>();
            foreach (var b in content.Holdings.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal))
            {
                if (!b.FitsSpecialization(h.Specialization) || h.Buildings.Contains(b.Id)) continue;
                var cmd = new BuildCommand(p.Id, h.Id, b.Id, content.Holdings);
                builds.Add(new BuildOption(h.Id, b.Id, $"Build {b.Name}  ({Bundle(b.Cost)})",
                    $"yield {Bundle(b.Yield)}" + (b.Upkeep.IsEmpty() ? "" : $" · upkeep {Bundle(b.Upkeep)}"), cmd.CanExecute(ctx)));
            }
            bool canRaise = new RaiseFleetCommand(p.Id, h.Id, content.Fleets).CanExecute(ctx);
            holdings.Add(new HoldingCard(h.Id, h.Name, spec?.Name ?? h.Specialization, w.Galaxy?.System(h.SystemId)?.Name,
                h.Population, spec?.MaxPopulation ?? 0, h.Unrest, h.Buildings.Count, spec?.Slots ?? 0,
                h.Buildings.Select(b => content.Holdings.Building(b)?.Name ?? b).ToList(), builds,
                canRaise, $"Lay down a hull at {h.Name}  ({Bundle(content.Fleets.ShipCost)})"));
        }

        StandingCard? standing = null;
        if (tc.Progression is { } prog)
        {
            var next = prog.NextTitle(w, house.Id);
            var paths = new List<PathOption>();
            if (next != null)
            {
                paths.Add(new PathOption("seize", "Seize it by force", "Military — ignores legitimacy; spikes the galaxy's fear", new SeizeTitleCommand(p.Id, prog).CanExecute(ctx)));
                paths.Add(new PathOption("petition", "Petition the throne", "Merit — granted only what you're legitimate for; arrives clean", new PetitionTitleCommand(p.Id, prog).CanExecute(ctx)));
                paths.Add(new PathOption("claim", "Press a claim", "Intrigue — needs a claim; launders legitimacy, leaves a trail", new ClaimTitleCommand(p.Id, prog).CanExecute(ctx)));
            }
            standing = new StandingCard(house.Legitimacy, w.Counter("title_instability"),
                house.Claims.OrderBy(c => c, StringComparer.Ordinal).ToList(), next?.Name, next?.LegitimacyRequirement ?? 0, paths,
                string.Join(" · ", house.SphereInfluence.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}")),
                w.Counter("threat"));
        }

        var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal)
            .Select(f => new FleetLine(f.Id, f.Name, w.Galaxy?.System(f.SystemId)?.Name ?? f.SystemId, f.Strength, f.Readiness, f.Moving,
                f.Moving ? w.Galaxy?.System(f.DestinationId)?.Name ?? f.DestinationId : null, f.Eta, f.Office)).ToList();

        var briefing = new List<BriefingCard>();
        foreach (var item in tc.Briefing)
        {
            var fired = item.Fired;
            var text = content.Text(fired.Def.Id);
            var ectx = new EvalContext(w, fired.Binding, NoRng, content);
            var choices = item.Resolved ? new List<ChoiceCard>() : tc.Offer(item).Select(oc => new ChoiceCard(
                oc.Choice.Id,
                text != null && text.Choices.TryGetValue(oc.Choice.Id, out var ct) ? TextRenderer.Substitute(ct, ectx) : oc.Choice.Id,
                oc.Choice.Cost.Count == 0 ? "" : string.Join(", ", oc.Choice.Cost.Select(c => $"{c.Value} {c.Key}")),
                oc.Available)).ToList();
            briefing.Add(new BriefingCard(item, TextRenderer.Substitute(text?.Title ?? fired.Def.Id, ectx), fired.Def.Tier.ToString().ToUpperInvariant(),
                fired.Def.Tier == EventTier.Setpiece, string.IsNullOrWhiteSpace(text?.Body) ? "" : TextRenderer.Substitute(text!.Body, ectx), item.Resolved, choices));
        }

        var members = house.Members.Select(w.Char).Where(c => c != null && c!.Id != p.Id).Select(c => c!)
            .Select(c => { var rel = w.GetRelationship(c.Id, p.Id); return new MemberLine(c.Name,
                c.Alive ? $"{content.Careers.RankName(c.CareerTrack, c.CareerRank) ?? $"rank {c.CareerRank}"}, {c.Age}" : "dead",
                c.Alive, rel?.Disposition ?? 0, BondName(rel)); }).ToList();

        var connections = w.LivingCharacters().Where(c => c.HouseId != house.Id).OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => { var rel = w.GetRelationship(c.Id, p.Id); int d = rel?.Disposition ?? 0; return new ConnectionLine(c.Name,
                w.House(c.HouseId)?.Name ?? c.HouseId, content.Careers.RankName(c.CareerTrack, c.CareerRank) ?? $"rank {c.CareerRank}", d, BondName(rel),
                c.Flags.Where(f => !f.StartsWith("background:")).OrderBy(f => f, StringComparer.Ordinal).ToList(), d < -20); }).ToList();

        CrisisClock? clock = null;
        if (tc.Crises is { } crises)
        {
            var active = new List<CrisisLine>();
            foreach (var a in w.Crises.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                var def = crises.Def(a.Id);
                var dampers = new List<DamperOption>();
                if (def != null)
                    foreach (var d in def.Dampers)
                    {
                        bool can = new ApplyDamperCommand(p.Id, crises, def, d).CanExecute(ctx);
                        dampers.Add(new DamperOption(def.Id, d.Id, $"{d.Name} (−{d.Relief})" + (d.Cost.Count > 0 ? $"  [{string.Join(", ", d.Cost.Select(c => $"{c.Value} {c.Key}"))}]" : ""),
                            can ? "" : "Not available — earned by prior play, or unaffordable.", can));
                    }
                active.Add(new CrisisLine(a.Id, def?.Name ?? a.Id, a.Severity, a.TurnStarted, a.Organic ? null : w.Char(a.AuthorId)?.Name ?? a.AuthorId, dampers));
            }
            clock = new CrisisClock(
                $"unrest {w.Counter("unrest")} · corruption {w.Counter("corruption")} · goodwill {w.Counter("goodwill")} · coalition pressure {w.Counter("coalition_pressure")} · incursions {w.Counter(FleetSystem.IncursionsCounter)}",
                active, crises.Causable(w, NoRng).Select(c => c.Name).ToList());
        }

        var chronicle = w.History.Skip(Math.Max(0, w.History.Count - ChronicleLines)).Reverse().Select(h => $"T{h.Turn}  {h.Text}").ToList();

        return new CourtModel(w.Turn, w.Era, w.Mode, w.Counter("generation") + 1, w.IsDynastyDead, tc.Briefing.Count == 0,
            character, new PoolsLine(pools.Influence, pools.Treasury, pools.Agents),
            new TreasuryLine(t.Credits, t.Materials, t.Manpower, t.Influence, t.Exotics, t.Credits < 0),
            holdings, standing, fleets, briefing, house.Name, house.AccentColor, members, connections, clock, chronicle);
    }

    private static string BondName(Relationship? rel) => rel?.Bond is { } b && b != BondType.None ? b.ToString().ToLowerInvariant() : "";

    public static string Bundle(Resources r)
    {
        var parts = Resource.All.Where(k => r.Get(k) != 0).Select(k => $"{r.Get(k)} {k}").ToList();
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }
}
