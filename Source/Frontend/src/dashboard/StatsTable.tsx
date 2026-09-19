import { formatNumber, formatShare } from '../lib/format'

export interface StatRow {
  key: string
  label: string
  metricCode: string
  count: number
  trueCount: number
  trueShare: number
  min: number | null
  max: number | null
  average: number | null
}

interface StatsTableProps {
  dimensionLabel: string
  rows: readonly StatRow[]
}

export const StatsTable = ({ dimensionLabel, rows }: StatsTableProps) => {
  const showDetections = rows.some((row) => row.trueCount > 0)
  const comparable = rows.every((row) => row.metricCode === rows[0].metricCode)
  const peak = comparable && rows.length > 0 ? weight(rows[0]) : null

  return (
    <div className="tbl-scroll">
      <table>
        <thead>
          <tr>
            <th>{dimensionLabel}</th>
            <th>Metric</th>
            <th>Readings</th>
            <th>Min</th>
            <th>Max</th>
            <th>Average</th>
            {showDetections ? <th>Detections</th> : null}
            {showDetections ? <th>Share</th> : null}
            {comparable ? <th className="bar-cell" /> : null}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key}>
              <td>{row.label}</td>
              <td>{row.metricCode}</td>
              <td>{row.count}</td>
              <td>{formatNumber(row.min)}</td>
              <td>{formatNumber(row.max)}</td>
              <td>{formatNumber(row.average)}</td>
              {showDetections ? <td>{row.trueCount}</td> : null}
              {showDetections ? <td>{formatShare(row.trueShare)}</td> : null}
              {comparable ? (
                <td className="bar-cell">
                  <div className="bar-track">
                    <div className="bar-fill" style={{ width: `${share(weight(row), peak)}%` }} />
                  </div>
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const weight = (row: StatRow): number => row.average ?? row.trueShare

const share = (value: number, peak: number | null): number => {
  if (peak === null || peak <= 0) {
    return 0
  }

  return Math.max(2, Math.min(100, (value / peak) * 100))
}
