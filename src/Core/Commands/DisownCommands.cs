using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Struck from the line, cast out (docs/HOUSEHOLD.md, "Struck from the line, cast out, and the undutiful will"): the head may
/// <see cref="Strike"/> a grown relative of the blood out of the line, <see cref="Restore"/> them to it, <see cref="CastOut"/> them out of
/// the house, or <see cref="TakeBack"/> one cast out. Each costs legitimacy and the kin's regard unless the record holds cause; undoing
/// costs what doing cost. No pool is spent.</summary>
public sealed class DisownCommand : ICommand
{
    public const string Strike = "strike", Restore = "restore", CastOut = "cast_out", TakeBack = "take_back";
    private readonly string _what, _personId;
    public DisownCommand(string actorId, string what, string personId) { ActorId = actorId; _what = what; _personId = personId; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var who = w.Char(_personId);
        return _what switch
        {
            Strike => DisownSystem.StrikeRefusal(w, head, who),
            Restore => DisownSystem.RestoreRefusal(w, head, who),
            CastOut => DisownSystem.CastOutRefusal(w, head, who),
            TakeBack => DisownSystem.TakeBackRefusal(w, head, who),
            _ => "no such thing"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var who = w.Char(_personId)!;
        switch (_what)
        {
            case Strike: DisownSystem.Strike(w, head, who); break;
            case Restore: DisownSystem.Restore(w, head, who); break;
            case CastOut: DisownSystem.CastOutOf(w, head, who); break;
            case TakeBack: DisownSystem.TakeBack(w, head, who); break;
        }
    }
}

/// <summary>The suit of the undutiful will, on the docket: the head may <see cref="Settle"/> it (pay what it asks now, and it is withdrawn)
/// or send someone of the house to <see cref="Plead"/> against it (1 influence, as for any suit).</summary>
public sealed class WillSuitCommand : ICommand
{
    public const string Settle = "settle", Plead = "plead";
    private static readonly Dictionary<string, int> Cost = new() { [Pool.Influence] = LadderSystem.PleadInfluence };
    private readonly string _what, _suitId, _pleaderId;
    public WillSuitCommand(string actorId, string what, string suitId, string pleaderId = "") { ActorId = actorId; _what = what; _suitId = suitId; _pleaderId = pleaderId; }
    public string ActorId { get; }
    // whose suit it is, kept when it is asked about or done, so that what was done is told by names and not by the suit's id
    private string _plaintiffId = "";
    public string Describe() => _what == Settle ? $"come to terms with {Whose()} over the will" : $"send {_pleaderId} to answer {Whose()}'s suit over the will";
    private string Whose() => _plaintiffId.Length > 0 ? _plaintiffId : "the child";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var s = w.WillSuits.FirstOrDefault(x => x.Id == _suitId); if (s != null) _plaintiffId = s.PlaintiffId;
        if (_what == Settle) return DisownSystem.SettleRefusal(w, head, s);
        if (_what != Plead) return "no such thing";
        return DisownSystem.PleadRefusal(w, head, s, w.Char(_pleaderId)) ?? ctx.PoolsOf(ActorId).Short(Cost);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var s = w.WillSuits.First(x => x.Id == _suitId); _plaintiffId = s.PlaintiffId;
        if (_what == Settle) { DisownSystem.Settle(w, head, s); return; }
        ctx.PoolsOf(ActorId).Spend(Cost); DisownSystem.SendPleader(w, head, s, w.Char(_pleaderId)!);
    }
}
