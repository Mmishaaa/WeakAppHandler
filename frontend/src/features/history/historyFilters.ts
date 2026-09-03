import { HISTORY_METRICS } from '../../entities/meter/historyMetrics'
import type { HistoryPeriod } from './historyRange'

export interface HistoryFilters {
  metricCode: string
  /** Empty string means "not yet chosen" - resolved to the first available location once the
   * location list for the current metric's meterType has loaded (see useHistoryLocations). */
  location: string
  period: HistoryPeriod
}

export const DEFAULT_HISTORY_FILTERS: HistoryFilters = {
  metricCode: HISTORY_METRICS[0].metricCode,
  location: '',
  period: 'DAY',
}
