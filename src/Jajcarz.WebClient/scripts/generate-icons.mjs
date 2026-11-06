// Rysuje ikony aplikacji do public/icons. Bez zależności: czyste PNG z node:zlib.
// Uruchomienie: node scripts/generate-icons.mjs
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { crc32, deflateSync } from 'node:zlib';

const BACKGROUND = [0xf7, 0xf4, 0xee];
const EGG = [0xb9, 0x7a, 0x00];
const DOTS = [
  [0x2e, 0x7d, 0x4f],
  [0xf7, 0xf4, 0xee],
  [0xc0, 0x39, 0x2b],
];
const SAMPLES = 4;

const output = join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'icons');

/** Kolor punktu w układzie, w którym ikona ma boki od -1 do 1. */
function colorAt(x, y, scale) {
  const px = x / scale;
  const py = y / scale;
  for (let index = 0; index < DOTS.length; index += 1) {
    const dotY = 0.05 + index * 0.27;
    if (Math.hypot(px, py - dotY + 0.15) < 0.1) {
      return DOTS[index];
    }
  }
  // Jajko: elipsa węższa u góry.
  const width = 0.62 * (1 - 0.22 * (-py / 0.82));
  const inEgg = (px / width) ** 2 + (py / 0.82) ** 2 <= 1;
  return inEgg ? EGG : BACKGROUND;
}

function pixel(column, row, size, scale) {
  const sum = [0, 0, 0];
  for (let sy = 0; sy < SAMPLES; sy += 1) {
    for (let sx = 0; sx < SAMPLES; sx += 1) {
      const x = ((column + (sx + 0.5) / SAMPLES) / size) * 2 - 1;
      const y = ((row + (sy + 0.5) / SAMPLES) / size) * 2 - 1;
      colorAt(x, y, scale).forEach((value, channel) => {
        sum[channel] += value;
      });
    }
  }
  return sum.map((value) => Math.round(value / SAMPLES ** 2));
}

function chunk(type, data) {
  const typed = Buffer.concat([Buffer.from(type, 'latin1'), data]);
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const checksum = Buffer.alloc(4);
  checksum.writeUInt32BE(crc32(typed));
  return Buffer.concat([length, typed, checksum]);
}

function png(size, scale) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header[8] = 8; // bitów na kanał
  header[9] = 2; // RGB

  const rows = Buffer.alloc((size * 3 + 1) * size);
  let offset = 0;
  for (let row = 0; row < size; row += 1) {
    rows[offset] = 0;
    offset += 1;
    for (let column = 0; column < size; column += 1) {
      for (const value of pixel(column, row, size, scale)) {
        rows[offset] = value;
        offset += 1;
      }
    }
  }

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(rows)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

mkdirSync(output, { recursive: true });
writeFileSync(join(output, 'icon-192.png'), png(192, 1));
writeFileSync(join(output, 'icon-512.png'), png(512, 1));
// Android przycina ikonę maskowalną, więc rysunek musi zmieścić się w środkowych 80%.
writeFileSync(join(output, 'icon-maskable-512.png'), png(512, 0.75));
writeFileSync(join(output, 'apple-touch-icon.png'), png(180, 0.9));
