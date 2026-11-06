import { useCallback, useEffect, useState } from 'react';
import { loadSession } from './api/client';
import { AppShell } from './app/AppShell';
import { LoginPage } from './auth/LoginPage';
import { clearLocalData, loadLogin, saveLogin } from './offline/localData';

type SessionState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ready'; login: string | null };

function initialSession(): SessionState {
  // W sklepie bez zasięgu aplikacja ma się otworzyć od razu. Serwer potwierdzi sesję, gdy będzie dostępny.
  const login = loadLogin();
  return login === null ? { kind: 'loading' } : { kind: 'ready', login };
}

export function App() {
  const [session, setSession] = useState<SessionState>(initialSession);

  const refresh = useCallback(async () => {
    try {
      const { login } = await loadSession();
      if (login === null) {
        clearLocalData();
      } else {
        saveLogin(login);
      }
      setSession({ kind: 'ready', login });
    } catch {
      setSession((current) => (current.kind === 'ready' && current.login !== null ? current : { kind: 'unavailable' }));
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
  return <AppShell key={session.login} login={session.login} onSignedOut={refresh} />;
}
