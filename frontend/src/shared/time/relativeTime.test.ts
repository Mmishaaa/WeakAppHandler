import { describe, expect, it } from 'vitest'
import { formatRelativeTime } from './relativeTime'

describe('formatRelativeTime', () => {
  const now = new Date('2026-09-03T12:00:00.000Z')

  it('formats seconds ago', () => {
    expect(formatRelativeTime('2026-09-03T11:59:50.000Z', now)).toBe('10 seconds ago')
  })

  it('formats minutes ago', () => {
    expect(formatRelativeTime('2026-09-03T11:55:00.000Z', now)).toBe('5 minutes ago')
  })

  it('formats hours ago', () => {
    expect(formatRelativeTime('2026-09-03T09:00:00.000Z', now)).toBe('3 hours ago')
  })

  it('formats a timestamp at exactly now as a few seconds ago', () => {
    expect(formatRelativeTime('2026-09-03T12:00:00.000Z', now)).toBe('now')
  })
})
