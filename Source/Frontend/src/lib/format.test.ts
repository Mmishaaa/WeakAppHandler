import { describe, expect, it } from 'vitest'
import { formatShare, formatValue } from './format'

describe('formatValue', () => {
  // Numbers follow the viewer's locale, so the separator is 42.5 in CI and 42,5 on a
  // Russian desktop; the test pins the value, not the separator.
  it('prefers the numeric reading', () => {
    expect(formatValue(42.5, null)).toBe(
      (42.5).toLocaleString(undefined, { maximumFractionDigits: 2 }),
    )
  })

  it('renders a boolean reading as a flag', () => {
    expect(formatValue(null, true)).toBe('true')
    expect(formatValue(null, false)).toBe('false')
  })

  it('does not mistake a false flag for a missing value', () => {
    expect(formatValue(null, false)).not.toBe('—')
  })

  it('does not mistake a zero reading for a missing value', () => {
    expect(formatValue(0, null)).toBe('0')
  })

  it('falls back when neither value is present', () => {
    expect(formatValue(null, null)).toBe('—')
    expect(formatValue(undefined, undefined)).toBe('—')
  })
})

describe('formatShare', () => {
  it('renders a fraction as a percentage', () => {
    expect(formatShare(0.5)).toContain('50')
    expect(formatShare(1)).toContain('100')
  })

  it('keeps zero distinct from missing', () => {
    expect(formatShare(0)).toContain('0')
    expect(formatShare(null)).toBe('—')
  })
})
