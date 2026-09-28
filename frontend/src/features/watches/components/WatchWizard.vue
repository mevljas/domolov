<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { LoaderCircle } from '@lucide/vue'
import {
  NOTIFICATION_CHANNELS,
  NOTIFICATION_TRIGGERS,
  type NotificationChannel,
  type NotificationTrigger,
} from '@/api/types'
import { problemMessage } from '@/api/problem'
import { useCreateRoute, useCreateWatch } from '@/features/watches/api'
import WatchWizardUrlStep from '@/features/watches/components/WatchWizardUrlStep.vue'
import ScheduleEditor from '@/features/watches/components/ScheduleEditor.vue'
import ResponsiveDialog from '@/shared/components/ResponsiveDialog.vue'
import { Button } from '@/shared/components/ui/button'
import { Input } from '@/shared/components/ui/input'
import { Label } from '@/shared/components/ui/label'
import { Checkbox } from '@/shared/components/ui/checkbox'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import { createCronModel, describeCron, toCron, validateCron } from '@/shared/lib/cron'
import { isAppLocale } from '@/shared/lib/locale'
import { useToast } from '@/shared/composables/useToast'

const open = defineModel<boolean>('open', { default: false })

const { t, locale } = useI18n()
const router = useRouter()
const toast = useToast()
const createWatch = useCreateWatch()
const routeWatchId = ref('')
const createRoute = useCreateRoute(routeWatchId)

const step = ref(0)
const url = ref('')
const name = ref('')
const urlOk = ref(false)
const schedule = ref(createCronModel())
const channel = ref<NotificationChannel>('discord')
const destination = ref('')
const triggers = ref<NotificationTrigger[]>(['newListing', 'priceDecreased'])
const submitting = ref(false)
const submitError = ref<string | null>(null)

const nameId = useId()
const destId = useId()

const STEPS = ['stepUrl', 'stepSchedule', 'stepRoute', 'stepReview'] as const
const appLocale = computed(() => (isAppLocale(locale.value) ? locale.value : 'sl'))
const cronSummary = computed(() => describeCron(toCron(schedule.value), appLocale.value))
const scheduleValid = computed(() =>
  schedule.value.mode === 'custom'
    ? validateCron(schedule.value.customCron?.trim() || '').valid
    : true,
)
const canContinue = computed(() => {
  if (step.value === 0) return urlOk.value
  if (step.value === 1) return name.value.trim().length > 0 && scheduleValid.value
  return true
})
const routePreview = computed(() => {
  const dest = destination.value.trim()
  if (!dest) return null
  return { channel: channel.value, destination: dest, triggers: triggers.value }
})

function reset() {
  step.value = 0
  url.value = ''
  name.value = ''
  urlOk.value = false
  schedule.value = createCronModel()
  channel.value = 'discord'
  destination.value = ''
  triggers.value = ['newListing', 'priceDecreased']
  submitting.value = false
  submitError.value = null
}

function onOpenChange(value: boolean) {
  open.value = value
  if (!value) reset()
}

function next() {
  if (!canContinue.value || step.value >= STEPS.length - 1) return
  step.value += 1
}

function back() {
  if (step.value === 0) return
  step.value -= 1
}

function toggleTrigger(trigger: NotificationTrigger, checked: boolean | 'indeterminate') {
  const on = checked === true
  const set = new Set(triggers.value)
  if (on) set.add(trigger)
  else set.delete(trigger)
  triggers.value = NOTIFICATION_TRIGGERS.filter((item) => set.has(item))
}

async function create() {
  if (submitting.value) return
  submitting.value = true
  submitError.value = null
  try {
    const watch = await createWatch.mutateAsync({
      name: name.value.trim(),
      searchUrl: url.value.trim(),
      cron: toCron(schedule.value),
      isPaused: false,
    })
    const dest = destination.value.trim()
    if (dest) {
      routeWatchId.value = watch.id
      await createRoute.mutateAsync({
        channel: channel.value,
        destination: dest,
        triggers: triggers.value,
        isEnabled: true,
      })
    }
    toast.success(t('watches.created'))
    open.value = false
    reset()
    await router.push({ name: 'watch', params: { id: watch.id } })
  } catch (error) {
    submitError.value = problemMessage(error, t)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <ResponsiveDialog
    :open="open"
    :title="t('watches.wizard.title')"
    :description="t('watches.wizard.description')"
    size="lg"
    @update:open="onOpenChange"
  >
    <ol class="mb-6 flex flex-wrap gap-2" :aria-label="t('watches.wizard.steps')">
      <li
        v-for="(key, index) in STEPS"
        :key="key"
        class="rounded-full px-3 py-1 text-xs font-semibold"
        :class="
          index === step
            ? 'bg-primary text-primary-foreground'
            : index < step
              ? 'bg-sage-soft text-primary'
              : 'bg-muted text-muted-foreground'
        "
      >
        {{ t(`watches.wizard.${key}`) }}
      </li>
    </ol>

    <WatchWizardUrlStep
      v-if="step === 0"
      v-model:url="url"
      v-model:suggested-name="name"
      @validity="urlOk = $event"
    />

    <div v-else-if="step === 1" class="grid gap-5">
      <div class="grid gap-2">
        <Label :for="nameId">{{ t('common.name') }}</Label>
        <Input :id="nameId" v-model="name" data-testid="wizard-name-input" />
        <p class="text-sm text-muted-foreground">{{ t('watches.wizard.nameHint') }}</p>
      </div>
      <ScheduleEditor v-model="schedule" />
    </div>

    <div v-else-if="step === 2" class="grid gap-5">
      <p class="text-sm text-muted-foreground">{{ t('watches.wizard.optionalRoute') }}</p>
      <div class="grid gap-2">
        <Label>{{ t('watches.wizard.channel') }}</Label>
        <Select v-model="channel">
          <SelectTrigger class="w-full" data-testid="wizard-channel">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="item in NOTIFICATION_CHANNELS" :key="item" :value="item">
              {{ t(`watches.routes.channels.${item}`) }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div class="grid gap-2">
        <Label :for="destId">{{ t('watches.routes.destination') }}</Label>
        <Input
          :id="destId"
          v-model="destination"
          :placeholder="t(`watches.routes.destinationHint.${channel}`)"
          data-testid="wizard-destination"
        />
      </div>
      <fieldset class="grid gap-2">
        <legend class="text-sm font-medium">{{ t('watches.routes.triggers') }}</legend>
        <div class="grid gap-2 sm:grid-cols-2">
          <label
            v-for="trigger in NOTIFICATION_TRIGGERS"
            :key="trigger"
            class="inline-flex items-center gap-2 text-sm"
          >
            <Checkbox
              :model-value="triggers.includes(trigger)"
              @update:model-value="(v) => toggleTrigger(trigger, v)"
            />
            {{ t(`watches.routes.trigger.${trigger}`) }}
          </label>
        </div>
      </fieldset>
    </div>

    <dl v-else class="grid gap-4 text-sm">
      <div>
        <dt class="text-muted-foreground">{{ t('watches.wizard.reviewName') }}</dt>
        <dd class="font-display text-lg text-foreground">{{ name }}</dd>
      </div>
      <div>
        <dt class="text-muted-foreground">{{ t('watches.wizard.reviewUrl') }}</dt>
        <dd class="break-all text-foreground">{{ url }}</dd>
      </div>
      <div>
        <dt class="text-muted-foreground">{{ t('watches.wizard.reviewSchedule') }}</dt>
        <dd class="text-foreground">{{ cronSummary }}</dd>
      </div>
      <div>
        <dt class="text-muted-foreground">{{ t('watches.wizard.reviewRoute') }}</dt>
        <dd class="text-foreground">
          <template v-if="routePreview">
            {{ t(`watches.routes.channels.${routePreview.channel}`) }} ·
            {{ routePreview.destination }}
          </template>
          <template v-else>{{ t('watches.wizard.noRoute') }}</template>
        </dd>
      </div>
      <p v-if="submitError" class="text-sm text-destructive" role="alert">{{ submitError }}</p>
    </dl>

    <template #footer>
      <div class="flex w-full flex-wrap justify-between gap-2">
        <Button v-if="step > 0" type="button" variant="ghost" :disabled="submitting" @click="back">
          {{ t('watches.wizard.back') }}
        </Button>
        <span v-else />
        <Button
          v-if="step < STEPS.length - 1"
          type="button"
          :disabled="!canContinue"
          data-testid="wizard-next"
          @click="next"
        >
          {{ t('watches.wizard.next') }}
        </Button>
        <Button
          v-else
          type="button"
          :disabled="submitting"
          data-testid="wizard-create"
          @click="create"
        >
          <LoaderCircle v-if="submitting" class="animate-spin" aria-hidden="true" />
          {{ submitting ? t('watches.wizard.creating') : t('watches.wizard.create') }}
        </Button>
      </div>
    </template>
  </ResponsiveDialog>
</template>
