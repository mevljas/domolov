<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useEventListener } from '@vueuse/core'
import { CheckCheck, Eye, Home, Languages, LogOut, Monitor, Moon, Plus, Sun } from '@lucide/vue'
import { problemMessage } from '@/api/problem'
import { useHomeSearch, useMarkAllSeen } from '@/features/homes/api'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '@/shared/components/ui/command'
import { isCommandPaletteShortcut, useCommandPalette } from '@/shared/composables/useCommandPalette'
import { useLocale } from '@/shared/composables/useLocale'
import { useTheme } from '@/shared/composables/useTheme'
import { useToast } from '@/shared/composables/useToast'
import { NAV_ITEMS } from '@/shared/navigation'
import { THEME_PREFERENCES } from '@/stores/preferences'

const emit = defineEmits<{ signOut: [] }>()

const { t } = useI18n()
const router = useRouter()
const toast = useToast()
const { open, hide, toggle } = useCommandPalette()
const { setTheme } = useTheme()
const { locales, setLocale } = useLocale()
const markAllSeen = useMarkAllSeen()

const searchQuery = ref('')
const homeSearch = useHomeSearch(searchQuery)

const THEME_ICONS = { system: Monitor, light: Sun, dark: Moon } as const

useEventListener(window, 'keydown', (event: KeyboardEvent) => {
  if (!isCommandPaletteShortcut(event)) return
  event.preventDefault()
  toggle()
})

watch(open, (isOpen) => {
  if (!isOpen) searchQuery.value = ''
})

const themeItems = computed(() =>
  THEME_PREFERENCES.map((value) => ({
    value,
    icon: THEME_ICONS[value],
    label: t('command.useTheme', { theme: t(`theme.${value}`).toLocaleLowerCase() }),
  })),
)

const homeHits = computed(() => homeSearch.data.value?.items ?? [])
const showHomes = computed(() => searchQuery.value.trim().length >= 2)

function run(action: () => void | Promise<unknown>) {
  hide()
  void action()
}

function onSearchInput(event: Event) {
  const target = event.target as HTMLInputElement | null
  searchQuery.value = target?.value ?? ''
}

async function markSeen() {
  try {
    const result = await markAllSeen.mutateAsync()
    toast.success(t('command.markAllSeenDone', { count: result.count }))
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}
</script>

<template>
  <CommandDialog
    v-model:open="open"
    :title="t('command.title')"
    :description="t('command.description')"
  >
    <CommandInput
      :placeholder="t('command.placeholder')"
      :aria-label="t('command.placeholder')"
      @input="onSearchInput"
    />
    <CommandList class="max-h-[min(24rem,60vh)]">
      <CommandEmpty>{{ t('command.empty') }}</CommandEmpty>
      <CommandGroup :heading="t('command.groups.navigation')">
        <CommandItem
          v-for="item in NAV_ITEMS"
          :key="item.key"
          :value="`nav:${item.key}`"
          :data-testid="`command-nav-${item.key}`"
          @select="run(() => router.push(item.to))"
        >
          <component :is="item.icon" aria-hidden="true" />
          {{ t(item.labelKey) }}
        </CommandItem>
      </CommandGroup>
      <CommandSeparator />
      <CommandGroup :heading="t('command.groups.actions')">
        <CommandItem
          value="action:new-watch"
          data-testid="command-new-watch"
          @select="run(() => router.push({ name: 'watches', query: { new: '1' } }))"
        >
          <Plus aria-hidden="true" />
          {{ t('command.newWatch') }}
        </CommandItem>
        <CommandItem
          value="action:mark-all-seen"
          data-testid="command-mark-all-seen"
          @select="run(() => markSeen())"
        >
          <Eye aria-hidden="true" />
          {{ t('command.markAllSeen') }}
        </CommandItem>
      </CommandGroup>
      <CommandSeparator v-if="showHomes" />
      <CommandGroup v-if="showHomes" :heading="t('command.groups.homes')">
        <CommandItem
          v-for="home in homeHits"
          :key="home.id"
          :value="`home:${home.id}:${home.title}`"
          @select="run(() => router.push({ name: 'home', params: { id: home.id } }))"
        >
          <Home aria-hidden="true" />
          <span class="truncate">{{ home.title }}</span>
        </CommandItem>
        <CommandItem
          v-if="homeHits.length === 0 && !homeSearch.isFetching.value"
          value="home:none"
          disabled
        >
          <CheckCheck aria-hidden="true" />
          {{ t('command.noHomes') }}
        </CommandItem>
      </CommandGroup>
      <CommandSeparator />
      <CommandGroup :heading="t('command.groups.preferences')">
        <CommandItem
          v-for="item in themeItems"
          :key="item.value"
          :value="`theme:${item.value}`"
          @select="run(() => setTheme(item.value))"
        >
          <component :is="item.icon" aria-hidden="true" />
          {{ item.label }}
        </CommandItem>
        <CommandItem
          v-for="code in locales"
          :key="code"
          :value="`locale:${code}`"
          @select="run(() => setLocale(code))"
        >
          <Languages aria-hidden="true" />
          {{ t('command.useLanguage', { language: t(`locale.${code}`) }) }}
        </CommandItem>
      </CommandGroup>
      <CommandSeparator />
      <CommandGroup :heading="t('command.groups.account')">
        <CommandItem value="account:sign-out" @select="run(() => emit('signOut'))">
          <LogOut aria-hidden="true" />
          {{ t('auth.signOut') }}
        </CommandItem>
      </CommandGroup>
    </CommandList>
  </CommandDialog>
</template>
