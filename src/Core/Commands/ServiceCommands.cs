using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// A command by service (docs/MILITARY.md, "A command by service"; ServiceSystem). Every act the player can take on it.

/// <summary>Name a legate: whoever holds the consulship, the Admiral of the Reach's office or emergency powers gives a fleet under them (an office
/// fleet, the state's fleet, or one of the holder's own house) to somebody of another house. One agent.</summary>
public sealed class NameLegateCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = ServiceSystem.AgentCost };
    private readonly string _fleetId, _characterId; private readonly OfficeCatalog _offices;
    public NameLegateCommand(string actorId, string fleetId, string characterId, OfficeCatalog offices) { ActorId = actorId; _fleetId = fleetId; _characterId = characterId; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => $"name {_characterId} legate of {_fleetId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); var c = w.Char(_characterId);
        if (me is not { Alive: true }) return "no one to name them";
        if (!w.Fleets.TryGetValue(_fleetId, out var f)) return "no such fleet";
        var auth = ServiceSystem.AuthorityOver(w, _offices, me, f);
        if (auth == null) return ServiceSystem.Authorities(w, _offices, me).Count == 0
            ? "you hold no consulship, no Admiralty and no emergency powers: only those name a legate"
            : $"{f.Name} is not under your office: a consul names a legate for a fleet of the house, or the state's fleet when no Admiral holds it; the Admiral for the office fleets and the house's own; emergency powers for both";
        if (c is not { Alive: true }) return "no such person";
        if (c.Id == me.Id) return "nobody names themselves legate";
        if (c.HouseId == f.OwnerId) return c.HouseId == me.HouseId ? $"{c.Name} is of your house: give the command on the fleet's card" : $"{c.Name} is of the house that owns {f.Name}, whose head gives its commands";
        if (World.Outsider(c.HouseId) || w.House(c.HouseId) is not { } ch) return $"{c.Name} is not of a house of the Reach";
        if (Economy.HeadOf(w, ch)?.Id == c.Id) return $"{c.Name} is the head of {ch.Name}, and a head of house stays at home";
        var free = ServiceSystem.FreeRefusal(w, c); if (free != null) return free;
        if (w.AtWar(c.HouseId, me.HouseId)) return $"{ch.Name} is at war with your house";
        if (w.AtWar(c.HouseId, f.OwnerId)) return $"{ch.Name} is at war with {w.House(f.OwnerId)?.Name ?? f.OwnerId}, whose fleet it is";
        if (FleetSystem.AdmiralOf(w, f) is { } cmd && cmd.Id == me.Id) return $"you command {f.Name} yourself";
        int will = w.GetRelationship(c.Id, me.Id)?.Disposition ?? 0;
        if (c.HouseId != me.HouseId && will < ServiceSystem.ServeRegard) return $"{c.Name} thinks too ill of you ({Counted.Figure(will)}) to serve under you: it takes {Counted.Figure(ServiceSystem.ServeRegard)} or better";
        return ctx.PoolsOf(ActorId).Short(CostMap);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; ctx.PoolsOf(ActorId).Spend(CostMap); var me = w.Char(ActorId)!; var f = w.Fleets[_fleetId];
        ServiceSystem.Begin(w, f, w.Char(_characterId)!, me, ServiceSystem.Legate, ServiceSystem.AuthorityOver(w, _offices, me, f)!);
    }
}

/// <summary>Borrow an officer: the head of a house at war takes somebody of a friendly house to command one of its fleets, with the leave of that
/// house's head, who gives or refuses it by plain reasons (<see cref="ServiceSystem.LeaveRefusal"/>). One agent.</summary>
public sealed class BorrowOfficerCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = ServiceSystem.AgentCost };
    private readonly string _fleetId, _characterId;
    public BorrowOfficerCommand(string actorId, string fleetId, string characterId) { ActorId = actorId; _fleetId = fleetId; _characterId = characterId; }
    public string ActorId { get; }
    public string Describe() => $"borrow {_characterId} for {_fleetId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); var house = me != null ? w.House(me.HouseId) : null; var c = w.Char(_characterId);
        if (me is not { Alive: true } || house == null) return "no house";
        if (Economy.HeadOf(w, house)?.Id != me.Id) return "only the head of the house borrows an officer";
        if (ServiceSystem.EnemyOf(w, house.Id) is not { } enemy) return "your house is at war with nobody: an officer is lent to a house at war";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || f.OwnerId != house.Id) return "not your fleet";
        if (f.Office || !ServiceSystem.Serviceable(f)) return $"{f.Name} is not a fleet of the house's own in commission: an officer is lent for one of those";
        if (c is not { Alive: true }) return "no such person";
        if (c.HouseId == house.Id) return $"{c.Name} is of your house: give the command on the fleet's card";
        if (World.Outsider(c.HouseId)) return $"{c.Name} is not of a house of the Reach";
        var free = ServiceSystem.FreeRefusal(w, c); if (free != null) return free;
        if (FleetSystem.AdmiralOf(w, f)?.Id == me.Id) return $"you command {f.Name} yourself";
        if (w.House(c.HouseId) is not { } lender || Economy.HeadOf(w, lender) is not { Alive: true } lenderHead) return $"{c.Name} has no head to give leave";
        var leave = ServiceSystem.LeaveRefusal(w, lenderHead, me, c, enemy); if (leave != null) return leave;
        return ctx.PoolsOf(ActorId).Short(CostMap);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; ctx.PoolsOf(ActorId).Spend(CostMap); var me = w.Char(ActorId)!; var c = w.Char(_characterId)!;
        var lenderHead = Economy.HeadOf(w, w.House(c.HouseId)!)!;
        ServiceSystem.Begin(w, w.Fleets[_fleetId], c, me, ServiceSystem.Lent, ServiceSystem.ByWar, ServiceSystem.EnemyOf(w, me.HouseId) ?? "", lenderHead.Id);
    }
}

/// <summary>Seek a command: the head offers their own service, or a grown relative's, to one who holds the consulship, the Admiralty or emergency
/// powers (a character id), or to a house at war (a house id). Taken or refused by plain reasons (<see cref="ServiceSystem.SeekRefusal"/>), and a
/// refusal says what is missing. One agent, spent only if they are taken on.</summary>
public sealed class SeekCommandCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Agents] = ServiceSystem.AgentCost };
    private readonly string _characterId, _takerId; private readonly OfficeCatalog _offices;
    public SeekCommandCommand(string actorId, string characterId, string takerId, OfficeCatalog offices) { ActorId = actorId; _characterId = characterId; _takerId = takerId; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => $"seek a command for {_characterId} from {_takerId}";
    public string? Refusal(CommandContext ctx) => Refusal(ctx, out _);
    private string? Refusal(CommandContext ctx, out ServiceSystem.Plan? plan)
    {
        plan = null; var w = ctx.World; var me = w.Char(ActorId); var c = w.Char(_characterId);
        if (me is not { Alive: true }) return "no one to offer it";
        if (c is not { Alive: true }) return "no such person";
        if (c.HouseId != me.HouseId) return $"{c.Name} is not of your house";
        if (c.Id != me.Id && w.House(me.HouseId) is { } h && Economy.HeadOf(w, h)?.Id != me.Id) return "only the head of the house offers another's service";
        if (c.Id != me.Id && c.Flags.Contains(FamilySystem.DefiantFlag)) return $"{c.Name} is defiant of you, and will not go at your word";
        var no = ServiceSystem.SeekRefusal(w, _offices, c, _takerId, out plan); if (no != null) return no;
        if (plan!.Taker.Id == me.Id && c.Id == me.Id) return "nobody takes themselves into service";
        return ctx.PoolsOf(ActorId).Short(CostMap);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; Refusal(ctx, out var plan); if (plan == null) return; ctx.PoolsOf(ActorId).Spend(CostMap);
        var me = w.Char(ActorId)!;
        ServiceSystem.Begin(w, plan.Fleet, w.Char(_characterId)!, plan.Taker, plan.Kind, plan.Authority, plan.EnemyId, plan.Kind == ServiceSystem.Lent && _characterId != me.Id ? me.Id : "", sought: true);
    }
}

/// <summary>Relieve a commander by service: the head of the house that owns the fleet, or the one who named or took them, takes the command back.
/// Free; the one relieved thinks the worse of whoever did it.</summary>
public sealed class RelieveServiceCommand : ICommand
{
    private readonly string _fleetId;
    public RelieveServiceCommand(string actorId, string fleetId) { ActorId = actorId; _fleetId = fleetId; }
    public string ActorId { get; }
    public string Describe() => $"relieve the commander by service of {_fleetId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId);
        if (me is not { Alive: true }) return "no one to give the order";
        if (!w.Fleets.TryGetValue(_fleetId, out var f) || ServiceSystem.Of(w, f) is not { } s) return "nobody commands it by service";
        bool owner = w.House(f.OwnerId) is { } h && Economy.HeadOf(w, h)?.Id == me.Id;
        return owner || s.NamedById == me.Id ? null : $"only the head of {w.House(f.OwnerId)?.Name ?? f.OwnerId}, whose fleet it is, or {w.Char(s.NamedById)?.Name ?? "the one who named them"}, who named them, relieves them (the Senate does it by a recall)";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId)!; var f = w.Fleets[_fleetId]; var s = ServiceSystem.Of(w, f)!; var c = w.Char(s.CommanderId);
        if (c != null) { var r = w.Relationship(c.Id, me.Id); r.Disposition = Math.Clamp(r.Disposition + ServiceSystem.RelievedRegard, -100, 100); }
        ServiceSystem.End(w, s, $"relieved by {me.Name}");
    }
}

/// <summary>Resign a command by service: the commander gives it up, or the head of their house calls them home. Free; whoever named or took them
/// thinks a little the worse of it.</summary>
public sealed class ResignServiceCommand : ICommand
{
    private readonly string _characterId;
    public ResignServiceCommand(string actorId, string characterId) { ActorId = actorId; _characterId = characterId; }
    public string ActorId { get; }
    public string Describe() => $"{_characterId} resigns the command by service";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); var c = w.Char(_characterId);
        if (me is not { Alive: true } || c is not { Alive: true }) return "no such person";
        if (ServiceSystem.Held(w, c) == null) return $"{c.Name} commands nothing by service";
        if (c.Id == me.Id) return null;
        return c.HouseId == me.HouseId && w.House(me.HouseId) is { } h && Economy.HeadOf(w, h)?.Id == me.Id ? null : $"{c.Name} is not of your house";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var c = w.Char(_characterId)!; var s = ServiceSystem.Held(w, c)!; var namer = w.Char(s.NamedById);
        if (namer != null) { var r = w.Relationship(namer.Id, c.Id); r.Disposition = Math.Clamp(r.Disposition + ServiceSystem.ResignRegard, -100, 100); }
        ServiceSystem.End(w, s, _characterId == ActorId ? "resigned" : $"called home by {w.Char(ActorId)!.Name}");
    }
}

/// <summary>Answer what was put to the head about a command by service (<see cref="ServiceAsk"/>): give or refuse leave to lend one of the house,
/// take or decline one who offers service, accept or decline being named legate. Free.</summary>
public sealed class AnswerServiceCommand : ICommand
{
    private readonly string _askId; private readonly bool _yes; private readonly OfficeCatalog _offices;
    public AnswerServiceCommand(string actorId, string askId, bool yes, OfficeCatalog offices) { ActorId = actorId; _askId = askId; _yes = yes; _offices = offices; }
    public string ActorId { get; }
    // what was answered, in words naming things by id, kept when it is asked about or done: the ask itself is gone once answered
    private string? _said;
    public string Describe() => _said ?? $"{(_yes ? "agree to" : "refuse")} what was asked";
    private ServiceAsk? Ask(World w) => w.ServiceAsks.FirstOrDefault(a => a.Id == _askId);
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); if (Ask(w) is { } asked) _said = ServiceSystem.AnswerSays(asked, _yes);
        if (me is not { Alive: true } || me.Id != w.ProtagonistId) return "it was put to the head of the house";
        return ServiceSystem.AnswerRefusal(w, _offices, me, Ask(w), _yes);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var a = Ask(ctx.World)!; _said = ServiceSystem.AnswerSays(a, _yes);
        ServiceSystem.Answer(ctx.World, _offices, ctx.World.Char(ActorId)!, a, _yes);
    }
}
