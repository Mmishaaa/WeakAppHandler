using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Notification.Api.RealTime;

/// <summary>
/// PRD §6.7's real-time channel for alerts: <see cref="SignalRAlertDispatcher"/> broadcasts alert
/// raised/resolved events to every connected client. Read-only — it defines no client-invokable hub
/// methods, only the transport server-to-client pushes travel over.
/// </summary>
/// <remarks>
/// <see cref="AuthorizeAttribute"/> makes ASP.NET Core's authorization middleware reject the
/// connection handshake itself for a missing or invalid JWT, before <c>OnConnectedAsync</c> ever
/// runs (TASK-031's "anonymous connections are rejected" acceptance criterion). Viewer policy, not
/// Admin: any authenticated user — not just an operator — is meant to see alerts on the dashboard.
///
/// Single-replica only (PRD §6.7): a client connected to a second Notification Service instance
/// would never see an event broadcast by the instance that handled the commit, since no cross-process
/// backplane (e.g. Redis) is configured here. Scaling this service out requires adding one first.
/// </remarks>
[Authorize(Policy = ServicePolicies.Viewer)]
public sealed class AlertsHub : Hub;
