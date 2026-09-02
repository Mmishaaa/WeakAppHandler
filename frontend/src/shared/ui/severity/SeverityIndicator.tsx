import type { Severity } from './Severity'
import './severity-indicator.css'

const SEVERITY_LABEL: Record<Severity, string> = {
  normal: 'Normal',
  warning: 'Warning',
  critical: 'Critical',
}

export interface SeverityIndicatorProps {
  severity: Severity
  /** Overrides the default label text; the label is always shown, never color-only. */
  label?: string
}

/**
 * The only place in the UI allowed to use the severity color scale (`--color-severity-*`).
 * Always pairs color with a text label so severity is never conveyed by color alone.
 */
export function SeverityIndicator({ severity, label }: SeverityIndicatorProps) {
  return (
    <span className={`severity-indicator severity-indicator--${severity}`}>
      <span aria-hidden="true" className="severity-indicator__dot" />
      {label ?? SEVERITY_LABEL[severity]}
    </span>
  )
}
