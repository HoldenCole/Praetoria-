using Praetoria.Core.Data;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

// Things worth a rich house's money (docs/WEALTH.md, the section of that name): the head's four acts. Each is paid out of the
// treasury (a town house may be paid out of the head's own purse) and costs no influence: the coin is the cost.

/// <summary>Buy land out of the treasury and settle it on one of the house, child or grown. It pays them a yearly income for life
/// and is theirs to leave.</summary>
public sealed class EndowCommand : ICommand
{
    private readonly string _personId;
    public EndowCommand(string actorId, string personId) { ActorId = actorId; _personId = personId; }
    public string ActorId { get; }
    public string Describe() => $"endow {_personId}";
    public string? Refusal(CommandContext ctx) => WealthSystem.EndowRefusal(ctx.World, ctx.World.Char(ActorId), ctx.World.Char(_personId));
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; WealthSystem.Endow(w, w.Char(ActorId)!, w.Char(_personId)!); }
}

/// <summary>Build the next grade of the house's seat: a manor on a world of the house's own that the head names, then a great
/// house and a palace where the manor stands.</summary>
public sealed class BuildSeatCommand : ICommand
{
    private readonly string _holdingId;
    public BuildSeatCommand(string actorId, string holdingId) { ActorId = actorId; _holdingId = holdingId; }
    public string ActorId { get; }
    public string Describe() => $"build seat {_holdingId}".Trim();
    public string? Refusal(CommandContext ctx) => WealthSystem.SeatRefusal(ctx.World, ctx.World.Char(ActorId), _holdingId);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; WealthSystem.BuildSeat(w, w.Char(ActorId)!, _holdingId); }
}

/// <summary>Buy one grown person of the house, or the head, a house at the capital: out of the treasury, or out of the head's own
/// purse. It is that person's own.</summary>
public sealed class TownHouseCommand : ICommand
{
    private readonly string _personId; private readonly bool _fromPurse;
    public TownHouseCommand(string actorId, string personId, bool fromPurse) { ActorId = actorId; _personId = personId; _fromPurse = fromPurse; }
    public string ActorId { get; }
    public string Describe() => $"town house {_personId}{(_fromPurse ? " from the purse" : "")}";
    public string? Refusal(CommandContext ctx) => WealthSystem.TownHouseRefusal(ctx.World, ctx.World.Char(ActorId), ctx.World.Char(_personId), _fromPurse);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; WealthSystem.BuyTownHouse(w, w.Char(ActorId)!, w.Char(_personId)!, _fromPurse); }
}

/// <summary>Put the treasury's coin behind one of the house who is standing for an office of the ladder (or behind the head's own
/// canvass): modest, handsome or lavish. A larger one may be paid for later, for the difference.</summary>
public sealed class FundCampaignCommand : ICommand
{
    private readonly string _personId, _size; private readonly OfficeCatalog _offices;
    public FundCampaignCommand(string actorId, string personId, string size, OfficeCatalog offices) { ActorId = actorId; _personId = personId; _size = size; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => $"fund {_size} canvass {_personId}";
    public string? Refusal(CommandContext ctx) => LadderSystem.CampaignRefusal(ctx.World, _offices, ctx.World.Char(ActorId), ctx.World.Char(_personId), _size);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; LadderSystem.Campaign(w, _offices, w.Char(ActorId)!, w.Char(_personId)!, _size); }
}
