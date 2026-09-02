import { beforeEach, describe, expect, it, vi } from 'vitest'

// Overrides this suite's own copy of the global default from setup-tests.ts with a fake precise
// enough to drive every status transition deliberately (mutable `state`, spy-captured
// onreconnecting/onreconnected/onclose callbacks) - this file's own vi.mock takes precedence over
// the setup file's for every test below.
const { fakeConnection } = vi.hoisted(() => {
  const fakeConnection = {
    state: 'Disconnected',
    start: vi.fn(),
    on: vi.fn(),
    off: vi.fn(),
    onclose: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
  }
  return { fakeConnection }
})

vi.mock('@microsoft/signalr', () => {
  class FakeHubConnectionBuilder {
    withUrl() {
      return this
    }

    withAutomaticReconnect() {
      return this
    }

    build() {
      return fakeConnection
    }
  }

  return {
    HubConnectionBuilder: FakeHubConnectionBuilder,
    HubConnectionState: {
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Connected: 'Connected',
      Disconnecting: 'Disconnecting',
      Reconnecting: 'Reconnecting',
    },
  }
})

import { ensureAlertsHubStarted, onAlertsHubReconnected, onAlertsHubStatusChange } from './alertsHubClient'

describe('alertsHubClient', () => {
  beforeEach(() => {
    fakeConnection.state = 'Disconnected'
    fakeConnection.start.mockReset().mockResolvedValue(undefined)
  })

  it('notifies connecting then connected on a successful start, and starts the connection only once', async () => {
    const statuses: string[] = []
    const unsubscribe = onAlertsHubStatusChange((status) => statuses.push(status))

    try {
      const first = ensureAlertsHubStarted()
      // Mirrors what a real HubConnection does synchronously once start() is called, before its
      // promise resolves - proves a concurrent second caller (e.g. a second mounted consumer)
      // doesn't trigger a second start().
      fakeConnection.state = 'Connecting'
      const second = ensureAlertsHubStarted()

      await Promise.all([first, second])

      expect(fakeConnection.start).toHaveBeenCalledTimes(1)
      expect(statuses).toEqual(['connecting', 'connected'])
    } finally {
      unsubscribe()
    }
  })

  it('does not fire the reconnect listener for the initial connect', async () => {
    const reconnected = vi.fn()
    const unsubscribe = onAlertsHubReconnected(reconnected)

    try {
      await ensureAlertsHubStarted()
      expect(reconnected).not.toHaveBeenCalled()
    } finally {
      unsubscribe()
    }
  })

  it('notifies reconnecting then connected, and fires the reconnect listener, on an actual reconnect', () => {
    const statuses: string[] = []
    const unsubscribeStatus = onAlertsHubStatusChange((status) => statuses.push(status))
    const reconnected = vi.fn()
    const unsubscribeReconnect = onAlertsHubReconnected(reconnected)

    try {
      const reconnectingHandler = fakeConnection.onreconnecting.mock.calls[0]?.[0] as () => void
      const reconnectedHandler = fakeConnection.onreconnected.mock.calls[0]?.[0] as () => void

      reconnectingHandler()
      reconnectedHandler()

      expect(statuses).toEqual(['reconnecting', 'connected'])
      expect(reconnected).toHaveBeenCalledTimes(1)
    } finally {
      unsubscribeStatus()
      unsubscribeReconnect()
    }
  })

  it('notifies disconnected when the connection closes', () => {
    const statuses: string[] = []
    const unsubscribe = onAlertsHubStatusChange((status) => statuses.push(status))

    try {
      const closeHandler = fakeConnection.onclose.mock.calls[0]?.[0] as () => void
      closeHandler()

      expect(statuses).toEqual(['disconnected'])
    } finally {
      unsubscribe()
    }
  })
})
