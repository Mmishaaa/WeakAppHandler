using System.Text.Json;
using AwesomeAssertions;
using DataIngestorService.Contracts;
using DataIngestorService.Parsing;
using Xunit;

namespace DataIngestorService.Tests;

public class MeterPayloadParserTests
{
    [Fact]
    public void ParseAll_MapsEveryKnownField_ToItsMetricCode()
    {
        var meters = Parse("""
            [{ "type": "air_quality", "name": "Kitchen",
               "payload": { "co2": 812, "pm25": 14, "humidity": 55 } }]
            """);

        var readings = MeterPayloadParser.ParseAll(meters);

        readings.Select(reading => reading.MetricCode)
            .Should().Equal("co2", "pm25", "humidity");
        readings.Should().OnlyContain(reading =>
            reading.Location == "Kitchen" && reading.MeterType == "air_quality");
    }

    [Fact]
    public void ParseAll_RenamesMotionDetected_ToSnakeCase()
    {
        var meters = Parse("""
            [{ "type": "motion", "name": "Bedroom", "payload": { "motionDetected": true } }]
            """);

        var reading = MeterPayloadParser.ParseAll(meters).Should().ContainSingle().Subject;

        reading.MetricCode.Should().Be("motion_detected");
        reading.Flag.Should().BeTrue();
        reading.Numeric.Should().BeNull();
    }

    [Fact]
    public void ParseAll_KeepsNumericAndBooleanValues_InSeparateFields()
    {
        var meters = Parse("""
            [{ "type": "air_quality", "name": "Office",
               "payload": { "co2": 640.5, "motionDetected": false } }]
            """);

        var readings = MeterPayloadParser.ParseAll(meters);

        readings.Should().SatisfyRespectively(
            numeric =>
            {
                numeric.Numeric.Should().Be(640.5m);
                numeric.Flag.Should().BeNull();
            },
            flag =>
            {
                flag.Numeric.Should().BeNull();
                flag.Flag.Should().BeFalse();
            });
    }

    [Fact]
    public void ParseAll_SkipsUnknownFields()
    {
        var meters = Parse("""
            [{ "type": "air_quality", "name": "Garage",
               "payload": { "co2": 500, "pressure": 1013, "vendor": "acme" } }]
            """);

        MeterPayloadParser.ParseAll(meters).Should().ContainSingle()
            .Which.MetricCode.Should().Be("co2");
    }

    [Theory]
    [InlineData("""[{ "type": "air_quality", "name": "Hall", "payload": null }]""")]
    [InlineData("""[{ "type": "air_quality", "name": "Hall", "payload": [1, 2] }]""")]
    [InlineData("""[{ "type": "air_quality", "name": "Hall", "payload": "broken" }]""")]
    public void ParseAll_IgnoresPayloadsThatAreNotObjects(string body)
    {
        MeterPayloadParser.ParseAll(Parse(body)).Should().BeEmpty();
    }

    [Fact]
    public void ParseAll_IgnoresKnownFieldsCarryingTheWrongType()
    {
        var meters = Parse("""
            [{ "type": "air_quality", "name": "Hall",
               "payload": { "co2": "high", "humidity": null, "pm25": 12 } }]
            """);

        MeterPayloadParser.ParseAll(meters).Should().ContainSingle()
            .Which.MetricCode.Should().Be("pm25");
    }

    [Fact]
    public void ParseAll_ReturnsEmpty_ForAnEmptyResponse()
    {
        MeterPayloadParser.ParseAll(Parse("[]")).Should().BeEmpty();
    }

    private static List<WeakAppMeterDto> Parse(string body) =>
        JsonSerializer.Deserialize<List<WeakAppMeterDto>>(body)!;
}
