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
}
