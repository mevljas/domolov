<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemMessage } from '@/api/problem'
import { useDashboard } from '@/features/dashboard/api'
import StatTiles from '@/features/dashboard/components/StatTiles.vue'
import HomeStrip from '@/features/dashboard/components/HomeStrip.vue'
import { useMarkAllSeen } from '@/features/homes/api'
import ScanStatusBadge from '@/features/scans/components/ScanStatusBadge.vue'
import { useScanEvents } from '@/features/scans/composables/useScanEvents'
import { urlHost } from '@/features/watches/lib/urlHost'
import PageHeader from '@/shared/components/PageHeader.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import ListSkeleton from '@/shared/components/skeletons/ListSkeleton.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { describeCron } from '@/shared/lib/cron'
import { isAppLocale } from '@/shared/lib/locale'
import { useToast } from '@/shared/composables/useToast'

const { t, locale } = useI18n()
const toast = useToast()
const dashboard = useDashboard()
const markAllSeen = useMarkAllSeen()
useScanEvents()

const appLocale = computed(() => (isAppLocale(locale.value) ? locale.value : 'sl'))
const data = computed(() => dashboard.data.value)

async function onMarkAllSeen() {
  try {
    await markAllSeen.mutateAsync()
    toast.success(t('dashboard.markedAllSeen'))
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}
</script>

<template>
  <PageHeader :title="t('dashboard.title')" :description="t('dashboard.description')" />

  <ListSkeleton v-if="dashboard.isPending.value && !data" :rows="6" />

  <p v-else-if="dashboard.isError.value" class="text-sm text-destructive" role="alert">
    {{ problemMessage(dashboard.error.value, t) || t('dashboard.loadError') }}
  </p>

  <div v-else-if="data" class="grid gap-10">
    <StatTiles :stats="data.stats" />

    <section class="grid gap-3">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 class="font-display text-xl font-semibold">{{ t('dashboard.newest') }}</h2>
        </div>
        <Button
          v-if="data.unseen.length"
          type="button"
          size="sm"
          variant="outline"
          :disabled="markAllSeen.isPending.value"
          @click="onMarkAllSeen"
        >
          {{ t('dashboard.markAllSeen') }}
        </Button>
      </div>
      <HomeStrip :homes="data.unseen" :empty-label="t('dashboard.emptyNewest')" />
    </section>

    <section class="grid gap-3">
      <h2 class="font-display text-xl font-semibold">{{ t('dashboard.priceDrops') }}</h2>
      <HomeStrip :homes="data.priceDrops" :empty-label="t('dashboard.emptyDrops')" />
    </section>

    <section class="grid gap-3">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <h2 class="font-display text-xl font-semibold">{{ t('dashboard.watchHealth') }}</h2>
        <RouterLink to="/watches" class="text-sm font-medium text-primary hover:underline">
          {{ t('nav.watches') }}
        </RouterLink>
      </div>
      <p v-if="!data.watches.length" class="text-sm text-muted-foreground">
        {{ t('dashboard.emptyWatches') }}
      </p>
      <ul v-else class="divide-y overflow-hidden rounded-2xl border bg-card shadow-xs">
        <li
          v-for="watch in data.watches"
          :key="watch.id"
          class="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
        >
          <div class="min-w-0">
            <RouterLink
              :to="{ name: 'watch', params: { id: watch.id } }"
              class="font-semibold text-foreground hover:text-primary"
            >
              {{ watch.name }}
            </RouterLink>
            <p class="truncate text-xs text-muted-foreground">
              {{ urlHost(watch.searchUrl) }} · {{ describeCron(watch.cron, appLocale) }}
            </p>
          </div>
          <div class="flex flex-wrap items-center gap-3 text-sm">
            <Badge v-if="watch.isPaused" variant="secondary">{{ t('watches.paused') }}</Badge>
            <ScanStatusBadge v-if="watch.lastScan" :status="watch.lastScan.status" />
            <span class="text-muted-foreground">
              {{ t('dashboard.nextRun') }}:
              <RelativeTime v-if="!watch.isPaused" :value="watch.nextRunAt" />
              <template v-else>{{ t('watches.paused') }}</template>
            </span>
          </div>
        </li>
      </ul>
    </section>

    <section class="grid gap-3">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <h2 class="font-display text-xl font-semibold">{{ t('dashboard.activeScans') }}</h2>
        <RouterLink to="/scans" class="text-sm font-medium text-primary hover:underline">
          {{ t('nav.scans') }}
        </RouterLink>
      </div>
      <p v-if="!data.activeScans.length" class="text-sm text-muted-foreground">
        {{ t('dashboard.noActiveScans') }}
      </p>
      <ul v-else class="divide-y overflow-hidden rounded-2xl border bg-card shadow-xs">
        <li
          v-for="scan in data.activeScans"
          :key="scan.id"
          class="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
        >
          <div class="flex flex-wrap items-center gap-3">
            <ScanStatusBadge :status="scan.status" pulse />
            <span class="font-medium">{{ scan.watchName }}</span>
          </div>
          <RelativeTime :value="scan.startedAt ?? scan.queuedAt" />
        </li>
      </ul>
    </section>
  </div>
</template>
