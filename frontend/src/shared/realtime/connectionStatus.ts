/**
 * `connecting` is the pre-first-success state (initial page load); `reconnecting` is the same
 * transport recovering a connection it already had once. Kept distinct so the UI can show
 * "Connecting…" on first load without implying anything was ever lost.
 */
export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

const SEVERITY_ORDER: readonly ConnectionStatus[] = ['disconnected', 'reconnecting', 'connecting', 'connected']

/**
 * Combines the two independent realtime channels (GraphQL WS + SignalR) into one status for a
 * single UI indicator: the worse of the two always wins, so "everything is fine" requires both
 * channels to actually be connected.
 */
export function combineConnectionStatus(a: ConnectionStatus, b: ConnectionStatus): ConnectionStatus {
  const rankA = SEVERITY_ORDER.indexOf(a)
  const rankB = SEVERITY_ORDER.indexOf(b)
  return rankA <= rankB ? a : b
}
