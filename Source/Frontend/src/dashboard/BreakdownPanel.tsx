import { useState } from 'react'
import type { WindowRange } from '../lib/windows'
import { LocationBreakdown } from './LocationBreakdown'
import { MeterTypeBreakdown } from './MeterTypeBreakdown'

type Dimension = 'location' | 'meterType'

const dimensions: readonly { key: Dimension; label: string }[] = [
  { key: 'location', label: 'By location' },
  { key: 'meterType', label: 'By type' },
]

interface BreakdownPanelProps {
  metricCode: string | null
  range: WindowRange
  windowLabel: string
  dataVersion: number
}

export const BreakdownPanel = ({
  metricCode,
  range,
  windowLabel,
  dataVersion,
}: BreakdownPanelProps) => {
  const [dimension, setDimension] = useState<Dimension>('location')

  return (
    <section className="panel">
      <div className="panel-head">
        <h3>Breakdown</h3>
        <div className="seg" role="group" aria-label="Group readings by">
          {dimensions.map((entry) => (
            <button
              key={entry.key}
              type="button"
              className={entry.key === dimension ? 'seg-item active' : 'seg-item'}
              aria-pressed={entry.key === dimension}
              onClick={() => setDimension(entry.key)}
            >
              {entry.label}
            </button>
          ))}
        </div>
        <span className="sub">
          {dimension === 'meterType'
            ? `all metrics · ${windowLabel.toLowerCase()}`
            : windowLabel.toLowerCase()}
        </span>
      </div>

      {dimension === 'location' ? (
        <LocationBreakdown
          metricCode={metricCode}
          range={range}
          dataVersion={dataVersion}
        />
      ) : (
        <MeterTypeBreakdown range={range} dataVersion={dataVersion} />
      )}
    </section>
  )
}
