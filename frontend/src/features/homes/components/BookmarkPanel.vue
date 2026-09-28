<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDebounceFn } from '@vueuse/core'
import { BOOKMARK_STAGES, type Bookmark, type BookmarkStage } from '@/api/types'
import { useRemoveBookmark, useSetBookmark } from '@/features/homes/api'
import { Button } from '@/shared/components/ui/button'
import { Label } from '@/shared/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import { Textarea } from '@/shared/components/ui/textarea'

const props = defineProps<{
  homeId: string
  bookmark: Bookmark | null
}>()

const { t } = useI18n()
const setBookmark = useSetBookmark()
const removeBookmark = useRemoveBookmark()

const stage = ref<BookmarkStage>(props.bookmark?.stage ?? 'interested')
const note = ref(props.bookmark?.note ?? '')

watch(
  () => props.bookmark,
  (value) => {
    stage.value = value?.stage ?? 'interested'
    note.value = value?.note ?? ''
  },
)

const persist = useDebounceFn(async () => {
  await setBookmark.mutateAsync({
    homeId: props.homeId,
    stage: stage.value,
    note: note.value.trim() || null,
  })
}, 600)

async function onStageChange(value: unknown) {
  if (typeof value !== 'string') return
  stage.value = value as BookmarkStage
  await setBookmark.mutateAsync({
    homeId: props.homeId,
    stage: stage.value,
    note: note.value.trim() || null,
  })
}

function onNoteInput() {
  void persist()
}

async function remove() {
  await removeBookmark.mutateAsync(props.homeId)
}

async function add() {
  await setBookmark.mutateAsync({
    homeId: props.homeId,
    stage: 'interested',
    note: null,
  })
}
</script>

<template>
  <section
    aria-labelledby="bookmark-panel-heading"
    class="space-y-4 rounded-2xl border bg-card p-5 shadow-sm"
  >
    <div class="flex items-center justify-between gap-2">
      <h2 id="bookmark-panel-heading" class="font-display text-lg font-semibold">
        {{ t('homes.bookmark.title') }}
      </h2>
      <Button v-if="bookmark" type="button" variant="ghost" size="sm" @click="remove">
        {{ t('bookmarks.remove') }}
      </Button>
      <Button v-else type="button" size="sm" @click="add">
        {{ t('bookmarks.add') }}
      </Button>
    </div>

    <template v-if="bookmark">
      <div class="grid gap-2">
        <Label for="bookmark-stage">{{ t('homes.bookmark.stage') }}</Label>
        <Select :model-value="stage" @update:model-value="onStageChange">
          <SelectTrigger id="bookmark-stage" class="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="s in BOOKMARK_STAGES" :key="s" :value="s">
              {{ t(`homes.bookmark.stages.${s}`) }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div class="grid gap-2">
        <Label for="bookmark-note">{{ t('homes.bookmark.note') }}</Label>
        <Textarea
          id="bookmark-note"
          v-model="note"
          rows="3"
          :placeholder="t('homes.bookmark.notePlaceholder')"
          @update:model-value="onNoteInput"
        />
      </div>
    </template>
  </section>
</template>
