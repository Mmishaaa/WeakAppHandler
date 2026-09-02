import type { ReactNode } from 'react'
import './async-boundary.css'

export interface AsyncBoundaryStaleness {
  /** True when previously-loaded data is being shown while a refetch or an error is in flight. */
  isStale: boolean
}

export interface AsyncBoundaryProps<T> {
  /** True while a fetch (initial or refetch) is in flight. */
  loading: boolean
  /** The error from the most recent fetch attempt, if any. */
  error?: unknown
  /** The most recently successful data, if any. Kept across errors/refetches by the caller. */
  data: T | undefined
  /** Returns true when `data` is present but represents "nothing to show" (e.g. an empty array). */
  isEmpty?: (data: T) => boolean
  /** Invoked when the user asks to retry from the error state or the stale-data banner. */
  onRetry?: () => void
  /** Rendered only while there is no data yet to show (first load). Must be a skeleton, not a spinner overlay. */
  skeleton: ReactNode
  /** Rendered when there is no error/loading and the data is absent or empty. */
  emptyState?: ReactNode
  /** Rendered when there is no data at all and the last attempt failed. Defaults to a retry panel. */
  renderError?: (error: unknown, retry: (() => void) | undefined) => ReactNode
  /** Rendered once real data is available, even if stale. */
  children: (data: T, staleness: AsyncBoundaryStaleness) => ReactNode
}

function DefaultEmptyState() {
  return <p className="async-boundary__empty">No data to show.</p>
}

function DefaultErrorState({ error, onRetry }: { error: unknown; onRetry: (() => void) | undefined }) {
  const message = error instanceof Error ? error.message : 'Something went wrong.'
  return (
    <div className="async-boundary__error" role="alert">
      <p>{message}</p>
      {onRetry && (
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  )
}

function StalenessBanner({
  reason,
  onRetry,
}: {
  reason: 'error' | 'refreshing'
  onRetry: (() => void) | undefined
}) {
  return (
    <div className="async-boundary__stale-banner" role="status">
      <span>
        {reason === 'error'
          ? 'Showing last known data - the latest update failed.'
          : 'Refreshing…'}
      </span>
      {reason === 'error' && onRetry && (
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  )
}

/**
 * Universal four-state async wrapper: loading (skeleton) / empty / error+retry / loaded.
 * When an error or a background refetch happens while good data already exists, the last good
 * data stays on screen behind a staleness banner instead of the screen going blank - this is the
 * one behaviour that a naive `if (loading) ... else if (error) ... else ...` chain gets wrong.
 */
export function AsyncBoundary<T>({
  loading,
  error,
  data,
  isEmpty,
  onRetry,
  skeleton,
  emptyState,
  renderError,
  children,
}: AsyncBoundaryProps<T>) {
  const hasData = data !== undefined && !(isEmpty?.(data) ?? false)

  if (!hasData && error) {
    return <>{renderError ? renderError(error, onRetry) : <DefaultErrorState error={error} onRetry={onRetry} />}</>
  }

  if (!hasData && loading) {
    return <>{skeleton}</>
  }

  // Re-checking `data === undefined` here (rather than just `!hasData`) gives TypeScript a real
  // narrowing point: control flow analysis tracks the negation of this specific comparison, so
  // `data` is known non-undefined below even though `hasData` alone couldn't prove that.
  if (!hasData || data === undefined) {
    return <>{emptyState ?? <DefaultEmptyState />}</>
  }

  const isStale = Boolean(error) || loading

  return (
    <div className="async-boundary">
      {isStale && <StalenessBanner reason={error ? 'error' : 'refreshing'} onRetry={error ? onRetry : undefined} />}
      {children(data, { isStale })}
    </div>
  )
}
