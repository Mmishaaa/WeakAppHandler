import { MeterTile, type MeterTileMetric } from './MeterTile'
import type { MeterTileSeverity } from './overviewSeverity'
import './location-section.css'

export interface LocationSectionMeter {
  id: string
  meterType: string
  currentValues: readonly MeterTileMetric[]
}

export interface LocationSectionProps {
  location: string
  meters: readonly LocationSectionMeter[]
  severityByMeterId: ReadonlyMap<string, MeterTileSeverity>
  now: Date
}

function toHeadingId(location: string): string {
  return `location-${location.toLowerCase().replace(/\s+/g, '-')}`
}

export function LocationSection({ location, meters, severityByMeterId, now }: LocationSectionProps) {
  const headingId = toHeadingId(location)

  return (
    <section className="location-section" aria-labelledby={headingId}>
      <h2 id={headingId} className="location-section__heading">
        {location}
      </h2>
      <ul className="location-section__tiles">
        {meters.map((meter) => (
          <MeterTile
            key={meter.id}
            meterType={meter.meterType}
            metrics={meter.currentValues}
            severity={severityByMeterId.get(meter.id)}
            now={now}
          />
        ))}
      </ul>
    </section>
  )
}
