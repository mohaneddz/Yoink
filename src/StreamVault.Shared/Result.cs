namespace StreamVault.Shared;

/// <summary>Lightweight result type for operations that can fail with a user-facing message.</summary>
public readonly struct Result
{
    private Result(bool success, string? error)
    {
        IsSuccess = success;
        Error = error;
    }

    public bool IsSuccess { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

/// <summary>Lightweight result type carrying a value on success.</summary>
public readonly struct Result<T>
{
    private Result(bool success, T? value, string? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}
