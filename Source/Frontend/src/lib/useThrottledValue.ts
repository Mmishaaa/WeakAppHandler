import { useEffect, useRef, useState } from 'react'

export const useThrottledValue = <T,>(value: T, intervalMs: number): T => {
  const [throttled, setThrottled] = useState(value)
  const lastAppliedAt = useRef(0)

  useEffect(() => {
    const elapsed = Date.now() - lastAppliedAt.current

    if (elapsed >= intervalMs) {
      lastAppliedAt.current = Date.now()
      setThrottled(value)

      return
    }

    const timer = window.setTimeout(() => {
      lastAppliedAt.current = Date.now()
      setThrottled(value)
    }, intervalMs - elapsed)

    return () => window.clearTimeout(timer)
  }, [value, intervalMs])

  return throttled
}
