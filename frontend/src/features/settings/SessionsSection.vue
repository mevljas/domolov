<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { problemMessage } from '@/api/problem'
import type { SessionListItem } from '@/api/types'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { useFormat } from '@/shared/composables/useFormat'
import { useToast } from '@/shared/composables/useToast'
import { useRevokeSession, useSessions, useSignOutEverywhere } from './api'

const { t } = useI18n()
const router = useRouter()
const toast = useToast()
const format = useFormat()
const sessions = useSessions()
const revoke = useRevokeSession()
const signOutEverywhere = useSignOutEverywhere()

const confirmEverywhere = ref(false)

async function onRevoke(session: SessionListItem) {
  try {
    await revoke.mutateAsync(session.id)
    toast.success(t('settings.sessions.revoked'))
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

async function onSignOutEverywhere() {
  try {
    await signOutEverywhere.mutateAsync()
    await router.push({ name: 'login' })
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

function agentLabel(session: SessionListItem) {
  return session.userAgent?.trim() || t('settings.sessions.unknownDevice')
}
</script>

<template>
  <section class="grid gap-4" aria-labelledby="settings-sessions">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 id="settings-sessions" class="font-display text-xl font-semibold tracking-tight">
          {{ t('settings.sessions.title') }}
        </h2>
        <p class="mt-1 text-sm text-muted-foreground">{{ t('settings.sessions.description') }}</p>
      </div>
      <Button
        variant="outline"
        :disabled="signOutEverywhere.isPending.value"
        data-testid="sign-out-everywhere"
        @click="confirmEverywhere = true"
      >
        {{ t('settings.sessions.signOutEverywhere') }}
      </Button>
    </div>

    <p v-if="sessions.isPending.value" class="text-sm text-muted-foreground">
      {{ t('common.loading') }}
    </p>
    <p v-else-if="sessions.isError.value" class="text-sm text-destructive">
      {{ problemMessage(sessions.error.value, t) }}
    </p>
    <ul v-else class="divide-y overflow-hidden rounded-2xl border bg-card shadow-xs">
      <li
        v-for="session in sessions.data.value ?? []"
        :key="session.id"
        class="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between"
      >
        <div class="min-w-0 space-y-1">
          <div class="flex flex-wrap items-center gap-2">
            <p class="truncate font-medium">{{ agentLabel(session) }}</p>
            <Badge v-if="session.isCurrent" variant="secondary">
              {{ t('settings.sessions.current') }}
            </Badge>
          </div>
          <p class="text-xs text-muted-foreground">
            {{ t('settings.sessions.lastSeen', { when: format.relative(session.lastSeenAt) }) }}
            <span v-if="session.ipAddress"> · {{ session.ipAddress }}</span>
          </p>
        </div>
        <Button
          v-if="!session.isCurrent"
          variant="ghost"
          size="sm"
          :disabled="revoke.isPending.value"
          @click="onRevoke(session)"
        >
          {{ t('settings.sessions.revoke') }}
        </Button>
      </li>
    </ul>

    <ConfirmDialog
      v-model:open="confirmEverywhere"
      :title="t('settings.sessions.signOutEverywhere')"
      :description="t('settings.sessions.signOutEverywhereConfirm')"
      :confirm-label="t('settings.sessions.signOutEverywhere')"
      destructive
      :loading="signOutEverywhere.isPending.value"
      :close-on-confirm="false"
      @confirm="onSignOutEverywhere"
    />
  </section>
</template>
