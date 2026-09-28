<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ScanArtifact, ScanRun } from '@/api/types'
import { artifactUrl, useScanArtifacts } from '@/features/scans/api'
import ScanStatusBadge from '@/features/scans/components/ScanStatusBadge.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet'
import { Badge } from '@/shared/components/ui/badge'
import { useFormat, useTicker } from '@/shared/composables/useFormat'
import { isActiveScan } from '@/api/types'

const props = defineProps<{ scan: ScanRun | null }>()
const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const format = useFormat()
const now = useTicker(1_000)

const scanId = computed(() => props.scan?.id ?? null)
const artifacts = useScanArtifacts(scanId, () => open.value && Boolean(scanId.value))

const elapsed = computed(() => {
  if (!props.scan || !isActiveScan(props.scan.status)) return null
  const start = props.scan.startedAt ?? props.scan.queuedAt
  return format.duration(now.value.getTime() - new Date(start).getTime())
})

function linkFor(artifact: ScanArtifact) {
  return artifactUrl(artifact.scanRunId, artifact.id)
}
</script>

<template>
  <Sheet v-model:open="open">
    <SheetContent
      side="right"
      :close-label="t('common.close')"
      class="w-full overflow-y-auto sm:max-w-lg"
    >
      <SheetHeader>
        <SheetTitle class="font-display text-2xl">{{ t('scans.detailTitle') }}</SheetTitle>
        <SheetDescription v-if="scan">
          {{ scan.watchName }}
        </SheetDescription>
      </SheetHeader>

      <div v-if="scan" class="grid gap-6 px-4 pb-8">
        <div class="flex flex-wrap items-center gap-2">
          <ScanStatusBadge :status="scan.status" />
          <Badge variant="outline">{{
            scan.isManual ? t('scans.manual') : t('scans.scheduled')
          }}</Badge>
          <span v-if="elapsed" class="text-sm text-muted-foreground">{{ elapsed }}</span>
        </div>

        <dl class="grid grid-cols-2 gap-3 text-sm">
          <div>
            <dt class="text-muted-foreground">{{ t('scans.queuedAt') }}</dt>
            <dd><RelativeTime :value="scan.queuedAt" /></dd>
          </div>
          <div>
            <dt class="text-muted-foreground">{{ t('scans.startedAt') }}</dt>
            <dd><RelativeTime :value="scan.startedAt" /></dd>
          </div>
          <div>
            <dt class="text-muted-foreground">{{ t('scans.finishedAt') }}</dt>
            <dd><RelativeTime :value="scan.finishedAt" /></dd>
          </div>
          <div>
            <dt class="text-muted-foreground">{{ t('scans.pages') }}</dt>
            <dd>{{ format.number(scan.pagesScanned) }}</dd>
          </div>
          <div>
            <dt class="text-muted-foreground">{{ t('scans.newListings') }}</dt>
            <dd>{{ format.number(scan.newCount) }}</dd>
          </div>
          <div>
            <dt class="text-muted-foreground">{{ t('scans.priceChanges') }}</dt>
            <dd>{{ format.number(scan.priceChangeCount) }}</dd>
          </div>
          <div>
            <dt class="text-muted-foreground">{{ t('scans.reposts') }}</dt>
            <dd>{{ format.number(scan.repostCount) }}</dd>
          </div>
        </dl>

        <p
          v-if="scan.cloudflareBlocked"
          class="rounded-lg border border-warning/25 bg-warning-soft px-3 py-2 text-sm text-warning-foreground"
        >
          {{ t('scans.cloudflareBlocked') }}
        </p>
        <p
          v-if="!scan.crawlReachedEnd && scan.status === 'succeeded'"
          class="text-sm text-muted-foreground"
        >
          {{ t('scans.crawlIncomplete') }}
        </p>

        <section class="grid gap-2">
          <h3 class="font-semibold">{{ t('scans.errors') }}</h3>
          <p
            v-if="scan.errorSummary"
            class="rounded-lg bg-destructive-soft px-3 py-2 text-sm whitespace-pre-wrap text-destructive"
          >
            {{ scan.errorSummary }}
          </p>
          <p v-else class="text-sm text-muted-foreground">{{ t('scans.noErrors') }}</p>
        </section>

        <section class="grid gap-2">
          <h3 class="font-semibold">{{ t('scans.notifyErrors') }}</h3>
          <p
            v-if="scan.notifyErrorSummary"
            class="rounded-lg bg-warning-soft px-3 py-2 text-sm whitespace-pre-wrap text-warning-foreground"
          >
            {{ scan.notifyErrorSummary }}
          </p>
          <p v-else class="text-sm text-muted-foreground">{{ t('scans.noErrors') }}</p>
        </section>

        <section class="grid gap-3">
          <h3 class="font-semibold">{{ t('scans.artifacts') }}</h3>
          <p v-if="artifacts.isPending.value" class="text-sm text-muted-foreground">
            {{ t('common.loading') }}
          </p>
          <p v-else-if="!artifacts.data.value?.length" class="text-sm text-muted-foreground">
            {{ t('scans.noArtifacts') }}
          </p>
          <ul v-else class="grid gap-3">
            <li
              v-for="artifact in artifacts.data.value"
              :key="artifact.id"
              class="rounded-xl border bg-card p-3"
            >
              <p class="text-sm font-medium">
                {{ t(`scans.kind.${artifact.kind}`) }} · {{ artifact.label }}
              </p>
              <img
                v-if="artifact.kind === 'screenshot'"
                :src="linkFor(artifact)"
                :alt="artifact.label"
                class="mt-2 max-h-64 w-full rounded-lg border object-contain"
              />
              <a
                v-else
                :href="linkFor(artifact)"
                class="mt-2 inline-flex text-sm font-medium text-primary hover:underline"
                download
              >
                {{ t('scans.download') }}
              </a>
            </li>
          </ul>
        </section>
      </div>
    </SheetContent>
  </Sheet>
</template>
