using WeakAppHandler.Processor.Application.Ingestion;

namespace WeakAppHandler.Processor.Infrastructure.Tests;

/// <summary>
/// Pure unit tests for the payload-to-metric-code mapping (PRD §6 F3, TASK-019) — no database, no
/// bus, just the JSON shapes observed in <c>docs/weakapp-observed-response.json</c>.
/// </summary>
public sealed class PayloadNormalizerTests
{
    [Fact]
    public void Normalize_AirQualityPayload_ReturnsThreeNumericValuesWithMappedMetricCodes()
    {
        var values = PayloadNormalizer.Normalize("""{"co2":727,"pm25":42,"humidity":47}""");

        Assert.Equal(3, values.Count);
        Assert.Contains(values, v => v.MetricCode == "co2" && v.ValueNumeric == 727m && v.ValueBool is null);
        Assert.Contains(values, v => v.MetricCode == "pm25" && v.ValueNumeric == 42m && v.ValueBool is null);
        Assert.Contains(values, v => v.MetricCode == "humidity" && v.ValueNumeric == 47m && v.ValueBool is null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Normalize_MotionPayload_MapsMotionDetectedFieldToMotionDetectedMetricCode(bool motionDetected)
    {
        var values = PayloadNormalizer.Normalize($$"""{"motionDetected":{{(motionDetected ? "true" : "false")}}}""");

        var value = Assert.Single(values);
        Assert.Equal("motion_detected", value.MetricCode);
        Assert.Equal(motionDetected, value.ValueBool);
        Assert.Null(value.ValueNumeric);
    }

    [Fact]
    public void Normalize_EnergyPayload_ReturnsOneNumericValue()
    {
        var values = PayloadNormalizer.Normalize("""{"energy":220.72}""");

        var value = Assert.Single(values);
        Assert.Equal("energy", value.MetricCode);
        Assert.Equal(220.72m, value.ValueNumeric);
        Assert.Null(value.ValueBool);
    }

    [Fact]
    public void Normalize_FieldWithNoKnownMetricCode_IsSkippedWithoutFailingTheRestOfThePayload()
    {
        var values = PayloadNormalizer.Normalize("""{"co2":512,"unknownField":"whatever"}""");

        var value = Assert.Single(values);
        Assert.Equal("co2", value.MetricCode);
    }

    [Fact]
    public void Normalize_EmptyPayload_ReturnsNoValues()
    {
        var values = PayloadNormalizer.Normalize("{}");

        Assert.Empty(values);
    }
}
