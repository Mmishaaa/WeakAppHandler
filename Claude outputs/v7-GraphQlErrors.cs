using HotChocolate;

namespace GraphQlGateway.API.Errors;

public static class GraphQlErrors
{
    public const string InvalidCursorCode = "INVALID_CURSOR";

    public const string InvalidPagingArgumentsCode = "INVALID_PAGING_ARGUMENTS";

    public static IError Paging(Exception exception, HotChocolate.Path path)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var (message, code) = exception is FormatException
            ? ("The supplied cursor is not valid.", InvalidCursorCode)
            : ("The supplied paging arguments are not valid.", InvalidPagingArgumentsCode);

        return ErrorBuilder.New()
            .SetMessage(message)
            .SetCode(code)
            .SetPath(path)
            .SetException(exception)
            .Build();
    }

    public static IError Sanitize(IError error, bool includeExceptionDetails)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Exception is null || includeExceptionDetails)
        {
            return error;
        }

        var builder = ErrorBuilder.FromError(error).SetException(null);

        if (error.Code is null)
        {
            builder.SetMessage("An unexpected error occurred.");
        }

        return builder.Build();
    }
}
