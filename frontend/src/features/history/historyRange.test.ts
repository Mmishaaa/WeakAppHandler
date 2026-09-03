import { describe, expect, it } from 'vitest'
import { computeHistoryRange, historyBucketLabel } from './historyRange'

describe('computeHistoryRange', () => {
  const now = new Date('2026-09-03T12:00:00Z')

  it('uses MINUTE buckets over the last hour for the HOUR period', () => {
    const range = computeHistoryRange('HOUR', now)

    expect(range.bucket).toBe('MINUTE')
    expect(range.to).toBe(now.toISOString())
    expect(range.from).toBe('2026-09-03T11:00:00.000Z')
  })

  it('uses HOUR buckets over the last day for the DAY period', () => {
    const range = computeHistoryRange('DAY', now)

    expect(range.bucket).toBe('HOUR')
    expect(range.from).toBe('2026-09-02T12:00:00.000Z')
  })

  it('uses DAY buckets over the last seven days for the WEEK period', () => {
    const range = computeHistoryRange('WEEK', now)

    expect(range.bucket).toBe('DAY')
    expect(range.from).toBe('2026-08-27T12:00:00.000Z')
  })
})

describe('historyBucketLabel', () => {
  it('leaves the hour and day periods to the chart default label', () => {
    expect(historyBucketLabel('HOUR')).toBeUndefined()
    expect(historyBucketLabel('DAY')).toBeUndefined()
  })

  it('formats week-period buckets as a month/day date', () => {
    const label = historyBucketLabel('WEEK')

    expect(label).toBeDefined()
    expect(label?.('2026-09-03T12:00:00Z')).toBe('Sep 3')
  })
})
