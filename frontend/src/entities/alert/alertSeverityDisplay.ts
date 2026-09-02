import type { AlertSeverity } from '../../gql/graphql'
import type { Severity } from '../../shared/ui/severity/Severity'

export interface AlertSeverityDisplay {
  severity: Severity
  label: string
}

const SEVERITY_RANK: Record<AlertSeverity, number> = {
  INFO: 1,
  WARNING: 2,
  CRITICAL: 3,
}

/**
 * The tile/row severity scale only has three tiers (normal/warning/critical - shared/ui/severity
 * has no "info" color), but the backend's AlertSeverity has four. An Info alert still needs a
 * visible signal that something is active, so it shares the "warning" color with a Warning alert
 * but keeps its own text label - the label, not the color, is what distinguishes them (severity is
 * never conveyed by color alone).
 */
export function toDisplaySeverity(alertSeverity: AlertSeverity): AlertSeverityDisplay {
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

/** Higher rank means more severe - used to pick the worst of several alerts on the same meter. */
export function compareAlertSeverity(a: AlertSeverity, b: AlertSeverity): number {
  return SEVERITY_RANK[a] - SEVERITY_RANK[b]
}
