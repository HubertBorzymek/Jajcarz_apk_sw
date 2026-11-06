import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, listPanels } from '../api/client';
import type { PanelView } from '../api/types';

export type PanelsStatus = 'loading' | 'ready' | 'failed';

interface Panels {
  panels: PanelView[];
  status: PanelsStatus;
  refresh: () => Promise<void>;
}

/** Stan paneli z serwera. `signal` rośnie przy każdej zmianie zgłoszonej przez strumień zdarzeń. */
export function usePanels(signal: number, onSignedOut: () => void): Panels {
  const [panels, setPanels] = useState<PanelView[]>([]);
  const [status, setStatus] = useState<PanelsStatus>('loading');
  const latest = useRef(0);

  const refresh = useCallback(async () => {
    // Odpowiedzi mogą wrócić w innej kolejności, a liczy się tylko najnowsza.
    latest.current += 1;
    const request = latest.current;
    try {
      const list = await listPanels();
      if (request === latest.current) {
        setPanels(list);
        setStatus('ready');
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        onSignedOut();
      } else if (request === latest.current) {
        setStatus('failed');
      }
    }
  }, [onSignedOut]);

  useEffect(() => {
    void refresh();
  }, [refresh, signal]);

  return { panels, status, refresh };
}
