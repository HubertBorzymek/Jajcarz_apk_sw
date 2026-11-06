import { useEffect, useState } from 'react';
import { ApiError, savePanelConfig } from '../api/client';
import type { PanelConfig, ProductState } from '../api/types';
import { STATE_LABELS, STATES } from '../products/format';
import { brightnessPercent, hexToRgb, rgbToHex } from './panelFormat';

const DEFAULT_CONFIG: PanelConfig = {
  brightness: 51,
  colors: { OK: [0, 255, 0], LOW: [255, 120, 0], OUT: [255, 0, 0] },
};

/** Suwak i wybór koloru zgłaszają zmianę przy każdym ruchu, a panel ma kolejkę na 8 wiadomości. */
const SAVE_DELAY_MS = 400;

interface ConfigEditorProps {
  deviceId: string;
  config: PanelConfig;
  onNotice: (message: string) => void;
  onSignedOut: () => void;
}

export function ConfigEditor({ deviceId, config, onNotice, onSignedOut }: ConfigEditorProps) {
  const [draft, setDraft] = useState(config);

  useEffect(() => setDraft(config), [config]);

  useEffect(() => {
    if (draft === config) {
      return undefined;
    }
    const timer = window.setTimeout(() => {
      savePanelConfig(deviceId, draft).catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 401) {
          onSignedOut();
        } else {
          setDraft(config);
          onNotice('Nie zapisano wyglądu panelu.');
        }
      });
    }, SAVE_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [config, deviceId, draft, onNotice, onSignedOut]);

  const setColor = (state: ProductState, hex: string) =>
    setDraft({ ...draft, colors: { ...draft.colors, [state]: hexToRgb(hex) } });

  return (
    <section className="group">
      <h2 className="group-title">Wygląd panelu</h2>
      <div className="panel-card form">
        <label className="field">
          <span>Jasność: {brightnessPercent(draft.brightness)}%</span>
          <input
            type="range"
            min={0}
            max={255}
            value={draft.brightness}
            onChange={(event) => setDraft({ ...draft, brightness: Number(event.target.value) })}
          />
        </label>
        <div className="color-row">
          {STATES.map((state) => (
            <label key={state} className="color-field">
              <span>{STATE_LABELS[state]}</span>
              <input
                type="color"
                value={rgbToHex(draft.colors[state])}
                aria-label={`Kolor: ${STATE_LABELS[state]}`}
                onChange={(event) => setColor(state, event.target.value)}
              />
            </label>
          ))}
        </div>
        <button type="button" className="button" onClick={() => setDraft(DEFAULT_CONFIG)}>
          Przywróć domyślne
        </button>
      </div>
    </section>
  );
}
