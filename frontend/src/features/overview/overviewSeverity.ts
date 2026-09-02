import type { AlertSeverity } from '../../gql/graphql'
import type { Severity } from '../../shared/ui/severity/Severity'

export interface MeterTileSeverity {
  severity: Severity
  label: string
}

export interface ActiveAlertLite {
  meterId: string
  severity: AlertSeverity
}

const SEVERITY_RANK: Record<AlertSeverity, number> = {
  INFO: 1,
  WARNING: 2,
  CRITICAL: 3,
}

/**
 * The tile severity scale only has three tiers (normal/warning/critical - shared/ui/severity has
 * no "info" color), but the backend's AlertSeverity has four. An Info alert still needs a visible
 * signal that something is active, so it shares the "warning" tile color with a Warning alert but
 * keeps its own text label - the label, not the color, is what distinguishes them (severity is
 * never conveyed by color alone, matching SeverityIndicator's own rule).
 */
function toTileSeverity(alertSeverity: AlertSeverity): MeterTileSeverity {
  switch (alertSeverity) {
    case 'CRITICAL':
      return { severity: 'critical', label: 'Critical' }
    case 'INFO':
      return { severity: 'warning', label: 'Info' }
    case 'WARNING':
    default:
      return { severity: 'warning', label: 'Warning' }
  }
}

/** Picks the worst active alert per meter and maps it to a tile severity/label pair. */
export function buildMeterSeverityMap(alerts: readonly ActiveAlertLite[]): Map<string, MeterTileSeverity> {
  const worstByMeter = new Map<string, AlertSeverity>()

  for (const alert of alerts) {
    const current = worstByMeter.get(alert.meterId)
    if (!current || SEVERITY_RANK[alert.severity] > SEVERITY_RANK[current]) {
      worstByMeter.set(alert.meterId, alert.severity)
    }
  }

  return new Map([...worstByMeter].map(([meterId, severity]) => [meterId, toTileSeverity(severity)]))
}
