import { Catalogue } from './views/Catalogue'
import { LotPage } from './views/LotPage'
import { useRoute } from './useRoute'
import { useSale } from './useSale'

export default function App() {
  const route = useRoute()
  const { lots, loading, error } = useSale()

  if (route.name === 'lot') {
    return <LotPage id={route.id} />
  }

  if (error) return <p>{error}</p>
  if (loading) return <p>Loading the sale…</p>

  return <Catalogue lots={lots} />
}
