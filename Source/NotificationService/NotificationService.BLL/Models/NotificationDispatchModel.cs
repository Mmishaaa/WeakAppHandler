namespace NotificationService.BLL.Models;

public sealed record NotificationDispatchModel(
    IReadOnlyList<NotificationEnvelopeModel> Envelopes,
    int ReadingCount,
    int AlertCount);
