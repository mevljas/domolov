import { describe, expect, it } from 'vitest'
import type { BookmarkColumn } from '@/api/types'
import { wordDiff } from '@/shared/lib/diff'
import { isBoardEmpty, sortColumnsByStage } from './board'

function column(stage: BookmarkColumn['stage'], count = 0): BookmarkColumn {
  return {
    stage,
    homes: Array.from({ length: count }, (_, i) => ({
      id: `${stage}-${i}`,
      title: `${stage} ${i}`,
      imageUrl: null,
      location: null,
      propertyType: null,
      roomCount: null,
      rooms: null,
      sizeM2: null,
      sizeText: null,
      landSizeM2: null,
      landSizeText: null,
      floorText: null,
      yearBuilt: null,
      price: null,
      previousPrice: null,
      currency: 'EUR',
      priceChangedAt: null,
      pricePerM2: null,
      firstSeenAt: '2026-01-01T00:00:00Z',
      lastSeenAt: '2026-01-01T00:00:00Z',
      offMarketAt: null,
      isUnseen: false,
      isDismissed: false,
      listingCount: 1,
      activeListingCount: 1,
      repostCount: 0,
      primaryListingId: null,
      url: null,
      providerId: null,
      bookmark: {
        stage,
        note: null,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    })),
  }
}

describe('sortColumnsByStage', () => {
  it('orders columns by BOOKMARK_STAGES regardless of API order', () => {
    const sorted = sortColumnsByStage([
      column('rejected'),
      column('interested'),
      column('offerMade'),
      column('contacted'),
      column('viewed'),
      column('viewingScheduled'),
    ])
    expect(sorted.map((c) => c.stage)).toEqual([
      'interested',
      'contacted',
      'viewingScheduled',
      'viewed',
      'offerMade',
      'rejected',
    ])
  })

  it('detects an empty board', () => {
    expect(isBoardEmpty([column('interested'), column('contacted')])).toBe(true)
    expect(isBoardEmpty([column('interested', 1)])).toBe(false)
  })
})

describe('wordDiff', () => {
  it('marks words that appear on only one side', () => {
    const { left, right } = wordDiff('bright sunny apartment', 'bright quiet apartment')
    expect(left.some((t) => t.kind === 'removed' && t.text.includes('sunny'))).toBe(true)
    expect(right.some((t) => t.kind === 'added' && t.text.includes('quiet'))).toBe(true)
    expect(left.some((t) => t.kind === 'same' && t.text.includes('bright'))).toBe(true)
  })
})
