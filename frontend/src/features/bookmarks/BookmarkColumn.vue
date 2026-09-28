<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BookmarkColumn, BookmarkStage, HomeSummary } from '@/api/types'
import { Badge } from '@/shared/components/ui/badge'
import { stageLabelKey } from './board'
import BookmarkCard from './BookmarkCard.vue'

const props = defineProps<{
  column: BookmarkColumn
}>()

const emit = defineEmits<{
  dragStart: [home: HomeSummary, event: DragEvent]
  drop: [stage: BookmarkStage]
  move: [home: HomeSummary, stage: BookmarkStage]
}>()

const { t } = useI18n()
const over = ref(false)

function onDragOver(event: DragEvent) {
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  over.value = true
}

function onDragLeave() {
  over.value = false
}

function onDrop(event: DragEvent) {
  event.preventDefault()
  over.value = false
  emit('drop', props.column.stage)
}
</script>

<template>
  <section
    class="flex w-[min(80vw,17.5rem)] shrink-0 snap-center flex-col gap-3 rounded-2xl border bg-muted/30 p-3 transition-colors lg:w-auto lg:min-w-0"
    :class="over && 'border-primary bg-sage-soft/40'"
    :aria-label="t(stageLabelKey(column.stage))"
    data-testid="bookmark-column"
    @dragover="onDragOver"
    @dragleave="onDragLeave"
    @drop="onDrop"
  >
    <header class="flex items-center justify-between gap-2 px-1">
      <h2 class="font-display text-sm font-semibold tracking-tight">
        {{ t(stageLabelKey(column.stage)) }}
      </h2>
      <Badge variant="secondary" class="tabular-nums">{{ column.homes.length }}</Badge>
    </header>

    <div class="flex min-h-24 flex-1 flex-col gap-2">
      <BookmarkCard
        v-for="home in column.homes"
        :key="home.id"
        :home="home"
        :current-stage="column.stage"
        @drag-start="(h, e) => emit('dragStart', h, e)"
        @move="(h, stage) => emit('move', h, stage)"
      />
      <p
        v-if="column.homes.length === 0"
        class="rounded-xl border border-dashed px-3 py-6 text-center text-xs text-muted-foreground"
      >
        {{ t('bookmarks.columnEmpty') }}
      </p>
    </div>
  </section>
</template>
