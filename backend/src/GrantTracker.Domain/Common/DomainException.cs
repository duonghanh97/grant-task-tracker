namespace GrantTracker.Domain.Common;

public enum DomainErrorKind
{
    /// <summary>The request data breaks a rule (HTTP 400).</summary>
    Validation,

    /// <summary>The request conflicts with the current state (HTTP 409).</summary>
    Conflict,
}

/// <summary>A business-rule violation raised by the domain model.</summary>
public sealed class DomainException : Exception
{
    private DomainException(
        string code,
        DomainErrorKind kind,
        string message,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        : base(message)
    {
        Code = code;
        Kind = kind;
        FieldErrors = fieldErrors;
    }

    public string Code { get; }

    public DomainErrorKind Kind { get; }

    /// <summary>Messages per field name (e.g. <c>Title</c>); empty for conflicts.</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    public static DomainException Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(ErrorCodes.ValidationFailed, DomainErrorKind.Validation,
            "One or more validation errors occurred.", fieldErrors);

    public static DomainException Conflict(string code, string message) =>
        new(code, DomainErrorKind.Conflict, message, new Dictionary<string, string[]>());
}
