<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { ExternalLink } from '@lucide/vue'
import type { HomeListing } from '@/api/types'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import { useFormat } from '@/shared/composables/useFormat'

const props = defineProps<{
  listings: HomeListing[]
  homeId: string
  unlinkLoading?: boolean
}>()

const emit = defineEmits<{
  unlink: [listingId: string]
}>()

const { t } = useI18n()
const format = useFormat()

const ordered = computed(() =>
  [...props.listings].sort(
    (a, b) => new Date(a.firstSeenAt).getTime() - new Date(b.firstSeenAt).getTime(),
  ),
)

function gapDays(prev: HomeListing, next: HomeListing): number | null {
  const end = new Date(prev.delistedAt ?? prev.lastSeenAt).getTime()
  const start = new Date(next.firstSeenAt).getTime()
  const days = Math.round((start - end) / 86_400_000)
  return days > 1 ? days : null
}
</script>

<template>
  <section aria-labelledby="home-timeline-heading" class="space-y-4">
    <h2 id="home-timeline-heading" class="font-display text-xl font-semibold">
      {{ t('homes.timeline.title') }}
    </h2>

    <ol class="relative space-y-0 border-l border-border pl-6">
      <template v-for="(listing, index) in ordered" :key="listing.id">
        <li
          v-if="index > 0 && gapDays(ordered[index - 1]!, listing)"
          class="relative mb-4 -ml-6 list-none py-2 pl-6 text-sm text-muted-foreground"
        >
          <span
            class="absolute top-1/2 left-0 size-2 -translate-x-[4.5px] -translate-y-1/2 rounded-full bg-muted-foreground/40"
            aria-hidden="true"
          />
          {{ t('homes.timeline.gap', { days: gapDays(ordered[index - 1]!, listing) }) }}
        </li>

        <li class="relative mb-6 last:mb-0">
          <span
            class="absolute top-1.5 left-0 size-2.5 -translate-x-[calc(1.5rem+5px)] rounded-full bg-primary ring-4 ring-background"
            aria-hidden="true"
          />

          <article class="rounded-2xl border bg-card p-4 shadow-sm">
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div class="min-w-0 space-y-1">
                <div class="flex flex-wrap items-center gap-2">
                  <Badge variant="secondary">
                    {{ t(`homes.timeline.role.${listing.role}`) }}
                  </Badge>
                  <Badge v-if="listing.delistedAt" variant="outline">
                    {{ t('homes.timeline.delisted') }}
                  </Badge>
                </div>
                <h3 class="truncate font-semibold">
                  <RouterLink
                    :to="{ name: 'listing', params: { id: listing.id } }"
                    class="hover:underline"
                  >
                    {{ listing.title }}
                  </RouterLink>
                </h3>
                <p class="font-display text-lg text-terracotta-foreground tabular-nums">
                  {{ format.price(listing.price, listing.currency) }}
                </p>
              </div>

              <div class="flex shrink-0 gap-1">
                <Button as-child variant="ghost" size="sm">
                  <a :href="listing.url" target="_blank" rel="noopener noreferrer">
                    <ExternalLink class="size-4" aria-hidden="true" />
                    <span class="sr-only">{{ t('listings.openOriginal') }}</span>
                  </a>
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  class="text-muted-foreground"
                  :disabled="unlinkLoading"
                  @click="emit('unlink', listing.id)"
                >
                  {{ t('homes.actions.unlinkListing') }}
                </Button>
              </div>
            </div>

            <dl class="mt-3 grid gap-2 text-sm sm:grid-cols-2">
              <div>
                <dt class="text-muted-foreground">{{ t('homes.timeline.firstSeen') }}</dt>
                <dd>
                  <RelativeTime :value="listing.firstSeenAt" />
                </dd>
              </div>
              <div>
                <dt class="text-muted-foreground">{{ t('homes.timeline.lastSeen') }}</dt>
                <dd>
                  <RelativeTime :value="listing.lastSeenAt" />
                </dd>
              </div>
              <div v-if="listing.delistedAt">
                <dt class="text-muted-foreground">{{ t('homes.timeline.delisted') }}</dt>
                <dd>
                  <time :datetime="listing.delistedAt">{{ format.date(listing.delistedAt) }}</time>
                </dd>
              </div>
              <div v-if="listing.watches.length">
                <dt class="text-muted-foreground">{{ t('homes.timeline.watches') }}</dt>
                <dd>{{ listing.watches.map((w) => w.name).join(', ') }}</dd>
              </div>
            </dl>
          </article>
        </li>
      </template>
    </ol>
  </section>
</template>
