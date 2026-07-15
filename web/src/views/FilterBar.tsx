import { NO_FILTERS, SORT_LABELS, facetsOf } from '../filters'
import type { Filters, SortKey } from '../filters'
import type { LotSummary } from '../api/types'

interface FilterBarProps {
  lots: LotSummary[]
  filters: Filters
  sort: SortKey
  showing: number
  onChange: (filters: Filters) => void
  onSort: (sort: SortKey) => void
}

const ODOMETER_STEPS = [50_000, 100_000, 150_000, 200_000]
const GRADE_STEPS = [2, 3, 3.5, 4, 4.5]

export function FilterBar({ lots, filters, sort, showing, onChange, onSort }: FilterBarProps) {
  const facets = facetsOf(lots)
  const set = <K extends keyof Filters>(key: K, value: Filters[K]) =>
    onChange({ ...filters, [key]: value })

  const dirty = JSON.stringify(filters) !== JSON.stringify(NO_FILTERS)

  return (
    <div className="filters">
      <input
        className="filters__search"
        type="search"
        value={filters.search}
        placeholder="Make, model, lot number, dealership"
        aria-label="Search the catalogue"
        onChange={(event) => set('search', event.target.value)}
      />

      <label>
        Status
        <select value={filters.state} onChange={(event) => set('state', event.target.value as Filters['state'])}>
          <option value="all">Any</option>
          <option value="live">Open for bidding</option>
          <option value="preview">Preview</option>
          <option value="ended">Closed</option>
        </select>
      </label>

      {/* The one thing that decides whether a bid can actually win. */}
      <label>
        Reserve
        <select value={filters.reserve} onChange={(event) => set('reserve', event.target.value as Filters['reserve'])}>
          <option value="all">Any</option>
          <option value="none">No reserve</option>
          <option value="met">Reserve met</option>
          <option value="notMet">Reserve not met</option>
        </select>
      </label>

      <label>
        Title
        <select value={filters.titleStatus} onChange={(event) => set('titleStatus', event.target.value)}>
          <option value="all">Any</option>
          {facets.titleStatuses.map((status) => (
            <option key={status} value={status}>
              {status}
            </option>
          ))}
        </select>
      </label>

      <label>
        Grade
        <select value={filters.minGrade} onChange={(event) => set('minGrade', Number(event.target.value))}>
          <option value={0}>Any</option>
          {GRADE_STEPS.map((grade) => (
            <option key={grade} value={grade}>
              {grade.toFixed(1)}+
            </option>
          ))}
        </select>
      </label>

      <label>
        Odometer
        <select
          value={filters.maxOdometerKm ?? ''}
          onChange={(event) => set('maxOdometerKm', event.target.value ? Number(event.target.value) : null)}
        >
          <option value="">Any</option>
          {ODOMETER_STEPS.map((km) => (
            <option key={km} value={km}>
              Under {km / 1000}k km
            </option>
          ))}
        </select>
      </label>

      {/* Not a nicety: shipping a car across the country eats the margin. */}
      <label>
        Location
        <select value={filters.province} onChange={(event) => set('province', event.target.value)}>
          <option value="all">Anywhere</option>
          {facets.provinces.map((province) => (
            <option key={province} value={province}>
              {province}
            </option>
          ))}
        </select>
      </label>

      <label>
        Lane
        <select value={filters.lane} onChange={(event) => set('lane', event.target.value)}>
          <option value="all">All lanes</option>
          {facets.lanes.map((lane) => (
            <option key={lane} value={lane}>
              Lane {lane}
            </option>
          ))}
        </select>
      </label>

      <label>
        Sort
        <select value={sort} onChange={(event) => onSort(event.target.value as SortKey)}>
          {Object.entries(SORT_LABELS).map(([key, label]) => (
            <option key={key} value={key}>
              {label}
            </option>
          ))}
        </select>
      </label>

      <p className="filters__count">
        {showing} of {lots.length} lots
        {dirty && (
          <button type="button" onClick={() => onChange(NO_FILTERS)}>
            Clear
          </button>
        )}
      </p>
    </div>
  )
}
