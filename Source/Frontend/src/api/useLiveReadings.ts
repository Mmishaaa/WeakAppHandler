import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import type { FeedEvent, LiveAlert, LiveReading, LiveStatus } from './liveReadings'

const feedLimit = 80

export interface LiveSubscription {
  location: string | null
  metricCode: string | null
}

export interface LiveReadingsState {
  status: LiveStatus
  group: string | null
  events: readonly FeedEvent[]
  revision: number
  clear: () => void
}

export const useLiveReadings = (subscription: LiveSubscription): LiveReadingsState => {
  const [status, setStatus] = useState<LiveStatus>('connecting')
  const [group, setGroup] = useState<string | null>(null)
  const [events, setEvents] = useState<readonly FeedEvent[]>([])
  const [revision, setRevision] = useState(0)
  const connectionRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/readings')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connectionRef.current = connection

    connection.on('ReadingsReceived', (readings: LiveReading[]) => {
      setEvents((current) => merge(current, readings, []))
      setRevision((current) => current + 1)
    })

    connection.on('AlertsRaised', (alerts: LiveAlert[]) => {
      setEvents((current) => merge(current, [], alerts))
    })

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    let cancelled = false

    connection
      .start()
      .then(() => {
        if (!cancelled) {
          setStatus('connected')
        }
      })
      .catch(() => {
        if (!cancelled) {
          setStatus('disconnected')
        }
      })

    return () => {
      cancelled = true
      connectionRef.current = null
      void connection.stop()
    }
  }, [])

  useEffect(() => {
    const connection = connectionRef.current

    if (connection === null || status !== 'connected') {
      return
    }

    let cancelled = false

    connection
      .invoke<string>('Subscribe', subscription.location, subscription.metricCode)
      .then((joined) => {
        if (!cancelled) {
          setGroup(joined)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setGroup(null)
        }
      })

    return () => {
      cancelled = true
    }
  }, [status, subscription.location, subscription.metricCode])

  return {
    status,
    group,
    events,
    revision,
    clear: () => setEvents([]),
  }
}

const merge = (
  current: readonly FeedEvent[],
  readings: readonly LiveReading[],
  alerts: readonly LiveAlert[],
): readonly FeedEvent[] => {
  const next = [...current]

  for (const reading of readings) {
    const key = `r-${reading.id}`

    if (!next.some((event) => event.key === key)) {
      next.unshift({ key, reading, alert: null })
    }
  }

  for (const alert of alerts) {
    const index = next.findIndex((event) => event.key === `r-${alert.reading.id}`)

    if (index >= 0) {
      next[index] = { ...next[index], alert }
    } else {
      next.unshift({ key: `r-${alert.reading.id}`, reading: alert.reading, alert })
    }
  }

  return next.slice(0, feedLimit)
}
