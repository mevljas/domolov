import { describe, expect, it, vi } from 'vitest'
import { flushPromises, renderWithPlugins } from '@/test/render'
import { createCronModel } from '@/shared/lib/cron'
import ScheduleEditor from './ScheduleEditor.vue'
import WatchWizardUrlStep from './WatchWizardUrlStep.vue'

vi.mock('@/features/watches/api', async () => {
  const actual = await vi.importActual('@/features/watches/api')
  return {
    ...(actual as object),
    checkSearchUrl: vi.fn(async () => ({
      supported: true,
      providerId: 'nepremicnine',
      suggestedName: 'Ljubljana apartments',
      problem: null,
    })),
  }
})

vi.mock('@/features/settings/api', () => ({
  useServerSettings: () => ({
    data: { value: { timeZone: 'Europe/Ljubljana' } },
  }),
}))

describe('ScheduleEditor', () => {
  it('shows a human summary for the selected mode', async () => {
    const model = createCronModel({ mode: 'every6Hours' })
    const { wrapper } = await renderWithPlugins(ScheduleEditor, {
      props: { modelValue: model },
    })

    expect(wrapper.get('[data-testid="schedule-summary"]').text()).toContain('Every 6 hours')
    expect(wrapper.get('[data-testid="schedule-editor"]').text()).toContain('Europe/Ljubljana')
  })
})

describe('WatchWizardUrlStep', () => {
  it('shows the suggested name after a successful URL check', async () => {
    const { wrapper } = await renderWithPlugins(WatchWizardUrlStep, {
      props: {
        url: 'https://www.nepremicnine.net/oglasi-prodaja/ljubljana-mesto/',
        suggestedName: '',
      },
    })

    await flushPromises()
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(wrapper.get('[data-testid="wizard-suggested-name"]').text()).toContain(
      'Ljubljana apartments',
    )
    expect(wrapper.find('[data-testid="wizard-url-supported"]').exists()).toBe(true)
  })
})
