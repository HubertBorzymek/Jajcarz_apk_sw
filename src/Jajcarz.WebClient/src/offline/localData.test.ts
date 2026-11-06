import { describe, expect, it } from 'vitest';
import type { QueuedChange } from './changeQueue';
import { clearLocalData, loadLogin, loadQueue, saveLogin, saveQueue } from './localData';

function memoryStorage(initial: Record<string, string> = {}): Storage {
  const data = new Map(Object.entries(initial));
  return {
    get length() {
      return data.size;
    },
    clear: () => data.clear(),
    getItem: (key) => data.get(key) ?? null,
    key: (index) => [...data.keys()][index] ?? null,
    removeItem: (key) => void data.delete(key),
    setItem: (key, value) => void data.set(key, value),
  };
}

const change: QueuedChange = { productId: 'a', state: 'OK', changeKey: 'k', queuedAt: 1 };

describe('localData', () => {
  it('zachowuje kolejkę konta', () => {
    const storage = memoryStorage();

    saveQueue('hubert', [change], storage);

    expect(loadQueue('hubert', storage)).toEqual([change]);
  });

  it('nie pokazuje danych innego konta', () => {
    const storage = memoryStorage();

    saveQueue('hubert', [change], storage);

    expect(loadQueue('anna', storage)).toEqual([]);
  });

  it('ignoruje uszkodzony zapis', () => {
    const storage = memoryStorage({ 'jajcarz.v1.queue': '{nie json' });

    expect(loadQueue('hubert', storage)).toEqual([]);
  });

  it('usuwa dane po wylogowaniu', () => {
    const storage = memoryStorage();
    saveLogin('hubert', storage);
    saveQueue('hubert', [change], storage);

    clearLocalData(storage);

    expect(loadLogin(storage)).toBeNull();
    expect(loadQueue('hubert', storage)).toEqual([]);
  });

  it('nie przerywa działania, gdy pamięć jest niedostępna', () => {
    const broken: Storage = {
      ...memoryStorage(),
      getItem: () => {
        throw new Error('zablokowane');
      },
      setItem: () => {
        throw new Error('zablokowane');
      },
    };

    expect(() => saveLogin('hubert', broken)).not.toThrow();
    expect(loadLogin(broken)).toBeNull();
  });
});
