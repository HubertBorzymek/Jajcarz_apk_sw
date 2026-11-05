import { useState, type FormEvent } from 'react';
import { ApiError, logIn } from '../api/client';

interface LoginPageProps {
  onLoggedIn: () => Promise<void>;
}

function describeError(error: unknown): string {
  if (error instanceof ApiError && error.status === 401) {
    return 'Nieprawidłowy login lub hasło.';
  }
  if (error instanceof ApiError && error.status === 429) {
    return 'Zbyt wiele prób. Spróbuj ponownie za minutę.';
  }
  return 'Nie można połączyć się z serwerem.';
}

export function LoginPage({ onLoggedIn }: LoginPageProps) {
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await logIn(login, password);
      await onLoggedIn();
    } catch (caught) {
      setError(describeError(caught));
      setBusy(false);
    }
  };

  return (
    <main className="login">
      <h1 className="wordmark">Jajcarz</h1>
      <p className="login-lead">Co jest w domu i co trzeba kupić.</p>
      <form className="form" onSubmit={(event) => void submit(event)}>
        <label className="field">
          <span>Login</span>
          <input
            value={login}
            onChange={(event) => setLogin(event.target.value)}
            autoComplete="username"
            autoCapitalize="none"
            autoCorrect="off"
            required
          />
        </label>
        <label className="field">
          <span>Hasło</span>
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            required
          />
        </label>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <button className="button button-primary" disabled={busy}>
          {busy ? 'Logowanie…' : 'Zaloguj się'}
        </button>
      </form>
    </main>
  );
}
