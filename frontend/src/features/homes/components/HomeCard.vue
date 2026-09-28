<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Bookmark, BookmarkCheck, EyeOff } from '@lucide/vue'
import type { HomeSummary } from '@/api/types'
import {
  useDismissHome,
  useRemoveBookmark,
  useRestoreHome,
  useSetBookmark,
} from '@/features/homes/api'
import { useToast } from '@/shared/composables/useToast'
import RooflinePattern from '@/shared/components/brand/RooflinePattern.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { useLocale } from '@/shared/composables/useLocale'
import { useReducedMotion } from '@/shared/composables/useReducedMotion'
import { formatPercentChange, formatPrice } from '@/shared/lib/format'

const props = defineProps<{
  home: HomeSummary
}>()

const emit = defineEmits<{
  dismiss: [homeId: string]
}>()

const { t } = useI18n()
const { locale } = useLocale()
const reducedMotion = useReducedMotion()
const setBookmark = useSetBookmark()
const removeBookmark = useRemoveBookmark()
const dismissHome = useDismissHome()
const restoreHome = useRestoreHome()
const toast = useToast()

const imageBroken = ref(false)

const priceLabel = computed(() =>
  formatPrice(props.home.price, locale.value, { currency: props.home.currency }),
)
const previousPriceLabel = computed(() =>
  props.home.previousPrice != null
    ? formatPrice(props.home.previousPrice, locale.value, { currency: props.home.currency })
    : null,
)
const percentDrop = computed(() => {
  if (props.home.previousPrice == null || props.home.price == null) return null
  if (props.home.price >= props.home.previousPrice) return null
  return formatPercentChange(props.home.previousPrice, props.home.price, locale.value)
})

const isBookmarked = computed(() => props.home.bookmark != null)
const transitionName = computed(() => (reducedMotion.value ? undefined : `home-${props.home.id}`))

function onImageError() {
  imageBroken.value = true
}

async function toggleBookmark(event: Event) {
  event.preventDefault()
  event.stopPropagation()
  if (isBookmarked.value) {
    await removeBookmark.mutateAsync(props.home.id)
  } else {
    await setBookmark.mutateAsync({
      homeId: props.home.id,
      stage: 'interested',
      note: null,
    })
  }
}

async function onDismiss(event: Event) {
  event.preventDefault()
  event.stopPropagation()
  emit('dismiss', props.home.id)
  const homeId = props.home.id
  await dismissHome.mutateAsync(homeId)
  toast.withUndo(t('homes.actions.dismissed'), () => {
    void restoreHome.mutateAsync(homeId)
  })
}
</script>

<template>
  <article
    class="group relative overflow-hidden rounded-2xl border bg-card shadow-sm transition-shadow hover:shadow-md"
  >
    <RouterLink
      :to="{ name: 'home', params: { id: home.id } }"
      class="absolute inset-0 z-0 rounded-2xl focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
      :aria-label="home.title"
    />

    <div class="pointer-events-none relative aspect-[4/3] overflow-hidden bg-muted">
      <img
        v-if="home.imageUrl && !imageBroken"
        :src="home.imageUrl"
        :alt="home.title"
        width="640"
        height="480"
        loading="lazy"
        decoding="async"
        class="size-full object-cover"
        :style="transitionName ? { viewTransitionName: transitionName } : undefined"
        @error="onImageError"
      />
      <div
        v-else
        class="flex size-full items-end justify-center bg-gradient-to-br from-sage-soft/80 to-terracotta-soft/60 p-4"
        :style="transitionName ? { viewTransitionName: transitionName } : undefined"
      >
        <RooflinePattern class="h-16 w-full text-primary/40" />
      </div>

      <div
        class="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/55 via-black/10 to-transparent"
        aria-hidden="true"
      />

      <span
        v-if="home.isUnseen"
        class="absolute top-3 left-3 size-2.5 rounded-full bg-terracotta shadow ring-2 ring-white"
        :title="t('homes.badge.unseen')"
        :aria-label="t('homes.badge.unseen')"
      />

      <div
        v-if="home.offMarketAt"
        class="absolute top-3 right-3 rotate-6 rounded-sm bg-foreground px-2 py-0.5 text-[10px] font-bold tracking-wider text-background uppercase shadow"
      >
        {{ t('homes.badge.offMarket') }}
      </div>

      <div class="absolute right-3 bottom-3 left-3 flex flex-wrap items-end justify-between gap-2">
        <div class="min-w-0">
          <p
            class="font-display text-2xl leading-none font-semibold text-white tabular-nums drop-shadow"
          >
            {{ priceLabel }}
          </p>
          <p
            v-if="percentDrop && previousPriceLabel"
            class="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-white/90"
          >
            <span class="line-through opacity-80">{{ previousPriceLabel }}</span>
            <Badge class="border-0 bg-terracotta text-white">{{ percentDrop }}</Badge>
          </p>
        </div>
      </div>
    </div>

    <div class="pointer-events-none relative z-10 space-y-3 p-4">
      <div class="min-w-0">
        <h2 class="truncate text-base font-semibold tracking-tight">{{ home.title }}</h2>
        <p v-if="home.location" class="mt-0.5 truncate text-sm text-muted-foreground">
          {{ home.location }}
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-1.5">
        <Badge v-if="home.repostCount > 0" variant="secondary">
          {{
            home.repostCount === 1
              ? t('homes.badge.repost')
              : t('homes.badge.reposts', { n: home.repostCount })
          }}
        </Badge>
        <Badge v-if="home.listingCount > 1" variant="outline">
          {{ t('homes.badge.ads', { n: home.listingCount }) }}
        </Badge>
        <Badge v-if="home.bookmark" class="border-0 bg-terracotta-soft text-terracotta-foreground">
          {{ t(`homes.bookmark.stages.${home.bookmark.stage}`) }}
        </Badge>
        <Badge v-if="home.sizeText || home.sizeM2" variant="outline">
          {{ home.sizeText ?? `${home.sizeM2} m²` }}
        </Badge>
        <Badge v-if="home.rooms" variant="outline">{{ home.rooms }}</Badge>
      </div>

      <div class="pointer-events-auto flex items-center gap-1 pt-1">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          class="gap-1.5"
          :aria-pressed="isBookmarked"
          :aria-label="isBookmarked ? t('bookmarks.remove') : t('bookmarks.add')"
          data-testid="home-bookmark"
          @click="toggleBookmark"
        >
          <BookmarkCheck v-if="isBookmarked" class="size-4" aria-hidden="true" />
          <Bookmark v-else class="size-4" aria-hidden="true" />
          <span class="sr-only sm:not-sr-only">
            {{ isBookmarked ? t('bookmarks.remove') : t('bookmarks.add') }}
          </span>
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          class="gap-1.5 text-muted-foreground"
          :aria-label="t('homes.actions.dismiss')"
          data-testid="home-dismiss"
          @click="onDismiss"
        >
          <EyeOff class="size-4" aria-hidden="true" />
          <span class="sr-only sm:not-sr-only">{{ t('homes.actions.dismiss') }}</span>
        </Button>
      </div>
    </div>
  </article>
</template>
