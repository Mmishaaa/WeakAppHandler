import { describe, expect, it } from 'vitest'
import {
  emptyAlertRuleFormValues,
  isAlertRuleFormValid,
  toAlertRuleRequestBody,
  validateAlertRuleForm,
  type AlertRuleFormValues,
} from './alertRuleValidation'

function validForm(overrides: Partial<AlertRuleFormValues> = {}): AlertRuleFormValues {
  return {
    ...emptyAlertRuleFormValues(),
    name: 'CO2 too high',
    metricCode: 'co2',
    operator: 'gt',
    thresholdKind: 'numeric',
    thresholdNumeric: '1000',
    severity: 'critical',
    ...overrides,
  }
}

describe('validateAlertRuleForm', () => {
  it('accepts a minimal valid rule', () => {
    const errors = validateAlertRuleForm(validForm())
    expect(isAlertRuleFormValid(errors)).toBe(true)
  })

  it('requires a name', () => {
    const errors = validateAlertRuleForm(validForm({ name: '   ' }))
    expect(errors.name).toBe('Name is required.')
  })

  it('requires a metric', () => {
    const errors = validateAlertRuleForm(validForm({ metricCode: '' }))
    expect(errors.metricCode).toBe('Metric is required.')
  })

  it('rejects an operator outside the known set', () => {
    const errors = validateAlertRuleForm(validForm({ operator: 'ne' as never }))
    expect(errors.operator).toMatch(/Operator must be one of/)
  })

  it('rejects a severity outside the known set', () => {
    const errors = validateAlertRuleForm(validForm({ severity: 'urgent' as never }))
    expect(errors.severity).toMatch(/Severity must be one of/)
  })

  it('rejects a negative cooldown', () => {
    const errors = validateAlertRuleForm(validForm({ cooldownSeconds: '-1' }))
    expect(errors.cooldownSeconds).toBe('Cooldown must not be negative.')
  })

  it('accepts a blank cooldown (means "use the default")', () => {
    const errors = validateAlertRuleForm(validForm({ cooldownSeconds: '' }))
    expect(errors.cooldownSeconds).toBeUndefined()
  })

  it('rejects hysteresis outside 0-100', () => {
    expect(validateAlertRuleForm(validForm({ hysteresisPercent: '101' })).hysteresisPercent).toBe(
      'Hysteresis must be between 0 and 100.',
    )
    expect(validateAlertRuleForm(validForm({ hysteresisPercent: '-1' })).hysteresisPercent).toBe(
      'Hysteresis must be between 0 and 100.',
    )
  })

  it('accepts hysteresis exactly on the boundary', () => {
    expect(validateAlertRuleForm(validForm({ hysteresisPercent: '0' })).hysteresisPercent).toBeUndefined()
    expect(validateAlertRuleForm(validForm({ hysteresisPercent: '100' })).hysteresisPercent).toBeUndefined()
  })

  it('requires a numeric threshold value when the metric is numeric', () => {
    const errors = validateAlertRuleForm(validForm({ thresholdNumeric: '' }))
    expect(errors.thresholdNumeric).toBe('Threshold value is required.')
  })

  it('rejects a non-numeric threshold value', () => {
    const errors = validateAlertRuleForm(validForm({ thresholdNumeric: 'not-a-number' }))
    expect(errors.thresholdNumeric).toBe('Threshold must be a number.')
  })

  it('does not require a numeric threshold for a boolean metric', () => {
    const errors = validateAlertRuleForm(
      validForm({ thresholdKind: 'boolean', thresholdNumeric: '', thresholdBool: true }),
    )
    expect(errors.thresholdNumeric).toBeUndefined()
  })
})

describe('toAlertRuleRequestBody', () => {
  it('trims blank optional fields down to null', () => {
    const body = toAlertRuleRequestBody(validForm({ location: '  ', meterType: '' }))
    expect(body.location).toBeNull()
    expect(body.meterType).toBeNull()
  })

  it('carries the numeric threshold and leaves the boolean one null', () => {
    const body = toAlertRuleRequestBody(validForm({ thresholdNumeric: '1400' }))
    expect(body.thresholdNumeric).toBe(1400)
    expect(body.thresholdBool).toBeNull()
  })

  it('carries the boolean threshold and leaves the numeric one null for a boolean metric', () => {
    const body = toAlertRuleRequestBody(
      validForm({ thresholdKind: 'boolean', thresholdNumeric: '', thresholdBool: true }),
    )
    expect(body.thresholdBool).toBe(true)
    expect(body.thresholdNumeric).toBeNull()
  })

  it('leaves hysteresis/cooldown null when left blank, for the server to default them', () => {
    const body = toAlertRuleRequestBody(validForm({ hysteresisPercent: '', cooldownSeconds: '' }))
    expect(body.hysteresisPercent).toBeNull()
    expect(body.cooldownSeconds).toBeNull()
  })
})
