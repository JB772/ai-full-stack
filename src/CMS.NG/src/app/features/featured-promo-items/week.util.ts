/**
 * Date helpers for the weekly scheduler. ScheduleOn is a SQL `date` → the API sends/accepts `yyyy-MM-dd`.
 * Everything here works on **local** date components. Never `toISOString()` — that shifts to UTC first and
 * lands the wrong day for UTC+8 users.
 */

/** `Date` → `yyyy-MM-dd` (local). */
export function toIsoDate(value: Date): string {
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

/** The Monday (local midnight) of the week containing `date`. Weeks run Monday → Sunday. */
export function mondayOf(date: Date): Date {
  const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const dow = d.getDay(); // 0 = Sunday, 1 = Monday, … 6 = Saturday
  const offset = dow === 0 ? -6 : 1 - dow; // Sunday counts as the end of the previous week
  d.setDate(d.getDate() + offset);
  return d;
}

/** Add `days` to a date, returning a new local `Date` (does not mutate the input). */
export function addDays(date: Date, days: number): Date {
  const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  d.setDate(d.getDate() + days);
  return d;
}

/** The seven local Dates Monday → Sunday for the week starting at `monday`. */
export function weekDays(monday: Date): Date[] {
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

const WEEKDAY_LABELS = ['日', '一', '二', '三', '四', '五', '六'];

/** `3/16 (一)` — month/day plus the Chinese weekday, matching the spec's day headers. */
export function dayLabel(date: Date): string {
  return `${date.getMonth() + 1}/${date.getDate()} (${WEEKDAY_LABELS[date.getDay()]})`;
}

/** `3/16 -- 3/22` — the week range shown between the prev/next arrows. */
export function weekRangeLabel(monday: Date): string {
  const sunday = addDays(monday, 6);
  return `${monday.getMonth() + 1}/${monday.getDate()} -- ${sunday.getMonth() + 1}/${sunday.getDate()}`;
}
