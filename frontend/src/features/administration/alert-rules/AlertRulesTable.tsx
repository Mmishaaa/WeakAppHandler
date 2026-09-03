import { toDisplaySeverity } from '../../../entities/alert/alertSeverityDisplay'
import { formatMetricValue, getMetricDisplayInfo } from '../../../entities/meter/metricDisplay'
import type { AlertOperator, AlertSeverity } from '../../../gql/graphql'
import { SeverityIndicator } from '../../../shared/ui/severity/SeverityIndicator'
import './alert-rules-table.css'

export interface AlertRuleRow {
  id: string
  name: string
  location?: string | null
  meterType?: string | null
  metricCode: string
  operator: AlertOperator
  thresholdNumeric?: number | string | null
  thresholdBool?: boolean | null
  severity: AlertSeverity
  hysteresisPercent: number | string
  cooldownSeconds: number
  isEnabled: boolean
}

export interface AlertRulesTableProps {
  rules: readonly AlertRuleRow[]
  onEdit: (rule: AlertRuleRow) => void
  onDelete: (id: string) => void
  deletingId?: string
}

const OPERATOR_SYMBOL: Record<AlertOperator, string> = {
  GT: '>',
  GTE: '≥',
  LT: '<',
  LTE: '≤',
  EQ: '=',
}

export function AlertRulesTable({ rules, onEdit, onDelete, deletingId }: AlertRulesTableProps) {
  if (rules.length === 0) {
    return <p className="alert-rules-table__empty">No alert rules yet.</p>
  }

  return (
    <table className="alert-rules-table">
      <thead>
        <tr>
          <th scope="col">Name</th>
          <th scope="col">Scope</th>
          <th scope="col">Condition</th>
          <th scope="col">Severity</th>
          <th scope="col">Hysteresis</th>
          <th scope="col">Cooldown</th>
          <th scope="col">Enabled</th>
          <th scope="col">
            <span className="visually-hidden">Actions</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {rules.map((rule) => {
          const metric = getMetricDisplayInfo(rule.metricCode)
          const thresholdDisplay = formatMetricValue(rule.metricCode, rule.thresholdNumeric, rule.thresholdBool)
          const display = toDisplaySeverity(rule.severity)

          return (
            <tr key={rule.id}>
              <td>{rule.name}</td>
              <td>{[rule.location ?? 'Any location', rule.meterType ?? 'any meter type'].join(' · ')}</td>
              <td>
                {metric.displayName} {OPERATOR_SYMBOL[rule.operator]} {thresholdDisplay}
              </td>
              <td>
                <SeverityIndicator severity={display.severity} label={display.label} />
              </td>
              <td>{rule.hysteresisPercent}%</td>
              <td>{rule.cooldownSeconds}s</td>
              <td>{rule.isEnabled ? 'Yes' : 'No'}</td>
              <td className="alert-rules-table__actions">
                <button type="button" onClick={() => onEdit(rule)}>
                  Edit
                </button>
                <button
                  type="button"
                  onClick={() => onDelete(rule.id)}
                  disabled={deletingId === rule.id}
                  aria-label={`Delete rule ${rule.name}`}
                >
                  {deletingId === rule.id ? 'Deleting…' : 'Delete'}
                </button>
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
