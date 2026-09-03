import type { ChangeEvent } from 'react'
import { getMetricDisplayInfo } from '../../entities/meter/metricDisplay'
import { HISTORY_METRICS } from '../../entities/meter/historyMetrics'
import type { HistoryFilters } from './historyFilters'
import type { HistoryPeriod } from './historyRange'
import './history-filter-bar.css'

export interface HistoryFilterBarProps {
  filters: HistoryFilters
  locations: readonly string[]
  onFiltersChange: (filters: HistoryFilters) => void
}

const PERIOD_LABELS: Record<HistoryPeriod, string> = {
  HOUR: 'Last hour',
  DAY: 'Last day',
  WEEK: 'Last week',
}

export function HistoryFilterBar({ filters, locations, onFiltersChange }: HistoryFilterBarProps) {
  return (
    <div className="history-filter-bar">
      <label className="history-filter-bar__field">
        <span>Metric</span>
        <select
          value={filters.metricCode}
          onChange={(event: ChangeEvent<HTMLSelectElement>) =>
            onFiltersChange({ ...filters, metricCode: event.target.value, location: '' })
          }
        >
          {HISTORY_METRICS.map((metric) => (
            <option key={metric.metricCode} value={metric.metricCode}>
              {getMetricDisplayInfo(metric.metricCode).displayName}
            </option>
          ))}
        </select>
      </label>
      <label className="history-filter-bar__field">
        <span>Location</span>
        <select
          value={filters.location}
          onChange={(event: ChangeEvent<HTMLSelectElement>) => onFiltersChange({ ...filters, location: event.target.value })}
        >
          {locations.map((location) => (
            <option key={location} value={location}>
              {location}
            </option>
          ))}
        </select>
      </label>
      <label className="history-filter-bar__field">
        <span>Period</span>
        <select
          value={filters.period}
          onChange={(event: ChangeEvent<HTMLSelectElement>) =>
            onFiltersChange({ ...filters, period: event.target.value as HistoryPeriod })
          }
        >
          {Object.entries(PERIOD_LABELS).map(([period, label]) => (
            <option key={period} value={period}>
              {label}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}
