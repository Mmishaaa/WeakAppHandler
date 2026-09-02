import { useSubscription } from '@apollo/client/react'
import { graphql } from '../../gql'

const ON_READING_STORED = graphql(`
  subscription OnReadingStored($location: String, $meterType: String) {
    onReadingStored(location: $location, meterType: $meterType) {
      meterId
      location
      meterType
      metricCode
      valueNumeric
      valueBool
      isChanged
      observedAt
    }
  }
`)

export interface UseOnReadingStoredSubscriptionOptions {
  location?: string
  meterType?: string
  /** Skips subscribing entirely, e.g. while a screen showing readings isn't mounted. */
  skip?: boolean
}

/**
 * The only place in the app allowed to subscribe to `onReadingStored` - consumers (TASK-036/038
 * onward) go through this hook rather than calling `useSubscription` on the document directly, so
 * a reading-stored event is never independently wired up through a second channel (e.g. polling)
 * in the same component alongside this one.
 */
export function useOnReadingStoredSubscription(options: UseOnReadingStoredSubscriptionOptions = {}) {
  const { location, meterType, skip } = options
  return useSubscription(ON_READING_STORED, {
    variables: { location, meterType },
    skip,
  })
}
