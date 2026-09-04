export interface NavItem {
  to: string
  label: string
  /** Passed through to react-router's `NavLink end` prop for exact-match active state. */
  end?: boolean
  /** Hidden from the nav unless the current session's role is Admin (TASK-041, PRD §F8: "a viewer
   * cannot see or reach the Administration screen"). */
  adminOnly?: boolean
}

export const NAV_ITEMS: readonly NavItem[] = [
  { to: '/', label: 'Overview', end: true },
  { to: '/history', label: 'History' },
  { to: '/alerts', label: 'Alerts' },
  { to: '/administration', label: 'Administration', adminOnly: true },
]
