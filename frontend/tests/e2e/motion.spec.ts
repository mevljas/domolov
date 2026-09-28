import { expect, mockApi, presetPreferences, test } from './fixtures'

test.describe('prefers-reduced-motion', () => {
  test.use({ reducedMotion: 'reduce' })

  test('navigation still works and transitions are effectively disabled', async ({
    page,
    isMobile,
  }) => {
    await mockApi(page, { signedIn: true })
    await presetPreferences(page, { locale: 'en' })
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')

    expect(await page.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches)).toBe(
      true,
    )

    const duration = await page.evaluate(() => {
      const button = document.querySelector('[data-testid="command-trigger"]')!
      return getComputedStyle(button).transitionDuration
    })
    expect(parseFloat(duration)).toBeLessThan(0.01)

    let viewTransitions = 0
    await page.exposeFunction('countViewTransition', () => viewTransitions++)
    await page.evaluate(() => {
      const original = document.startViewTransition?.bind(document)
      if (!original) return
      document.startViewTransition = ((cb: () => void) => {
        ;(window as unknown as { countViewTransition: () => void }).countViewTransition()
        return original(cb)
      }) as typeof document.startViewTransition
    })

    const target = isMobile ? page.getByTestId('bottom-nav-homes') : page.getByTestId('nav-homes')
    await target.click()
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Homes')
    expect(viewTransitions).toBe(0)
  })
})

test('view transitions wrap route changes when motion is allowed', async ({ page, isMobile }) => {
  test.skip(isMobile, 'covered once on desktop')
  await mockApi(page, { signedIn: true })
  await presetPreferences(page, { locale: 'en' })
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')

  const supported = await page.evaluate(() => typeof document.startViewTransition === 'function')
  test.skip(!supported, 'View Transitions API not available')

  let viewTransitions = 0
  await page.exposeFunction('countViewTransition', () => viewTransitions++)
  await page.evaluate(() => {
    const original = document.startViewTransition.bind(document)
    document.startViewTransition = ((cb: () => void) => {
      ;(window as unknown as { countViewTransition: () => void }).countViewTransition()
      return original(cb)
    }) as typeof document.startViewTransition
  })

  await page.getByTestId('nav-scans').click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Scans')
  expect(viewTransitions).toBe(1)
})
