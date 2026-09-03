import type { AggregationBucketSize } from '../../gql/graphql'

export type HistoryPeriod = 'HOUR' | 'DAY' | 'WEEK'

export interface HistoryRange {
  bucket: AggregationBucketSize
  from: string
  to: string
}

/**
 * The Gateway's AggregationBucketSize enum only has MINUTE/HOUR/DAY (no WEEK) - so the "week"
 * period is expressed as DAY-sized buckets over a 7-day window rather than a distinct bucket size.
 */
const PERIOD_CONFIG: Record<HistoryPeriod, { bucket: AggregationBucketSize; spanMs: number }> = {
  HOUR: { bucket: 'MINUTE', spanMs: 60 * 60 * 1000 },
  DAY: { bucket: 'HOUR', spanMs: 24 * 60 * 60 * 1000 },
  WEEK: { bucket: 'DAY', spanMs: 7 * 24 * 60 * 60 * 1000 },
}

export function computeHistoryRange(period: HistoryPeriod, now: Date = new Date()): HistoryRange {
  const config = PERIOD_CONFIG[period]
  const from = new Date(now.getTime() - config.spanMs)
  return { bucket: config.bucket, from: from.toISOString(), to: now.toISOString() }
}

const dayLabelFormatter = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

/**
 * Overrides the charts' default "HH:mm" x-axis label for the week period, whose DAY-sized buckets
 * need a date label instead. Hour/day periods keep the chart's own default (undefined here).
 */
export function historyBucketLabel(period: HistoryPeriod): ((isoDate: string) => string) | undefined {
  return period === 'WEEK' ? (isoDate: string) => dayLabelFormatter.format(new Date(isoDate)) : undefined
}
