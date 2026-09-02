import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

// Same rationale as alertsHubClient.test.ts: override the network-free global default from
// setup-tests.ts with a fake precise enough to assert exactly which handlers get registered.
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

import { AlertHubMethod } from './alertsHubClient'
import { useAlertsHub } from './useAlertsHub'

describe('useAlertsHub', () => {
  beforeEach(() => {
    fakeConnection.state = 'Disconnected'
    fakeConnection.start.mockReset().mockResolvedValue(undefined)
    fakeConnection.on.mockClear()
    fakeConnection.off.mockClear()
  })

  it('registers exactly the supplied AlertRaised/AlertResolved handlers on mount, and removes them on unmount', () => {
    const onAlertRaised = vi.fn()
    const onAlertResolved = vi.fn()

    const { unmount } = renderHook(() => useAlertsHub({ onAlertRaised, onAlertResolved }))

    expect(fakeConnection.on).toHaveBeenCalledWith(AlertHubMethod.Raised, onAlertRaised)
    expect(fakeConnection.on).toHaveBeenCalledWith(AlertHubMethod.Resolved, onAlertResolved)

    unmount()

    expect(fakeConnection.off).toHaveBeenCalledWith(AlertHubMethod.Raised, onAlertRaised)
    expect(fakeConnection.off).toHaveBeenCalledWith(AlertHubMethod.Resolved, onAlertResolved)
  })

  it('registers no hub method handlers when none are supplied, so it never intercepts events on a caller\'s behalf', () => {
    renderHook(() => useAlertsHub())

    expect(fakeConnection.on).not.toHaveBeenCalled()
  })

  it('starts the underlying connection on mount and exposes its status once connected', async () => {
    const { result } = renderHook(() => useAlertsHub())

    expect(fakeConnection.start).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(result.current.status).toBe('connected'))
  })
})
