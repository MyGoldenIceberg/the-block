/**
 * The client's read on the server's clock.
 *
 * Countdowns tick once a second and cannot be round-tripped, so they run on
 * the browser's own clock. That clock is whatever the buyer's laptop says it
 * is: a machine ninety seconds fast would show time left on a lot the server
 * has already closed, and refuse the bid it just invited. Every response the
 * API sends carries its own time, so the difference is measured once and every
 * countdown reads through it.
 *
 * The measurement includes however long the response took to arrive, so it is
 * accurate to a network hop rather than a millisecond. The problem being
 * solved is a clock minutes out, not microseconds.
 *
 * The server stays the authority on whether a lot is open. This only keeps the
 * UI honest about it.
 */
let skewMs = 0

export function recordServerTime(serverTime: string): void {
  const server = new Date(serverTime).getTime()
  if (!Number.isNaN(server)) {
    skewMs = server - Date.now()
  }
}

/** Now, as the server would tell it. */
export function serverNow(): number {
  return Date.now() + skewMs
}

export function skew(): number {
  return skewMs
}
