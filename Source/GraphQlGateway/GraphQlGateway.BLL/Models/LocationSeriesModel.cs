namespace GraphQlGateway.BLL.Models;

public sealed record LocationSeriesModel(
    string Location,
    IReadOnlyList<ReadingBucketModel> Buckets);
