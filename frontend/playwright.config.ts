import { defineConfig, devices } from '@playwright/test'

/**
 * TASK-050 (PRD §11's demo scenarios S1-S7). Runs against a stack already brought up via
 * `docker compose up` - this config does not itself start or stop the stack, matching the task's
 * own test_step ("run the Playwright suite against a stack already up via docker compose").
 * Every service's host-published port defaults match .env.example; override the same-named
 * environment variables for a non-default setup.
 */
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: 'list',
  timeout: 60_000,
  use: {
    baseURL: process.env.E2E_FRONTEND_URL ?? 'http://localhost:8086',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
