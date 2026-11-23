using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>What the head of a house does with another house (docs/WEALTH.md, "Dealings between houses"):
/// <see cref="Gift"/> a sum of credits (value: the sum); <see cref="AskCoin"/> or <see cref="AskVote"/> (value: the motion) a
/// favour of it; <see cref="CallVote"/> a favour it owes for its vote (value: the motion); <see cref="Pledge"/> your own vote
/// to it (value: the motion); <see cref="Threat"/>; <see cref="Lend"/> it a sum at a rate (value: the sum; rate);
/// <see cref="Forgive"/> or <see cref="Press"/> what it owes on such a loan; <see cref="Honour"/> or <see cref="Break"/> a
/// favour it has called in for coin.</summary>
public sealed class DealingsCommand : ICommand
{
    public const string Gift = "gift", AskCoin = "ask_coin", AskVote = "ask_vote", CallVote = "call_vote", Pledge = "pledge", Threat = "threat", Lend = "lend", Forgive = "forgive", Press = "press", Honour = "honour", Break = "break";
    public static readonly string[] All = { Gift, AskCoin, AskVote, CallVote, Pledge, Threat, Lend, Forgive, Press, Honour, Break };
    private readonly string _what, _houseId, _value; private readonly int _rate; private readonly DealingsSystem _dealings;
    public DealingsCommand(string actorId, string what, string houseId, DealingsSystem dealings, string value = "", int ratePct = 0)
    { ActorId = actorId; _what = what; _houseId = houseId; _dealings = dealings; _value = value; _rate = ratePct; }
    public string ActorId { get; }
    private int Sum => int.TryParse(_value, out int n) ? n : 0;
    public string Describe() => $"{_what} {_houseId} {(_what is Gift or Lend ? Money.Show(Sum) : _value)}{(_what == Lend ? $" at {_rate}" : "")}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); var theirs = w.House(_houseId);
        return _what switch
        {
            Gift => _dealings.GiftRefusal(w, me, theirs, Sum),
            AskCoin => _dealings.AskRefusal(w, me, theirs, DealingsSystem.Coin),
            AskVote => _dealings.AskRefusal(w, me, theirs, DealingsSystem.Vote, _value),
            CallVote => _dealings.CallVoteRefusal(w, me, theirs, _value),
            Pledge => _dealings.PledgeRefusal(w, me, theirs, _value),
            Threat => _dealings.ThreatRefusal(w, me, theirs),
            Lend => _dealings.LendRefusal(w, me, theirs, Sum, _rate),
            Forgive => _dealings.ForgiveRefusal(w, me, theirs),
            Press => _dealings.PressRefusal(w, me, theirs),
            Honour => _dealings.AnswerRefusal(w, me, theirs, true),
            Break => _dealings.AnswerRefusal(w, me, theirs, false),
            _ => "no such dealing"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId)!; var mine = w.House(me.HouseId)!; var theirs = w.House(_houseId)!;
        switch (_what)
        {
            case Gift: _dealings.Gift(w, me, theirs, Sum); break;
            case AskCoin: _dealings.Ask(w, me, theirs, DealingsSystem.Coin); break;
            case AskVote: _dealings.Ask(w, me, theirs, DealingsSystem.Vote, _value); break;
            case CallVote: _dealings.CallVote(w, me, theirs, _value); break;
            case Pledge: _dealings.Pledge(w, me, theirs, _value); break;
            case Threat: _dealings.Threat(w, mine, theirs); break;
            case Lend: _dealings.Lend(w, mine, theirs, Sum, _rate); break;
            case Forgive: DealingsSystem.Forgive(w, mine, theirs); break;
            case Press: DealingsSystem.Press(w, mine, theirs); break;
            case Honour: _dealings.Answer(w, mine, theirs, true); break;
            case Break: _dealings.Answer(w, mine, theirs, false); break;
        }
    }
}
