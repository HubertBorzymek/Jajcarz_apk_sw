import { useMemo, useState, type Dispatch } from 'react';
import type { Product, ProductState } from '../api/types';
import { groupProducts, listCategories, type ProductAction } from './productList';
import { ProductRow } from './ProductRow';
import { ProductSheet, type SheetTarget } from './ProductSheet';

interface ProductsPageProps {
  products: Product[];
  loaded: boolean;
  pending: Record<string, ProductState>;
  now: number;
  dispatch: Dispatch<ProductAction>;
  onChangeState: (productId: string, state: ProductState) => void;
  onSignedOut: () => void;
}

export function ProductsPage({ products, loaded, pending, now, dispatch, onChangeState, onSignedOut }: ProductsPageProps) {
  const [sheet, setSheet] = useState<SheetTarget | null>(null);
  const groups = useMemo(() => groupProducts(products), [products]);
  const categories = useMemo(() => listCategories(products), [products]);

  return (
    <>
      <main className="products">
        {!loaded && <p className="center-note">Wczytywanie produktów…</p>}
        {loaded && products.length === 0 && (
          <div className="center-note">
            <p>Nie ma jeszcze produktów.</p>
            <p>Dodaj pierwszy przyciskiem na dole.</p>
          </div>
        )}
        {groups.map((group) => (
          <section key={group.category ?? ''} className="group">
            {groups.length > 1 && <h2 className="group-title">{group.category ?? 'Bez kategorii'}</h2>}
            <ul className="product-list">
              {group.products.map((product) => (
                <ProductRow
                  key={product.id}
                  product={product}
                  pendingState={pending[product.id] ?? null}
                  now={now}
                  onChangeState={onChangeState}
                  onEdit={setSheet}
                />
              ))}
            </ul>
          </section>
        ))}
      </main>

      <button className="add-button" onClick={() => setSheet({ kind: 'new' })}>
        <span aria-hidden="true">+</span> Dodaj
      </button>

      {sheet && (
        <ProductSheet
          target={sheet}
          categories={categories}
          onSaved={(product) => dispatch({ type: 'product', product })}
          onDeleted={(productId) => dispatch({ type: 'removed', productId })}
          onUnauthorized={onSignedOut}
          onClose={() => setSheet(null)}
        />
      )}
    </>
  );
}
