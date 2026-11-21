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
        return no ?? (ctx.PoolsOf(ActorId).Short(Cost(_what)));
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
        return HouseholdSystem.MentorRefusal(w, me, w.Char(_mentorId)) ?? (ctx.PoolsOf(ActorId).Short(CostMap));
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

/// <summary>Tell the steward what to favour (<see cref="HouseholdSystem.Favours"/>).</summary>
public sealed class StewardFavourCommand : ICommand
{
    private readonly string _favour;
    public StewardFavourCommand(string actorId, string favour) { ActorId = actorId; _favour = favour; }
    public string ActorId { get; }
    public string Describe() => $"tell the steward to favour {(_favour.Length == 0 ? "their own judgment" : _favour)}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var actor = w.Char(ActorId); var house = actor != null ? w.House(actor.HouseId) : null;
        if (actor?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != actor.Id) return "not yours to say";
        if (house.Duty(HouseholdSystem.Steward) == null) return "the house has no steward";
        return HouseholdSystem.Favours.Contains(_favour) ? null : "no such instruction";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { var w = ctx.World; w.House(w.Char(ActorId)!.HouseId)!.Duty(HouseholdSystem.Steward)!.Favour = _favour; }
}

/// <summary>Education (docs/EDUCATION.md). <see cref="Study"/>: what you yourself study (your own choice only).
/// <see cref="Upbringing"/>: the course set for a child of the house, or empty to let them follow their bent.
/// <see cref="Tutor"/>: a paid teacher for yourself or anyone of the house (0 none, 1 good, 2 the best).</summary>
public sealed class EducationCommand : ICommand
{
    public const string Study = "study", Upbringing = "upbringing", Tutor = "tutor", Ask = "ask", Teach = "teach", AskAndPay = "ask_and_pay", AskAndSpeak = "ask_and_speak", Thank = "thank";
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
            case Ask: return EducationSystem.AskRefusal(w, head, who, _value);
            case Teach: return EducationSystem.TeachRefusal(w, head, who, w.Char(_value));
            case AskAndPay: return EducationSystem.AskAndPayRefusal(w, head, who, _value);
            case AskAndSpeak:
                return EducationSystem.AskAndSpeakRefusal(w, head, who, _value) ?? (ctx.PoolsOf(ActorId).Short(new Dictionary<string, int> { [Pool.Influence] = EducationSystem.FavourPrice(w, head, who, _value) }));
            case Thank: return EducationSystem.ThankRefusal(w, head, who);
            case Study:
                if (who.Id != head.Id) return "what they study is their own affair; you may ask";
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
        if (_what == Ask) EducationSystem.Ask(ctx.World, ctx.World.Char(ActorId)!, who, _value);
        else if (_what == Teach) EducationSystem.SetToTeach(ctx.World, ctx.World.Char(ActorId)!, who, ctx.World.Char(_value)!);
        else if (_what == AskAndPay) EducationSystem.AskAndPay(ctx.World, ctx.World.Char(ActorId)!, who, _value);
        else if (_what == AskAndSpeak)
        {
            var head = ctx.World.Char(ActorId)!; ctx.PoolsOf(ActorId).Spend(new Dictionary<string, int> { [Pool.Influence] = EducationSystem.FavourPrice(ctx.World, head, who, _value) });
            EducationSystem.AskAndSpeak(ctx.World, head, who, _value);
        }
        else if (_what == Thank) EducationSystem.Thank(ctx.World, ctx.World.Char(ActorId)!, who);
        else if (_what == Study) who.Study = _value; else if (_what == Upbringing) who.Upbringing = _value; else who.Tutor = int.Parse(_value);
    }
}

/// <summary>Personal wealth (docs/WEALTH.md). <see cref="Allowance"/>: what the house gives its grown members.
/// <see cref="Inheritance"/>: whether the treasury is kept whole or shared out when the head dies. <see cref="Move"/>: credits
/// between the head's own purse and the treasury. <see cref="Settle"/>: pay what one of the house owes (value "purse" or
/// "treasury"). <see cref="Will"/>: who is to have what part of your purse. <see cref="Bequeath"/>, <see cref="Give"/>,
/// <see cref="Sell"/>: an heirloom.</summary>
public sealed class WealthCommand : ICommand
{
    public const string Allowance = "allowance", Inheritance = "inheritance", Move = "move", Settle = "settle", Will = "will", Legacy = "legacy", Bequeath = "bequeath", Give = "give", Sell = "sell", Gift = "gift", Lend = "lend", Forgive = "forgive", Dowries = "dowries", EntailItem = "entail_item", BreakEntail = "break_entail", LendAtInterest = "lend_interest", Borrow = "borrow", Repay = "repay", Commission = "commission", Ransom = "ransom", Favour = "favour";
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
            case Legacy: return WealthSystem.LegacyRefusal(w, me, _targetId, int.TryParse(_value, out int part) ? part : -1);
            case Bequeath: return WealthSystem.BequeathRefusal(w, me, item, _value);
            case Give: return WealthSystem.GiveRefusal(w, me, item, w.Char(_value));
            case Sell: return WealthSystem.SellRefusal(w, me, item);
            case Gift: case Lend: case LendAtInterest: return int.TryParse(_value, out int sum) ? WealthSystem.GiftRefusal(w, me, w.Char(_targetId), sum, _what != Gift) : "not a sum";
            case EntailItem: case BreakEntail: return WealthSystem.EntailRefusal(w, me, item, _what == EntailItem);
            case Borrow: return int.TryParse(_value, out int ask) ? WealthSystem.BorrowRefusal(w, me, w.Char(_targetId), ask) : "not a sum";
            case Repay: return WealthSystem.RepayRefusal(w, me, w.Char(_targetId));
            case Forgive: return WealthSystem.ForgiveRefusal(w, me, w.Char(_targetId));
            case Dowries: return _value is "purse" or "treasury" ? null : "no such custom";
            case Commission: return WealthSystem.CommissionRefusal(w, me, _value);
            case Ransom: return WealthSystem.RansomRefusal(w, me, item);
            case Favour: return WealthSystem.CallRefusal(w, me, w.House(_targetId), _value);
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
            case Will: me.Will ??= new State.Will(); me.Will.LegateeId = int.Parse(_value) == 0 ? "" : _targetId; me.Will.SharePct = _targetId.Length == 0 ? 0 : int.Parse(_value);
                /* the one given the larger share has no smaller one besides, and the smaller ones give way where the whole would be overrun */
                me.Will.Also.Remove(me.Will.LegateeId); foreach (var k in me.Will.Also.Keys.OrderByDescending(k => k, StringComparer.Ordinal).ToList()) if (me.Will.SharePct + me.Will.Also.Values.Sum() > 100) me.Will.Also.Remove(k);
                break;
            case Legacy: me.Will ??= new State.Will(); if (int.Parse(_value) == 0) me.Will.Also.Remove(_targetId); else me.Will.Also[_targetId] = int.Parse(_value); break;
            case Bequeath: me.Will ??= new State.Will(); if (_value.Length == 0) me.Will.Items.Remove(_targetId); else me.Will.Items[_targetId] = _value; break;
            case Give: WealthSystem.Give(w, me, item!, w.Char(_value)!); break;
            case Sell: WealthSystem.Sell(w, me, item!); break;
            case Gift: WealthSystem.Gift(w, me, w.Char(_targetId)!, int.Parse(_value)); break;
            case Lend: WealthSystem.Lend(w, me, w.Char(_targetId)!, int.Parse(_value)); break;
            case Forgive: WealthSystem.Forgive(w, me, w.Char(_targetId)!); break;
            case LendAtInterest: WealthSystem.LendAtInterest(w, me, w.Char(_targetId)!, int.Parse(_value)); break;
            case EntailItem: case BreakEntail: WealthSystem.SetEntail(w, me, item!, _what == EntailItem); break;
            case Borrow: WealthSystem.Borrow(w, me, w.Char(_targetId)!, int.Parse(_value)); break;
            case Repay: WealthSystem.Repay(w, me, w.Char(_targetId)!); break;
            case Dowries: house.DowryFromPurse = _value == "purse"; break;
            case Commission: WealthSystem.Commission(w, me, _value); break;
            case Ransom: WealthSystem.Ransom(w, me, item!); break;
            case Favour: WealthSystem.Call(w, me, w.House(_targetId)!, _value); break;
        }
    }
}

/// <summary>Ambitions (docs/AMBITIONS.md). <see cref="Grant"/> or <see cref="Refuse"/> what one of the house has asked the head
/// for; <see cref="NameHeir"/> someone of the house to succeed you (an empty person names nobody, and the law decides).</summary>
public sealed class AmbitionCommand : ICommand
{
    public const string Grant = "grant", Refuse = "refuse", NameHeir = "name_heir", SecondStart = "second_start";
    private readonly string _what, _personId; private readonly AmbitionSystem _system;
    public AmbitionCommand(string actorId, string what, string personId, AmbitionSystem system) { ActorId = actorId; _what = what; _personId = personId; _system = system; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var who = _personId.Length > 0 ? w.Char(_personId) : null;
        if (_what == NameHeir) return _personId.Length > 0 && who == null ? "no such person" : AmbitionSystem.NameHeirRefusal(w, head, who);
        if (_what == SecondStart) return _system.SecondStartOffer(w, head, who, ctx).refusal;
        return _system.AnswerRefusal(w, head, who, _what == Grant, ctx.PoolsOf(ActorId));
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var who = _personId.Length > 0 ? w.Char(_personId) : null;
        if (_what == SecondStart) { _system.SecondStart(w, head, who!, new CommandExecutor(), ctx); return; }
        if (_what == NameHeir) AmbitionSystem.NameHeir(w, head, who); else _system.Answer(w, head, who!, _what == Grant, new CommandExecutor(), ctx);
    }
}

/// <summary>A life of one's own (docs/LIFE.md). <see cref="Rest"/>: a season away (2 influence, once a year).
/// <see cref="Physicians"/>: 6 credits from your own purse. <see cref="Retire"/>: lay the house down in life to your heir.</summary>
public sealed class LifeCommand : ICommand
{
    public const string Rest = "rest", Physicians = "physicians", Retire = "retire", Send = "send", AskElder = "ask_elder";
    private static readonly Dictionary<string, int> RestCost = new() { [Pool.Influence] = LifeSystem.RestInfluence };
    private readonly string _what, _personId;
    public LifeCommand(string actorId, string what, string personId = "") { ActorId = actorId; _what = what; _personId = personId; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_personId}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId);
        return _what switch
        {
            Rest => LifeSystem.RestRefusal(w, me) ?? (ctx.PoolsOf(ActorId).Short(RestCost)),
            Physicians => LifeSystem.PhysicianRefusal(w, me),
            Retire => LifeSystem.RetireRefusal(w, me),
            Send => LifeSystem.SendRefusal(w, me, w.Char(_personId)),
            AskElder => LifeSystem.AskElderRefusal(w, me, _personId),
            _ => "no such thing"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var me = w.Char(ActorId)!;
        if (_what == Rest) { ctx.PoolsOf(ActorId).Spend(RestCost); LifeSystem.Rest(w, me); }
        else if (_what == Physicians) LifeSystem.Physicians(w, me);
        else if (_what == Send) LifeSystem.Send(w, me, w.Char(_personId)!);
        else if (_what == AskElder) LifeSystem.AskElder(w, me, _personId);
        else LifeSystem.Retire(w, me);
    }
}

/// <summary>Take a place on another of the Senate's committees (docs/LADDER.md, "Committees").</summary>
public sealed class CommitteeCommand : ICommand
{
    private readonly string _committee;
    public CommitteeCommand(string actorId, string committee) { ActorId = actorId; _committee = committee; }
    public string ActorId { get; }
    public string Describe() => $"committee {_committee}";
    public string? Refusal(CommandContext ctx) => LadderSystem.ChangeCommitteeRefusal(ctx.World, ctx.World.Char(ActorId), _committee);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) => LadderSystem.ChangeCommittee(ctx.World, ctx.World.Char(ActorId)!, _committee);
}

/// <summary>The courts (docs/LADDER.md). <see cref="Prosecute"/>: bring a charge against a former magistrate for something on the
/// record (the deed's id). <see cref="Buy"/>: offer a sitting praetor a sum (the praetor's id).</summary>
public sealed class ProsecutionCommand : ICommand
{
    public const string Prosecute = "prosecute", Buy = "buy";
    private static readonly Dictionary<string, int> Cost = new() { [Pool.Influence] = LadderSystem.ProsecuteInfluence };
    private readonly string _what, _targetId; private readonly Data.OfficeCatalog _offices;
    public ProsecutionCommand(string actorId, string what, string targetId, Data.OfficeCatalog offices) { ActorId = actorId; _what = what; _targetId = targetId; _offices = offices; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_targetId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId);
        if (!LadderSystem.Active(w, _offices)) return "there are no courts of the Republic";
        if (_what == Buy) return LadderSystem.BuyRefusal(w, head, w.Char(_targetId));
        if (_what != Prosecute) return "no such thing";
        return LadderSystem.ProsecuteRefusal(w, head, w.OfficeRecord.FirstOrDefault(d => d.Id == _targetId)) ?? (ctx.PoolsOf(ActorId).Short(Cost));
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!;
        if (_what == Buy) { LadderSystem.Buy(w, head, w.Char(_targetId)!); return; }
        ctx.PoolsOf(ActorId).Spend(Cost); LadderSystem.Prosecute(w, _offices, head, w.OfficeRecord.First(d => d.Id == _targetId));
    }
}

/// <summary>Answering a suit that names the house before it is heard (docs/PLAYTHROUGH.md 12). <see cref="Settle"/>: come to terms
/// out of court, the side the law is against paying half now. <see cref="Plead"/>: send someone of the house to plead it (1 influence).</summary>
public sealed class SuitCommand : ICommand
{
    public const string Settle = "settle", Plead = "plead";
    private static readonly Dictionary<string, int> Cost = new() { [Pool.Influence] = LadderSystem.PleadInfluence };
    private readonly string _what, _suitId, _advocateId;
    public SuitCommand(string actorId, string what, string suitId, string advocateId = "") { ActorId = actorId; _what = what; _suitId = suitId; _advocateId = advocateId; }
    public string ActorId { get; }
    public string Describe() => $"{_what} the suit {_suitId}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var b = w.OfficeWork.FirstOrDefault(x => x.Id == _suitId);
        if (_what == Settle) return LadderSystem.SettleRefusal(w, head, b);
        if (_what != Plead) return "no such thing";
        return LadderSystem.PleadRefusal(w, head, b, w.Char(_advocateId)) ?? (ctx.PoolsOf(ActorId).Short(Cost));
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId)!; var b = w.OfficeWork.First(x => x.Id == _suitId);
        if (_what == Settle) { LadderSystem.SettleOut(w, head, b); return; }
        ctx.PoolsOf(ActorId).Spend(Cost); LadderSystem.SendPleader(w, head, b, w.Char(_advocateId)!);
    }
}

/// <summary>The ladder of offices (docs/LADDER.md). <see cref="Stand"/>: put your own name down for a rung at the next election
/// (2 influence). <see cref="Withdraw"/> it. <see cref="Back"/>: put your weight behind yourself or someone of the house who is
/// standing (2 influence, twice at most). <see cref="Games"/>: as aedile, give games out of your own purse.</summary>
public sealed class LadderCommand : ICommand
{
    public const string Stand = "stand", Withdraw = "withdraw", Back = "back", Games = "games", Dissuade = "dissuade", Decide = "decide";
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
            Decide => who?.Id == ActorId ? LadderSystem.DecideRefusal(w, who, _rungId) : "the business of their office is their own",
            _ => "no such thing"
        };
        return no ?? (ctx.PoolsOf(ActorId).Short(Cost(_what)));
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
            case Decide: LadderSystem.Decide(w, _offices, who, _rungId); break;   // the choice travels in the rung slot
        }
    }
}

/// <summary>A year's duty that is not an elected office's (docs/LADDER.md, "The bench and the seat"): a judge's hearing, a
/// justice's appeal, a senator's year. It is the holder's own to decide.</summary>
public sealed class DutyCommand : ICommand
{
    private readonly string _kind, _choice; private readonly Data.OfficeCatalog _offices; private readonly Data.PolicyCatalog? _policy;
    public DutyCommand(string actorId, string kind, string choice, Data.OfficeCatalog offices, Data.PolicyCatalog? policy = null) { ActorId = actorId; _kind = kind; _choice = choice; _offices = offices; _policy = policy; }
    public string ActorId { get; }
    public string Describe() => $"{_kind} {_choice}";
    public string? Refusal(CommandContext ctx) => LadderSystem.DutyRefusal(ctx.World, _offices, ctx.World.Char(ActorId), _kind, _choice);
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) => LadderSystem.DecideDuty(ctx.World, _offices, _policy, ctx.World.Char(ActorId)!, _kind, _choice);
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

/// <summary>Breaking away (docs/BREAKAWAY.md): the head of a house that controls a region takes it out of the Reach as a state of its
/// own, or (in the great war) lets it be known that a partition would be accepted.</summary>
public sealed class SecessionCommand : ICommand
{
    public const string Declare = "declare", SeekPartition = "seek_partition", Rejoin = "rejoin", Join = "join", Stir = "stir", Leave = "leave";
    private static readonly Dictionary<string, int> StirCost = new() { [Pool.Influence] = PolitySystem.StirInfluence };
    private readonly string _what, _regionId, _form; private readonly PolitySystem _polities;
    public SecessionCommand(string actorId, string what, string regionId, string form, PolitySystem polities) { ActorId = actorId; _what = what; _regionId = regionId; _form = form; _polities = polities; }
    public string ActorId { get; }
    public string Describe() => $"{_what} {_regionId} {_form}".Trim();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || head.Id != w.ProtagonistId) return "no house";
        return _what switch
        {
            Declare => _polities.DeclareRefusal(w, house, _regionId, _form),
            Rejoin => _polities.RejoinRefusal(w, house),
            Join => _polities.AccedeRefusal(w, house, w.Polities.FirstOrDefault(p => p.Id == _form), _regionId),   // the state travels in the form slot
            Stir => _polities.StirRefusal(w, house, _regionId.Length > 0 ? w.House(_regionId) : null) ?? (_regionId.Length > 0 && w.House(_regionId) == null ? "no such house" : ctx.PoolsOf(ActorId).Short(StirCost)),   // a house to set them against travels in the region slot
            Leave => _polities.DepartRefusal(w, house),
            SeekPartition => !CaesarSystem.GreatWar(w) ? "the Reach is not in its great war" : w.HasFlag(GreatCrisisSystem.PartitionWishFlag) ? "it is already known" : !PolitySystem.InReach(w, house.Id) ? "the house has left the Reach" : null,
            _ => "no such thing"
        };
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var house = w.House(w.Char(ActorId)!.HouseId)!;
        if (_what == Declare) _polities.Declare(w, house, _regionId, _form);
        else if (_what == Rejoin) _polities.Rejoin(w, PolitySystem.Of(w, house.Id)!);
        else if (_what == Join) _polities.Accede(w, house, w.Polities.First(p => p.Id == _form), _regionId);
        else if (_what == Stir) { ctx.PoolsOf(ActorId).Spend(StirCost); _polities.Stir(w, house, _regionId.Length > 0 ? w.House(_regionId) : null); }
        else if (_what == Leave) _polities.Depart(w, house, PolitySystem.Of(w, house.Id)!);
        else { w.WorldFlags.Add(GreatCrisisSystem.PartitionWishFlag); w.Log($"{house.Name} lets it be known that it would take a region of its own as the price of peace."); }
    }
}

/// <summary>Working on a house of the region (docs/BREAKAWAY.md): a quiet word, or a gift, to bring it over before the region goes.</summary>
public sealed class WorkHouseCommand : ICommand
{
    private readonly string _targetHouseId, _how; private readonly PolitySystem _polities;
    public WorkHouseCommand(string actorId, string targetHouseId, string how, PolitySystem polities) { ActorId = actorId; _targetHouseId = targetHouseId; _how = how; _polities = polities; }
    public string ActorId { get; }
    public string Describe() => $"{_how} to {_targetHouseId}";
    private IReadOnlyDictionary<string, int> Cost => _how is PolitySystem.Word or PolitySystem.Promise or PolitySystem.Threat ? new Dictionary<string, int> { [Pool.Influence] = PolitySystem.WordInfluence } : new Dictionary<string, int>();
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var head = w.Char(ActorId); var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || head.Id != w.ProtagonistId) return "no house";
        return _polities.WorkRefusal(w, house, w.House(_targetHouseId), _how) ?? (ctx.PoolsOf(ActorId).Short(Cost));
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; ctx.PoolsOf(ActorId).Spend(Cost);
        _polities.Work(w, w.House(w.Char(ActorId)!.HouseId)!, w.House(_targetHouseId)!, _how);
    }
}
