<script setup lang="ts">
import { useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { BellRing, Eye, EyeOff, LoaderCircle, Sparkles, TrendingDown } from '@lucide/vue'
import BrandMark from '@/shared/components/brand/BrandMark.vue'
import BrandWordmark from '@/shared/components/brand/BrandWordmark.vue'
import RooflinePattern from '@/shared/components/brand/RooflinePattern.vue'
import LocaleSwitcher from '@/shared/components/LocaleSwitcher.vue'
import ThemeToggle from '@/shared/components/ThemeToggle.vue'
import { Button } from '@/shared/components/ui/button'
import { Input } from '@/shared/components/ui/input'
import { Label } from '@/shared/components/ui/label'
import { useLoginForm } from './useLoginForm'

const { t } = useI18n()
const { password, showPassword, errorMessage, isSubmitting, isLocked, submit } = useLoginForm()

const passwordId = useId()
const errorId = `${passwordId}-error`

const heroPoints = [
  { icon: Sparkles, key: 'auth.heroPoint1' },
  { icon: TrendingDown, key: 'auth.heroPoint2' },
  { icon: BellRing, key: 'auth.heroPoint3' },
] as const
</script>

<template>
  <div class="grid min-h-dvh lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]">
    <aside
      class="relative isolate hidden overflow-hidden bg-forest text-paper lg:flex lg:flex-col lg:justify-between lg:p-12 xl:p-16 dark:bg-[#132820]"
      :aria-label="t('common.tagline')"
    >
      <div
        class="absolute inset-0 -z-10 bg-[radial-gradient(120%_80%_at_0%_0%,rgb(143_169_143/0.28),transparent_60%)]"
        aria-hidden="true"
      />
      <RooflinePattern
        :rows="3"
        class="absolute inset-x-0 bottom-0 -z-10 h-[46%] w-full text-paper opacity-[0.09]"
      />

      <div class="flex items-center gap-3">
        <BrandMark class="size-11 drop-shadow" variant="onDark" decorative />
        <BrandWordmark tone="inverse" class="mt-1 text-[2rem]" decorative />
        <span class="sr-only">{{ t('common.appName') }}</span>
      </div>

      <div class="max-w-xl py-12">
        <p class="mb-5 text-sm font-semibold tracking-[0.18em] text-paper-muted uppercase">
          {{ t('common.tagline') }}
        </p>
        <p
          class="font-display text-5xl leading-[1.05] font-semibold tracking-tight text-balance xl:text-6xl"
        >
          {{ t('auth.heroTitle') }}
        </p>
        <p class="mt-6 max-w-lg text-lg leading-relaxed text-paper-muted">
          {{ t('auth.heroBody') }}
        </p>
        <ul class="mt-10 space-y-4">
          <li v-for="point in heroPoints" :key="point.key" class="flex items-center gap-3.5">
            <span
              class="grid size-9 shrink-0 place-items-center rounded-xl bg-paper/10 ring-1 ring-paper/15"
            >
              <component
                :is="point.icon"
                class="size-[1.125rem] text-[#F0B79C]"
                aria-hidden="true"
              />
            </span>
            <span class="text-base font-medium">{{ t(point.key) }}</span>
          </li>
        </ul>
      </div>

      <p class="text-sm text-paper-muted">{{ t('auth.selfHosted') }}</p>
    </aside>

    <main
      id="main"
      tabindex="-1"
      class="relative isolate flex flex-col px-5 py-6 outline-none sm:px-10"
    >
      <RooflinePattern
        :rows="2"
        class="absolute inset-x-0 bottom-0 -z-10 h-40 w-full text-sage opacity-25 lg:hidden"
      />
      <div class="flex items-center justify-between gap-2">
        <div class="flex items-center gap-2.5 lg:invisible">
          <BrandMark class="size-9" decorative />
          <BrandWordmark class="mt-0.5 text-[1.5rem]" decorative />
        </div>
        <div class="flex items-center gap-1">
          <LocaleSwitcher side="bottom" align="end" />
          <ThemeToggle side="bottom" align="end" />
        </div>
      </div>

      <div class="flex flex-1 items-center justify-center py-10">
        <div class="w-full max-w-sm">
          <p class="mb-8 text-lg text-muted-foreground lg:hidden">
            {{ t('common.tagline') }}
          </p>

          <div class="rounded-2xl border bg-card p-7 shadow-lg sm:p-8">
            <h1 class="text-3xl font-semibold tracking-tight">
              {{ t('auth.heading') }}
            </h1>
            <p class="mt-2 text-muted-foreground">{{ t('auth.subheading') }}</p>

            <form
              class="mt-8 grid gap-5"
              method="post"
              action="/api/session"
              novalidate
              :aria-busy="isSubmitting || undefined"
              @submit.prevent="submit"
            >
              <div class="grid gap-2">
                <Label :for="passwordId" class="text-sm font-semibold">{{
                  t('auth.password')
                }}</Label>
                <div class="relative">
                  <Input
                    :id="passwordId"
                    v-model="password"
                    name="password"
                    :type="showPassword ? 'text' : 'password'"
                    autocomplete="current-password"
                    autocapitalize="off"
                    spellcheck="false"
                    autofocus
                    class="h-11 pr-12 text-base"
                    :aria-invalid="errorMessage ? true : undefined"
                    :aria-describedby="errorMessage ? errorId : undefined"
                    data-testid="password-input"
                  />
                  <button
                    type="button"
                    class="absolute inset-y-0 right-0 grid w-11 place-items-center rounded-r-md text-muted-foreground transition-colors hover:text-foreground"
                    :aria-label="showPassword ? t('a11y.hidePassword') : t('a11y.showPassword')"
                    :aria-pressed="showPassword"
                    :aria-controls="passwordId"
                    data-testid="toggle-password"
                    @click="showPassword = !showPassword"
                  >
                    <EyeOff v-if="showPassword" class="size-[1.125rem]" aria-hidden="true" />
                    <Eye v-else class="size-[1.125rem]" aria-hidden="true" />
                  </button>
                </div>
                <div aria-live="assertive">
                  <Transition name="rise">
                    <p
                      v-if="errorMessage"
                      :id="errorId"
                      role="alert"
                      class="rounded-lg border border-destructive/25 bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive"
                      data-testid="login-error"
                    >
                      {{ errorMessage }}
                    </p>
                  </Transition>
                </div>
              </div>

              <Button
                type="submit"
                size="lg"
                class="h-11 w-full text-base font-semibold"
                :disabled="isSubmitting || isLocked"
                data-testid="login-submit"
              >
                <LoaderCircle v-if="isSubmitting" class="size-5 animate-spin" aria-hidden="true" />
                {{ isSubmitting ? t('auth.submitting') : t('auth.submit') }}
              </Button>
            </form>
          </div>

          <p class="mt-8 text-center text-sm text-muted-foreground lg:hidden">
            {{ t('auth.selfHosted') }}
          </p>
        </div>
      </div>
    </main>
  </div>
</template>
