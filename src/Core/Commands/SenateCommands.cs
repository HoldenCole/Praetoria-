using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Move a motion before the Senate (GDD §6.1). A baron or better, or a Magistrate head, for the kind's influence; one open motion
/// a house; the docket holds two. The vote is at the end of the next turn.</summary>
public sealed class ProposeMotionCommand : ICommand
{
    private readonly string _kind, _targetId; private readonly SenateSystem _senate;
    public ProposeMotionCommand(string actorId, string kind, string targetId, SenateSystem senate) { ActorId = actorId; _kind = kind; _targetId = targetId; _senate = senate; }
    public string ActorId { get; }
    public string Describe() => $"move {_kind}{(_targetId.Length > 0 ? " against " + _targetId : "")} before the Senate";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var def = _senate.Motions.Def(_kind); if (def == null) return "no such motion";
        if (!_senate.MayPropose(w, house.Id)) return "the Senate hears a baron, or a Magistrate";
        if (w.Motions.Any(m => m.Open && m.ProposerHouseId == house.Id)) return "you already have a motion before the Senate";
        if (SenateSystem.Docket(w).Count() >= SenateSystem.DocketSize) return "the docket is full this session";
        if (SenateSystem.Stale(w, house.Id, _kind, def.Target == "none" ? "" : _targetId)) return "the chamber heard that from you lately";
        if (def.Target == "house")
        {
            var t = w.House(_targetId); if (t == null || _targetId == PirateSystem.HouseId) return "no such house";
            if (_targetId == house.Id && !def.TargetSelfAllowed) return "not against yourself";
            if (def.TargetMustBeAtWar && !w.Wars.Any(x => x.Involves(_targetId))) return $"{t.Name} is at peace";
            if (w.Motions.Any(m => m.Open && m.Kind == _kind && m.TargetId == _targetId)) return "already before the Senate";
        }
        else if (def.Target == "region") { if (w.Galaxy?.Regions.Any(r => r.Id == _targetId) != true) return "no such region"; }
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = def.ProposeInfluence }) ? null : $"needs {def.ProposeInfluence} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var def = _senate.Motions.Def(_kind)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = def.ProposeInfluence });
        var m = _senate.Add(w, actor.HouseId, _kind, def.Target == "none" ? "" : _targetId);
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
