namespace WeakAppHandler.Messaging.Tests;

// One row of the management UI's exchange-bindings table.
internal sealed record BindingInfo(string Source, string Destination, string DestinationType, string RoutingKey);
