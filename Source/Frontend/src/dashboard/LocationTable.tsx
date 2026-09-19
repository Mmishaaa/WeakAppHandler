import { useQuery } from '@apollo/client/react'
import { LocationStatsDocument } from '../gql/graphql'
import { Panel } from '../components/Panel'
import { EmptyState, ErrorState, LoadingState } from '../components/PanelState'
import { formatNumber } from '../lib/format'
import { useRefetchOn } from '../lib/useRefetchOn'
import type { WindowRange } from '../lib/windows'

interface LocationTableProps {
  metricCode: string | null
  range: WindowRange
  windowLabel: string
  dataVersion: number
}

export const LocationTable = ({
  metricCode,
  range,
  windowLabel,
  dataVersion,
}: LocationTableProps) => {
  const { data, loading, error, refetch } = useQuery(LocationStatsDocument, {
    variables: { from: range.from, to: range.to, metricCode },
  })

  useRefetchOn(dataVersion, () => void refetch())

  const rows = data?.locationStats ?? []
  const peak = rows.length === 0 ? null : rows[0].average

  return (
    <Panel title="By location" sub={windowLabel.toLowerCase()}>
      {loading && data === undefined ? (
        <LoadingState rows={5} />
      ) : error !== undefined ? (
        <ErrorState message={error.message} onRetry={() => void refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          title="No numeric readings"
          hint="Nothing in this window has a numeric value to average."
        />
      ) : (
        <div className="tbl-scroll">
          <table>
            <thead>
              <tr>
                <th>Location</th>
                <th>Metric</th>
                <th>Readings</th>
                <th>Min</th>
                <th>Max</th>
                <th>Average</th>
                <th className="bar-cell" />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${row.location}-${row.metricCode}`}>
                  <td>{row.location}</td>
                  <td>{row.metricCode}</td>
                  <td>{row.count}</td>
                  <td>{formatNumber(row.min)}</td>
                  <td>{formatNumber(row.max)}</td>
                  <td>{formatNumber(row.average)}</td>
                  <td className="bar-cell">
                    <div className="bar-track">
                      <div
                        className="bar-fill"
                        style={{ width: `${share(row.average, peak)}%` }}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Panel>
  )
}

const share = (value: number | null, peak: number | null): number => {
  if (value === null || peak === null || peak <= 0) {
    return 0
  }

  return Math.max(2, Math.min(100, (value / peak) * 100))
}
