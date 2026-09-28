<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MatchListing, MatchSignals } from '@/api/types'
import { useFormat } from '@/shared/composables/useFormat'
import { wordDiff } from '@/shared/lib/diff'
import DiffText from './DiffText.vue'

const props = defineProps<{
  listing: MatchListing | null
  side: 'left' | 'right'
  other: MatchListing | null
  signals: MatchSignals
  label: string
}>()

const { t } = useI18n()
const format = useFormat()

const descriptionTokens = computed(() => {
  const left =
    props.side === 'left' ? (props.listing?.description ?? '') : (props.other?.description ?? '')
  const right =
    props.side === 'left' ? (props.other?.description ?? '') : (props.listing?.description ?? '')
  const diff = wordDiff(left, right)
  return props.side === 'left' ? diff.left : diff.right
})

const titleTokens = computed(() => {
  const left = props.side === 'left' ? (props.listing?.title ?? '') : (props.other?.title ?? '')
  const right = props.side === 'left' ? (props.other?.title ?? '') : (props.listing?.title ?? '')
  const diff = wordDiff(left, right)
  return props.side === 'left' ? diff.left : diff.right
})

const attributeRows = computed(() => {
  const keys = [
    'location',
    'propertyType',
    'rooms',
    'sizeText',
    'landSizeText',
    'floorText',
    'yearText',
  ] as const
  return keys.map((key) => {
    const value = props.listing?.[key] ?? null
    const matched = props.signals.matchedAttributes.includes(key)
    const mismatched = props.signals.mismatchedAttributes.includes(key)
    return { key, value, matched, mismatched }
  })
})
</script>

<template>
  <article class="flex min-w-0 flex-col gap-4 rounded-2xl border bg-card p-4 shadow-xs">
    <p class="text-xs font-semibold tracking-[0.14em] text-primary uppercase">{{ label }}</p>

    <div
      class="aspect-[4/3] overflow-hidden rounded-xl bg-muted"
      :class="!listing?.imageUrl && 'grid place-items-center text-sm text-muted-foreground'"
    >
      <img
        v-if="listing?.imageUrl"
        :src="listing.imageUrl"
        :alt="listing.title"
        class="size-full object-cover"
      />
      <span v-else>{{ t('matches.noPhoto') }}</span>
    </div>

    <div class="space-y-2">
      <h3 class="font-display text-lg leading-snug font-semibold">
        <DiffText as="span" :tokens="titleTokens" />
      </h3>
      <p v-if="listing" class="font-display text-xl text-terracotta-foreground tabular-nums">
        {{ format.price(listing.price, listing.currency) }}
      </p>
      <a
        v-if="listing"
        :href="listing.url"
        target="_blank"
        rel="noopener noreferrer"
        class="text-sm font-medium text-primary underline-offset-4 hover:underline"
      >
        {{ t('listings.openOriginal') }}
      </a>
    </div>

    <DiffText v-if="listing?.description" :tokens="descriptionTokens" />
    <p v-else class="text-sm text-muted-foreground">{{ t('matches.noDescription') }}</p>

    <dl class="grid gap-2">
      <div
        v-for="row in attributeRows"
        :key="row.key"
        class="flex items-baseline justify-between gap-3 border-b border-border/60 py-1.5 text-sm last:border-0"
      >
        <dt class="text-muted-foreground">{{ t(`matches.attributes.${row.key}`) }}</dt>
        <dd
          class="text-right font-medium"
          :class="{
            'text-success': row.matched,
            'text-terracotta-foreground': row.mismatched,
          }"
        >
          {{ row.value || '—' }}
        </dd>
      </div>
    </dl>
  </article>
</template>
