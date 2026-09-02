namespace WeakAppHandler.Ingestor.Admin;

/// <summary>
/// The configuration in force after <c>PUT /api/v1/ingestion/config</c>. Echoed back rather than
/// returning an empty 204 so a caller can see what actually took effect without a second request.
/// </summary>
public sealed record IngestionConfigResponse(int PollingIntervalSeconds);
