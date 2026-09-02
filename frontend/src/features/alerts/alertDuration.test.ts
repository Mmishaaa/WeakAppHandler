import { describe, expect, it } from 'vitest'
import { formatAlertDuration } from './alertDuration'

describe('formatAlertDuration', () => {
  it('formats sub-minute durations as seconds', () => {
    expect(formatAlertDuration('2026-09-03T12:00:00.000Z', '2026-09-03T12:00:45.000Z')).toBe('45s')
  })

  it('formats sub-hour durations as minutes and seconds', () => {
    expect(formatAlertDuration('2026-09-03T12:00:00.000Z', '2026-09-03T12:05:32.000Z')).toBe('5m 32s')
  })

  it('formats sub-day durations as hours and minutes', () => {
    expect(formatAlertDuration('2026-09-03T12:00:00.000Z', '2026-09-03T13:05:00.000Z')).toBe('1h 05m')
  })

  it('formats multi-day durations as days and hours', () => {
    expect(formatAlertDuration('2026-09-01T12:00:00.000Z', '2026-09-03T15:00:00.000Z')).toBe('2d 03h')
  })

  it('never returns a negative duration for out-of-order timestamps', () => {
    expect(formatAlertDuration('2026-09-03T12:00:10.000Z', '2026-09-03T12:00:00.000Z')).toBe('0s')
  })
})
