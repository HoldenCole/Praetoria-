namespace Praetoria.Core.Events;

/// <summary>Canonical world-flag names the engine itself manages.</summary>
public static class EventFlags
{
    /// <summary>Set when a non-repeatable event fires, so it won't fire again (BuildSpec §4).</summary>
    public static string Fired(string eventId) => $"evt:{eventId}:fired";
}
