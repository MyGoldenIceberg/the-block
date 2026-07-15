import { useEffect, useState } from 'react'
import { placeBid } from '../api/client'
import { formatMoney, formatPrice } from '../format'
import { OutcomeBadge, ReserveBadge } from './Badges'
import { Countdown } from './Countdown'
import type { BidRefusalReason, LotSummary } from '../api/types'

/**
 * Refusals in the buyer's language rather than the wire's.
 *
 * Being outbid is an ordinary event in an auction, not an error, and the only
 * useful answer to it is the amount that would work now.
 */
function explain(reason: BidRefusalReason, minimum: number): string {
  switch (reason) {
    case 'outbid':
      return `Someone got there first. It takes ${formatMoney(minimum)} now.`
    case 'belowMinimum':
      return `Bidding moves in steps. The next one is ${formatMoney(minimum)}.`
    case 'lotClosed':
      return 'This lot has closed.'
    case 'alreadyLeading':
      return 'You are already the high bidder.'
    case 'unreachable':
      return 'The sale could not be reached, so your bid was not placed.'
    default:
      return 'That bid could not be placed.'
  }
}

export function BidPanel({ lot, onBid }: { lot: LotSummary; onBid: (lot: LotSummary) => void }) {
  const [amount, setAmount] = useState(lot.minimumNextBid)
  const [busy, setBusy] = useState(false)
  const [refusal, setRefusal] = useState<string | null>(null)
  const [placed, setPlaced] = useState(false)

  // Someone else bidding moves the floor under this form. Follow it up, but
  // leave a buyer's own larger number alone.
  useEffect(() => {
    setAmount((current) => (current < lot.minimumNextBid ? lot.minimumNextBid : current))
  }, [lot.minimumNextBid])

  const price = formatPrice(lot)
  const short = amount < lot.minimumNextBid

  const submit = async () => {
    setBusy(true)
    setRefusal(null)

    const result = await placeBid(lot.id, amount)

    if (result.ok) {
      onBid(result.lot)
      setPlaced(true)
    } else {
      setRefusal(explain(result.reason, result.minimumNextBid))
      setAmount(result.minimumNextBid)
    }

    setBusy(false)
  }

  return (
    <aside className="bidbox" data-state={lot.state}>
      <div className="bidbox__price">
        <span className="bidbox__label">{price.label}</span>
        <strong className="bidbox__amount">{price.amount}</strong>
        <span className="bidbox__bids">
          {lot.bidCount === 0 ? 'No bids yet' : `${lot.bidCount} bids`}
        </span>
      </div>

      <div className="bidbox__status">
        <ReserveBadge lot={lot} />
        <OutcomeBadge lot={lot} />
        <Countdown lot={lot} />
      </div>

      {lot.state === 'ended' ? (
        <p className="bidbox__closed">Bidding has closed on this lot.</p>
      ) : (
        <>
          <label className="bidbox__field">
            Your bid
            <input
              type="number"
              inputMode="numeric"
              value={amount}
              min={lot.minimumNextBid}
              step={lot.minimumNextBid - (lot.currentBid ?? lot.startingBid) || 100}
              onChange={(event) => setAmount(Number(event.target.value))}
            />
          </label>

          <button className="bidbox__go" type="button" onClick={submit} disabled={busy || short}>
            {busy ? 'Placing…' : lot.state === 'preview' ? 'Place pre-bid' : 'Place bid'}
          </button>

          <p className="bidbox__hint">
            {short
              ? `The next bid is ${formatMoney(lot.minimumNextBid)}.`
              : `Minimum ${formatMoney(lot.minimumNextBid)}. Bid more to hold it against others.`}
          </p>

          {lot.state === 'preview' && (
            <p className="bidbox__hint">
              This lot has not opened. Pre-bids stand as soon as it does.
            </p>
          )}
        </>
      )}

      {lot.buyNowPrice !== null && lot.state !== 'ended' && (
        <p className="bidbox__buynow">
          Buy now {formatMoney(lot.buyNowPrice)}
          <span> — outside this prototype</span>
        </p>
      )}

      {refusal && <p className="bidbox__refusal">{refusal}</p>}
      {placed && !refusal && <p className="bidbox__ok">Your bid is in.</p>}
    </aside>
  )
}
