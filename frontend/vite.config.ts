/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/shared/testing/setup-tests.ts'],
    css: true,
    // tests/e2e/** are Playwright specs (playwright.config.ts, run via `npm run test:e2e`) - they
    // define their own `test`/`describe` globals, incompatible with vitest's, so vitest must never
    // try to collect them too. Vitest's own defaults (node_modules, dist, .git, ...) are repeated
    // here rather than dropped, since setting `exclude` at all replaces them instead of merging.
    exclude: [
      '**/node_modules/**',
      '**/dist/**',
      '**/.{idea,git,cache,output,temp}/**',
      '**/{karma,rollup,webpack,vite,vitest,jest,ava,babel,nyc,cypress,tsup,build}.config.*',
      'tests/e2e/**',
    ],
  },
})
