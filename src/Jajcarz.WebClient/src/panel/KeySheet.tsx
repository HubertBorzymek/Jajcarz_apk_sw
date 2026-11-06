import { useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, bindProduct, identifyKey, unbindProduct } from '../api/client';
import type { PanelKeyView, Product } from '../api/types';
import { describeKey } from '../products/format';
import { sortForBinding } from './panelFormat';

interface KeySheetProps {
  deviceId: string;
  panelKey: PanelKeyView;
  products: Product[];
  onChanged: () => Promise<void>;
  onUnauthorized: () => void;
  onClose: () => void;
}

/** Przypięcie produktu do klawisza. Przenosi też produkt z innego klawisza. */
export function KeySheet({ deviceId, panelKey, products, onChanged, onUnauthorized, onClose }: KeySheetProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [productId, setProductId] = useState(panelKey.productId ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const options = useMemo(() => sortForBinding(products), [products]);

  useEffect(() => {
    dialog.current?.showModal();
  }, []);

  const run = async (action: () => Promise<void>, closeAfter: boolean) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      await onChanged();
      if (closeAfter) {
        onClose();
      }
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 401) {
        onUnauthorized();
        return;
      }
      setError(caught instanceof ApiError ? caught.message : 'Brak połączenia z serwerem.');
    }
    setBusy(false);
  };

  const save = () =>
    void run(async () => {
      if (productId !== '' && productId !== panelKey.productId) {
        await bindProduct(productId, deviceId, panelKey.addr);
      }
    }, true);

  const unbind = () => {
    if (panelKey.productId !== null) {
      const id = panelKey.productId;
      void run(() => unbindProduct(id), true);
    }
  };

  const identify = () => void run(() => identifyKey(deviceId, panelKey.addr), false);

  return (
    <dialog ref={dialog} className="sheet" onClose={onClose} aria-label="Klawisz panelu">
      <div className="form">
        <h2 className="sheet-title">{describeKey(panelKey.addr)}</h2>
        <label className="field">
          <span>Produkt</span>
          <select value={productId} onChange={(event) => setProductId(event.target.value)}>
            <option value="">Bez produktu</option>
            {options.map((product) => (
              <option key={product.id} value={product.id}>
                {product.name}
                {product.addr && product.addr !== panelKey.addr ? ` (${describeKey(product.addr)})` : ''}
              </option>
            ))}
          </select>
        </label>
        {products.length === 0 && <p className="sheet-note">Najpierw dodaj produkt na liście.</p>}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <button type="button" className="button" disabled={busy} onClick={identify}>
          Mignij tym klawiszem
        </button>
        <div className="sheet-actions">
          <button type="button" className="button" onClick={onClose}>
            Anuluj
          </button>
          <button
            type="button"
            className="button button-primary"
            disabled={busy || productId === ''}
            onClick={save}
          >
            Zapisz
          </button>
        </div>
        {panelKey.productId !== null && (
          <button type="button" className="button button-danger" disabled={busy} onClick={unbind}>
            Odepnij produkt
          </button>
        )}
      </div>
    </dialog>
  );
}
