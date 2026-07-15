import { formatGrade, formatOdometer, vehicleName } from '../format'
import { useLot } from '../useSale'
import { BidPanel } from './BidPanel'
import { Gallery } from './Gallery'
import { StateBadge } from './Badges'
import { gradeTier } from './LotCard'
import type { LotDetail, LotSummary } from '../api/types'

interface LotPageProps {
  id: string
  /** The catalogue's copy of this lot, kept current by the feed. */
  live: LotSummary | undefined
  onBid: (lot: LotSummary) => void
}

export function LotPage({ id, live, onBid }: LotPageProps) {
  const { lot: detail, error } = useLot(id)

  if (error) {
    return (
      <div className="lot lot--message">
        <a href="#">Back to the catalogue</a>
        <p>{error}</p>
      </div>
    )
  }

  if (!detail) {
    return <div className="lot lot--message">Loading the lot…</div>
  }

  // The detail request answers a deep link before the catalogue has loaded.
  // After that the feed's copy is the fresher one.
  const lot = live ?? detail.lot

  return (
    <div className="lot">
      <nav className="lot__back">
        <a href="#">← Back to the catalogue</a>
      </nav>

      <header className="lot__head">
        <div className="lot__badges">
          <StateBadge lot={lot} />
          {lot.titleStatus !== 'clean' && (
            <span className="badge badge--warn">{lot.titleStatus} title</span>
          )}
        </div>
        <h1>{vehicleName(lot)}</h1>
        <p className="lot__ident">
          Lot {lot.lotNumber} · Lane {lot.lane} · VIN {detail.vin}
        </p>
      </header>

      <div className="lot__main">
        <Gallery images={detail.images} name={vehicleName(lot)} />
        <Condition detail={detail} lot={lot} />
        <Specification detail={detail} lot={lot} />
        <Seller lot={lot} />
      </div>

      <div className="lot__side">
        <BidPanel lot={lot} onBid={onBid} />
      </div>
    </div>
  )
}

/**
 * The whole reason a dealer can buy this sight unseen.
 *
 * Grade, title and damage are not trivia to tuck under the specs -- they are
 * the entire basis on which someone commits money to a car they have never
 * seen. They lead.
 */
function Condition({ detail, lot }: { detail: LotDetail; lot: LotSummary }) {
  return (
    <section className="panel">
      <h2>Condition</h2>

      <div className="condition__top">
        <div className="condition__grade" data-tier={gradeTier(lot.conditionGrade)}>
          <strong>{formatGrade(lot.conditionGrade)}</strong>
          <span>of 5.0</span>
        </div>
        <dl className="condition__facts">
          <div>
            <dt>Title</dt>
            <dd data-clean={lot.titleStatus === 'clean'}>{lot.titleStatus}</dd>
          </div>
          <div>
            <dt>Odometer</dt>
            <dd>{formatOdometer(lot.odometerKm)}</dd>
          </div>
        </dl>
      </div>

      <p className="condition__report">{detail.conditionReport}</p>

      <h3>
        Damage {detail.damageNotes.length > 0 && <span>({detail.damageNotes.length})</span>}
      </h3>
      {detail.damageNotes.length === 0 ? (
        <p className="condition__none">No damage noted.</p>
      ) : (
        <ul className="condition__damage">
          {detail.damageNotes.map((note) => (
            <li key={note}>{note}</li>
          ))}
        </ul>
      )}
    </section>
  )
}

function Specification({ detail, lot }: { detail: LotDetail; lot: LotSummary }) {
  const rows: [string, string][] = [
    ['Year', String(lot.year)],
    ['Body', lot.bodyStyle],
    ['Engine', detail.engine],
    ['Transmission', detail.transmission],
    ['Drivetrain', detail.drivetrain],
    ['Fuel', detail.fuelType],
    ['Exterior', detail.exteriorColor],
    ['Interior', detail.interiorColor],
  ]

  return (
    <section className="panel">
      <h2>Specification</h2>
      <dl className="spec">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}

function Seller({ lot }: { lot: LotSummary }) {
  return (
    <section className="panel">
      <h2>Selling dealer</h2>
      <p className="seller__name">{lot.sellingDealership}</p>
      <p className="seller__where">
        {lot.city}, {lot.province}
      </p>
      <p className="seller__note">Transport from {lot.province} is the buyer's cost.</p>
    </section>
  )
}
