import { describe, expect, it } from 'vitest';
import { describeActor, describeKey, describeTime } from './format';

describe('describeActor', () => {
  it.each([
    ['web:hubert', 'hubert'],
    ['device:6825dd21a970', 'panel'],
    ['server', 'system'],
  ])('%s to %s', (actor, expected) => {
    expect(describeActor(actor)).toBe(expected);
  });
});

describe('describeTime', () => {
  const now = new Date('2026-11-05T12:00:00Z').getTime();

  it.each([
    ['2026-11-05T11:59:30Z', 'przed chwilą'],
    ['2026-11-05T11:55:00Z', '5 minut temu'],
    ['2026-11-05T09:00:00Z', '3 godziny temu'],
    ['2026-11-04T12:00:00Z', 'wczoraj'],
  ])('%s to %s', (time, expected) => {
    expect(describeTime(time, now)).toBe(expected);
  });

  it('starsze zmiany pokazuje jako datę', () => {
    expect(describeTime('2026-10-01T12:00:00Z', now)).toContain('paź');
  });
});

describe('describeKey', () => {
  it('numeruje klawisze od jedynki', () => {
    expect(describeKey('0.0')).toBe('klawisz 1');
    expect(describeKey('0.2')).toBe('klawisz 3');
  });

  it('podaje moduł poza płytą główną', () => {
    expect(describeKey('2.1')).toBe('moduł 2, klawisz 2');
  });
});
