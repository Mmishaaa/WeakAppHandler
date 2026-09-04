import { NavLink } from 'react-router-dom'
import { logout } from './authSessionManager'
import './auth-status.css'
import { useAuthSession } from './useAuthSession'

/**
 * Header widget (TASK-041): a "Log in" link when signed out, or the signed-in email/role plus a
 * log-out control. See `authSessionManager.logout`'s own doc comment for why logging out only
 * clears the in-memory session rather than revoking anything server-side.
 */
export function AuthStatus() {
  const session = useAuthSession()

  if (!session) {
    return (
      <NavLink to="/login" className="auth-status__login-link">
        Log in
      </NavLink>
    )
  }

  return (
    <div className="auth-status">
      <span className="auth-status__identity">
        {session.email} · {session.role}
      </span>
      <button type="button" className="auth-status__logout" onClick={logout}>
        Log out
      </button>
    </div>
  )
}
