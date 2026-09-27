import { useQuery } from '@apollo/client/react'
import { MetricSnapshotDocument } from '../gql/graphql'
import type { MetricState } from '../gql/graphql'
import { StatePill } from '../components/StatePill'
import { EmptyState, ErrorState, LoadingState } from '../components/PanelState'
import { formatTime, formatValue } from '../lib/format'
import { isPageHidden } from '../lib/usePageVisible'
import { useRefetchOn } from '../lib/useRefetchOn'

const tileClassNames: Record<MetricState, string> = {
  ABOVE: 'tile alarm',
  BELOW: 'tile warn',
  OK: 'tile',
  UNKNOWN: 'tile',
}

interface MetricTilesProps {
  dataVersion: number
}

export const MetricTiles = ({ dataVersion }: MetricTilesProps) => {
  const { data, loading, error, refetch } = useQuery(MetricSnapshotDocument, {
    pollInterval: 60_000,
    skipPollAttempt: isPageHidden,
  })

  useRefetchOn(dataVersion, refetch)

  if (loading && data === undefined) {
    return (
      <div className="tiles">
        {Array.from({ length: 5 }, (_, index) => (
          <div className="tile" key={index}>
            <LoadingState rows={3} />
          </div>
        ))}
      </div>
    )
  }

  if (error !== undefined) {
    return (
      <div className="panel">
        <ErrorState message={error.message} onRetry={() => void refetch()} />
      </div>
    )
  }

  const metrics = data?.metricSnapshot ?? []

  if (metrics.length === 0) {
    return (
      <div className="panel">
        <EmptyState
          title="No readings yet"
          hint="Nothing has reached the database, so there is no metric to show."
        />
      </div>
    )
  }

  return (
    <div className="tiles">
      {metrics.map((metric) => (
        <article
          key={metric.metricCode}
          className={tileClassNames[metric.state]}
        >
          <div className="tile-top">
            <span className="tile-metric">{metric.metricCode}</span>
            <StatePill state={metric.state} threshold={metric.threshold} />
          </div>
          <div className="tile-value">
            {formatValue(metric.numeric, metric.flag)}
            {metric.unit === '' ? null : <span className="unit">{metric.unit}</span>}
          </div>
          <div className="tile-foot">
            <span className="where">
              {metric.location}
              {metric.locationCount > 1 ? ` +${metric.locationCount - 1}` : ''}
            </span>
            <time dateTime={metric.observedAt}>{formatTime(metric.observedAt)}</time>
          </div>
        </article>
      ))}
    </div>
  )
}
