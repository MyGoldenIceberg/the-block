import { useState } from 'react'
import { placeBid } from '../api/client'
import { formatMoney } from '../format'
import type { BidRefusalReason, LotSummary } from '../api/types'

/**
 * Refusals in the buyer's language, not the wire's.
 *
 * "Reserve not met" and "you were outbid" are ordinary events in an auction.
 * They deserve a sentence that says what to do next.
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
      return 'The sale could not be reached. Your bid was not placed.'
    default:
      return 'That bid could not be placed.'
  }
}

export function BidPanel({ lot, onBid }: { lot: LotSummary; onBid: (lot: LotSummary) => void }) {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  if (lot.state === 'ended') {
    return <p>Bidding has closed on this lot.</p>
  }

  const submit = async () => {
    setBusy(true)
    setMessage(null)

    const result = await placeBid(lot.id, lot.minimumNextBid)

    if (result.ok) {
      onBid(result.lot)
      setMessage('Your bid is in.')
    } else {
      setMessage(explain(result.reason, result.minimumNextBid))
    }

    setBusy(false)
  }

  return (
    <div>
      <button type="button" onClick={submit} disabled={busy}>
        {lot.state === 'preview' ? 'Pre-bid' : 'Bid'} {formatMoney(lot.minimumNextBid)}
      </button>
      {lot.state === 'preview' && <p>This lot has not opened. Pre-bids stand when it does.</p>}
      {message && <p>{message}</p>}
    </div>
  )
}
