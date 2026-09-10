using System.Reflection;
using System.Runtime.CompilerServices;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors;

namespace GraphQlGateway.API.Errors;

public sealed class HandlePagingErrorsAttribute : ObjectFieldDescriptorAttribute
{
    public HandlePagingErrorsAttribute([CallerLineNumber] int order = 0) => Order = order;

    protected override void OnConfigure(
        IDescriptorContext context,
        IObjectFieldDescriptor descriptor,
        MemberInfo? member)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        descriptor.Use(next => async middlewareContext =>
        {
            try
            {
                await next(middlewareContext);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                throw new GraphQLException(GraphQlErrors.Paging(exception, middlewareContext.Path));
            }
        });
    }
}
