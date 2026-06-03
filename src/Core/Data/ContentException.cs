namespace Praetoria.Core.Data;

/// <summary>Raised when content data is malformed or references something that doesn't exist.
/// The content validator (BuildSpec §7) turns these into actionable lint output.</summary>
public sealed class ContentException : Exception
{
    public ContentException(string message) : base(message) { }
    public ContentException(string message, Exception inner) : base(message, inner) { }
}
