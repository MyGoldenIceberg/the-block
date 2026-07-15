import { serverNow } from '../api/clock'
import { formatCountdown } from '../format'
import { useTick } from '../useRoute'
import type { LotSummary } from '../api/types'

/**
 * How long a lot has, read against the server's clock rather than the
 * browser's. The server decides when a lot closes; this only says so.
 */
export function Countdown({ lot }: { lot: LotSummary }) {
  useTick(1000)

  if (lot.state === 'ended') {
    return <span>Closed</span>
  }

  const target = lot.state === 'preview' ? lot.opensAt : lot.closesAt
  const remaining = new Date(target).getTime() - serverNow()
  const verb = lot.state === 'preview' ? 'Opens in' : 'Closes in'

  return (
    <span>
      {verb} {formatCountdown(remaining)}
    </span>
  )
}
