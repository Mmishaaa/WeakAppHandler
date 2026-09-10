using GraphQlGateway.BLL.Models;
using Shared.Entities;

namespace GraphQlGateway.BLL.Projections;

public static class MeterProjections
{
    extension(IQueryable<DbMeter> meters)
    {
        public IQueryable<MeterModel> ToModels() =>
            meters.Select(meter => new MeterModel
            {
                Id = meter.Id,
                Location = meter.Location,
                MeterType = meter.MeterType,
                FirstSeenAt = meter.FirstSeenAt,
                LastSeenAt = meter.LastSeenAt,
            });
    }
}
