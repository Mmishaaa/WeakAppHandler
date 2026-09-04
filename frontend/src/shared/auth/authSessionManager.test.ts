import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getAuthSession, setAuthSession } from './accessTokenStore'
import { AuthApiError, type AuthTokenResult } from './authApi'

vi.mock('./authApi', () => ({
  login: vi.fn(),
  refreshAuthSession: vi.fn(),
  AuthApiError: class AuthApiError extends Error {
    status: number
    constructor(status: number, message: string) {
      super(message)
      this.status = status
    }
  },
}))

function tokenResult(overrides: Partial<AuthTokenResult> = {}): AuthTokenResult {
  return {
    accessToken: 'token-a',
    role: 'Admin',
    email: 'admin@example.com',
    expiresInSeconds: 900,
    ...overrides,
  }
}

describe('authSessionManager', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    setAuthSession(null)
    vi.useRealTimers()
    vi.clearAllMocks()
  })

  it('login() populates the session from the auth API response', async () => {
    const { login: loginApi } = await import('./authApi')
    vi.mocked(loginApi).mockResolvedValue(tokenResult({ email: 'viewer@example.com', role: 'Viewer' }))

    const { login } = await import('./authSessionManager')
    await login('viewer@example.com', 'secret')

    const session = getAuthSession()
    expect(session?.accessToken).toBe('token-a')
    expect(session?.role).toBe('Viewer')
    expect(session?.email).toBe('viewer@example.com')
  })

  it('login() propagates a failed attempt without touching the session', async () => {
    const { login: loginApi } = await import('./authApi')
    vi.mocked(loginApi).mockRejectedValue(new AuthApiError(401, 'nope'))

    const { login } = await import('./authSessionManager')
    await expect(login('viewer@example.com', 'wrong')).rejects.toThrow('nope')
    expect(getAuthSession()).toBeNull()
  })

  it('schedules a silent refresh before the access token expires, and applies its result', async () => {
    const { login: loginApi, refreshAuthSession } = await import('./authApi')
    vi.mocked(loginApi).mockResolvedValue(tokenResult({ expiresInSeconds: 900 }))
    vi.mocked(refreshAuthSession).mockResolvedValue(tokenResult({ accessToken: 'token-b', expiresInSeconds: 900 }))

    const { login } = await import('./authSessionManager')
    await login('admin@example.com', 'secret')

    expect(refreshAuthSession).not.toHaveBeenCalled()

    // Refresh fires 30s before expiry (REFRESH_SKEW_MS) - advancing to that point should trigger it.
    await vi.advanceTimersByTimeAsync(900_000 - 30_000)

    expect(refreshAuthSession).toHaveBeenCalledTimes(1)
    expect(getAuthSession()?.accessToken).toBe('token-b')
  })

  it('clears the session when the scheduled silent refresh itself fails', async () => {
    const { login: loginApi, refreshAuthSession } = await import('./authApi')
    vi.mocked(loginApi).mockResolvedValue(tokenResult({ expiresInSeconds: 900 }))
    vi.mocked(refreshAuthSession).mockRejectedValue(new AuthApiError(401, 'refresh token expired'))

    const { login } = await import('./authSessionManager')
    await login('admin@example.com', 'secret')

    await vi.advanceTimersByTimeAsync(900_000 - 30_000)

    expect(getAuthSession()).toBeNull()
  })

  it('resumeAuthSession() applies a successful cookie-based refresh', async () => {
    const { refreshAuthSession } = await import('./authApi')
    vi.mocked(refreshAuthSession).mockResolvedValue(tokenResult())

    const { resumeAuthSession } = await import('./authSessionManager')
    const resumed = await resumeAuthSession()

    expect(resumed).toBe(true)
    expect(getAuthSession()?.accessToken).toBe('token-a')
  })

  it('resumeAuthSession() leaves no session when there is no valid refresh cookie', async () => {
    const { refreshAuthSession } = await import('./authApi')
    vi.mocked(refreshAuthSession).mockRejectedValue(new AuthApiError(401, 'no cookie'))

    const { resumeAuthSession } = await import('./authSessionManager')
    const resumed = await resumeAuthSession()

    expect(resumed).toBe(false)
    expect(getAuthSession()).toBeNull()
  })

  it('logout() clears the session and cancels any scheduled refresh', async () => {
    const { login: loginApi, refreshAuthSession } = await import('./authApi')
    vi.mocked(loginApi).mockResolvedValue(tokenResult({ expiresInSeconds: 900 }))

    const { login, logout } = await import('./authSessionManager')
    await login('admin@example.com', 'secret')
    expect(getAuthSession()).not.toBeNull()

    logout()
    expect(getAuthSession()).toBeNull()

    await vi.advanceTimersByTimeAsync(900_000)
    expect(refreshAuthSession).not.toHaveBeenCalled()
  })
})
