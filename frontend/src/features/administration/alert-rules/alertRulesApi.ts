import { runtimeConfig } from '../../../shared/config/runtimeConfig'
import { restFetch } from '../../../shared/rest/restClient'
import type { AlertRuleRequestBody } from './alertRuleValidation'

const BASE_URL = `${runtimeConfig.notificationApiUrl}/api/v1/alert-rules`

/**
 * Mirrors AlertRuleResponse (Notification.Api). Reads go through the Gateway's GraphQL
 * `alertRules` query instead (see useAlertRulesAdmin.ts) - there is no GraphQL mutation type, so
 * writes go straight to Notification.Api, the one place this app calls a REST endpoint that isn't
 * proxied through the Gateway.
 */
export interface AlertRuleResponse {
  id: string
  name: string
  location: string | null
  meterType: string | null
  metricCode: string
  operator: string
  thresholdNumeric: number | null
  thresholdBool: boolean | null
  severity: string
  hysteresisPercent: number
  cooldownSeconds: number
  isEnabled: boolean
  lastTriggeredAt: string | null
  createdAt: string
  updatedAt: string
}

export function createAlertRule(request: AlertRuleRequestBody): Promise<AlertRuleResponse> {
  return restFetch<AlertRuleResponse>(BASE_URL, { method: 'POST', body: JSON.stringify(request) })
}

export function updateAlertRule(id: string, request: AlertRuleRequestBody): Promise<AlertRuleResponse> {
  return restFetch<AlertRuleResponse>(`${BASE_URL}/${id}`, { method: 'PUT', body: JSON.stringify(request) })
}

export function deleteAlertRule(id: string): Promise<void> {
  return restFetch<void>(`${BASE_URL}/${id}`, { method: 'DELETE' })
}
