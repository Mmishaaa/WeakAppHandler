import { formatMetricValue, getMetricDisplayInfo } from '../../../entities/meter/metricDisplay'
import { computeNiceTicks, defaultBucketLabel, pickTickIndices, toNumber } from './chartLayout'
import './range-line-chart.css'

export interface RangeChartBucket {
  bucketStart: string
  avg: number | string | null
  min: number | string | null
  max: number | string | null
}

export interface RangeLineChartProps {
  /** Selects display name/unit via entities/meter/metricDisplay (e.g. 'co2', 'pm25', 'humidity'). */
  metricCode: string
  buckets: readonly RangeChartBucket[]
  /** Overrides the default "HH:mm" x-axis label, e.g. for day/week-granularity buckets. */
  formatBucketLabel?: (isoDate: string) => string
  maxTicks?: number
}

const WIDTH = 360
const HEIGHT = 200
const PADDING = { top: 16, right: 12, bottom: 24, left: 44 }
const PLOT_WIDTH = WIDTH - PADDING.left - PADDING.right
const PLOT_HEIGHT = HEIGHT - PADDING.top - PADDING.bottom

interface ChartPoint {
  index: number
  bucketStart: string
  avg: number | null
  top: number | null
  bottom: number | null
}

export function RangeLineChart({ metricCode, buckets, formatBucketLabel = defaultBucketLabel, maxTicks = 6 }: RangeLineChartProps) {
  const info = getMetricDisplayInfo(metricCode)
  const caption = info.unit ? `${info.displayName} (${info.unit})` : info.displayName

  const points: ChartPoint[] = buckets.map((bucket, index) => {
    const avg = toNumber(bucket.avg)
    const max = toNumber(bucket.max)
    const min = toNumber(bucket.min)
    return {
      index,
      bucketStart: bucket.bucketStart,
      avg,
      top: avg === null ? null : (max ?? avg),
      bottom: avg === null ? null : (min ?? avg),
    }
  })

  const definedPoints = points.filter((point): point is ChartPoint & { avg: number; top: number; bottom: number } => point.avg !== null)

  if (points.length === 0 || definedPoints.length === 0) {
    return (
      <figure className="range-line-chart range-line-chart--empty">
        <figcaption className="range-line-chart__caption">{caption}</figcaption>
        <p className="range-line-chart__empty-message">No data for this period</p>
      </figure>
    )
  }

  const yMin = Math.min(...definedPoints.map((point) => point.bottom))
  const yMax = Math.max(...definedPoints.map((point) => point.top))
  const yTicks = computeNiceTicks(yMin, yMax, 4)
  const domainMin = yTicks[0]
  const domainMax = yTicks[yTicks.length - 1]
  const domainSpan = domainMax - domainMin || 1

  const xScale = (index: number) => PADDING.left + (points.length === 1 ? PLOT_WIDTH / 2 : (index / (points.length - 1)) * PLOT_WIDTH)
  const yScale = (value: number) => PADDING.top + (1 - (value - domainMin) / domainSpan) * PLOT_HEIGHT

  const segments = splitIntoDefinedSegments(points)

  const tickIndices = pickTickIndices(points.length, maxTicks)
  const lastPoint = definedPoints[definedPoints.length - 1]
  const avgValues = definedPoints.map((point) => point.avg)
  const summary = `${caption}: averaged ${formatMetricValue(metricCode, Math.min(...avgValues), null)} to ${formatMetricValue(
    metricCode,
    Math.max(...avgValues),
    null,
  )} across ${points.length} interval${points.length === 1 ? '' : 's'}`

  return (
    <figure className="range-line-chart">
      <figcaption className="range-line-chart__caption">{caption}</figcaption>
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="range-line-chart__svg" role="img" aria-label={summary}>
        {yTicks.map((tick) => (
          <g key={tick}>
            <line
              className="range-line-chart__grid-line"
              x1={PADDING.left}
              x2={WIDTH - PADDING.right}
              y1={yScale(tick)}
              y2={yScale(tick)}
            />
            <text className="range-line-chart__axis-label" x={PADDING.left - 6} y={yScale(tick)} textAnchor="end" dominantBaseline="middle">
              {formatAxisNumber(tick)}
            </text>
          </g>
        ))}

        {segments.map((segment) => (
          <g key={segment[0].index}>
            <path className="range-line-chart__band" d={buildBandPath(segment, xScale, yScale)} />
            <path className="range-line-chart__line" d={buildLinePath(segment, xScale, yScale)} />
          </g>
        ))}

        <circle className="range-line-chart__end-dot" cx={xScale(lastPoint.index)} cy={yScale(lastPoint.avg)} r={3.5} />
        <text
          className="range-line-chart__value-label"
          x={xScale(lastPoint.index)}
          y={yScale(lastPoint.avg) - 8}
          textAnchor={lastPoint.index === points.length - 1 ? 'end' : 'middle'}
        >
          {formatMetricValue(metricCode, lastPoint.avg, null)}
        </text>

        {tickIndices.map((index) => (
          <text key={index} className="range-line-chart__axis-label" x={xScale(index)} y={HEIGHT - 6} textAnchor="middle">
            {formatBucketLabel(points[index].bucketStart)}
          </text>
        ))}
      </svg>
    </figure>
  )
}

function splitIntoDefinedSegments(
  points: readonly ChartPoint[],
): ReadonlyArray<ReadonlyArray<ChartPoint & { avg: number; top: number; bottom: number }>> {
  const segments: Array<Array<ChartPoint & { avg: number; top: number; bottom: number }>> = []
  let current: Array<ChartPoint & { avg: number; top: number; bottom: number }> = []

  for (const point of points) {
    if (point.avg === null || point.top === null || point.bottom === null) {
      if (current.length > 0) {
        segments.push(current)
        current = []
      }
      continue
    }
    current.push({ ...point, avg: point.avg, top: point.top, bottom: point.bottom })
  }
  if (current.length > 0) {
    segments.push(current)
  }
  return segments
}

function buildLinePath(
  segment: ReadonlyArray<{ index: number; avg: number }>,
  xScale: (index: number) => number,
  yScale: (value: number) => number,
): string {
  return segment.map((point, i) => `${i === 0 ? 'M' : 'L'} ${xScale(point.index)},${yScale(point.avg)}`).join(' ')
}

function buildBandPath(
  segment: ReadonlyArray<{ index: number; top: number; bottom: number }>,
  xScale: (index: number) => number,
  yScale: (value: number) => number,
): string {
  const forward = segment.map((point, i) => `${i === 0 ? 'M' : 'L'} ${xScale(point.index)},${yScale(point.top)}`)
  const backward = [...segment].reverse().map((point) => `L ${xScale(point.index)},${yScale(point.bottom)}`)
  return [...forward, ...backward, 'Z'].join(' ')
}

function formatAxisNumber(value: number): string {
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value)
}
