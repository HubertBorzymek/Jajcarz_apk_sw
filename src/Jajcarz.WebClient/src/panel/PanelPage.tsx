import { useEffect, useRef, useState } from 'react';
import type { PanelKeyView, PanelView, Product } from '../api/types';
import { describeKey, STATE_LABELS } from '../products/format';
import type { KeyPress } from '../products/useProductFeed';
import { ConfigEditor } from './ConfigEditor';
import { KeySheet } from './KeySheet';
import { describeConnection } from './panelFormat';
import { usePanels } from './usePanels';

interface PanelPageProps {
  products: Product[];
  panelSignal: number;
  keyPress: KeyPress | null;
  now: number;
  onNotice: (message: string) => void;
  onSignedOut: () => void;
}

interface OpenKey {
  deviceId: string;
  addr: string;
}

function KeyRow({ panelKey, onOpen }: { panelKey: PanelKeyView; onOpen: () => void }) {
  return (
    <li>
      <button className="key-row" onClick={onOpen}>
        <span className="key-address">{describeKey(panelKey.addr)}</span>
        <span className={panelKey.productName ? 'key-product' : 'key-product key-product-empty'}>
          {panelKey.productName ?? 'Bez produktu'}
        </span>
        {panelKey.state && (
          <span className={`shop-state shop-state-${panelKey.state.toLowerCase()}`}>{STATE_LABELS[panelKey.state]}</span>
        )}
      </button>
    </li>
  );
}

function PanelCard({ panel, now, onOpen }: { panel: PanelView; now: number; onOpen: (addr: string) => void }) {
  return (
    <section className="group">
      <h2 className="group-title">Panel</h2>
      <div className="panel-card">
        <p className={panel.connected ? 'panel-state panel-state-on' : 'panel-state'}>{describeConnection(panel, now)}</p>
        <p className="panel-meta">
          {panel.firmware ? `Firmware ${panel.firmware}, ` : ''}
          numer {panel.id}
        </p>
      </div>
      {panel.keys.length > 0 ? (
        <>
          <h2 className="group-title group-title-spaced">Klawisze</h2>
          <ul className="product-list">
            {panel.keys.map((key) => (
              <KeyRow key={key.addr} panelKey={key} onOpen={() => onOpen(key.addr)} />
            ))}
          </ul>
          <p className="panel-hint">Naciśnij wolny klawisz na panelu albo wybierz go z listy.</p>
        </>
      ) : (
        <p className="panel-hint">Klawisze pojawią się po pierwszym połączeniu panelu.</p>
      )}
    </section>
  );
}

export function PanelPage({ products, panelSignal, keyPress, now, onNotice, onSignedOut }: PanelPageProps) {
  const { panels, status, refresh } = usePanels(panelSignal, onSignedOut);
  const [openKey, setOpenKey] = useState<OpenKey | null>(null);

  const seenKeyPress = useRef(keyPress);

  // Naciśnięcie wolnego klawisza otwiera od razu jego przypisanie. Starsze, sprzed wejścia na ekran, się nie liczy.
  useEffect(() => {
    if (keyPress !== null && keyPress !== seenKeyPress.current) {
      seenKeyPress.current = keyPress;
      setOpenKey((current) => current ?? { deviceId: keyPress.deviceId, addr: keyPress.addr });
    }
  }, [keyPress]);

  const selected = openKey && panels.find((panel) => panel.id === openKey.deviceId);
  const selectedKey = selected?.keys.find((key) => key.addr === openKey?.addr);

  return (
    <main className="products">
      {status === 'loading' && <p className="center-note">Wczytywanie panelu…</p>}
      {status === 'failed' && (
        <div className="center-note">
          <p>Stan panelu wymaga połączenia z serwerem.</p>
          <button className="button" onClick={() => void refresh()}>
            Spróbuj ponownie
          </button>
        </div>
      )}
      {status === 'ready' && panels.length === 0 && (
        <div className="center-note">
          <p>Nie ma jeszcze żadnego panelu.</p>
        </div>
      )}
      {status === 'ready' &&
        panels.map((panel) => (
          <div key={panel.id}>
            <PanelCard
              panel={panel}
              now={now}
              onOpen={(addr) => setOpenKey({ deviceId: panel.id, addr })}
            />
            <ConfigEditor deviceId={panel.id} config={panel.config} onNotice={onNotice} onSignedOut={onSignedOut} />
          </div>
        ))}

      {openKey && selectedKey && (
        <KeySheet
          key={`${openKey.deviceId}-${openKey.addr}`}
          deviceId={openKey.deviceId}
          panelKey={selectedKey}
          products={products}
          onChanged={refresh}
          onUnauthorized={onSignedOut}
          onClose={() => setOpenKey(null)}
        />
      )}
    </main>
  );
}
