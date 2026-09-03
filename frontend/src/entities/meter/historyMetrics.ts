export type HistoryAggregationKind = 'sum-avg' | 'avg-band' | 'count'

export interface HistoryMetricInfo {
  metricCode: string
  meterType: string
  aggregationKind: HistoryAggregationKind
}

/**
 * Maps each metric to its owning meterType and the shape of aggregation the History screen should
 * render for it (TASK-038: sum+avg for energy, avg+min/max band for the air_quality metrics, event
 * count for motion). Same seed-data source as entities/meter/metricDisplay.ts - see that file's
 * comment for why this isn't exposed over GraphQL yet.
 */
export const HISTORY_METRICS: readonly HistoryMetricInfo[] = [
  { metricCode: 'energy', meterType: 'energy', aggregationKind: 'sum-avg' },
  { metricCode: 'co2', meterType: 'air_quality', aggregationKind: 'avg-band' },
  { metricCode: 'pm25', meterType: 'air_quality', aggregationKind: 'avg-band' },
  { metricCode: 'humidity', meterType: 'air_quality', aggregationKind: 'avg-band' },
  { metricCode: 'motion_detected', meterType: 'motion', aggregationKind: 'count' },
]

/** Falls back to the first known metric so callers always get a usable meterType/aggregationKind. */
export function getHistoryMetricInfo(metricCode: string): HistoryMetricInfo {
  return HISTORY_METRICS.find((metric) => metric.metricCode === metricCode) ?? HISTORY_METRICS[0]
}
