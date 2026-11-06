import { useMemo, useState } from 'react';
import type { Product, ProductState } from '../api/types';
import { STATE_LABELS } from '../products/format';
import { buildShoppingList, type ShoppingItem } from './shoppingList';

interface ShoppingPageProps {
  products: Product[];
  loaded: boolean;
  pending: Record<string, ProductState>;
  onChangeState: (productId: string, state: ProductState) => void;
}

/** Lista zakupów. Stan kupionych pozycji znika razem z ekranem, bo wtedy nie są już potrzebne. */
export function ShoppingPage({ products, loaded, pending, onChangeState }: ShoppingPageProps) {
  const [bought, setBought] = useState<Record<string, ProductState>>({});
  const groups = useMemo(() => buildShoppingList(products, pending, bought), [products, pending, bought]);

  const buy = (item: ShoppingItem) => {
    setBought((current) => ({ ...current, [item.product.id]: item.state }));
    onChangeState(item.product.id, 'OK');
  };

  const undo = (item: ShoppingItem) => {
    const { [item.product.id]: _undone, ...rest } = bought;
    setBought(rest);
    if (item.boughtFrom !== null) {
      onChangeState(item.product.id, item.boughtFrom);
    }
  };

  return (
    <main className="products">
      {!loaded && <p className="center-note">Wczytywanie produktów…</p>}
      {loaded && groups.length === 0 && (
        <div className="center-note">
          <p>Nic nie trzeba kupować.</p>
          <p>Wszystkie produkty są w domu.</p>
        </div>
      )}
      {groups.map((group) => (
        <section key={group.category ?? ''} className="group">
          {groups.length > 1 && <h2 className="group-title">{group.category ?? 'Bez kategorii'}</h2>}
          <ul className="product-list">
            {group.items.map((item) => {
              const isBought = item.boughtFrom !== null;
              const state = item.boughtFrom ?? item.state;
              return (
                <li key={item.product.id} className={isBought ? 'shop-item shop-item-bought' : 'shop-item'}>
                  <span className="shop-name">{item.product.name}</span>
                  <span className={`shop-state shop-state-${state.toLowerCase()}`}>{STATE_LABELS[state]}</span>
                  {isBought ? (
                    <button className="button shop-action" onClick={() => undo(item)}>
                      Cofnij
                    </button>
                  ) : (
                    <button className="button button-primary shop-action" onClick={() => buy(item)}>
                      Kupione
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </main>
  );
}
