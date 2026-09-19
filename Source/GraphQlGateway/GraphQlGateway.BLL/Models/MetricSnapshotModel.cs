namespace GraphQlGateway.BLL.Models;

public sealed record MetricSnapshotModel(
    string MetricCode,
    string Unit,
    string Location,
    string MeterType,
    DateTimeOffset ObservedAt,
    decimal? Numeric,
    bool? Flag,
    int LocationCount,
    MetricState State,
    decimal? Threshold);
