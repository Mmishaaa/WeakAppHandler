import { describe, expect, it } from 'vitest'
import { combineConnectionStatus, type ConnectionStatus } from './connectionStatus'

describe('combineConnectionStatus', () => {
  it('returns connected only when both channels are connected', () => {
    expect(combineConnectionStatus('connected', 'connected')).toBe('connected')
  })

  const worseThanConnected: ConnectionStatus[] = ['connecting', 'reconnecting', 'disconnected']
  it.each(worseThanConnected)('prefers %s over connected regardless of argument order', (worse) => {
    expect(combineConnectionStatus('connected', worse)).toBe(worse)
    expect(combineConnectionStatus(worse, 'connected')).toBe(worse)
  })

  it('treats disconnected as worse than reconnecting', () => {
    expect(combineConnectionStatus('disconnected', 'reconnecting')).toBe('disconnected')
    expect(combineConnectionStatus('reconnecting', 'disconnected')).toBe('disconnected')
  })

  it('treats reconnecting as worse than connecting', () => {
    expect(combineConnectionStatus('reconnecting', 'connecting')).toBe('reconnecting')
    expect(combineConnectionStatus('connecting', 'reconnecting')).toBe('reconnecting')
  })

  it('is a no-op when both sides already agree', () => {
    for (const status of ['connecting', 'connected', 'reconnecting', 'disconnected'] as const) {
      expect(combineConnectionStatus(status, status)).toBe(status)
    }
  })
})
