import { defineConfig, devices } from '@playwright/test'

const baseURL = process.env.SMOKE_BASE_URL ?? 'http://localhost:8080'

/** Real stack (fixture provider). No local webServer — Compose is already up. */
export default defineConfig({
  testDir: 'tests/smoke',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 240_000,
  expect: { timeout: 15_000 },
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    serviceWorkers: 'block',
    ...devices['Desktop Chrome'],
    viewport: { width: 1440, height: 900 },
  },
})
