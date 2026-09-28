<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Building2, ExternalLink, House } from '@lucide/vue'
import { useListingDetail } from '@/features/homes/api'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import RooflinePattern from '@/shared/components/brand/RooflinePattern.vue'
import DetailSkeleton from '@/shared/components/skeletons/DetailSkeleton.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import { useFormat } from '@/shared/composables/useFormat'
import PriceChart from './components/PriceChart.vue'

const props = defineProps<{ id: string }>()

const { t } = useI18n()
const format = useFormat()
const detail = useListingDetail(() => props.id)
const imageBroken = ref(false)

const listing = computed(() => detail.data.value)

const attrs = computed(() => {
  const l = listing.value
  if (!l) return []
  const rows: { key: string; label: string; value: string }[] = []
  if (l.sizeText || l.sizeM2 != null) {
    rows.push({
      key: 'size',
      label: t('homes.attrs.size'),
      value: l.sizeText ?? format.area(l.sizeM2),
    })
  }
  if (l.landSizeText || l.landSizeM2 != null) {
    rows.push({
      key: 'land',
      label: t('homes.attrs.land'),
      value: l.landSizeText ?? format.area(l.landSizeM2),
    })
  }
  if (l.rooms || l.roomCount != null) {
    rows.push({
      key: 'rooms',
      label: t('homes.attrs.rooms'),
      value: l.rooms ?? format.number(l.roomCount, 1),
    })
  }
  if (l.floorText) {
    rows.push({ key: 'floor', label: t('homes.attrs.floor'), value: l.floorText })
  }
  if (l.yearText || l.yearBuilt != null) {
    rows.push({
      key: 'year',
      label: t('homes.attrs.year'),
      value: l.yearText ?? String(l.yearBuilt),
    })
  }
  if (l.pricePerM2 != null) {
    rows.push({
      key: 'ppm',
      label: t('homes.attrs.pricePerM2'),
      value: format.price(l.pricePerM2, l.currency),
    })
  }
  if (l.propertyType) {
    rows.push({ key: 'type', label: t('homes.attrs.type'), value: l.propertyType })
  }
  if (l.location) {
    rows.push({ key: 'location', label: t('homes.attrs.location'), value: l.location })
  }
  return rows
})
</script>

<template>
  <template v-if="detail.isLoading.value">
    <PageHeader
      :title="t('listings.detailTitle')"
      :description="t('listings.detailDescription')"
      back-to="/homes"
      :back-label="t('nav.homes')"
    />
    <DetailSkeleton />
  </template>

  <EmptyState
    v-else-if="detail.isError.value || !listing"
    :icon="Building2"
    :title="t('listings.notFound')"
    :description="t('listings.detailDescription')"
  >
    <template #actions>
      <Button as-child variant="outline">
        <RouterLink to="/homes">{{ t('nav.homes') }}</RouterLink>
      </Button>
      <Button v-if="detail.isError.value" type="button" @click="detail.refetch()">
        {{ t('common.retry') }}
      </Button>
    </template>
  </EmptyState>

  <template v-else>
    <PageHeader
      :eyebrow="listing.externalId"
      :title="listing.title"
      :description="t('listings.detailDescription')"
      :back-to="listing.homeId ? { name: 'home', params: { id: listing.homeId } } : '/homes'"
      :back-label="listing.homeId ? t('homes.actions.viewHome') : t('nav.homes')"
    >
      <template #actions>
        <Button as-child variant="outline">
          <a :href="listing.url" target="_blank" rel="noopener noreferrer">
            <ExternalLink class="size-4" aria-hidden="true" />
            {{ t('listings.openOriginal') }}
          </a>
        </Button>
        <Button v-if="listing.homeId" as-child>
          <RouterLink :to="{ name: 'home', params: { id: listing.homeId } }">
            <House class="size-4" aria-hidden="true" />
            {{ t('homes.actions.viewHome') }}
          </RouterLink>
        </Button>
      </template>
    </PageHeader>

    <div class="mx-auto max-w-3xl space-y-6">
      <div class="relative aspect-[4/3] overflow-hidden rounded-2xl bg-muted shadow-sm">
        <img
          v-if="listing.imageUrl && !imageBroken"
          :src="listing.imageUrl"
          :alt="listing.title"
          width="800"
          height="600"
          loading="eager"
          decoding="async"
          class="size-full object-cover"
          @error="imageBroken = true"
        />
        <div
          v-else
          class="flex size-full items-end justify-center bg-gradient-to-br from-sage-soft/80 to-terracotta-soft/60 p-6"
        >
          <RooflinePattern class="h-20 w-full text-primary/40" />
        </div>
      </div>

      <div class="space-y-2">
        <p class="font-display text-3xl font-semibold text-terracotta-foreground tabular-nums">
          {{ format.price(listing.price, listing.currency) }}
        </p>
        <p v-if="listing.previousPrice != null" class="text-sm text-muted-foreground">
          {{
            t('listings.priceWas', {
              price: format.price(listing.previousPrice, listing.currency),
            })
          }}
        </p>
        <div class="flex flex-wrap gap-2 pt-1">
          <Badge v-if="listing.delistedAt" variant="outline">
            {{ t('homes.timeline.delisted') }}
          </Badge>
          <Badge v-if="listing.providerId" variant="secondary">{{ listing.providerId }}</Badge>
        </div>
      </div>

      <dl
        v-if="attrs.length"
        class="grid gap-4 rounded-2xl border bg-card p-5 shadow-sm sm:grid-cols-2"
      >
        <div v-for="row in attrs" :key="row.key">
          <dt class="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            {{ row.label }}
          </dt>
          <dd class="mt-1 text-base font-medium tabular-nums">{{ row.value }}</dd>
        </div>
        <div>
          <dt class="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            {{ t('homes.timeline.firstSeen') }}
          </dt>
          <dd class="mt-1"><RelativeTime :value="listing.firstSeenAt" /></dd>
        </div>
        <div>
          <dt class="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            {{ t('homes.timeline.lastSeen') }}
          </dt>
          <dd class="mt-1"><RelativeTime :value="listing.lastSeenAt" /></dd>
        </div>
      </dl>

      <section v-if="listing.description" aria-labelledby="listing-description-heading">
        <h2 id="listing-description-heading" class="font-display text-xl font-semibold">
          {{ t('homes.descriptionLabel') }}
        </h2>
        <p class="mt-3 whitespace-pre-wrap text-muted-foreground">{{ listing.description }}</p>
      </section>

      <PriceChart :points="listing.prices" :currency="listing.currency" />

      <section v-if="listing.watches.length" class="space-y-2">
        <h2 class="font-display text-lg font-semibold">{{ t('homes.timeline.watches') }}</h2>
        <ul class="flex flex-wrap gap-2">
          <li v-for="watch in listing.watches" :key="watch.id">
            <Button as-child variant="outline" size="sm">
              <RouterLink :to="{ name: 'watch', params: { id: watch.id } }">
                {{ watch.name }}
              </RouterLink>
            </Button>
          </li>
        </ul>
      </section>
    </div>
  </template>
</template>
