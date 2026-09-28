import { defineConfig, devices } from '@playwright/test'

const baseURL = process.env.SMOKE_BASE_URL ?? 'http://localhost:8080'

/** Docs captures against the fixture stack. Reduced motion keeps stills stable. */
export default defineConfig({
  testDir: 'tests/screenshots',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 480_000,
  reporter: [['list']],
  use: {
    baseURL,
    serviceWorkers: 'block',
    ...devices['Desktop Chrome'],
  },
})
