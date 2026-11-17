using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Something done about blood between houses (docs/FEUD.md). As the house that is owed: call the one to blame out (in person, or
/// by a champion of the house), ask the price, or let it lie. As the house that owes: offer the price. And the answers to what another
/// house has asked: pay or refuse a price demanded, take or spurn a price offered, fight or decline when called out. A knife is the
/// ordinary <see cref="AssassinateCommand"/>; the debt is settled by who dies.</summary>
public sealed class FeudCommand : ICommand
{
    public const string Challenge = "challenge", Demand = "demand", Offer = "offer", LetLie = "let_lie", Pay = "pay", Refuse = "refuse", Take = "take", Spurn = "spurn", Fight = "fight", Decline = "decline";
    public static readonly string[] Kinds = { Challenge, Demand, Offer, LetLie, Pay, Refuse, Take, Spurn, Fight, Decline };
    private readonly string _feudId, _what, _championId;
    public FeudCommand(string actorId, string feudId, string what, string championId = "") { ActorId = actorId; _feudId = feudId; _what = what; _championId = championId; }
    public string ActorId { get; }
    public string Describe() => $"{_what} over {_feudId}{(_championId.Length > 0 ? " by " + _championId : "")}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var champion = _championId.Length > 0 ? w.Char(_championId) : null;
        if (_championId.Length > 0 && champion == null) return "no such person";
        return FeudSystem.ActRefusal(w, w.Char(ActorId), w.Feuds.FirstOrDefault(f => f.Id == _feudId), _what, champion);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        FeudSystem.Act(w, w.Char(ActorId)!, w.Feuds.First(f => f.Id == _feudId), _what, _championId.Length > 0 ? w.Char(_championId) : null, ctx.Rng);
    }
}
