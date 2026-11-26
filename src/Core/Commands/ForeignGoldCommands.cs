using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Answer the gold a nation beyond the Reach has sent before a war is voted (docs/BEYOND.md, "Foreign gold in the Senate"):
/// <see cref="ForeignGoldSystem.Take"/> it (the sum is the head's own, the vote is declared as it asks, and it goes on the record),
/// <see cref="ForeignGoldSystem.Refuse"/> it (nothing is lost), or <see cref="ForeignGoldSystem.Expose"/> it to the Senate (the Reach's
/// trust and the house's standing rise, the chamber leans the more for the war, and the nation thinks the less of the Reach). Free.</summary>
public sealed class ForeignGoldCommand : ICommand
{
    private readonly string _offerId, _answer;
    public ForeignGoldCommand(string actorId, string offerId, string answer) { ActorId = actorId; _offerId = offerId; _answer = answer; }
    public string ActorId { get; }
    public string Describe() => $"{_answer} the gold of {_offerId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World;
        return ForeignGoldSystem.AnswerRefusal(w, w.Char(ActorId), w.GoldOffers.FirstOrDefault(o => o.Id == _offerId), _answer);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var o = w.GoldOffers.First(x => x.Id == _offerId);
        ForeignGoldSystem.Answer(w, o, _answer);
        w.WorldFlags.Remove(ForeignGoldSystem.OfferedFlag); if (ForeignGoldSystem.Waiting(w, o.HouseId).Any()) w.WorldFlags.Add(ForeignGoldSystem.OfferedFlag);
    }
}
