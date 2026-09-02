import { describe, expect, it } from 'vitest'
import { buildMeterSeverityMap } from './overviewSeverity'

describe('buildMeterSeverityMap', () => {
  it('maps a meter with no active alerts to nothing (caller treats absence as normal)', () => {
    const result = buildMeterSeverityMap([])
    expect(result.size).toBe(0)
  })

  it('maps CRITICAL to the critical tile tier', () => {
    const result = buildMeterSeverityMap([{ meterId: 'm1', severity: 'CRITICAL' }])
    expect(result.get('m1')).toEqual({ severity: 'critical', label: 'Critical' })
  })

  it('maps WARNING to the warning tile tier with a Warning label', () => {
    const result = buildMeterSeverityMap([{ meterId: 'm1', severity: 'WARNING' }])
    expect(result.get('m1')).toEqual({ severity: 'warning', label: 'Warning' })
  })

  it('maps INFO to the warning tile tier but keeps a distinct Info label', () => {
    const result = buildMeterSeverityMap([{ meterId: 'm1', severity: 'INFO' }])
    expect(result.get('m1')).toEqual({ severity: 'warning', label: 'Info' })
  })

  it('picks the worst of several active alerts on the same meter', () => {
    const result = buildMeterSeverityMap([
      { meterId: 'm1', severity: 'INFO' },
      { meterId: 'm1', severity: 'CRITICAL' },
      { meterId: 'm1', severity: 'WARNING' },
    ])
    expect(result.get('m1')).toEqual({ severity: 'critical', label: 'Critical' })
  })

  it('keeps unrelated meters independent - one alert does not affect another meter', () => {
    const result = buildMeterSeverityMap([
      { meterId: 'm1', severity: 'CRITICAL' },
      { meterId: 'm2', severity: 'WARNING' },
    ])
    expect(result.get('m1')).toEqual({ severity: 'critical', label: 'Critical' })
    expect(result.get('m2')).toEqual({ severity: 'warning', label: 'Warning' })
  })
})
