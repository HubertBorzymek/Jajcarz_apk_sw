import type { PanelView, Product, Rgb } from '../api/types';
import { describeTime } from '../products/format';

export function rgbToHex(color: Rgb): string {
  return `#${color.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`;
}

export function hexToRgb(hex: string): Rgb {
  return [1, 3, 5].map((start) => Number.parseInt(hex.slice(start, start + 2), 16)) as Rgb;
}

/** Jasność na panelu to 0..255, a ludzie myślą w procentach. */
export function brightnessPercent(brightness: number): number {
  return Math.round((brightness / 255) * 100);
}

export function describeConnection(panel: PanelView, now: number): string {
  if (panel.connected) {
    return 'Połączony';
  }
  return panel.lastSeenAt === null
    ? 'Jeszcze się nie połączył'
    : `Rozłączony, ostatnio ${describeTime(panel.lastSeenAt, now)}`;
}

/** Do klawisza można przypiąć każdy produkt. Wolne są pierwsze, bo przenoszenie to rzadszy przypadek. */
export function sortForBinding(products: Product[], collator = new Intl.Collator('pl')): Product[] {
  return [...products].sort((a, b) => {
    if ((a.addr === null) !== (b.addr === null)) {
      return a.addr === null ? -1 : 1;
    }
    return collator.compare(a.name, b.name);
  });
}
