import type { ConnectionStatus } from './connectionStatus'
import './connection-status-badge.css'
import { useRealtimeConnectionStatus } from './useRealtimeConnectionStatus'

const STATUS_LABEL: Record<ConnectionStatus, string> = {
  connecting: 'Connecting…',
  connected: 'Live',
  reconnecting: 'Reconnecting…',
  disconnected: 'Offline',
}

/**
 * Single indicator for both realtime channels (GraphQL WS `onReadingStored` + SignalR alerts
 * hub) - TASK-035's acceptance criterion that the UI visibly cycles disconnected -> reconnecting
 * -> connected without any user action. `role="status"` makes every label change a polite
 * screen-reader announcement on its own, so this needs no separate wiring into `useAnnouncer`.
 */
export function ConnectionStatusBadge() {
  const status = useRealtimeConnectionStatus()

  return (
    <span className={`connection-status-badge connection-status-badge--${status}`} role="status">
      <span aria-hidden="true" className="connection-status-badge__dot" />
      {STATUS_LABEL[status]}
    </span>
  )
}
