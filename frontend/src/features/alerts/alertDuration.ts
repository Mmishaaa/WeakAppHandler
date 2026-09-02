/** Formats the elapsed time between an alert's trigger and resolution as a short duration string
 * ("45s", "5m 32s", "1h 05m", "2d 03h") - always the two most significant units for anything a
 * minute or longer, so a multi-day alert doesn't get lost among seconds. */
export function formatAlertDuration(triggeredAt: string, resolvedAt: string): string {
  const totalSeconds = Math.max(
    0,
    Math.round((new Date(resolvedAt).getTime() - new Date(triggeredAt).getTime()) / 1000),
  )

  if (totalSeconds < 60) {
    return `${totalSeconds}s`
  }

  if (totalSeconds < 60 * 60) {
    const minutes = Math.floor(totalSeconds / 60)
    const seconds = totalSeconds % 60
    return `${minutes}m ${String(seconds).padStart(2, '0')}s`
  }

  if (totalSeconds < 60 * 60 * 24) {
    const hours = Math.floor(totalSeconds / 3600)
    const minutes = Math.floor((totalSeconds % 3600) / 60)
    return `${hours}h ${String(minutes).padStart(2, '0')}m`
  }

  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  return `${days}d ${String(hours).padStart(2, '0')}h`
}
