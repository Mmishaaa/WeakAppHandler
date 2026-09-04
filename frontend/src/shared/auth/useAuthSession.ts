import { useEffect, useState } from 'react'
import { getAuthSession, onAuthSessionChange, type AuthSession } from './accessTokenStore'

/** Re-renders on every login/refresh/logout - the store's own change notification, following the
 * same subscribe-in-an-effect shape as `useGraphQlWsStatus`/`useAlertsHub`. */
export function useAuthSession(): AuthSession | null {
  const [session, setSession] = useState<AuthSession | null>(() => getAuthSession())
  useEffect(() => onAuthSessionChange(setSession), [])
  return session
}
