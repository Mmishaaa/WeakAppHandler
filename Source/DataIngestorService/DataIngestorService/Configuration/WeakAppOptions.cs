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
    public int AttemptTimeoutSeconds { get; set; } = 3;

    [Range(1, 600)]
    public int TotalTimeoutSeconds { get; set; } = 8;

    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AttemptTimeoutSeconds >= TotalTimeoutSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(AttemptTimeoutSeconds)} must be smaller than {nameof(TotalTimeoutSeconds)}.",
                [nameof(AttemptTimeoutSeconds)]);
        }

        if (TotalTimeoutSeconds >= PollingIntervalSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(TotalTimeoutSeconds)} must be smaller than {nameof(PollingIntervalSeconds)}.",
                [nameof(TotalTimeoutSeconds)]);
        }
    }
}
