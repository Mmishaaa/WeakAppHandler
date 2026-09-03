import { useState } from 'react'
import { HistoryContent } from '../../features/history/HistoryContent'
import { HistoryFilterBar } from '../../features/history/HistoryFilterBar'
import { DEFAULT_HISTORY_FILTERS } from '../../features/history/historyFilters'
import { useHistoryData } from '../../features/history/useHistoryData'
import { AsyncBoundary } from '../../shared/ui/async-boundary/AsyncBoundary'
import { Skeleton } from '../../shared/ui/skeleton/Skeleton'

export function HistoryPage() {
  const [filters, setFilters] = useState(DEFAULT_HISTORY_FILTERS)
  const { data, loading, error, refetch, locations, effectiveLocation } = useHistoryData(filters)

  return (
    <>
      <h1>History</h1>
      <HistoryFilterBar
        filters={{ ...filters, location: effectiveLocation ?? '' }}
        locations={locations}
        onFiltersChange={setFilters}
      />
      <AsyncBoundary
        loading={loading}
        error={error}
        data={data}
        onRetry={() => void refetch()}
        skeleton={<Skeleton lines={6} label="Loading history" />}
      >
        {(historyData) => <HistoryContent data={historyData} metricCode={filters.metricCode} period={filters.period} />}
      </AsyncBoundary>
    </>
  )
}
