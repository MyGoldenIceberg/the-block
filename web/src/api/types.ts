export type LotState = 'preview' | 'live' | 'ended'

export type ReserveStatus = 'none' | 'met' | 'notMet'

export type LotOutcome = 'pending' | 'noSale' | 'sold' | 'ifSale'

export interface LotSummary {
  id: string
  lotNumber: string
  lane: string
  year: number
  make: string
  model: string
  trim: string | null
  bodyStyle: string
  odometerKm: number
  conditionGrade: number
  titleStatus: string
  province: string
  city: string
  sellingDealership: string
  thumbnail: string | null
  damageCount: number

  state: LotState
  onTheBlock: boolean
  /** Real instants, not sale-timeline ones. Ordinary date maths applies. */
  opensAt: string
  closesAt: string

  startingBid: number
  /**
   * Null on a lot nobody has bid on, which is over half the catalogue --
   * never zero. A lot without bids is not a lot worth nothing, so this has to
   * stay nullable all the way to the component that renders it.
   */
  currentBid: number | null
  bidCount: number
  minimumNextBid: number

  /** Whether the reserve is met. The amount is the seller's business. */
  reserve: ReserveStatus
  buyNowPrice: number | null
  outcome: LotOutcome
}

export interface LotDetail {
  lot: LotSummary
  vin: string
  exteriorColor: string
  interiorColor: string
  engine: string
  transmission: string
  drivetrain: string
  fuelType: string
  conditionReport: string
  damageNotes: string[]
  images: string[]
}

export interface SaleEnvelope<T> {
  serverTime: string
  data: T
}

export type BidRefusalReason =
  | 'belowMinimum'
  | 'lotClosed'
  | 'alreadyLeading'
  | 'outbid'
  | 'unidentified'
  | 'unreachable'

export interface BidRefused {
  reason: BidRefusalReason
  minimumNextBid: number
  currentBid: number | null
}

/**
 * A union rather than a thrown error: being outbid is an ordinary part of an
 * auction, and the UI has to answer it with the price that would work.
 */
export type BidResult = { ok: true; lot: LotSummary } | ({ ok: false } & BidRefused)
