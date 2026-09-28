import { mkdir } from 'node:fs/promises'
import path from 'node:path'
import { expect, test, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import { preferEnglish, signIn, waitForScan } from '../support/stack'

const outDir = path.resolve(import.meta.dirname, '../../../docs/assets/screenshots')

const shots = [
  ['login', '/login', false],
  ['dashboard', '/', true],
  ['homes', '/homes', true],
  ['bookmarks', '/bookmarks', true],
  ['watches', '/watches', true],
  ['scans', '/scans', true],
  ['settings', '/settings', true],
] as const

async function settle(page: Page) {
  await page.waitForLoadState('domcontentloaded')
  await page.evaluate(() => document.fonts.ready)
  await page.waitForTimeout(400)
}

async function shoot(
  browser: Browser,
  name: string,
  url: string,
  theme: 'light' | 'dark',
  storageState: BrowserContextOptions['storageState'],
) {
  for (const viewport of [
    { id: 'desktop', width: 1440, height: 900 },
    { id: 'mobile', width: 390, height: 844 },
  ] as const) {
    const context = await browser.newContext({
      viewport: { width: viewport.width, height: viewport.height },
      reducedMotion: 'reduce',
      colorScheme: theme,
      storageState,
    })
    const page = await context.newPage()
    await preferEnglish(page, theme)
    await page.goto(url)
    await settle(page)
    await page.screenshot({
      path: path.join(outDir, `${name}-${theme}-${viewport.id}.png`),
      fullPage: false,
    })
    await context.close()
  }
}

test('capture docs stills and a wizard recording', async ({ browser, page }) => {
  await mkdir(outDir, { recursive: true })

  await preferEnglish(page)
  await signIn(page)

  const created = await page.request.post('/api/watches', {
    headers: { 'X-Requested-With': 'domolov', 'Content-Type': 'application/json' },
    data: {
      name: 'Ljubljana flats',
      searchUrl: `https://fixtures.domolov.test/oglasi-prodaja/ljubljana-shots-${Date.now()}/`,
      cron: '0 */6 * * *',
      isPaused: false,
    },
  })
  expect(created.ok()).toBeTruthy()
  const watch = (await created.json()) as { id: string }
  let scan = await waitForScan(page, watch.id)
  for (let round = 0; round < 3; round++) {
    const run = await page.request.post(`/api/watches/${watch.id}/scans`, {
      headers: { 'X-Requested-With': 'domolov' },
    })
    expect(run.status()).toBe(202)
    scan = await waitForScan(page, watch.id, scan.id)
  }

  const homes = await page.request.get('/api/homes?PageSize=1')
  const first = ((await homes.json()) as { items: { id: string }[] }).items[0]
  expect(first).toBeTruthy()
  await page.request.put(`/api/homes/${first!.id}/bookmark`, {
    headers: { 'X-Requested-With': 'domolov', 'Content-Type': 'application/json' },
    data: { stage: 'interested', note: 'Saturday viewing' },
  })

  const session = await page.context().storageState()
  const signedIn = { cookies: session.cookies, origins: [] }

  for (const [name, url, needsSession] of shots) {
    await shoot(browser, name, url, 'light', needsSession ? signedIn : undefined)
    await shoot(browser, name, url, 'dark', needsSession ? signedIn : undefined)
  }

  await shoot(browser, 'home', `/homes/${first!.id}`, 'light', signedIn)
  await shoot(browser, 'home', `/homes/${first!.id}`, 'dark', signedIn)
  await shoot(browser, 'matches', '/matches', 'light', signedIn)
  await shoot(browser, 'matches', '/matches', 'dark', signedIn)

  const videoContext = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    storageState: signedIn,
    recordVideo: { dir: outDir, size: { width: 1440, height: 900 } },
  })
  const videoPage = await videoContext.newPage()
  await preferEnglish(videoPage)
  await videoPage.goto('/watches')
  await videoPage.getByTestId('new-watch').click()
  await videoPage
    .getByTestId('wizard-url-input')
    .fill('https://fixtures.domolov.test/oglasi-prodaja/ljubljana-wizard/')
  await videoPage.getByTestId('wizard-url-supported').waitFor()
  await videoPage.waitForTimeout(600)
  const video = videoPage.video()
  await videoContext.close()
  await video?.saveAs(path.join(outDir, 'wizard.webm'))
})
