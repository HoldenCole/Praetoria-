using Praetoria.Core.Progression;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Buy a seat in the Senate from the census (docs/REPUBLIC.md stage 3): a house of some account in commerce, with no land,
/// bench or sword to seat it, pays the fee to the chair and may move motions and stand for office from then on.</summary>
public sealed class EnrollCommand : ICommand
{
    private readonly OrderSystem _os; private readonly TitleCatalog? _titles;
    public EnrollCommand(string actorId, OrderSystem os, TitleCatalog? titles) { ActorId = actorId; _os = os; _titles = titles; }
    public string ActorId { get; }
    public string Describe() => "enrol in the Senate by the census";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (w.Char(ActorId) is not { Alive: true } a || w.House(a.HouseId) is not { } h) return "no house";
        bool seated = _titles != null && OrderSystem.Door(w, h, _titles, SenateSystem.ProposerTitleRank, SenateSystem.ProposerLawRank) != null;
        return _os.EnrollRefusal(w, h, seated);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; _os.Enroll(w, w.House(w.Char(ActorId)!.HouseId)!); }
}

/// <summary>Patrons and clients: take a needy house as a client (pay its debts and a gift), put the house under a greater one's
/// protection, or cut the tie from either end.</summary>
public sealed class ClientCommand : ICommand
{
    public const string Take = "take", Seek = "seek", Release = "release", Leave = "leave";
    private readonly string _act, _otherId; private readonly OrderSystem _os;
    public ClientCommand(string actorId, string act, string otherHouseId, OrderSystem os) { ActorId = actorId; _act = act; _otherId = otherHouseId; _os = os; }
    public string ActorId { get; }
    public string Describe() => $"{_act} {_otherId}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (w.Char(ActorId) is not { Alive: true } a || w.House(a.HouseId) is not { } h) return "no house"; var other = w.House(_otherId);
        return _act switch
        {
            Take => w.IsImperium ? "patronage is the throne's now" : _os.TakeClientRefusal(w, h, other),
            Seek => w.IsImperium ? "patronage is the throne's now" : _os.SeekPatronRefusal(w, h, other),
            Release => other != null && other.PatronId == h.Id ? null : "not your client",
            Leave => h.PatronId.Length > 0 ? null : "the house has no patron",
            _ => "no such order"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var h = w.House(w.Char(ActorId)!.HouseId)!; var other = w.House(_otherId);
        switch (_act)
        {
            case Take: _os.TakeClient(w, h, other!); break;
            case Seek: _os.SeekPatron(w, h, other!); break;
            case Release: _os.Part(w, other!, byClient: false); break;
            case Leave: _os.Part(w, h, byClient: true); break;
        }
    }
}

/// <summary>Spend on the voters in a nominee's name before a consular election: ten credits a vote's weight, five times an election.</summary>
public sealed class CanvassCommand : ICommand
{
    private readonly string _nomineeId; private readonly OrderSystem _os; private readonly Func<World, IEnumerable<string>>? _nominees; private readonly Func<string, bool>? _any;
    public CanvassCommand(string actorId, string nomineeHouseId, OrderSystem os, Func<World, IEnumerable<string>> nominees) { ActorId = actorId; _nomineeId = nomineeHouseId; _os = os; _nominees = nominees; }
    /// <summary>For stewards: any house the predicate admits is taken as a nominee.</summary>
    public CanvassCommand(string actorId, string nomineeHouseId, OrderSystem os, Func<string, bool> any) { ActorId = actorId; _nomineeId = nomineeHouseId; _os = os; _any = any; }
    public string ActorId { get; }
    public string Describe() => $"canvass for {_nomineeId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (w.Char(ActorId) is not { Alive: true } a || w.House(a.HouseId) is not { } h) return "no house";
        var nominees = _nominees != null ? _nominees(w) : _any != null && w.House(_nomineeId) != null && _any(_nomineeId) ? new[] { _nomineeId } : Array.Empty<string>();
        return _os.CanvassRefusal(w, h, _nomineeId, nominees);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; _os.Canvass(w, w.House(w.Char(ActorId)!.HouseId)!, _nomineeId); }
}
