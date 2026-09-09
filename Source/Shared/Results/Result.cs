namespace Shared.Results;

public static class Result
{
    public static Result<TValue> Success<TValue>(TValue value) => new(value, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, error);
}
