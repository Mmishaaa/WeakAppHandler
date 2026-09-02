import './skeleton.css'

export interface SkeletonProps {
  /** Number of placeholder lines to render. */
  lines?: number
  /** Accessible label announced while the skeleton is on screen. */
  label?: string
}

/** Generic loading placeholder for use as an {@link AsyncBoundary} `skeleton` prop. */
export function Skeleton({ lines = 3, label = 'Loading' }: SkeletonProps) {
  return (
    <div className="skeleton" role="status" aria-label={label}>
      {Array.from({ length: lines }, (_, index) => (
        <span key={index} className="skeleton__line" />
      ))}
    </div>
  )
}
