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
