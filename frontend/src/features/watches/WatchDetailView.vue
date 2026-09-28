<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { ExternalLink, Play, Radar, Trash2 } from '@lucide/vue'
import { problemMessage } from '@/api/problem'
import ScheduleEditor from '@/features/watches/components/ScheduleEditor.vue'
import NotificationRoutesPanel from '@/features/watches/components/NotificationRoutesPanel.vue'
import { useWatchDetail } from '@/features/watches/composables/useWatchDetail'
import ScanStatusBadge from '@/features/scans/components/ScanStatusBadge.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import DetailSkeleton from '@/shared/components/skeletons/DetailSkeleton.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { Switch } from '@/shared/components/ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/components/ui/tabs'
import { useFormat } from '@/shared/composables/useFormat'

const props = defineProps<{ id: string }>()

const { t } = useI18n()
const router = useRouter()
const format = useFormat()
const tab = ref('overview')
const confirmOpen = ref(false)

const {
  watchQuery,
  watchData,
  scansQuery,
  scheduleModel,
  savingSchedule,
  host,
  scheduleSummary,
  isCooling,
  scheduleValid,
  saveSchedule,
  deleteWatch,
  runNow,
  setPaused,
  run,
  remove,
} = useWatchDetail(() => props.id)

function onDelete() {
  return remove(async () => {
    confirmOpen.value = false
    await router.push({ name: 'watches' })
  })
}
</script>

<template>
  <DetailSkeleton v-if="watchQuery.isPending.value && !watchData" />

  <EmptyState
    v-else-if="watchQuery.isError.value || !watchData"
    :icon="Radar"
    :title="t('watches.notFound')"
    :description="problemMessage(watchQuery.error.value, t)"
  />

  <template v-else>
    <PageHeader
      :eyebrow="host"
      :title="watchData.name"
      :description="t('watches.detailDescription')"
      back-to="/watches"
      :back-label="t('nav.watches')"
    >
      <template #actions>
        <label class="inline-flex items-center gap-2 text-sm text-muted-foreground">
          {{ watchData.isPaused ? t('watches.paused') : t('watches.active') }}
          <Switch :model-value="!watchData.isPaused" @update:model-value="(v) => setPaused(!v)" />
        </label>
        <Button
          type="button"
          variant="outline"
          size="sm"
          :disabled="runNow.isPending.value"
          data-testid="watch-run-now"
          @click="run"
        >
          <Play aria-hidden="true" />
          {{ t('watches.runNow') }}
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          class="text-destructive"
          @click="confirmOpen = true"
        >
          <Trash2 aria-hidden="true" />
          {{ t('common.delete') }}
        </Button>
      </template>
    </PageHeader>

    <Tabs v-model="tab" class="gap-6">
      <TabsList class="flex h-auto w-full flex-wrap justify-start gap-1 bg-transparent p-0">
        <TabsTrigger value="overview">{{ t('watches.tabs.overview') }}</TabsTrigger>
        <TabsTrigger value="schedule">{{ t('watches.tabs.schedule') }}</TabsTrigger>
        <TabsTrigger value="notifications">{{ t('watches.tabs.notifications') }}</TabsTrigger>
        <TabsTrigger value="scans">{{ t('watches.tabs.scans') }}</TabsTrigger>
      </TabsList>

      <TabsContent value="overview" class="grid gap-4">
        <dl class="grid gap-4 rounded-2xl border bg-card p-5 shadow-xs sm:grid-cols-2">
          <div class="sm:col-span-2">
            <dt class="text-sm text-muted-foreground">{{ t('watches.searchUrl') }}</dt>
            <dd class="mt-1 break-all">
              <a
                :href="watchData.searchUrl"
                target="_blank"
                rel="noopener noreferrer"
                class="inline-flex items-center gap-1.5 text-primary hover:underline"
              >
                {{ watchData.searchUrl }}
                <ExternalLink class="size-3.5 shrink-0" aria-hidden="true" />
              </a>
            </dd>
          </div>
          <div>
            <dt class="text-sm text-muted-foreground">{{ t('schedule.title') }}</dt>
            <dd class="mt-1 text-foreground">{{ scheduleSummary }}</dd>
          </div>
          <div>
            <dt class="text-sm text-muted-foreground">{{ t('watches.nextRun') }}</dt>
            <dd class="mt-1">
              <RelativeTime v-if="!watchData.isPaused" :value="watchData.nextRunAt" />
              <span v-else>{{ t('watches.paused') }}</span>
            </dd>
          </div>
          <div>
            <dt class="text-sm text-muted-foreground">{{ t('watches.lastScan') }}</dt>
            <dd class="mt-1 flex flex-wrap items-center gap-2">
              <ScanStatusBadge v-if="watchData.lastScan" :status="watchData.lastScan.status" />
              <RelativeTime :value="watchData.lastScannedAt" />
            </dd>
          </div>
          <div>
            <dt class="text-sm text-muted-foreground">{{ t('common.status') }}</dt>
            <dd class="mt-1 flex flex-wrap gap-2">
              <Badge v-if="watchData.isPaused" variant="secondary">{{ t('watches.paused') }}</Badge>
              <Badge variant="outline">
                {{
                  watchData.hasCompletedBaseline
                    ? t('watches.baselineDone')
                    : t('watches.baselinePending')
                }}
              </Badge>
              <span class="text-sm text-muted-foreground">
                {{ t('watches.listingsCount', watchData.listingCount) }}
              </span>
            </dd>
          </div>
        </dl>

        <p
          v-if="isCooling && watchData.cloudflareBlockedUntil"
          class="rounded-xl border border-warning/25 bg-warning-soft px-4 py-3 text-sm text-warning-foreground"
        >
          {{
            t('cloudflare.cooling', {
              until: format.dateTime(watchData.cloudflareBlockedUntil),
            })
          }}
          <span v-if="watchData.cloudflareStrikeCount" class="mt-1 block">
            {{ t('cloudflare.strikes', { count: watchData.cloudflareStrikeCount }) }}
          </span>
        </p>
      </TabsContent>

      <TabsContent value="schedule" class="grid max-w-xl gap-4">
        <ScheduleEditor v-model="scheduleModel" />
        <div>
          <Button type="button" :disabled="!scheduleValid || savingSchedule" @click="saveSchedule">
            {{ t('schedule.save') }}
          </Button>
        </div>
      </TabsContent>

      <TabsContent value="notifications">
        <NotificationRoutesPanel :watch="watchData" />
      </TabsContent>

      <TabsContent value="scans">
        <ul
          v-if="scansQuery.data.value?.items.length"
          class="divide-y overflow-hidden rounded-2xl border bg-card shadow-xs"
        >
          <li
            v-for="scan in scansQuery.data.value.items"
            :key="scan.id"
            class="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
          >
            <div class="flex flex-wrap items-center gap-3">
              <ScanStatusBadge :status="scan.status" />
              <RelativeTime :value="scan.queuedAt" />
              <Badge v-if="scan.isManual" variant="outline">{{ t('scans.manual') }}</Badge>
            </div>
            <p class="text-sm text-muted-foreground">
              {{ t('scans.newListings') }} {{ scan.newCount }} · {{ t('scans.priceChanges') }}
              {{ scan.priceChangeCount }} · {{ t('scans.reposts') }} {{ scan.repostCount }}
            </p>
          </li>
        </ul>
        <p v-else class="text-sm text-muted-foreground">{{ t('scans.empty') }}</p>
      </TabsContent>
    </Tabs>

    <ConfirmDialog
      v-model:open="confirmOpen"
      :title="t('watches.confirmDeleteTitle')"
      :description="t('watches.confirmDelete')"
      :confirm-text="watchData.name"
      :confirm-label="t('common.delete')"
      destructive
      :loading="deleteWatch.isPending.value"
      :close-on-confirm="false"
      @confirm="onDelete"
    />
  </template>
</template>
