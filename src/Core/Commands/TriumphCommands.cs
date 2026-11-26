using Praetoria.Core.Systems;

namespace Praetoria.Core.Commands;

/// <summary>The player's answer to a triumph the Senate has voted them (docs/CAESAR.md, "The triumph"): come home and lay the command
/// down for a year to have it (<see cref="TriumphSystem.Home"/>), or decline it and keep the fleet (<see cref="TriumphSystem.Keep"/>).
/// Free: it is an answer, not a scheme. Left unanswered for the year, the triumph lapses as if declined.</summary>
public sealed class TriumphCommand : ICommand
{
    private readonly string _choice; private readonly CaesarSystem _caesar;
    public TriumphCommand(string actorId, string choice, CaesarSystem caesar) { ActorId = actorId; _choice = choice; _caesar = caesar; }
    public string ActorId { get; }
    public string Describe() => _choice == TriumphSystem.Home ? "come home for the triumph" : "decline the triumph and keep the fleet";
    public string? Refusal(CommandContext ctx)
    {
        var w = ctx.World; var who = w.Char(ActorId);
        if (who?.Alive != true || who.Id != w.ProtagonistId) return "no one to answer";
        return TriumphSystem.ChoiceRefusal(w, who, _choice);
    }
    public bool CanExecute(CommandContext ctx) => Refusal(ctx) == null;
    public void Execute(CommandContext ctx)
    {
        var w = ctx.World; var t = TriumphSystem.Waiting(w, ActorId)!;
        if (_choice == TriumphSystem.Home) TriumphSystem.ComeHome(w, _caesar, t); else TriumphSystem.Decline(w, t, TriumphSystem.Keep);
    }
}
