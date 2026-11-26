using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Issue an edict (docs/EDICTS.md): the actor must hold the position the edict hangs on, have the influence and the coin, and the
/// edict must not be in force already over the same worlds, nor be lately ended, nor be one too many for the actor. An edict that names
/// a house is given the house as <c>targetId</c>; one on a short good names the dearest itself.</summary>
public sealed class IssueEdictCommand : ICommand
{
    private readonly string _edictId, _targetId; private readonly ContentDatabase _content;
    public IssueEdictCommand(string actorId, string edictId, ContentDatabase content, string targetId = "") { ActorId = actorId; _edictId = edictId; _content = content; _targetId = targetId; }
    public string ActorId { get; }
    /// <summary>Why another house or magistrate issues it, for the chronicle ("" for the player).</summary>
    public string Reason { get; init; } = "";
    public string Describe() => $"issue the edict {_edictId}{(_targetId.Length > 0 ? " against " + _targetId : "")}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId);
        if (EdictSystem.IssueRefusal(w, _content, actor, _edictId, _targetId) is { } no) return no;
        var d = _content.Edicts.Def(_edictId)!;
        return d.Influence > 0 ? ctx.PoolsOf(ActorId).Short(new Dictionary<string, int> { [Pool.Influence] = d.Influence }) : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var d = _content.Edicts.Def(_edictId)!;
        if (d.Influence > 0) ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = d.Influence });
        EdictSystem.Issue(w, _content, w.Char(ActorId)!, d, _targetId, Reason);
    }
}

/// <summary>Take back an edict one issued, before it lapses. Nothing paid for it comes back, and it may not be issued again for its years.
/// An edict that outlives its issuer (an amnesty, a remission, a forced loan) has been given and cannot be taken back.</summary>
public sealed class WithdrawEdictCommand : ICommand
{
    private readonly string _edictId;
    public WithdrawEdictCommand(string actorId, string edictId) { ActorId = actorId; _edictId = edictId; }
    public string ActorId { get; }
    public string Describe() => $"take back the edict {_edictId}";
    public string? Refusal(CommandContext ctx) => EdictSystem.WithdrawRefusal(ctx.World, ctx.World.Char(ActorId), _edictId);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { if (EdictSystem.InForce(ctx.World, _edictId) is { } e) EdictSystem.End(ctx.World, e, EdictSystem.Withdrawn); }
}
