import type { Product } from '../api/types';

export type ProductAction =
  | { type: 'list'; products: Product[] }
  | { type: 'product'; product: Product }
  | { type: 'removed'; productId: string };

/**
 * Lista z serwera. Odpowiedź na zmianę i zdarzenie o tej samej zmianie mogą przyjść
 * w dowolnej kolejności, więc starszy rev nie nadpisuje nowszego.
 */
export function productsReducer(products: Product[], action: ProductAction): Product[] {
  switch (action.type) {
    case 'list':
      return action.products;
    case 'removed':
      return products.filter((product) => product.id !== action.productId);
    case 'product': {
      const known = products.find((product) => product.id === action.product.id);
      if (!known) {
        return [...products, action.product];
      }
      return known.rev > action.product.rev
        ? products
        : products.map((product) => (product.id === action.product.id ? action.product : product));
    }
  }
}

export interface ProductGroup {
  category: string | null;
  products: Product[];
}

const collator = new Intl.Collator('pl', { sensitivity: 'base' });

/** Kategorie alfabetycznie, a produkty bez kategorii na końcu. */
export function groupProducts(products: Product[]): ProductGroup[] {
  const groups = new Map<string | null, Product[]>();
  for (const product of products) {
    groups.set(product.category, [...(groups.get(product.category) ?? []), product]);
  }
  return [...groups.entries()]
    .map(([category, items]) => ({
      category,
      products: [...items].sort((a, b) => collator.compare(a.name, b.name)),
    }))
    .sort((a, b) => {
      if (a.category === null || b.category === null) {
        return a.category === b.category ? 0 : a.category === null ? 1 : -1;
      }
      return collator.compare(a.category, b.category);
    });
}

export function listCategories(products: Product[]): string[] {
  const categories = products.flatMap((product) => (product.category ? [product.category] : []));
  return [...new Set(categories)].sort(collator.compare);
}
