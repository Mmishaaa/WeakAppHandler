import { setAuthSession } from './accessTokenStore'
import { login as requestLogin, refreshAuthSession, type AuthTokenResult } from './authApi'

/** Refresh this long before the access token's real expiry, so a request that straddles the
 * boundary never carries a token that has already expired by the time it reaches a service. */
const REFRESH_SKEW_MS = 30_000

let refreshTimer: ReturnType<typeof setTimeout> | undefined

function clearScheduledRefresh(): void {
  if (refreshTimer !== undefined) {
    clearTimeout(refreshTimer)
    refreshTimer = undefined
  }
}

function scheduleRefresh(expiresInSeconds: number): void {
  clearScheduledRefresh()
  const delay = Math.max(expiresInSeconds * 1000 - REFRESH_SKEW_MS, 0)
  refreshTimer = setTimeout(() => {
    void silentlyRefresh()
  }, delay)
}

function applyResult(result: AuthTokenResult): void {
  setAuthSession({
    accessToken: result.accessToken,
    role: result.role,
    email: result.email,
    expiresAt: Date.now() + result.expiresInSeconds * 1000,
  })
  scheduleRefresh(result.expiresInSeconds)
}

/** PRD §F9's "expired access token is refreshed transparently without user-visible interruption" -
 * on failure (the refresh cookie itself expired, was revoked, or the Auth Service is unreachable)
 * the session just ends; the next protected action naturally redirects to /login. */
async function silentlyRefresh(): Promise<boolean> {
  try {
    applyResult(await refreshAuthSession())
    return true
  } catch {
    clearScheduledRefresh()
    setAuthSession(null)
    return false
  }
}

export async function login(email: string, password: string): Promise<void> {
  applyResult(await requestLogin(email, password))
}

/**
 * Attempts to resume a session from the httpOnly refresh cookie alone. Called once at app
 * startup: a full page reload always wipes the in-memory access token (by design - it must never
 * live in localStorage/sessionStorage), but the 7-day refresh cookie survives the reload, so this
 * is what keeps a reload from forcing a re-login every time.
 */
export function resumeAuthSession(): Promise<boolean> {
  return silentlyRefresh()
}

/**
 * Clears the in-memory session only. The Auth Service has no revoke/logout endpoint, so the
 * httpOnly refresh cookie itself is untouched - a returning visit within its 7-day lifetime will
 * transparently resume the session again via `resumeAuthSession`. Revoking server-side would be a
 * new Auth Service endpoint, out of this task's scope (TASK-041 is frontend-only).
 */
export function logout(): void {
  clearScheduledRefresh()
  setAuthSession(null)
}
