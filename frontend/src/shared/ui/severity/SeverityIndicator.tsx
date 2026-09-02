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
 * Always pairs color with a text label so severity is never conveyed by color alone. The other
 * intentional consumer of `--color-severity-*` is the meter tile background in
 * features/overview/MeterTile.tsx (via a `data-severity` attribute) - acceptance criteria for
 * TASK-036 require the tile itself, not just an indicator dot, to change color.
 */
export function SeverityIndicator({ severity, label }: SeverityIndicatorProps) {
  return (
    <span className={`severity-indicator severity-indicator--${severity}`}>
      <span aria-hidden="true" className="severity-indicator__dot" />
      {label ?? SEVERITY_LABEL[severity]}
    </span>
  )
}
