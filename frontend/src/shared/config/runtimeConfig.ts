function readEnv(name: string, fallback: string): string {
  const value = import.meta.env[name]
  return typeof value === 'string' && value.length > 0 ? value : fallback
}

/**
 * Defaults match the Gateway/Notification `https` `dotnet run` profiles
 * (`launchSettings.json`) rather than docker-compose - TASK-047 has not wired either service into
 * the compose stack yet, so local frontend development runs against services started directly via
 * `dotnet run`. Override via `frontend/.env.local` (see `.env.example`) once that changes.
 */
export const runtimeConfig = {
  graphqlHttpUrl: readEnv('VITE_GATEWAY_GRAPHQL_HTTP_URL', 'https://localhost:7069/graphql'),
  graphqlWsUrl: readEnv('VITE_GATEWAY_GRAPHQL_WS_URL', 'wss://localhost:7069/graphql'),
  alertsHubUrl: readEnv('VITE_NOTIFICATION_ALERTS_HUB_URL', 'https://localhost:7031/hubs/alerts'),

  /** Same origin as graphqlHttpUrl - the Gateway's REST admin proxy (TASK-026/040), not GraphQL. */
  gatewayRestUrl: readEnv('VITE_GATEWAY_REST_URL', 'https://localhost:7069'),

  /** Notification.Api's own REST origin for alert-rules CRUD (TASK-030/040) - a different service
   * from the Gateway, so it needs its own base URL rather than reusing gatewayRestUrl. */
  notificationApiUrl: readEnv('VITE_NOTIFICATION_API_URL', 'https://localhost:7031'),

  /** Auth Service origin for /login and /refresh (TASK-041) - yet another service, called
   * directly rather than through the Gateway's proxy. */
  authApiUrl: readEnv('VITE_AUTH_API_URL', 'https://localhost:7238'),
}
