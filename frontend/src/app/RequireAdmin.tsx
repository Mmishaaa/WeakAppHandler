import type { ReactElement } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuthSession } from '../shared/auth/useAuthSession'

/**
 * Administration is admin-only (PRD §F8's acceptance criterion: "a viewer cannot see or reach the
 * Administration screen"). Redirects home rather than rendering a bespoke access-denied screen -
 * there is no such screen elsewhere in this app, and the nav item itself is already hidden for
 * non-admins (see nav-items.ts's `adminOnly` filtering in AppShell), so reaching this guard at all
 * only happens via a manually-typed URL.
 */
export function RequireAdmin({ children }: { children: ReactElement }): ReactElement {
  const session = useAuthSession()
  return session?.role === 'Admin' ? children : <Navigate to="/" replace />
}
