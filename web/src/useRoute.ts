import { useEffect, useReducer, useState } from 'react'

export type Route = { name: 'catalogue' } | { name: 'lot'; id: string }

/**
 * Two views and a back button.
 *
 * A router library would be a dependency and an abstraction for a problem this
 * app does not have. The hash keeps deep links working and the browser's back
 * button doing what a buyer expects, which is all that is needed.
 */
export function useRoute(): Route {
  const [hash, setHash] = useState(() => window.location.hash)

  useEffect(() => {
    const onChange = () => setHash(window.location.hash)
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])

  const match = /^#\/lots\/(.+)$/.exec(hash)
  return match ? { name: 'lot', id: match[1] } : { name: 'catalogue' }
}

export function lotHref(id: string): string {
  return `#/lots/${id}`
}

export function goToCatalogue(): void {
  window.location.hash = ''
}

/**
 * Re-renders on an interval, so countdowns move.
 *
 * Nothing here owns the time; this only decides how often the component asks
 * the clock again.
 */
export function useTick(everyMs = 1000): void {
  const [, bump] = useReducer((count: number) => count + 1, 0)

  useEffect(() => {
    const timer = setInterval(bump, everyMs)
    return () => clearInterval(timer)
  }, [everyMs])
}
