using GraphQlGateway.BLL.Models;
using HotChocolate.Data.Filters;

namespace GraphQlGateway.API.Schema.Filters;

public sealed class MeterFilterType : FilterInputType<MeterModel>
{
    protected override void Configure(IFilterInputTypeDescriptor<MeterModel> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Field(meter => meter.Location);
        descriptor.Field(meter => meter.MeterType);
        descriptor.Field(meter => meter.LastSeenAt);
    }
}
