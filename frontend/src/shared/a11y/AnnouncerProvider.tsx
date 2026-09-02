import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { AnnouncerContext, type AnnouncerContextValue } from './announcer-context'

/** Zero-width space (U+200B), built from its code point rather than a literal invisible byte in
 * the source - the latter trips ESLint's `no-irregular-whitespace` (correctly: an invisible
 * character sitting unescaped in a source file is a real footgun on its own), and this produces
 * the byte-for-byte identical string either way. */
const ZERO_WIDTH_SPACE = String.fromCharCode(0x200b)

/**
 * Hosts the single shared `aria-live` region used for non-visual announcements across the app
 * (route changes now; alert raised/resolved events from TASK-039/TASK-035 later). One region per
 * app, not one per feature, so screen readers announce a predictable single stream instead of
 * racing multiple live regions against each other.
 */
export function AnnouncerProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState('')
  const parityRef = useRef(false)

  const announce = useCallback((next: string) => {
    // aria-live only fires on an actual text change - append a zero-width space that flips each
    // call so back-to-back identical announcements ("Refreshing…" twice) still fire.
    parityRef.current = !parityRef.current
    setMessage(parityRef.current ? `${next}${ZERO_WIDTH_SPACE}` : next)
  }, [])

  const value = useMemo<AnnouncerContextValue>(() => ({ announce }), [announce])

  return (
    <AnnouncerContext.Provider value={value}>
      {children}
      <div className="visually-hidden" role="status" aria-live="polite" aria-atomic="true">
        {message}
      </div>
    </AnnouncerContext.Provider>
  )
}
