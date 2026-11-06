import { describe, expect, it } from 'vitest';
import type { PanelView, Product } from '../api/types';
import { brightnessPercent, describeConnection, hexToRgb, rgbToHex, sortForBinding } from './panelFormat';

const panel: PanelView = {
  id: 'aabbccddeeff',
  firmware: 'v0.3.0',
  connected: false,
  lastSeenAt: null,
  config: { brightness: 51, colors: { OK: [0, 255, 0], LOW: [255, 120, 0], OUT: [255, 0, 0] } },
  keys: [],
};

function product(id: string, name: string, addr: string | null): Product {
  return {
    id,
    name,
    addr,
    category: null,
    state: 'OK',
    rev: 1,
    changedBy: 'web:hubert',
    changedAt: '2026-11-05T12:00:00Z',
  };
}

describe('kolory', () => {
  it('zamienia kolor w obie strony', () => {
    expect(rgbToHex([255, 120, 0])).toBe('#ff7800');
    expect(hexToRgb('#ff7800')).toEqual([255, 120, 0]);
  });

  it('dopełnia małe wartości zerem', () => {
    expect(rgbToHex([1, 2, 3])).toBe('#010203');
  });
});

describe('brightnessPercent', () => {
  it('pokazuje procenty', () => {
    expect(brightnessPercent(51)).toBe(20);
    expect(brightnessPercent(255)).toBe(100);
  });
});

describe('describeConnection', () => {
  const now = new Date('2026-11-05T12:00:00Z').getTime();

  it('opisuje połączony panel', () => {
    expect(describeConnection({ ...panel, connected: true }, now)).toBe('Połączony');
  });

  it('opisuje panel, który nigdy się nie połączył', () => {
    expect(describeConnection(panel, now)).toBe('Jeszcze się nie połączył');
  });

  it('podaje ostatni kontakt', () => {
    const seen = { ...panel, lastSeenAt: '2026-11-05T11:00:00Z' };

    expect(describeConnection(seen, now)).toBe('Rozłączony, ostatnio 1 godzinę temu');
  });
});

describe('sortForBinding', () => {
  it('pokazuje najpierw produkty bez klawisza', () => {
    const sorted = sortForBinding([product('a', 'Masło', '0.0'), product('b', 'Żurek', null), product('c', 'Cukier', null)]);

    expect(sorted.map((item) => item.name)).toEqual(['Cukier', 'Żurek', 'Masło']);
  });
});
