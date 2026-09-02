import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import '@testing-library/jest-dom/vitest'

// @testing-library/react's own automatic afterEach(cleanup) only registers itself when it finds
// a GLOBAL `afterEach` on `globalThis` at import time - which requires vitest's `test.globals: true`.
// This project's test files import `afterEach`/`it`/`describe` explicitly from 'vitest' instead of
// relying on injected globals (vite.config.ts's test block has no `globals: true`), so that
// detection silently never fires and every render() left its tree in jsdom's shared document,
// accumulating across tests in the same file. Registered once here instead.
afterEach(cleanup)
