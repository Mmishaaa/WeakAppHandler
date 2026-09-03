/** Parses a GraphQL `Decimal` field, which codegen types as `number | string | null`, into a
 * plain finite number or `null` for anything missing/non-numeric. */
export function toNumber(value: number | string | null | undefined): number | null {
  if (value === null || value === undefined) {
    return null
  }
  const parsed = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * Picks up to `maxTicks` evenly spaced indices from a `length`-long sequence, always keeping the
 * first and last index so axis endpoints are never dropped. This is what keeps x-axis labels from
 * overlapping once a chart has more buckets (e.g. 24 hourly buckets) than a 360px-wide viewport
 * has room to letter.
 */
export function pickTickIndices(length: number, maxTicks: number): number[] {
  if (length <= 0) {
    return []
  }
  if (maxTicks <= 1 || length <= maxTicks) {
    return Array.from({ length }, (_, index) => index)
  }

  const step = (length - 1) / (maxTicks - 1)
  const indices = new Set<number>()
  for (let i = 0; i < maxTicks; i++) {
    indices.add(Math.round(i * step))
  }
  return Array.from(indices).sort((a, b) => a - b)
}

/**
 * Heckbert's "nice numbers" algorithm: picks a human-friendly step (1/2/5 x a power of ten) and
 * returns ticks covering [min, max] rounded out to that step, so y-axis labels read as 0/500/1000
 * rather than the raw data extremes.
 */
export function computeNiceTicks(min: number, max: number, targetCount = 4): number[] {
  const domainMin = Math.min(min, max)
  const domainMax = Math.max(min, max)
  const range = domainMax - domainMin || Math.abs(domainMax) || 1
  const step = niceNumber(range / Math.max(targetCount - 1, 1), true)
  const niceMin = Math.floor(domainMin / step) * step
  const niceMax = Math.ceil(domainMax / step) * step

  const ticks: number[] = []
  for (let value = niceMin; value <= niceMax + step / 2; value += step) {
    ticks.push(Math.round((value + Number.EPSILON) * 1e6) / 1e6)
  }
  return ticks
}

function niceNumber(value: number, round: boolean): number {
  const exponent = Math.floor(Math.log10(value))
  const fraction = value / 10 ** exponent

  let niceFraction: number
  if (round) {
    if (fraction < 1.5) niceFraction = 1
    else if (fraction < 3) niceFraction = 2
    else if (fraction < 7) niceFraction = 5
    else niceFraction = 10
  } else {
    if (fraction <= 1) niceFraction = 1
    else if (fraction <= 2) niceFraction = 2
    else if (fraction <= 5) niceFraction = 5
    else niceFraction = 10
  }
  return niceFraction * 10 ** exponent
}

const bucketLabelFormatter = new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit' })

/** Default x-axis label for a bucket: "HH:mm" in en-US, matching this app's other fixed-locale
 * formatters (see relativeTime.ts). Callers with day/week buckets (TASK-038) can override this. */
export function defaultBucketLabel(isoDate: string): string {
  return bucketLabelFormatter.format(new Date(isoDate))
}

/** Builds an SVG path for a rectangle with rounded top corners and a square baseline - the "4px
 * rounded data-end, square at the baseline" bar mark spec. */
export function roundedTopRectPath(x: number, y: number, width: number, height: number, radius: number): string {
  const r = Math.max(Math.min(radius, width / 2, height), 0)
  if (r === 0) {
    return `M ${x},${y + height} L ${x},${y} L ${x + width},${y} L ${x + width},${y + height} Z`
  }
  return [
    `M ${x},${y + height}`,
    `L ${x},${y + r}`,
    `Q ${x},${y} ${x + r},${y}`,
    `L ${x + width - r},${y}`,
    `Q ${x + width},${y} ${x + width},${y + r}`,
    `L ${x + width},${y + height}`,
    'Z',
  ].join(' ')
}
