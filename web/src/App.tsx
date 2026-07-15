import { useSaleFeed } from './api/live'
import { Catalogue } from './views/Catalogue'
import { ClockPanel } from './views/ClockPanel'
import { LotPage } from './views/LotPage'
import { useRoute } from './useRoute'
import { useSale } from './useSale'

export default function App() {
  const route = useRoute()
  const { lots, loading, error, replace, reload } = useSale()

  // One subscription for the whole app. Both views read the same lots, so a
  // bid landing anywhere is seen everywhere without either of them knowing
  // the feed exists.
  useSaleFeed(replace, reload)

  return (
    <>
      {route.name === 'lot' ? (
        <LotPage id={route.id} live={lots.find((lot) => lot.id === route.id)} onBid={replace} />
      ) : error ? (
        <p className="lot lot--message">{error}</p>
      ) : loading ? (
        <p className="lot lot--message">Loading the sale…</p>
      ) : (
        <Catalogue lots={lots} />
      )}

      {import.meta.env.DEV && (
        <ClockPanel lotId={route.name === 'lot' ? route.id : undefined} onMoved={reload} />
      )}
    </>
  )
}
