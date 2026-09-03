import { formatMetricValue } from '../../entities/meter/metricDisplay'
import { toNumber } from '../../shared/ui/charts/chartLayout'
import './energy-summary.css'

export interface EnergySummaryBucket {
  sum?: number | string | null
  count: number
}

export interface EnergySummaryProps {
  metricCode: string
  buckets: readonly EnergySummaryBucket[]
}

/**
 * Sum and average for the selected period, shown alongside the line chart for energy (the one
 * metric TASK-038 requires both figures for). The average is total-sum / total-count rather than
 * an average of per-bucket averages, so it stays correct however many readings landed in each
 * bucket.
 */
export function EnergySummary({ metricCode, buckets }: EnergySummaryProps) {
  const totalSum = buckets.reduce((sum, bucket) => sum + (toNumber(bucket.sum) ?? 0), 0)
  const totalCount = buckets.reduce((sum, bucket) => sum + bucket.count, 0)
  const average = totalCount > 0 ? totalSum / totalCount : null

  return (
    <dl className="energy-summary">
      <div className="energy-summary__stat">
        <dt>Total</dt>
        <dd>{formatMetricValue(metricCode, totalSum, null)}</dd>
      </div>
      <div className="energy-summary__stat">
        <dt>Average</dt>
        <dd>{average === null ? '—' : formatMetricValue(metricCode, average, null)}</dd>
      </div>
    </dl>
  )
}
