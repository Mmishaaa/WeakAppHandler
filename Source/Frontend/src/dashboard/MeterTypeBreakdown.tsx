import { useQuery } from '@apollo/client/react'
import { MeterTypeStatsDocument } from '../gql/graphql'
import { EmptyState, ErrorState, LoadingState } from '../components/PanelState'
import { useRefetchOn } from '../lib/useRefetchOn'
import type { WindowRange } from '../lib/windows'
import { StatsTable } from './StatsTable'

interface MeterTypeBreakdownProps {
  range: WindowRange
  dataVersion: number
}

export const MeterTypeBreakdown = ({ range, dataVersion }: MeterTypeBreakdownProps) => {
  const { data, loading, error, refetch } = useQuery(MeterTypeStatsDocument, {
    variables: { from: range.from, to: range.to, metricCode: null },
  })

  useRefetchOn(dataVersion, () => void refetch())

  if (loading && data === undefined) {
    return <LoadingState rows={5} />
  }

  if (error !== undefined) {
    return <ErrorState message={error.message} onRetry={() => void refetch()} />
  }

  const rows = data?.meterTypeStats ?? []

  if (rows.length === 0) {
    return (
      <EmptyState
        title="No readings"
        hint="Nothing was stored in the selected period."
      />
    )
  }

  return (
    <StatsTable
      dimensionLabel="Meter type"
      rows={rows.map((row) => ({
        key: `${row.meterType}-${row.metricCode}`,
        label: row.meterType,
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
