import { formatGrade, formatMoney, formatOdometer, formatPrice, vehicleName } from '../format'
import { lotHref } from '../useRoute'
import { Countdown } from './Countdown'
import { LotThumbnail } from './LotThumbnail'
import { ReserveBadge, StateBadge } from './Badges'
import type { LotSummary } from '../api/types'

/**
 * A lot at a glance.
 *
 * A dealer scans hundreds of these. What has to survive the glance is the risk
 * -- grade, title, damage, distance -- and whether a bid on it can actually
 * win. Everything else can wait for the detail page.
 */
export function LotCard({ lot }: { lot: LotSummary }) {
  const price = formatPrice(lot)
  const salvage = lot.titleStatus !== 'clean'

  return (
    <a className="card" href={lotHref(lot.id)} data-state={lot.state}>
      <LotThumbnail lot={lot} />

      <div className="card__body">
        <div className="card__badges">
          <StateBadge lot={lot} />
          {salvage && <span className="badge badge--warn">{lot.titleStatus} title</span>}
        </div>

        <h2 className="card__name">{vehicleName(lot)}</h2>

        <p className="card__facts">
          <span>{formatOdometer(lot.odometerKm)}</span>
          <span className="card__grade" data-tier={gradeTier(lot.conditionGrade)}>
            Grade {formatGrade(lot.conditionGrade)}
          </span>
          {lot.damageCount > 0 && (
            <span>
              {lot.damageCount} damage {lot.damageCount === 1 ? 'note' : 'notes'}
            </span>
          )}
        </p>

        <p className="card__where">
          {lot.city}, {lot.province} · Lot {lot.lotNumber}
        </p>

        <div className="card__price">
          <span className="card__price-label">{price.label}</span>
          <strong className="card__price-amount">{price.amount}</strong>
          <span className="card__bids">
            {lot.bidCount === 0 ? 'No bids yet' : `${lot.bidCount} bids`}
          </span>
        </div>

        <div className="card__footer">
          <ReserveBadge lot={lot} />
          <span className="card__clock">
            <Countdown lot={lot} />
          </span>
        </div>

        {lot.buyNowPrice !== null && lot.state !== 'ended' && (
          <p className="card__buynow">Buy now {formatMoney(lot.buyNowPrice)}</p>
        )}
      </div>
    </a>
  )
}

/** Grades run 1.2 to 5.0. Roughly: rough, average, clean. */
export function gradeTier(grade: number): 'low' | 'mid' | 'high' {
  if (grade < 3) return 'low'
  return grade < 4 ? 'mid' : 'high'
}
