namespace Banking.Domain.Primitives;

/// <summary>
/// Outcome of a domain operation. Business rule violations come back as failed
/// results; exceptions are kept for actual bugs and infrastructure problems.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && error.Length != 0)
        {
            throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
        }

        if (!isSuccess && error.Length == 0)
        {
            throw new ArgumentException("A failed result must carry an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string Error { get; }

    public static Result Success() => new(true, string.Empty);

    public static Result Failure(string error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, string.Empty);

    public static Result<TValue> Failure<TValue>(string error) => new(default, false, error);
}

/// <summary>
/// A <see cref="Result"/> that also carries a value on success.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, string error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>Throws on a failed result; check IsSuccess before reading this.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result: {Error}");
}
