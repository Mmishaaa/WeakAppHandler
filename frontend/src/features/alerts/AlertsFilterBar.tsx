import type { ChangeEvent } from 'react'
import type { AlertSeverityFilter, AlertStatusFilter } from './alertFilters'
import './alerts-filter-bar.css'

export interface AlertsFilterBarProps {
  status: AlertStatusFilter
  severity: AlertSeverityFilter
  onStatusChange: (status: AlertStatusFilter) => void
  onSeverityChange: (severity: AlertSeverityFilter) => void
}

export function AlertsFilterBar({ status, severity, onStatusChange, onSeverityChange }: AlertsFilterBarProps) {
  return (
    <div className="alerts-filter-bar">
      <label className="alerts-filter-bar__field">
        <span>Status</span>
        <select
          value={status}
          onChange={(event: ChangeEvent<HTMLSelectElement>) => onStatusChange(event.target.value as AlertStatusFilter)}
        >
          <option value="ALL">All</option>
          <option value="ACTIVE">Active</option>
          <option value="RESOLVED">Resolved</option>
        </select>
      </label>
      <label className="alerts-filter-bar__field">
        <span>Severity</span>
        <select
          value={severity}
          onChange={(event: ChangeEvent<HTMLSelectElement>) =>
            onSeverityChange(event.target.value as AlertSeverityFilter)
          }
        >
          <option value="ALL">All</option>
          <option value="INFO">Info</option>
          <option value="WARNING">Warning</option>
          <option value="CRITICAL">Critical</option>
        </select>
      </label>
    </div>
  )
}
