namespace WeakAppHandler.Contracts;

// Payload is kept as opaque JSON text; flattening it into metric rows is the Processor's job.
public sealed record MeterReadingEnvelope(
    string Location,
    string MeterType,
    string Payload,
    string PayloadHash);
