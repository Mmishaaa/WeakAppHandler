import { useMemo } from 'react'
import { AlertRow, type AlertRowData } from './AlertRow'
import { AlertsFilterBar } from './AlertsFilterBar'
import { filterAlerts, type AlertFilters } from './alertFilters'
import './alerts-content.css'

export interface AlertsContentData {
  /** Newest first - the server already orders by triggeredAt DESC. */
  alerts: readonly AlertRowData[]
}

export interface AlertsContentProps {
  data: AlertsContentData
  now: Date
  filters: AlertFilters
  onFiltersChange: (filters: AlertFilters) => void
  highlightedIds: ReadonlySet<string>
}

/** Pure presentation of an already-resolved Alerts snapshot - no queries, no realtime wiring. */
export function AlertsContent({ data, now, filters, onFiltersChange, highlightedIds }: AlertsContentProps) {
  const filtered = useMemo(() => filterAlerts(data.alerts, filters), [data.alerts, filters])

  return (
    <>
      <AlertsFilterBar
        status={filters.status}
        severity={filters.severity}
        onStatusChange={(status) => onFiltersChange({ ...filters, status })}
        onSeverityChange={(severity) => onFiltersChange({ ...filters, severity })}
      />
      {filtered.length === 0 ? (
        <p className="alerts-content__empty">No alerts match the current filters.</p>
      ) : (
        <ul className="alerts-content__list">
          {filtered.map((alert) => (
            <AlertRow key={alert.id} alert={alert} now={now} isNew={highlightedIds.has(alert.id)} />
          ))}
        </ul>
      )}
    </>
  )
}
