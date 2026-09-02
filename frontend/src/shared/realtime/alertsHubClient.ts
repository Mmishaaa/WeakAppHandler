import * as signalR from '@microsoft/signalr'
import { getAccessToken } from '../auth/accessTokenStore'
import { runtimeConfig } from '../config/runtimeConfig'
import type { ConnectionStatus } from './connectionStatus'

export interface AlertMetricValue {
  numeric: number | null
  boolean: boolean | null
}

interface AlertEventBase {
  alertId: string
  ruleId: string
  meterId: string
  location: string
  meterType: string
  metricCode: string
  severity: string
}

export interface AlertRaisedMessage extends AlertEventBase {
  triggeredValue: AlertMetricValue
  triggeredAt: string
}

export interface AlertResolvedMessage extends AlertEventBase {
  resolvedValue: AlertMetricValue
  resolvedAt: string
}

/** Method names `SignalRAlertDispatcher` (Notification.Api) broadcasts to every connected client. */
export const AlertHubMethod = {
  Raised: 'AlertRaised',
  Resolved: 'AlertResolved',
} as const

type StatusListener = (status: ConnectionStatus) => void
type ReconnectedListener = () => void

/**
 * One connection for the whole app lifetime (module singleton, not per-component) - `AlertsHub`
 * pushes to every connected client regardless of which screen is open, so there is exactly one
 * subscription to manage, not one per consumer.
 */
export const alertsHubConnection = new signalR.HubConnectionBuilder()
  .withUrl(runtimeConfig.alertsHubUrl, {
    // Only attached once TASK-041 populates the store; every service falls back to its own
    // dev-bypass authentication until then (see accessTokenStore.ts).
    accessTokenFactory: () => getAccessToken() ?? '',
  })

  // Default policy (0s, 2s, 10s, 30s, then give up) - PRD §6.7 asks for reconnect-then-refetch on
  // the client, not a bespoke backoff schedule.
  .withAutomaticReconnect()
  .build()

const statusListeners = new Set<StatusListener>()
const reconnectedListeners = new Set<ReconnectedListener>()

function notifyStatus(status: ConnectionStatus): void {
  for (const listener of statusListeners) {
    listener(status)
  }
}

alertsHubConnection.onreconnecting(() => notifyStatus('reconnecting'))
alertsHubConnection.onclose(() => notifyStatus('disconnected'))
alertsHubConnection.onreconnected(() => {
  notifyStatus('connected')
  for (const listener of reconnectedListeners) {
    listener()
  }
})

let startPromise: Promise<void> | undefined

/**
 * Idempotent: safe to call from every mounted consumer's effect (including React StrictMode's
 * deliberate double-invoke) - a connection already starting or started is left alone.
 */
export function ensureAlertsHubStarted(): Promise<void> {
  if (alertsHubConnection.state !== signalR.HubConnectionState.Disconnected) {
    return startPromise ?? Promise.resolve()
  }

  notifyStatus('connecting')
  startPromise = alertsHubConnection
    .start()
    .then(() => notifyStatus('connected'))
    .catch((error: unknown) => {
      notifyStatus('disconnected')
      throw error
    })
  return startPromise
}

/** Fires on every status transition, including the initial `connecting`/`connected` pair. */
export function onAlertsHubStatusChange(listener: StatusListener): () => void {
  statusListeners.add(listener)
  return () => {
    statusListeners.delete(listener)
  }
}

/**
 * Fires only after a *reconnect* (never the first connect) - the seam consumers of the alerts
 * list use to refetch and pick up whatever was missed while disconnected (PRD §6.7 / TASK-035's
 * acceptance criterion).
 */
export function onAlertsHubReconnected(listener: ReconnectedListener): () => void {
  reconnectedListeners.add(listener)
  return () => {
    reconnectedListeners.delete(listener)
  }
}
