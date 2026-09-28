<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { MoreHorizontal } from '@lucide/vue'
import { BOOKMARK_STAGES, type BookmarkStage, type HomeSummary } from '@/api/types'
import { Button } from '@/shared/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu'
import { useFormat } from '@/shared/composables/useFormat'
import { stageLabelKey } from './board'

const props = defineProps<{
  home: HomeSummary
  currentStage: BookmarkStage
}>()

const emit = defineEmits<{
  dragStart: [home: HomeSummary, event: DragEvent]
  move: [home: HomeSummary, stage: BookmarkStage]
}>()

const { t } = useI18n()
const format = useFormat()

const notePreview = computed(() => {
  const note = props.home.bookmark?.note?.trim()
  if (!note) return null
  return note.length > 80 ? `${note.slice(0, 80)}…` : note
})

const otherStages = computed(() => BOOKMARK_STAGES.filter((stage) => stage !== props.currentStage))

function onDragStart(event: DragEvent) {
  event.dataTransfer?.setData('text/plain', props.home.id)
  event.dataTransfer!.effectAllowed = 'move'
  emit('dragStart', props.home, event)
}
</script>

<template>
  <article
    draggable="true"
    class="group cursor-grab rounded-xl border bg-card p-3 shadow-xs transition-shadow hover:shadow-sm active:cursor-grabbing"
    data-testid="bookmark-card"
    @dragstart="onDragStart"
  >
    <div class="flex items-start gap-2">
      <RouterLink
        :to="{ name: 'home', params: { id: home.id } }"
        class="min-w-0 flex-1 space-y-1 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <h3 class="line-clamp-2 text-sm leading-snug font-semibold text-foreground">
          {{ home.title }}
        </h3>
        <p class="font-display text-base text-terracotta-foreground tabular-nums">
          {{ format.price(home.price, home.currency) }}
        </p>
        <p v-if="notePreview" class="line-clamp-2 text-xs text-muted-foreground">
          {{ notePreview }}
        </p>
      </RouterLink>

      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button
            variant="ghost"
            size="icon"
            class="size-8 shrink-0 opacity-70 group-hover:opacity-100"
            :aria-label="t('bookmarks.moveToStage')"
            data-testid="bookmark-stage-menu"
          >
            <MoreHorizontal class="size-4" aria-hidden="true" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" class="w-52">
          <DropdownMenuLabel>{{ t('bookmarks.moveToStage') }}</DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            v-for="stage in otherStages"
            :key="stage"
            :data-testid="`bookmark-move-${stage}`"
            @select="emit('move', home, stage)"
          >
            {{ t(stageLabelKey(stage)) }}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  </article>
</template>
