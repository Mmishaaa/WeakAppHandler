import { useQuery } from '@apollo/client/react'
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { ReadingSeriesDocument } from '../gql/graphql'
import { Panel } from '../components/Panel'
import { EmptyState, ErrorState, LoadingState } from '../components/PanelState'
import { formatBucket, formatNumber } from '../lib/format'
import { seriesColor } from '../lib/series'
import { FlagStrip } from './FlagStrip'
import { useRefetchOn } from '../lib/useRefetchOn'
import type { WindowRange } from '../lib/windows'

interface SeriesChartProps {
  metricCode: string | null
  location: string | null
  range: WindowRange
  windowLabel: string
  dataVersion: number
}

export const SeriesChart = ({
  metricCode,
  location,
  range,
  windowLabel,
  dataVersion,
}: SeriesChartProps) => {
  const { data, loading, error, refetch } = useQuery(ReadingSeriesDocument, {
    variables: {
      input: {
        metricCode: metricCode ?? '',
        bucket: range.bucket,
        from: range.from,
        to: range.to,
      },
      locations: location === null ? null : [location],
    },
    skip: metricCode === null,
  })

  useRefetchOn(dataVersion, () => void refetch())

  const title = metricCode === null ? 'Readings over time' : `${metricCode} over time`
  const series = data?.readingSeries ?? []
  const flagMode =
    series.some((entry) => entry.buckets.length > 0) &&
    series.every((entry) => entry.buckets.every((bucket) => bucket.average === null))
  const subtitle = flagMode
    ? `share of readings detecting · ${windowLabel.toLowerCase()}`
    : windowLabel.toLowerCase()

  return (
    <Panel title={title} sub={subtitle}>
      {renderBody()}
    </Panel>
  )

  function renderBody() {
    if (metricCode === null) {
      return <EmptyState title="Pick a metric" hint="Choose one in the toolbar above." />
    }

    if (loading && data === undefined) {
      return <LoadingState block rows={2} />
    }

    if (error !== undefined) {
      return <ErrorState message={error.message} onRetry={() => void refetch()} />
    }

    if (series.length === 0) {
      return (
        <EmptyState
          title="Nothing in this window"
          hint="No reading for this metric in the selected period."
        />
      )
    }

    if (flagMode) {
      return <FlagStrip series={series} bucket={range.bucket} />
    }

    return (
      <>
        <div style={{ width: '100%', height: 260 }}>
          <ResponsiveContainer>
            <LineChart margin={{ top: 8, right: 12, bottom: 4, left: 0 }}>
              <CartesianGrid stroke="var(--rule)" vertical={false} />
              <XAxis
                dataKey="bucketStart"
                allowDuplicatedCategory={false}
                tickFormatter={(value: string) => formatBucket(value, range.bucket)}
                stroke="var(--axis)"
                tick={{ fill: 'var(--ink-3)', fontSize: 11 }}
                minTickGap={24}
              />
              <YAxis
                stroke="var(--axis)"
                tick={{ fill: 'var(--ink-3)', fontSize: 11 }}
                width={52}
              />
              <Tooltip
                contentStyle={{
                  background: 'var(--surface)',
                  border: '1px solid var(--ring)',
                  borderRadius: 6,
                  color: 'var(--ink)',
                  fontSize: 12,
                }}
                labelFormatter={(value) => formatBucket(String(value), range.bucket)}
                formatter={(value) => formatNumber(typeof value === 'number' ? value : null)}
              />
              {series.map((entry, index) => (
                <Line
                  key={entry.location}
                  type="monotone"
                  data={[...entry.buckets]}
                  dataKey="average"
                  name={entry.location}
                  stroke={seriesColor(index)}
                  strokeWidth={2}
                  dot={false}
                  isAnimationActive={false}
                />
              ))}
            </LineChart>
          </ResponsiveContainer>
        </div>
        <div className="legend">
          {series.map((entry, index) => (
            <b key={entry.location}>
              <span
                className="swatch"
                style={{ background: seriesColor(index) }}
                aria-hidden="true"
              />
              {entry.location}
            </b>
          ))}
        </div>
      </>
    )
  }
}

