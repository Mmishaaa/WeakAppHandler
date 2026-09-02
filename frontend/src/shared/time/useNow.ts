import { useEffect, useState } from 'react'

/** Re-renders every `intervalMs` so relative-time labels (e.g. "5 minutes ago") stay fresh. */
export function useNow(intervalMs = 30_000): Date {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs)
    return () => clearInterval(id)
  }, [intervalMs])

  return now
}
