using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;

namespace Praetoria.Core;

/// <summary>
/// A thin orchestration layer binding World + content + engine + RNG for a playthrough
/// (used by the console harness and the tests). It is NOT where rules live — the engine and
/// effects own those. It just sequences the turn and keeps the World's serialised RNG position
/// in sync after every interaction, so a save taken at any point reproduces exactly (BuildSpec §1.5).
/// The full Briefing → Action → Resolve turn structure (GDD §9) arrives in Milestone 2; M1 fires
/// one event per turn to prove the engine.
/// </summary>
public sealed class GameSession
{
    public World World { get; }
    public ContentDatabase Content { get; }
    public EventEngine Engine { get; }
    private readonly IRng _rng;

    public GameSession(World world, ContentDatabase content, Director? director = null)
    {
        World = world;
        Content = content;
        _rng = WorldBuilder.RngFor(world);
        Engine = new EventEngine(content, _rng, director);
    }

    /// <summary>Snapshot the RNG position into the World (call before serialising a save).</summary>
    public void SyncRng() => World.RngState = _rng.State;

    /// <summary>Move to the next turn. (Ageing/economy/succession are later milestones.)</summary>
    public void AdvanceTurn()
    {
        World.Turn++;
        SyncRng();
    }

    /// <summary>Director-selected event for this turn, or null if nothing is eligible.</summary>
    public FiredEvent? NextEvent()
    {
        var e = Engine.NextEvent(World);
        SyncRng();
        return e;
    }

    public List<OfferedChoice> Offer(FiredEvent fired) => Engine.OfferChoices(fired, World);

    /// <summary>Apply the player's (or an NPC's) choice and persist RNG position.</summary>
    public void Resolve(FiredEvent fired, string choiceId)
    {
        Engine.Resolve(fired, World, choiceId);
        SyncRng();
    }
}
