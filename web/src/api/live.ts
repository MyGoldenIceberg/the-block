import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef } from 'react'
import { recordServerTime } from './clock'
import type { LotSummary, SaleEnvelope } from './types'

/**
 * Keeps the sale current without anyone asking it to.
 *
 * A price is a claim about right now. Polling would make every buyer's screen
 * a little bit wrong a little bit of the time, and on a lot in its closing
 * minutes "a little bit wrong" is the whole game. The sale pushes instead:
 * seeded bidding lands as the clock reaches it, and a bid placed in one window
 * shows up in every other.
 */
export function useSaleFeed(onLot: (lot: LotSummary) => void, onClockMoved?: () => void): void {
  // Held in a ref so the connection is made once, rather than torn down and
  // rebuilt every time a parent re-renders with a new closure.
  const handleLot = useRef(onLot)
  const handleClock = useRef(onClockMoved)
  handleLot.current = onLot
  handleClock.current = onClockMoved

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/auction')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    const lotMoved = (envelope: SaleEnvelope<LotSummary>) => {
      recordServerTime(envelope.serverTime)
      handleLot.current(envelope.data)
    }

    connection.on('lotChanged', lotMoved)
    connection.on('lotStateChanged', lotMoved)
    connection.on('clockChanged', (envelope: SaleEnvelope<unknown>) => {
      recordServerTime(envelope.serverTime)
      handleClock.current?.()
    })

    // A sale that cannot be watched live is still a sale worth browsing, so a
    // failed connection is not worth interrupting anyone over.
    connection.start().catch(() => undefined)

    return () => {
      void connection.stop()
    }
  }, [])
}
