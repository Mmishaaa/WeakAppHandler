import { describe, expect, it } from 'vitest'
import { computeNiceTicks, pickTickIndices, roundedTopRectPath, toNumber } from './chartLayout'

describe('toNumber', () => {
  it('passes through a finite number unchanged', () => {
    expect(toNumber(42)).toBe(42)
  })

  it('parses a numeric string, matching how GraphQL Decimal is serialized by codegen', () => {
    expect(toNumber('1234.5')).toBe(1234.5)
  })

  it('returns null for null, undefined, and non-numeric strings', () => {
    expect(toNumber(null)).toBeNull()
    expect(toNumber(undefined)).toBeNull()
    expect(toNumber('not-a-number')).toBeNull()
  })
})

describe('pickTickIndices', () => {
  it('returns every index when the sequence already fits within maxTicks', () => {
    expect(pickTickIndices(4, 6)).toEqual([0, 1, 2, 3])
  })

  it('returns an empty array for an empty sequence', () => {
    expect(pickTickIndices(0, 6)).toEqual([])
  })

  it('thins a long sequence down to at most maxTicks indices, keeping the first and last', () => {
    const indices = pickTickIndices(24, 6)
    expect(indices.length).toBeLessThanOrEqual(6)
    expect(indices[0]).toBe(0)
    expect(indices[indices.length - 1]).toBe(23)
    // Strictly increasing - no duplicate/out-of-order ticks from the rounding step.
    for (let i = 1; i < indices.length; i++) {
      expect(indices[i]).toBeGreaterThan(indices[i - 1])
    }
  })
})

describe('computeNiceTicks', () => {
  it('produces ticks that fully cover the requested domain', () => {
    const ticks = computeNiceTicks(410, 1380, 4)
    expect(ticks[0]).toBeLessThanOrEqual(410)
    expect(ticks[ticks.length - 1]).toBeGreaterThanOrEqual(1380)
  })

  it('rounds to clean human-friendly steps rather than the raw extremes', () => {
    const ticks = computeNiceTicks(0, 97, 4)
    // 97 should round out to a step of 25 or similar, never leaving 97 itself as a tick.
    expect(ticks).not.toContain(97)
    expect(new Set(ticks).size).toBe(ticks.length)
  })

  it('handles a zero-width domain (min === max) without producing NaN', () => {
    const ticks = computeNiceTicks(5, 5, 4)
    expect(ticks.every((tick) => Number.isFinite(tick))).toBe(true)
  })

  it('handles an all-zero domain without dividing by zero', () => {
    const ticks = computeNiceTicks(0, 0, 4)
    expect(ticks.every((tick) => Number.isFinite(tick))).toBe(true)
  })
})

describe('roundedTopRectPath', () => {
  it('produces a closed path string starting and ending consistently', () => {
    const path = roundedTopRectPath(10, 20, 24, 80, 4)
    expect(path.startsWith('M')).toBe(true)
    expect(path.endsWith('Z')).toBe(true)
  })

  it('falls back to a square rectangle when height is zero (no room for a radius)', () => {
    const path = roundedTopRectPath(0, 100, 24, 0, 4)
    expect(path).not.toContain('Q')
  })
})
