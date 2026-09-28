namespace Domolov.Application.Common;

/// <summary>The requested resource does not exist (404).</summary>
public sealed class NotFoundException(string resource, object id)
    : Exception($"{resource} '{id}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>The request conflicts with the current state (409).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>The If-Match precondition no longer matches the stored version (412).</summary>
public sealed class PreconditionFailedException()
    : Exception("The resource was changed by someone else. Reload and try again.");
