using System.Diagnostics.CodeAnalysis;

namespace ServiceTemplate.Api.Domain;

/// <summary>Outcome of an operation that has no value on success.</summary>
public class Result
{
    private protected Result(DomainError? error) => Error = error;

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(DomainError error) => new(error);

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(DomainError error) => new(default, error);
}

/// <summary>Outcome of an operation that produces a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, DomainError? error)
        : base(error) => _value = value;

    /// <summary>The value; throws when the result is a failure, which always indicates a programming error.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error.Code}).");
}
