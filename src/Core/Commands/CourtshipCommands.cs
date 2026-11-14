using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Begin paying court (docs/MARRIAGE.md): the head, or one of the house, to someone of another house. Between children of
/// six and over it is the families sounding each other out about a promise.</summary>
public sealed class CourtCommand : ICommand
{
    private readonly string _suitorId, _targetId;
    public CourtCommand(string actorId, string suitorId, string targetId) { ActorId = actorId; _suitorId = suitorId; _targetId = targetId; }
    public string ActorId { get; }
    public string Describe() => $"court {_targetId} for {_suitorId}";
    public string? Refusal(CommandContext ctx) { var w = ctx.World; return CourtshipSystem.BeginRefusal(w, w.Char(ActorId), w.Char(_suitorId), w.Char(_targetId)); }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; CourtshipSystem.Begin(w, w.Char(_suitorId)!, w.Char(_targetId)!); }
}

/// <summary>The year's one act for a courtship: time together (1 influence), a gift (credits), the case put to the family
/// (2 influence), or word put about against a rival (1 agent).</summary>
public sealed class WooCommand : ICommand
{
    private readonly string _courtshipId, _act;
    public WooCommand(string actorId, string courtshipId, string act) { ActorId = actorId; _courtshipId = courtshipId; _act = act; }
    public string ActorId { get; }
    public string Describe() => $"{_act} for {_courtshipId}";
    public static IReadOnlyDictionary<string, int> Cost(string act) => act switch
    {
        CourtshipSystem.ActTime => new Dictionary<string, int> { [Pool.Influence] = 1 },
        CourtshipSystem.ActFamily => new Dictionary<string, int> { [Pool.Influence] = 2 },
        CourtshipSystem.ActRival => new Dictionary<string, int> { [Pool.Agents] = 1 },
        _ => new Dictionary<string, int>()
    };
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var c = w.Courtships.FirstOrDefault(x => x.Id == _courtshipId);
        if (c == null || w.Char(c.SuitorId)?.HouseId != w.Char(ActorId)?.HouseId) return "no such suit";
        var no = CourtshipSystem.ActRefusal(w, c, _act); if (no != null) return no;
        return ctx.PoolsOf(ActorId).CanAfford(Cost(_act)) ? null : _act == CourtshipSystem.ActRival ? "no agents" : "no influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; ctx.PoolsOf(ActorId).Spend(Cost(_act));
        CourtshipSystem.Act(w, w.Courtships.First(x => x.Id == _courtshipId), _act);
    }
}

/// <summary>Settle a suit. <see cref="Propose"/> puts the match and its terms to the other family (or accepts a suit they brought);
/// <see cref="Elope"/> marries without their leave; <see cref="BreakOff"/> drops it or breaks a promise; <see cref="Refuse"/>
/// declines a suit brought for one of your kin.</summary>
public sealed class SettleSuitCommand : ICommand
{
    public const string Propose = "propose", Elope = "elope", BreakOff = "break", Refuse = "refuse";
    private readonly string _courtshipId, _how, _dowry, _childrenTo;
    public SettleSuitCommand(string actorId, string courtshipId, string how, string dowry = CourtshipSystem.Customary, string childrenTo = "")
    { ActorId = actorId; _courtshipId = courtshipId; _how = how; _dowry = dowry; _childrenTo = childrenTo; }
    public string ActorId { get; }
    public string Describe() => $"{_how} {_courtshipId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var c = w.Courtships.FirstOrDefault(x => x.Id == _courtshipId); var actor = w.Char(ActorId);
        if (c == null || actor?.Alive != true) return "no such suit";
        string ourSide = c.Incoming ? c.TargetId : c.SuitorId;
        if (w.Char(ourSide)?.HouseId != actor.HouseId) return "no such suit";
        return _how switch
        {
            Propose => c.Betrothed ? "the match is agreed" : c.AskedTurn >= w.Turn ? "they have given their answer for this year" : null,
            Elope => CourtshipSystem.ElopeRefusal(w, c),
            BreakOff => c.Incoming ? "it is their suit to drop" : null,
            Refuse => c.Incoming ? null : "it is your own suit",
            _ => "no such way to settle it"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var c = w.Courtships.First(x => x.Id == _courtshipId);
        switch (_how)
        {
            case Propose:
                if (!CourtshipSystem.Propose(w, c, _dowry, _childrenTo)) c.AskedTurn = w.Turn;
                break;
            case Elope: CourtshipSystem.Elope(w, c); break;
            case BreakOff: CourtshipSystem.BreakOff(w, c); break;
            case Refuse: CourtshipSystem.RefuseSuit(w, c); break;
        }
    }
}

/// <summary>Give your own marriage a season's attention (1 influence, once a year), or end it. The marriages of the rest of the
/// house are theirs.</summary>
public sealed class MarriageCommand : ICommand
{
    public const string Tend = "tend", Divorce = "divorce";
    private static readonly Dictionary<string, int> TendCost = new() { [Pool.Influence] = 1 };
    private readonly string _whoId, _what;
    public MarriageCommand(string actorId, string whoId, string what) { ActorId = actorId; _whoId = whoId; _what = what; }
    public string ActorId { get; }
    public string Describe() => $"{_what} the marriage of {_whoId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var who = w.Char(_whoId);
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId) return "not of your house";
        if (who.Id != head.Id) return "their marriage is their own";
        if (_what == Divorce) return CourtshipSystem.DivorceRefusal(w, head, who);
        return CourtshipSystem.TendRefusal(w, who) ?? (ctx.PoolsOf(ActorId).CanAfford(TendCost) ? null : "no influence");
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var who = w.Char(_whoId)!;
        if (_what == Divorce) CourtshipSystem.Divorce(w, who);
        else { ctx.PoolsOf(ActorId).Spend(TendCost); CourtshipSystem.Tend(w, who); }
    }
}
