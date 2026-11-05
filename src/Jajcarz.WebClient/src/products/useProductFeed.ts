import { useEffect, useReducer, useState, type Dispatch } from 'react';
import { loadSession } from '../api/client';
import type { Product } from '../api/types';
import { productsReducer, type ProductAction } from './productList';

export type FeedStatus = 'connecting' | 'live' | 'offline';

const RECONNECT_DELAY_MS = 3_000;
const WATCHDOG_INTERVAL_MS = 15_000;

/** Serwer wysyła ping co 25 s. Dłuższa cisza oznacza martwe połączenie, częste po wyjściu z zasięgu. */
const SILENCE_LIMIT_MS = 60_000;

interface ProductFeed {
  products: Product[];
  loaded: boolean;
  status: FeedStatus;
  dispatch: Dispatch<ProductAction>;
}

/** Lista produktów na żywo. Po każdym połączeniu serwer wysyła najpierw całą listę. */
export function useProductFeed(onSignedOut: () => void): ProductFeed {
  const [products, dispatch] = useReducer(productsReducer, []);
  const [loaded, setLoaded] = useState(false);
  const [status, setStatus] = useState<FeedStatus>('connecting');

  useEffect(() => {
    let source: EventSource | null = null;
    let retryTimer: number | undefined;
    let lastSignal = Date.now();
    let stopped = false;

    const scheduleReconnect = () => {
      source?.close();
      setStatus('offline');
      window.clearTimeout(retryTimer);
      retryTimer = window.setTimeout(() => void reconnect(), RECONNECT_DELAY_MS);
    };

    const reconnect = async () => {
      try {
        const session = await loadSession();
        if (session.login === null) {
          onSignedOut();
          return;
        }
      } catch {
        // Bez sieci: ponawianie pokaże stan offline, a sesja zostanie sprawdzona następnym razem.
      }
      if (!stopped) {
        connect();
      }
    };

    const connect = () => {
      lastSignal = Date.now();
      source = new EventSource('/api/events');

      const listen = (type: string, handle: (data: never) => void) =>
        source?.addEventListener(type, (event) => {
          lastSignal = Date.now();
          handle(JSON.parse((event as MessageEvent<string>).data) as never);
        });

      listen('products', (list: Product[]) => {
        dispatch({ type: 'list', products: list });
        setLoaded(true);
        setStatus('live');
      });
      listen('product', (product: Product) => dispatch({ type: 'product', product }));
      listen('removed', ({ productId }: { productId: string }) => dispatch({ type: 'removed', productId }));
      listen('ping', () => undefined);

      // Po zerwaniu sieci EventSource wznawia się sam, ale po 401 zamyka się na stałe.
      source.onerror = () => {
        if (source?.readyState === EventSource.CLOSED) {
          scheduleReconnect();
        } else {
          setStatus('offline');
        }
      };
    };

    const watchdog = window.setInterval(() => {
      if (Date.now() - lastSignal > SILENCE_LIMIT_MS) {
        scheduleReconnect();
      }
    }, WATCHDOG_INTERVAL_MS);

    connect();
    return () => {
      stopped = true;
      source?.close();
      window.clearTimeout(retryTimer);
      window.clearInterval(watchdog);
    };
  }, [onSignedOut]);

  return { products, loaded, status, dispatch };
}
