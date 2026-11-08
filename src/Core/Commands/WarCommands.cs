using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Declare war on another house (GDD §6/§13 Military path). Costs Influence; sours the two heads.</summary>
public sealed class DeclareWarCommand : ICommand
{
    private readonly string _targetHouseId;
    private readonly FleetRules _rules;
    public DeclareWarCommand(string actorId, string targetHouseId, FleetRules rules) { ActorId = actorId; _targetHouseId = targetHouseId; _rules = rules; }

    public string ActorId { get; }
    public string Describe() => $"declare war on {_targetHouseId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || actor.HouseId == _targetHouseId || w.House(_targetHouseId) == null) return false;
        if (w.AtWar(actor.HouseId, _targetHouseId)) return false;
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = _rules.DeclareWarCost });
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = _rules.DeclareWarCost });
        new FleetSystem(_rules).DeclareWar(w, actor.HouseId, _targetHouseId, ActorId);
        foreach (var head in w.LivingCharacters().Where(c => c.HouseId == _targetHouseId))
            w.Relationship(head.Id, ActorId).Disposition = Math.Clamp(w.Relationship(head.Id, ActorId).Disposition - 30, -100, 100);
    }
}

/// <summary>Sue for peace. Costs Influence. Toward an NPC house: refused while it smells victory (its hulls far
/// outnumber yours), otherwise peace at once. Toward the player's house: never auto-resolved — the actor's head is
/// flagged <c>suing_for_peace</c> and the player answers through the envoy event (GDD §15: decisions are scenes).</summary>
public sealed class SueForPeaceCommand : ICommand
{
    private readonly string _targetHouseId;
    private readonly FleetRules _rules;
    public SueForPeaceCommand(string actorId, string targetHouseId, FleetRules rules) { ActorId = actorId; _targetHouseId = targetHouseId; _rules = rules; }

    public string ActorId { get; }
    public string Describe() => $"sue for peace with {_targetHouseId}";

    public bool CanExecute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId);
        if (actor?.Alive != true || !w.AtWar(actor.HouseId, _targetHouseId)) return false;
        bool toPlayer = _targetHouseId == w.Protagonist?.HouseId;
        if (toPlayer && actor.Flags.Contains(FleetSystem.SuingForPeaceFlag)) return false;       // offer already on the table
        if (!toPlayer && !new FleetSystem(_rules).WouldAcceptPeace(w, _targetHouseId, actor.HouseId)) return false;
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = _rules.PeaceCost });
    }

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World;
        var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = _rules.PeaceCost });
        if (_targetHouseId == w.Protagonist?.HouseId)
        {
            actor.Flags.Add(FleetSystem.SuingForPeaceFlag);                                        // the player decides
            w.Log($"{actor.Name} of {w.House(actor.HouseId)?.Name ?? actor.HouseId} sues for peace.");
            return;
        }
        var fleets = new FleetSystem(_rules);
        int tribute = fleets.Tribute(w, actor.HouseId, _targetHouseId);          // the side behind on score pays
        if (tribute > 0)
        {
            w.House(actor.HouseId)!.Treasury.Add(Resource.Credits, -tribute);
            w.House(_targetHouseId)!.Treasury.Add(Resource.Credits, tribute);
            w.Log($"{w.House(actor.HouseId)?.Name} pays {tribute} credits in tribute.");
        }
        fleets.MakePeace(w, actor.HouseId, _targetHouseId);
        foreach (var head in w.LivingCharacters().Where(c => c.HouseId == _targetHouseId))
            w.Relationship(head.Id, ActorId).Disposition = Math.Clamp(w.Relationship(head.Id, ActorId).Disposition + 10, -100, 100);
    }
}


/// <summary>Buy a free hold back (GAP §6 "free holds are orphans"): a world that broke away from your house swears to it again for
/// a price that rises with its people and its leader's grudge. The free hold's leader comes in as the world's governor; a free
/// house left with nothing is folded away, its militia with it. The other way back is war.</summary>
public sealed class ReclaimFreeholdCommand : ICommand
{
    private readonly string _holdingId;
    public ReclaimFreeholdCommand(string actorId, string holdingId) { ActorId = actorId; _holdingId = holdingId; }
    public string ActorId { get; }
    public string Describe() => $"reclaim {_holdingId}";

    public static int Price(World w, Holding h, string actorHouse)
    {
        var leader = Systems.Economy.HeadOf(w, w.House(h.OwnerId)!); var ours = w.House(actorHouse) is { } ah ? Systems.Economy.HeadOf(w, ah) : null;
        int grudge = leader != null && ours != null ? Math.Max(0, -(w.GetRelationship(leader.Id, ours.Id)?.Disposition ?? 0)) : 0;
        return 15 + h.Population / 2 + grudge / 2;
    }

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var h = w.Holding(_holdingId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || h == null || house == null) return "no such world";
        var free = w.House(h.OwnerId);
        if (free == null || free.Bloc != Systems.RegionSystem.FreeholdsBloc) return "not a free hold";
        if (h.FormerOwnerId != house.Id) return "it was never yours";
        if (w.AtWar(house.Id, free.Id)) return "not while you make war on them";
        if (h.InRebellion) return "it is in revolt";
        if (h.Unrest >= 70) return "its people would not have you back";
        return house.Treasury.Credits >= Price(w, h, house.Id) ? null : $"needs {Price(w, h, house.Id)} credits";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;

    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var h = w.Holding(_holdingId)!; var free = w.House(h.OwnerId)!;
        int price = Price(w, h, house.Id); house.Treasury.Credits -= price;
        var leader = Systems.Economy.HeadOf(w, free);
        h.OwnerId = house.Id; h.FormerOwnerId = free.Id; h.Unrest = Math.Min(100, h.Unrest + 10); h.Prosperity = Math.Min(100, h.Prosperity + 5);
        bool folded = !w.HoldingsOf(free.Id).Any();
        if (leader != null)
        {
            if (folded) { free.Members.Remove(leader.Id); leader.HouseId = house.Id; house.Members.Add(leader.Id); h.GovernorId = leader.Id; w.Relationship(leader.Id, actor.Id).Disposition = Math.Clamp((w.GetRelationship(leader.Id, actor.Id)?.Disposition ?? 0) + 40, -100, 100); }
            else h.GovernorId = actor.Id;
        }
        if (folded)
        {
            foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == free.Id)) { f.OwnerId = house.Id; f.Name = f.Name.Replace("Militia", "Levy"); }
            foreach (var b in w.Businesses.Values.Where(b => b.OwnerId == free.Id)) b.OwnerId = house.Id;
            house.Treasury.Add(free.Treasury);
            foreach (var c in w.Characters.Values.Where(c => c.HouseId == free.Id)) { c.HouseId = house.Id; house.Members.Add(c.Id); }
            w.Wars.RemoveAll(x => x.Involves(free.Id)); w.Houses.Remove(free.Id);
        }
        house.Legitimacy += 2; w.WorldCounters["reclaimed"] = w.Counter("reclaimed") + 1;
        w.Log($"{house.Name} buys {h.Name} back for {price} credits; {(leader != null ? leader.Name + (folded ? " keeps the governor's chair under the old banner" : " takes the coin and keeps the rest") : "the free hold")} swears again.");
        Systems.FleetSystem.RefreshWarFlags(w);
    }
}
