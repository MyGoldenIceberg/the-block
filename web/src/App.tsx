import { useSaleFeed } from './api/live'
import { Catalogue } from './views/Catalogue'
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

  if (route.name === 'lot') {
    return <LotPage id={route.id} live={lots.find((lot) => lot.id === route.id)} onBid={replace} />
  }

  if (error) return <p>{error}</p>
  if (loading) return <p>Loading the sale…</p>

  return <Catalogue lots={lots} />
}
