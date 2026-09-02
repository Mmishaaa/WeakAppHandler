import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AsyncBoundary } from './AsyncBoundary'

describe('AsyncBoundary', () => {
  it('renders the skeleton while loading and no data has ever arrived', () => {
    render(
      <AsyncBoundary loading error={undefined} data={undefined} skeleton={<div data-testid="skeleton">Loading…</div>}>
        {() => <div>content</div>}
      </AsyncBoundary>,
    )

    expect(screen.getByTestId('skeleton')).toBeInTheDocument()
    expect(screen.queryByText('content')).not.toBeInTheDocument()
  })

  it('renders an empty state when data is present but represents "nothing to show"', () => {
    render(
      <AsyncBoundary
        loading={false}
        data={[] as string[]}
        isEmpty={(items) => items.length === 0}
        skeleton={<div>skeleton</div>}
        emptyState={<div data-testid="empty">Nothing here yet</div>}
      >
        {() => <div>content</div>}
      </AsyncBoundary>,
    )

    expect(screen.getByTestId('empty')).toBeInTheDocument()
    expect(screen.queryByText('content')).not.toBeInTheDocument()
  })

  it('renders an error state with a working retry action when there is no data to fall back on', async () => {
    const user = userEvent.setup()
    const onRetry = vi.fn()

    render(
      <AsyncBoundary
        loading={false}
        error={new Error('network down')}
        data={undefined}
        onRetry={onRetry}
        skeleton={<div>skeleton</div>}
      >
        {() => <div>content</div>}
      </AsyncBoundary>,
    )

    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('network down')
    expect(screen.queryByText('content')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /retry/i }))
    expect(onRetry).toHaveBeenCalledOnce()
  })

  it('renders the loaded content with no staleness banner when everything is fresh', () => {
    render(
      <AsyncBoundary loading={false} data="hello" skeleton={<div>skeleton</div>}>
        {(data, { isStale }) => (
          <div data-testid="loaded">
            {data} / stale:{String(isStale)}
          </div>
        )}
      </AsyncBoundary>,
    )

    expect(screen.getByTestId('loaded')).toHaveTextContent('hello / stale:false')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('keeps the last good data on screen with a staleness banner when a refetch errors', () => {
    render(
      <AsyncBoundary loading={false} error={new Error('refresh failed')} data="stale-but-good" skeleton={<div>skeleton</div>}>
        {(data, { isStale }) => (
          <div data-testid="loaded">
            {data} / stale:{String(isStale)}
          </div>
        )}
      </AsyncBoundary>,
    )

    expect(screen.getByTestId('loaded')).toHaveTextContent('stale-but-good / stale:true')
    expect(screen.getByRole('status')).toHaveTextContent(/last known data/i)
  })

  it('shows a refreshing indicator (not the skeleton) when loading again with existing data', () => {
    render(
      <AsyncBoundary loading data="already-have-this" skeleton={<div data-testid="skeleton">skeleton</div>}>
        {(data) => <div data-testid="loaded">{data}</div>}
      </AsyncBoundary>,
    )

    expect(screen.getByTestId('loaded')).toHaveTextContent('already-have-this')
    expect(screen.queryByTestId('skeleton')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(/refreshing/i)
  })
})
