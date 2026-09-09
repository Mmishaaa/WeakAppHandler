namespace Shared.Results;

public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    internal Result(TValue? value, Error error)
    {
        _value = value;
        Error = error;
    }

    public Error Error { get; }

    public bool IsSuccess => Error == Error.None;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read the value of a failed result ({Error.Code}: {Error.Message}).");
}
