import { BOOKMARK_STAGES, type BookmarkColumn, type BookmarkStage } from '@/api/types'

/** Order board columns by the canonical stage progression, ignoring API order. */
export function sortColumnsByStage(columns: BookmarkColumn[]): BookmarkColumn[] {
  const rank = new Map(BOOKMARK_STAGES.map((stage, index) => [stage, index]))
  return [...columns].sort((a, b) => (rank.get(a.stage) ?? 99) - (rank.get(b.stage) ?? 99))
}

export function isBoardEmpty(columns: BookmarkColumn[]): boolean {
  return columns.every((column) => column.homes.length === 0)
}

export function stageLabelKey(stage: BookmarkStage): `bookmarks.stages.${BookmarkStage}` {
  return `bookmarks.stages.${stage}`
}
