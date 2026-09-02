export interface NavItem {
  to: string
  label: string
  /** Passed through to react-router's `NavLink end` prop for exact-match active state. */
  end?: boolean
}

export const NAV_ITEMS: readonly NavItem[] = [
  { to: '/', label: 'Overview', end: true },
  { to: '/history', label: 'History' },
  { to: '/alerts', label: 'Alerts' },
  { to: '/administration', label: 'Administration' },
]
