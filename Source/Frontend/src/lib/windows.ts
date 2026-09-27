import type { TimeBucket } from '../gql/graphql'

export type WindowKey = '1h' | '6h' | '24h' | '7d' | '30d'

export interface TimeWindow {
  key: WindowKey
  label: string
  hours: number
  bucket: TimeBucket
}

// Each window aims for a line with enough points to show a trend: 12 in the last hour,
// 24 in the last 6 and 24 hours, one per day beyond that.
export const timeWindows: readonly TimeWindow[] = [
  { key: '1h', label: 'Last hour', hours: 1, bucket: 'FIVE_MINUTES' },
  { key: '6h', label: 'Last 6 hours', hours: 6, bucket: 'FIFTEEN_MINUTES' },
  { key: '24h', label: 'Last 24 hours', hours: 24, bucket: 'HOUR' },
  { key: '7d', label: 'Last 7 days', hours: 24 * 7, bucket: 'DAY' },
  { key: '30d', label: 'Last 30 days', hours: 24 * 30, bucket: 'DAY' },
]

const fallbackWindow = timeWindows.find((window) => window.key === '24h') ?? timeWindows[0]

export const findWindow = (key: WindowKey): TimeWindow =>
  timeWindows.find((window) => window.key === key) ?? fallbackWindow

export interface WindowRange {
  from: string
  to: string
  bucket: TimeBucket
}

const minuteMs = 60_000
const hourMs = 60 * minuteMs

const bucketMs: Record<TimeBucket, number> = {
  FIVE_MINUTES: 5 * minuteMs,
  FIFTEEN_MINUTES: 15 * minuteMs,
  HOUR: hourMs,
  DAY: 24 * hourMs,
}

export const resolveWindow = (window: TimeWindow, now: number): WindowRange => {
  const step = bucketMs[window.bucket]
  const to = Math.floor(now / step) * step + step

  return {
    from: new Date(to - window.hours * hourMs).toISOString(),
    to: new Date(to).toISOString(),
    bucket: window.bucket,
  }
}
