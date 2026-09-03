import { runtimeConfig } from '../../../shared/config/runtimeConfig'
import { restFetch } from '../../../shared/rest/restClient'

const BASE_URL = `${runtimeConfig.gatewayRestUrl}/api/v1/ingestion`

/** Mirrors IngestionStatusResponse (Ingestor.Admin), proxied verbatim through the Gateway. */
export interface IngestionStatus {
  lastOutcome: string | null
  lastPolledAt: string | null
  lastSuccessAt: string | null
  lastBatchId: string | null
  lastReadingCount: number | null
  lastHttpStatus: number | null
  lastDurationMs: number | null
  lastErrorMessage: string | null
  totalPolls: number
  failureCountsByReason: Readonly<Record<string, number>>
  circuitBreakerState: string
  pollingIntervalSeconds: number
}

/** Mirrors IngestionTriggerResponse. */
export interface IngestionTriggerResult {
  batchId: string
  outcome: string
  readingCount: number
  httpStatus: number | null
  durationMs: number
  errorMessage: string | null
  fetchedAt: string
}

/** Mirrors IngestionConfigResponse. */
export interface IngestionConfigResult {
  pollingIntervalSeconds: number
}

export function fetchIngestionStatus(): Promise<IngestionStatus> {
  return restFetch<IngestionStatus>(`${BASE_URL}/status`)
}

/** Runs one poll now and returns its outcome - awaited, not fire-and-forget (see
 * IngestionAdminController.TriggerAsync's own doc comment for why that's safe to await). */
export function triggerIngestion(): Promise<IngestionTriggerResult> {
  return restFetch<IngestionTriggerResult>(`${BASE_URL}/trigger`, { method: 'POST' })
}

/** In-memory on the Ingestor - resets to the configured default on restart. */
export function updateIngestionInterval(pollingIntervalSeconds: number): Promise<IngestionConfigResult> {
  return restFetch<IngestionConfigResult>(`${BASE_URL}/config`, {
    method: 'PUT',
    body: JSON.stringify({ pollingIntervalSeconds }),
  })
}
