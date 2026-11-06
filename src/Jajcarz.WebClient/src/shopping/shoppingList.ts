import type { Product, ProductState } from '../api/types';

export interface ShoppingItem {
  product: Product;
  state: ProductState;
  /** Stan sprzed „Kupione”, gdy pozycja jest przekreślona. */
  boughtFrom: ProductState | null;
}

export interface ShoppingGroup {
  category: string | null;
  items: ShoppingItem[];
}

const collator = new Intl.Collator('pl', { sensitivity: 'base' });

function compareItems(a: ShoppingItem, b: ShoppingItem): number {
  // Kolejność nie może się zmienić po dotknięciu, bo pozycja uciekałaby spod palca.
  const aState = a.boughtFrom ?? a.state;
  const bState = b.boughtFrom ?? b.state;
  return aState === bState ? collator.compare(a.product.name, b.product.name) : aState === 'OUT' ? -1 : 1;
}

function compareGroups(a: ShoppingGroup, b: ShoppingGroup): number {
  if (a.category === null || b.category === null) {
    return a.category === b.category ? 0 : a.category === null ? 1 : -1;
  }
  return collator.compare(a.category, b.category);
}

/**
 * Co trzeba kupić. Kupione pozycje zostają przekreślone do wyjścia z widoku.
 * Pozycja, którą ktoś w tym czasie znów oznaczył jako brakującą, wraca do listy.
 */
export function buildShoppingList(
  products: Product[],
  pending: Record<string, ProductState>,
  bought: Record<string, ProductState>,
): ShoppingGroup[] {
  const groups = new Map<string | null, ShoppingItem[]>();
  for (const product of products) {
    const state = pending[product.id] ?? product.state;
    const boughtFrom = state === 'OK' ? (bought[product.id] ?? null) : null;
    if (state === 'OK' && boughtFrom === null) {
      continue;
    }
    groups.set(product.category, [...(groups.get(product.category) ?? []), { product, state, boughtFrom }]);
  }
  return [...groups.entries()]
    .map(([category, items]) => ({ category, items: [...items].sort(compareItems) }))
    .sort(compareGroups);
}

export function countToBuy(products: Product[], pending: Record<string, ProductState>): number {
  return products.filter((product) => (pending[product.id] ?? product.state) !== 'OK').length;
}
