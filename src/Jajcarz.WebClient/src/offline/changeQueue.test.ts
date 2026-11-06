import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/client';
import {
  changeAge,
  classifyFailure,
  createChangeKey,
  enqueueChange,
  removeChange,
  type QueuedChange,
} from './changeQueue';

function change(overrides: Partial<QueuedChange>): QueuedChange {
  return { productId: 'a', state: 'OK', changeKey: 'k1', queuedAt: 1000, ...overrides };
}

describe('enqueueChange', () => {
  it('zostawia tylko ostatnią zmianę produktu', () => {
    const queue = enqueueChange([change({ state: 'LOW' })], change({ state: 'OUT', changeKey: 'k2' }));

    expect(queue).toEqual([change({ state: 'OUT', changeKey: 'k2' })]);
  });

  it('nowszą zmianę ustawia na końcu, żeby zachować kolejność', () => {
    const queue = enqueueChange(
      [change({ productId: 'a' }), change({ productId: 'b', changeKey: 'k2' })],
      change({ productId: 'a', changeKey: 'k3' }),
    );

    expect(queue.map((item) => item.productId)).toEqual(['b', 'a']);
  });
});

describe('removeChange', () => {
  it('nie usuwa nowszej zmiany tego samego produktu', () => {
    const queue = [change({ changeKey: 'nowszy' })];

    expect(removeChange(queue, 'wysłany')).toEqual(queue);
  });

  it('usuwa potwierdzoną zmianę', () => {
    expect(removeChange([change({ changeKey: 'k1' })], 'k1')).toEqual([]);
  });
});

describe('changeAge', () => {
  it('liczy czas od dodania do kolejki', () => {
    expect(changeAge(change({ queuedAt: 1000 }), 6400)).toBe(5400);
  });

  it('nie zwraca wartości ujemnej po cofnięciu zegara telefonu', () => {
    expect(changeAge(change({ queuedAt: 9000 }), 1000)).toBe(0);
  });

  it('nie przekracza limitu API', () => {
    const fortyDays = 40 * 24 * 60 * 60 * 1000;

    expect(changeAge(change({ queuedAt: 0 }), fortyDays)).toBe(30 * 24 * 60 * 60 * 1000);
  });
});

describe('classifyFailure', () => {
  it.each([
    [new TypeError('Failed to fetch'), 'retry'],
    [new ApiError(503, 'x'), 'retry'],
    [new ApiError(429, 'x'), 'retry'],
    [new ApiError(404, 'x'), 'drop'],
    [new ApiError(400, 'x'), 'drop'],
    [new ApiError(401, 'x'), 'signed-out'],
  ])('%s -> %s', (error, action) => {
    expect(classifyFailure(error)).toBe(action);
  });
});

describe('createChangeKey', () => {
  it('mieści się w limicie API i się nie powtarza', () => {
    const key = createChangeKey();

    expect(key).toHaveLength(32);
    expect(createChangeKey()).not.toBe(key);
  });
});
