import { describe, expect, it } from 'vitest'
import { formatMetricValue, getMetricDisplayInfo } from './metricDisplay'

describe('getMetricDisplayInfo', () => {
  it('resolves known seed metrics with their display name and unit', () => {
    expect(getMetricDisplayInfo('co2')).toEqual({ displayName: 'CO2', unit: 'ppm', kind: 'numeric' })
    expect(getMetricDisplayInfo('motion_detected')).toEqual({
      displayName: 'Motion Detected',
      unit: null,
      kind: 'boolean',
    })
  })

  it('falls back to the raw metric code for an unknown metric', () => {
    expect(getMetricDisplayInfo('unknown_metric')).toEqual({
      displayName: 'unknown_metric',
      unit: null,
      kind: 'numeric',
    })
  })
})

describe('formatMetricValue', () => {
  it('formats a numeric metric with its unit', () => {
    expect(formatMetricValue('co2', 842, null)).toBe('842 ppm')
  })

  it('rounds numeric values to at most two fraction digits', () => {
    expect(formatMetricValue('pm25', 12.3456, null)).toBe('12.35 µg/m³')
  })

  it('renders a boolean metric as Yes/No', () => {
    expect(formatMetricValue('motion_detected', null, true)).toBe('Yes')
    expect(formatMetricValue('motion_detected', null, false)).toBe('No')
  })

  it('renders a dash when the numeric value is missing', () => {
    expect(formatMetricValue('energy', null, null)).toBe('—')
  })
})
