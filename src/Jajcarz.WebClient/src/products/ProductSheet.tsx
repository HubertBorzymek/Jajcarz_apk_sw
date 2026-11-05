import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ApiError, createProduct, deleteProduct, updateProduct } from '../api/client';
import type { Product } from '../api/types';
import { describeKey } from './format';

export type SheetTarget = { kind: 'new' } | { kind: 'edit'; product: Product };

interface ProductSheetProps {
  target: SheetTarget;
  categories: string[];
  onSaved: (product: Product) => void;
  onDeleted: (productId: string) => void;
  onUnauthorized: () => void;
  onClose: () => void;
}

/** Dolny arkusz dodawania i edycji. Przyciski są w zasięgu kciuka. */
export function ProductSheet({ target, categories, onSaved, onDeleted, onUnauthorized, onClose }: ProductSheetProps) {
  const product = target.kind === 'edit' ? target.product : null;
  const dialog = useRef<HTMLDialogElement>(null);
  const [name, setName] = useState(product?.name ?? '');
  const [category, setCategory] = useState(product?.category ?? '');
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    dialog.current?.showModal();
  }, []);

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      onClose();
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 401) {
        onUnauthorized();
        return;
      }
      setError(caught instanceof ApiError ? caught.message : 'Brak połączenia z serwerem.');
      setBusy(false);
    }
  };

  const save = (event: FormEvent) => {
    event.preventDefault();
    const fields = { name, category: category.trim() === '' ? null : category };
    void run(async () => {
      onSaved(product ? await updateProduct(product.id, fields) : await createProduct(fields));
    });
  };

  const remove = () => {
    if (!product) {
      return;
    }
    if (!confirmingDelete) {
      setConfirmingDelete(true);
      return;
    }
    void run(async () => {
      await deleteProduct(product.id);
      onDeleted(product.id);
    });
  };

  return (
    <dialog ref={dialog} className="sheet" onClose={onClose} aria-label={product ? 'Edycja produktu' : 'Nowy produkt'}>
      <form className="form" onSubmit={save}>
        <h2 className="sheet-title">{product ? 'Edycja produktu' : 'Nowy produkt'}</h2>
        <label className="field">
          <span>Nazwa</span>
          <input value={name} onChange={(event) => setName(event.target.value)} maxLength={80} required autoFocus />
        </label>
        <label className="field">
          <span>Kategoria (opcjonalnie)</span>
          <input
            value={category}
            onChange={(event) => setCategory(event.target.value)}
            maxLength={40}
            list="product-categories"
          />
          <datalist id="product-categories">
            {categories.map((item) => (
              <option key={item} value={item} />
            ))}
          </datalist>
        </label>
        {product?.addr && <p className="sheet-note">Przypisany: {describeKey(product.addr)}.</p>}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <div className="sheet-actions">
          <button type="button" className="button" onClick={onClose}>
            Anuluj
          </button>
          <button className="button button-primary" disabled={busy}>
            Zapisz
          </button>
        </div>
        {product && (
          <button type="button" className="button button-danger" disabled={busy} onClick={remove}>
            {confirmingDelete ? 'Na pewno usunąć?' : 'Usuń produkt'}
          </button>
        )}
      </form>
    </dialog>
  );
}
