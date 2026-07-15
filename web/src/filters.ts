import type { LotState, LotSummary, ReserveStatus } from './api/types'

/**
 * What a wholesale buyer actually sorts and filters on.
 *
 * The buyer here is a dealer buying stock to resell, sight unseen, at volume.
 * Their whole question is what a vehicle will cost to make retail-ready and
 * what it costs to get home: condition grade, title, damage, odometer, and
 * where it is. The dataset also carries exterior colour, interior colour,
 * engine and fuel type, and none of them are facets -- nobody buys wholesale
 * by colour. Filtering by the wrong things is worse than filtering by fewer.
 */
export interface Filters {
  search: string
  state: LotState | 'all'
  reserve: ReserveStatus | 'all'
  titleStatus: string
  province: string
  lane: string
  minGrade: number
  maxOdometerKm: number | null
}

export const NO_FILTERS: Filters = {
  search: '',
  state: 'all',
  reserve: 'all',
  titleStatus: 'all',
  province: 'all',
  lane: 'all',
  minGrade: 0,
  maxOdometerKm: null,
}

export type SortKey = 'closing' | 'priceLow' | 'priceHigh' | 'odometer' | 'grade' | 'newest'

export const SORT_LABELS: Record<SortKey, string> = {
  closing: 'Closing soonest',
  priceLow: 'Price: low to high',
  priceHigh: 'Price: high to low',
  odometer: 'Lowest odometer',
  grade: 'Best condition',
  newest: 'Newest model year',
}

/** What a lot costs right now: the standing bid, or what it opens at. */
export function askingPrice(lot: LotSummary): number {
  return lot.currentBid ?? lot.startingBid
}

function matchesSearch(lot: LotSummary, search: string): boolean {
  const needle = search.trim().toLowerCase()
  if (!needle) return true

  return [lot.make, lot.model, lot.trim, lot.lotNumber, lot.sellingDealership, String(lot.year)]
    .some((field) => field?.toLowerCase().includes(needle))
}

export function applyFilters(lots: LotSummary[], filters: Filters): LotSummary[] {
  return lots.filter(
    (lot) =>
      matchesSearch(lot, filters.search) &&
      (filters.state === 'all' || lot.state === filters.state) &&
      (filters.reserve === 'all' || lot.reserve === filters.reserve) &&
      (filters.titleStatus === 'all' || lot.titleStatus === filters.titleStatus) &&
      (filters.province === 'all' || lot.province === filters.province) &&
      (filters.lane === 'all' || lot.lane === filters.lane) &&
      lot.conditionGrade >= filters.minGrade &&
      (filters.maxOdometerKm === null || lot.odometerKm <= filters.maxOdometerKm),
  )
}

/**
 * Lots you can still bid on come first.
 *
 * Sorting purely by close time would open the catalogue with the lots that
 * already closed, which is the least useful thing a buyer could be shown.
 */
function biddableFirst(lot: LotSummary): number {
  if (lot.onTheBlock) return 0
  if (lot.state === 'live') return 1
  if (lot.state === 'preview') return 2
  return 3
}

export function applySort(lots: LotSummary[], key: SortKey): LotSummary[] {
  const sorted = [...lots]

  switch (key) {
    case 'closing':
      return sorted.sort((a, b) => {
        const rank = biddableFirst(a) - biddableFirst(b)
        if (rank !== 0) return rank

        const left = new Date(a.closesAt).getTime()
        const right = new Date(b.closesAt).getTime()

        // Among lots still running, whichever goes first. Among lots already
        // closed, whichever went most recently.
        return a.state === 'ended' ? right - left : left - right
      })
    case 'priceLow':
      return sorted.sort((a, b) => askingPrice(a) - askingPrice(b))
    case 'priceHigh':
      return sorted.sort((a, b) => askingPrice(b) - askingPrice(a))
    case 'odometer':
      return sorted.sort((a, b) => a.odometerKm - b.odometerKm)
    case 'grade':
      return sorted.sort((a, b) => b.conditionGrade - a.conditionGrade)
    case 'newest':
      return sorted.sort((a, b) => b.year - a.year)
  }
}

/** The values actually present in the catalogue, so no facet leads nowhere. */
export function facetsOf(lots: LotSummary[]) {
  const unique = (pick: (lot: LotSummary) => string) => [...new Set(lots.map(pick))].sort()

  return {
    provinces: unique((lot) => lot.province),
    titleStatuses: unique((lot) => lot.titleStatus),
    lanes: unique((lot) => lot.lane),
  }
}
