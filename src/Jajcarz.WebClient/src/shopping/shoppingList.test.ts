import { describe, expect, it } from 'vitest';
import type { Product } from '../api/types';
import { buildShoppingList, countToBuy } from './shoppingList';

function product(overrides: Partial<Product>): Product {
  return {
    id: 'a',
    name: 'Mleko',
    category: null,
    state: 'OK',
    rev: 1,
    changedBy: 'web:hubert',
    changedAt: '2026-11-05T12:00:00Z',
    addr: null,
    ...overrides,
  };
}

describe('buildShoppingList', () => {
  it('pomija produkty, które są', () => {
    expect(buildShoppingList([product({ state: 'OK' })], {}, {})).toEqual([]);
  });

  it('pokazuje najpierw braki, potem to, co się kończy', () => {
    const list = buildShoppingList(
      [
        product({ id: 'a', name: 'Masło', state: 'LOW' }),
        product({ id: 'b', name: 'Żurek', state: 'OUT' }),
        product({ id: 'c', name: 'Cukier', state: 'OUT' }),
      ],
      {},
      {},
    );

    expect(list[0].items.map((item) => item.product.name)).toEqual(['Cukier', 'Żurek', 'Masło']);
  });

  it('grupuje kategorie, a produkty bez kategorii daje na koniec', () => {
    const list = buildShoppingList(
      [
        product({ id: 'a', state: 'LOW' }),
        product({ id: 'b', state: 'LOW', category: 'Warzywa' }),
        product({ id: 'c', state: 'LOW', category: 'Nabiał' }),
      ],
      {},
      {},
    );

    expect(list.map((group) => group.category)).toEqual(['Nabiał', 'Warzywa', null]);
  });

  it('uwzględnia zmianę czekającą na wysłanie', () => {
    const list = buildShoppingList([product({ state: 'OK' })], { a: 'OUT' }, {});

    expect(list[0].items[0].state).toBe('OUT');
  });

  it('zostawia kupioną pozycję na tym samym miejscu, przekreśloną', () => {
    const products = [
      product({ id: 'a', name: 'Masło', state: 'LOW' }),
      product({ id: 'b', name: 'Żurek', state: 'OUT' }),
    ];
    const items = buildShoppingList(products, { b: 'OK' }, { b: 'OUT' })[0].items;

    expect(items.map((item) => item.product.name)).toEqual(['Żurek', 'Masło']);
    expect(items[0].boughtFrom).toBe('OUT');
  });

  it('kupiona pozycja zostaje po potwierdzeniu przez serwer', () => {
    const list = buildShoppingList([product({ state: 'OK' })], {}, { a: 'OUT' });

    expect(list[0].items[0].boughtFrom).toBe('OUT');
  });

  it('pozycja oznaczona jako brak po kupieniu nie jest już przekreślona', () => {
    const list = buildShoppingList([product({ state: 'LOW' })], {}, { a: 'OUT' });

    expect(list[0].items[0].boughtFrom).toBeNull();
  });
});

describe('countToBuy', () => {
  it('liczy produkty do kupienia razem z oczekującymi zmianami', () => {
    const products = [product({ id: 'a', state: 'LOW' }), product({ id: 'b', state: 'OUT' }), product({ id: 'c' })];

    expect(countToBuy(products, { b: 'OK', c: 'LOW' })).toBe(2);
  });
});
