using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Found family (docs/HOUSEHOLD.md). <see cref="TakeIn"/> a ward, a protégé or a companion (2 influence);
/// <see cref="Adopt"/> one of the household as an heir (3 influence); <see cref="Release"/> one of them; <see cref="Decline"/>
/// someone asking at the door.</summary>
public sealed class HouseholdCommand : ICommand
{
    public const string TakeIn = "take_in", Adopt = "adopt", Release = "release", Decline = "decline";
    private readonly string _what, _personId, _kind;
    public HouseholdCommand(string actorId, string what, string personId, string kind = "") { ActorId = actorId; _what = what; _personId = personId; _kind = kind; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId}";
    private static IReadOnlyDictionary<string, int> Cost(string what) => what switch
    {
        TakeIn => new Dictionary<string, int> { [Pool.Influence] = HouseholdSystem.TakeInInfluence },
        Adopt => new Dictionary<string, int> { [Pool.Influence] = HouseholdSystem.AdoptInfluence },
        _ => new Dictionary<string, int>()
    };
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var person = w.Char(_personId); var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null) return "no house";
        string? no = _what switch
        {
            TakeIn => HouseholdSystem.TakeInRefusal(w, head, person, _kind),
            Adopt => HouseholdSystem.AdoptRefusal(w, head, person),
            Release => person is { Alive: true } && person.HouseId == house.Id && HouseholdSystem.IsFound(person) ? (w.House(person.BornHouseId) != null ? null : "the house they were born to is gone: they have nowhere else") : "not one of those you took in",
            Decline => house.Petitions.Any(p => p.PersonId == _personId) ? null : "nobody is asking",
            _ => "no such thing"
        };
        return no ?? (ctx.PoolsOf(ActorId).CanAfford(Cost(_what)) ? null : "no influence");
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var house = w.House(head.HouseId)!; ctx.PoolsOf(ActorId).Spend(Cost(_what));
        switch (_what)
        {
            case TakeIn: HouseholdSystem.TakeIn(w, head, w.Char(_personId)!, _kind); break;
            case Adopt: HouseholdSystem.Adopt(w, head, w.Char(_personId)!); break;
            case Release: HouseholdSystem.Release(w, head, w.Char(_personId)!); break;
            case Decline: house.Petitions.RemoveAll(p => p.PersonId == _personId); w.Log($"{head.Name} has no place for {w.Char(_personId)?.Name} in the household."); break;
        }
    }
}

/// <summary>Ask a senior of your own service to take you under their wing (2 influence), or leave the one you have.</summary>
public sealed class MentorCommand : ICommand
{
    private static readonly Dictionary<string, int> CostMap = new() { [Pool.Influence] = 2 };
    private readonly string _mentorId;
    public MentorCommand(string actorId, string mentorId) { ActorId = actorId; _mentorId = mentorId; }
    public string ActorId { get; }
    public string Describe() => _mentorId.Length == 0 ? "leave your mentor" : $"seek {_mentorId} as a mentor";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId);
        if (_mentorId.Length == 0) return me is { MentorId.Length: > 0 } ? null : "you have no mentor";
        if (me is { MentorId.Length: > 0 }) return "you have a mentor already";
        return HouseholdSystem.MentorRefusal(w, me, w.Char(_mentorId)) ?? (ctx.PoolsOf(ActorId).CanAfford(CostMap) ? null : "no influence");
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId)!;
        if (_mentorId.Length == 0) { var old = w.Char(me.MentorId); me.MentorId = ""; if (old != null) { var r = w.Relationship(old.Id, me.Id); r.Disposition = Math.Clamp(r.Disposition - 10, -100, 100); w.Log($"{me.Name} is {old.Name}'s pupil no longer."); } return; }
        ctx.PoolsOf(ActorId).Spend(CostMap); HouseholdSystem.TakeMentor(w, me, w.Char(_mentorId)!);
    }
}

/// <summary>Hand a duty of the house to one of its members, change the reserve they must leave, or (no holder) take it back.</summary>
public sealed class DelegateCommand : ICommand
{
    private readonly string _role, _holderId; private readonly int _reserve;
    public DelegateCommand(string actorId, string role, string holderId, int reserve = 20) { ActorId = actorId; _role = role; _holderId = holderId; _reserve = reserve; }
    public string ActorId { get; }
    public string Describe() => _holderId.Length == 0 ? $"take back the {_role}'s work" : $"make {_holderId} {_role}";
    public string? Refusal(CommandContext ctx) { var w = ctx.World; return HouseholdSystem.AssignRefusal(w, w.Char(ActorId), _role, _holderId.Length == 0 ? null : w.Char(_holderId)); }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; HouseholdSystem.Assign(w, w.Char(ActorId)!, _role, _holderId.Length == 0 ? null : w.Char(_holderId), _reserve); }
}

/// <summary>Education (docs/EDUCATION.md). <see cref="Study"/>: what you yourself study (your own choice only).
/// <see cref="Upbringing"/>: the course set for a child of the house, or empty to let them follow their bent.
/// <see cref="Tutor"/>: a paid teacher for yourself or anyone of the house (0 none, 1 good, 2 the best).</summary>
public sealed class EducationCommand : ICommand
{
    public const string Study = "study", Upbringing = "upbringing", Tutor = "tutor";
    private readonly string _what, _personId, _value;
    public EducationCommand(string actorId, string what, string personId, string value) { ActorId = actorId; _what = what; _personId = personId; _value = value; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId} → {_value}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var who = w.Char(_personId);
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId) return "not of your house";
        switch (_what)
        {
            case Study:
                if (who.Id != head.Id) return "what they study is their own affair";
                if (_value.Length == 0) return null;
                if (!EducationSystem.All.Contains(_value)) return "no such study";
                return who.Skill(_value) < EducationSystem.Ceiling(who, _value) ? null : "you have gone as far in it as you can";
            case Upbringing:
                if (who.Age >= EducationSystem.Adult) return "they are grown, and choose for themselves";
                return _value.Length == 0 || EducationSystem.Groups.Contains(_value) ? null : "no such course";
            case Tutor:
                if (!int.TryParse(_value, out int level) || level is < 0 or > 2) return "no such tutor";
                if (who.Age < EducationSystem.SchoolAge) return "too young for lessons";
                return level == 0 || (w.TreasuryOf(head.Id)?.Credits ?? 0) >= EducationSystem.TutorCost(level) ? null : "the house cannot pay for it";
            default: return "no such thing";
        }
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var who = ctx.World.Char(_personId)!;
        if (_what == Study) who.Study = _value; else if (_what == Upbringing) who.Upbringing = _value; else who.Tutor = int.Parse(_value);
    }
}

/// <summary>Personal wealth (docs/WEALTH.md). <see cref="Allowance"/>: what the house gives its grown members.
/// <see cref="Inheritance"/>: whether the treasury is kept whole or shared out when the head dies. <see cref="Move"/>: credits
/// between the head's own purse and the treasury. <see cref="Settle"/>: pay what one of the house owes (value "purse" or
/// "treasury"). <see cref="Will"/>: who is to have what part of your purse. <see cref="Bequeath"/>, <see cref="Give"/>,
/// <see cref="Sell"/>: an heirloom.</summary>
public sealed class WealthCommand : ICommand
{
    public const string Allowance = "allowance", Inheritance = "inheritance", Move = "move", Settle = "settle", Will = "will", Bequeath = "bequeath", Give = "give", Sell = "sell";
    private readonly string _what, _targetId, _value;
    public WealthCommand(string actorId, string what, string targetId, string value) { ActorId = actorId; _what = what; _targetId = targetId; _value = value; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_targetId} {_value}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId); var house = me != null ? w.House(me.HouseId) : null; if (me?.Alive != true || house == null) return "no house";
        var item = w.Heirlooms.FirstOrDefault(h => h.Id == _targetId);
        switch (_what)
        {
            case Allowance: return WealthSystem.Allowances.Contains(_value) ? null : "no such allowance";
            case Inheritance: return _value is WealthSystem.Entail or WealthSystem.Portions ? null : "no such custom";
            case Move: return int.TryParse(_value, out int amount) ? WealthSystem.MoveRefusal(w, me, amount) : "not a sum";
            case Settle: return WealthSystem.SettleRefusal(w, me, w.Char(_targetId), _value == "purse");
            case Will: return WealthSystem.WillRefusal(w, me, _targetId, int.TryParse(_value, out int pct) ? pct : -1);
            case Bequeath: return item == null || item.HolderId != me.Id ? "not yours to leave" : _value.Length == 0 || w.Char(_value) is { Alive: true } ? null : "no such person";
            case Give: return WealthSystem.GiveRefusal(w, me, item, w.Char(_value));
            case Sell: return item != null && item.HolderId == me.Id ? null : "not yours to sell";
            default: return "no such thing";
        }
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId)!; var house = w.House(me.HouseId)!; var item = w.Heirlooms.FirstOrDefault(h => h.Id == _targetId);
        switch (_what)
        {
            case Allowance: house.Allowance = _value; w.Log($"{house.Name} keeps a {_value} house from now on."); break;
            case Inheritance: house.Inheritance = _value; break;
            case Move: WealthSystem.Move(w, me, int.Parse(_value)); break;
            case Settle: WealthSystem.Settle(w, me, w.Char(_targetId)!, _value == "purse"); break;
            case Will: me.Will ??= new State.Will(); me.Will.LegateeId = int.Parse(_value) == 0 ? "" : _targetId; me.Will.SharePct = _targetId.Length == 0 ? 0 : int.Parse(_value); break;
            case Bequeath: me.Will ??= new State.Will(); if (_value.Length == 0) me.Will.Items.Remove(_targetId); else me.Will.Items[_targetId] = _value; break;
            case Give: WealthSystem.Give(w, me, item!, w.Char(_value)!); break;
            case Sell: WealthSystem.Sell(w, me, item!); break;
        }
    }
}

/// <summary>Ambitions (docs/AMBITIONS.md). <see cref="Grant"/> or <see cref="Refuse"/> what one of the house has asked the head
/// for; <see cref="NameHeir"/> someone of the house to succeed you (an empty person names nobody, and the law decides).</summary>
public sealed class AmbitionCommand : ICommand
{
    public const string Grant = "grant", Refuse = "refuse", NameHeir = "name_heir";
    private readonly string _what, _personId; private readonly AmbitionSystem _system;
    public AmbitionCommand(string actorId, string what, string personId, AmbitionSystem system) { ActorId = actorId; _what = what; _personId = personId; _system = system; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var who = _personId.Length > 0 ? w.Char(_personId) : null;
        if (_what == NameHeir) return _personId.Length > 0 && who == null ? "no such person" : AmbitionSystem.NameHeirRefusal(w, head, who);
        return _system.AnswerRefusal(w, head, who, _what == Grant, ctx.PoolsOf(ActorId));
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var who = _personId.Length > 0 ? w.Char(_personId) : null;
        if (_what == NameHeir) AmbitionSystem.NameHeir(w, head, who); else _system.Answer(w, head, who!, _what == Grant, new CommandExecutor(), ctx);
    }
}

/// <summary>A life of one's own (docs/LIFE.md). <see cref="Rest"/>: a season away (2 influence, once a year).
/// <see cref="Physicians"/>: 6 credits from your own purse. <see cref="Retire"/>: lay the house down in life to your heir.</summary>
public sealed class LifeCommand : ICommand
{
    public const string Rest = "rest", Physicians = "physicians", Retire = "retire";
    private static readonly Dictionary<string, int> RestCost = new() { [Pool.Influence] = LifeSystem.RestInfluence };
    private readonly string _what;
    public LifeCommand(string actorId, string what) { ActorId = actorId; _what = what; }
    public string ActorId { get; }
    public string Describe() => _what;
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId);
        return _what switch
        {
            Rest => LifeSystem.RestRefusal(w, me) ?? (ctx.PoolsOf(ActorId).CanAfford(RestCost) ? null : "no influence"),
            Physicians => LifeSystem.PhysicianRefusal(w, me),
            Retire => LifeSystem.RetireRefusal(w, me),
            _ => "no such thing"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId)!;
        if (_what == Rest) { ctx.PoolsOf(ActorId).Spend(RestCost); LifeSystem.Rest(w, me); }
        else if (_what == Physicians) LifeSystem.Physicians(w, me);
        else LifeSystem.Retire(w, me);
    }
}

/// <summary>The ladder of offices (docs/LADDER.md). <see cref="Stand"/>: put your own name down for a rung at the next election
/// (2 influence). <see cref="Withdraw"/> it. <see cref="Back"/>: put your weight behind yourself or someone of the house who is
/// standing (2 influence, twice at most). <see cref="Games"/>: as aedile, give games out of your own purse.</summary>
public sealed class LadderCommand : ICommand
{
    public const string Stand = "stand", Withdraw = "withdraw", Back = "back", Games = "games", Dissuade = "dissuade";
    private readonly string _what, _personId, _rungId; private readonly Data.OfficeCatalog _offices;
    public LadderCommand(string actorId, string what, string personId, string rungId, Data.OfficeCatalog offices) { ActorId = actorId; _what = what; _personId = personId; _rungId = rungId; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId} {_rungId}".Trim();
    private static IReadOnlyDictionary<string, int> Cost(string what) => what is Stand or Back ? new Dictionary<string, int> { [Pool.Influence] = what == Stand ? LadderSystem.StandInfluence : LadderSystem.BackInfluence } : new Dictionary<string, int>();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var who = w.Char(_personId);
        string? no = _what switch
        {
            Stand => LadderSystem.StandRefusal(w, _offices, head, who, _rungId),
            Withdraw => who != null && who.Id == ActorId && w.Candidacies.Any(c => c.CharacterId == who.Id) ? null : "you are not standing",
            Back => LadderSystem.BackRefusal(w, head, who),
            Dissuade => LadderSystem.DissuadeRefusal(w, _offices, head, who),
            Games => who?.Id == ActorId ? LadderSystem.GamesRefusal(w, _offices, who) : "their games are their own to give",
            _ => "no such thing"
        };
        return no ?? (ctx.PoolsOf(ActorId).CanAfford(Cost(_what)) ? null : "no influence");
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var who = w.Char(_personId)!; ctx.PoolsOf(ActorId).Spend(Cost(_what));
        switch (_what)
        {
            case Stand: LadderSystem.Stand(w, _offices, who, _rungId); break;
            case Withdraw: w.Candidacies.RemoveAll(c => c.CharacterId == who.Id); w.Log($"{who.Name} withdraws their name."); break;
            case Back: LadderSystem.Back(w, head, who); break;
            case Dissuade: LadderSystem.Dissuade(w, _offices, head, who); break;
            case Games: LadderSystem.Games(w, _offices, who); break;
        }
    }
}

/// <summary>What the head of a house does on the Caesar road (docs/CAESAR.md): bring a recalled general home, cross the line,
/// declare for a side in another's civil war, or pay the crews a donative in their commander's name.</summary>
public sealed class CaesarCommand : ICommand
{
    public const string Obey = "obey", Cross = "cross", Join = "join", Donative = "donative";
    private readonly string _what, _target; private readonly CaesarSystem _caesar;
    public CaesarCommand(string actorId, string what, string target, CaesarSystem caesar) { ActorId = actorId; _what = what; _target = target; _caesar = caesar; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_target}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || head.Id != w.ProtagonistId) return "no house";
        return _what switch
        {
            Obey => _caesar.ObeyRefusal(w, house),
            Cross => _caesar.CrossRefusal(w, house),
            Join => _caesar.JoinRefusal(w, house, _target),
            Donative => _caesar.DonativeRefusal(w, house, w.Fleets.GetValueOrDefault(_target)),
            _ => "no such thing"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!;
        switch (_what)
        {
            case Obey: _caesar.Obey(w, house, ordered: true); break;
            case Cross: _caesar.Cross(w, house); break;
            case Join: _caesar.Join(w, house, _target); break;
            case Donative: _caesar.Donative(w, house, w.Fleets[_target]); break;
        }
    }
}
