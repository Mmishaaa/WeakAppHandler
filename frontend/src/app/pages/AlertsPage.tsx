import { useState } from 'react'
import { AlertsContent } from '../../features/alerts/AlertsContent'
import { DEFAULT_ALERT_FILTERS } from '../../features/alerts/alertFilters'
import { useAlertsData } from '../../features/alerts/useAlertsData'
import { useNow } from '../../shared/time/useNow'
import { AsyncBoundary } from '../../shared/ui/async-boundary/AsyncBoundary'
import { Skeleton } from '../../shared/ui/skeleton/Skeleton'

export function AlertsPage() {
  const { data, loading, error, refetch, highlightedIds } = useAlertsData()
  const now = useNow()
  const [filters, setFilters] = useState(DEFAULT_ALERT_FILTERS)

  return (
    <>
      <h1>Alerts</h1>
      <AsyncBoundary
        loading={loading}
        error={error}
        data={data}
        isEmpty={(alertsData) => (alertsData.alerts?.nodes?.length ?? 0) === 0}
        onRetry={() => void refetch()}
        skeleton={<Skeleton lines={6} label="Loading alerts" />}
      >
        {(alertsData) => (
          <AlertsContent
            data={{ alerts: alertsData.alerts?.nodes ?? [] }}
            now={now}
            filters={filters}
            onFiltersChange={setFilters}
            highlightedIds={highlightedIds}
          />
        )}
      </AsyncBoundary>
    </>
  )
}
