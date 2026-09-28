<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ExternalLink, Play, Trash2 } from '@lucide/vue'
import type { Watch } from '@/api/types'
import { useWatchCommands } from '@/features/watches/composables/useWatchCommands'
import { urlHost } from '@/features/watches/lib/urlHost'
import ScanStatusBadge from '@/features/scans/components/ScanStatusBadge.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { Switch } from '@/shared/components/ui/switch'
import { describeCron } from '@/shared/lib/cron'
import { isAppLocale } from '@/shared/lib/locale'
import { useFormat } from '@/shared/composables/useFormat'

const props = defineProps<{ watch: Watch }>()

const { t, locale } = useI18n()
const format = useFormat()
const { updateWatch, deleteWatch, runNow, setPaused, run, remove } = useWatchCommands(
  () => props.watch,
)

const confirmOpen = ref(false)
const appLocale = computed(() => (isAppLocale(locale.value) ? locale.value : 'sl'))
const host = computed(() => urlHost(props.watch.searchUrl))
const schedule = computed(() => describeCron(props.watch.cron, appLocale.value))
const blockedUntil = computed(() => props.watch.cloudflareBlockedUntil)
const isCooling = computed(() => {
  if (!blockedUntil.value) return false
  return new Date(blockedUntil.value).getTime() > Date.now()
})

function onDelete() {
  return remove(() => {
    confirmOpen.value = false
  })
}
</script>

<template>
  <article
    class="rounded-2xl border bg-card p-5 shadow-xs transition-shadow hover:shadow-sm"
    data-testid="watch-card"
  >
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0">
        <RouterLink
          :to="{ name: 'watch', params: { id: watch.id } }"
          class="font-display text-xl font-semibold tracking-tight text-foreground hover:text-primary"
        >
          {{ watch.name }}
        </RouterLink>
        <p class="mt-1 truncate text-sm text-muted-foreground">{{ host }}</p>
      </div>
      <div class="flex items-center gap-2">
        <Badge v-if="watch.isPaused" variant="secondary">{{ t('watches.paused') }}</Badge>
        <label class="inline-flex items-center gap-2 text-sm text-muted-foreground">
          <span class="sr-only">{{
            watch.isPaused ? t('watches.resume') : t('watches.pause')
          }}</span>
          <Switch
            :model-value="!watch.isPaused"
            :disabled="updateWatch.isPending.value"
            data-testid="watch-pause-switch"
            @update:model-value="(v) => setPaused(!v)"
          />
        </label>
      </div>
    </div>

    <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-2">
      <div>
        <dt class="text-muted-foreground">{{ t('schedule.title') }}</dt>
        <dd class="text-foreground">{{ schedule }}</dd>
      </div>
      <div>
        <dt class="text-muted-foreground">{{ t('common.status') }}</dt>
        <dd class="flex flex-wrap items-center gap-2">
          <ScanStatusBadge v-if="watch.lastScan" :status="watch.lastScan.status" />
          <span v-else class="text-muted-foreground">—</span>
          <span class="text-muted-foreground">
            {{ t('watches.listingsCount', watch.listingCount) }}
          </span>
        </dd>
      </div>
      <div>
        <dt class="text-muted-foreground">{{ t('watches.lastScan') }}</dt>
        <dd>
          <RelativeTime :value="watch.lastScannedAt" />
        </dd>
      </div>
      <div>
        <dt class="text-muted-foreground">{{ t('watches.nextRun') }}</dt>
        <dd>
          <RelativeTime v-if="!watch.isPaused" :value="watch.nextRunAt" />
          <span v-else class="text-muted-foreground">{{ t('watches.paused') }}</span>
        </dd>
      </div>
    </dl>

    <p
      v-if="isCooling && blockedUntil"
      class="mt-3 rounded-lg border border-warning/25 bg-warning-soft px-3 py-2 text-sm text-warning-foreground"
    >
      {{ t('cloudflare.cooling', { until: format.dateTime(blockedUntil) }) }}
    </p>

    <div class="mt-4 flex flex-wrap gap-2">
      <Button
        type="button"
        size="sm"
        variant="outline"
        :disabled="runNow.isPending.value"
        data-testid="watch-run-now"
        @click="run"
      >
        <Play aria-hidden="true" />
        {{ t('watches.runNow') }}
      </Button>
      <Button type="button" size="sm" variant="ghost" as-child>
        <a :href="watch.searchUrl" target="_blank" rel="noopener noreferrer">
          <ExternalLink aria-hidden="true" />
          {{ t('watches.openUrl') }}
        </a>
      </Button>
      <Button
        type="button"
        size="sm"
        variant="ghost"
        class="text-destructive"
        @click="confirmOpen = true"
      >
        <Trash2 aria-hidden="true" />
        {{ t('common.delete') }}
      </Button>
    </div>

    <ConfirmDialog
      v-model:open="confirmOpen"
      :title="t('watches.confirmDeleteTitle')"
      :description="t('watches.confirmDelete')"
      :confirm-text="watch.name"
      :confirm-label="t('common.delete')"
      destructive
      :loading="deleteWatch.isPending.value"
      :close-on-confirm="false"
      @confirm="onDelete"
    />
  </article>
</template>
