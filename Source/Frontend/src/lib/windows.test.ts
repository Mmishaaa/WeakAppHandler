import { describe, expect, it } from 'vitest'
import { findWindow, resolveWindow, timeWindows } from './windows'

const at = (iso: string) => Date.parse(iso)

describe('resolveWindow', () => {
  it('ends on the next bucket boundary so the current bucket stays open', () => {
    const range = resolveWindow(findWindow('24h'), at('2026-09-20T01:24:34.512Z'))

    expect(range.to).toBe('2026-09-20T02:00:00.000Z')
    expect(range.from).toBe('2026-09-19T02:00:00.000Z')
  })

  it('covers exactly as many buckets as the window is wide', () => {
    const cases = [
      { key: '6h', buckets: 6, ms: 3_600_000 },
      { key: '24h', buckets: 24, ms: 3_600_000 },
      { key: '7d', buckets: 7, ms: 86_400_000 },
      { key: '30d', buckets: 30, ms: 86_400_000 },
    ] as const

    for (const entry of cases) {
      const range = resolveWindow(findWindow(entry.key), at('2026-09-20T01:24:34.512Z'))
      const span = Date.parse(range.to) - Date.parse(range.from)

      expect(span / entry.ms).toBe(entry.buckets)
    }
  })

  it('holds the same bounds for the whole bucket, so the query is not re-issued', () => {
    const window = findWindow('24h')
    const first = resolveWindow(window, at('2026-09-20T01:00:00.000Z'))
    const later = resolveWindow(window, at('2026-09-20T01:59:59.999Z'))

    expect(later).toEqual(first)
  })

  it('moves on once the boundary is crossed', () => {
    const window = findWindow('24h')
    const before = resolveWindow(window, at('2026-09-20T01:59:59.999Z'))
    const after = resolveWindow(window, at('2026-09-20T02:00:00.000Z'))

    expect(after.to).not.toBe(before.to)
  })

  it('never ends in the past, even exactly on a boundary', () => {
    const now = at('2026-09-20T02:00:00.000Z')
    const range = resolveWindow(findWindow('24h'), now)

    expect(Date.parse(range.to)).toBeGreaterThan(now)
  })

  it('picks the bucket size from the window', () => {
    expect(resolveWindow(findWindow('6h'), 0).bucket).toBe('HOUR')
    expect(resolveWindow(findWindow('30d'), 0).bucket).toBe('DAY')
  })
})

describe('findWindow', () => {
  it('returns the window with the given key', () => {
    expect(findWindow('7d').label).toBe('Last 7 days')
  })

  it('falls back to 24 hours when the key is unknown', () => {
    expect(findWindow('nonsense' as never)).toBe(timeWindows[1])
  })
})
