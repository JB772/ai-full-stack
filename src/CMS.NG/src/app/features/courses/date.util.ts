/**
 * Course dates are SQL `date` columns → the API sends/accepts `yyyy-MM-dd`. These helpers convert to
 * and from the `Date` objects PrimeNG's p-datepicker binds to, using **local** components only.
 * Never `toISOString()` — that shifts to UTC first and lands the wrong day for UTC+8 users.
 */

/** `Date` → `yyyy-MM-dd` (local), or null. */
export function toIsoDate(value: Date | null | undefined): string | null {
  if (!(value instanceof Date) || isNaN(value.getTime())) {
    return null;
  }

  const year = value.getFullYear();
  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** `yyyy-MM-dd` (or full ISO) → local `Date`, or null. */
export function fromIsoDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const [datePart] = value.split('T');
  const [year, month, day] = datePart.split('-').map(Number);
  if (!year || !month || !day) {
    return null;
  }

  return new Date(year, month - 1, day);
}
