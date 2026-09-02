/**
 * Module-level holder for the current access token, shared by both realtime channels (the
 * GraphQL WS link and the SignalR alerts hub) so each attaches credentials the same way once
 * TASK-041 lands a real login flow. Until then this stays `null` and every service falls back to
 * its own dev-bypass authentication (see `Auth:DevBypassEnabled` in each host's configuration) -
 * `getAccessToken` is deliberately a plain function rather than a React hook so non-component
 * code (the hub connection builder, the graphql-ws client) can read it too.
 */
let currentAccessToken: string | null = null

export function getAccessToken(): string | null {
  return currentAccessToken
}

export function setAccessToken(token: string | null): void {
  currentAccessToken = token
}
