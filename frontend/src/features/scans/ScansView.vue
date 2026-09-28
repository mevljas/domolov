<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ScanSearch } from '@lucide/vue'
import {
  SCAN_STATUSES,
  isActiveScan,
  type ScanRun,
  type ScanRunStatus,
  type ScansQuery,
} from '@/api/types'
import { problemMessage } from '@/api/problem'
import { useScans } from '@/features/scans/api'
import ScanStatusBadge from '@/features/scans/components/ScanStatusBadge.vue'
import ScanDetailSheet from '@/features/scans/components/ScanDetailSheet.vue'
import { useScanEvents } from '@/features/scans/composables/useScanEvents'
import { useWatches } from '@/features/watches/api'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import ListSkeleton from '@/shared/components/skeletons/ListSkeleton.vue'
import { Badge } from '@/shared/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import { Label } from '@/shared/components/ui/label'
import { useFormat, useTicker } from '@/shared/composables/useFormat'

const { t } = useI18n()
const format = useFormat()
const now = useTicker(1_000)
useScanEvents()

const watches = useWatches()
const watchFilter = ref<string>('all')
const statusFilter = ref<string>('all')
const selected = ref<ScanRun | null>(null)
const sheetOpen = ref(false)

const query = computed<ScansQuery>(() => ({
  page: 1,
  pageSize: 50,
  watchId: watchFilter.value === 'all' ? undefined : watchFilter.value,
  status: statusFilter.value === 'all' ? undefined : (statusFilter.value as ScanRunStatus),
}))

const scans = useScans(query)

function elapsed(scan: ScanRun) {
  if (!isActiveScan(scan.status)) return null
  const start = scan.startedAt ?? scan.queuedAt
  return format.duration(now.value.getTime() - new Date(start).getTime())
}

function openScan(scan: ScanRun) {
  selected.value = scan
  sheetOpen.value = true
}
</script>

<template>
  <PageHeader :title="t('scans.title')" :description="t('scans.description')" />

  <div class="mb-6 flex flex-wrap gap-4">
    <div class="grid min-w-48 gap-1.5">
      <Label>{{ t('scans.filterWatch') }}</Label>
      <Select v-model="watchFilter">
        <SelectTrigger class="w-56">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{{ t('scans.allWatches') }}</SelectItem>
          <SelectItem v-for="watch in watches.data.value ?? []" :key="watch.id" :value="watch.id">
            {{ watch.name }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>
    <div class="grid min-w-40 gap-1.5">
      <Label>{{ t('scans.filterStatus') }}</Label>
      <Select v-model="statusFilter">
        <SelectTrigger class="w-48">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{{ t('scans.allStatuses') }}</SelectItem>
          <SelectItem v-for="status in SCAN_STATUSES" :key="status" :value="status">
            {{ t(`scans.status.${status}`) }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>
  </div>

  <ListSkeleton v-if="scans.isPending.value && !scans.data.value" :rows="6" />

  <p v-else-if="scans.isError.value" class="text-sm text-destructive" role="alert">
    {{ problemMessage(scans.error.value, t) || t('scans.loadError') }}
  </p>

  <EmptyState
    v-else-if="!scans.data.value?.items.length"
    :icon="ScanSearch"
    :title="t('scans.empty')"
  />

  <ul
    v-else
    class="divide-y overflow-hidden rounded-2xl border bg-card shadow-xs"
    data-testid="scans-list"
  >
    <li v-for="scan in scans.data.value.items" :key="scan.id">
      <button
        type="button"
        class="flex w-full flex-wrap items-center justify-between gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/40"
        @click="openScan(scan)"
      >
        <div class="min-w-0 space-y-1">
          <div class="flex flex-wrap items-center gap-2">
            <ScanStatusBadge :status="scan.status" />
            <span class="font-semibold">{{ scan.watchName }}</span>
            <Badge v-if="scan.isManual" variant="outline">{{ t('scans.manual') }}</Badge>
          </div>
          <p class="text-sm text-muted-foreground">
            <RelativeTime :value="scan.queuedAt" />
            <template v-if="elapsed(scan)">
              ·
              {{
                t('scans.statusWithElapsed', {
                  status: t(`scans.status.${scan.status}`),
                  elapsed: elapsed(scan),
                })
              }}
            </template>
          </p>
        </div>
        <p class="text-sm text-muted-foreground">
          {{ t('scans.newListings') }} {{ scan.newCount }} · {{ t('scans.priceChanges') }}
          {{ scan.priceChangeCount }} · {{ t('scans.pages') }} {{ scan.pagesScanned }}
        </p>
      </button>
    </li>
  </ul>

  <ScanDetailSheet v-model:open="sheetOpen" :scan="selected" />
</template>
