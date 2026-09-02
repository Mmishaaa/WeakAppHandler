import { useCallback, useEffect, useRef } from 'react'
import { useQuery } from '@apollo/client/react'
import { graphql } from '../../gql'
import { useAlertsHub } from '../../shared/realtime/useAlertsHub'
import { useOnReadingStoredSubscription } from '../../shared/realtime/useOnReadingStoredSubscription'

const OVERVIEW_DATA = graphql(`
  query OverviewData {
    meters(order: [{ location: ASC }, { meterType: ASC }]) {
      id
      location
      meterType
      lastSeenAt
      currentValues {
        metricCode
        valueNumeric
        valueBool
        observedAt
      }
    }
    alerts(where: { status: { eq: ACTIVE } }, first: 100) {
      nodes {
        id
        meterId
        severity
      }
    }
  }
`)

/** Coalesces a burst of realtime events (e.g. one poll cycle touching 18 meters) into one refetch. */
const REFETCH_DEBOUNCE_MS = 500

/**
 * Overview's data source: the meters+active-alerts snapshot, kept live by two independent realtime
 * channels - a new `onReadingStored` event or an alert raised/resolved schedules a debounced
 * refetch, and an AlertsHub reconnect triggers an immediate one (to pick up whatever was missed
 * while disconnected, per TASK-035's reconnect-then-refetch seam). Server data stays the single
 * source of truth; no client-side merging of realtime payloads into the query result.
 */
export function useOverviewData() {
  const { data, loading, error, refetch } = useQuery(OVERVIEW_DATA, {
    notifyOnNetworkStatusChange: true,
  })

  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const scheduleRefetch = useCallback(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
    }
    debounceRef.current = setTimeout(() => {
      void refetch()
    }, REFETCH_DEBOUNCE_MS)
  }, [refetch])

  useEffect(
    () => () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current)
      }
    },
    [],
  )

  const { data: readingStoredData } = useOnReadingStoredSubscription()
  useEffect(() => {
    if (readingStoredData) {
      scheduleRefetch()
    }
  }, [readingStoredData, scheduleRefetch])

  useAlertsHub({
    onAlertRaised: scheduleRefetch,
    onAlertResolved: scheduleRefetch,
    onReconnected: () => void refetch(),
  })

  return { data, loading, error, refetch }
}
