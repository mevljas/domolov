<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MatchSignals } from '@/api/types'

const props = defineProps<{
  score: number
  signals: MatchSignals
}>()

const { t } = useI18n()

const bars = computed(() => {
  const entries: { key: string; value: number | null }[] = [
    { key: 'photo', value: props.signals.photoScore },
    { key: 'title', value: props.signals.titleSimilarity },
    { key: 'description', value: props.signals.descriptionSimilarity },
    { key: 'text', value: props.signals.textScore },
    { key: 'attributes', value: props.signals.attributeScore },
  ]
  return entries.filter((entry) => entry.value !== null && entry.value !== undefined)
})

function pct(value: number) {
  return Math.round(Math.min(1, Math.max(0, value)) * 100)
}
</script>

<template>
  <section class="rounded-2xl border bg-card p-4 shadow-xs" :aria-label="t('matches.signalsTitle')">
    <div class="mb-4 flex items-end justify-between gap-3">
      <div>
        <p class="text-xs font-semibold tracking-[0.14em] text-primary uppercase">
          {{ t('matches.score') }}
        </p>
        <p class="font-display text-3xl text-terracotta-foreground tabular-nums">
          {{ pct(score) }}%
        </p>
      </div>
    </div>

    <ul class="grid gap-3">
      <li v-for="bar in bars" :key="bar.key" class="grid gap-1.5">
        <div class="flex justify-between text-xs">
          <span class="text-muted-foreground">{{ t(`matches.signals.${bar.key}`) }}</span>
          <span class="font-medium tabular-nums">{{ pct(bar.value!) }}%</span>
        </div>
        <div class="h-2 overflow-hidden rounded-full bg-muted" role="presentation">
          <div
            class="h-full rounded-full bg-primary transition-[width]"
            :style="{ width: `${pct(bar.value!)}%` }"
          />
        </div>
      </li>
    </ul>
  </section>
</template>
