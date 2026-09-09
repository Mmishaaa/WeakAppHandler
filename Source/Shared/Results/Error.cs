namespace Shared.Results;

public readonly record struct Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
