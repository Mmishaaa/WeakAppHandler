import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { IngestionPanel } from './IngestionPanel'
import type { IngestionStatus } from './ingestionApi'

const NOW = new Date('2026-09-04T12:00:00.000Z')

function buildStatus(overrides: Partial<IngestionStatus> = {}): IngestionStatus {
  return {
    lastOutcome: 'Success',
    lastPolledAt: '2026-09-04T11:59:30.000Z',
    lastSuccessAt: '2026-09-04T11:59:30.000Z',
    lastBatchId: 'batch-1',
    lastReadingCount: 18,
    lastHttpStatus: 200,
    lastDurationMs: 120,
    lastErrorMessage: null,
    totalPolls: 42,
    failureCountsByReason: {},
    circuitBreakerState: 'Closed',
    pollingIntervalSeconds: 10,
    ...overrides,
  }
}

describe('IngestionPanel', () => {
  it('shows the last outcome, circuit breaker state and total polls', () => {
    render(
      <IngestionPanel
        status={buildStatus()}
        now={NOW}
        onTrigger={vi.fn()}
        triggering={false}
        onIntervalChange={vi.fn()}
        updatingInterval={false}
      />,
    )

    expect(screen.getByText('Success')).toBeInTheDocument()
    expect(screen.getByText('Closed')).toBeInTheDocument()
    expect(screen.getByText('42')).toBeInTheDocument()
  })

  it('lists failure counts by reason', () => {
    render(
      <IngestionPanel
        status={buildStatus({ failureCountsByReason: { HttpError: 3, Corrupted: 1 } })}
        now={NOW}
        onTrigger={vi.fn()}
        triggering={false}
        onIntervalChange={vi.fn()}
        updatingInterval={false}
      />,
    )

    expect(screen.getByText('HTTP error: 3')).toBeInTheDocument()
    expect(screen.getByText('Corrupted response: 1')).toBeInTheDocument()
  })

  it('calls onTrigger when the trigger button is clicked and shows the outcome once it resolves', async () => {
    const user = userEvent.setup()
    const onTrigger = vi.fn()
    const { rerender } = render(
      <IngestionPanel
        status={buildStatus()}
        now={NOW}
        onTrigger={onTrigger}
        triggering={false}
        onIntervalChange={vi.fn()}
        updatingInterval={false}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Trigger poll now' }))
    expect(onTrigger).toHaveBeenCalledTimes(1)

    rerender(
      <IngestionPanel
        status={buildStatus()}
        now={NOW}
        onTrigger={onTrigger}
        triggering={false}
        lastTriggerResult={{
          batchId: 'batch-2',
          outcome: 'Success',
          readingCount: 18,
          httpStatus: 200,
          durationMs: 95,
          errorMessage: null,
          fetchedAt: '2026-09-04T12:00:05.000Z',
        }}
        onIntervalChange={vi.fn()}
        updatingInterval={false}
      />,
    )

    expect(screen.getByText(/Last manual trigger: Success · 18 readings · 95ms/)).toBeInTheDocument()
  })

  it('calls onIntervalChange with the new value when the interval form is submitted', async () => {
    const user = userEvent.setup()
    const onIntervalChange = vi.fn()
    render(
      <IngestionPanel
        status={buildStatus({ pollingIntervalSeconds: 10 })}
        now={NOW}
        onTrigger={vi.fn()}
        triggering={false}
        onIntervalChange={onIntervalChange}
        updatingInterval={false}
      />,
    )

    const input = screen.getByLabelText('Polling interval (seconds)')
    await user.clear(input)
    await user.type(input, '30')
    await user.click(screen.getByRole('button', { name: 'Save interval' }))

    expect(onIntervalChange).toHaveBeenCalledWith(30)
  })

  it('shows the interval error message when one is provided', () => {
    render(
      <IngestionPanel
        status={buildStatus()}
        now={NOW}
        onTrigger={vi.fn()}
        triggering={false}
        onIntervalChange={vi.fn()}
        updatingInterval={false}
        intervalError="Polling interval must be greater than 5s."
      />,
    )

    expect(screen.getByText('Polling interval must be greater than 5s.')).toBeInTheDocument()
  })
})
