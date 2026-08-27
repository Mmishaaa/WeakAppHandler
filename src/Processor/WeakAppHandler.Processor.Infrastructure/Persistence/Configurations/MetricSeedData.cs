using WeakAppHandler.Processor.Domain;

namespace WeakAppHandler.Processor.Infrastructure.Persistence.Configurations;

public static class MetricSeedData
{
    public static readonly IReadOnlyList<Metric> All =
    [
        new Metric
        {
            Code = "energy",
            MeterType = "energy",
            Unit = "kWh",
            ValueKind = MetricValueKind.Numeric,
            DisplayName = "Energy",
        },
        new Metric
        {
            Code = "co2",
            MeterType = "air_quality",
            Unit = "ppm",
            ValueKind = MetricValueKind.Numeric,
            DisplayName = "CO2",
        },
        new Metric
        {
            Code = "pm25",
            MeterType = "air_quality",
            Unit = "µg/m³",
            ValueKind = MetricValueKind.Numeric,
            DisplayName = "PM2.5",
        },
        new Metric
        {
            Code = "humidity",
            MeterType = "air_quality",
            Unit = "%",
            ValueKind = MetricValueKind.Numeric,
            DisplayName = "Humidity",
        },
        new Metric
        {
            Code = "motion_detected",
            MeterType = "motion",
            Unit = "—",
            ValueKind = MetricValueKind.Boolean,
            DisplayName = "Motion Detected",
        },
    ];
}
