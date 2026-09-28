import type { Page } from '@playwright/test'

export const smokePassword = process.env.SMOKE_PASSWORD ?? 'smoke-password'

export async function preferEnglish(page: Page, theme: 'light' | 'dark' = 'light') {
  await page.addInitScript((nextTheme) => {
    localStorage.setItem('domolov:preferences', JSON.stringify({ theme: nextTheme, locale: 'en' }))
  }, theme)
}

export async function signIn(page: Page) {
  await page.goto('/login')
  await page.getByTestId('password-input').fill(smokePassword)
  await page.getByTestId('login-submit').click()
  await page.getByTestId('dashboard-stats').waitFor()
}

type ScanList = { items?: { id?: string; status?: string }[] }

const FINISHED = new Set(['baseline', 'succeeded', 'failed', 'interrupted'])

export async function latestScan(page: Page, watchId: string) {
  const response = await page.request.get(`/api/scans?watchId=${watchId}&pageSize=1`)
  if (!response.ok()) return undefined
  const body = (await response.json()) as ScanList
  return body.items?.[0]
}

/**
 * Wait until the newest ScanRun is finished and is not `previousId`.
 * The previous run stays "baseline"/"succeeded" until the new one is queued, so a
 * status-only check would return the old run immediately.
 */
export async function waitForScan(page: Page, watchId: string, previousId?: string) {
  for (let attempt = 0; attempt < 60; attempt++) {
    const scan = await latestScan(page, watchId)
    if (scan?.id && scan.id !== previousId && scan.status && FINISHED.has(scan.status)) {
      return scan
    }
    await page.waitForTimeout(1000)
  }
  throw new Error(`Scan for ${watchId} did not finish`)
}
