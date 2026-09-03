import { getMetricDisplayInfo } from '../../../entities/meter/metricDisplay'
import type { AlertRuleRow } from './AlertRulesTable'
import type { AlertRuleFormValues, AlertRuleOperatorCode, AlertRuleSeverityCode } from './alertRuleValidation'

/**
 * The GraphQL read model (which AlertRuleRow mirrors the shape of) uses the schema's uppercase
 * enums (AlertOperator/AlertSeverity); the REST write model (and this form) uses the lower-case
 * codes AlertRuleRequestValidator checks - `toLowerCase` is enough for both since the two
 * vocabularies are otherwise identical (GT/gt, CRITICAL/critical, ...).
 */
export function toAlertRuleFormValues(rule: AlertRuleRow): AlertRuleFormValues {
  return {
    name: rule.name,
    location: rule.location ?? '',
    meterType: rule.meterType ?? '',
    metricCode: rule.metricCode,
    operator: rule.operator.toLowerCase() as AlertRuleOperatorCode,
    thresholdKind: getMetricDisplayInfo(rule.metricCode).kind,
    thresholdNumeric: rule.thresholdNumeric != null ? String(rule.thresholdNumeric) : '',
    thresholdBool: rule.thresholdBool ?? true,
    severity: rule.severity.toLowerCase() as AlertRuleSeverityCode,
    hysteresisPercent: String(rule.hysteresisPercent),
    cooldownSeconds: String(rule.cooldownSeconds),
    isEnabled: rule.isEnabled,
  }
}
