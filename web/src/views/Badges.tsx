import type { LotSummary } from '../api/types'

export function StateBadge({ lot }: { lot: LotSummary }) {
  if (lot.onTheBlock) {
    return <span className="badge badge--block">On the block</span>
  }

  if (lot.state === 'live') {
    return <span className="badge badge--live">Open for bidding</span>
  }

  if (lot.state === 'preview') {
    return <span className="badge badge--preview">Preview</span>
  }

  return <span className="badge badge--ended">Closed</span>
}

/**
 * Whether a bid on this lot can win, without saying what it would take.
 *
 * The single most decision-relevant fact on the card, and the amount behind it
 * is the seller's business. "No reserve" is a selling point rather than an
 * omission: 60 of the 200 lots sell to whoever bids highest.
 */
export function ReserveBadge({ lot }: { lot: LotSummary }) {
  switch (lot.reserve) {
    case 'none':
      return <span className="badge badge--good">No reserve</span>
    case 'met':
      return <span className="badge badge--good">Reserve met</span>
    case 'notMet':
      return <span className="badge badge--muted">Reserve not met</span>
  }
}

const OUTCOME = {
  sold: { text: 'Sold', tone: 'good' },
  ifSale: { text: 'Reserve not met — with seller', tone: 'muted' },
  noSale: { text: 'No sale', tone: 'muted' },
  pending: { text: '', tone: 'muted' },
} as const

/**
 * How a lot finished.
 *
 * "IF" is the lane's word for a lot that closed under its reserve: the high
 * bid goes to the seller, who may still take it. It is the commonest ending in
 * this catalogue by some distance -- the dataset draws bidding from 72-102% of
 * reserve -- and it is an outcome, not a failure. Worth spelling out on the
 * page rather than hiding behind the jargon.
 */
export function OutcomeBadge({ lot }: { lot: LotSummary }) {
  if (lot.outcome === 'pending') {
    return null
  }

  const outcome = OUTCOME[lot.outcome]

  return (
    <span
      className={`badge badge--${outcome.tone}`}
      title={lot.outcome === 'ifSale' ? 'An "IF" in the lane: the seller can still accept the high bid.' : undefined}
    >
      {outcome.text}
    </span>
  )
}
