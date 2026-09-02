import { act, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

// Full-chain override of setup-tests.ts's network-free defaults: precise enough to drive both
// realtime channels independently and prove the badge really does cycle through
// connecting -> connected -> reconnecting -> connected -> disconnected end to end (TASK-035's
// acceptance criterion), not just that the underlying modules compute the right status in
// isolation.
const { fakeHubConnection, capturedWsOn } = vi.hoisted(() => {
  const fakeHubConnection = {
    state: 'Disconnected',
    start: vi.fn().mockResolvedValue(undefined),
    on: vi.fn(),
    off: vi.fn(),
    onclose: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
  }
  const capturedWsOn: {
    current?: { connecting: (isRetry: boolean) => void; connected: () => void; closed: () => void }
  } = {}
  return { fakeHubConnection, capturedWsOn }
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
      return fakeHubConnection
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

vi.mock('graphql-ws', () => ({
  createClient: vi.fn((options: { on: NonNullable<typeof capturedWsOn.current> }) => {
    capturedWsOn.current = options.on
    return { on: vi.fn(), subscribe: vi.fn(), dispose: vi.fn() }
  }),
}))

import { ConnectionStatusBadge } from './ConnectionStatusBadge'

describe('ConnectionStatusBadge', () => {
  it('shows the combined status of both realtime channels, worse-of-two, live', async () => {
    render(<ConnectionStatusBadge />)

    expect(screen.getByRole('status')).toHaveTextContent('Connecting…')

    const wsOn = capturedWsOn.current
    if (!wsOn) {
      throw new Error('graphql-ws createClient was not called with an `on` handler set')
    }

    act(() => {
      wsOn.connecting(false)
      wsOn.connected()
    })

    // "Live" requires BOTH channels connected - the alerts hub connects on its own via the
    // mocked start() promise, so this also waits out that microtask.
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Live'))

    const reconnectingHandler = fakeHubConnection.onreconnecting.mock.calls[0]?.[0] as () => void
    act(() => reconnectingHandler())
    expect(screen.getByRole('status')).toHaveTextContent('Reconnecting…')

    const reconnectedHandler = fakeHubConnection.onreconnected.mock.calls[0]?.[0] as () => void
    act(() => reconnectedHandler())
    expect(screen.getByRole('status')).toHaveTextContent('Live')

    const closeHandler = fakeHubConnection.onclose.mock.calls[0]?.[0] as () => void
    act(() => closeHandler())
    expect(screen.getByRole('status')).toHaveTextContent('Offline')
  })
})
