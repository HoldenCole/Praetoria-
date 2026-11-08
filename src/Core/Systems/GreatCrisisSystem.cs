using Praetoria.Core.Crises;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The Great-Crisis framework (GDD §11/§16, BuildSpec M6): a clock, an intensity setting, stages with flags and effects,
/// and the archetype's behaviours. V1's archetype is the human galaxy-war: the houses sort into the hegemon's bloc
/// and the alliance against it, the blocs go to war, and when the clock runs out the wars end and — in the fractured
/// stars — the strongest house of the winning bloc takes the throne. Deterministic; the clock is a world counter.
/// </summary>
public sealed class GreatCrisisSystem
{
    public const string ClockCounter = "great_crisis_clock";    // tenths
    public const string StageCounter = "great_crisis_stage";    // index of the current stage (−1 = not begun)
    public static string StageFlag(string stageId) => $"great_crisis:{stageId}";
    public const string ResolvedFlag = "great_crisis:resolved";
    public const string HegemonBloc = "hegemon";
    public const string AllianceBloc = "alliance";

    private readonly GreatCrisisDef? _def;
    private readonly FleetSystem _fleets;
    public GreatCrisisSystem(GreatCrisisDef? def, FleetSystem fleets) { _def = def; _fleets = fleets; }
    public GreatCrisisDef? Def => _def;
    public bool IsEmpty => _def == null || _def.Stages.Count == 0;

    public GreatCrisisStage? CurrentStage(World w)
    {
        if (_def == null) return null;
        int i = w.WorldCounters.TryGetValue(StageCounter, out var s) ? s : -1;
        return i >= 0 && i < _def.Stages.Count ? _def.Stages[i] : null;
    }

    /// <summary>Advance the clock and enter any stage it crosses. Call once per turn (after the economy and fleets).</summary>
    public void Tick(World w, IRng rng)
    {
        if (_def == null || w.HasFlag(ResolvedFlag) || w.Turn < _def.StartTurn) return;
        double intensity = _def.IntensityOf(w.CrisisIntensity);
        if (intensity <= 0) return;

        // the galaxy's own turmoil hurries the clock: wars, coalition pressure, fear
        int turmoil = w.Wars.Count * 3 + w.Counter("coalition_pressure") + Math.Min(5, w.Counter("seizures"));
        int advance = (int)Math.Round(_def.BaseRate * intensity) + turmoil;
        w.WorldCounters[ClockCounter] = w.Counter(ClockCounter) + advance;

        int stage = w.WorldCounters.TryGetValue(StageCounter, out var s) ? s : -1;
        while (stage + 1 < _def.Stages.Count && w.Counter(ClockCounter) >= _def.Stages[stage + 1].At)
        {
            stage++;
            w.WorldCounters[StageCounter] = stage;
            Enter(w, rng, _def.Stages[stage]);
        }
    }

    private void Enter(World w, IRng rng, GreatCrisisStage stage)
    {
        w.WorldFlags.Add(StageFlag(stage.Id));
        var ctx = new EvalContext(w, new Binding(w.ProtagonistId), rng);
        foreach (var e in stage.Effects) e.Apply(ctx);
        if (stage.Log.Length > 0) w.Log($"[{_def!.Name}] {stage.Log}");
        switch (stage.Behavior)
        {
            case "blocs": FormBlocs(w); break;
            case "bloc_war": BlocWar(w); break;
            case "climax": Climax(w); break;
            case "resolution": Resolve(w); break;
        }
    }

    /// <summary>The strongest house (hulls, then legitimacy) is the hegemon; houses whose heads are well disposed to its
    /// head join it, the rest draw together against it. The player's house follows the same rule — and can change
    /// sides through the <c>joinBloc</c> effect.</summary>
    public void FormBlocs(World w)
    {
        var houses = w.Houses.Values.OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (houses.Count < 2) return;
        var hegemon = houses[0];
        var hegemonHead = w.LivingCharacters().Where(c => c.HouseId == hegemon.Id).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        hegemon.Bloc = HegemonBloc;
        foreach (var h in houses.Skip(1))
        {
            if (!w.HoldingsOf(h.Id).Any() && FleetSystem.StrengthOf(w, h.Id) == 0) { h.Bloc = ""; continue; }   // the landless have nothing to bring
            int disp = 0;
            if (hegemonHead != null)
                foreach (var c in w.LivingCharacters().Where(c => c.HouseId == h.Id)) disp += w.GetRelationship(c.Id, hegemonHead.Id)?.Disposition ?? 0;
            h.Bloc = disp > 0 && !w.AtWar(h.Id, hegemon.Id) ? HegemonBloc : AllianceBloc;
        }
        w.Log($"Blocs: {hegemon.Name} and {string.Join(", ", houses.Where(h => h.Bloc == HegemonBloc && h.Id != hegemon.Id).Select(h => h.Name).DefaultIfEmpty("no one"))} " +
              $"against {string.Join(", ", houses.Where(h => h.Bloc == AllianceBloc).Select(h => h.Name).DefaultIfEmpty("no one"))}.");
    }

    /// <summary>Every hegemon house goes to war with every alliance house. Rival-vs-rival wars included.</summary>
    public void BlocWar(World w)
    {
        // The hegemon fights the whole alliance; each of its followers takes one alliance house (paired in id order), so a
        // galaxy of twenty houses has twenty-odd wars, not a hundred — fronts, not a free-for-all.
        var a = w.Houses.Values.Where(h => h.Bloc == HegemonBloc).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var b = w.Houses.Values.Where(h => h.Bloc == AllianceBloc).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        if (a.Count == 0 || b.Count == 0) return;
        var hegemon = a.OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).First();
        foreach (var y in b) _fleets.DeclareWar(w, hegemon.Id, y.Id);
        var others = a.Where(h => h.Id != hegemon.Id).ToList();
        for (int i = 0; i < others.Count; i++) _fleets.DeclareWar(w, others[i].Id, b[i % b.Count].Id);
    }

    private void Climax(World w)
    {
        foreach (var f in w.Fleets.Values) f.Readiness = Math.Min(100, f.Readiness + 20);     // everyone commits what they have
        var player = w.Protagonist?.HouseId;
        if (player != null && w.House(player)?.Bloc == HegemonBloc) w.WorldCounters["coalition_pressure"] = SphereSystem.MaxCoalitionPressure;
    }

    /// <summary>The wars end; the bloc with more hulls wins; in the fractured stars its strongest house takes the throne.</summary>
    public void Resolve(World w)
    {
        int hegemonHulls = w.Houses.Values.Where(h => h.Bloc == HegemonBloc).Sum(h => FleetSystem.StrengthOf(w, h.Id));
        int allianceHulls = w.Houses.Values.Where(h => h.Bloc == AllianceBloc).Sum(h => FleetSystem.StrengthOf(w, h.Id));
        string winning = hegemonHulls >= allianceHulls ? HegemonBloc : AllianceBloc;
        foreach (var war in w.Wars.ToList()) _fleets.MakePeace(w, war.HouseA, war.HouseB, terms: false);   // the great peace is one settlement, not forty cessions
        var victor = w.Houses.Values.Where(h => h.Bloc == winning).OrderByDescending(h => FleetSystem.StrengthOf(w, h.Id)).ThenByDescending(h => h.Legitimacy).ThenBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault();
        w.WorldFlags.Add(ResolvedFlag);
        w.WorldFlags.Add($"great_crisis:won:{winning}");
        var player = w.Protagonist?.HouseId;
        if (player != null) w.WorldFlags.Add(w.House(player)?.Bloc == winning ? "great_crisis:player_won" : "great_crisis:player_lost");
        // a throne is taken, not awarded: the field must be won decisively (hulls at five to four) and the victor's bloc must hold
        // the capital, or the victor's fleet its orbit (GDD §19) — otherwise the war ends in exhaustion and no one is crowned
        int winHulls = winning == HegemonBloc ? hegemonHulls : allianceHulls, loseHulls = winning == HegemonBloc ? allianceHulls : hegemonHulls;
        bool decisive = winHulls > 0 && winHulls >= (int)Math.Ceiling(loseHulls * 1.25);
        // the throne stands in the capital: it goes to the winning bloc's house that holds the capital's world, or to the victor
        // whose fleet holds its orbit — never to a strongest house that was somewhere else when the war ended
        House? crowned = null;
        if (victor != null && w.Galaxy is { Capital.Length: > 0 } cap)
        {
            var holder = w.PlanetHolder(cap.Capital) is { } ch ? w.House(ch) : null;
            if (holder != null && holder.Bloc == winning) crowned = holder;
            else if (w.Fleets.Values.Any(f => f.OwnerId == victor.Id && !f.Moving && f.SystemId == cap.Capital && f.Strength >= 2)) crowned = victor;
        }
        else crowned = victor;
        if (crowned != null && w.Era != "imperium" && crowned.Title != Progression.Title.Emperor && decisive)
        {
            crowned.Title = Progression.Title.Archduke;
            ThroneSystem.Enthrone(w, crowned.Id, seized: true);
            w.Log($"The War of the Reach ends with {crowned.Name} on a throne that did not exist when it began{(crowned.Id != victor!.Id ? $"; {victor.Name} won the field and {crowned.Name} the capital" : "")}.");
        }
        else if (victor != null && w.Era != "imperium")
        {
            w.WorldFlags.Add("great_crisis:stalemate"); w.WorldCounters["stalemates"] = w.Counter("stalemates") + 1;
            if (Progression.Title.RankOf(victor.Title) < 4) victor.Title = Progression.Title.Duke;
            victor.Legitimacy += 10;
            w.Log($"The War of the Reach ends in exhaustion. {victor.Name} holds the field but {(decisive ? $"no one of the winning side holds {w.Galaxy?.System(w.Galaxy.Capital)?.Name ?? "the capital"}" : "not decisively enough")}; no one is crowned, and the Fractured Stars go on.");
        }
        else w.Log($"The War of the Reach ends. The {winning} bloc holds the field{(victor != null ? $"; {victor.Name} stands tallest" : "")}.");
        foreach (var h in w.Houses.Values) h.Bloc = "";
    }
}
