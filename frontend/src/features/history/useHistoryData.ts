import { useMemo } from 'react'
import { useQuery } from '@apollo/client/react'
import { getHistoryMetricInfo } from '../../entities/meter/historyMetrics'
import { graphql } from '../../gql'
import type { HistoryFilters } from './historyFilters'
import { computeHistoryRange } from './historyRange'

const HISTORY_LOCATIONS = graphql(`
  query HistoryLocations {
    meters(order: [{ location: ASC }]) {
      location
      meterType
    }
  }
`)

const HISTORY_AGGREGATIONS = graphql(`
  query HistoryAggregations(
    $metricCode: String!
    $bucket: AggregationBucketSize!
    $from: DateTime!
    $to: DateTime!
    $location: String
    $meterType: String
  ) {
    aggregations(metricCode: $metricCode, bucket: $bucket, from: $from, to: $to, location: $location, meterType: $meterType) {
      bucketStart
      avg
      min
      max
      sum
      count
    }
  }
`)

/** Distinct, order-preserved locations that actually have a meter of the given type. */
function distinctLocationsForMeterType(
  meters: readonly { location: string; meterType: string }[] | undefined,
  meterType: string,
): string[] {
  const seen = new Set<string>()
  for (const meter of meters ?? []) {
    if (meter.meterType === meterType) {
      seen.add(meter.location)
    }
  }
  return Array.from(seen)
}

/**
 * History screen's data source. Two independent queries: the full location list (fetched once,
 * filtered client-side to the locations relevant to the selected metric's meterType so the
 * location dropdown never offers a choice with no data), and the aggregation buckets for the
 * current metric/location/period combo. The aggregation query is skipped until a location is
 * resolved (either chosen by the user or defaulted to the first available one), since the Gateway
 * mixes rows across locations when `location` is omitted. The window's end is anchored to "now" at
 * the moment a filter changes, not re-anchored on every render - History is filter-driven, not a
 * live feed.
 */
export function useHistoryData(filters: HistoryFilters) {
  const metricInfo = getHistoryMetricInfo(filters.metricCode)

  const { data: locationsData, loading: locationsLoading, error: locationsError } = useQuery(HISTORY_LOCATIONS)

  const locations = useMemo(
    () => distinctLocationsForMeterType(locationsData?.meters, metricInfo.meterType),
    [locationsData, metricInfo.meterType],
  )

  const effectiveLocation = locations.includes(filters.location) ? filters.location : locations[0]

  const range = useMemo(() => computeHistoryRange(filters.period), [filters.period])

  const {
    data: aggregationsData,
    loading: aggregationsLoading,
    error: aggregationsError,
    refetch,
  } = useQuery(HISTORY_AGGREGATIONS, {
    variables: {
      metricCode: filters.metricCode,
      meterType: metricInfo.meterType,
      location: effectiveLocation,
      bucket: range.bucket,
      from: range.from,
      to: range.to,
    },
    skip: !effectiveLocation,
    notifyOnNetworkStatusChange: true,
  })

  return {
    data: aggregationsData,
    loading: locationsLoading || aggregationsLoading,
    error: aggregationsError ?? locationsError,
    refetch,
    locations,
    effectiveLocation,
  }
}
