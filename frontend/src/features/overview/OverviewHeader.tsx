import { formatRelativeTime } from '../../shared/time/relativeTime'
import './overview-header.css'

export interface OverviewHeaderProps {
  meterCount: number
  /** ISO timestamp of the most recent successful poll across all meters, or null if none yet. */
  lastPolledAt: string | null
  activeAlertCount: number
  now: Date
}

export function OverviewHeader({ meterCount, lastPolledAt, activeAlertCount, now }: OverviewHeaderProps) {
  return (
    <dl className="overview-header">
      <div className="overview-header__stat">
        <dt>Meters reporting</dt>
        <dd>{meterCount}</dd>
      </div>
      <div className="overview-header__stat">
        <dt>Last successful poll</dt>
        <dd>{lastPolledAt ? formatRelativeTime(lastPolledAt, now) : 'Never'}</dd>
      </div>
      <div className="overview-header__stat">
        <dt>Active alerts</dt>
        <dd>{activeAlertCount}</dd>
      </div>
    </dl>
  )
}
