using Praetoria.Core.State;

namespace Praetoria.Core.Events;

/// <summary>
/// A resolution of an event's role variables to concrete character ids (BuildSpec §4 "Binder").
/// This is the engine of "by ROLE, not by NAME" (GDD §15 L2): an event says
/// {a_rival_of_higher_rank}; the Binder fills it with whoever fits *this* world. One authored
/// event → many lived contexts. "self" is always pre-bound to the protagonist.
/// </summary>
public sealed class Binding
{
    private readonly Dictionary<string, string> _roleToCharId = new();

    public const string Self = "self";

    public Binding(string protagonistId) => _roleToCharId[Self] = protagonistId;

    private Binding(Dictionary<string, string> map) => _roleToCharId = map;

    public bool TryGet(string role, out string charId) => _roleToCharId.TryGetValue(role, out charId!);

    public string Resolve(string role) =>
        _roleToCharId.TryGetValue(role, out var id)
            ? id
            : throw new InvalidOperationException($"Role '{role}' is not bound.");

    public bool IsBound(string role) => _roleToCharId.ContainsKey(role);

    public IReadOnlyDictionary<string, string> All => _roleToCharId;

    public IEnumerable<string> BoundCharIds => _roleToCharId.Values;

    public Binding With(string role, string charId)
    {
        var copy = new Dictionary<string, string>(_roleToCharId) { [role] = charId };
        return new Binding(copy);
    }
}
