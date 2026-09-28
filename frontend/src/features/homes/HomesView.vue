<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Filter, House, LoaderCircle, Radar, X } from '@lucide/vue'
import { HOME_SORTS, type HomeSort, type HomesQuery } from '@/api/types'
import { useHomesFeed, useMarkAllSeen } from '@/features/homes/api'
import { useWatches } from '@/features/watches/api'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import CardGridSkeleton from '@/shared/components/skeletons/CardGridSkeleton.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet'
import HomeCard from './components/HomeCard.vue'
import HomeFilters from './components/HomeFilters.vue'
import { useHomeFilters } from './composables/useHomeFilters'

const { t } = useI18n()
const filtersOpen = ref(false)

const { data: watches, isLoading: watchesLoading } = useWatches()
const watchName = computed(() => {
  const id = filters.value.WatchId
  if (!id) return null
  return watches.value?.find((w) => w.id === id)?.name ?? null
})

const { filters, chips, hasActiveFilters, setFilters, clearChip, resetFilters } = useHomeFilters({
  watchName,
})

const feed = useHomesFeed(filters)
const markAllSeen = useMarkAllSeen()

const homes = computed(() => feed.data.value?.pages.flatMap((p) => p.items) ?? [])
const total = computed(() => feed.data.value?.pages[0]?.total ?? 0)
const noWatches = computed(() => !watchesLoading.value && (watches.value?.length ?? 0) === 0)

async function onFiltersUpdate(next: HomesQuery) {
  await setFilters(next)
  filtersOpen.value = false
}

async function onSortChange(value: unknown) {
  if (typeof value !== 'string') return
  await setFilters({ Sort: value as HomeSort })
}
</script>

<template>
  <PageHeader :title="t('homes.title')" :description="t('homes.description')">
    <template #actions>
      <Button type="button" variant="outline" class="sm:hidden" @click="filtersOpen = true">
        <Filter class="size-4" aria-hidden="true" />
        {{ t('homes.openFilters') }}
        <Badge v-if="hasActiveFilters" variant="secondary" class="ml-1">
          {{ chips.length }}
        </Badge>
      </Button>
      <Button
        type="button"
        variant="outline"
        :disabled="markAllSeen.isPending.value"
        @click="markAllSeen.mutate()"
      >
        {{ t('homes.markAllSeen') }}
      </Button>
    </template>
  </PageHeader>

  <div class="grid gap-8 lg:grid-cols-[16rem_1fr]">
    <aside class="hidden sm:block">
      <div class="sticky top-20 rounded-2xl border bg-card p-4 shadow-sm">
        <h2 class="mb-4 text-sm font-semibold tracking-wide text-muted-foreground uppercase">
          {{ t('homes.filters') }}
        </h2>
        <HomeFilters
          :model-value="filters"
          :watches="watches ?? []"
          @update:model-value="onFiltersUpdate"
        />
      </div>
    </aside>

    <div class="min-w-0 space-y-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <p class="text-sm text-muted-foreground" aria-live="polite">
          {{ t('homes.resultCount', { n: total }) }}
        </p>
        <div class="flex items-center gap-2">
          <label for="homes-sort" class="sr-only">{{ t('homes.sort') }}</label>
          <Select :model-value="filters.Sort ?? 'newest'" @update:model-value="onSortChange">
            <SelectTrigger id="homes-sort" class="w-[11rem]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem v-for="sort in HOME_SORTS" :key="sort" :value="sort!">
                {{ t(`homes.sorts.${sort}`) }}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      <div v-if="chips.length" class="flex flex-wrap gap-2" :aria-label="t('homes.activeFilters')">
        <button
          v-for="chip in chips"
          :key="chip.id"
          type="button"
          class="inline-flex items-center gap-1 rounded-full border bg-background px-2.5 py-1 text-xs font-medium transition-colors hover:bg-muted"
          @click="clearChip(chip)"
        >
          {{ chip.label }}
          <X class="size-3" aria-hidden="true" />
          <span class="sr-only">{{ t('homes.filter.removeChip', { label: chip.label }) }}</span>
        </button>
        <Button type="button" variant="ghost" size="sm" @click="resetFilters">
          {{ t('homes.filter.reset') }}
        </Button>
      </div>

      <CardGridSkeleton v-if="feed.isLoading.value || watchesLoading" />

      <EmptyState
        v-else-if="feed.isError.value"
        :icon="House"
        :title="t('common.error')"
        :description="t('errors.unknown')"
      >
        <template #actions>
          <Button type="button" @click="feed.refetch()">{{ t('common.retry') }}</Button>
        </template>
      </EmptyState>

      <EmptyState
        v-else-if="noWatches"
        :icon="Radar"
        :title="t('homes.emptyNoWatchesTitle')"
        :description="t('homes.emptyNoWatchesBody')"
      >
        <template #actions>
          <Button as-child>
            <RouterLink to="/watches">{{ t('homes.emptyNoWatchesAction') }}</RouterLink>
          </Button>
        </template>
      </EmptyState>

      <EmptyState
        v-else-if="homes.length === 0"
        :icon="House"
        :title="t('homes.emptyNoResultsTitle')"
        :description="t('homes.emptyNoResultsBody')"
      >
        <template #actions>
          <Button type="button" variant="outline" @click="resetFilters">
            {{ t('homes.emptyNoResultsAction') }}
          </Button>
        </template>
      </EmptyState>

      <template v-else>
        <div class="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          <HomeCard v-for="home in homes" :key="home.id" :home="home" />
        </div>

        <div v-if="feed.hasNextPage.value" class="flex justify-center pt-4">
          <Button
            type="button"
            variant="outline"
            :disabled="feed.isFetchingNextPage.value"
            @click="feed.fetchNextPage()"
          >
            <LoaderCircle
              v-if="feed.isFetchingNextPage.value"
              class="animate-spin"
              aria-hidden="true"
            />
            {{ t('homes.loadMore') }}
          </Button>
        </div>
      </template>
    </div>
  </div>

  <Sheet v-model:open="filtersOpen">
    <SheetContent side="bottom" class="max-h-[92dvh] overflow-y-auto rounded-t-2xl">
      <SheetHeader>
        <SheetTitle>{{ t('homes.filters') }}</SheetTitle>
        <SheetDescription>{{ t('homes.description') }}</SheetDescription>
      </SheetHeader>
      <div class="px-4 pb-6">
        <HomeFilters
          :model-value="filters"
          :watches="watches ?? []"
          @update:model-value="onFiltersUpdate"
        />
      </div>
    </SheetContent>
  </Sheet>
</template>
