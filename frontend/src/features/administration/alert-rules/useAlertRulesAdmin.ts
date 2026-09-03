import { useCallback, useState } from 'react'
import { useQuery } from '@apollo/client/react'
import { graphql } from '../../../gql'
import { createAlertRule, deleteAlertRule, updateAlertRule } from './alertRulesApi'
import type { AlertRuleRequestBody } from './alertRuleValidation'

const ADMIN_ALERT_RULES = graphql(`
  query AdminAlertRules {
    alertRules(order: [{ name: ASC }]) {
      id
      name
      location
      meterType
      metricCode
      operator
      thresholdNumeric
      thresholdBool
      severity
      hysteresisPercent
      cooldownSeconds
      isEnabled
      lastTriggeredAt
      createdAt
      updatedAt
    }
  }
`)

/**
 * Reads through the Gateway's `alertRules` GraphQL query (TASK-032) - there is no GraphQL mutation
 * type for it, so create/update/delete go straight to Notification.Api's own REST CRUD (TASK-030)
 * instead, each followed by a refetch of the GraphQL list so the table reflects the write without
 * a second, separately-tracked copy of the same row.
 */
export function useAlertRulesAdmin() {
  const { data, loading, error, refetch } = useQuery(ADMIN_ALERT_RULES, {
    notifyOnNetworkStatusChange: true,
  })

  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<unknown>()

  const create = useCallback(
    async (request: AlertRuleRequestBody) => {
      setSaving(true)
      try {
        await createAlertRule(request)
        await refetch()
        setSaveError(undefined)
        return true
      } catch (thrown) {
        setSaveError(thrown)
        return false
      } finally {
        setSaving(false)
      }
    },
    [refetch],
  )

  const update = useCallback(
    async (id: string, request: AlertRuleRequestBody) => {
      setSaving(true)
      try {
        await updateAlertRule(id, request)
        await refetch()
        setSaveError(undefined)
        return true
      } catch (thrown) {
        setSaveError(thrown)
        return false
      } finally {
        setSaving(false)
      }
    },
    [refetch],
  )

  const remove = useCallback(
    async (id: string) => {
      setSaving(true)
      try {
        await deleteAlertRule(id)
        await refetch()
        setSaveError(undefined)
      } catch (thrown) {
        setSaveError(thrown)
      } finally {
        setSaving(false)
      }
    },
    [refetch],
  )

  return {
    // Left undefined (rather than defaulted to []) until the first response lands, so
    // AsyncBoundary's loading skeleton shows on first load instead of the empty-list state.
    data,
    loading,
    error,
    refetch,
    create,
    update,
    remove,
    saving,
    saveError,
  }
}
