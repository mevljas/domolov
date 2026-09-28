<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { cn } from '@/shared/lib/cn'

export type ScanStatus = 'queued' | 'running' | 'baseline' | 'succeeded' | 'failed'
export type StatusTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'accent'

const props = defineProps<{
  status?: ScanStatus
  tone?: StatusTone
  label?: string
  /** Animate the dot (e.g. while a scan is running). */
  pulse?: boolean
}>()

const { t } = useI18n()

const STATUS_TONES: Record<ScanStatus, StatusTone> = {
  queued: 'neutral',
  running: 'info',
  baseline: 'accent',
  succeeded: 'success',
  failed: 'danger',
}

const TONE_CLASSES: Record<StatusTone, { badge: string; dot: string }> = {
  neutral: { badge: 'bg-muted text-muted-foreground border-border', dot: 'bg-muted-foreground' },
  success: { badge: 'bg-success-soft text-success border-success/20', dot: 'bg-success' },
  warning: {
    badge: 'bg-warning-soft text-warning-foreground border-warning/25',
    dot: 'bg-warning',
  },
  danger: {
    badge: 'bg-destructive-soft text-destructive border-destructive/20',
    dot: 'bg-destructive',
  },
  info: { badge: 'bg-info-soft text-info border-info/20', dot: 'bg-info' },
  accent: {
    badge: 'bg-terracotta-soft text-terracotta-foreground border-terracotta/25',
    dot: 'bg-terracotta',
  },
}

const resolvedTone = computed<StatusTone>(
  () => props.tone ?? (props.status ? STATUS_TONES[props.status] : 'neutral'),
)
const text = computed(() => props.label ?? (props.status ? t(`scans.status.${props.status}`) : ''))
const isPulsing = computed(() => props.pulse ?? props.status === 'running')
const classes = computed(() => TONE_CLASSES[resolvedTone.value])
</script>

<template>
  <span
    :class="
      cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap',
        classes.badge,
      )
    "
    :data-tone="resolvedTone"
  >
    <span class="relative flex size-1.5" aria-hidden="true">
      <span
        v-if="isPulsing"
        :class="
          cn('absolute inline-flex size-full animate-ping rounded-full opacity-60', classes.dot)
        "
      />
      <span :class="cn('relative inline-flex size-1.5 rounded-full', classes.dot)" />
    </span>
    {{ text }}
  </span>
</template>
