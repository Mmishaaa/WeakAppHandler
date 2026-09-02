namespace WeakAppHandler.Gateway.Application.Readings;

/// <summary>Time-bucket granularity for <see cref="IGatewayReadContext.GetAggregationsAsync"/> (PRD F4).</summary>
public enum AggregationBucketSize
{
    Minute,
    Hour,
    Day,
}
