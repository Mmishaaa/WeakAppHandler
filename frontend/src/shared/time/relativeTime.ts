const UNITS: ReadonlyArray<readonly [Intl.RelativeTimeFormatUnit, number]> = [
  ['year', 60 * 60 * 24 * 365],
  ['month', 60 * 60 * 24 * 30],
  ['day', 60 * 60 * 24],
  ['hour', 60 * 60],
  ['minute', 60],
  ['second', 1],
]

// Fixed to 'en-US' rather than the runtime's default locale - the rest of the UI's copy (labels,
// headings) is English-only, and leaving this locale-dependent would make the displayed time
// silently follow the host OS/browser locale instead of matching everything else on screen.
const formatter = new Intl.RelativeTimeFormat('en-US', { numeric: 'auto' })

/** Formats an ISO timestamp relative to `now` (e.g. "5 minutes ago"), always past-tense here. */
export function formatRelativeTime(isoDate: string, now: Date): string {
  const thenMs = new Date(isoDate).getTime()
  const diffSeconds = Math.round((thenMs - now.getTime()) / 1000)
  const absSeconds = Math.abs(diffSeconds)

  for (const [unit, secondsInUnit] of UNITS) {
    if (absSeconds >= secondsInUnit || unit === 'second') {
      return formatter.format(Math.round(diffSeconds / secondsInUnit), unit)
    }
  }

  return formatter.format(0, 'second')
}
