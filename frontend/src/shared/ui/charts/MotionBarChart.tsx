import { computeNiceTicks, defaultBucketLabel, pickTickIndices, roundedTopRectPath } from './chartLayout'
import './motion-bar-chart.css'

export interface MotionChartBucket {
  bucketStart: string
  count: number
}

export interface MotionBarChartProps {
  buckets: readonly MotionChartBucket[]
  /** Overrides the default "HH:mm" x-axis label, e.g. for day/week-granularity buckets. */
  formatBucketLabel?: (isoDate: string) => string
  maxTicks?: number
}

const WIDTH = 360
const HEIGHT = 200
const PADDING = { top: 16, right: 12, bottom: 24, left: 32 }
const PLOT_WIDTH = WIDTH - PADDING.left - PADDING.right
const PLOT_HEIGHT = HEIGHT - PADDING.top - PADDING.bottom
const MAX_BAR_WIDTH = 24
const BAR_GAP = 2
const CORNER_RADIUS = 4

export function MotionBarChart({ buckets, formatBucketLabel = defaultBucketLabel, maxTicks = 6 }: MotionBarChartProps) {
  if (buckets.length === 0) {
    return (
      <figure className="motion-bar-chart motion-bar-chart--empty">
        <figcaption className="motion-bar-chart__caption">Motion events</figcaption>
        <p className="motion-bar-chart__empty-message">No data for this period</p>
      </figure>
    )
  }

  const maxCount = Math.max(...buckets.map((bucket) => bucket.count), 0)
  const yTicks = computeNiceTicks(0, maxCount, 4)
  const domainMax = yTicks[yTicks.length - 1] || 1

  const slotWidth = PLOT_WIDTH / buckets.length
  const barWidth = Math.max(Math.min(slotWidth - BAR_GAP, MAX_BAR_WIDTH), 1)
  const yScale = (value: number) => PADDING.top + (1 - value / domainMax) * PLOT_HEIGHT
  const baselineY = yScale(0)

  const tickIndices = pickTickIndices(buckets.length, maxTicks)
  const totalCount = buckets.reduce((sum, bucket) => sum + bucket.count, 0)
  const summary = `Motion events: ${totalCount} total across ${buckets.length} interval${buckets.length === 1 ? '' : 's'}`

  return (
    <figure className="motion-bar-chart">
      <figcaption className="motion-bar-chart__caption">Motion events</figcaption>
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="motion-bar-chart__svg" role="img" aria-label={summary}>
        {yTicks.map((tick) => (
          <g key={tick}>
            <line className="motion-bar-chart__grid-line" x1={PADDING.left} x2={WIDTH - PADDING.right} y1={yScale(tick)} y2={yScale(tick)} />
            <text className="motion-bar-chart__axis-label" x={PADDING.left - 6} y={yScale(tick)} textAnchor="end" dominantBaseline="middle">
              {tick}
            </text>
          </g>
        ))}

        {buckets.map((bucket, index) => {
          const slotCenter = PADDING.left + slotWidth * (index + 0.5)
          const barX = slotCenter - barWidth / 2
          const barTop = yScale(bucket.count)
          const barHeight = Math.max(baselineY - barTop, 0)
          return (
            <path
              key={bucket.bucketStart}
              className="motion-bar-chart__bar"
              d={roundedTopRectPath(barX, barTop, barWidth, barHeight, CORNER_RADIUS)}
            >
              <title>
                {formatBucketLabel(bucket.bucketStart)}: {bucket.count}
              </title>
            </path>
          )
        })}

        {tickIndices.map((index) => (
          <text
            key={index}
            className="motion-bar-chart__axis-label"
            x={PADDING.left + slotWidth * (index + 0.5)}
            y={HEIGHT - 6}
            textAnchor="middle"
          >
            {formatBucketLabel(buckets[index].bucketStart)}
          </text>
        ))}
      </svg>
    </figure>
  )
}
