/** Short aliases for the generated OpenAPI schemas — never redeclare DTO shapes by hand. */
import type { components, operations } from './schema'

type Schemas = components['schemas']

export type BookmarkBoard = Schemas['BookmarkBoardResponse']
export type BookmarkColumn = Schemas['BookmarkColumnResponse']
export type Bookmark = Schemas['BookmarkResponse']
export type BookmarkStage = Schemas['BookmarkStage']
export type CleanupRun = Schemas['CleanupRunResponse']
export type CreateNotificationRouteRequest = Schemas['CreateNotificationRouteRequest']
export type CreateWatchRequest = Schemas['CreateWatchRequest']
export type Dashboard = Schemas['DashboardResponse']
export type DashboardStats = Schemas['DashboardStats']
export type HomeDetail = Schemas['HomeDetailResponse']
export type HomeListing = Schemas['HomeListingResponse']
export type HomeListingRole = Schemas['HomeListingRole']
export type HomeMatch = Schemas['HomeMatchResponse']
export type HomeMatchState = Schemas['HomeMatchState']
export type HomeSort = NonNullable<Schemas['HomeSort']>
export type HomeSummary = Schemas['HomeSummaryResponse']
export type ListingDetail = Schemas['ListingDetailResponse']
export type MarketStatus = NonNullable<Schemas['MarketStatus']>
export type DismissedFilter = NonNullable<Schemas['DismissedFilter']>
export type MatchListing = Schemas['MatchListingResponse']
export type MatchSignals = Schemas['MatchSignalsResponse']
export type NotificationChannel = Schemas['NotificationChannel']
export type NotificationRoute = Schemas['NotificationRouteResponse']
export type PagedHomes = Schemas['PagedResponseOfHomeSummaryResponse']
export type PagedScanRuns = Schemas['PagedResponseOfScanRunResponse']
export type PricePoint = Schemas['PricePointResponse']
export type ScanArtifact = Schemas['ScanArtifactResponse']
export type ScanRun = Schemas['ScanRunResponse']
export type ScanRunStatus = Schemas['ScanRunStatus']
export type SearchUrlCheck = Schemas['SearchUrlCheckResponse']
export type SessionListItem = Schemas['SessionListItem']
export type Settings = Schemas['SettingsResponse']
export type Storage = Schemas['StorageResponse']
export type TestDelivery = Schemas['TestDeliveryResponse']
export type UpdateNotificationRouteRequest = Schemas['UpdateNotificationRouteRequest']
export type UpdateWatchRequest = Schemas['UpdateWatchRequest']
export type Watch = Schemas['WatchResponse']

export type HomesQuery = NonNullable<operations['SearchHomes']['parameters']['query']>
export type ScansQuery = NonNullable<operations['ListScanRuns']['parameters']['query']>

/**
 * The OpenAPI document types triggers as `string`; these are the values the API accepts
 * (see docs/api.md and NotificationTrigger in the backend).
 */
export const NOTIFICATION_TRIGGERS = [
  'newListing',
  'priceDecreased',
  'priceIncreased',
  'anyPriceChange',
  'reposted',
] as const
export type NotificationTrigger = (typeof NOTIFICATION_TRIGGERS)[number]

export const NOTIFICATION_CHANNELS: readonly NotificationChannel[] = [
  'discord',
  'telegram',
  'email',
  'webPush',
]

export const BOOKMARK_STAGES: readonly BookmarkStage[] = [
  'interested',
  'contacted',
  'viewingScheduled',
  'viewed',
  'offerMade',
  'rejected',
]

export const SCAN_STATUSES: readonly ScanRunStatus[] = [
  'queued',
  'running',
  'baseline',
  'succeeded',
  'failed',
  'interrupted',
]

export const HOME_SORTS: readonly HomeSort[] = [
  'newest',
  'priceDrop',
  'priceAsc',
  'priceDesc',
  'pricePerM2',
  'size',
  'recentlySeen',
]

export function isActiveScan(status: ScanRunStatus): boolean {
  return status === 'queued' || status === 'running'
}
