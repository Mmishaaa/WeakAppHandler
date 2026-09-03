import { useState, type FormEvent } from 'react'
import { HISTORY_METRICS } from '../../../entities/meter/historyMetrics'
import { getMetricDisplayInfo } from '../../../entities/meter/metricDisplay'
import {
  VALID_OPERATORS,
  VALID_SEVERITIES,
  isAlertRuleFormValid,
  validateAlertRuleForm,
  type AlertRuleFormErrors,
  type AlertRuleFormValues,
  type AlertRuleOperatorCode,
  type AlertRuleSeverityCode,
} from './alertRuleValidation'
import './alert-rule-form.css'

export interface AlertRuleFormProps {
  initialValues: AlertRuleFormValues
  submitLabel: string
  onSubmit: (values: AlertRuleFormValues) => void
  onCancel?: () => void
  submitting: boolean
  serverError?: string
}

const OPERATOR_LABEL: Record<AlertRuleOperatorCode, string> = {
  gt: 'Greater than (>)',
  gte: 'Greater than or equal (≥)',
  lt: 'Less than (<)',
  lte: 'Less than or equal (≤)',
  eq: 'Equal to (=)',
}

const SEVERITY_LABEL: Record<AlertRuleSeverityCode, string> = {
  info: 'Info',
  warning: 'Warning',
  critical: 'Critical',
}

function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) {
    return null
  }
  return (
    <p id={id} role="alert" className="alert-rule-form__error">
      {message}
    </p>
  )
}

function errorId(field: keyof AlertRuleFormErrors): string {
  return `alert-rule-form-error-${field}`
}

function describedBy(errors: AlertRuleFormErrors, field: keyof AlertRuleFormErrors): string | undefined {
  return errors[field] ? errorId(field) : undefined
}

/**
 * Create/edit form for a single alert rule (TASK-040), with inline validation mirroring
 * AlertRuleRequestValidator (Notification.Api) - see alertRuleValidation.ts. Errors are computed
 * on every render but only shown once the user has attempted a submit, so an empty "create" form
 * doesn't open with every field already flagged invalid.
 */
export function AlertRuleForm({ initialValues, submitLabel, onSubmit, onCancel, submitting, serverError }: AlertRuleFormProps) {
  const [values, setValues] = useState<AlertRuleFormValues>(initialValues)
  const [submitted, setSubmitted] = useState(false)

  const errors = validateAlertRuleForm(values)
  const shownErrors = submitted ? errors : {}
  const metricInfo = getMetricDisplayInfo(values.metricCode)

  function handleMetricChange(metricCode: string) {
    const kind = getMetricDisplayInfo(metricCode).kind
    setValues((current) => ({
      ...current,
      metricCode,
      thresholdKind: kind,
      thresholdNumeric: kind === 'numeric' ? current.thresholdNumeric : '',
      thresholdBool: kind === 'boolean' ? current.thresholdBool : true,
    }))
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    if (!isAlertRuleFormValid(errors)) {
      // Invalid submissions never reach the network - the whole point of inline validation.
      return
    }
    onSubmit(values)
  }

  return (
    <form className="alert-rule-form" onSubmit={handleSubmit} noValidate>
      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-name">Name</label>
        <input
          id="alert-rule-name"
          type="text"
          value={values.name}
          onChange={(event) => setValues((current) => ({ ...current, name: event.target.value }))}
          aria-invalid={shownErrors.name ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'name')}
        />
        <FieldError id={errorId('name')} message={shownErrors.name} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-metric">Metric</label>
        <select
          id="alert-rule-metric"
          value={values.metricCode}
          onChange={(event) => handleMetricChange(event.target.value)}
          aria-invalid={shownErrors.metricCode ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'metricCode')}
        >
          <option value="">Select a metric…</option>
          {HISTORY_METRICS.map((metric) => (
            <option key={metric.metricCode} value={metric.metricCode}>
              {getMetricDisplayInfo(metric.metricCode).displayName}
            </option>
          ))}
        </select>
        <FieldError id={errorId('metricCode')} message={shownErrors.metricCode} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-location">Location (optional - blank matches any)</label>
        <input
          id="alert-rule-location"
          type="text"
          value={values.location}
          onChange={(event) => setValues((current) => ({ ...current, location: event.target.value }))}
          aria-invalid={shownErrors.location ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'location')}
        />
        <FieldError id={errorId('location')} message={shownErrors.location} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-meter-type">Meter type (optional - blank matches any)</label>
        <input
          id="alert-rule-meter-type"
          type="text"
          value={values.meterType}
          onChange={(event) => setValues((current) => ({ ...current, meterType: event.target.value }))}
          aria-invalid={shownErrors.meterType ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'meterType')}
        />
        <FieldError id={errorId('meterType')} message={shownErrors.meterType} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-operator">Operator</label>
        <select
          id="alert-rule-operator"
          value={values.operator}
          onChange={(event) =>
            setValues((current) => ({ ...current, operator: event.target.value as AlertRuleOperatorCode }))
          }
          aria-invalid={shownErrors.operator ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'operator')}
        >
          {VALID_OPERATORS.map((operator) => (
            <option key={operator} value={operator}>
              {OPERATOR_LABEL[operator]}
            </option>
          ))}
        </select>
        <FieldError id={errorId('operator')} message={shownErrors.operator} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-threshold">Threshold {metricInfo.unit ? `(${metricInfo.unit})` : ''}</label>
        {values.thresholdKind === 'boolean' ? (
          <select
            id="alert-rule-threshold"
            value={values.thresholdBool ? 'true' : 'false'}
            onChange={(event) => setValues((current) => ({ ...current, thresholdBool: event.target.value === 'true' }))}
          >
            <option value="true">Yes</option>
            <option value="false">No</option>
          </select>
        ) : (
          <input
            id="alert-rule-threshold"
            type="number"
            value={values.thresholdNumeric}
            onChange={(event) => setValues((current) => ({ ...current, thresholdNumeric: event.target.value }))}
            aria-invalid={shownErrors.thresholdNumeric ? true : undefined}
            aria-describedby={describedBy(shownErrors, 'thresholdNumeric')}
          />
        )}
        <FieldError id={errorId('thresholdNumeric')} message={shownErrors.thresholdNumeric} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-severity">Severity</label>
        <select
          id="alert-rule-severity"
          value={values.severity}
          onChange={(event) =>
            setValues((current) => ({ ...current, severity: event.target.value as AlertRuleSeverityCode }))
          }
          aria-invalid={shownErrors.severity ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'severity')}
        >
          {VALID_SEVERITIES.map((severity) => (
            <option key={severity} value={severity}>
              {SEVERITY_LABEL[severity]}
            </option>
          ))}
        </select>
        <FieldError id={errorId('severity')} message={shownErrors.severity} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-hysteresis">Hysteresis % (optional, default 5)</label>
        <input
          id="alert-rule-hysteresis"
          type="number"
          value={values.hysteresisPercent}
          onChange={(event) => setValues((current) => ({ ...current, hysteresisPercent: event.target.value }))}
          aria-invalid={shownErrors.hysteresisPercent ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'hysteresisPercent')}
        />
        <FieldError id={errorId('hysteresisPercent')} message={shownErrors.hysteresisPercent} />
      </div>

      <div className="alert-rule-form__field">
        <label htmlFor="alert-rule-cooldown">Cooldown seconds (optional, default 300)</label>
        <input
          id="alert-rule-cooldown"
          type="number"
          value={values.cooldownSeconds}
          onChange={(event) => setValues((current) => ({ ...current, cooldownSeconds: event.target.value }))}
          aria-invalid={shownErrors.cooldownSeconds ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'cooldownSeconds')}
        />
        <FieldError id={errorId('cooldownSeconds')} message={shownErrors.cooldownSeconds} />
      </div>

      <div className="alert-rule-form__field alert-rule-form__field--checkbox">
        <label htmlFor="alert-rule-enabled">
          <input
            id="alert-rule-enabled"
            type="checkbox"
            checked={values.isEnabled}
            onChange={(event) => setValues((current) => ({ ...current, isEnabled: event.target.checked }))}
          />
          Enabled
        </label>
      </div>

      {serverError && (
        <p role="alert" className="alert-rule-form__error">
          {serverError}
        </p>
      )}

      <div className="alert-rule-form__actions">
        <button type="submit" disabled={submitting}>
          {submitting ? 'Saving…' : submitLabel}
        </button>
        {onCancel && (
          <button type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </button>
        )}
      </div>
    </form>
  )
}
