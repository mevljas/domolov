<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { useServerSettings } from '@/features/settings/api'
import { Label } from '@/shared/components/ui/label'
import { Input } from '@/shared/components/ui/input'
import { Checkbox } from '@/shared/components/ui/checkbox'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import {
  ALL_DAYS,
  describeCron,
  toCron,
  validateCron,
  WATCH_CRON_MODES,
  type DayOfWeek,
  type WatchCronMode,
  type WatchCronModel,
} from '@/shared/lib/cron'
import { isAppLocale } from '@/shared/lib/locale'

const model = defineModel<WatchCronModel>({ required: true })

const { t, locale } = useI18n()
const settings = useServerSettings()
const modeId = useId()
const timeId = useId()
const cronId = useId()

const DAY_KEYS = [
  'sunday',
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
] as const

const appLocale = computed(() => (isAppLocale(locale.value) ? locale.value : 'sl'))
const cron = computed(() => toCron(model.value))
const summary = computed(() => describeCron(cron.value, appLocale.value))
const validation = computed(() =>
  model.value.mode === 'custom'
    ? validateCron(model.value.customCron?.trim() || '')
    : { valid: true, error: null },
)
const timeZone = computed(() => settings.data.value?.timeZone ?? 'UTC')

const timeValue = computed({
  get() {
    const { hour, minute } = model.value.time
    return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`
  },
  set(value: string) {
    const [h, m] = value.split(':').map((part) => Number.parseInt(part, 10))
    if (!Number.isFinite(h) || !Number.isFinite(m)) return
    model.value = {
      ...model.value,
      time: { hour: Math.min(23, Math.max(0, h!)), minute: Math.min(59, Math.max(0, m!)) },
    }
  },
})

function setMode(mode: WatchCronMode) {
  model.value = { ...model.value, mode }
}

function toggleDay(day: DayOfWeek, checked: boolean | 'indeterminate') {
  const on = checked === true
  const days = new Set(model.value.days)
  if (on) days.add(day)
  else days.delete(day)
  const next = ALL_DAYS.filter((d) => days.has(d))
  model.value = { ...model.value, days: next.length > 0 ? next : [day] }
}

function isDayOn(day: DayOfWeek) {
  return model.value.days.includes(day)
}
</script>

<template>
  <div class="grid gap-5" data-testid="schedule-editor">
    <div class="grid gap-2">
      <Label :for="modeId">{{ t('schedule.title') }}</Label>
      <Select :model-value="model.mode" @update:model-value="(v) => setMode(v as WatchCronMode)">
        <SelectTrigger :id="modeId" class="w-full" data-testid="schedule-mode">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem v-for="mode in WATCH_CRON_MODES" :key="mode" :value="mode">
            {{ t(`schedule.modes.${mode}`) }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div v-if="model.mode === 'weekly'" class="grid gap-4">
      <div class="grid gap-2">
        <Label :for="timeId">{{ t('schedule.time') }}</Label>
        <Input
          :id="timeId"
          v-model="timeValue"
          type="time"
          class="w-40"
          data-testid="schedule-time"
        />
      </div>
      <fieldset class="grid gap-2">
        <legend class="text-sm font-medium">{{ t('schedule.days') }}</legend>
        <div class="flex flex-wrap gap-3">
          <label v-for="day in ALL_DAYS" :key="day" class="inline-flex items-center gap-2 text-sm">
            <Checkbox
              :model-value="isDayOn(day)"
              :data-testid="`schedule-day-${day}`"
              @update:model-value="(v) => toggleDay(day, v)"
            />
            {{ t(`days.short.${DAY_KEYS[day]}`) }}
          </label>
        </div>
      </fieldset>
    </div>

    <div v-else-if="model.mode === 'custom'" class="grid gap-2">
      <Label :for="cronId">{{ t('schedule.customLabel') }}</Label>
      <Input
        :id="cronId"
        :model-value="model.customCron ?? ''"
        class="font-mono"
        spellcheck="false"
        data-testid="schedule-custom-cron"
        :aria-invalid="!validation.valid || undefined"
        @update:model-value="(v) => (model = { ...model, customCron: String(v) })"
      />
      <p class="text-sm text-muted-foreground">{{ t('schedule.customHint') }}</p>
      <p v-if="!validation.valid" class="text-sm text-destructive" role="alert">
        {{ t('schedule.invalid') }}
      </p>
    </div>

    <div class="rounded-xl border bg-card px-4 py-3 shadow-xs">
      <p class="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
        {{ t('schedule.summary') }}
      </p>
      <p class="mt-1 font-display text-lg text-foreground" data-testid="schedule-summary">
        {{ summary }}
      </p>
      <p class="mt-1 text-sm text-muted-foreground">
        {{ t('schedule.timezoneHint', { timeZone }) }}
      </p>
    </div>
  </div>
</template>
