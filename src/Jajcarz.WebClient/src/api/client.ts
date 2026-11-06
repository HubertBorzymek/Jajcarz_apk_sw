import type { Product, ProductFields, ProductState, Session, StateChangeResult } from './types';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

/** Przy słabym zasięgu żądanie potrafi wisieć bez końca, a użytkownik musi wiedzieć, że się nie udało. */
const REQUEST_TIMEOUT_MS = 10_000;

let csrfToken = '';

/** Publiczne. Odświeża też token CSRF, który zależy od zalogowanego konta. */
export async function loadSession(): Promise<Session> {
  const response = await fetch('/api/session', { signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
  if (!response.ok) {
    throw new ApiError(response.status, 'Nie można odczytać sesji.');
  }
  const session = (await response.json()) as Session;
  csrfToken = session.csrfToken;
  return session;
}

async function send(method: string, path: string, body?: unknown, canRetry = true): Promise<Response> {
  const response = await fetch(path, {
    method,
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });

  // Token ginie razem z cookie CSRF, na przykład po zamknięciu przeglądarki.
  if (response.status === 403 && canRetry) {
    await loadSession();
    return send(method, path, body, false);
  }
  if (!response.ok) {
    throw new ApiError(response.status, await errorMessage(response));
  }
  return response;
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string };
    return problem.detail ?? 'Serwer odrzucił zmianę.';
  } catch {
    return 'Serwer odrzucił zmianę.';
  }
}

export async function logIn(login: string, password: string): Promise<void> {
  await send('POST', '/api/session', { login, password });
}

export async function logOut(): Promise<void> {
  await send('DELETE', '/api/session');
}

export async function createProduct(fields: ProductFields): Promise<Product> {
  const response = await send('POST', '/api/products', fields);
  return (await response.json()) as Product;
}

export async function updateProduct(id: string, fields: ProductFields): Promise<Product> {
  const response = await send('PUT', `/api/products/${id}`, fields);
  return (await response.json()) as Product;
}

export async function deleteProduct(id: string): Promise<void> {
  await send('DELETE', `/api/products/${id}`);
}

export async function changeProductState(
  id: string,
  state: ProductState,
  ageMs: number,
  changeKey: string,
): Promise<StateChangeResult> {
  const response = await send('POST', `/api/products/${id}/state`, { state, ageMs, changeKey });
  return (await response.json()) as StateChangeResult;
}
