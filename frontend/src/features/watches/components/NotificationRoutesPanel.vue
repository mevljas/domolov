<script setup lang="ts">
import { computed, ref, useId, watch as watchSource } from 'vue'
import { useI18n } from 'vue-i18n'
import { LoaderCircle, Plus, Send, Trash2 } from '@lucide/vue'
import {
  NOTIFICATION_CHANNELS,
  NOTIFICATION_TRIGGERS,
  type NotificationChannel,
  type NotificationRoute,
  type NotificationTrigger,
  type Watch,
} from '@/api/types'
import { problemMessage } from '@/api/problem'
import {
  useCreateRoute,
  useDeleteRoute,
  useTestRoute,
  useUpdateRoute,
} from '@/features/watches/api'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { Checkbox } from '@/shared/components/ui/checkbox'
import { Input } from '@/shared/components/ui/input'
import { Label } from '@/shared/components/ui/label'
import { Switch } from '@/shared/components/ui/switch'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import { useToast } from '@/shared/composables/useToast'

const props = defineProps<{ watch: Watch }>()

const { t } = useI18n()
const toast = useToast()
const createRoute = useCreateRoute(() => props.watch.id)
const updateRoute = useUpdateRoute()
const deleteRoute = useDeleteRoute()
const testRoute = useTestRoute()

const adding = ref(false)
const channel = ref<NotificationChannel>('discord')
const destination = ref('')
const triggers = ref<NotificationTrigger[]>(['newListing', 'priceDecreased'])
const destId = useId()
const pendingDelete = ref<NotificationRoute | null>(null)
const deleteOpen = ref(false)
const testResults = ref<Record<string, { ok: boolean; error: string | null }>>({})

const routes = computed(() => props.watch.routes)

watchSource(
  () => props.watch.id,
  () => {
    adding.value = false
    destination.value = ''
    testResults.value = {}
    pendingDelete.value = null
    deleteOpen.value = false
  },
)

function toggleTrigger(list: NotificationTrigger[], trigger: NotificationTrigger, on: boolean) {
  const set = new Set(list)
  if (on) set.add(trigger)
  else set.delete(trigger)
  return NOTIFICATION_TRIGGERS.filter((item) => set.has(item))
}

async function onToggleEnabled(route: NotificationRoute, enabled: boolean) {
  try {
    await updateRoute.mutateAsync({ route, changes: { isEnabled: enabled } })
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

async function onToggleTrigger(
  route: NotificationRoute,
  trigger: NotificationTrigger,
  on: boolean,
) {
  try {
    await updateRoute.mutateAsync({
      route,
      changes: { triggers: toggleTrigger(route.triggers as NotificationTrigger[], trigger, on) },
    })
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

async function onAdd() {
  const dest = destination.value.trim()
  if (!dest) return
  try {
    await createRoute.mutateAsync({
      channel: channel.value,
      destination: dest,
      triggers: triggers.value,
      isEnabled: true,
    })
    toast.success(t('watches.routes.added'))
    adding.value = false
    destination.value = ''
    triggers.value = ['newListing', 'priceDecreased']
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

async function onDelete() {
  const route = pendingDelete.value
  if (!route) return
  try {
    await deleteRoute.mutateAsync(route)
    toast.success(t('watches.routes.deleted'))
    pendingDelete.value = null
    deleteOpen.value = false
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

function askDelete(route: NotificationRoute) {
  pendingDelete.value = route
  deleteOpen.value = true
}

async function onTest(route: NotificationRoute) {
  try {
    const result = await testRoute.mutateAsync(route)
    testResults.value = { ...testResults.value, [route.id]: { ok: result.ok, error: result.error } }
    if (result.ok) toast.success(t('watches.routes.testOk'))
    else toast.error(t('watches.routes.testFailed', { error: result.error ?? '—' }))
  } catch (error) {
    const message = problemMessage(error, t)
    testResults.value = { ...testResults.value, [route.id]: { ok: false, error: message } }
    toast.error(message)
  }
}
</script>

<template>
  <section class="grid gap-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="font-display text-xl font-semibold">{{ t('watches.routes.title') }}</h2>
      <Button type="button" size="sm" variant="outline" @click="adding = !adding">
        <Plus aria-hidden="true" />
        {{ t('watches.routes.add') }}
      </Button>
    </div>

    <div v-if="adding" class="grid gap-4 rounded-2xl border bg-card p-4 shadow-xs">
      <div class="grid gap-2 sm:grid-cols-2">
        <div class="grid gap-2">
          <Label>{{ t('watches.wizard.channel') }}</Label>
          <Select v-model="channel">
            <SelectTrigger class="w-full">
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
          />
        </div>
      </div>
      <fieldset class="grid gap-2">
        <legend class="text-sm font-medium">{{ t('watches.routes.triggers') }}</legend>
        <div class="flex flex-wrap gap-3">
          <label
            v-for="trigger in NOTIFICATION_TRIGGERS"
            :key="trigger"
            class="inline-flex items-center gap-2 text-sm"
          >
            <Checkbox
              :model-value="triggers.includes(trigger)"
              @update:model-value="(v) => (triggers = toggleTrigger(triggers, trigger, v === true))"
            />
            {{ t(`watches.routes.trigger.${trigger}`) }}
          </label>
        </div>
      </fieldset>
      <div class="flex gap-2">
        <Button
          type="button"
          :disabled="!destination.trim() || createRoute.isPending.value"
          @click="onAdd"
        >
          <LoaderCircle
            v-if="createRoute.isPending.value"
            class="animate-spin"
            aria-hidden="true"
          />
          {{ t('watches.routes.add') }}
        </Button>
        <Button type="button" variant="ghost" @click="adding = false">{{
          t('common.cancel')
        }}</Button>
      </div>
    </div>

    <p v-if="routes.length === 0" class="text-sm text-muted-foreground">
      {{ t('watches.routes.empty') }}
    </p>

    <ul v-else class="grid gap-3">
      <li v-for="route in routes" :key="route.id" class="rounded-2xl border bg-card p-4 shadow-xs">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">{{ t(`watches.routes.channels.${route.channel}`) }}</Badge>
              <span class="truncate text-sm text-foreground">{{ route.destination }}</span>
            </div>
          </div>
          <label class="inline-flex items-center gap-2 text-sm text-muted-foreground">
            {{ t('watches.routes.enabled') }}
            <Switch
              :model-value="route.isEnabled"
              @update:model-value="(v) => onToggleEnabled(route, v)"
            />
          </label>
        </div>

        <fieldset class="mt-3 grid gap-2">
          <legend class="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
            {{ t('watches.routes.triggers') }}
          </legend>
          <div class="flex flex-wrap gap-3">
            <label
              v-for="trigger in NOTIFICATION_TRIGGERS"
              :key="trigger"
              class="inline-flex items-center gap-2 text-sm"
            >
              <Checkbox
                :model-value="route.triggers.includes(trigger)"
                @update:model-value="(v) => onToggleTrigger(route, trigger, v === true)"
              />
              {{ t(`watches.routes.trigger.${trigger}`) }}
            </label>
          </div>
        </fieldset>

        <div class="mt-3 flex flex-wrap items-center gap-2">
          <Button
            type="button"
            size="sm"
            variant="outline"
            :disabled="testRoute.isPending.value"
            @click="onTest(route)"
          >
            <Send aria-hidden="true" />
            {{ t('watches.routes.test') }}
          </Button>
          <Button
            type="button"
            size="sm"
            variant="ghost"
            class="text-destructive"
            @click="askDelete(route)"
          >
            <Trash2 aria-hidden="true" />
            {{ t('common.delete') }}
          </Button>
          <p
            v-if="testResults[route.id]"
            class="text-sm"
            :class="testResults[route.id]!.ok ? 'text-success' : 'text-destructive'"
            role="status"
          >
            {{
              testResults[route.id]!.ok
                ? t('watches.routes.testOk')
                : t('watches.routes.testFailed', { error: testResults[route.id]!.error ?? '—' })
            }}
          </p>
        </div>
      </li>
    </ul>

    <ConfirmDialog
      v-model:open="deleteOpen"
      :title="t('common.delete')"
      :description="t('watches.routes.confirmDelete')"
      :confirm-label="t('common.delete')"
      destructive
      :loading="deleteRoute.isPending.value"
      :close-on-confirm="false"
      @confirm="onDelete"
    />
  </section>
</template>
