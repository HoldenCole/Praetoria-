using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// Dynasty intents (GDD §14 "scatter the seed", §18 marriage, §13 Intrigue). Through the shared bus like everything.

/// <summary>Foster a child of the house away, out of reach (GDD §14 hidden kin): the child cannot be bound by events
/// — no gala, no duel, no knife — but still inherits. Returns home at sixteen. Costs 1 Agent.</summary>
public sealed class FosterCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = 1 };
    private readonly string _childId;
    public FosterCommand(string actorId, string childId) { ActorId = actorId; _childId = childId; }
    public string ActorId { get; }
    public string Describe() => $"foster {_childId} away";
    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var c = w.Char(_childId);
        return actor?.Alive == true && c?.Alive == true && c.Id != ActorId && c.HouseId == actor.HouseId
            && c.Age < DynastySystem.AgeOfMajority && !c.Flags.Contains(DynastySystem.HiddenFlag) && ctx.PoolsOf(ActorId).CanAfford(CostMap);
    }
    public void Execute(CommandContext ctx)
    {
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var c = ctx.World.Char(_childId)!;
        c.Flags.Add(DynastySystem.HiddenFlag);
        ctx.World.Log($"{c.Name} is sent to be fostered far from court, under another name.");
    }
}

/// <summary>Bring a fostered child home early (exposed again, but present).</summary>
public sealed class RecallCommand : ICommand
{
    private readonly string _childId;
    public RecallCommand(string actorId, string childId) { ActorId = actorId; _childId = childId; }
    public string ActorId { get; }
    public string Describe() => $"recall {_childId}";
    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var c = w.Char(_childId);
        return actor?.Alive == true && c?.Alive == true && c.HouseId == actor.HouseId && c.Flags.Contains(DynastySystem.HiddenFlag);
    }
    public void Execute(CommandContext ctx)
    {
        var c = ctx.World.Char(_childId)!;
        c.Flags.Remove(DynastySystem.HiddenFlag);
        ctx.World.Log($"{c.Name} is recalled to court.");
    }
}

/// <summary>Propose a marriage (GDD §18/§13): both living, unmarried adults of opposite sex. Accepted when the target
/// is not ill-disposed and their house is not far above yours (a greater house wants more). Costs 2 Influence. A
/// match with a higher-titled house grants a claim to its rung; the lower house gains standing.</summary>
public sealed class ProposeMarriageCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 2 };
    private readonly string _suitorId, _targetId;
    private readonly Progression.TitleCatalog? _titles;
    public ProposeMarriageCommand(string actorId, string suitorId, string targetId, Progression.TitleCatalog? titles = null)
    { ActorId = actorId; _suitorId = suitorId; _targetId = targetId; _titles = titles; }
    public string ActorId { get; }
    public string Describe() => $"propose {_suitorId} ⚭ {_targetId}";

    public static bool IsMarried(World w, string id) => DynastySystem.Spouse(w, w.Char(id)!) != null;

    /// <summary>Char flag on someone who has asked for the protagonist's hand; *The Suit* answers it.</summary>
    public const string SuitorFlag = "suitor";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var s = w.Char(_suitorId); var t = w.Char(_targetId);
        if (actor?.Alive != true || s?.Alive != true || t?.Alive != true) return "no such person";
        if (s.HouseId != actor.HouseId) return "not of your house";
        if (DynastySystem.AreKin(w, s, t)) return "too close in blood";
        if (s.Id == t.Id || s.HouseId == t.HouseId) return "too close";
        if (s.Age < DynastySystem.AgeOfMajority || t.Age < DynastySystem.AgeOfMajority) return "too young";
        if (s.Sex == "" || t.Sex == "" || s.Sex == t.Sex) return "no heirs could come of it";
        if (IsMarried(w, s.Id) || IsMarried(w, t.Id)) return "already wed";
        if (w.AtWar(s.HouseId, t.HouseId)) return "not while your houses are at war";
        if (t.Flags.Contains(DynastySystem.HiddenFlag)) return "they cannot be found";
        if (t.Id == w.ProtagonistId) return s.Flags.Contains(SuitorFlag) ? "your suit is already before them" : null;   // the player decides
        if ((w.GetRelationship(t.Id, s.Id)?.Disposition ?? 0) < -10) return "they will not have it";
        int theirs = w.House(t.HouseId)?.Legitimacy ?? 0, ours = w.House(s.HouseId)?.Legitimacy ?? 0;
        if (theirs > ours + 40) return "their house looks too far down on yours";
        return ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "no influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var s = w.Char(_suitorId)!; var t = w.Char(_targetId)!;
        if (t.Id == w.ProtagonistId)
        {
            s.Flags.Add(SuitorFlag);
            w.Log($"{s.Name} of {w.House(s.HouseId)?.Name} seeks {t.Name}'s hand.");
            return;
        }
        foreach (var (a, b) in new[] { (s.Id, t.Id), (t.Id, s.Id) })
        {
            var rel = w.Relationship(a, b); rel.Bond = BondType.Marriage; rel.BondStrength = 60;
            rel.Disposition = Math.Clamp(rel.Disposition + 20, -100, 100);
        }
        s.Flags.Add("wed"); t.Flags.Add("wed");
        var ours = w.House(s.HouseId)!; var theirs = w.House(t.HouseId)!;
        int ourRank = _titles?.RankOf(ours.Title) ?? 0, theirRank = _titles?.RankOf(theirs.Title) ?? 0;
        if (theirRank > ourRank) { ours.Claims.Add(theirs.Title); ours.Legitimacy += 5; }
        else if (ourRank > theirRank) { theirs.Claims.Add(ours.Title); theirs.Legitimacy += 5; }
        else { ours.Legitimacy += 2; theirs.Legitimacy += 2; }
        w.WorldCounters["marriages"] = w.Counter("marriages") + 1;
        w.Log($"{s.Name} of {ours.Name} weds {t.Name} of {theirs.Name}.");
    }
}

/// <summary>Arrange a death (GDD §13 Intrigue — "engineer deaths"; §14 heir-hunting has costs). Costs 2 Agents.
/// Success odds from the assassin's intrigue against the target's house's Paranoid heads; a failure is traced:
/// the target's house learns who paid, the actor's standing bleeds, and the galaxy recoils (`seizures`). The
/// protagonist pays nature-trait friction either way. Hidden kin cannot be reached.</summary>
public sealed class AssassinateCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = 2 };
    private readonly string _targetId;
    private readonly FrictionTable _friction;
    public AssassinateCommand(string actorId, string targetId, FrictionTable? friction = null) { ActorId = actorId; _targetId = targetId; _friction = friction ?? FrictionTable.Empty; }
    public string ActorId { get; }
    public string Describe() => $"arrange an accident for {_targetId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var t = w.Char(_targetId);
        return actor?.Alive == true && t?.Alive == true && t.Id != ActorId && t.HouseId != actor.HouseId
            && t.Age >= DynastySystem.AgeOfMajority   // no house, however ruthless, puts a knife to a child (GDD §14)
            && !t.Flags.Contains(DynastySystem.HiddenFlag) && ctx.PoolsOf(ActorId).CanAfford(CostMap);
    }

    public static double Odds(World w, Character actor, Character target)
    {
        double odds = 0.35 + actor.Skill("intrigue") * 0.07;
        if (actor.AptitudeTraits.Contains("Schemer")) odds += 0.10;
        foreach (var guard in w.LivingCharacters().Where(c => c.HouseId == target.HouseId && c.AptitudeTraits.Contains("Paranoid"))) odds -= 0.15;
        if (target.Id == w.ProtagonistId) odds -= 0.10;
        return Math.Clamp(odds, 0.05, 0.9);
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        ctx.PoolsOf(ActorId).Spend(CostMap);
        var actor = w.Char(ActorId)!; var t = w.Char(_targetId)!;
        bool player = actor.Id == w.ProtagonistId;
        double roll = ctx.Rng.NextDouble();
        if (roll < Odds(w, actor, t))
        {
            w.Log($"{t.Name} of {w.House(t.HouseId)?.Name} dies suddenly. Nothing is proven.");
            DynastySystem.Die(w, t.Id);
            if (player) w.WorldCounters["corruption"] = w.Counter("corruption") + 1;
        }
        else
        {
            w.Log($"An attempt on {t.Name} fails, and the trail leads to {actor.Name} of {w.House(actor.HouseId)?.Name}.");
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == t.HouseId))
            {
                var rel = w.Relationship(c.Id, actor.Id); rel.Disposition = Math.Clamp(rel.Disposition - 40, -100, 100);
                c.Flags.Add("feud");
            }
            w.WorldFlags.Add($"assassin:{actor.HouseId}"); if (!player) w.WorldFlags.Add("assassin_any");   // a knife from somewhere, for content to trace
            if (w.House(actor.HouseId) is { } h) h.Legitimacy = Math.Max(0, h.Legitimacy - 5);
            if (player) { w.WorldCounters["seizures"] = w.Counter("seizures") + 1; w.WorldFlags.Add("plot_exposed"); }
            if (t.Id == w.ProtagonistId || t.HouseId == w.Protagonist?.HouseId) w.WorldFlags.Add("knife_turned");
        }
        w.WorldCounters["assassinations_attempted"] = w.Counter("assassinations_attempted") + 1;
        _friction.Apply(w, actor, "assassination");
    }
}

/// <summary>Change the house's succession law (GDD §14). Costs 2 Influence and a little legitimacy — the realm
/// dislikes a house rewriting the order of inheritance.</summary>
public sealed class SetSuccessionLawCommand : ICommand
{
    public static readonly string[] Laws = { "primogeniture", "ultimogeniture", "seniority", "merit" };
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 2 };
    private readonly string _law;
    public SetSuccessionLawCommand(string actorId, string law) { ActorId = actorId; _law = law; }
    public string ActorId { get; }
    public string Describe() => $"succession law → {_law}";
    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var h = actor != null ? w.House(actor.HouseId) : null;
        return actor?.Alive == true && h != null && Laws.Contains(_law) && h.SuccessionLaw != _law && ctx.PoolsOf(ActorId).CanAfford(CostMap);
    }
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; ctx.PoolsOf(ActorId).Spend(CostMap);
        var h = w.House(w.Char(ActorId)!.HouseId)!;
        h.SuccessionLaw = _law; h.Legitimacy = Math.Max(0, h.Legitimacy - 2);
        w.Log($"{h.Name} adopts {_law} as its law of succession.");
    }
}
