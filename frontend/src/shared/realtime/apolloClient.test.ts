import { describe, expect, it, vi } from 'vitest'
import type { ConnectionStatus } from './connectionStatus'

// Captures the `on` callbacks apolloClient.ts passes to graphql-ws's createClient at module load,
// so this suite can drive connecting/connected/closed transitions directly instead of needing a
// real WebSocket server - overrides setup-tests.ts's own network-free default for this file only.
const { capturedOn } = vi.hoisted(() => {
  const capturedOn: {
    current?: {
      connecting: (isRetry: boolean) => void
      connected: () => void
      closed: () => void
    }
  } = {}
  return { capturedOn }
})

vi.mock('graphql-ws', () => ({
  createClient: vi.fn((options: { on: NonNullable<typeof capturedOn.current> }) => {
    capturedOn.current = options.on
    return { on: vi.fn(), subscribe: vi.fn(), dispose: vi.fn() }
  }),
}))

import { onGraphQlWsStatusChange } from './apolloClient'

describe('apolloClient graphql-ws status wiring', () => {
  it('maps connecting/connected/closed events to ConnectionStatus, distinguishing first connect from reconnect', () => {
    const statuses: ConnectionStatus[] = []
    const unsubscribe = onGraphQlWsStatusChange((status) => statuses.push(status))

    try {
      const on = capturedOn.current
      if (!on) {
        throw new Error('createClient was not called with an `on` handler set')
      }

      on.connecting(false)
      on.connected()
      on.closed()
      on.connecting(true)
      on.connected()

      expect(statuses).toEqual(['connecting', 'connected', 'disconnected', 'reconnecting', 'connected'])
    } finally {
      unsubscribe()
    }
  })
})
