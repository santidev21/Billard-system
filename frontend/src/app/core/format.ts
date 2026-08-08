export function fmtMoney(value: number | null | undefined): string {
  const n = Math.round(Number(value ?? 0));
  return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}
