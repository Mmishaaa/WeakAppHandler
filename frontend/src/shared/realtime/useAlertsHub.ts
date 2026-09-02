import { useEffect, useState } from 'react'
import {
  AlertHubMethod,
  alertsHubConnection,
  ensureAlertsHubStarted,
  onAlertsHubReconnected,
  onAlertsHubStatusChange,
  type AlertRaisedMessage,
  type AlertResolvedMessage,
} from './alertsHubClient'
import type { ConnectionStatus } from './connectionStatus'

export interface UseAlertsHubOptions {
  onAlertRaised?: (message: AlertRaisedMessage) => void
  onAlertResolved?: (message: AlertResolvedMessage) => void
  /** Called after a reconnect (not the first connect) - the place to refetch the alerts list. */
  onReconnected?: () => void
}

export interface UseAlertsHubResult {
  status: ConnectionStatus
}

/**
 * The only place in the app allowed to call `alertsHubConnection.on(...)` for
 * `AlertRaised`/`AlertResolved` - callers register through the options above instead of reaching
 * into the connection directly, so an alert event can never end up handled twice by two different
 * components subscribing to the same hub method.
 */
export function useAlertsHub(options: UseAlertsHubOptions = {}): UseAlertsHubResult {
  const { onAlertRaised, onAlertResolved, onReconnected } = options
  const [status, setStatus] = useState<ConnectionStatus>('connecting')

  useEffect(() => onAlertsHubStatusChange(setStatus), [])

  useEffect(() => {
    void ensureAlertsHubStarted()
  }, [])

  useEffect(() => {
    if (!onAlertRaised) {
      return
    }
    alertsHubConnection.on(AlertHubMethod.Raised, onAlertRaised)
    return () => alertsHubConnection.off(AlertHubMethod.Raised, onAlertRaised)
  }, [onAlertRaised])

  useEffect(() => {
    if (!onAlertResolved) {
      return
    }
    alertsHubConnection.on(AlertHubMethod.Resolved, onAlertResolved)
    return () => alertsHubConnection.off(AlertHubMethod.Resolved, onAlertResolved)
  }, [onAlertResolved])

  useEffect(() => {
    if (!onReconnected) {
      return
    }
    return onAlertsHubReconnected(onReconnected)
  }, [onReconnected])

  return { status }
}
