<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  BOOKMARK_STAGES,
  HOME_SORTS,
  type BookmarkStage,
  type DismissedFilter,
  type HomeSort,
  type HomesQuery,
  type MarketStatus,
  type Watch,
} from '@/api/types'
import { Button } from '@/shared/components/ui/button'
import { Checkbox } from '@/shared/components/ui/checkbox'
import { Input } from '@/shared/components/ui/input'
import { Label } from '@/shared/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select'
import { HOME_FILTER_DEFAULTS } from '../composables/useHomeFilters'

const props = defineProps<{
  modelValue: HomesQuery
  watches?: Watch[]
}>()

const emit = defineEmits<{
  'update:modelValue': [HomesQuery]
}>()

const { t } = useI18n()

const draft = reactive({
  Q: '',
  Status: HOME_FILTER_DEFAULTS.Status as MarketStatus,
  Dismissed: HOME_FILTER_DEFAULTS.Dismissed as DismissedFilter,
  Sort: HOME_FILTER_DEFAULTS.Sort as HomeSort,
  WatchId: '' as string,
  Stage: '' as string,
  Unseen: false,
  Bookmarked: false,
  Reposted: false,
  HasDuplicates: false,
  MinPrice: '' as string,
  MaxPrice: '' as string,
  MinSize: '' as string,
  MaxSize: '' as string,
  MaxPricePerM2: '' as string,
  MinRooms: '' as string,
  PropertyType: '',
})

function syncFromProps(value: HomesQuery) {
  draft.Q = value.Q ?? ''
  draft.Status = value.Status ?? HOME_FILTER_DEFAULTS.Status!
  draft.Dismissed = value.Dismissed ?? HOME_FILTER_DEFAULTS.Dismissed!
  draft.Sort = value.Sort ?? HOME_FILTER_DEFAULTS.Sort!
  draft.WatchId = value.WatchId ?? ''
  draft.Stage = value.Stage ?? ''
  draft.Unseen = value.Unseen ?? false
  draft.Bookmarked = value.Bookmarked ?? false
  draft.Reposted = value.Reposted ?? false
  draft.HasDuplicates = value.HasDuplicates ?? false
  draft.MinPrice = value.MinPrice !== undefined ? String(value.MinPrice) : ''
  draft.MaxPrice = value.MaxPrice !== undefined ? String(value.MaxPrice) : ''
  draft.MinSize = value.MinSize !== undefined ? String(value.MinSize) : ''
  draft.MaxSize = value.MaxSize !== undefined ? String(value.MaxSize) : ''
  draft.MaxPricePerM2 = value.MaxPricePerM2 !== undefined ? String(value.MaxPricePerM2) : ''
  draft.MinRooms = value.MinRooms !== undefined ? String(value.MinRooms) : ''
  draft.PropertyType = value.PropertyType ?? ''
}

watch(
  () => props.modelValue,
  (value) => syncFromProps(value),
  { immediate: true, deep: true },
)

const statuses: MarketStatus[] = ['onMarket', 'offMarket', 'all']
const dismissedOptions: DismissedFilter[] = ['exclude', 'include', 'only']

const stageOptions = computed(() => [
  { value: '', label: t('homes.filter.anyStage') },
  ...BOOKMARK_STAGES.map((s) => ({ value: s, label: t(`homes.bookmark.stages.${s}`) })),
])

function parseOptionalNumber(raw: string): number | undefined {
  const trimmed = raw.trim()
  if (!trimmed) return undefined
  const n = Number(trimmed)
  return Number.isFinite(n) ? n : undefined
}

function setFlag(
  key: 'Unseen' | 'Bookmarked' | 'Reposted' | 'HasDuplicates',
  value: boolean | 'indeterminate',
) {
  draft[key] = value === true
}

function onUnseen(value: boolean | 'indeterminate') {
  setFlag('Unseen', value)
}

function onBookmarked(value: boolean | 'indeterminate') {
  setFlag('Bookmarked', value)
}

function onReposted(value: boolean | 'indeterminate') {
  setFlag('Reposted', value)
}

function onHasDuplicates(value: boolean | 'indeterminate') {
  setFlag('HasDuplicates', value)
}

function buildQuery(): HomesQuery {
  const next: HomesQuery = {
    Status: draft.Status,
    Dismissed: draft.Dismissed,
    Sort: draft.Sort,
  }
  const q = draft.Q.trim()
  if (q) next.Q = q
  if (draft.WatchId) next.WatchId = draft.WatchId
  if (draft.Stage) next.Stage = draft.Stage as BookmarkStage
  if (draft.Unseen) next.Unseen = true
  if (draft.Bookmarked) next.Bookmarked = true
  if (draft.Reposted) next.Reposted = true
  if (draft.HasDuplicates) next.HasDuplicates = true
  const minPrice = parseOptionalNumber(draft.MinPrice)
  const maxPrice = parseOptionalNumber(draft.MaxPrice)
  const minSize = parseOptionalNumber(draft.MinSize)
  const maxSize = parseOptionalNumber(draft.MaxSize)
  const maxPpm = parseOptionalNumber(draft.MaxPricePerM2)
  const minRooms = parseOptionalNumber(draft.MinRooms)
  if (minPrice !== undefined) next.MinPrice = minPrice
  if (maxPrice !== undefined) next.MaxPrice = maxPrice
  if (minSize !== undefined) next.MinSize = minSize
  if (maxSize !== undefined) next.MaxSize = maxSize
  if (maxPpm !== undefined) next.MaxPricePerM2 = maxPpm
  if (minRooms !== undefined) next.MinRooms = minRooms
  const propertyType = draft.PropertyType.trim()
  if (propertyType) next.PropertyType = propertyType
  return next
}

function apply() {
  emit('update:modelValue', buildQuery())
}

function reset() {
  syncFromProps({ ...HOME_FILTER_DEFAULTS })
  emit('update:modelValue', { ...HOME_FILTER_DEFAULTS })
}
</script>

<template>
  <form class="grid gap-5" @submit.prevent="apply">
    <div class="grid gap-2">
      <Label for="home-filter-q">{{ t('homes.filter.search') }}</Label>
      <Input
        id="home-filter-q"
        v-model="draft.Q"
        type="search"
        :placeholder="t('homes.filter.searchPlaceholder')"
        autocomplete="off"
      />
    </div>

    <div v-if="watches?.length" class="grid gap-2">
      <Label for="home-filter-watch">{{ t('homes.filter.watch') }}</Label>
      <Select
        :model-value="draft.WatchId || 'all'"
        @update:model-value="(v) => (draft.WatchId = v === 'all' || v == null ? '' : String(v))"
      >
        <SelectTrigger id="home-filter-watch" class="w-full">
          <SelectValue :placeholder="t('homes.filter.allWatches')" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{{ t('homes.filter.allWatches') }}</SelectItem>
          <SelectItem v-for="w in watches" :key="w.id" :value="w.id">
            {{ w.name }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div class="grid gap-2">
      <Label for="home-filter-status">{{ t('homes.filter.status') }}</Label>
      <Select
        :model-value="draft.Status"
        @update:model-value="(v) => v && (draft.Status = v as MarketStatus)"
      >
        <SelectTrigger id="home-filter-status" class="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem v-for="status in statuses" :key="status" :value="status">
            {{ t(`homes.filter.status_${status}`) }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div class="grid gap-2">
      <Label for="home-filter-sort">{{ t('homes.sort') }}</Label>
      <Select
        :model-value="draft.Sort"
        @update:model-value="(v) => v && (draft.Sort = v as HomeSort)"
      >
        <SelectTrigger id="home-filter-sort" class="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem v-for="sort in HOME_SORTS" :key="sort" :value="sort!">
            {{ t(`homes.sorts.${sort}`) }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div class="grid gap-2">
      <Label for="home-filter-dismissed">{{ t('homes.filter.dismissed') }}</Label>
      <Select
        :model-value="draft.Dismissed"
        @update:model-value="(v) => v && (draft.Dismissed = v as DismissedFilter)"
      >
        <SelectTrigger id="home-filter-dismissed" class="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem v-for="opt in dismissedOptions" :key="opt" :value="opt">
            {{ t(`homes.filter.dismissed_${opt}`) }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div class="grid gap-2">
      <Label for="home-filter-stage">{{ t('homes.filter.stage') }}</Label>
      <Select
        :model-value="draft.Stage || 'any'"
        @update:model-value="(v) => (draft.Stage = v === 'any' || v == null ? '' : String(v))"
      >
        <SelectTrigger id="home-filter-stage" class="w-full">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem
            v-for="opt in stageOptions"
            :key="opt.value || 'any'"
            :value="opt.value || 'any'"
          >
            {{ opt.label }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <fieldset class="grid gap-3">
      <legend class="text-sm font-medium">{{ t('homes.filter.flags') }}</legend>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :checked="draft.Unseen" @update:checked="onUnseen" />
        {{ t('homes.filter.unseen') }}
      </label>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :checked="draft.Bookmarked" @update:checked="onBookmarked" />
        {{ t('homes.filter.bookmarked') }}
      </label>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :checked="draft.Reposted" @update:checked="onReposted" />
        {{ t('homes.filter.reposted') }}
      </label>
      <label class="flex items-center gap-2 text-sm">
        <Checkbox :checked="draft.HasDuplicates" @update:checked="onHasDuplicates" />
        {{ t('homes.filter.hasDuplicates') }}
      </label>
    </fieldset>

    <div class="grid grid-cols-2 gap-3">
      <div class="grid gap-2">
        <Label for="home-filter-min-price">{{ t('homes.filter.minPrice') }}</Label>
        <Input id="home-filter-min-price" v-model="draft.MinPrice" inputmode="numeric" />
      </div>
      <div class="grid gap-2">
        <Label for="home-filter-max-price">{{ t('homes.filter.maxPrice') }}</Label>
        <Input id="home-filter-max-price" v-model="draft.MaxPrice" inputmode="numeric" />
      </div>
      <div class="grid gap-2">
        <Label for="home-filter-min-size">{{ t('homes.filter.minSize') }}</Label>
        <Input id="home-filter-min-size" v-model="draft.MinSize" inputmode="decimal" />
      </div>
      <div class="grid gap-2">
        <Label for="home-filter-max-size">{{ t('homes.filter.maxSize') }}</Label>
        <Input id="home-filter-max-size" v-model="draft.MaxSize" inputmode="decimal" />
      </div>
      <div class="grid gap-2">
        <Label for="home-filter-ppm">{{ t('homes.filter.maxPricePerM2') }}</Label>
        <Input id="home-filter-ppm" v-model="draft.MaxPricePerM2" inputmode="numeric" />
      </div>
      <div class="grid gap-2">
        <Label for="home-filter-rooms">{{ t('homes.filter.minRooms') }}</Label>
        <Input id="home-filter-rooms" v-model="draft.MinRooms" inputmode="decimal" />
      </div>
    </div>

    <div class="grid gap-2">
      <Label for="home-filter-type">{{ t('homes.filter.propertyType') }}</Label>
      <Input
        id="home-filter-type"
        v-model="draft.PropertyType"
        :placeholder="t('homes.filter.propertyTypePlaceholder')"
        autocomplete="off"
      />
    </div>

    <div class="flex flex-wrap gap-2 pt-1">
      <Button type="submit">{{ t('homes.filter.apply') }}</Button>
      <Button type="button" variant="outline" @click="reset">{{ t('homes.filter.reset') }}</Button>
    </div>
  </form>
</template>
