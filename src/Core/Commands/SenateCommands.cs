using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Move a motion before the Senate (GDD §6.1). A baron or better, or a Judge head, for the kind's influence; one open motion
/// a house; the docket holds two. The vote is at the end of the next turn.</summary>
public sealed class ProposeMotionCommand : ICommand
{
    private readonly string _kind, _targetId, _officeId, _lawId, _optionId; private readonly SenateSystem _senate;
    public ProposeMotionCommand(string actorId, string kind, string targetId, SenateSystem senate, string officeId = "", string lawId = "", string optionId = "")
    { ActorId = actorId; _kind = kind; _targetId = targetId; _senate = senate; _officeId = officeId; _lawId = lawId; _optionId = optionId; }
    public string ActorId { get; }
    public string Describe() => $"move {_kind}{(_targetId.Length > 0 ? " on " + _targetId : "")}{(_officeId.Length > 0 ? " for the " + _officeId : "")}{(_lawId.Length > 0 ? $" that {_lawId} be {_optionId}" : "")} before the Senate";

    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        var def = _senate.Motions.Def(_kind); if (def == null) return "no such motion";
        if (_kind == "petition") return "only an estate brings a petition";
        if (!_senate.MayPropose(w, house.Id)) return w.IsImperium ? $"the Senate hears a {Progression.Title.StyleOf(Progression.Title.Baron, w.Era)}, or a Judge" : "the house has its votes but no voice in the Senate: it may not move a motion";
        if (w.Motions.Any(m => m.Open && m.ProposerHouseId == house.Id)) return "you already have a motion before the Senate";
        if (SenateSystem.Docket(w).Count() >= SenateSystem.DocketSize) return "the docket is full this session";
        if (SenateSystem.Stale(w, house.Id, _kind, def.Law ? _lawId + ":" + _optionId : def.Target == "none" ? "" : def.Office ? _targetId + ":" + _officeId : _targetId)) return "the chamber heard that from you lately";
        if (def.Law)
        {
            var law = _senate.Policies.Law(_lawId); if (law == null || law.Scope != "senate") return "name a law of the Senate";
            var opt = law.Option(_optionId); if (opt == null) return "name one of its options";
            if ((PolicySystem.ReachOption(w, _senate.Policies, law) ?? law.DefaultOption).Id == opt.Id) return "already the law of the Reach";
            if (PolicySystem.OnCooldown(w, PolicySystem.SenateHolder, law.Id)) return $"the Senate settled that lately — {w.Counter($"law_cd:{PolicySystem.SenateHolder}:{law.Id}") - w.Turn} year(s) more";
            if (w.Motions.Any(m => m.Open && m.LawId == law.Id)) return "that law is already before the Senate";
        }
        if (def.Office)
        {
            var office = _senate.Offices.Def(_officeId); if (office == null) return "name an office";
            if (office.Elected) return "the consuls are elected, not commissioned";
            if (OfficeSystem.Holds(w, _targetId, office.Id)) return $"{w.House(_targetId)?.Name} holds it already";
            if (OfficeSystem.Candidate(w, office, _targetId) == null) return $"no one of {w.House(_targetId)?.Name} has the rank for the {office.Name}";
            if (office.CommandsFleets && RegionSystem.CapitalHolder(w) is { } chair && chair.Id != _targetId && w.AtWar(_targetId, chair.Id)) return "the chair will not arm a house at war with it";
            if (w.Motions.Any(m => m.Open && m.Kind == _kind && m.OfficeId == _officeId)) return "the office is already before the Senate";
        }
        if (def.Target == "house")
        {
            var t = w.House(_targetId); if (t == null || World.Outsider(_targetId)) return "no such house";
            if (_targetId == house.Id && !def.TargetSelfAllowed) return "not against yourself";
            if (def.TargetMustBeAtWar && !w.Wars.Any(x => x.Involves(_targetId))) return $"{t.Name} is at peace";
            if (w.Motions.Any(m => m.Open && m.Kind == _kind && m.TargetId == _targetId)) return "already before the Senate";
            if (def.Interest == "triumph" && (_senate.Caesar == null ? "no such motion" : _senate.Caesar.TriumphRefusal(w, t)) is { } noTriumph) return noTriumph;
            if (def.Interest == "reconquest" && (_senate.Polities == null ? "no such motion" : _senate.Polities.ReconquestRefusal(w, t)) is { } noReconquest) return noReconquest;
            if (def.Interest == "recall" && (_senate.Caesar == null ? "no such motion" : _senate.Caesar.RecallRefusal(w, t)) is { } noRecall) return noRecall;
            if (def.Interest == "emergency" && EmergencySystem.GrantRefusal(w, t, _senate.Offices) is { } noPowers) return noPowers;
            if (def.Interest == "for_life" && EmergencySystem.ForLifeRefusal(w, t) is { } noLife) return noLife;
        }
        else if (def.Target == "region") { if (w.Galaxy?.Regions.Any(r => r.Id == _targetId) != true) return "no such region"; }
        else if (def.Target == "nation")
        {
            var d = _senate.Nations.Def(_targetId); var n = w.Nation(_targetId); if (d == null || n == null) return "no such nation";
            if (def.Interest == "foreign_peace")
            {
                if (NationSystem.PeaceRefusal(w, _senate.Nations, _targetId) is { } noPeace) return noPeace;
                if (w.Motions.Any(m => m.Open && m.Kind == _kind)) return "a peace is already before the Senate";
            }
            else
            {
                if (n.AtWar && n.WarOf.Length == 0) return $"the Reach is already at war with {d.Name}";   // a house's own war may be made the Reach's
                if (n.Fallen) return $"{d.Name} is no more";
                if (n.Subdued(w.Turn)) return $"{d.Name} already pays tribute";
                if (NationSystem.AtWar(w) is { } other && other.Id != n.Id) return $"the Reach fights one war abroad at a time ({_senate.Nations.Def(other.Id)?.Name ?? other.Id})";
                if (w.Motions.Any(m => m.Open && m.Kind == _kind)) return "a war is already before the Senate";
            }
        }
        int cost = Cost(w, house.Id, def);
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = cost }) ? null : $"needs {cost} influence";
    }
    /// <summary>The kind's influence, less the Speaker's discount, never under one.</summary>
    public int Cost(World w, string houseId, MotionDef def) => Math.Max(1, def.ProposeInfluence - OfficeSystem.HeldBy(w, houseId).Sum(o => _senate.Offices.Def(o.OfficeId)?.MotionDiscount ?? 0)
        + (OfficeSystem.Holds(w, houseId, "speaker") ? 0 : PolicySystem.ReachModifier(w, _senate.Policies, "motion_cost")));   // the Speaker's standing orders bind everyone else
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var def = _senate.Motions.Def(_kind)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = Cost(w, actor.HouseId, def) });
        var m = _senate.Add(w, actor.HouseId, _kind, def.Target == "none" ? "" : _targetId, def.Office ? _officeId : "", def.Law ? _lawId : "", def.Law ? _optionId : "");
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
        var target = w.House(_houseId); if (target == null || _houseId == house.Id || World.Outsider(_houseId)) return "no such house";
        if (_side == 0) return "for or against";
        if (m.Lobbied.Contains(house.Id + "→" + _houseId)) return "you have already had their ear";
        var head = Economy.HeadOf(w, target); if (head == null) return "no one to speak to";
        if (_bribe) { int price = SenateSystem.BribeCost(w, target, actor); return SenateSystem.BribeRefused(w, target, actor) ?? (house.Treasury.Credits >= price ? null : $"needs {price} credits (the house has {house.Treasury.Credits})"); }
        if ((w.GetRelationship(head.Id, actor.Id)?.Disposition ?? 0) < -20) return $"{head.Name} will not hear you — try coin";
        return ctx.PoolsOf(ActorId).Short(new Dictionary<string, int> { [Pool.Influence] = 1 });
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var house = w.House(actor.HouseId)!; var m = w.Motions.First(x => x.Id == _motionId);
        if (_bribe)
        {
            int price = SenateSystem.BribeCost(w, w.House(_houseId)!, actor); house.Treasury.Credits -= price;
            if (Economy.HeadOf(w, w.House(_houseId)!) is { } taker) taker.Purse += price;   // it is the head who is paid, not the house's treasury
            m.Bought[_houseId] = m.Bought.GetValueOrDefault(_houseId) + _side * SenateSystem.BribeWeight; m.BoughtBy[_houseId] = house.Id;
        }
        else { ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = 1 }); m.Lobby[_houseId] = m.Lobby.GetValueOrDefault(_houseId) + _side * SenateSystem.LobbyWeight; }
        m.Lobbied.Add(house.Id + "→" + _houseId);
        m.Votes.Remove(_houseId);   // a lobbied house reconsiders
        var head = Economy.HeadOf(w, w.House(_houseId)!); if (head != null && !_bribe) w.Relationship(head.Id, actor.Id).Disposition = Math.Clamp((w.GetRelationship(head.Id, actor.Id)?.Disposition ?? 0) + 2, -100, 100);
        w.WorldCounters["lobbied"] = w.Counter("lobbied") + 1;
        // the one who asked is told that it was heard, and by how much (docs/PLAYTHROUGH.md: "a whip or a bribe says whether it worked")
        if (house.Id == w.Protagonist?.HouseId)
            w.Tell("info", _bribe ? $"{w.House(_houseId)?.Name} has taken the purse: its own vote leans {(_side > 0 ? "for" : "against")} the motion by {SenateSystem.BribeWeight} more. Whether that is enough shows in the count"
                : $"{w.House(_houseId)?.Name} has heard you: it leans {(_side > 0 ? "for" : "against")} the motion by {SenateSystem.LobbyWeight} more{(FactionSystem.Leads(w, _houseId) ? ", and its bench by half as much" : "")}. Whether that is enough shows in the count", "senate:chamber", _houseId);
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
        // crossing the floor is one step: the old bench is left and the new one joined, at the price of both
        var old = FactionSystem.Of(w, house.Id);
        if (old != null && old.Id == f.Id) return "you sit with them already";
        var leaderHead = w.House(f.LeaderHouseId) is { } lh ? Economy.HeadOf(w, lh) : null;
        if (leaderHead != null && (w.GetRelationship(leaderHead.Id, actor.Id)?.Disposition ?? 0) < -10) return $"{leaderHead.Name} will not have you on the bench";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = FactionSystem.JoinInfluence }) ? null : $"needs {FactionSystem.JoinInfluence} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var f = w.Factions.First(x => x.Id == _factionId);
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = FactionSystem.JoinInfluence });
        if (FactionSystem.Of(w, actor.HouseId) is { } old) { if (old.LeaderHouseId != actor.HouseId) FactionSystem.Walked(w, actor.HouseId, old); old.Members.Remove(actor.HouseId); w.Log($"{w.House(actor.HouseId)?.Name} crosses the floor from {old.Name}."); }
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
        if (f.LeaderHouseId != actor.HouseId) FactionSystem.Walked(w, actor.HouseId, f);
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
        w.Houses.Values.Where(h => h.Id != houseId && !World.Outsider(h.Id) && FactionSystem.Of(w, h.Id) == null && FactionSystem.Affinity(w, h.Id, houseId) >= FactionSystem.FormThreshold)
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


/// <summary>Stand for the consulship at the next election (docs/REPUBLIC.md): your party nominates you in place of its weightiest, or you run alone.
/// Two influence; a qualified member of the house; not the consul of the last term.</summary>
public sealed class StandForConsulCommand : ICommand
{
    private readonly OfficeCatalog _offices;
    public StandForConsulCommand(string actorId, OfficeCatalog offices) { ActorId = actorId; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => "stand for consul";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (!ElectionSystem.ConsulOffices(_offices).Any()) return "there is no consulship in this age";
        if (w.Era == "imperium") return "the Emperor holds every office";
        if (w.HasFlag(ElectionSystem.StandFlag(house.Id))) return "you already stand";
        if (ElectionSystem.IsConsul(w, _offices, house.Id)) return "you hold the consulship";
        if (!ElectionSystem.Eligible(w, _offices, house.Id)) return OfficeSystem.Candidate(w, ElectionSystem.ConsulOffices(_offices).First(), house.Id) == null ? $"no one of the house may stand: a consul must be {OfficeSystem.Wants(_offices, ElectionSystem.ConsulOffices(_offices).First())}, and not barred from office" : "a consul may not stand again at the next election";
        return ctx.PoolsOf(ActorId).CanAfford(new Dictionary<string, int> { [Pool.Influence] = ElectionSystem.StandInfluence }) ? null : $"needs {ElectionSystem.StandInfluence} influence";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!;
        ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = ElectionSystem.StandInfluence });
        w.WorldFlags.Add(ElectionSystem.StandFlag(actor.HouseId));
        var who = OfficeSystem.Candidate(w, ElectionSystem.ConsulOffices(_offices).First(), actor.HouseId);
        w.Log($"{w.House(actor.HouseId)?.Name} declares for the consulship{(who != null && who.Id != actor.Id ? $", in the person of {who.Name}" : "")}; the election is in {ElectionSystem.NextIn(w)} year(s).");
    }
}

/// <summary>A consul's veto: strike an open motion from the docket before it is voted. Once a term; never your own motion.</summary>
public sealed class VetoCommand : ICommand
{
    private readonly string _motionId; private readonly OfficeCatalog _offices; private readonly SenateSystem _senate;
    public VetoCommand(string actorId, string motionId, OfficeCatalog offices, SenateSystem senate) { ActorId = actorId; _motionId = motionId; _offices = offices; _senate = senate; }
    public string ActorId { get; }
    public string Describe() => $"veto {_motionId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null) return "no house";
        if (!ElectionSystem.IsConsul(w, _offices, house.Id)) return "only a consul may veto";
        var m = w.Motions.FirstOrDefault(x => x.Id == _motionId); if (m == null || !m.Open) return "no such motion before the Senate";
        if (m.ProposerHouseId == house.Id) return "not your own motion";
        if (!ElectionSystem.MayVeto(w, _offices, house.Id)) return w.Emergency == null ? "the veto is spent this term" : EmergencySystem.Holds(w, house.Id) ? "you have struck one motion this year" : "the emergency powers have set your veto aside";
        return null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId)!; var m = w.Motions.First(x => x.Id == _motionId);
        ElectionSystem.Veto(w, _offices, actor.HouseId, m);
        w.Log($"{w.House(actor.HouseId)?.Name}, as consul, vetoes: {_senate.Describe(w, m)}");
    }
}

/// <summary>The chair's own acts on a motion before the Senate (docs/LADDER.md, "The chair"): the Speaker holds it over a year; the
/// presiding consul speaks for it or against it from the chair. Free; once a year each.</summary>
public sealed class ChairCommand : ICommand
{
    private readonly string _motionId, _what; private readonly SenateSystem _senate;
    public ChairCommand(string actorId, string motionId, string what, SenateSystem senate) { ActorId = actorId; _motionId = motionId; _what = what; _senate = senate; }
    public string ActorId { get; }
    public string Describe() => $"chair {_what} {_motionId}";
    public string? Refusal(CommandContext ctx) => LadderSystem.ChairRefusal(ctx.World, ctx.World.Char(ActorId), ctx.World.Motions.FirstOrDefault(x => x.Id == _motionId), _what);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var m = w.Motions.First(x => x.Id == _motionId);
        LadderSystem.Chair(w, w.Char(ActorId)!, m, _what, _senate.Describe(w, m));
    }
}
