import type { Product } from '../api/types';
import type { QueuedChange } from './changeQueue';

/** Dane telefonu potrzebne bez zasięgu. Pamięć bywa niedostępna, więc każdy zapis może się nie udać. */

const LOGIN_KEY = 'jajcarz.v1.login';
const PRODUCTS_KEY = 'jajcarz.v1.products';
const QUEUE_KEY = 'jajcarz.v1.queue';

interface OwnedList<T> {
  login: string;
  items: T[];
}

function read(storage: Storage, key: string): string | null {
  try {
    return storage.getItem(key);
  } catch {
    return null;
  }
}

function write(storage: Storage, key: string, value: string): void {
  try {
    storage.setItem(key, value);
  } catch {
    // Przepełnienie lub tryb prywatny: aplikacja działa dalej, tylko bez pamięci.
  }
}

function readOwned<T>(storage: Storage, key: string, login: string): T[] {
  try {
    const list = JSON.parse(read(storage, key) ?? 'null') as OwnedList<T> | null;
    return list?.login === login && Array.isArray(list.items) ? list.items : [];
  } catch {
    return [];
  }
}

function writeOwned<T>(storage: Storage, key: string, login: string, items: T[]): void {
  write(storage, key, JSON.stringify({ login, items } satisfies OwnedList<T>));
}

export function loadLogin(storage: Storage = localStorage): string | null {
  return read(storage, LOGIN_KEY);
}

export function saveLogin(login: string, storage: Storage = localStorage): void {
  write(storage, LOGIN_KEY, login);
}

export function loadProducts(login: string, storage: Storage = localStorage): Product[] {
  return readOwned<Product>(storage, PRODUCTS_KEY, login);
}

export function saveProducts(login: string, products: Product[], storage: Storage = localStorage): void {
  writeOwned(storage, PRODUCTS_KEY, login, products);
}

export function loadQueue(login: string, storage: Storage = localStorage): QueuedChange[] {
  return readOwned<QueuedChange>(storage, QUEUE_KEY, login);
}

export function saveQueue(login: string, queue: QueuedChange[], storage: Storage = localStorage): void {
  writeOwned(storage, QUEUE_KEY, login, queue);
}

/** Po wylogowaniu lista produktów nie może zostać na telefonie. */
export function clearLocalData(storage: Storage = localStorage): void {
  for (const key of [LOGIN_KEY, PRODUCTS_KEY, QUEUE_KEY]) {
    try {
      storage.removeItem(key);
    } catch {
      // Tak samo jak przy zapisie.
    }
  }
}
