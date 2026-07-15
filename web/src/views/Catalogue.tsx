import { useMemo, useState } from 'react'
import { NO_FILTERS, applyFilters, applySort } from '../filters'
import { FilterBar } from './FilterBar'
import { LotCard } from './LotCard'
import type { Filters, SortKey } from '../filters'
import type { LotSummary } from '../api/types'

export function Catalogue({ lots }: { lots: LotSummary[] }) {
  const [filters, setFilters] = useState<Filters>(NO_FILTERS)

  // Not price, and not lot number. A buyer's first question is what they can
  // still get, and how long they have to decide.
  const [sort, setSort] = useState<SortKey>('closing')

  const visible = useMemo(() => applySort(applyFilters(lots, filters), sort), [lots, filters, sort])

  return (
    <div className="catalogue">
      <header className="masthead">
        <h1>The Block</h1>
        <p>Wholesale vehicle auction</p>
      </header>

      <FilterBar
        lots={lots}
        filters={filters}
        sort={sort}
        showing={visible.length}
        onChange={setFilters}
        onSort={setSort}
      />

      {visible.length === 0 ? (
        <p className="catalogue__empty">
          No lots match that. Try widening the grade or looking beyond one province.
        </p>
      ) : (
        <div className="catalogue__grid">
          {visible.map((lot) => (
            <LotCard key={lot.id} lot={lot} />
          ))}
        </div>
      )}
    </div>
  )
}
