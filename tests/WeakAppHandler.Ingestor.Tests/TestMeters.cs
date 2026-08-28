using System.Text.Json;
using WeakAppHandler.Contracts;
using WeakAppHandler.Ingestor.WeakApp;

namespace WeakAppHandler.Ingestor.Tests;

internal static class TestMeters
{
    /// <summary>
    /// The full 18-element response WeakApp really returns (6 locations x 3 sensor types), copied
    /// from the live capture in docs/weakapp-observed-response.json rather than invented, so the
    /// batch size these tests exercise is the batch size production sees.
    /// </summary>
    public static IReadOnlyList<WeakAppMeterDto> ObservedResponse { get; } =
    [
        Meter("motion", "Office", """{"motionDetected":true}"""),
        Meter("energy", "Kitchen", """{"energy":220.72}"""),
        Meter("air_quality", "Corridor", """{"co2":727,"pm25":42,"humidity":47}"""),
        Meter("motion", "Corridor", """{"motionDetected":false}"""),
        Meter("air_quality", "Bedroom", """{"co2":357,"pm25":7,"humidity":51}"""),
        Meter("energy", "Bedroom", """{"energy":139.89}"""),
        Meter("energy", "Office", """{"energy":405}"""),
        Meter("motion", "Garage", """{"motionDetected":false}"""),
        Meter("energy", "Living Room", """{"energy":760.36}"""),
        Meter("air_quality", "Garage", """{"co2":682,"pm25":5,"humidity":74}"""),
        Meter("motion", "Kitchen", """{"motionDetected":true}"""),
        Meter("energy", "Corridor", """{"energy":616.62}"""),
        Meter("energy", "Garage", """{"energy":392.65}"""),
        Meter("motion", "Bedroom", """{"motionDetected":true}"""),
        Meter("air_quality", "Office", """{"co2":712,"pm25":34,"humidity":20}"""),
        Meter("air_quality", "Kitchen", """{"co2":938,"pm25":39,"humidity":70}"""),
        Meter("air_quality", "Living Room", """{"co2":662,"pm25":36,"humidity":21}"""),
        Meter("motion", "Living Room", """{"motionDetected":false}"""),
    ];

    public static WeakAppMeterDto Meter(string type, string name, string payloadJson) =>
        new(type, name, JsonDocument.Parse(payloadJson).RootElement.Clone());

    public static WeakAppFetchResult Success(IReadOnlyList<WeakAppMeterDto> meters, int durationMs = 42) => new()
    {
        Outcome = IngestOutcome.Success,
        HttpStatusCode = 200,
        Meters = meters,
        Duration = TimeSpan.FromMilliseconds(durationMs),
    };

    public static WeakAppFetchResult Failure(IngestOutcome outcome, int? httpStatus, string errorMessage) => new()
    {
        Outcome = outcome,
        HttpStatusCode = httpStatus,
        ErrorMessage = errorMessage,
        Duration = TimeSpan.FromMilliseconds(7),
    };
}
