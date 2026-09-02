import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import '@testing-library/jest-dom/vitest'

// @testing-library/react's own automatic afterEach(cleanup) only registers itself when it finds
// a GLOBAL `afterEach` on `globalThis` at import time - which requires vitest's `test.globals: true`.
// This project's test files import `afterEach`/`it`/`describe` explicitly from 'vitest' instead of
// relying on injected globals (vite.config.ts's test block has no `globals: true`), so that
// detection silently never fires and every render() left its tree in jsdom's shared document,
// accumulating across tests in the same file. Registered once here instead.
afterEach(cleanup)

// Both realtime transports (SignalR, graphql-ws - see shared/realtime/alertsHubClient.ts and
// apolloClient.ts) open a real socket as a side effect of module import/component mount. Any test
// that renders something reachable from ConnectionStatusBadge (which AppShell always renders)
// would otherwise spend real time retrying a connection to a backend that isn't running during
// the suite. These network-free defaults apply to every test file; one that cares about the
// actual event wiring (status transitions, reconnect dispatch) overrides them with its own
// `vi.mock` for the same specifier, which takes precedence for that file.
vi.mock('@microsoft/signalr', () => {
  class FakeHubConnectionBuilder {
    withUrl() {
      return this
    }

    withAutomaticReconnect() {
      return this
    }

    build() {
      return {
        state: 'Disconnected',
        start: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        off: vi.fn(),
        onclose: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
      }
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
  createClient: vi.fn(() => ({
    on: vi.fn(() => () => {}),
    subscribe: vi.fn(() => () => {}),
    dispose: vi.fn(),
    terminate: vi.fn(),
  })),
}))
