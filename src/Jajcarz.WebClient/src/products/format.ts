import type { ProductState } from '../api/types';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const RELATIVE_DAYS_LIMIT = 7;

const relative = new Intl.RelativeTimeFormat('pl', { numeric: 'auto' });
const shortDate = new Intl.DateTimeFormat('pl', { day: 'numeric', month: 'short' });

export const STATE_LABELS: Record<ProductState, string> = {
  OK: 'Jest',
  LOW: 'Mało',
  OUT: 'Brak',
};

export const STATES: ProductState[] = ['OK', 'LOW', 'OUT'];

export function describeActor(actor: string): string {
  if (actor.startsWith('web:')) {
    return actor.slice('web:'.length);
  }
  return actor.startsWith('device:') ? 'panel' : 'system';
}

export function describeTime(iso: string, now: number): string {
  const elapsed = now - new Date(iso).getTime();
  if (elapsed < MINUTE) {
    return 'przed chwilą';
  }
  if (elapsed < HOUR) {
    return relative.format(-Math.floor(elapsed / MINUTE), 'minute');
  }
  if (elapsed < DAY) {
    return relative.format(-Math.floor(elapsed / HOUR), 'hour');
  }
  if (elapsed < RELATIVE_DAYS_LIMIT * DAY) {
    return relative.format(-Math.floor(elapsed / DAY), 'day');
  }
  return shortDate.format(new Date(iso));
}

/** Protokół liczy od zera, a ludzie od jedynki: adres 0.2 to trzeci klawisz. */
export function describeKey(addr: string): string {
  const [segment, key] = addr.split('.').map(Number);
  return segment === 0 ? `klawisz ${key + 1}` : `moduł ${segment}, klawisz ${key + 1}`;
}
