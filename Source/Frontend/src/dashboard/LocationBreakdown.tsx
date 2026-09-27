import { useQuery } from '@apollo/client/react'
import { LocationStatsDocument } from '../gql/graphql'
import { EmptyState, ErrorState, LoadingState } from '../components/PanelState'
import { useRefetchOn } from '../lib/useRefetchOn'
import type { WindowRange } from '../lib/windows'
import { StatsTable } from './StatsTable'

interface LocationBreakdownProps {
  metricCode: string | null
  range: WindowRange
  dataVersion: number
}

export const LocationBreakdown = ({
  metricCode,
  range,
  dataVersion,
}: LocationBreakdownProps) => {
  const { data, loading, error, refetch } = useQuery(LocationStatsDocument, {
    variables: { from: range.from, to: range.to, metricCode },
  })

  useRefetchOn(dataVersion, refetch)

  if (loading && data === undefined) {
    return <LoadingState rows={5} />
  }

  if (error !== undefined) {
    return <ErrorState message={error.message} onRetry={() => void refetch()} />
  }

  const rows = data?.locationStats ?? []

  if (rows.length === 0) {
    return (
      <EmptyState
        title="No readings"
        hint="Nothing was stored for this metric in the selected period."
      />
    )
  }

  return (
    <StatsTable
      dimensionLabel="Location"
      rows={rows.map((row) => ({
        key: `${row.location}-${row.metricCode}`,
        label: row.location,
        metricCode: row.metricCode,
        count: row.count,
        trueCount: row.trueCount,
        trueShare: row.trueShare,
        min: row.min,
        max: row.max,
        average: row.average,
      }))}
    />
  )
}
