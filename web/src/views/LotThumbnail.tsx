import { useState } from 'react'
import { vehicleName } from '../format'
import type { LotSummary } from '../api/types'

/**
 * A lot's photo, or an honest space where it should be.
 *
 * Every image in this dataset is a placeholder hosted somewhere else, and the
 * catalogue asks for two hundred of them at once. If that host is slow, rate-
 * limits, or the reviewer is on a train, the naive version of this page is a
 * grid of broken-image icons. The box holds its shape whether or not the
 * picture arrives, so nothing below it moves when it does.
 */
export function LotThumbnail({ lot }: { lot: LotSummary }) {
  const [failed, setFailed] = useState(false)

  if (!lot.thumbnail || failed) {
    return (
      <div className="thumb thumb--empty" aria-hidden="true">
        <span>{lot.make}</span>
      </div>
    )
  }

  return (
    <img
      className="thumb"
      src={lot.thumbnail}
      alt={vehicleName(lot)}
      loading="lazy"
      decoding="async"
      width={800}
      height={600}
      onError={() => setFailed(true)}
    />
  )
}
