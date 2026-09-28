<script lang="ts" setup>
import type { ToasterProps } from 'vue-sonner'
import {
  CircleCheckIcon,
  InfoIcon,
  Loader2Icon,
  OctagonXIcon,
  TriangleAlertIcon,
  XIcon,
} from '@lucide/vue'
import { Toaster as Sonner } from 'vue-sonner'
import { cn } from '@/shared/lib/cn'

const props = defineProps<ToasterProps>()

const toastClasses = {
  toast: 'font-sans !shadow-lg',
  title: '!font-semibold',
  description: '!text-muted-foreground',
  actionButton: '!bg-primary !text-primary-foreground !font-semibold !rounded-md',
  cancelButton: '!bg-muted !text-foreground !rounded-md',
}
</script>

<template>
  <Sonner
    v-bind="props"
    :class="cn('toaster group', props.class)"
    :style="{
      '--normal-bg': 'var(--popover)',
      '--normal-text': 'var(--popover-foreground)',
      '--normal-border': 'var(--border)',
      '--border-radius': 'var(--radius)',
      '--success-bg': 'var(--success-soft)',
      '--success-text': 'var(--success)',
      '--success-border': 'color-mix(in oklab, var(--success) 25%, transparent)',
      '--error-bg': 'var(--destructive-soft)',
      '--error-text': 'var(--destructive)',
      '--error-border': 'color-mix(in oklab, var(--destructive) 25%, transparent)',
      '--warning-bg': 'var(--warning-soft)',
      '--warning-text': 'var(--warning-foreground)',
      '--warning-border': 'color-mix(in oklab, var(--warning) 30%, transparent)',
      '--info-bg': 'var(--info-soft)',
      '--info-text': 'var(--info)',
      '--info-border': 'color-mix(in oklab, var(--info) 25%, transparent)',
    }"
    :toast-options="{
      ...props.toastOptions,
      classes: { ...toastClasses, ...props.toastOptions?.classes },
    }"
  >
    <template #success-icon>
      <CircleCheckIcon class="size-4" />
    </template>
    <template #info-icon>
      <InfoIcon class="size-4" />
    </template>
    <template #warning-icon>
      <TriangleAlertIcon class="size-4" />
    </template>
    <template #error-icon>
      <OctagonXIcon class="size-4" />
    </template>
    <template #loading-icon>
      <div>
        <Loader2Icon class="size-4 animate-spin" />
      </div>
    </template>
    <template #close-icon>
      <XIcon class="size-4" />
    </template>
  </Sonner>
</template>
