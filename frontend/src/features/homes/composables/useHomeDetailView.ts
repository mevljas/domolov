import { computed, onMounted, ref, watch, type MaybeRefOrGetter, toValue } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  useDismissHome,
  useHomeDetail,
  useLinkListing,
  useMarkSeen,
  useRestoreHome,
  useUnlinkListing,
} from '@/features/homes/api'
import { useFormat } from '@/shared/composables/useFormat'
import { useReducedMotion } from '@/shared/composables/useReducedMotion'

/** Attribute rows, mark-seen, and link/dismiss actions for the Home detail route. */
export function useHomeDetailView(id: MaybeRefOrGetter<string>) {
  const { t } = useI18n()
  const format = useFormat()
  const reducedMotion = useReducedMotion()

  const detail = useHomeDetail(() => toValue(id))
  const markSeen = useMarkSeen()
  const dismissHome = useDismissHome()
  const restoreHome = useRestoreHome()
  const linkListing = useLinkListing(() => toValue(id))
  const unlinkListing = useUnlinkListing(() => toValue(id))

  const imageBroken = ref(false)
  const unlinkId = ref<string | null>(null)
  const linkOpen = ref(false)
  const listingIdInput = ref('')
  const unlinkOpen = computed({
    get: () => unlinkId.value != null,
    set: (open: boolean) => {
      if (!open) unlinkId.value = null
    },
  })

  const home = computed(() => detail.data.value?.home)
  const transitionName = computed(() =>
    home.value && !reducedMotion.value ? `home-${home.value.id}` : undefined,
  )

  const attrs = computed(() => {
    const h = home.value
    if (!h) return []
    const rows: { key: string; label: string; value: string }[] = []
    if (h.sizeText || h.sizeM2 != null) {
      rows.push({
        key: 'size',
        label: t('homes.attrs.size'),
        value: h.sizeText ?? format.area(h.sizeM2),
      })
    }
    if (h.landSizeText || h.landSizeM2 != null) {
      rows.push({
        key: 'land',
        label: t('homes.attrs.land'),
        value: h.landSizeText ?? format.area(h.landSizeM2),
      })
    }
    if (h.rooms || h.roomCount != null) {
      rows.push({
        key: 'rooms',
        label: t('homes.attrs.rooms'),
        value: h.rooms ?? format.number(h.roomCount, 1),
      })
    }
    if (h.floorText) {
      rows.push({ key: 'floor', label: t('homes.attrs.floor'), value: h.floorText })
    }
    if (detail.data.value?.yearText || h.yearBuilt != null) {
      rows.push({
        key: 'year',
        label: t('homes.attrs.year'),
        value: detail.data.value?.yearText ?? String(h.yearBuilt),
      })
    }
    if (h.pricePerM2 != null) {
      rows.push({
        key: 'ppm',
        label: t('homes.attrs.pricePerM2'),
        value: format.price(h.pricePerM2, h.currency),
      })
    }
    if (h.propertyType) {
      rows.push({ key: 'type', label: t('homes.attrs.type'), value: h.propertyType })
    }
    return rows
  })

  function maybeMarkSeen() {
    const current = detail.data.value?.home
    if (current?.isUnseen) markSeen.mutate(current.id)
  }

  onMounted(maybeMarkSeen)
  watch(
    () => toValue(id),
    () => {
      imageBroken.value = false
    },
  )
  watch(
    () => detail.data.value?.home.isUnseen,
    (unseen) => {
      if (unseen) maybeMarkSeen()
    },
  )

  async function onDismiss() {
    await dismissHome.mutateAsync(toValue(id))
  }

  async function onRestore() {
    await restoreHome.mutateAsync(toValue(id))
  }

  function requestUnlink(listingId: string) {
    unlinkId.value = listingId
  }

  async function confirmUnlink() {
    if (!unlinkId.value) return
    await unlinkListing.mutateAsync(unlinkId.value)
    unlinkId.value = null
  }

  async function confirmLink() {
    const listingId = listingIdInput.value.trim()
    if (!listingId) return
    await linkListing.mutateAsync(listingId)
    listingIdInput.value = ''
    linkOpen.value = false
  }

  return {
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
  }
}
