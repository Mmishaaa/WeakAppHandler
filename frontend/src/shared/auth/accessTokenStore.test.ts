import { afterEach, describe, expect, it, vi } from 'vitest'
import { getAccessToken, getAuthSession, onAuthSessionChange, setAuthSession, type AuthSession } from './accessTokenStore'

function session(overrides: Partial<AuthSession> = {}): AuthSession {
  return {
    accessToken: 'token-a',
    role: 'Viewer',
    email: 'viewer@example.com',
    expiresAt: Date.now() + 900_000,
    ...overrides,
  }
}

describe('accessTokenStore', () => {
  afterEach(() => {
    setAuthSession(null)
  })

  it('starts with no session', () => {
    expect(getAuthSession()).toBeNull()
    expect(getAccessToken()).toBeNull()
  })

  it('exposes the access token of whichever session is current', () => {
    setAuthSession(session({ accessToken: 'token-b' }))
    expect(getAccessToken()).toBe('token-b')

    setAuthSession(null)
    expect(getAccessToken()).toBeNull()
  })

  it('notifies subscribers on every change, and stops once unsubscribed', () => {
    const listener = vi.fn()
    const unsubscribe = onAuthSessionChange(listener)

    const first = session()
    setAuthSession(first)
    expect(listener).toHaveBeenCalledWith(first)

    setAuthSession(null)
    expect(listener).toHaveBeenCalledWith(null)
    expect(listener).toHaveBeenCalledTimes(2)

    unsubscribe()
    setAuthSession(session())
    expect(listener).toHaveBeenCalledTimes(2)
  })
})
