import { describe, expect, it } from 'vitest'
import { compareAlertSeverity, toDisplaySeverity } from './alertSeverityDisplay'

describe('toDisplaySeverity', () => {
  it('maps CRITICAL to the critical tier', () => {
    expect(toDisplaySeverity('CRITICAL')).toEqual({ severity: 'critical', label: 'Critical' })
  })

  it('maps WARNING to the warning tier with a Warning label', () => {
    expect(toDisplaySeverity('WARNING')).toEqual({ severity: 'warning', label: 'Warning' })
  })

  it('maps INFO to the warning tier but keeps a distinct Info label', () => {
    expect(toDisplaySeverity('INFO')).toEqual({ severity: 'warning', label: 'Info' })
  })
})

describe('compareAlertSeverity', () => {
  it('ranks CRITICAL above WARNING above INFO', () => {
    expect(compareAlertSeverity('CRITICAL', 'WARNING')).toBeGreaterThan(0)
    expect(compareAlertSeverity('WARNING', 'INFO')).toBeGreaterThan(0)
    expect(compareAlertSeverity('INFO', 'INFO')).toBe(0)
  })
})
