import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { changeProductState } from '../api/client';
import type { Product, ProductState } from '../api/types';
import {
  changeAge,
  classifyFailure,
  createChangeKey,
  enqueueChange,
  removeChange,
  type QueuedChange,
} from './changeQueue';
import { loadQueue, saveQueue } from './localData';

const RETRY_DELAY_MS = 5_000;

interface ChangeQueueOptions {
  login: string;
  online: boolean;
  onSaved: (product: Product) => void;
  onNotice: (message: string) => void;
  onSignedOut: () => void;
  nameOf: (productId: string) => string;
}

interface ChangeQueue {
  /** Stan czekający na wysłanie, według produktu. */
  pending: Record<string, ProductState>;
  pendingCount: number;
  changeState: (productId: string, state: ProductState) => void;
}

/** Zmiany stanu najpierw trafiają do trwałej kolejki, więc działają tak samo z zasięgiem i bez niego. */
export function useChangeQueue({ login, online, onSaved, onNotice, onSignedOut, nameOf }: ChangeQueueOptions): ChangeQueue {
  const [queue, setQueue] = useState(() => loadQueue(login));
  const current = useRef(queue);
  const flushing = useRef(false);
  const retryTimer = useRef<number | undefined>(undefined);
  const stopped = useRef(false);

  // Wysyłka trwa dłużej niż jedno renderowanie, a nazwa i wywołania zwrotne mogą się w tym czasie zmienić.
  const handlers = useRef({ onSaved, onNotice, onSignedOut, nameOf });
  handlers.current = { onSaved, onNotice, onSignedOut, nameOf };

  const update = useCallback(
    (change: (queue: QueuedChange[]) => QueuedChange[]) => {
      current.current = change(current.current);
      saveQueue(login, current.current);
      setQueue(current.current);
    },
    [login],
  );

  const flush = useCallback(async () => {
    if (flushing.current) {
      return;
    }
    flushing.current = true;
    window.clearTimeout(retryTimer.current);
    try {
      while (!stopped.current && current.current.length > 0) {
        const change = current.current[0];
        try {
          const result = await changeProductState(
            change.productId,
            change.state,
            changeAge(change, Date.now()),
            change.changeKey,
          );
          update((queue) => removeChange(queue, change.changeKey));
          handlers.current.onSaved(result.product);
          if (!result.accepted) {
            handlers.current.onNotice(`${result.product.name}: ktoś zapisał nowszą zmianę.`);
          }
        } catch (error) {
          const action = classifyFailure(error);
          if (action === 'signed-out') {
            handlers.current.onSignedOut();
            return;
          }
          if (action === 'retry') {
            retryTimer.current = window.setTimeout(() => void flush(), RETRY_DELAY_MS);
            return;
          }
          update((queue) => removeChange(queue, change.changeKey));
          handlers.current.onNotice(`Nie zapisano zmiany: ${handlers.current.nameOf(change.productId)}.`);
        }
      }
    } finally {
      flushing.current = false;
    }
  }, [update]);

  const changeState = useCallback(
    (productId: string, state: ProductState) => {
      update((queue) =>
        enqueueChange(queue, { productId, state, changeKey: createChangeKey(), queuedAt: Date.now() }),
      );
      void flush();
    },
    [flush, update],
  );

  useEffect(() => {
    stopped.current = false;
    if (online) {
      void flush();
    }
    const flushNow = () => void flush();
    window.addEventListener('online', flushNow);
    return () => {
      stopped.current = true;
      window.clearTimeout(retryTimer.current);
      window.removeEventListener('online', flushNow);
    };
  }, [flush, online]);

  const pending = useMemo(
    () => Object.fromEntries(queue.map((change) => [change.productId, change.state])),
    [queue],
  );

  return { pending, pendingCount: queue.length, changeState };
}
