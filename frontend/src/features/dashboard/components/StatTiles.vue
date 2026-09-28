<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { DashboardStats } from '@/api/types'
import { useFormat } from '@/shared/composables/useFormat'

const props = defineProps<{ stats: DashboardStats }>()

const { t } = useI18n()
const format = useFormat()

const tiles = computed(() => [
  {
    key: 'activeWatches',
    label: t('dashboard.stats.activeWatches'),
    value: props.stats.activeWatches,
    to: '/watches',
  },
  {
    key: 'pausedWatches',
    label: t('dashboard.stats.pausedWatches'),
    value: props.stats.pausedWatches,
    to: '/watches',
  },
  {
    key: 'unseenHomes',
    label: t('dashboard.stats.unseenHomes'),
    value: props.stats.unseenHomes,
    to: '/homes',
  },
  {
    key: 'newHomes24h',
    label: t('dashboard.stats.newHomes24h'),
    value: props.stats.newHomes24h,
    to: '/homes',
  },
  {
    key: 'priceDrops7d',
    label: t('dashboard.stats.priceDrops7d'),
    value: props.stats.priceDrops7d,
    to: '/homes',
  },
  {
    key: 'reposts7d',
    label: t('dashboard.stats.reposts7d'),
    value: props.stats.reposts7d,
    to: '/homes',
  },
  {
    key: 'possibleMatches',
    label: t('dashboard.stats.possibleMatches'),
    value: props.stats.possibleMatches,
    to: '/matches',
  },
  {
    key: 'bookmarkedHomes',
    label: t('dashboard.stats.bookmarkedHomes'),
    value: props.stats.bookmarkedHomes,
    to: '/bookmarks',
  },
])
</script>

<template>
  <ul class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4" data-testid="dashboard-stats">
    <li v-for="tile in tiles" :key="tile.key">
      <RouterLink
        :to="tile.to"
        class="block rounded-2xl border bg-card px-4 py-4 shadow-xs transition-shadow hover:shadow-sm"
      >
        <p class="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
          {{ tile.label }}
        </p>
        <p class="mt-2 font-display text-3xl font-semibold tracking-tight text-foreground">
          {{ format.number(tile.value) }}
        </p>
      </RouterLink>
    </li>
  </ul>
</template>
