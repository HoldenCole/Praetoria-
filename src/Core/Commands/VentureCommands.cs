using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Sell a venture (docs/ECONOMY_MARKET.md stage B): to a named house at its bid, to the best bidder ("best": a steward's sale,
/// which offers the player first refusal where it is due), or on the open market ("": four-fifths of value, and it is broken up).</summary>
public sealed class SellVentureCommand : ICommand
{
    public const string Best = "best";
    private readonly string _id, _to; private readonly VentureSystem _ventures;
    public SellVentureCommand(string actorId, string businessId, string toHouseId, VentureSystem ventures) { ActorId = actorId; _id = businessId; _to = toHouseId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => _to.Length == 0 ? $"sell {_id} on the market" : $"sell {_id} to {_to}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var b = w.Business(_id);
        if (actor?.Alive != true || house == null || b == null || _ventures.Catalog.Def(b.Kind) == null) return "no such venture";
        if (b.OwnerId != house.Id) return "not yours";
        if (b.CompanyId.Length > 0) return "held by a company: sell its shares instead";
        if (_to.Length == 0 || _to == Best) return null;
        if (w.House(_to) == null) return "no such house";
        return _ventures.Bid(w, _to, b) > 0 ? null : $"{w.House(_to)!.Name} will not buy it";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var b = w.Business(_id)!;
        if (_to.Length == 0) _ventures.SellToMarket(w, b);
        else if (_to == Best) _ventures.StewardSells(w, b);
        else _ventures.SellTo(w, b, _to);
    }
}

/// <summary>Buy another house's venture at its asking price. The holder of the world may take it first at that price.</summary>
public sealed class BuyVentureCommand : ICommand
{
    private readonly string _id; private readonly VentureSystem _ventures;
    public BuyVentureCommand(string actorId, string businessId, VentureSystem ventures) { ActorId = actorId; _id = businessId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"buy {_id}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var b = w.Business(_id);
        if (actor?.Alive != true || house == null || b == null) return "no such venture";
        var (price, refusal) = _ventures.Ask(w, b, house.Id); if (refusal != null) return refusal;
        return house.Treasury.Credits >= price ? null : $"needs {price} credits";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; _ventures.BuyAt(w, w.Business(_id)!, w.Char(ActorId)!.HouseId); }
}

/// <summary>Take up a venture offered to the house, at the offered price.</summary>
public sealed class AcceptVentureOfferCommand : ICommand
{
    private readonly string _offerId; private readonly VentureSystem _ventures;
    public AcceptVentureOfferCommand(string actorId, string offerId, VentureSystem ventures) { ActorId = actorId; _offerId = offerId; _ventures = ventures; }
    public string ActorId { get; }
    public string Describe() => $"accept {_offerId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null; var o = w.Offers.FirstOrDefault(x => x.Id == _offerId);
        if (actor?.Alive != true || house == null || o == null || o.ToHouseId != house.Id) return "no such offer";
        if (w.Business(o.BusinessId) is not { } b || b.OwnerId != o.FromHouseId) return "the venture is no longer theirs to sell";
        if (w.Businesses.Values.Any(x => x.OwnerId == house.Id && x.Kind == b.Kind && x.SystemId == b.SystemId)) return "you already have one here";
        return house.Treasury.Credits >= o.Price ? null : $"needs {o.Price} credits";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var o = w.Offers.First(x => x.Id == _offerId); var house = w.House(w.Char(ActorId)!.HouseId)!;
        _ventures.Transfer(w, w.Business(o.BusinessId)!, house, o.Price, "accepts the offer and buys");
    }
}

/// <summary>Pause or resume a project: a paused site costs nothing and goes nowhere.</summary>
public sealed class PauseProjectCommand : ICommand
{
    private readonly string _id;
    public PauseProjectCommand(string actorId, string projectId) { ActorId = actorId; _id = projectId; }
    public string ActorId { get; }
    public string Describe() => $"pause or resume {_id}";
    public bool CanExecute(CommandContext ctx) => ctx.World.Char(ActorId) is { Alive: true } a && ctx.World.Projects.Any(p => p.Id == _id && p.OwnerId == a.HouseId);
    public void Execute(CommandContext ctx) { var p = ctx.World.Projects.First(x => x.Id == _id); p.Paused = !p.Paused; p.Stalled = false; }
}

/// <summary>Rush a project: pay everything still owed and a quarter more in credits, and it is done next year.</summary>
public sealed class RushProjectCommand : ICommand
{
    private readonly string _id;
    public RushProjectCommand(string actorId, string projectId) { ActorId = actorId; _id = projectId; }
    public string ActorId { get; }
    public string Describe() => $"rush {_id}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var p = w.Projects.FirstOrDefault(x => x.Id == _id); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null || p == null || p.OwnerId != house.Id) return "no such project";
        if (p.CompanyId.Length > 0) return "the company builds at its own pace";
        if (p.TurnsLeft <= 1) return "done next year as it is";
        var rem = ProjectSystem.Remaining(p); int premium = ProjectSystem.RushPremium(w, p);
        var need = rem.Clone(); need.Credits += premium;
        return house.Treasury.CanAfford(need) ? null : $"needs {Presentation.CourtProjection.Bundle(need)}";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var p = w.Projects.First(x => x.Id == _id); var house = w.House(p.OwnerId)!;
        var rem = ProjectSystem.Remaining(p); int premium = ProjectSystem.RushPremium(w, p);
        house.Treasury.Spend(rem); house.Treasury.Credits -= premium; foreach (var r in Resource.All) p.Paid.Add(r, rem.Get(r));
        p.TurnsLeft = 1; p.Paused = false; p.Stalled = false;
        w.Log($"{house.Name} pays the crews double: {p.Name} will be done within the year.");
    }
}

/// <summary>Abandon a project: half of what was paid comes back; the site is cleared.</summary>
public sealed class AbandonProjectCommand : ICommand
{
    private readonly string _id;
    public AbandonProjectCommand(string actorId, string projectId) { ActorId = actorId; _id = projectId; }
    public string ActorId { get; }
    public string Describe() => $"abandon {_id}";
    public bool CanExecute(CommandContext ctx) => ctx.World.Char(ActorId) is { Alive: true } a && ctx.World.Projects.Any(p => p.Id == _id && p.OwnerId == a.HouseId && p.CompanyId.Length == 0);
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var p = w.Projects.First(x => x.Id == _id); var house = w.House(p.OwnerId)!;
        house.Treasury.Add(ProjectSystem.Refund(p)); w.Projects.Remove(p);
        w.Log($"{house.Name} abandons {p.Name}.");
    }
}
