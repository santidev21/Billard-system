import { fmtMoney } from './format';

describe('fmtMoney', () => {
  it('formatea números con separador de miles', () => {
    expect(fmtMoney(12000)).toBe('12,000');
  });

  it('redondea decimales', () => {
    expect(fmtMoney(10.6)).toBe('11');
  });

  it('maneja nulos y undefined', () => {
    expect(fmtMoney(null)).toBe('0');
    expect(fmtMoney(undefined)).toBe('0');
  });
});