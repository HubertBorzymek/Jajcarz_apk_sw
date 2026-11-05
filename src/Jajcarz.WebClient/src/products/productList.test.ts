import { describe, expect, it } from 'vitest';
import type { Product } from '../api/types';
import { groupProducts, listCategories, productsReducer } from './productList';

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

describe('productsReducer', () => {
  it('zastępuje listę po ponownym połączeniu', () => {
    const list = [product({ id: 'b' })];

    expect(productsReducer([product({})], { type: 'list', products: list })).toEqual(list);
  });

  it('dodaje nowy produkt', () => {
    const added = product({ id: 'b' });

    expect(productsReducer([product({})], { type: 'product', product: added })).toHaveLength(2);
  });

  it('nowszy rev zastępuje produkt', () => {
    const newer = product({ rev: 2, state: 'OUT' });

    expect(productsReducer([product({})], { type: 'product', product: newer })).toEqual([newer]);
  });

  it('starszy rev nie cofa stanu', () => {
    const current = [product({ rev: 5, state: 'OUT' })];

    expect(productsReducer(current, { type: 'product', product: product({ rev: 4, state: 'LOW' }) })).toBe(current);
  });

  it('ten sam rev przyjmuje zmianę nazwy', () => {
    const renamed = product({ name: 'Mleko 2%' });

    expect(productsReducer([product({})], { type: 'product', product: renamed })).toEqual([renamed]);
  });

  it('usuwa produkt', () => {
    expect(productsReducer([product({})], { type: 'removed', productId: 'a' })).toEqual([]);
  });
});

describe('groupProducts', () => {
  it('sortuje po polsku i stawia produkty bez kategorii na końcu', () => {
    const groups = groupProducts([
      product({ id: '1', name: 'Żurek', category: null }),
      product({ id: '2', name: 'Ser', category: 'Nabiał' }),
      product({ id: '3', name: 'Masło', category: 'Nabiał' }),
      product({ id: '4', name: 'Łosoś', category: 'Ryby' }),
      product({ id: '5', name: 'Chleb', category: 'Ćwiartki' }),
    ]);

    expect(groups.map((group) => group.category)).toEqual(['Ćwiartki', 'Nabiał', 'Ryby', null]);
    expect(groups[1].products.map((item) => item.name)).toEqual(['Masło', 'Ser']);
  });
});

describe('listCategories', () => {
  it('zwraca kategorie bez powtórzeń', () => {
    const categories = listCategories([
      product({ category: 'Nabiał' }),
      product({ category: 'Nabiał' }),
      product({ category: null }),
    ]);

    expect(categories).toEqual(['Nabiał']);
  });
});
