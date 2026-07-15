import { useCallback, useEffect, useState } from 'react'
import { fetchLot, fetchLots } from './api/client'
import type { LotDetail, LotSummary } from './api/types'

interface SaleState {
  lots: LotSummary[]
  loading: boolean
  error: string | null
}

/**
 * The two processes do not start together.
 *
 * `npm run dev` brings up the client and the API side by side, but Vite serves
 * in under a second and the API has to build first. Without this, a fresh
 * clone's very first impression is an error telling the reviewer the sale is
 * unreachable -- ten seconds before it is.
 */
async function waitForSale<T>(load: () => Promise<T>): Promise<T> {
  const deadline = Date.now() + 30_000

  for (;;) {
    try {
      return await load()
    } catch (error) {
      if (Date.now() > deadline) {
        throw error
      }

      await new Promise((resume) => setTimeout(resume, 600))
    }
  }
}

/**
 * The catalogue.
 *
 * All 200 lots arrive in one response and are filtered and sorted in the
 * browser. At this size that beats a round trip for every facet change, and
 * browsing is the one part of this that genuinely does not need a server.
 */
export function useSale(): SaleState & { replace: (lot: LotSummary) => void } {
  const [lots, setLots] = useState<LotSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true

    waitForSale(fetchLots)
      .then((loaded) => live && setLots(loaded))
      .catch(() => live && setError('The sale could not be reached. Is the API running?'))
      .finally(() => live && setLoading(false))

    return () => {
      live = false
    }
  }, [])

  const replace = useCallback((updated: LotSummary) => {
    setLots((current) => current.map((lot) => (lot.id === updated.id ? updated : lot)))
  }, [])

  return { lots, loading, error, replace }
}

export function useLot(id: string): { lot: LotDetail | null; error: string | null } {
  const [lot, setLot] = useState<LotDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    setLot(null)

    fetchLot(id)
      .then((loaded) => live && setLot(loaded))
      .catch(() => live && setError('That lot could not be loaded.'))

    return () => {
      live = false
    }
  }, [id])

  return { lot, error }
}
