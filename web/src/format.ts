import type { LotSummary } from './api/types'

const money = new Intl.NumberFormat('en-CA', {
  style: 'currency',
  currency: 'CAD',
  maximumFractionDigits: 0,
})

const distance = new Intl.NumberFormat('en-CA', { maximumFractionDigits: 0 })

export function formatMoney(amount: number): string {
  return money.format(amount)
}

export function formatOdometer(km: number): string {
  return `${distance.format(km)} km`
}

/**
 * What a lot costs to a buyer looking at it.
 *
 * A lot nobody has bid on has no current bid -- the value is null, not zero,
 * on more than half the catalogue. It still has a price: the one it opens at.
 * Rendering the null as a number is how a marketplace ends up advertising 112
 * vehicles as free.
 */
export function formatPrice(lot: LotSummary): { label: string; amount: string } {
  return lot.currentBid === null
    ? { label: 'Opening bid', amount: formatMoney(lot.startingBid) }
    : { label: 'Current bid', amount: formatMoney(lot.currentBid) }
}

/**
 * A duration, at the resolution worth reading.
 *
 * Days out, seconds are noise. Inside the last hour they are the only thing
 * that matters.
 */
export function formatCountdown(ms: number): string {
  if (ms <= 0) {
    return '0s'
  }

  const totalSeconds = Math.floor(ms / 1000)
  const days = Math.floor(totalSeconds / 86_400)
  const hours = Math.floor((totalSeconds % 86_400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  if (days > 0) return `${days}d ${hours}h`
  if (hours > 0) return `${hours}h ${minutes}m`
  if (minutes > 0) return `${minutes}m ${seconds}s`
  return `${seconds}s`
}

export function formatGrade(grade: number): string {
  return grade.toFixed(1)
}

export function vehicleName(lot: LotSummary): string {
  return [lot.year, lot.make, lot.model, lot.trim].filter(Boolean).join(' ')
}
