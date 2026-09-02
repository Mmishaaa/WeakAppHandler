import { compareAlertSeverity, toDisplaySeverity, type AlertSeverityDisplay } from '../../entities/alert/alertSeverityDisplay'
import type { AlertSeverity } from '../../gql/graphql'

export type MeterTileSeverity = AlertSeverityDisplay

export interface ActiveAlertLite {
  meterId: string
  severity: AlertSeverity
}

/** Picks the worst active alert per meter and maps it to a tile severity/label pair. */
export function buildMeterSeverityMap(alerts: readonly ActiveAlertLite[]): Map<string, MeterTileSeverity> {
  const worstByMeter = new Map<string, AlertSeverity>()

  for (const alert of alerts) {
    const current = worstByMeter.get(alert.meterId)
    if (!current || compareAlertSeverity(alert.severity, current) > 0) {
      worstByMeter.set(alert.meterId, alert.severity)
    }
  }

  return new Map([...worstByMeter].map(([meterId, severity]) => [meterId, toDisplaySeverity(severity)]))
}
