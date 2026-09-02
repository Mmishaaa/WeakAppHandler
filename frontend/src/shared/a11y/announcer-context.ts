import { createContext } from 'react'

export interface AnnouncerContextValue {
  /** Pushes a message into the shared ARIA live region so screen readers announce it. */
  announce: (message: string) => void
}

export const AnnouncerContext = createContext<AnnouncerContextValue | null>(null)
