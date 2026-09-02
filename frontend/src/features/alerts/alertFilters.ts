import type { AlertSeverity, AlertStatus } from '../../gql/graphql'

export type AlertStatusFilter = 'ALL' | AlertStatus
export type AlertSeverityFilter = 'ALL' | AlertSeverity

export interface AlertFilters {
  status: AlertStatusFilter
  severity: AlertSeverityFilter
}

export const DEFAULT_ALERT_FILTERS: AlertFilters = { status: 'ALL', severity: 'ALL' }

/** Narrows an already-fetched alert list by status/severity - both filters are client-side since
 * the whole (small, at this project's scale) feed is fetched once and filters change often. */
export function filterAlerts<T extends { status: AlertStatus; severity: AlertSeverity }>(
  alerts: readonly T[],
  filters: AlertFilters,
): T[] {
  return alerts.filter(
    (alert) =>
      (filters.status === 'ALL' || alert.status === filters.status) &&
      (filters.severity === 'ALL' || alert.severity === filters.severity),
  )
}
