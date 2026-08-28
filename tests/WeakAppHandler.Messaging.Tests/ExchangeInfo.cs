namespace WeakAppHandler.Messaging.Tests;

// One row of the management UI's Exchanges page.
internal sealed record ExchangeInfo(string Name, string Type, bool Durable, bool AutoDelete);
