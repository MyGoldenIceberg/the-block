import { formatMoney, formatOdometer, formatPrice, vehicleName } from '../format'
import { useLot } from '../useSale'
import { BidPanel } from './BidPanel'
import { Countdown } from './Countdown'
import type { LotSummary } from '../api/types'

const RESERVE_COPY = {
  none: 'No reserve — sells to the highest bidder',
  met: 'Reserve met',
  notMet: 'Reserve not met',
} as const

const OUTCOME_COPY = {
  pending: '',
  sold: 'Sold',
  noSale: 'No sale — the lot drew no bids',
  ifSale: 'Reserve not met — the high bid is with the seller',
} as const

interface LotPageProps {
  id: string
  /** The catalogue's copy of this lot, kept current by the feed. */
  live: LotSummary | undefined
  onBid: (lot: LotSummary) => void
}

export function LotPage({ id, live, onBid }: LotPageProps) {
  const { lot: detail, error } = useLot(id)

  if (error) return <p>{error}</p>
  if (!detail) return <p>Loading…</p>

  // The detail request answers a deep link before the catalogue has loaded;
  // after that the feed's copy is the fresher one.
  const lot = live ?? detail.lot
  const price = formatPrice(lot)

  return (
    <div>
      <p>
        <a href="#">Back to the catalogue</a>
      </p>

      <h1>{vehicleName(lot)}</h1>
      <p>
        Lot {lot.lotNumber} · Lane {lot.lane} · VIN {detail.vin}
      </p>

      <p>
        {lot.state}
        {lot.onTheBlock ? ' — on the block' : ''} · <Countdown lot={lot} />
      </p>

      <h2>
        {price.label} {price.amount} · {lot.bidCount} bids
      </h2>
      <p>{RESERVE_COPY[lot.reserve]}</p>
      {lot.buyNowPrice && <p>Buy now {formatMoney(lot.buyNowPrice)}</p>}
      {lot.outcome !== 'pending' && <p>{OUTCOME_COPY[lot.outcome]}</p>}

      <BidPanel lot={lot} onBid={onBid} />

      <h3>Condition</h3>
      <p>
        Grade {lot.conditionGrade} · Title {lot.titleStatus}
      </p>
      <p>{detail.conditionReport}</p>
      {detail.damageNotes.length > 0 && (
        <ul>
          {detail.damageNotes.map((note) => (
            <li key={note}>{note}</li>
          ))}
        </ul>
      )}

      <h3>Specification</h3>
      <ul>
        <li>{formatOdometer(lot.odometerKm)}</li>
        <li>{detail.engine}</li>
        <li>
          {detail.transmission} · {detail.drivetrain} · {detail.fuelType}
        </li>
        <li>
          {detail.exteriorColor} over {detail.interiorColor}
        </li>
      </ul>

      <h3>Seller</h3>
      <p>
        {lot.sellingDealership} — {lot.city}, {lot.province}
      </p>

      <h3>Photos</h3>
      {detail.images.map((image) => (
        <img key={image} src={image} alt="" width={240} loading="lazy" />
      ))}
    </div>
  )
}
