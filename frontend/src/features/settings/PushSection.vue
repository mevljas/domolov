<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemMessage } from '@/api/problem'
import type { Settings } from '@/api/types'
import { Label } from '@/shared/components/ui/label'
import { Switch } from '@/shared/components/ui/switch'
import { useToast } from '@/shared/composables/useToast'
import { useDeletePushSubscription, useUpsertPushSubscription } from './api'
import {
  getStoredPushSubscriptionId,
  setStoredPushSubscriptionId,
  urlBase64ToUint8Array,
} from './push'

const props = defineProps<{
  settings: Settings
}>()

const { t } = useI18n()
const toast = useToast()
const upsert = useUpsertPushSubscription()
const remove = useDeletePushSubscription()

const enabled = ref(Boolean(getStoredPushSubscriptionId()))
const busy = ref(false)

const vapidReady = computed(() => Boolean(props.settings.vapidPublicKey))

watch(
  () => props.settings.vapidPublicKey,
  () => {
    enabled.value = Boolean(getStoredPushSubscriptionId())
  },
)

async function onToggle(next: boolean) {
  if (!vapidReady.value || busy.value) return
  busy.value = true
  try {
    if (next) await subscribe()
    else await unsubscribe()
    enabled.value = next
  } catch (error) {
    enabled.value = Boolean(getStoredPushSubscriptionId())
    toast.error(error instanceof Error ? error.message : problemMessage(error, t))
  } finally {
    busy.value = false
  }
}

async function subscribe() {
  const key = props.settings.vapidPublicKey
  if (!key) throw new Error('VAPID not configured')
  if (
    !('Notification' in window) ||
    !('serviceWorker' in navigator) ||
    !('PushManager' in window)
  ) {
    throw new Error(t('settings.push.unsupported'))
  }
  const permission = await Notification.requestPermission()
  if (permission !== 'granted') throw new Error(t('settings.push.permissionDenied'))

  const registration = await navigator.serviceWorker.ready
  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(key),
  })
  const json = subscription.toJSON()
  const endpoint = json.endpoint
  const p256dh = json.keys?.p256dh
  const auth = json.keys?.auth
  if (!endpoint || !p256dh || !auth) throw new Error(t('settings.push.subscribeFailed'))

  const result = await upsert.mutateAsync({ endpoint, p256dh, auth })
  setStoredPushSubscriptionId(result.id)
  toast.success(t('settings.push.enabled'))
}

async function unsubscribe() {
  const id = getStoredPushSubscriptionId()
  if (id) {
    try {
      await remove.mutateAsync(id)
    } catch {
      // still clear local state if the server already dropped it
    }
  }
  if ('serviceWorker' in navigator) {
    const registration = await navigator.serviceWorker.ready
    const subscription = await registration.pushManager.getSubscription()
    await subscription?.unsubscribe()
  }
  setStoredPushSubscriptionId(null)
  toast.success(t('settings.push.disabled'))
}
</script>

<template>
  <section class="grid gap-4" aria-labelledby="settings-push">
    <h2 id="settings-push" class="font-display text-xl font-semibold tracking-tight">
      {{ t('settings.push.title') }}
    </h2>
    <p class="text-sm text-muted-foreground">{{ t('settings.push.description') }}</p>

    <div class="rounded-2xl border bg-card p-4 shadow-xs">
      <p v-if="!vapidReady" class="text-sm text-muted-foreground">
        {{ t('settings.push.notConfigured') }}
      </p>
      <div v-else class="flex items-center justify-between gap-4">
        <div class="min-w-0">
          <Label for="web-push-toggle" class="text-base">{{ t('settings.push.toggle') }}</Label>
          <p class="mt-1 text-sm text-muted-foreground">{{ t('settings.push.toggleHint') }}</p>
        </div>
        <Switch
          id="web-push-toggle"
          :model-value="enabled"
          :disabled="busy"
          data-testid="web-push-switch"
          @update:model-value="onToggle"
        />
      </div>
    </div>
  </section>
</template>
