<script setup lang="ts">
import { ref, watch as watchRoute } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Plus, Radar } from '@lucide/vue'
import { useWatches } from '@/features/watches/api'
import WatchCard from '@/features/watches/components/WatchCard.vue'
import WatchWizard from '@/features/watches/components/WatchWizard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import ListSkeleton from '@/shared/components/skeletons/ListSkeleton.vue'
import { Button } from '@/shared/components/ui/button'
import { problemMessage } from '@/api/problem'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const watches = useWatches()
const wizardOpen = ref(false)

watchRoute(
  () => route.query.new,
  (value) => {
    if (value !== '1') return
    wizardOpen.value = true
    const next = { ...route.query }
    delete next.new
    void router.replace({ query: next })
  },
  { immediate: true },
)
</script>

<template>
  <PageHeader :title="t('watches.title')" :description="t('watches.description')">
    <template #actions>
      <Button type="button" data-testid="new-watch" @click="wizardOpen = true">
        <Plus aria-hidden="true" />
        {{ t('watches.create') }}
      </Button>
    </template>
  </PageHeader>

  <ListSkeleton v-if="watches.isPending.value" :rows="4" />

  <p v-else-if="watches.isError.value" class="text-sm text-destructive" role="alert">
    {{ problemMessage(watches.error.value, t) }}
  </p>

  <EmptyState
    v-else-if="!watches.data.value?.length"
    :icon="Radar"
    :title="t('watches.emptyTitle')"
    :description="t('watches.emptyDescription')"
  >
    <template #actions>
      <Button type="button" @click="wizardOpen = true">{{ t('watches.emptyCta') }}</Button>
    </template>
  </EmptyState>

  <div v-else class="grid gap-4">
    <WatchCard v-for="watch in watches.data.value" :key="watch.id" :watch="watch" />
  </div>

  <WatchWizard v-model:open="wizardOpen" />
</template>
