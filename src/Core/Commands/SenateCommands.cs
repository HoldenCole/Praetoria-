using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Move a motion before the Senate (GDD §6.1). A baron or better, or a Magistrate head, for the kind's influence; one open motion
/// a house; the docket holds two. The vote is at the end of the next turn.</summary>
public sealed class ProposeMotionCommand : ICommand
{
    private readonly string _kind, _targetId, _officeId; private readonly SenateSystem _senate;
    public ProposeMotionCommand(string actorId, string kind, string targetId, SenateSystem senate, string officeId = "") { ActorId = actorId; _kind = kind; _targetId = targetId; _senate = senate; _officeId = officeId; }
    public string ActorId { get; }
    public string Describe() => $"move {_kind}{(_targetId.Length > 0 ? " on " + _targetId : "")}{(_officeId.Length > 0 ? " for the " + _officeId : "")} before the Senate";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var def = _senate.Motions.Def(_kind); if (def == null) return "no such motion";
        if (!_senate.MayPropose(w, house.Id)) return "the Senate hears a baron, or a Magistrate";
        if (w.Motions.Any(m => m.Open && m.ProposerHouseId == house.Id)) return "you already have a motion before the Senate";
        if (SenateSystem.Docket(w).Count() >= SenateSystem.DocketSize) return "the docket is full this session";
        if (SenateSystem.Stale(w, house.Id, _kind, def.Target == "none" ? "" : def.Office ? _targetId + ":" + _officeId : _targetId)) return "the chamber heard that from you lately";
        if (def.Office)
        {
            var office = _senate.Offices.Def(_officeId); if (office == null) return "name an office";
            if (OfficeSystem.Holds(w, _targetId, office.Id)) return $"{w.House(_targetId)?.Name} holds it already";
            if (OfficeSystem.Candidate(w, office, _targetId) == null) return $"no one of {w.House(_targetId)?.Name} has the rank for the {office.Name}";
            if (office.CommandsFleets && RegionSystem.CapitalHolder(w) is { } chair && chair.Id != _targetId && w.AtWar(_targetId, chair.Id)) return "the chair will not arm a house at war with it";
            if (w.Motions.Any(m => m.Open && m.Kind == _kind && m.OfficeId == _officeId)) return "the office is already before the Senate";
        }
        if (def.Target == "house")
        {
            var t = w.House(_targetId); if (t == null || _targetId == PirateSystem.HouseId) return "no such house";
            if (_targetId == house.Id && !def.TargetSelfAllowed) return "not against yourself";
            if (def.TargetMustBeAtWar && !w.Wars.Any(x => x.Involves(_targetId))) return $"{t.Name} is at peace";
            if (w.Motions.Any(m => m.Open && m.Kind == _kind && m.TargetId == _targetId)) return "already before the Senate";
        }
        else if (def.Target == "region") { if (w.Galaxy?.Regions.Any(r => r.Id == _targetId) != true) return "no such region"; }
        int cost = Cost(w, house.Id, def);
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = cost }) ? null : $"needs {cost} influence";
    }
    /// <summary>The kind's influence, less the Speaker's discount, never under one.</summary>
    public int Cost(World w, string houseId, MotionDef def) => Math.Max(1, def.ProposeInfluence - OfficeSystem.HeldBy(w, houseId).Sum(o => _senate.Offices.Def(o.OfficeId)?.MotionDiscount ?? 0));
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var def = _senate.Motions.Def(_kind)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = Cost(w, actor.HouseId, def) });
        var m = _senate.Add(w, actor.HouseId, _kind, def.Target == "none" ? "" : _targetId, def.Office ? _officeId : "");
        m.Votes[actor.HouseId] = 1;
    }
}

/// <summary>Declare the house's vote on an open motion: +1 for, −1 against, 0 abstain. Free — a vote is a right, not a favour.</summary>
public sealed class VoteCommand : ICommand
{
    private readonly string _motionId; private readonly int _stance;
    public VoteCommand(string actorId, string motionId, int stance) { ActorId = actorId; _motionId = motionId; _stance = Math.Sign(stance); }
    public string ActorId { get; }
    public string Describe() => $"vote {(_stance > 0 ? "for" : _stance < 0 ? "against" : "abstain on")} {_motionId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true || w.House(actor.HouseId) == null) return "no house";
        var m = w.Motions.FirstOrDefault(x => x.Id == _motionId); if (m == null || !m.Open) return "no such motion before the Senate";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; var actor = w.Char(ActorId)!; w.Motions.First(x => x.Id == _motionId).Votes[actor.HouseId] = _stance; }
}

/// <summary>Whip a house on an open motion (GDD §9 influence "Senate maneuvers"): one influence to lean a house that does not dislike you,
/// or five credits to lean one that does — a bribe weighs more and asks nothing of friendship. Once per house per motion.</summary>
public sealed class LobbyCommand : ICommand
{
    private readonly string _motionId, _houseId; private readonly int _side; private readonly bool _bribe;
    public LobbyCommand(string actorId, string motionId, string houseId, int side, bool bribe = false) { ActorId = actorId; _motionId = motionId; _houseId = houseId; _side = Math.Sign(side); _bribe = bribe; }
    public string ActorId { get; }
    public string Describe() => $"{(_bribe ? "bribe" : "lobby")} {_houseId} to vote {(_side > 0 ? "for" : "against")} {_motionId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var m = w.Motions.FirstOrDefault(x => x.Id == _motionId); if (m == null || !m.Open) return "no such motion before the Senate";
        var target = w.House(_houseId); if (target == null || _houseId == house.Id || _houseId == PirateSystem.HouseId) return "no such house";
        if (_side == 0) return "for or against";
        if (m.Lobbied.Contains(house.Id + "→" + _houseId)) return "you have already had their ear";
        var head = Economy.HeadOf(w, target); if (head == null) return "no one to speak to";
        if (_bribe) return house.Treasury.Credits >= SenateSystem.BribeCredits ? null : $"needs {SenateSystem.BribeCredits} credits";
        if ((w.GetRelationship(head.Id, actor.Id)?.Disposition ?? 0) < -20) return $"{head.Name} will not hear you — try coin";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = 1 }) ? null : "needs 1 influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var m = w.Motions.First(x => x.Id == _motionId);
        if (_bribe) house.Treasury.Credits -= SenateSystem.BribeCredits; else ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = 1 });
        m.Lobby[_houseId] = m.Lobby.GetValueOrDefault(_houseId) + _side * (_bribe ? SenateSystem.BribeWeight : SenateSystem.LobbyWeight);
        m.Lobbied.Add(house.Id + "→" + _houseId);
        m.Votes.Remove(_houseId);   // a lobbied house reconsiders
        var head = Economy.HeadOf(w, w.House(_houseId)!); if (head != null && !_bribe) w.Relationship(head.Id, actor.Id).Disposition = Math.Clamp((w.GetRelationship(head.Id, actor.Id)?.Disposition ?? 0) + 2, -100, 100);
        w.WorldCounters["lobbied"] = w.Counter("lobbied") + 1;
    }
}

/// <summary>Take a seat with a Senate faction (two influence): the leader's head must not dislike you, and you must sit with no other bench.</summary>
public sealed class JoinFactionCommand : ICommand
{
    private readonly string _factionId;
    public JoinFactionCommand(string actorId, string factionId) { ActorId = actorId; _factionId = factionId; }
    public string ActorId { get; }
    public string Describe() => $"join {_factionId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var f = w.Factions.FirstOrDefault(x => x.Id == _factionId); if (f == null) return "no such bench";
        if (FactionSystem.Of(w, house.Id) != null) return "you sit with a bench already";
        var leaderHead = w.House(f.LeaderHouseId) is { } lh ? Economy.HeadOf(w, lh) : null;
        if (leaderHead != null && (w.GetRelationship(leaderHead.Id, actor.Id)?.Disposition ?? 0) < -10) return $"{leaderHead.Name} will not have you on the bench";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = FactionSystem.JoinInfluence }) ? null : $"needs {FactionSystem.JoinInfluence} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var f = w.Factions.First(x => x.Id == _factionId);
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = FactionSystem.JoinInfluence });
        f.Members.Add(actor.HouseId);
        if (FactionSystem.Affinity(w, actor.HouseId, f.LeaderHouseId) < FactionSystem.FormThreshold) w.WorldCounters[FactionSystem.AffinityKey(actor.HouseId, f.LeaderHouseId)] = FactionSystem.FormThreshold;
        w.WorldFlags.Add("faction_joined");
        w.Log($"{w.House(actor.HouseId)?.Name} takes its seat with {f.Name}.");
    }
}

/// <summary>Leave your bench. Free, but the leader takes it ill.</summary>
public sealed class LeaveFactionCommand : ICommand
{
    public LeaveFactionCommand(string actorId) { ActorId = actorId; }
    public string ActorId { get; }
    public string Describe() => "leave the bench";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); if (actor?.Alive != true || w.House(actor.HouseId) == null) return "no house";
        return FactionSystem.Of(w, actor.HouseId) == null ? "you sit with no bench" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var f = FactionSystem.Of(w, actor.HouseId)!;
        if (f.LeaderHouseId != actor.HouseId) FactionSystem.BreakWhip(w, actor.HouseId, f);
        f.Members.Remove(actor.HouseId);
        w.Log($"{w.House(actor.HouseId)?.Name} leaves {f.Name}.");
    }
}

/// <summary>Found a bench of your own (two influence): two houses or more with the affinity for you and no bench of their own sit with you, and you lead.</summary>
public sealed class FoundFactionCommand : ICommand
{
    public FoundFactionCommand(string actorId) { ActorId = actorId; }
    public string ActorId { get; }
    public string Describe() => "found a bench";
    public static List<string> Followers(World w, string houseId) =>
        w.Houses.Values.Where(h => h.Id != houseId && h.Id != PirateSystem.HouseId && FactionSystem.Of(w, h.Id) == null && FactionSystem.Affinity(w, h.Id, houseId) >= FactionSystem.FormThreshold)
            .OrderByDescending(h => FactionSystem.Affinity(w, h.Id, houseId)).ThenBy(h => h.Id, StringComparer.Ordinal).Take(4).Select(h => h.Id).ToList();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (FactionSystem.Of(w, house.Id) != null) return "you sit with a bench already";
        int n = Followers(w, house.Id).Count; if (n < 2) return n == 0 ? "no house has voted with you often enough" : "one house would follow; a bench needs two";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = FactionSystem.FoundInfluence }) ? null : $"needs {FactionSystem.FoundInfluence} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = FactionSystem.FoundInfluence });
        FactionSystem.Form(w, actor.HouseId, Followers(w, actor.HouseId), actor.HouseId);
    }
}
