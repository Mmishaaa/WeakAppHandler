import { useEffect, useRef } from 'react'

export const useRefetchOn = (version: number, refetch: () => void): void => {
  const previous = useRef(version)

  useEffect(() => {
    if (previous.current === version) {
      return
    }

    previous.current = version
    refetch()
  }, [version, refetch])
}
