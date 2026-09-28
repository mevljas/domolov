<script setup lang="ts">
import type { HomeSummary } from '@/api/types'
import HomePhoto from '@/shared/components/HomePhoto.vue'
import { useFormat } from '@/shared/composables/useFormat'

defineProps<{
  homes: HomeSummary[]
  emptyLabel: string
}>()

const format = useFormat()
</script>

<template>
  <div class="-mx-1 overflow-x-auto pb-1">
    <ul v-if="homes.length" class="flex gap-3 px-1">
      <li v-for="home in homes" :key="home.id" class="w-56 shrink-0">
        <RouterLink
          :to="{ name: 'home', params: { id: home.id } }"
          class="block overflow-hidden rounded-2xl border bg-card shadow-xs transition-shadow hover:shadow-sm"
        >
          <HomePhoto
            :src="home.imageUrl"
            :alt="home.title"
            :width="320"
            :height="200"
            class="aspect-[8/5]"
          />
          <div class="space-y-1 p-3">
            <p class="line-clamp-2 font-display text-sm leading-snug font-semibold">
              {{ home.title }}
            </p>
            <p class="text-sm font-semibold text-terracotta">
              {{ format.price(home.price, home.currency) }}
            </p>
            <p v-if="home.location" class="truncate text-xs text-muted-foreground">
              {{ home.location }}
            </p>
          </div>
        </RouterLink>
      </li>
    </ul>
    <p v-else class="px-1 text-sm text-muted-foreground">{{ emptyLabel }}</p>
  </div>
</template>
