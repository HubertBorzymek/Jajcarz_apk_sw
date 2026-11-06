import { useSyncExternalStore } from 'react';

export type Route = 'products' | 'shopping' | 'panel';

export const ROUTE_HASHES: Record<Route, string> = {
  products: '#/',
  shopping: '#/zakupy',
  panel: '#/panel',
};

/** Adres po hashu: serwer zawsze podaje tę samą stronę, a przycisk wstecz na telefonie działa. */
export function parseRoute(hash: string): Route {
  if (hash === ROUTE_HASHES.shopping) {
    return 'shopping';
  }
  return hash === ROUTE_HASHES.panel ? 'panel' : 'products';
}

function subscribe(listener: () => void): () => void {
  window.addEventListener('hashchange', listener);
  return () => window.removeEventListener('hashchange', listener);
}

export function useRoute(): Route {
  return useSyncExternalStore(subscribe, () => parseRoute(window.location.hash));
}
