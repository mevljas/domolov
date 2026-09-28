<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemMessage } from '@/api/problem'
import { Button } from '@/shared/components/ui/button'
import { useFormat } from '@/shared/composables/useFormat'
import { useToast } from '@/shared/composables/useToast'
import { useRunCleanup, useStorage } from './api'

const { t } = useI18n()
const toast = useToast()
const format = useFormat()
const storage = useStorage()
const cleanup = useRunCleanup()

const maxTableBytes = computed(() =>
  Math.max(1, ...(storage.data.value?.tables.map((table) => table.bytes) ?? [1])),
)

async function runCleanup() {
  try {
    await cleanup.mutateAsync()
    toast.success(t('settings.storage.cleanupStarted'))
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

function barWidth(bytes: number) {
  return `${Math.max(2, Math.round((bytes / maxTableBytes.value) * 100))}%`
}
</script>

<template>
  <section class="grid gap-4" aria-labelledby="settings-storage">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 id="settings-storage" class="font-display text-xl font-semibold tracking-tight">
          {{ t('settings.storage.title') }}
        </h2>
        <p class="mt-1 text-sm text-muted-foreground">{{ t('settings.storage.description') }}</p>
      </div>
      <Button :disabled="cleanup.isPending.value" data-testid="run-cleanup" @click="runCleanup">
        {{ t('settings.storage.runCleanup') }}
      </Button>
    </div>

    <p v-if="storage.isPending.value" class="text-sm text-muted-foreground">
      {{ t('common.loading') }}
    </p>
    <p v-else-if="storage.isError.value" class="text-sm text-destructive">
      {{ problemMessage(storage.error.value, t) }}
    </p>
    <div v-else-if="storage.data.value" class="grid gap-4 rounded-2xl border bg-card p-4 shadow-xs">
      <div class="flex flex-wrap gap-6">
        <div>
          <p class="text-xs text-muted-foreground">{{ t('settings.storage.database') }}</p>
          <p class="font-display text-2xl tabular-nums">
            {{ format.bytes(storage.data.value.databaseBytes) }}
          </p>
        </div>
        <div>
          <p class="text-xs text-muted-foreground">{{ t('settings.storage.browserProfile') }}</p>
          <p class="font-display text-2xl tabular-nums">
            {{ format.bytes(storage.data.value.lastCleanup?.browserProfileBytes) }}
          </p>
        </div>
        <div>
          <p class="text-xs text-muted-foreground">{{ t('settings.storage.lastCleanup') }}</p>
          <p class="font-medium">
            {{
              storage.data.value.lastCleanup
                ? format.relative(
                    storage.data.value.lastCleanup.finishedAt ??
                      storage.data.value.lastCleanup.startedAt,
                  )
                : '—'
            }}
          </p>
        </div>
        <div>
          <p class="text-xs text-muted-foreground">{{ t('settings.storage.nextCleanup') }}</p>
          <p class="font-medium">
            {{
              storage.data.value.nextCleanupAt
                ? format.relative(storage.data.value.nextCleanupAt)
                : '—'
            }}
          </p>
        </div>
      </div>

      <ul class="grid gap-3">
        <li v-for="table in storage.data.value.tables" :key="table.name" class="grid gap-1.5">
          <div class="flex justify-between gap-3 text-sm">
            <span class="font-medium">{{ table.name }}</span>
            <span class="text-muted-foreground tabular-nums">
              {{ format.bytes(table.bytes) }}
              ·
              {{ t('settings.storage.rows', { n: format.number(table.approximateRows) }) }}
            </span>
          </div>
          <div class="h-2 overflow-hidden rounded-full bg-muted" role="presentation">
            <div class="h-full rounded-full bg-primary" :style="{ width: barWidth(table.bytes) }" />
          </div>
        </li>
      </ul>
    </div>
  </section>
</template>
