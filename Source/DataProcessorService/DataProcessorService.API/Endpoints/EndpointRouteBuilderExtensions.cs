using DataProcessorService.API.Contracts;
using DataProcessorService.BLL.Models;
using DataProcessorService.BLL.Services;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Shared.MessageContracts;
using Shared.Results;

namespace DataProcessorService.API.Endpoints;

internal static class EndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapApi()
        {
            var group = endpoints.MapGroup("/api").WithTags("Readings");

            group.MapGet("/meters", GetMetersAsync);
            group.MapGet("/meters/{id:guid}", GetMeterAsync);
            group.MapGet("/meters/{id:guid}/readings", GetReadingsAsync);
            group.MapPost("/readings", SubmitReadingsAsync);

            return endpoints;
        }
    }

    private static async Task<Ok<IReadOnlyList<MeterModel>>> GetMetersAsync(
        IMeterQueryService meterQueryService,
        CancellationToken cancellationToken,
        string? location = null,
        string? meterType = null) =>
        TypedResults.Ok(await meterQueryService.GetMetersAsync(location, meterType, cancellationToken));

    private static async Task<Results<Ok<MeterModel>, ProblemHttpResult>> GetMeterAsync(
        Guid id,
        IMeterQueryService meterQueryService,
        CancellationToken cancellationToken)
    {
        var result = await meterQueryService.GetMeterAsync(id, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : NotFound(result.Error);
    }

    private static async Task<Results<Ok<PagedResultModel<StoredReadingModel>>, ProblemHttpResult>> GetReadingsAsync(
        Guid id,
        IMeterQueryService meterQueryService,
        CancellationToken cancellationToken,
        string? metricCode = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50)
    {
        var result = await meterQueryService.GetReadingsAsync(
            new ReadingQueryModel(id, metricCode, from, to, page, pageSize),
            cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : NotFound(result.Error);
    }

    private static async Task<Results<Accepted<SubmitReadingsResponse>, ValidationProblem>> SubmitReadingsAsync(
        SubmitReadingsRequest request,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var readings = request.Readings!;
        var batchId = Guid.CreateVersion7();
        var capturedAt = request.CapturedAt ?? DateTimeOffset.UtcNow;

        await publishEndpoint.Publish(
            new MeterReadingsCaptured(
                batchId,
                capturedAt,
                [.. readings.Select(ToDto)]),
            cancellationToken);

        return TypedResults.Accepted(
            (string?)null,
            new SubmitReadingsResponse(batchId, capturedAt, readings.Count));
    }

    private static MeterReadingDto ToDto(SubmitReadingRequest reading) =>
        new(reading.Location!, reading.MeterType!, reading.MetricCode!, reading.Numeric, reading.Flag);

    private static Dictionary<string, string[]> Validate(SubmitReadingsRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.Readings is null or { Count: 0 })
        {
            errors[nameof(SubmitReadingsRequest.Readings)] = ["At least one reading is required."];

            return errors;
        }

        for (var index = 0; index < request.Readings.Count; index++)
        {
            var reading = request.Readings[index];
            var messages = new List<string>();

            if (string.IsNullOrWhiteSpace(reading.Location))
            {
                messages.Add("Location is required.");
            }

            if (string.IsNullOrWhiteSpace(reading.MeterType))
            {
                messages.Add("MeterType is required.");
            }

            if (string.IsNullOrWhiteSpace(reading.MetricCode))
            {
                messages.Add("MetricCode is required.");
            }

            if (reading.Numeric is null == reading.Flag is null)
            {
                messages.Add("Exactly one of Numeric or Flag must be set.");
            }

            if (messages.Count > 0)
            {
                errors[$"{nameof(SubmitReadingsRequest.Readings)}[{index}]"] = [.. messages];
            }
        }

        return errors;
    }

    private static ProblemHttpResult NotFound(Error error) =>
        TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: StatusCodes.Status404NotFound);
}
