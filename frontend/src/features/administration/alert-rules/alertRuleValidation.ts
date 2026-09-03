/**
 * Client-side mirror of AlertRuleRequestValidator (Notification.Api) - same field names, same
 * bounds - so a bad submission is rejected before it ever reaches the network (TASK-040's own
 * acceptance criterion), not just reported back after a round trip.
 */
export const VALID_OPERATORS = ['gt', 'gte', 'lt', 'lte', 'eq'] as const
export type AlertRuleOperatorCode = (typeof VALID_OPERATORS)[number]

export const VALID_SEVERITIES = ['info', 'warning', 'critical'] as const
export type AlertRuleSeverityCode = (typeof VALID_SEVERITIES)[number]

export interface AlertRuleFormValues {
  name: string
  location: string
  meterType: string
  metricCode: string
  operator: AlertRuleOperatorCode
  thresholdKind: 'numeric' | 'boolean'
  thresholdNumeric: string
  thresholdBool: boolean
  severity: AlertRuleSeverityCode
  hysteresisPercent: string
  cooldownSeconds: string
  isEnabled: boolean
}

export interface AlertRuleFormErrors {
  name?: string
  metricCode?: string
  location?: string
  meterType?: string
  operator?: string
  severity?: string
  cooldownSeconds?: string
  hysteresisPercent?: string
  thresholdNumeric?: string
}

export function emptyAlertRuleFormValues(): AlertRuleFormValues {
  return {
    name: '',
    location: '',
    meterType: '',
    metricCode: '',
    operator: 'gt',
    thresholdKind: 'numeric',
    thresholdNumeric: '',
    thresholdBool: true,
    severity: 'warning',
    hysteresisPercent: '',
    cooldownSeconds: '',
    isEnabled: true,
  }
}

function isBlank(value: string): boolean {
  return value.trim().length === 0
}

function parsedNumber(value: string): number | undefined {
  if (isBlank(value)) {
    return undefined
  }
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : undefined
}

/** Returns one error per invalid field, empty when the form is ready to submit. */
export function validateAlertRuleForm(values: AlertRuleFormValues): AlertRuleFormErrors {
  const errors: AlertRuleFormErrors = {}

  if (isBlank(values.name)) {
    errors.name = 'Name is required.'
  } else if (values.name.length > 128) {
    errors.name = 'Name must be 128 characters or fewer.'
  }

  if (isBlank(values.metricCode)) {
    errors.metricCode = 'Metric is required.'
  } else if (values.metricCode.length > 32) {
    errors.metricCode = 'Metric code must be 32 characters or fewer.'
  }

  if (values.location.length > 64) {
    errors.location = 'Location must be 64 characters or fewer.'
  }

  if (values.meterType.length > 32) {
    errors.meterType = 'Meter type must be 32 characters or fewer.'
  }

  if (!VALID_OPERATORS.includes(values.operator)) {
    errors.operator = `Operator must be one of: ${VALID_OPERATORS.join(', ')}.`
  }

  if (!VALID_SEVERITIES.includes(values.severity)) {
    errors.severity = `Severity must be one of: ${VALID_SEVERITIES.join(', ')}.`
  }

  if (!isBlank(values.cooldownSeconds)) {
    const cooldown = parsedNumber(values.cooldownSeconds)
    if (cooldown === undefined || cooldown < 0) {
      errors.cooldownSeconds = 'Cooldown must not be negative.'
    }
  }

  if (!isBlank(values.hysteresisPercent)) {
    const hysteresis = parsedNumber(values.hysteresisPercent)
    if (hysteresis === undefined || hysteresis < 0 || hysteresis > 100) {
      errors.hysteresisPercent = 'Hysteresis must be between 0 and 100.'
    }
  }

  if (values.thresholdKind === 'numeric') {
    if (isBlank(values.thresholdNumeric)) {
      errors.thresholdNumeric = 'Threshold value is required.'
    } else if (parsedNumber(values.thresholdNumeric) === undefined) {
      errors.thresholdNumeric = 'Threshold must be a number.'
    }
  }

  return errors
}

export function isAlertRuleFormValid(errors: AlertRuleFormErrors): boolean {
  return Object.keys(errors).length === 0
}

/** REST wire shape (AlertRuleRequest) built from already-validated form values. */
export interface AlertRuleRequestBody {
  name: string
  location: string | null
  meterType: string | null
  metricCode: string
  operator: string
  thresholdNumeric: number | null
  thresholdBool: boolean | null
  severity: string
  hysteresisPercent: number | null
  cooldownSeconds: number | null
  isEnabled: boolean
}

export function toAlertRuleRequestBody(values: AlertRuleFormValues): AlertRuleRequestBody {
  return {
    name: values.name.trim(),
    location: isBlank(values.location) ? null : values.location.trim(),
    meterType: isBlank(values.meterType) ? null : values.meterType.trim(),
    metricCode: values.metricCode.trim(),
    operator: values.operator,
    thresholdNumeric: values.thresholdKind === 'numeric' ? (parsedNumber(values.thresholdNumeric) ?? null) : null,
    thresholdBool: values.thresholdKind === 'boolean' ? values.thresholdBool : null,
    severity: values.severity,
    hysteresisPercent: parsedNumber(values.hysteresisPercent) ?? null,
    cooldownSeconds: parsedNumber(values.cooldownSeconds) ?? null,
    isEnabled: values.isEnabled,
  }
}
