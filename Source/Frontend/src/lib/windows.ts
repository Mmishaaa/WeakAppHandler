import type { TimeBucket } from '../gql/graphql'

export type WindowKey = '6h' | '24h' | '7d' | '30d'

export interface TimeWindow {
  key: WindowKey
  label: string
  hours: number
  bucket: TimeBucket
}

export const timeWindows: readonly TimeWindow[] = [
  { key: '6h', label: 'Last 6 hours', hours: 6, bucket: 'HOUR' },
  { key: '24h', label: 'Last 24 hours', hours: 24, bucket: 'HOUR' },
  { key: '7d', label: 'Last 7 days', hours: 24 * 7, bucket: 'DAY' },
  { key: '30d', label: 'Last 30 days', hours: 24 * 30, bucket: 'DAY' },
]

export const findWindow = (key: WindowKey): TimeWindow =>
  timeWindows.find((window) => window.key === key) ?? timeWindows[1]

export interface WindowRange {
  from: string
  to: string
  bucket: TimeBucket
}

const hourMs = 3_600_000

const bucketMs: Record<TimeBucket, number> = {
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
