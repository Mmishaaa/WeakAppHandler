import type { FeedEvent, LiveStatus } from '../api/liveReadings'
import { Panel } from '../components/Panel'
import { EmptyState } from '../components/PanelState'
import { formatTime, formatValue } from '../lib/format'

interface LiveFeedProps {
  events: readonly FeedEvent[]
  status: LiveStatus
  group: string | null
  onClear: () => void
}

export const LiveFeed = ({ events, status, group, onClear }: LiveFeedProps) => (
  <Panel
    title="Live feed"
    sub={
      <button
        type="button"
        className="btn ghost"
        style={{ height: 24, padding: '0 8px', fontSize: 12 }}
        onClick={onClear}
        disabled={events.length === 0}
      >
        Clear
      </button>
    }
  >
    {status === 'disconnected' ? (
      <EmptyState
        title="Not connected"
        hint="The notification service is unreachable, so nothing is streaming in."
      />
    ) : events.length === 0 ? (
      <EmptyState
        title="Waiting for readings"
        hint={`Subscribed to ${group ?? 'the default group'}. New readings appear here as they are stored.`}
      />
    ) : (
      <div className="feed">
        {events.map((event) => (
          <div key={event.key} className={event.alert === null ? 'event' : 'event alert'}>
            <time dateTime={event.reading.observedAt}>
              {formatTime(event.reading.observedAt)}
            </time>
            <span className="what">
              <b>{event.reading.location}</b> · {event.reading.metricCode}
              {event.alert === null
                ? ''
                : ` · ${event.alert.kind === 'Above' ? 'above' : 'below'} ${event.alert.threshold}`}
            </span>
            <span className="val">
              {formatValue(event.reading.numeric, event.reading.flag)}
            </span>
          </div>
        ))}
      </div>
    )}
  </Panel>
)
