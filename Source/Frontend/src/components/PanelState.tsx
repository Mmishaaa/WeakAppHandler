interface LoadingProps {
  rows?: number
  block?: boolean
}

export const LoadingState = ({ rows = 4, block = false }: LoadingProps) => (
  <div style={{ display: 'grid', gap: 8 }} aria-busy="true" aria-label="Loading">
    {block ? <div className="skeleton sk-block" /> : null}
    {Array.from({ length: rows }, (_, index) => (
      <div
        key={index}
        className="skeleton sk-line"
        style={{ width: `${100 - index * 12}%` }}
      />
    ))}
  </div>
)

interface ErrorProps {
  message: string
  onRetry?: () => void
}

export const ErrorState = ({ message, onRetry }: ErrorProps) => (
  <div className="state-msg" role="alert">
    <h4>Could not load</h4>
    <p>{message}</p>
    {onRetry === undefined ? null : (
      <button type="button" className="btn ghost" onClick={onRetry}>
        Try again
      </button>
    )}
  </div>
)

interface EmptyProps {
  title: string
  hint: string
}

export const EmptyState = ({ title, hint }: EmptyProps) => (
  <div className="state-msg">
    <h4>{title}</h4>
    <p>{hint}</p>
  </div>
)
