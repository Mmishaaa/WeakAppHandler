import { useEffect, useRef } from 'react'
import { usePageVisible } from './usePageVisible'

export const useRefetchOn = (version: number, refetch: () => unknown): void => {
  const refetchRef = useRef(refetch)
  const applied = useRef(version)
  const visible = usePageVisible()

  useEffect(() => {
    refetchRef.current = refetch
  }, [refetch])

  useEffect(() => {
    if (!visible || applied.current === version) {
      return
    }

    applied.current = version
    void refetchRef.current()
  }, [version, visible])
}
