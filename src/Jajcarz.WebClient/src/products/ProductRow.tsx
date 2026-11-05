import { memo } from 'react';
import type { Product, ProductState } from '../api/types';
import { describeActor, describeKey, describeTime, STATE_LABELS, STATES } from './format';

interface ProductRowProps {
  product: Product;
  displayedState: ProductState;
  saving: boolean;
  now: number;
  onChangeState: (state: ProductState) => void;
  onEdit: () => void;
}

function ProductRowView({ product, displayedState, saving, now, onChangeState, onEdit }: ProductRowProps) {
  return (
    <li className="product">
      <button className="product-main" onClick={onEdit} aria-label={`Edytuj: ${product.name}`}>
        <span className="product-name">{product.name}</span>
        <span className="product-meta">
          {saving ? 'Zapisywanie…' : `${describeActor(product.changedBy)}, ${describeTime(product.changedAt, now)}`}
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
                onChangeState(state);
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

export const ProductRow = memo(ProductRowView, (previous, next) => {
  return (
    previous.product === next.product &&
    previous.displayedState === next.displayedState &&
    previous.saving === next.saving &&
    previous.now === next.now
  );
});
