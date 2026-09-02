import { useCallback, useEffect, useRef, useState } from 'react'
import { useQuery } from '@apollo/client/react'
import { graphql } from '../../gql'
import { useAnnouncer } from '../../shared/a11y/useAnnouncer'
import type { AlertRaisedMessage, AlertResolvedMessage } from '../../shared/realtime/alertsHubClient'
import { useAlertsHub } from '../../shared/realtime/useAlertsHub'

const ALERTS_DATA = graphql(`
  query AlertsData {
    alerts(order: [{ triggeredAt: DESC }], first: 100) {
      nodes {
        id
        location
        meterType
        metricCode
        status
        severity
        triggeredAt
        triggeredValueNumeric
        triggeredValueBool
        resolvedAt
        resolvedValueNumeric
        resolvedValueBool
      }
    }
  }
`)

/** Coalesces a burst of realtime events into one refetch, same shape as useOverviewData. */
const REFETCH_DEBOUNCE_MS = 500

/** How long a row surfaced by a raised/resolved event keeps its highlight, once the debounced
 * refetch that surfaces it actually lands - matches alert-row.css's highlight animation length. */
const HIGHLIGHT_DURATION_MS = 3_000

function describeAlertEvent(message: AlertRaisedMessage | AlertResolvedMessage, verb: string): string {
  return `${verb} ${message.severity.toLowerCase()} alert in ${message.location}: ${message.metricCode}`
}

/**
 * Alerts screen's data source: the alerts feed (all statuses, newest first from the server), kept
 * live by AlertsHub raised/resolved events. Mirrors useOverviewData's debounce-then-refetch shape,
 * plus two things specific to this screen: a short-lived highlight set so a newly arrived/resolved
 * row can be visually called out once the refetch actually lands, and a screen-reader announcement
 * per event via the app's shared AnnouncerProvider live region. Server data stays the single source
 * of truth throughout - the realtime payload itself is never merged into the Apollo cache.
 */
export function useAlertsData() {
  const { data, loading, error, refetch } = useQuery(ALERTS_DATA, {
    notifyOnNetworkStatusChange: true,
  })

  const { announce } = useAnnouncer()
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const [highlightedIds, setHighlightedIds] = useState<ReadonlySet<string>>(new Set())

  const scheduleRefetch = useCallback(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
    }
    debounceRef.current = setTimeout(() => {
      void refetch()
    }, REFETCH_DEBOUNCE_MS)
  }, [refetch])

  const highlight = useCallback((alertId: string) => {
    setHighlightedIds((current) => new Set(current).add(alertId))
    setTimeout(() => {
      setHighlightedIds((current) => {
        if (!current.has(alertId)) {
          return current
        }
        const next = new Set(current)
        next.delete(alertId)
        return next
      })
    }, HIGHLIGHT_DURATION_MS)
  }, [])

  useEffect(
    () => () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current)
      }
    },
    [],
  )

  const onAlertRaised = useCallback(
    (message: AlertRaisedMessage) => {
      highlight(message.alertId)
      announce(describeAlertEvent(message, 'New'))
      scheduleRefetch()
    },
    [announce, highlight, scheduleRefetch],
  )

  const onAlertResolved = useCallback(
    (message: AlertResolvedMessage) => {
      highlight(message.alertId)
      announce(describeAlertEvent(message, 'Resolved'))
      scheduleRefetch()
    },
    [announce, highlight, scheduleRefetch],
  )

  useAlertsHub({
    onAlertRaised,
    onAlertResolved,
    onReconnected: () => void refetch(),
  })

  return { data, loading, error, refetch, highlightedIds }
}
