import { formatMoney, formatOdometer, formatPrice, vehicleName } from '../format'
import { lotHref } from '../useRoute'
import { Countdown } from './Countdown'
import type { LotSummary } from '../api/types'

export function Catalogue({ lots }: { lots: LotSummary[] }) {
  return (
    <div>
      <h1>The Block</h1>
      <p>{lots.length} lots</p>

      <ul>
        {lots.map((lot) => (
          <li key={lot.id}>
            <a href={lotHref(lot.id)}>
              {lot.lotNumber} — {vehicleName(lot)}
            </a>{' '}
            | {lot.state}
            {lot.onTheBlock ? ' (on the block)' : ''} | {formatPrice(lot).label}{' '}
            {formatPrice(lot).amount} | {lot.bidCount} bids | <Countdown lot={lot} /> |{' '}
            {formatOdometer(lot.odometerKm)} | grade {lot.conditionGrade} | {lot.city},{' '}
            {lot.province} | reserve {lot.reserve}
            {lot.buyNowPrice ? ` | buy now ${formatMoney(lot.buyNowPrice)}` : ''}
          </li>
        ))}
      </ul>
    </div>
  )
}
