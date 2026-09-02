import { OverviewContent } from '../../features/overview/OverviewContent'
import { useOverviewData } from '../../features/overview/useOverviewData'
import { useNow } from '../../shared/time/useNow'
import { AsyncBoundary } from '../../shared/ui/async-boundary/AsyncBoundary'
import { Skeleton } from '../../shared/ui/skeleton/Skeleton'

export function OverviewPage() {
  const { data, loading, error, refetch } = useOverviewData()
  const now = useNow()

  return (
    <>
      <h1>Overview</h1>
      <AsyncBoundary
        loading={loading}
        error={error}
        data={data}
        isEmpty={(overviewData) => overviewData.meters.length === 0}
        onRetry={() => void refetch()}
        skeleton={<Skeleton lines={6} label="Loading meters" />}
      >
        {(overviewData) => <OverviewContent data={overviewData} now={now} />}
      </AsyncBoundary>
    </>
  )
}
