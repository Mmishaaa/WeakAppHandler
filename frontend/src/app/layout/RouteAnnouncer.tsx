import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'
import { useAnnouncer } from '../../shared/a11y/useAnnouncer'
import { NAV_ITEMS } from './nav-items'

/**
 * SPA route changes don't trigger a browser navigation, so screen readers get no automatic
 * "page changed" announcement the way they would for a full page load. This announces the new
 * page's title on every route change, skipping the very first render (that page's own heading is
 * already the natural landing announcement).
 */
export function RouteAnnouncer() {
  const location = useLocation()
  const { announce } = useAnnouncer()
  const isFirstRender = useRef(true)

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false
      return
    }

    const match = NAV_ITEMS.find((item) => (item.end ? location.pathname === item.to : location.pathname.startsWith(item.to)))
    announce(match ? `Navigated to ${match.label}` : 'Navigated')
  }, [location.pathname, announce])

  return null
}
