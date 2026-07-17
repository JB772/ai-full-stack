# Reference features — what each one demonstrates

The built features double as reference implementations. The table in `docs/architecture.md`
(**Adding a feature**) maps PK shape → which to copy; this file records the *secondary* patterns each
one is the reference for. Read the row for the pattern you're about to build.

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

### Sticky page toolbar (`course-form`, New + Edit)

The viewport is the scroll container — **there is no fixed app header.** `.layout` is a flex row (sidebar
+ `.content`) with `min-height: 100vh`; the document itself scrolls. So a `.page-toolbar` can be pinned
with plain `position: sticky; top: 0` (+ a `z-index` above the fields) — no scroll listener, no fixed
positioning. It sticks to the viewport top within the content region without covering the sidebar (a
separate flex column) or a header (there is none), and the toolbar's opaque `--p-content-background` keeps
scrolling fields from showing through. `course-form` does this via a `sticky-toolbar` class on both New and
Edit (one component serves both). Assert it in a headless Karma run with
`getComputedStyle(el).position === 'sticky'`.

### PDF export (`course-detail`, 存成 PDF button)

One-click archive download via `pdfmake` + vendored Noto Sans TC — pure docDefinition builder
(`course-pdf.def.ts`), lazy-loading service (`course-pdf.service.ts`), completeness spec over the
model's fields. Full pattern: `docs/ui-patterns.md`.

### QR code (`course-detail`, 基本資料 block)

QR codes use the framework-agnostic **`qrcode`** package, **not `angularx-qrcode`** — it has no Angular
peer dep and returns a PNG data URL, which doubles as the `<img [src]>` and the download payload (build an
anchor with `href=dataUrl`, `download={name}.png`, `.click()`). In tests, `qrcode.toDataURL` is overloaded
— `spyOn(QRCode, 'toDataURL') as unknown as jasmine.Spy` to stub it.

### In-place (inline) cell editing on the list page

The `course-list` table edits cells in place — the reference for adding inline editing to any list.

- **Double-click to edit, not `pEditableColumn`.** Editors are rendered by a single `editing` signal
  (`{ pkid, field, value, error }`); each editable `<td>` binds `(dblclick)="startEdit(course, field)"`.
  PrimeNG's `pEditableColumn` opens on single click, so it's deliberately **not** used — single click must
  do nothing. `[(ngModel)]="editValue"` (a get/set bridge over the signal) keeps the active editor reactive.
- **Editor matches the column type**: `pInputText` (text), `p-inputnumber` (numeric), `p-datepicker`
  (dates), `p-select` (上架狀態), `p-checkbox` (允許重聽). Read-only columns — `pkid`, `partner`,
  `courseGroup` — carry a `data-field` but **no** `(dblclick)`, so they can't open an editor.
- **Blur persists for the plain inputs; the overlay editors must NOT commit on blur.** Text/number
  commit on `(blur)`/`(onBlur)`. Both the **上架狀態 `p-select`** and the **`p-datepicker`s** are
  `appendTo="body"`, so a blur-to-commit fires the instant you click an option / a date — tearing the
  editor down before the pick lands, which reads as "the dropdown/calendar won't edit". Instead:
  - `p-select` commits on `(onChange)` and closes via `(onHide)` (`onSelectHide`) when the panel hides
    with no change.
  - `p-datepicker` commits on `(onSelect)` (calendar pick) and `(onClose)` (panel closes — covers
    click-away and typed-then-close). Binding both is safe: `commit()` guards on `savingEdit()` + the
    active-cell check, so the second event is a no-op.
  - The checkbox commits on `(onChange)` since a toggle is the discrete action.
- **Validate before the call, save the whole row.** `validate()` mirrors the form's rules (required text,
  non-negative numbers, valid dates, 上架日期 ≤ 下架日期). On failure the cell **stays in edit mode** with an
  inline error and no request goes out. On success `buildRequest()` sends a full `CourseRequest` rebuilt
  from the row with only the edited field overridden (the row is a superset of the DTO), and a dropdown
  edit also refreshes the `publishStatus` nav label.
- **The row is mutated only after the server accepts.** So a failed save needs no explicit revert —
  closing the editor restores the untouched cell — and it surfaces the API message as a toast.

## AppUser (`int` IDENTITY + natural key, same shape as AppRole)

- A **server-managed, write-only column**: `PasswordHash` never appears in any DTO/model, is seeded
  from `SysConfig` on create, and changes only through the auth endpoints (self-service change or the
  Admin reset-to-default) — never via the AppUser CRUD DTOs.
- The **N-N membership editor** (`AppUserRole`): Admin-only, per-row `GET`/`POST`/`DELETE
  `/api/app-users/{id}/roles` (audited, `403` for non-Admins) with a role picker on the AppUser **edit**
  form, edit-mode only. It is the reference for assigning/removing junction-table membership — see
  `docs/relationships-and-nav.md`.
- Detail in `docs/pk-shapes.md`; the reset/change flows are in `docs/auth.md`.

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
