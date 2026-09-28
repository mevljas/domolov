<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useEventListener } from '@vueuse/core'
import { Check, Crosshair, X } from '@lucide/vue'
import { problemMessage } from '@/api/problem'
import type { ReviewDecision } from './api'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import { Button } from '@/shared/components/ui/button'
import ListSkeleton from '@/shared/components/skeletons/ListSkeleton.vue'
import { useToast } from '@/shared/composables/useToast'
import { usePossibleMatches, useReviewMatch } from './api'
import MatchSide from './MatchSide.vue'
import MatchSignalsPanel from './MatchSignalsPanel.vue'

const { t } = useI18n()
const toast = useToast()
const matches = usePossibleMatches()
const review = useReviewMatch()

const index = ref(0)

const list = computed(() => matches.data.value ?? [])
const current = computed(() => list.value[index.value] ?? null)
const queueLabel = computed(() =>
  list.value.length === 0
    ? t('matches.count', 0)
    : t('matches.queuePosition', { current: index.value + 1, total: list.value.length }),
)

watch(list, (items) => {
  if (index.value >= items.length) index.value = Math.max(0, items.length - 1)
})

function move(delta: number) {
  if (list.value.length === 0) return
  index.value = (index.value + delta + list.value.length) % list.value.length
}

async function decide(state: ReviewDecision) {
  const match = current.value
  if (!match || review.isPending.value) return
  try {
    await review.mutateAsync({ id: match.id, state })
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

useEventListener(window, 'keydown', (event: KeyboardEvent) => {
  if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.altKey) return
  const target = event.target as HTMLElement | null
  if (target && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName))) {
    return
  }
  const key = event.key.toLowerCase()
  if (key === 's') {
    event.preventDefault()
    void decide('confirmed')
  } else if (key === 'd') {
    event.preventDefault()
    void decide('rejected')
  } else if (event.key === 'ArrowRight') {
    event.preventDefault()
    move(1)
  } else if (event.key === 'ArrowLeft') {
    event.preventDefault()
    move(-1)
  }
})
</script>

<template>
  <PageHeader :title="t('matches.title')" :description="t('matches.description')">
    <template #actions>
      <p v-if="list.length > 0" class="text-sm text-muted-foreground tabular-nums">
        {{ queueLabel }}
      </p>
    </template>
  </PageHeader>

  <ListSkeleton v-if="matches.isPending.value" :rows="3" />

  <EmptyState
    v-else-if="matches.isError.value"
    :icon="Crosshair"
    :title="t('common.error')"
    :description="problemMessage(matches.error.value, t)"
  >
    <template #actions>
      <button
        type="button"
        class="text-sm font-medium text-primary underline"
        @click="matches.refetch()"
      >
        {{ t('common.retry') }}
      </button>
    </template>
  </EmptyState>

  <EmptyState
    v-else-if="!current"
    :icon="Check"
    :title="t('matches.allCaughtUp')"
    :description="t('matches.allCaughtUpHint')"
  />

  <div v-else class="grid gap-6" data-testid="match-review">
    <div class="grid gap-4 lg:grid-cols-[1fr_minmax(12rem,16rem)_1fr] lg:items-start">
      <MatchSide
        :listing="current.listing"
        side="left"
        :other="current.homePrimaryListing"
        :signals="current.signals"
        :label="t('matches.newListing')"
      />
      <MatchSignalsPanel :score="current.score" :signals="current.signals" />
      <MatchSide
        :listing="current.homePrimaryListing"
        side="right"
        :other="current.listing"
        :signals="current.signals"
        :label="t('matches.existingHome')"
      />
    </div>

    <div class="flex flex-wrap items-center justify-center gap-3">
      <Button
        variant="outline"
        size="lg"
        :disabled="review.isPending.value"
        data-testid="match-reject"
        @click="decide('rejected')"
      >
        <X aria-hidden="true" />
        {{ t('matches.different') }}
        <kbd class="ml-1 rounded border bg-muted px-1.5 py-0.5 text-[0.65rem] font-medium">D</kbd>
      </Button>
      <Button
        size="lg"
        :disabled="review.isPending.value"
        data-testid="match-confirm"
        @click="decide('confirmed')"
      >
        <Check aria-hidden="true" />
        {{ t('matches.sameHome') }}
        <kbd
          class="ml-1 rounded border border-primary-foreground/30 bg-primary-foreground/15 px-1.5 py-0.5 text-[0.65rem] font-medium"
          >S</kbd
        >
      </Button>
    </div>
  </div>
</template>
