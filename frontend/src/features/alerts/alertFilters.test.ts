import { describe, expect, it } from 'vitest'
import { DEFAULT_ALERT_FILTERS, filterAlerts } from './alertFilters'

interface TestAlert {
  id: string
  status: 'ACTIVE' | 'RESOLVED'
  severity: 'INFO' | 'WARNING' | 'CRITICAL'
}

const ALERTS: readonly TestAlert[] = [
  { id: 'a1', status: 'ACTIVE', severity: 'CRITICAL' },
  { id: 'a2', status: 'RESOLVED', severity: 'WARNING' },
  { id: 'a3', status: 'ACTIVE', severity: 'INFO' },
]

describe('filterAlerts', () => {
  it('returns every alert when both filters are ALL', () => {
    expect(filterAlerts(ALERTS, DEFAULT_ALERT_FILTERS)).toEqual(ALERTS)
  })

  it('narrows by status alone', () => {
    const result = filterAlerts(ALERTS, { status: 'ACTIVE', severity: 'ALL' })
    expect(result.map((a) => a.id)).toEqual(['a1', 'a3'])
  })

  it('narrows by severity alone', () => {
    const result = filterAlerts(ALERTS, { status: 'ALL', severity: 'CRITICAL' })
    expect(result.map((a) => a.id)).toEqual(['a1'])
  })

  it('combines status and severity filters', () => {
    const result = filterAlerts(ALERTS, { status: 'ACTIVE', severity: 'INFO' })
    expect(result.map((a) => a.id)).toEqual(['a3'])
  })

  it('returns an empty list when nothing matches', () => {
    const result = filterAlerts(ALERTS, { status: 'RESOLVED', severity: 'CRITICAL' })
    expect(result).toEqual([])
  })
})
