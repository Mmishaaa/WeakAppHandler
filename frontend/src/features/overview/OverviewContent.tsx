import { useMemo } from 'react'
import { groupMetersByLocation } from './groupMetersByLocation'
import { LocationSection, type LocationSectionMeter } from './LocationSection'
import { OverviewHeader } from './OverviewHeader'
import { buildMeterSeverityMap, type ActiveAlertLite } from './overviewSeverity'

export interface OverviewContentData {
  meters: readonly (LocationSectionMeter & { location: string; lastSeenAt: string })[]
  alerts?: { nodes?: readonly ActiveAlertLite[] | null } | null
}

export interface OverviewContentProps {
  data: OverviewContentData
  now: Date
}

/** Pure presentation of an already-resolved Overview snapshot - no queries, no realtime wiring. */
export function OverviewContent({ data, now }: OverviewContentProps) {
  const groups = useMemo(() => groupMetersByLocation(data.meters), [data.meters])

  const activeAlerts = useMemo(() => data.alerts?.nodes ?? [], [data.alerts])
  const severityByMeterId = useMemo(() => buildMeterSeverityMap(activeAlerts), [activeAlerts])

  const lastPolledAt = useMemo(
    () =>
      data.meters.reduce<string | null>(
        (latest, meter) => (!latest || meter.lastSeenAt > latest ? meter.lastSeenAt : latest),
        null,
      ),
    [data.meters],
  )

  return (
    <>
      <OverviewHeader
        meterCount={data.meters.length}
        lastPolledAt={lastPolledAt}
        activeAlertCount={activeAlerts.length}
        now={now}
      />
      {groups.map((group) => (
        <LocationSection
          key={group.location}
          location={group.location}
          meters={group.meters}
          severityByMeterId={severityByMeterId}
          now={now}
        />
      ))}
    </>
  )
}
