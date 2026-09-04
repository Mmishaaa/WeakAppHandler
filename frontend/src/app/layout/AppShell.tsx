import { NavLink, Outlet } from 'react-router-dom'
import { AuthStatus } from '../../shared/auth/AuthStatus'
import { useAuthSession } from '../../shared/auth/useAuthSession'
import { ConnectionStatusBadge } from '../../shared/realtime/ConnectionStatusBadge'
import { NAV_ITEMS } from './nav-items'
import { RouteAnnouncer } from './RouteAnnouncer'
import './app-shell.css'

export function AppShell() {
  const session = useAuthSession()
  const visibleNavItems = NAV_ITEMS.filter((item) => !item.adminOnly || session?.role === 'Admin')

  return (
    <div className="app-shell">
      <a className="visually-hidden focusable app-shell__skip-link" href="#main-content">
        Skip to main content
      </a>
      <header className="app-shell__header">
        <span className="app-shell__brand">WeakAppHandler</span>
        <nav aria-label="Main" className="app-shell__nav">
          <ul>
            {visibleNavItems.map((item) => (
              <li key={item.to}>
                <NavLink to={item.to} end={item.end} className={({ isActive }) => (isActive ? 'is-active' : undefined)}>
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <ConnectionStatusBadge />
        <AuthStatus />
      </header>
      <main id="main-content" className="app-shell__main" tabIndex={-1}>
        <Outlet />
      </main>
      <RouteAnnouncer />
    </div>
  )
}
