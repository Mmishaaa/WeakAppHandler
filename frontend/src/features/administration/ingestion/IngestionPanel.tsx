import { useState, type FormEvent } from 'react'
import { formatRelativeTime } from '../../../shared/time/relativeTime'
import type { IngestionStatus, IngestionTriggerResult } from './ingestionApi'
import './ingestion-panel.css'

export interface IngestionPanelProps {
  status: IngestionStatus
  now: Date
  onTrigger: () => void
  triggering: boolean
  lastTriggerResult?: IngestionTriggerResult
  triggerError?: unknown
  onIntervalChange: (seconds: number) => void
  updatingInterval: boolean
  intervalError?: string
}

const FAILURE_REASON_LABELS: Readonly<Record<string, string>> = {
  HttpError: 'HTTP error',
  Corrupted: 'Corrupted response',
  Unauthorized: 'Unauthorized',
  Timeout: 'Timeout',
  CircuitOpen: 'Circuit open',
}

function describeFailureReason(reason: string): string {
  return FAILURE_REASON_LABELS[reason] ?? reason
}

function triggerErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'The manual trigger failed.'
}

/**
 * Pure presentation of the Ingestor's admin state (TASK-040): last poll outcome, failure counts by
 * reason, circuit-breaker state, the interval control, and the manual-trigger button. No fetching
 * here - see useIngestionAdmin for that.
 */
export function IngestionPanel({
  status,
  now,
  onTrigger,
  triggering,
  lastTriggerResult,
  triggerError,
  onIntervalChange,
  updatingInterval,
  intervalError,
}: IngestionPanelProps) {
  const [intervalInput, setIntervalInput] = useState(String(status.pollingIntervalSeconds))

  // Resyncs the editable field whenever the Ingestor's own interval changes (after a save, or on
  // first load), following React's documented "adjusting state when a prop changes" pattern
  // (https://react.dev/learn/you-might-not-need-an-effect) - setState during render rather than
  // in a useEffect, so this doesn't trigger the extra-render pattern react-hooks/set-state-in-effect
  // flags. Safe to overwrite mid-edit input here since this panel never polls status on a timer of
  // its own - a resync only happens right after this same component's own save completes.
  const [syncedInterval, setSyncedInterval] = useState(status.pollingIntervalSeconds)
  if (syncedInterval !== status.pollingIntervalSeconds) {
    setSyncedInterval(status.pollingIntervalSeconds)
    setIntervalInput(String(status.pollingIntervalSeconds))
  }

  const failureEntries = Object.entries(status.failureCountsByReason)

  function handleIntervalSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const seconds = Number(intervalInput)
    if (Number.isFinite(seconds) && seconds > 0) {
      onIntervalChange(seconds)
    }
  }

  return (
    <div className="ingestion-panel">
      <dl className="ingestion-panel__facts">
        <div>
          <dt>Last outcome</dt>
          <dd>{status.lastOutcome ?? 'No poll yet'}</dd>
        </div>
        <div>
          <dt>Last polled</dt>
          <dd>{status.lastPolledAt ? formatRelativeTime(status.lastPolledAt, now) : '—'}</dd>
        </div>
        <div>
          <dt>Circuit breaker</dt>
          <dd>
            <span
              className="ingestion-panel__breaker-badge"
              data-state={status.circuitBreakerState.toLowerCase()}
            >
              {status.circuitBreakerState}
            </span>
          </dd>
        </div>
        <div>
          <dt>Total polls</dt>
          <dd>{status.totalPolls}</dd>
        </div>
      </dl>

      {status.lastErrorMessage && (
        <p className="ingestion-panel__error" role="alert">
          Last error: {status.lastErrorMessage}
        </p>
      )}

      <div className="ingestion-panel__failures">
        <h3>Failures by reason</h3>
        {failureEntries.length === 0 ? (
          <p className="ingestion-panel__empty">No failures recorded.</p>
        ) : (
          <ul>
            {failureEntries.map(([reason, count]) => (
              <li key={reason}>
                {describeFailureReason(reason)}: {count}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="ingestion-panel__actions">
        <button type="button" onClick={onTrigger} disabled={triggering}>
          {triggering ? 'Triggering…' : 'Trigger poll now'}
        </button>

        {lastTriggerResult && (
          <p className="ingestion-panel__trigger-result" role="status">
            Last manual trigger: {lastTriggerResult.outcome} · {lastTriggerResult.readingCount} readings ·{' '}
            {lastTriggerResult.durationMs}ms
          </p>
        )}
        {triggerError ? (
          <p className="ingestion-panel__error" role="alert">
            {triggerErrorMessage(triggerError)}
          </p>
        ) : null}
      </div>

      <form className="ingestion-panel__interval-form" onSubmit={handleIntervalSubmit}>
        <label className="ingestion-panel__interval-field">
          <span>Polling interval (seconds)</span>
          <input
            type="number"
            min={1}
            value={intervalInput}
            onChange={(event) => setIntervalInput(event.target.value)}
            aria-invalid={intervalError ? true : undefined}
            aria-describedby={intervalError ? 'ingestion-interval-error' : undefined}
          />
        </label>
        <button type="submit" disabled={updatingInterval}>
          {updatingInterval ? 'Saving…' : 'Save interval'}
        </button>
        {intervalError && (
          <p id="ingestion-interval-error" className="ingestion-panel__error" role="alert">
            {intervalError}
          </p>
        )}
      </form>
    </div>
  )
}
