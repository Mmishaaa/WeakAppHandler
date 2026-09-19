import type { LiveStatus } from '../api/liveReadings'
import type { DashboardFilters } from './filters'
import { timeWindows } from '../lib/windows'
import type { WindowKey } from '../lib/windows'

const statusLabels: Record<LiveStatus, string> = {
  connecting: 'connecting',
  connected: 'live',
  reconnecting: 'reconnecting',
  disconnected: 'offline',
}

const beaconClass: Record<LiveStatus, string> = {
  connecting: 'beacon pending',
  connected: 'beacon',
  reconnecting: 'beacon pending',
  disconnected: 'beacon off',
}

interface AppBarProps {
  filters: DashboardFilters
  locations: readonly string[]
  metricCodes: readonly string[]
  status: LiveStatus
  group: string | null
  onChange: (filters: DashboardFilters) => void
}

export const AppBar = ({
  filters,
  locations,
  metricCodes,
  status,
  group,
  onChange,
}: AppBarProps) => (
  <header className="appbar">
    <div className="wordmark">
      <span className="dot" />
      WeakAppHandler
      <small>readings</small>
    </div>

    <div className="filters">
      <label className="field">
        <span className="lbl">Location</span>
        <select
          value={filters.location ?? ''}
          onChange={(event) =>
            onChange({ ...filters, location: event.target.value || null })
          }
        >
          <option value="">All</option>
          {locations.map((location) => (
            <option key={location} value={location}>
              {location}
            </option>
          ))}
        </select>
      </label>

      <label className="field">
        <span className="lbl">Metric</span>
        <select
          value={filters.metricCode ?? ''}
          onChange={(event) =>
            onChange({ ...filters, metricCode: event.target.value || null })
          }
        >
          {metricCodes.length === 0 ? <option value="">—</option> : null}
          {metricCodes.map((metricCode) => (
            <option key={metricCode} value={metricCode}>
              {metricCode}
            </option>
          ))}
        </select>
      </label>

      <label className="field">
        <span className="lbl">Window</span>
        <select
          value={filters.windowKey}
          onChange={(event) =>
            onChange({ ...filters, windowKey: event.target.value as WindowKey })
          }
        >
          {timeWindows.map((window) => (
            <option key={window.key} value={window.key}>
              {window.label}
            </option>
          ))}
        </select>
      </label>
    </div>

    <div className="live" title={group ?? 'not subscribed'}>
      <span className={beaconClass[status]} />
      {statusLabels[status]}
    </div>
  </header>
)
