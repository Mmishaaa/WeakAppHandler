const numberFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 })

const timeFormat = new Intl.DateTimeFormat(undefined, {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

const hourFormat = new Intl.DateTimeFormat(undefined, {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const dayFormat = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' })

const stampFormat = new Intl.DateTimeFormat(undefined, {
  month: 'short',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

export const formatNumber = (value: number | null | undefined): string =>
  value === null || value === undefined ? '—' : numberFormat.format(value)

export const formatValue = (
  numeric: number | null | undefined,
  flag: boolean | null | undefined,
): string => {
  if (numeric !== null && numeric !== undefined) {
    return numberFormat.format(numeric)
  }

  if (flag !== null && flag !== undefined) {
    return flag ? 'true' : 'false'
  }

  return '—'
}

export const formatTime = (iso: string): string => timeFormat.format(new Date(iso))

export const formatStamp = (iso: string): string => stampFormat.format(new Date(iso))

export const formatBucket = (iso: string, bucket: 'HOUR' | 'DAY'): string =>
  bucket === 'DAY' ? dayFormat.format(new Date(iso)) : hourFormat.format(new Date(iso))
