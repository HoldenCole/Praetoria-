namespace Praetoria.Core.Events;

/// <summary>Comparison operators usable in data-authored numeric conditions.</summary>
public enum CompareOp { Eq, Neq, Lt, Lte, Gt, Gte }

public static class Comparison
{
    public static CompareOp Parse(string? s) => (s ?? "eq").ToLowerInvariant() switch
    {
        "eq" or "==" => CompareOp.Eq,
        "neq" or "!=" => CompareOp.Neq,
        "lt" or "<" => CompareOp.Lt,
        "lte" or "<=" => CompareOp.Lte,
        "gt" or ">" => CompareOp.Gt,
        "gte" or ">=" => CompareOp.Gte,
        _ => throw new FormatException($"Unknown comparison operator '{s}'.")
    };

    public static bool Apply(CompareOp op, int actual, int expected) => op switch
    {
        CompareOp.Eq => actual == expected,
        CompareOp.Neq => actual != expected,
        CompareOp.Lt => actual < expected,
        CompareOp.Lte => actual <= expected,
        CompareOp.Gt => actual > expected,
        CompareOp.Gte => actual >= expected,
        _ => false
    };
}
