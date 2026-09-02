import { useEffect, useState } from 'react'
import { onGraphQlWsStatusChange } from './apolloClient'
import { combineConnectionStatus, type ConnectionStatus } from './connectionStatus'
import { useAlertsHub } from './useAlertsHub'

/** Status of the `onReadingStored` GraphQL subscription's WebSocket transport alone. */
export function useGraphQlWsStatus(): ConnectionStatus {
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  useEffect(() => onGraphQlWsStatusChange(setStatus), [])
  return status
}

/**
 * Combines both realtime channels (GraphQL WS + SignalR alerts hub) into one status: the worse
 * of the two always wins, so a single indicator can tell the user everything is live without
 * needing to know there are two independent transports underneath.
 */
export function useRealtimeConnectionStatus(): ConnectionStatus {
  const graphqlStatus = useGraphQlWsStatus()
  const { status: alertsStatus } = useAlertsHub()
  return combineConnectionStatus(graphqlStatus, alertsStatus)
}
