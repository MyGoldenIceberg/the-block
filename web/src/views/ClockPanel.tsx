import { useCallback, useEffect, useState } from 'react'
import { recordServerTime } from '../api/clock'
import type { SaleEnvelope } from '../api/types'

interface ClockReport {
  saleNow: string
  scrubSeconds: number
  preview: number
  live: number
  ended: number
  onTheBlock: number
  nextCloseInSeconds: number | null
}

async function ask(path: string, body?: unknown): Promise<ClockReport | null> {
  try {
    const response = await fetch(`/api/dev/clock${path}`, {
      method: body ? 'POST' : 'GET',
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    })

    if (!response.ok) return null

    const envelope = (await response.json()) as SaleEnvelope<ClockReport>
    recordServerTime(envelope.serverTime)
    return envelope.data
  } catch {
    return null
  }
}

/**
 * A way to show the sale clock doing its job.
 *
 * The catalogue spans a week and closes a lot about once an hour, which is
 * true to how a sale runs and useless to anyone watching for five minutes.
 * Running a lot winds the clock to just before that lot closes: it goes on
 * the block, the bidding lands, and it resolves, on a lot the viewer picked.
 *
 * Development builds only, and the server refuses these routes outside
 * development regardless -- this only hides the buttons.
 */
export function ClockPanel({ lotId, onMoved }: { lotId?: string; onMoved: () => void }) {
  const [report, setReport] = useState<ClockReport | null>(null)
  const [open, setOpen] = useState(false)

  const refresh = useCallback(() => {
    void ask('/').then(setReport)
  }, [])

  useEffect(refresh, [refresh])

  const move = async (path: string, body?: unknown) => {
    const moved = await ask(path, body)
    if (moved) {
      setReport(moved)
      onMoved()
    }
  }

  if (!report) return null

  const scrubbed = Math.abs(report.scrubSeconds) > 1

  return (
    <div className="devclock" data-open={open}>
      <button className="devclock__toggle" type="button" onClick={() => setOpen(!open)}>
        Sale clock{scrubbed ? ' · moved' : ''}
      </button>

      {open && (
        <div className="devclock__body">
          <p className="devclock__counts">
            {report.preview} preview · {report.live} open · {report.ended} closed
            {report.onTheBlock > 0 && ` · ${report.onTheBlock} on the block`}
          </p>

          {lotId && (
            <button type="button" onClick={() => move('/run-lot', { lotId })}>
              Run this lot
            </button>
          )}

          <button type="button" onClick={() => move('/reset')} disabled={!scrubbed}>
            Back to now
          </button>

          <p className="devclock__note">
            Winds the sale to ninety seconds before this lot closes, so its bidding can be watched.
          </p>
        </div>
      )}
    </div>
  )
}
