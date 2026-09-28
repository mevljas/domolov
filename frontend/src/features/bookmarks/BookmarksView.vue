<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Bookmark } from '@lucide/vue'
import type { BookmarkStage, HomeSummary } from '@/api/types'
import { problemMessage } from '@/api/problem'
import { useMoveBookmark } from '@/features/homes/api'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import ListSkeleton from '@/shared/components/skeletons/ListSkeleton.vue'
import { useToast } from '@/shared/composables/useToast'
import { useBookmarkBoard } from './api'
import BookmarkColumn from './BookmarkColumn.vue'
import { isBoardEmpty, sortColumnsByStage } from './board'

const { t } = useI18n()
const toast = useToast()
const board = useBookmarkBoard()
const moveBookmark = useMoveBookmark()

const dragging = ref<HomeSummary | null>(null)

const columns = computed(() => sortColumnsByStage(board.data.value?.columns ?? []))
const empty = computed(() => isBoardEmpty(columns.value))

function onDragStart(home: HomeSummary) {
  dragging.value = home
}

async function onDrop(stage: BookmarkStage) {
  const home = dragging.value
  dragging.value = null
  if (!home || home.bookmark?.stage === stage) return
  try {
    await moveBookmark.mutateAsync({ home, stage })
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}

async function onMove(home: HomeSummary, stage: BookmarkStage) {
  if (home.bookmark?.stage === stage) return
  try {
    await moveBookmark.mutateAsync({ home, stage })
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}
</script>

<template>
  <PageHeader :title="t('bookmarks.title')" :description="t('bookmarks.description')" />

  <ListSkeleton v-if="board.isPending.value" :rows="4" />

  <EmptyState
    v-else-if="board.isError.value"
    :icon="Bookmark"
    :title="t('common.error')"
    :description="problemMessage(board.error.value, t)"
  >
    <template #actions>
      <button
        type="button"
        class="text-sm font-medium text-primary underline"
        @click="board.refetch()"
      >
        {{ t('common.retry') }}
      </button>
    </template>
  </EmptyState>

  <EmptyState
    v-else-if="empty"
    :icon="Bookmark"
    :title="t('bookmarks.empty')"
    :description="t('bookmarks.emptyHint')"
  />

  <div
    v-else
    class="flex snap-x snap-mandatory gap-3 overflow-x-auto pb-4 lg:grid lg:grid-cols-6 lg:gap-4 lg:overflow-visible lg:pb-0"
    data-testid="bookmark-board"
  >
    <BookmarkColumn
      v-for="column in columns"
      :key="column.stage"
      :column="column"
      @drag-start="onDragStart"
      @drop="onDrop"
      @move="onMove"
    />
  </div>
</template>
