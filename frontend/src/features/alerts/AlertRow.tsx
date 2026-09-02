import { toDisplaySeverity } from '../../entities/alert/alertSeverityDisplay'
import { formatMetricValue, getMetricDisplayInfo } from '../../entities/meter/metricDisplay'
import type { AlertSeverity, AlertStatus } from '../../gql/graphql'
import { formatRelativeTime } from '../../shared/time/relativeTime'
import { SeverityIndicator } from '../../shared/ui/severity/SeverityIndicator'
import { formatAlertDuration } from './alertDuration'
import './alert-row.css'

export interface AlertRowData {
  id: string
  location: string
  meterType: string
  metricCode: string
  status: AlertStatus
  severity: AlertSeverity
  triggeredAt: string
  triggeredValueNumeric?: number | string | null
  triggeredValueBool?: boolean | null
  resolvedAt?: string | null
  resolvedValueNumeric?: number | string | null
  resolvedValueBool?: boolean | null
}

export interface AlertRowProps {
  alert: AlertRowData
  now: Date
  /** True for the few seconds after a raised/resolved realtime event surfaced this row. */
  isNew: boolean
}

export function AlertRow({ alert, now, isNew }: AlertRowProps) {
  const display = toDisplaySeverity(alert.severity)
  const metricName = getMetricDisplayInfo(alert.metricCode).displayName
  const triggeredValue = formatMetricValue(alert.metricCode, alert.triggeredValueNumeric, alert.triggeredValueBool)

  return (
    <li className="alert-row" data-severity={display.severity} data-new={isNew || undefined}>
      <SeverityIndicator severity={display.severity} label={display.label} />
      <div className="alert-row__body">
        <p className="alert-row__title">
          {alert.location} · {alert.meterType} · {metricName}
        </p>
        <p className="alert-row__detail">
          Triggered at {triggeredValue} · {formatRelativeTime(alert.triggeredAt, now)}
        </p>
        {alert.status === 'RESOLVED' && alert.resolvedAt && (
          <p className="alert-row__detail alert-row__detail--resolved">
            Resolved · lasted {formatAlertDuration(alert.triggeredAt, alert.resolvedAt)}
          </p>
        )}
      </div>
      <span className="alert-row__status">{alert.status === 'ACTIVE' ? 'Active' : 'Resolved'}</span>
    </li>
  )
}
