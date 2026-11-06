import { memo } from 'react';
import type { Product, ProductState } from '../api/types';
import { describeActor, describeKey, describeTime, STATE_LABELS, STATES } from './format';
import type { SheetTarget } from './ProductSheet';

interface ProductRowProps {
  product: Product;
  pendingState: ProductState | null;
  now: number;
  onChangeState: (productId: string, state: ProductState) => void;
  onEdit: (target: SheetTarget) => void;
}

function ProductRowView({ product, pendingState, now, onChangeState, onEdit }: ProductRowProps) {
  const displayedState = pendingState ?? product.state;
  const saving = pendingState !== null;

  return (
    <li className="product">
      <button
        className="product-main"
        onClick={() => onEdit({ kind: 'edit', product })}
        aria-label={`Edytuj: ${product.name}`}
      >
        <span className="product-name">{product.name}</span>
        <span className="product-meta">
          {saving ? 'Czeka na wysłanie…' : `${describeActor(product.changedBy)}, ${describeTime(product.changedAt, now)}`}
        </span>
        {product.addr && <span className="product-key">{describeKey(product.addr)}</span>}
      </button>
      <div className="state-picker" role="group" aria-label={`Stan: ${product.name}`} aria-busy={saving}>
        {STATES.map((state) => (
          <button
            key={state}
            className={`state state-${state.toLowerCase()}`}
            aria-pressed={displayedState === state}
            onClick={() => {
              if (displayedState !== state) {
                onChangeState(product.id, state);
              }
            }}
          >
            {STATE_LABELS[state]}
          </button>
        ))}
      </div>
    </li>
  );
}

export const ProductRow = memo(ProductRowView);
