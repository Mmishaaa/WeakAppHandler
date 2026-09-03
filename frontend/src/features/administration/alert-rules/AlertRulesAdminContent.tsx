import { useState } from 'react'
import { RestError } from '../../../shared/rest/restClient'
import { AlertRuleForm } from './AlertRuleForm'
import { toAlertRuleFormValues } from './alertRuleMapping'
import { emptyAlertRuleFormValues, toAlertRuleRequestBody, type AlertRuleRequestBody } from './alertRuleValidation'
import { AlertRulesTable, type AlertRuleRow } from './AlertRulesTable'
import './alert-rules-admin-content.css'

export interface AlertRulesAdminContentProps {
  rules: readonly AlertRuleRow[]
  onCreate: (request: AlertRuleRequestBody) => Promise<boolean>
  onUpdate: (id: string, request: AlertRuleRequestBody) => Promise<boolean>
  onDelete: (id: string) => Promise<void>
  saving: boolean
  saveError?: unknown
}

function serverErrorMessage(error: unknown): string | undefined {
  if (!error) {
    return undefined
  }
  if (error instanceof RestError) {
    const firstFieldError = Object.values(error.fieldErrors)[0]?.[0]
    return firstFieldError ?? error.message
  }
  return 'Something went wrong while saving the rule.'
}

/**
 * Owns which rule (if any) is being created/edited; the table and form themselves stay pure.
 * Reads come from the Gateway's GraphQL `alertRules` query, writes go straight to Notification.Api
 * (see useAlertRulesAdmin.ts for why there's no GraphQL mutation to use instead).
 */
export function AlertRulesAdminContent({ rules, onCreate, onUpdate, onDelete, saving, saveError }: AlertRulesAdminContentProps) {
  const [creating, setCreating] = useState(false)
  const [editingRule, setEditingRule] = useState<AlertRuleRow | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string>()

  async function handleDelete(id: string) {
    setPendingDeleteId(id)
    await onDelete(id)
    setPendingDeleteId(undefined)
  }

  async function handleCreateSubmit(values: Parameters<typeof toAlertRuleRequestBody>[0]) {
    const ok = await onCreate(toAlertRuleRequestBody(values))
    if (ok) {
      setCreating(false)
    }
  }

  async function handleUpdateSubmit(rule: AlertRuleRow, values: Parameters<typeof toAlertRuleRequestBody>[0]) {
    const ok = await onUpdate(rule.id, toAlertRuleRequestBody(values))
    if (ok) {
      setEditingRule(null)
    }
  }

  const errorMessage = serverErrorMessage(saveError)

  return (
    <div className="alert-rules-admin-content">
      <AlertRulesTable
        rules={rules}
        onEdit={(rule) => {
          setEditingRule(rule)
          setCreating(false)
        }}
        onDelete={(id) => void handleDelete(id)}
        deletingId={pendingDeleteId}
      />

      {editingRule ? (
        <section className="alert-rules-admin-content__form" aria-label={`Edit rule ${editingRule.name}`}>
          <h3>Edit rule</h3>
          <AlertRuleForm
            key={editingRule.id}
            initialValues={toAlertRuleFormValues(editingRule)}
            submitLabel="Save changes"
            onSubmit={(values) => void handleUpdateSubmit(editingRule, values)}
            onCancel={() => setEditingRule(null)}
            submitting={saving}
            serverError={errorMessage}
          />
        </section>
      ) : creating ? (
        <section className="alert-rules-admin-content__form" aria-label="New rule">
          <h3>New rule</h3>
          <AlertRuleForm
            key="new"
            initialValues={emptyAlertRuleFormValues()}
            submitLabel="Create rule"
            onSubmit={(values) => void handleCreateSubmit(values)}
            onCancel={() => setCreating(false)}
            submitting={saving}
            serverError={errorMessage}
          />
        </section>
      ) : (
        <button type="button" onClick={() => setCreating(true)}>
          New rule
        </button>
      )}
    </div>
  )
}
