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
