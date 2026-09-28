<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { ExternalLink, House, Link2 } from '@lucide/vue'
import { useHomeDetailView } from '@/features/homes/composables/useHomeDetailView'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import RelativeTime from '@/shared/components/RelativeTime.vue'
import RooflinePattern from '@/shared/components/brand/RooflinePattern.vue'
import DetailSkeleton from '@/shared/components/skeletons/DetailSkeleton.vue'
import { Badge } from '@/shared/components/ui/badge'
import { Button } from '@/shared/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog'
import { Input } from '@/shared/components/ui/input'
import { Label } from '@/shared/components/ui/label'
import BookmarkPanel from './components/BookmarkPanel.vue'
import HomeTimeline from './components/HomeTimeline.vue'
import PriceChart from './components/PriceChart.vue'

const props = defineProps<{ id: string }>()

const { t } = useI18n()
const {
  detail,
  format,
  home,
  attrs,
  transitionName,
  imageBroken,
  linkOpen,
  listingIdInput,
  unlinkOpen,
  linkListing,
  unlinkListing,
  onDismiss,
  onRestore,
  requestUnlink,
  confirmUnlink,
  confirmLink,
} = useHomeDetailView(() => props.id)
</script>

<template>
  <template v-if="detail.isLoading.value">
    <PageHeader
      :title="t('homes.detailTitle')"
      :description="t('homes.detailDescription')"
      back-to="/homes"
      :back-label="t('nav.homes')"
    />
    <DetailSkeleton />
  </template>

  <EmptyState
    v-else-if="detail.isError.value || !home"
    :icon="House"
    :title="t('homes.notFound')"
    :description="t('homes.detailDescription')"
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
      :eyebrow="home.location ?? undefined"
      :title="home.title"
      :description="t('homes.detailDescription')"
      back-to="/homes"
      :back-label="t('nav.homes')"
    >
      <template #actions>
        <Button v-if="home.url" as-child variant="outline">
          <a :href="home.url" target="_blank" rel="noopener noreferrer">
            <ExternalLink class="size-4" aria-hidden="true" />
            {{ t('listings.openOriginal') }}
          </a>
        </Button>
        <Button type="button" variant="outline" @click="linkOpen = true">
          <Link2 class="size-4" aria-hidden="true" />
          {{ t('homes.actions.linkListing') }}
        </Button>
        <Button v-if="home.isDismissed" type="button" variant="outline" @click="onRestore">
          {{ t('homes.actions.restore') }}
        </Button>
        <Button v-else type="button" variant="outline" @click="onDismiss">
          {{ t('homes.actions.dismiss') }}
        </Button>
      </template>
    </PageHeader>

    <div
      v-if="home.isDismissed"
      class="mb-6 rounded-2xl border border-warning/30 bg-warning-soft px-4 py-3 text-sm text-warning-foreground"
      role="status"
    >
      {{ t('homes.dismissedBanner') }}
    </div>

    <div class="grid gap-8 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div class="min-w-0 space-y-8">
        <div
          class="relative aspect-[4/3] overflow-hidden rounded-2xl bg-muted shadow-sm sm:aspect-[16/9]"
        >
          <img
            v-if="home.imageUrl && !imageBroken"
            :src="home.imageUrl"
            :alt="home.title"
            width="960"
            height="540"
            loading="eager"
            decoding="async"
            class="size-full object-cover"
            :style="transitionName ? { viewTransitionName: transitionName } : undefined"
            @error="imageBroken = true"
          />
          <div
            v-else
            class="flex size-full items-end justify-center bg-gradient-to-br from-sage-soft/80 to-terracotta-soft/60 p-8"
            :style="transitionName ? { viewTransitionName: transitionName } : undefined"
          >
            <RooflinePattern class="h-24 w-full text-primary/40" />
          </div>

          <div
            class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/60 to-transparent p-5"
          >
            <p
              class="font-display text-3xl font-semibold text-white tabular-nums drop-shadow md:text-4xl"
            >
              {{ format.price(home.price, home.currency) }}
            </p>
            <p
              v-if="
                home.previousPrice != null && home.price != null && home.price < home.previousPrice
              "
              class="mt-1 text-sm text-white/90"
            >
              <span class="line-through">{{
                format.price(home.previousPrice, home.currency)
              }}</span>
              <Badge class="ml-2 border-0 bg-terracotta text-white">
                {{ format.percentChange(home.previousPrice, home.price) }}
              </Badge>
            </p>
          </div>

          <Badge
            v-if="home.offMarketAt"
            class="absolute top-4 right-4 bg-foreground text-background"
          >
            {{ t('homes.badge.offMarket') }}
          </Badge>
        </div>

        <div class="flex flex-wrap gap-2">
          <Badge
            v-if="home.isUnseen"
            class="border-0 bg-terracotta-soft text-terracotta-foreground"
          >
            {{ t('homes.badge.unseen') }}
          </Badge>
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
              {{ t('homes.attrs.timeOnMarket') }}
            </dt>
            <dd class="mt-1 text-base font-medium tabular-nums">
              {{ t('homes.attrs.timeOnMarketDays', { n: detail.data.value!.timeOnMarketDays }) }}
            </dd>
          </div>
          <div>
            <dt class="text-xs font-medium tracking-wide text-muted-foreground uppercase">
              {{ t('homes.timeline.firstSeen') }}
            </dt>
            <dd class="mt-1 text-base font-medium">
              <RelativeTime :value="home.firstSeenAt" />
            </dd>
          </div>
          <div>
            <dt class="text-xs font-medium tracking-wide text-muted-foreground uppercase">
              {{ t('homes.timeline.lastSeen') }}
            </dt>
            <dd class="mt-1 text-base font-medium">
              <RelativeTime :value="home.lastSeenAt" />
            </dd>
          </div>
        </dl>

        <section v-if="detail.data.value?.description" aria-labelledby="home-description-heading">
          <h2 id="home-description-heading" class="font-display text-xl font-semibold">
            {{ t('homes.descriptionLabel') }}
          </h2>
          <p class="mt-3 whitespace-pre-wrap text-muted-foreground">
            {{ detail.data.value.description }}
          </p>
        </section>

        <PriceChart :points="detail.data.value?.priceHistory ?? []" :currency="home.currency" />

        <HomeTimeline
          :listings="detail.data.value?.listings ?? []"
          :home-id="home.id"
          :unlink-loading="unlinkListing.isPending.value"
          @unlink="requestUnlink"
        />

        <section
          v-if="detail.data.value?.possibleMatches.length"
          aria-labelledby="home-matches-heading"
          class="space-y-4"
        >
          <h2 id="home-matches-heading" class="font-display text-xl font-semibold">
            {{ t('homes.matches.title') }}
          </h2>
          <ul class="grid gap-3">
            <li
              v-for="match in detail.data.value.possibleMatches"
              :key="match.id"
              class="rounded-2xl border bg-card p-4 shadow-sm"
            >
              <div class="flex flex-wrap items-center justify-between gap-2">
                <div class="min-w-0">
                  <p class="truncate font-medium">{{ match.listing.title }}</p>
                  <p class="text-sm text-muted-foreground">
                    {{ t('homes.matches.score', { score: format.number(match.score, 2) }) }}
                  </p>
                </div>
                <Button as-child variant="outline" size="sm">
                  <RouterLink to="/matches">{{ t('homes.matches.review') }}</RouterLink>
                </Button>
              </div>
            </li>
          </ul>
        </section>
      </div>

      <aside class="space-y-4 lg:sticky lg:top-20 lg:self-start">
        <BookmarkPanel :home-id="home.id" :bookmark="home.bookmark" />
      </aside>
    </div>
  </template>

  <ConfirmDialog
    v-model:open="unlinkOpen"
    :title="t('homes.actions.unlinkListing')"
    :description="t('homes.actions.unlinkConfirm')"
    :confirm-label="t('homes.actions.unlinkListing')"
    :loading="unlinkListing.isPending.value"
    destructive
    @confirm="confirmUnlink"
  />

  <Dialog v-model:open="linkOpen">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>{{ t('homes.link.title') }}</DialogTitle>
        <DialogDescription>{{ t('homes.link.description') }}</DialogDescription>
      </DialogHeader>
      <form class="grid gap-3" @submit.prevent="confirmLink">
        <Label for="link-listing-id">{{ t('homes.link.listingId') }}</Label>
        <Input
          id="link-listing-id"
          v-model="listingIdInput"
          autocomplete="off"
          spellcheck="false"
          required
        />
        <DialogFooter>
          <Button type="button" variant="outline" @click="linkOpen = false">
            {{ t('common.cancel') }}
          </Button>
          <Button type="submit" :disabled="linkListing.isPending.value">
            {{ t('homes.link.submit') }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
