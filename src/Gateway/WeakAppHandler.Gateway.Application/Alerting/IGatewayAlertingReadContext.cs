namespace WeakAppHandler.Gateway.Application.Alerting;

/// <summary>
/// The Gateway's read-only seam onto the alerting schema Notification owns and migrates (PRD §4.3's
/// read-only path, applied to a second service's tables the same way
/// <see cref="Readings.IGatewayReadContext"/> applies it to Processor's). A separate interface, not
/// extra members on <see cref="Readings.IGatewayReadContext"/>, so a schema boundary in the database
/// stays a type boundary in code.
/// </summary>
public interface IGatewayAlertingReadContext
{
    public IQueryable<AlertReadModel> Alerts { get; }

    public IQueryable<AlertRuleReadModel> AlertRules { get; }
}
