import AxeBuilder from '@axe-core/playwright'
import { expect, mockApi, presetPreferences, test, type Page } from './fixtures'

async function seriousViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'])
    .analyze()
  return results.violations
    .filter((v) => v.impact === 'serious' || v.impact === 'critical')
    .map((v) => ({
      id: v.id,
      impact: v.impact,
      help: v.help,
      targets: v.nodes.map((n) => n.target.join(' ')),
    }))
}

for (const theme of ['light', 'dark'] as const) {
  test.describe(`${theme} theme`, () => {
    test.beforeEach(async ({ page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' })
    })

    test('login page has no serious or critical axe violations', async ({ page }) => {
      await mockApi(page)
      await page.goto('/login')
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
      expect(await seriousViolations(page)).toEqual([])
    })

    test('dashboard placeholder has no serious or critical axe violations', async ({ page }) => {
      await mockApi(page, { signedIn: true })
      await presetPreferences(page, { locale: 'en' })
      await page.goto('/')
      await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')
      expect(await seriousViolations(page)).toEqual([])
    })
  })
}

test('skip link moves focus to the main content', async ({ page }) => {
  await mockApi(page, { signedIn: true })
  await presetPreferences(page, { locale: 'en' })
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

  await page.keyboard.press('Tab')
  const skip = page.getByRole('link', { name: 'Skip to content' })
  await expect(skip).toBeFocused()
  await expect(skip).toBeInViewport()
  await page.keyboard.press('Enter')
  await expect(page.locator('main#main')).toBeFocused()
})
