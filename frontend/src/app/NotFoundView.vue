<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { MapPinned } from '@lucide/vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PageHeader from '@/shared/components/PageHeader.vue'
import { Button } from '@/shared/components/ui/button'

const { t } = useI18n()
</script>

<template>
  <PageHeader eyebrow="404" :title="t('notFound.title')" :description="t('notFound.description')" />
  <EmptyState :icon="MapPinned" :title="t('notFound.title')" :description="t('notFound.body')">
    <template #actions>
      <Button as-child>
        <RouterLink to="/">{{ t('notFound.backHome') }}</RouterLink>
      </Button>
    </template>
  </EmptyState>
</template>
