import { useCallback, useEffect, useState } from 'react'
import { RestError } from '../../../shared/rest/restClient'
import {
  fetchIngestionStatus,
  triggerIngestion,
  updateIngestionInterval,
  type IngestionStatus,
  type IngestionTriggerResult,
} from './ingestionApi'

/**
 * Data + actions for the Administration screen's ingestion panel (TASK-040). Status is fetched
 * once on mount; both the manual trigger and the interval change re-fetch it afterwards so the
 * panel reflects the Ingestor's real state (circuit breaker, counters) rather than a value this
 * hook would otherwise have to guess at locally.
 */
export function useIngestionAdmin() {
  const [status, setStatus] = useState<IngestionStatus>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<unknown>()

  const [triggering, setTriggering] = useState(false)
  const [lastTriggerResult, setLastTriggerResult] = useState<IngestionTriggerResult>()
  const [triggerError, setTriggerError] = useState<unknown>()

  const [updatingInterval, setUpdatingInterval] = useState(false)
  const [intervalError, setIntervalError] = useState<string>()

  const refetch = useCallback(async () => {
    setLoading(true)
    try {
      setStatus(await fetchIngestionStatus())
      setError(undefined)
    } catch (fetchError) {
      setError(fetchError)
    } finally {
      setLoading(false)
    }
  }, [])

  // Deliberately not `void refetch()` here: refetch's own `setLoading(true)` runs synchronously
  // the moment it's called (an async function body runs synchronously up to its first `await`),
  // which is exactly the "setState synchronously within an effect" pattern react-hooks/set-state-in-effect
  // flags. `loading` already starts `true`, so the mount fetch only needs to settle it - every
  // setState below happens inside a `.then` callback (a microtask, after the effect has already
  // returned), which is the pattern that lint rule is designed to allow.
  useEffect(() => {
    let cancelled = false

    fetchIngestionStatus().then(
      (result) => {
        if (!cancelled) {
          setStatus(result)
          setError(undefined)
          setLoading(false)
        }
      },
      (fetchError: unknown) => {
        if (!cancelled) {
          setError(fetchError)
          setLoading(false)
        }
      },
    )

    return () => {
      cancelled = true
    }
  }, [])

  const trigger = useCallback(async () => {
    setTriggering(true)
    setTriggerError(undefined)
    try {
      setLastTriggerResult(await triggerIngestion())
      await refetch()
    } catch (thrown) {
      setTriggerError(thrown)
    } finally {
      setTriggering(false)
    }
  }, [refetch])

  const changeInterval = useCallback(
    async (pollingIntervalSeconds: number) => {
      setUpdatingInterval(true)
      setIntervalError(undefined)
      try {
        await updateIngestionInterval(pollingIntervalSeconds)
        await refetch()
      } catch (thrown) {
        setIntervalError(
          thrown instanceof RestError
            ? (thrown.fieldErrors.PollingIntervalSeconds?.[0] ?? thrown.message)
            : 'Failed to update the polling interval.',
        )
      } finally {
        setUpdatingInterval(false)
      }
    },
    [refetch],
  )

  return {
    status,
    loading,
    error,
    refetch,
    trigger,
    triggering,
    lastTriggerResult,
    triggerError,
    changeInterval,
    updatingInterval,
    intervalError,
  }
}
