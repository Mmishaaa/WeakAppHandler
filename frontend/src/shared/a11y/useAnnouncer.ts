import { useContext } from 'react'
import { AnnouncerContext, type AnnouncerContextValue } from './announcer-context'

export function useAnnouncer(): AnnouncerContextValue {
  const context = useContext(AnnouncerContext)
  if (!context) {
    throw new Error('useAnnouncer must be used within an AnnouncerProvider')
  }
  return context
}
