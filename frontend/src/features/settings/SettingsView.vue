<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Settings } from '@lucide/vue'
import { problemMessage } from '@/api/problem'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import DetailSkeleton from '@/shared/components/skeletons/DetailSkeleton.vue'
import AppearanceSection from './AppearanceSection.vue'
import DangerSection from './DangerSection.vue'
import PushSection from './PushSection.vue'
import ServerSection from './ServerSection.vue'
import SessionsSection from './SessionsSection.vue'
import StorageSection from './StorageSection.vue'
import { useServerSettings } from './api'

const { t } = useI18n()
const settings = useServerSettings()
</script>

<template>
  <PageHeader :title="t('settings.title')" :description="t('settings.description')" />

  <DetailSkeleton v-if="settings.isPending.value" />

  <EmptyState
    v-else-if="settings.isError.value"
    :icon="Settings"
    :title="t('common.error')"
    :description="problemMessage(settings.error.value, t)"
  >
    <template #actions>
      <button
        type="button"
        class="text-sm font-medium text-primary underline"
        @click="settings.refetch()"
      >
        {{ t('common.retry') }}
      </button>
    </template>
  </EmptyState>

  <div v-else-if="settings.data.value" class="mx-auto grid max-w-3xl gap-12">
    <AppearanceSection />
    <PushSection :settings="settings.data.value" />
    <SessionsSection />
    <ServerSection :settings="settings.data.value" />
    <StorageSection />
    <DangerSection />
  </div>
</template>
