using System.ComponentModel.DataAnnotations;

namespace DataIngestorService.Configuration;

sealed class WeakAppOptions : IValidatableObject
{
    public const string SectionName = "WeakApp";

    [Required]
    public Uri? BaseUrl { get; set; }

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 3600)]
    public int PollingIntervalSeconds { get; set; } = 10;

    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 5;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestTimeoutSeconds >= PollingIntervalSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(RequestTimeoutSeconds)} must be smaller than {nameof(PollingIntervalSeconds)}.",
                [nameof(RequestTimeoutSeconds)]);
        }
    }
}
