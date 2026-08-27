using Microsoft.Extensions.Options;

namespace WeakAppHandler.Ingestor.WeakApp;

/// <summary>
/// Enforces the invariant behind "timeout budget shorter than the polling interval" (PRD §6.1):
/// an attempt must fit inside the total budget, and the total budget must fit inside one poll cycle.
/// </summary>
public sealed class WeakAppOptionsValidator : IValidateOptions<WeakAppOptions>
{
    public ValidateOptionsResult Validate(string? name, WeakAppOptions options)
    {
        var failures = new List<string>();

        if (options.AttemptTimeoutSeconds <= 0)
        {
            failures.Add($"{nameof(WeakAppOptions.AttemptTimeoutSeconds)} must be greater than zero.");
        }

        if (options.TotalTimeoutSeconds <= options.AttemptTimeoutSeconds)
        {
            failures.Add(
                $"{nameof(WeakAppOptions.TotalTimeoutSeconds)} ({options.TotalTimeoutSeconds}s) must be greater than " +
                $"{nameof(WeakAppOptions.AttemptTimeoutSeconds)} ({options.AttemptTimeoutSeconds}s).");
        }

        if (options.TotalTimeoutSeconds >= options.PollingIntervalSeconds)
        {
            failures.Add(
                $"{nameof(WeakAppOptions.TotalTimeoutSeconds)} ({options.TotalTimeoutSeconds}s) must be less than " +
                $"{nameof(WeakAppOptions.PollingIntervalSeconds)} ({options.PollingIntervalSeconds}s), otherwise retries " +
                "can overlap the next scheduled poll.");
        }

        if (options.MaxRetryAttempts < 0)
        {
            failures.Add($"{nameof(WeakAppOptions.MaxRetryAttempts)} must not be negative.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
