export type AuthRole = 'Viewer' | 'Admin'

export interface AuthSession {
  readonly accessToken: string
  readonly role: AuthRole
  readonly email: string
  /** Epoch ms the access token expires at, per the login/refresh response's `expiresInSeconds`. */
  readonly expiresAt: number
}

type SessionListener = (session: AuthSession | null) => void

/**
 * Module-level holder for the current session, shared by both realtime channels (the GraphQL WS
 * link and the SignalR alerts hub), the REST client, and the UI (nav gating, header status) -
 * `getAccessToken` is deliberately a plain function rather than a React hook so non-component code
 * (the hub connection builder, the graphql-ws client) can read it too. Nothing here talks to the
 * network; see authApi.ts (raw HTTP) and authSessionManager.ts (login/refresh orchestration).
 */
let currentSession: AuthSession | null = null
const listeners = new Set<SessionListener>()

export function getAccessToken(): string | null {
  return currentSession?.accessToken ?? null
}

export function getAuthSession(): AuthSession | null {
  return currentSession
}

export function setAuthSession(session: AuthSession | null): void {
  currentSession = session
  for (const listener of listeners) {
    listener(session)
  }
}

export function onAuthSessionChange(listener: SessionListener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
