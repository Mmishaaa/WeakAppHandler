import { getHistoryMetricInfo } from '../../entities/meter/historyMetrics'
import { MotionBarChart } from '../../shared/ui/charts/MotionBarChart'
import { RangeLineChart } from '../../shared/ui/charts/RangeLineChart'
import { EnergySummary } from './EnergySummary'
import { historyBucketLabel } from './historyRange'
import type { HistoryPeriod } from './historyRange'

export interface HistoryContentBucket {
  bucketStart: string
  avg?: number | string | null
  min?: number | string | null
  max?: number | string | null
  sum?: number | string | null
  count: number
}

export interface HistoryContentData {
  aggregations: readonly HistoryContentBucket[]
}

export interface HistoryContentProps {
  data: HistoryContentData
  metricCode: string
  period: HistoryPeriod
}

/**
 * Pure presentation of an already-resolved aggregations snapshot - no queries here. Which chart
 * (and whether a sum+avg summary sits above it) is entirely a function of the selected metric's
 * aggregationKind (TASK-038: sum+avg for energy, avg+min/max band for co2/pm25/humidity, event
 * count as a bar chart for motion).
 */
export function HistoryContent({ data, metricCode, period }: HistoryContentProps) {
  const { aggregationKind } = getHistoryMetricInfo(metricCode)
  const formatBucketLabel = historyBucketLabel(period)

  if (aggregationKind === 'count') {
    const motionBuckets = data.aggregations.map((bucket) => ({ bucketStart: bucket.bucketStart, count: bucket.count }))
    return <MotionBarChart buckets={motionBuckets} formatBucketLabel={formatBucketLabel} />
  }

  const rangeBuckets = data.aggregations.map((bucket) => ({
    bucketStart: bucket.bucketStart,
    avg: bucket.avg ?? null,
    min: bucket.min ?? null,
    max: bucket.max ?? null,
  }))

  return (
    <>
      {aggregationKind === 'sum-avg' && <EnergySummary metricCode={metricCode} buckets={data.aggregations} />}
      <RangeLineChart metricCode={metricCode} buckets={rangeBuckets} formatBucketLabel={formatBucketLabel} />
    </>
  )
}
