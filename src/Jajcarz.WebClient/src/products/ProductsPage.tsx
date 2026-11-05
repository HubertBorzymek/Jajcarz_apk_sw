import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, changeProductState, logOut } from '../api/client';
import type { Product, ProductState } from '../api/types';
import { groupProducts, listCategories } from './productList';
import { ProductRow } from './ProductRow';
import { ProductSheet, type SheetTarget } from './ProductSheet';
import { useProductFeed, type FeedStatus } from './useProductFeed';

const NOW_REFRESH_MS = 30_000;
const NOTICE_DURATION_MS = 4_000;

const STATUS_LABELS: Record<FeedStatus, string> = {
  connecting: 'Łączenie…',
  live: 'Na żywo',
  offline: 'Brak połączenia',
};

interface ProductsPageProps {
  login: string;
  onSignedOut: () => Promise<void>;
}

interface PendingChange {
  state: ProductState;
  request: number;
}

export function ProductsPage({ login, onSignedOut }: ProductsPageProps) {
  const signedOut = useCallback(() => void onSignedOut(), [onSignedOut]);
  const { products, loaded, status, dispatch } = useProductFeed(signedOut);

  const [pending, setPending] = useState<Record<string, PendingChange>>({});
  const [sheet, setSheet] = useState<SheetTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const nextRequest = useRef(0);

  useEffect(() => {
    setNow(Date.now());
    const timer = window.setInterval(() => setNow(Date.now()), NOW_REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [products]);

  useEffect(() => {
    if (notice === null) {
      return undefined;
    }
    const timer = window.setTimeout(() => setNotice(null), NOTICE_DURATION_MS);
    return () => window.clearTimeout(timer);
  }, [notice]);

  const groups = useMemo(() => groupProducts(products), [products]);
  const categories = useMemo(() => listCategories(products), [products]);

  const reportError = useCallback(
    (error: unknown, message: string) => {
      if (error instanceof ApiError && error.status === 401) {
        signedOut();
      } else {
        setNotice(message);
      }
    },
    [signedOut],
  );

  const changeState = useCallback(
    async (product: Product, state: ProductState) => {
      nextRequest.current += 1;
      const request = nextRequest.current;
      setPending((current) => ({ ...current, [product.id]: { state, request } }));
      try {
        const result = await changeProductState(product.id, state);
        dispatch({ type: 'product', product: result.product });
        if (!result.accepted) {
          setNotice(`${product.name}: ktoś zapisał nowszą zmianę.`);
        }
      } catch (error) {
        reportError(error, `Nie zapisano zmiany: ${product.name}.`);
      } finally {
        // Przy kilku szybkich dotknięciach liczy się tylko ostatnie.
        setPending((current) => {
          if (current[product.id]?.request !== request) {
            return current;
          }
          const { [product.id]: _finished, ...rest } = current;
          return rest;
        });
      }
    },
    [dispatch, reportError],
  );

  const signOut = async () => {
    try {
      await logOut();
      signedOut();
    } catch (error) {
      reportError(error, 'Nie można się wylogować bez połączenia.');
    }
  };

  return (
    <div className="page">
      <header className="topbar">
        <h1 className="wordmark wordmark-small">Jajcarz</h1>
        <span className={`feed-status feed-status-${status}`} role="status">
          {STATUS_LABELS[status]}
        </span>
        <button className="link-button" onClick={() => void signOut()} title={`Zalogowano jako ${login}`}>
          Wyloguj
        </button>
      </header>

      <main className="products">
        {!loaded && <p className="center-note">Wczytywanie produktów…</p>}
        {loaded && products.length === 0 && (
          <div className="center-note">
            <p>Nie ma jeszcze produktów.</p>
            <p>Dodaj pierwszy przyciskiem na dole.</p>
          </div>
        )}
        {groups.map((group) => (
          <section key={group.category ?? ''} className="group">
            {groups.length > 1 && <h2 className="group-title">{group.category ?? 'Bez kategorii'}</h2>}
            <ul className="product-list">
              {group.products.map((product) => (
                <ProductRow
                  key={product.id}
                  product={product}
                  displayedState={pending[product.id]?.state ?? product.state}
                  saving={product.id in pending}
                  now={now}
                  onChangeState={(state) => void changeState(product, state)}
                  onEdit={() => setSheet({ kind: 'edit', product })}
                />
              ))}
            </ul>
          </section>
        ))}
      </main>

      <button className="add-button" onClick={() => setSheet({ kind: 'new' })}>
        <span aria-hidden="true">+</span> Dodaj
      </button>

      {notice && (
        <div className="notice" role="alert">
          {notice}
        </div>
      )}

      {sheet && (
        <ProductSheet
          target={sheet}
          categories={categories}
          onSaved={(product) => dispatch({ type: 'product', product })}
          onDeleted={(productId) => dispatch({ type: 'removed', productId })}
          onUnauthorized={signedOut}
          onClose={() => setSheet(null)}
        />
      )}
    </div>
  );
}
