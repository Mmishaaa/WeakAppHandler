export interface MetricDisplayInfo {
  displayName: string
  unit: string | null
  kind: 'numeric' | 'boolean'
}

/**
 * The Gateway GraphQL schema has no unit/display-name metadata for a metric (MeterReadModel /
 * MeterCurrentValueReadModel only carry `metricCode`), even though Processor's `metrics` seed
 * table does (see MetricSeedData.cs). Hardcoded here from that same seed data until a follow-up
 * task exposes it over GraphQL - tracked as a known gap, not an oversight.
 */
const METRIC_DISPLAY: Record<string, MetricDisplayInfo> = {
  energy: { displayName: 'Energy', unit: 'kWh', kind: 'numeric' },
  co2: { displayName: 'CO2', unit: 'ppm', kind: 'numeric' },
  pm25: { displayName: 'PM2.5', unit: 'µg/m³', kind: 'numeric' },
  humidity: { displayName: 'Humidity', unit: '%', kind: 'numeric' },
  motion_detected: { displayName: 'Motion Detected', unit: null, kind: 'boolean' },
}

/** Falls back to the raw metric code for anything not in the known seed set above. */
export function getMetricDisplayInfo(metricCode: string): MetricDisplayInfo {
  return METRIC_DISPLAY[metricCode] ?? { displayName: metricCode, unit: null, kind: 'numeric' }
}

export function formatMetricValue(
  metricCode: string,
  valueNumeric: number | string | null | undefined,
  valueBool: boolean | null | undefined,
): string {
  const info = getMetricDisplayInfo(metricCode)

  if (info.kind === 'boolean') {
    return valueBool ? 'Yes' : 'No'
  }

  if (valueNumeric === null || valueNumeric === undefined) {
    return '—'
  }

  // Fixed to 'en-US' for the same reason as relativeTime.ts - the rest of the UI is English-only.
  const formatted = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(Number(valueNumeric))
  return info.unit ? `${formatted} ${info.unit}` : formatted
}
