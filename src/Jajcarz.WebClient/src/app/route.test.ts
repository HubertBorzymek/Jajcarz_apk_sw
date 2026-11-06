import { describe, expect, it } from 'vitest';
import { parseRoute, ROUTE_HASHES } from './route';

describe('parseRoute', () => {
  it.each([
    ['', 'products'],
    ['#/', 'products'],
    ['#/nieznany', 'products'],
    [ROUTE_HASHES.shopping, 'shopping'],
    [ROUTE_HASHES.panel, 'panel'],
  ])('%s -> %s', (hash, route) => {
    expect(parseRoute(hash)).toBe(route);
  });
});
