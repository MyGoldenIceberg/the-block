import { recordServerTime } from './clock'
import type { BidRefused, BidResult, LotDetail, LotSummary, SaleEnvelope } from './types'

const BUYER_STORAGE_KEY = 'the-block.buyer-id'

/**
 * Who this tab is bidding as.
 *
 * There are no accounts -- the challenge does not ask for any -- so the
 * browser invents an id. Enough to tell a buyer which bids are theirs and to
 * stop them bidding against themselves. It is not authentication and nothing
 * of value hangs off it.
 *
 * Per tab rather than per browser, deliberately: two tabs are two buyers, so
 * a bidding war can be watched from both sides on one machine. It survives a
 * reload, which is what a buyer would notice.
 */
export function buyerId(): string {
  let id = sessionStorage.getItem(BUYER_STORAGE_KEY)

  if (!id) {
    id = crypto.randomUUID()
    sessionStorage.setItem(BUYER_STORAGE_KEY, id)
  }

  return id
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number) {
    super(`The sale answered with ${status}.`)
    this.status = status
  }
}

/**
 * Every response carries the server's time, so every response is a chance to
 * re-measure the skew. Reading it here means no caller has to remember to.
 */
async function unwrap<T>(response: Response): Promise<T> {
  const envelope = (await response.json()) as SaleEnvelope<T>
  recordServerTime(envelope.serverTime)
  return envelope.data
}

async function get<T>(path: string): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' } })

  if (!response.ok) {
    throw new ApiError(response.status)
  }

  return unwrap<T>(response)
}

export function fetchLots(): Promise<LotSummary[]> {
  return get<LotSummary[]>('/api/lots')
}

export function fetchLot(id: string): Promise<LotDetail> {
  return get<LotDetail>(`/api/lots/${id}`)
}

/**
 * Offers a bid to the sale.
 *
 * Refusals come back as a value rather than an exception: being outbid is an
 * ordinary part of an auction, not a failure, and the answer to it is the
 * price that would have worked.
 */
export async function placeBid(lotId: string, amount: number): Promise<BidResult> {
  let response: Response

  try {
    response = await fetch(`/api/lots/${lotId}/bids`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        'X-Buyer-Id': buyerId(),
      },
      body: JSON.stringify({ amount }),
    })
  } catch {
    return { ok: false, reason: 'unreachable', minimumNextBid: amount, currentBid: null }
  }

  if (response.ok) {
    return { ok: true, lot: await unwrap<LotSummary>(response) }
  }

  if (response.status === 400 || response.status === 409) {
    return { ok: false, ...((await response.json()) as BidRefused) }
  }

  throw new ApiError(response.status)
}
