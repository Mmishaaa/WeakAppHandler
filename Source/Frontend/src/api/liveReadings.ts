export interface LiveReading {
  id: number
  meterId: string
  location: string
  meterType: string
  metricCode: string
  observedAt: string
  numeric: number | null
  flag: boolean | null
}

export type AlertKind = 'Below' | 'Above'

export interface LiveAlert {
  reading: LiveReading
  kind: AlertKind
  threshold: number
}

export interface FeedEvent {
  key: string
  reading: LiveReading
  alert: LiveAlert | null
}

export type LiveStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export const feedLimit = 80

export const mergeFeed = (
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
