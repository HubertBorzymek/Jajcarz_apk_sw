import { useCallback, useEffect, useState } from 'react';
import { loadSession } from './api/client';
import { LoginPage } from './auth/LoginPage';
import { ProductsPage } from './products/ProductsPage';

type SessionState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ready'; login: string | null };

export function App() {
  const [session, setSession] = useState<SessionState>({ kind: 'loading' });

  const refresh = useCallback(async () => {
    try {
      const { login } = await loadSession();
      setSession({ kind: 'ready', login });
    } catch {
      setSession({ kind: 'unavailable' });
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  if (session.kind === 'loading') {
    return <p className="center-note">Wczytywanie…</p>;
  }
  if (session.kind === 'unavailable') {
    return (
      <div className="center-note">
        <p>Nie można połączyć się z serwerem.</p>
        <button className="button" onClick={() => void refresh()}>
          Spróbuj ponownie
        </button>
      </div>
    );
  }
  if (session.login === null) {
    return <LoginPage onLoggedIn={refresh} />;
  }
  return <ProductsPage login={session.login} onSignedOut={refresh} />;
}
