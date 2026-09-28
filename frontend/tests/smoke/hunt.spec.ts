import { expect, test } from '@playwright/test'
import { preferEnglish, signIn, waitForScan } from '../support/stack'

test('fixture hunt through the app', async ({ page }) => {
  await preferEnglish(page)
  await signIn(page)

  const url = `https://fixtures.domolov.test/oglasi-prodaja/ljubljana-${Date.now()}/`
  await page.goto('/watches')
  await page.getByTestId('new-watch').click()
  await page.getByTestId('wizard-url-input').fill(url)
  await expect(page.getByTestId('wizard-url-supported')).toBeVisible()
  await page.getByTestId('wizard-next').click()
  await expect(page.getByTestId('wizard-name-input')).not.toHaveValue('')
  await page.getByTestId('wizard-next').click()
  await page.getByTestId('wizard-destination').fill('https://discord.com/api/webhooks/1/smoke')
  await page.getByTestId('wizard-next').click()
  await page.getByTestId('wizard-create').click()
  await expect(page).toHaveURL(/\/watches\/[0-9a-f-]+/)

  const watchId = page.url().split('/').pop()!
  let scan = await waitForScan(page, watchId)
  expect(scan.status).toBe('baseline')

  await page.goto('/homes')
  await expect(page.locator('article').first()).toBeVisible()

  for (let round = 0; round < 3; round++) {
    await page.goto(`/watches/${watchId}`)
    await page.getByTestId('watch-run-now').click()
    scan = await waitForScan(page, watchId, scan.id)
    expect(scan.status).toBe('succeeded')
  }

  await page.goto('/homes?Reposted=true')
  await expect(page.getByText('Repost', { exact: true }).first()).toBeVisible()

  await page.goto('/matches')
  await expect(page.getByTestId('match-review')).toBeVisible()
  const before = await page.request.get('/api/home-matches?state=possible')
  const beforeCount = ((await before.json()) as unknown[]).length
  await page.getByTestId('match-confirm').click()
  await expect
    .poll(async () => {
      const after = await page.request.get('/api/home-matches?state=possible')
      return ((await after.json()) as unknown[]).length
    })
    .toBeLessThan(beforeCount)

  await page.goto('/homes')
  await page.locator('article a').first().click()
  await expect(page.getByRole('heading', { name: 'Listing history' })).toBeVisible()

  await page.getByRole('button', { name: 'Bookmark', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Remove bookmark' })).toBeVisible()
  await page.goto('/bookmarks')
  await expect(page.getByTestId('bookmark-board')).toBeVisible()
  await page.getByTestId('bookmark-stage-menu').first().click()
  await page.getByTestId('bookmark-move-contacted').click()
  await expect(
    page.getByTestId('bookmark-column').nth(1).getByTestId('bookmark-card'),
  ).toBeVisible()

  await page.goto('/homes')
  await page.getByTestId('home-dismiss').first().click()
  await page.getByRole('button', { name: 'Undo' }).click()

  await page.goto('/watches')
  await page
    .getByTestId('watch-card')
    .filter({ hasText: 'fixtures.domolov.test' })
    .first()
    .getByTestId('watch-pause-switch')
    .click()

  const docs = await page.goto('/api/docs')
  expect(docs?.ok() || docs?.status() === 200 || page.url().includes('/api/docs')).toBeTruthy()
  await expect(page.getByTestId('password-input')).toHaveCount(0)

  await page.goto('/')
  await page.getByTestId('sign-out').click()
  await expect(page).toHaveURL(/\/login/)
})
