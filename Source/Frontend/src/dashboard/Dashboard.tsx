import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@apollo/client/react'
import { FilterOptionsDocument } from '../gql/graphql'
import type { ReadingFilterModelInput } from '../gql/graphql'
import { useLiveReadings } from '../api/useLiveReadings'
import { useThrottledValue } from '../lib/useThrottledValue'
import { findWindow, resolveWindow } from '../lib/windows'
import { AppBar } from './AppBar'
import { MetricTiles } from './MetricTiles'
import { SeriesChart } from './SeriesChart'
import { LiveFeed } from './LiveFeed'
import { LocationTable } from './LocationTable'
import { SubmitForm } from './SubmitForm'
import { ReadingsTable } from './ReadingsTable'
import { initialFilters } from './filters'
import { ErrorState } from '../components/PanelState'

const rangeRefreshMs = 60_000

export const Dashboard = () => {
  const [filters, setFilters] = useState(initialFilters)
  const [manualVersion, setManualVersion] = useState(0)
  const [rangeAnchor, setRangeAnchor] = useState(() => Date.now())

  const optionsQuery = useQuery(FilterOptionsDocument, { pollInterval: 300_000 })
  const options = optionsQuery.data?.filterOptions ?? null

  useEffect(() => {
    const timer = window.setInterval(() => setRangeAnchor(Date.now()), rangeRefreshMs)

    return () => window.clearInterval(timer)
  }, [])

  const metricCode = filters.metricCode ?? options?.metricCodes[0] ?? null
  const activeFilters = { ...filters, metricCode }

  const live = useLiveReadings({
    location: filters.location,
    metricCode,
  })

  const liveVersion = useThrottledValue(live.revision, 5_000)
  const dataVersion = liveVersion + manualVersion

  const window_ = findWindow(filters.windowKey)
  const range = useMemo(
    () => resolveWindow(window_, rangeAnchor),
    [window_, rangeAnchor],
  )

  const readingsFilter = useMemo<ReadingFilterModelInput>(
    () => ({
      location: filters.location,
      metricCode,
      from: range.from,
      to: range.to,
    }),
    [filters.location, metricCode, range.from, range.to],
  )

  return (
    <>
      <AppBar
        filters={activeFilters}
        locations={options?.locations ?? []}
        metricCodes={options?.metricCodes ?? []}
        status={live.status}
        group={live.group}
        onChange={setFilters}
      />

      <main className="canvas">
        {optionsQuery.error === undefined ? null : (
          <div className="panel">
            <ErrorState
              message={optionsQuery.error.message}
              onRetry={() => void optionsQuery.refetch()}
            />
          </div>
        )}

        <MetricTiles dataVersion={dataVersion} />

        <div className="split">
          <SeriesChart
            metricCode={metricCode}
            location={filters.location}
            range={range}
            windowLabel={window_.label}
            dataVersion={dataVersion}
          />
          <LiveFeed
            events={live.events}
            status={live.status}
            group={live.group}
            onClear={live.clear}
          />
        </div>

        <div className="split">
          <LocationTable
            metricCode={metricCode}
            range={range}
            windowLabel={window_.label}
            dataVersion={dataVersion}
          />
          <SubmitForm
            locations={options?.locations ?? []}
            meterTypes={options?.meterTypes ?? []}
            metricCodes={options?.metricCodes ?? []}
            onSubmitted={() => setManualVersion((current) => current + 1)}
          />
        </div>

        <ReadingsTable filter={readingsFilter} dataVersion={dataVersion} />
      </main>
    </>
  )
}
