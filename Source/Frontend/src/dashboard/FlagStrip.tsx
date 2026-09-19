import type { TimeBucket } from '../gql/graphql'
import { formatBucket, formatShare } from '../lib/format'

interface FlagBucket {
  bucketStart: string
  count: number
  trueCount: number
  trueShare: number
}

interface FlagSeries {
  location: string
  buckets: readonly FlagBucket[]
}

interface FlagStripProps {
  series: readonly FlagSeries[]
  bucket: TimeBucket
}

export const FlagStrip = ({ series, bucket }: FlagStripProps) => {
  const bucketStarts = [
    ...new Set(series.flatMap((entry) => entry.buckets.map((slot) => slot.bucketStart))),
  ].sort()

  const columns = `var(--strip-label) repeat(${bucketStarts.length}, minmax(0, 1fr))`

  return (
    <div className="strip">
      <div className="strip-grid" style={{ gridTemplateColumns: columns }}>
        {series.map((entry) => {
          const byStart = new Map(entry.buckets.map((slot) => [slot.bucketStart, slot]))

          return (
            <div className="strip-row" key={entry.location} style={{ display: 'contents' }}>
              <span className="strip-label">{entry.location}</span>
              {bucketStarts.map((start) => {
                const slot = byStart.get(start)

                return (
                  <div
                    key={start}
                    className={slot === undefined ? 'strip-cell blank' : 'strip-cell'}
                    style={
                      slot === undefined
                        ? undefined
                        : {
                            background: `color-mix(in srgb, var(--seq-450) ${Math.round(
                              slot.trueShare * 100,
                            )}%, var(--surface-sunk))`,
                          }
                    }
                    title={
                      slot === undefined
                        ? `${entry.location} · ${formatBucket(start, bucket)} · no readings`
                        : `${entry.location} · ${formatBucket(start, bucket)} · ${formatShare(
                            slot.trueShare,
                          )} (${slot.trueCount} of ${slot.count})`
                    }
                  />
                )
              })}
            </div>
          )
        })}
      </div>

      <div className="strip-axis">
        <span>{bucketStarts.length === 0 ? '' : formatBucket(bucketStarts[0], bucket)}</span>
        <span>
          {bucketStarts.length === 0
            ? ''
            : formatBucket(bucketStarts[bucketStarts.length - 1], bucket)}
        </span>
      </div>

      <div className="strip-scale">
        <span>none</span>
        <div className="strip-ramp" />
        <span>always</span>
      </div>
    </div>
  )
}
