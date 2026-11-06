import { ApiError } from '../api/client';
import type { ProductState } from '../api/types';

/** Zmiana stanu czekająca na wysłanie. Klucz pozwala bezpiecznie ponowić zmianę, która mogła już dotrzeć. */
export interface QueuedChange {
  productId: string;
  state: ProductState;
  changeKey: string;
  queuedAt: number;
}

/** Limit API. Tak stara zmiana i tak przegra z każdą nowszą. */
const MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000;

const KEY_BYTES = 16;

/** Tak samo jak panel: dla produktu liczy się tylko ostatnia zmiana. */
export function enqueueChange(queue: QueuedChange[], change: QueuedChange): QueuedChange[] {
  return [...queue.filter((item) => item.productId !== change.productId), change];
}

/** Po klawiszu, bo w trakcie wysyłki ten sam produkt mógł dostać nowszą zmianę. */
export function removeChange(queue: QueuedChange[], changeKey: string): QueuedChange[] {
  return queue.filter((item) => item.changeKey !== changeKey);
}

export function changeAge(change: QueuedChange, now: number): number {
  return Math.min(Math.max(now - change.queuedAt, 0), MAX_AGE_MS);
}

export type FailureAction = 'retry' | 'drop' | 'signed-out';

/** Błąd sieci i awaria serwera mijają. Odrzuconej zmiany nie ma sensu ponawiać. */
export function classifyFailure(error: unknown): FailureAction {
  if (!(error instanceof ApiError)) {
    return 'retry';
  }
  if (error.status === 401) {
    return 'signed-out';
  }
  return error.status === 408 || error.status === 429 || error.status >= 500 ? 'retry' : 'drop';
}

/** crypto.randomUUID działa tylko na HTTPS, a aplikacja bywa otwierana po adresie w sieci lokalnej. */
export function createChangeKey(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(KEY_BYTES));
  return [...bytes].map((byte) => byte.toString(16).padStart(2, '0')).join('');
}
