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

/// <summary>Trade between houses (docs/WEALTH.md, "Trade between houses"), done by the head with a house of the Reach:
/// <see cref="Buy"/> a lot of a good out of its store, or <see cref="Sell"/> it one out of yours (good; units);
/// strike a standing deal in which it sends you so many a year (<see cref="DealBuy"/>) or you send it (<see cref="DealSell"/>)
/// for a term (good; units a year; years); or <see cref="End"/> a deal in force before its term (dealId; the house is not
/// looked at).</summary>
public sealed class HouseTradeCommand : ICommand
{
    public const string Buy = "buy", Sell = "sell", DealBuy = "deal_buy", DealSell = "deal_sell", End = "end";
    public static readonly string[] All = { Buy, Sell, DealBuy, DealSell, End };
    private readonly string _what, _houseId, _good, _dealId; private readonly int _units, _years; private readonly HouseTradeSystem _trade;
    public HouseTradeCommand(string actorId, string what, string houseId, HouseTradeSystem trade, string good = "", int units = 0, int years = 0, string dealId = "")
    { ActorId = actorId; _what = what; _houseId = houseId; _trade = trade; _good = good; _units = units; _years = years; _dealId = dealId; }
    public string ActorId { get; }
    public string Describe() => _what == End ? $"end {_dealId}" : $"{_what} {_houseId} {_units} {Resource.Label(_good)}{(_what is DealBuy or DealSell ? $" a year for {_years}" : "")}";
    /// <summary>What a lot would cost or fetch (<see cref="Buy"/> and <see cref="Sell"/> only).</summary>
    public HouseQuote Quote(World w) => _what == Sell ? _trade.SellQuote(w, w.Char(ActorId), w.House(_houseId), _good, _units) : _trade.BuyQuote(w, w.Char(ActorId), w.House(_houseId), _good, _units);
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); var theirs = w.House(_houseId);
        return _what switch
        {
            Buy => _trade.BuyRefusal(w, me, theirs, _good, _units),
            Sell => _trade.SellRefusal(w, me, theirs, _good, _units),
            DealBuy => _trade.DealRefusal(w, me, theirs, _good, _units, _years, selling: false),
            DealSell => _trade.DealRefusal(w, me, theirs, _good, _units, _years, selling: true),
            End => _trade.EndRefusal(w, me, _dealId),
            _ => "no such trade"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var mine = w.House(w.Char(ActorId)!.HouseId)!;
        if (_what == End) { HouseTradeSystem.End(w, HouseTradeSystem.Deal(w, _dealId)!, mine); return; }
        var theirs = w.House(_houseId)!;
        switch (_what)
        {
            case Buy: _trade.Buy(w, mine, theirs, _good, _units); break;
            case Sell: _trade.Sell(w, mine, theirs, _good, _units); break;
            case DealBuy: HouseTradeSystem.Strike(w, theirs, mine, _good, _units, _years, _trade.DealUnitCredits(w, mine, theirs, _good, selling: false)); break;
            case DealSell: HouseTradeSystem.Strike(w, mine, theirs, _good, _units, _years, _trade.DealUnitCredits(w, mine, theirs, _good, selling: true)); break;
        }
    }
}
