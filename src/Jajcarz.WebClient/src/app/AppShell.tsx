import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, logOut } from '../api/client';
import { useChangeQueue } from '../offline/useChangeQueue';
import { PanelPage } from '../panel/PanelPage';
import { ProductsPage } from '../products/ProductsPage';
import { useProductFeed, type FeedStatus } from '../products/useProductFeed';
import { countToBuy } from '../shopping/shoppingList';
import { ShoppingPage } from '../shopping/ShoppingPage';
import { useRoute } from './route';
import { TabBar } from './TabBar';

const NOW_REFRESH_MS = 30_000;
const NOTICE_DURATION_MS = 4_000;

const STATUS_LABELS: Record<FeedStatus, string> = {
  connecting: 'Łączenie…',
  live: 'Na żywo',
  offline: 'Brak połączenia',
};

interface AppShellProps {
  login: string;
  onSignedOut: () => Promise<void>;
}

/** Wspólny stan ekranów: lista na żywo, kolejka zmian, nagłówek i dolna nawigacja. */
export function AppShell({ login, onSignedOut }: AppShellProps) {
  const signedOut = useCallback(() => void onSignedOut(), [onSignedOut]);
  const { products, loaded, status, dispatch, panelSignal, keyPress } = useProductFeed(login, signedOut);
  const route = useRoute();
  const [notice, setNotice] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());

  const nameOf = useCallback(
    (productId: string) => products.find((product) => product.id === productId)?.name ?? 'produkt',
    [products],
  );
  const { pending, pendingCount, changeState } = useChangeQueue({
    login,
    online: status === 'live',
    onSaved: (product) => dispatch({ type: 'product', product }),
    onNotice: setNotice,
    onSignedOut: signedOut,
    nameOf,
  });

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

  const toBuy = useMemo(() => countToBuy(products, pending), [products, pending]);

  const signOut = async () => {
    if (pendingCount > 0) {
      setNotice('Poczekaj na wysłanie zmian, zanim się wylogujesz.');
      return;
    }
    try {
      await logOut();
      signedOut();
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        signedOut();
      } else {
        setNotice('Nie można się wylogować bez połączenia.');
      }
    }
  };

  return (
    <div className="page">
      <header className="topbar">
        <h1 className="wordmark wordmark-small">Jajcarz</h1>
        <span className={`feed-status feed-status-${status}`} role="status">
          {STATUS_LABELS[status]}
          {pendingCount > 0 && ` · czeka: ${pendingCount}`}
        </span>
        <button className="link-button" onClick={() => void signOut()} title={`Zalogowano jako ${login}`}>
          Wyloguj
        </button>
      </header>

      {route === 'products' && (
        <ProductsPage
          products={products}
          loaded={loaded}
          pending={pending}
          now={now}
          dispatch={dispatch}
          onChangeState={changeState}
          onSignedOut={signedOut}
        />
      )}
      {route === 'shopping' && (
        <ShoppingPage products={products} loaded={loaded} pending={pending} onChangeState={changeState} />
      )}
      {route === 'panel' && (
        <PanelPage
          products={products}
          panelSignal={panelSignal}
          keyPress={keyPress}
          now={now}
          onNotice={setNotice}
          onSignedOut={signedOut}
        />
      )}

      <TabBar route={route} toBuy={toBuy} />

      {notice && (
        <div className="notice" role="alert">
          {notice}
        </div>
      )}
    </div>
  );
}
