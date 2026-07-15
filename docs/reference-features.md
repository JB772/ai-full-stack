# Reference features — what each one demonstrates

The built features double as reference implementations. The table in `CLAUDE.md` (**Adding a feature**)
maps PK shape → which to copy; this file records the *secondary* patterns each one is the reference for.
Read the row for the pattern you're about to build.

## Partner (`smallint` IDENTITY)

- The common **"DB generates the key"** case.
- A **multi-child delete guard**: several child tables → one readable 409 that names each offender
  (only the children that actually have rows). See `docs/delete-guards.md`.
- A table with **no outbound FKs** (no nav objects).
- `AppKey` *reads* like a natural key but has **no UNIQUE index** — freely editable, no duplicate 409.

## CourseGroup (`smallint` IDENTITY)

- A **cascading** FK — the case where the delete guard is the *only* thing preventing data loss
  (SQL Server would otherwise `ON DELETE CASCADE` the children away silently). See `docs/delete-guards.md`.
- A **single-column** table: one form field, keyword-only filter. Don't pad it.

## Course (`int` IDENTITY, plain)

- **Outbound FKs** via Dapper multi-map nav objects; column order + `splitOn` matter.
- A **nullable FK** → `LEFT JOIN` yields a null nav object (not an empty one).
- FK dropdowns fed by `forkJoin` of existing feature services.
- `date` ⇄ `p-datepicker` conversion using **local** components — never `toISOString()`.
- Detail in `docs/relationships-and-nav.md`.

## AppUser (`int` IDENTITY + natural key, same shape as AppRole)

- A **server-managed, write-only column**: `PasswordHash` never appears in any DTO/model, is seeded
  from `SysConfig` on create, and changes only through a dedicated **bodyless reset endpoint**.
- Detail in `docs/pk-shapes.md`.

## FeaturedPromoItem (`int` IDENTITY, plain) — the first custom (non-CRUD-triad) UI

Patterns the standard list/detail/form triad doesn't cover:

- **Not the triad.** One `features/featured-promo-items/featured-promo-item-list/` scheduler component —
  TrainingCenter tabs × a Mon–Sun week × 3 slots/day — with the Edit/New form rendered *inline in the
  grid cell*, plus Copy/Paste and slot move. A **single** route (`/featured-promo-items`); no `/new`,
  `/:id`, or `/:id/edit`. List-state persistence keys are `featured-promo-item-center` / `-week`
  (not the `-filters` / `-sort` / `-page` triad). Week math lives in `week.util.ts`.
- **Multi-column UNIQUE → 409.** `(ScheduleOn, TrainingCenter_pkid, Slot)` is UNIQUE, so create/update
  call a `SlotTakenAsync` guard and return a friendly 409 instead of surfacing a raw SQL error.
- **Positional swap under that UNIQUE index.** `POST {id}/move` (`up`/`down`) swaps two rows' `Slot`
  values inside a transaction, parking one at temp `Slot 0` first so the UNIQUE index is never
  momentarily violated — verified against real SQL Server. See `FeaturedPromoItemRepository.MoveAsync`.
- **Lookup-only FK targets.** `TrainingCenter` and `Promotion2` have **no full CRUD feature** — they are
  exposed only via `/api/lookups/training-centers` and `/api/lookups/promo-codes` (thin `…Lookup` models
  + `I{Table}Repository.GetAllAsync`). The edit form resolves a typed `PromoCode` → `Promotion_pkid` from
  that list client-side. Add a lookup endpoint like this when you need an FK target's data but not a whole
  feature for it. Both are still swapped into `CmsApiFactory` so their tests avoid real SQL Server.
