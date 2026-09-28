<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ScanRunStatus } from '@/api/types'
import StatusBadge, { type ScanStatus } from '@/shared/components/StatusBadge.vue'

const props = defineProps<{
  status: ScanRunStatus
  pulse?: boolean
}>()

const { t } = useI18n()

const known = computed(() => props.status !== 'interrupted')
const badgeStatus = computed(() => props.status as ScanStatus)
</script>

<template>
  <StatusBadge
    v-if="known"
    :status="badgeStatus"
    :pulse="pulse ?? (status === 'running' || status === 'queued')"
  />
  <StatusBadge v-else tone="warning" :label="t('scans.status.interrupted')" :pulse="pulse" />
</template>
