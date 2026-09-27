import { useSyncExternalStore } from 'react'

export const isPageHidden = (): boolean => document.visibilityState === 'hidden'

const subscribe = (onChange: () => void): (() => void) => {
  document.addEventListener('visibilitychange', onChange)

  return () => document.removeEventListener('visibilitychange', onChange)
}

const getSnapshot = (): boolean => !isPageHidden()

const getServerSnapshot = (): boolean => true

export const usePageVisible = (): boolean =>
  useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot)
