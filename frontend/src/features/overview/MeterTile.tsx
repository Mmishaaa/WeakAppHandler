import { getMetricDisplayInfo, formatMetricValue } from '../../entities/meter/metricDisplay'
import { formatRelativeTime } from '../../shared/time/relativeTime'
import { SeverityIndicator } from '../../shared/ui/severity/SeverityIndicator'
import type { MeterTileSeverity } from './overviewSeverity'
import './meter-tile.css'

export interface MeterTileMetric {
  metricCode: string
  valueNumeric?: number | string | null
  valueBool?: boolean | null
  observedAt: string
}

export interface MeterTileProps {
  meterType: string
  metrics: readonly MeterTileMetric[]
  severity?: MeterTileSeverity
  now: Date
}

export function MeterTile({ meterType, metrics, severity, now }: MeterTileProps) {
  return (
    <li className="meter-tile" data-severity={severity?.severity ?? 'normal'}>
      <div className="meter-tile__header">
        <h3 className="meter-tile__title">{meterType}</h3>
        {severity && <SeverityIndicator severity={severity.severity} label={severity.label} />}
      </div>
      <ul className="meter-tile__metrics">
        {metrics.map((metric) => (
          <li key={metric.metricCode} className="meter-tile__metric">
            <span className="meter-tile__metric-name">{getMetricDisplayInfo(metric.metricCode).displayName}</span>
            <span className="meter-tile__metric-value">
              {formatMetricValue(metric.metricCode, metric.valueNumeric, metric.valueBool)}
            </span>
            <span className="meter-tile__metric-time">{formatRelativeTime(metric.observedAt, now)}</span>
          </li>
        ))}
      </ul>
    </li>
  )
}
