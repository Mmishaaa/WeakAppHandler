using Shared.Results;

namespace DataProcessorService.BLL.Results;

public static class MeterErrors
{
    public static readonly Error NotFound = new(
        "meter.not_found",
        "No meter exists with the requested identifier.");
}
