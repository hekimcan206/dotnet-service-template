namespace ServiceTemplate.Api.Domain;

/// <summary>How a domain error should be surfaced to callers; mapped to HTTP status codes at the edge.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
}

/// <summary>
/// An expected, recoverable failure. Business rule violations are returned as values instead of thrown,
/// so exceptions stay reserved for things the caller cannot fix (bugs, infrastructure faults).
/// </summary>
/// <param name="Code">Stable machine-readable code, e.g. <c>order.not_draft</c>. Clients branch on this, never on the message.</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="Kind">Category used to choose the HTTP status code.</param>
public sealed record DomainError(string Code, string Message, ErrorKind Kind)
{
    public static DomainError Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static DomainError NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static DomainError Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);
}
