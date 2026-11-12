using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>Marks a command as the Academy's own business: the only kind the bus will run while the cadet is at the Academy (docs/ACADEMY.md).</summary>
public interface IAcademyCommand : ICommand { }

/// <summary>Choose the subject to read this term. Free: a cadet's time is the Academy's, not the house's pools.</summary>
public sealed class ChooseStudyCommand : IAcademyCommand
{
    private readonly string _subject; private readonly AcademyCatalog _cat;
    public ChooseStudyCommand(string actorId, string subject, AcademyCatalog catalog) { ActorId = actorId; _subject = subject; _cat = catalog; }
    public string ActorId { get; }
    public string Describe() => $"read {_subject}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (!w.InAcademy || ActorId != w.ProtagonistId) return "not at the Academy";
        if (_cat.Subject(_subject) == null) return "no such subject";
        return w.Academy!.Focus == _subject ? "already so" : null;
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { ctx.World.Academy!.Focus = _subject; }
}

/// <summary>How much the Academy teaches: "full" (every lesson, for a first run) or "brief" (the state of the Reach, the houses and the cohort only).</summary>
public sealed class SetTutorialCommand : IAcademyCommand
{
    private readonly string _mode;
    public SetTutorialCommand(string actorId, string mode) { ActorId = actorId; _mode = mode; }
    public string ActorId { get; }
    public string Describe() => $"instruction: {_mode}";
    public bool CanExecute(CommandContext ctx) => ctx.World.Academy != null && ActorId == ctx.World.ProtagonistId && _mode is "full" or "brief";
    public void Execute(CommandContext ctx) { ctx.World.Academy!.Tutorial = _mode; }
}

/// <summary>Ask the commissioning board for a service: the Navy, the Administration or the Courts. Taken at passing out; the board chooses if the cadet does not.</summary>
public sealed class ChooseServiceCommand : IAcademyCommand
{
    private readonly string _track;
    public ChooseServiceCommand(string actorId, string track) { ActorId = actorId; _track = track; }
    public string ActorId { get; }
    public string Describe() => $"ask for {_track}";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; if (!w.InAcademy || ActorId != w.ProtagonistId) return "not at the Academy";
        return AcademySystem.Tracks.Contains(_track) ? null : "no such service";
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx) { ctx.World.Academy!.Service = _track; }
}
