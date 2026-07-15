# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth —
it already exists and is populated. Never recreate or migrate it; read `database/*.sql` to learn
a table's shape before writing code against it.

| Path | What |
|------|------|
| `database/*.sql` | The schema (auth, admin, course, promotion). Reference only — already deployed. |
| `spec/code-gen.convention.md` | **The** code-generation convention. Read it before adding any feature. |
| `spec/sample1.spec.md`, `sample2.spec.md` | Worked examples of a feature spec (Course, SkillTrain). **Aspirational** — see the RowAudit gotcha. `sample1` is a much richer Course than what shipped; the real, built spec is `spec/course/Course.md`. Don't conflate them. |
| `spec/feature-spec.template.md` | Template for new feature specs. |
| `spec/{sub-system}/{Table}.md` | Generated feature specs (e.g. `spec/admin/PublishStatus.md`, `spec/course/Course.md`). |
| `spec/ui-sample-*.png` | UI style reference only — not actual content. |
| `docs/*.md` | Deep-dive references (PK shapes, delete guards, nav objects, schema/test traps, per-feature reference patterns, env traps). Read the relevant one before working in that area — see the pointer table under **Gotchas**. |
| `src/CMS.API` | .NET 9 Web API, Dapper, Swagger. Port 5000. |
| `src/CMS.API.Tests` | xUnit. |
| `src/CMS.NG` | Angular 20 standalone + PrimeNG 20. Port 4200. |

## Commands

Node is **not on PATH** — prepend it: PowerShell `$env:Path = "C:\Program Files\nodejs;$env:Path"`
(Bash tool `export PATH="/c/Program Files/nodejs:$PATH"`).

```powershell
dotnet run --project src\CMS.API      # API + Swagger UI at http://localhost:5000/swagger
dotnet test                           # xUnit — stop the API first (see below)
cd src\CMS.NG; npm start              # http://localhost:4200
cd src\CMS.NG; npm test               # Karma + Jasmine (headless needs CHROME_BIN)
```

**Stop the API before `dotnet test`** — a running `dotnet run` locks the API dll the test build
overwrites, failing with a confusing `MSB3021` (not a compile error).
`Get-Process CMS.API | Stop-Process -Force`.

Shell/env traps (PowerShell `.ps1` execution-policy block on `npm`/`ng`/`npx`, the `dotnet test` file
lock, headless Chrome, UTF-8 curl) → **`docs/environment.md`**.

## Architecture

**Backend.** Per table: `Models/{Table}.cs` (response, with nav objects / subquery counts),
`{Table}Request.cs` (write DTO), `{Table}Query.cs` (search DTO); `Repositories/I{Table}Repository.cs`
+ Dapper implementation; `Controllers/{TablePlural}Controller.cs`. Routes are kebab-case
(`/api/app-roles`). `PUT` takes the pkid **from the body**, never a route param. Repositories take
`IDbConnectionFactory` and open a connection per call — Dapper only, no EF.
`DateOnly`/`TimeOnly` Dapper type handlers are registered in `Program.cs`.

**Frontend.** Per table: `features/{table-plural}/{table}-list|-detail|-form/` plus
`{table}.model.ts` and `{table}.service.ts`. Standalone components, lazy-loaded in `app.routes.ts`.
List pages use `p-table` (sortable, paginated) with a `p-drawer` filter, and persist state to
session storage under `{table}-list-filters` / `-sort` / `-page`. Forms use Reactive Forms.
Sidebar entries live in `app.ts` (`navGroups`) and render via `app.html`.

**API base URL** comes from `environment.ts` / `environment.development.ts` (aliases `@env/*`,
`@app/*`). There is **no dev-server proxy** — the frontend calls `http://localhost:5000/api`
directly, and the API's CORS policy allows any localhost origin.

## Gotchas

### Always-true rules (apply to almost any change)

- **The database is real.** Test writes land in the live CMS database. Exercise endpoints through
  `CMS.API.Tests` (it swaps in an in-memory repo via `WebApplicationFactory`, no SQL Server), or use a
  throwaway row you delete.
- **New repositories must be swapped in `CmsApiFactory`.** It removes each `I{Table}Repository` and
  registers an in-memory fake; miss one and those tests hit real SQL Server.
- **RowAudit is not wired up.** The table exists and the sample specs / `/crud` skill reference a
  `RowAuditWriter` / `RowAuditBadgeComponent` / `AuditHelper`, but **none of these exist in the code.**
  Don't invent the subsystem while scaffolding a table.
- **The `/crud` skill's file layout is wrong for this repo.** It says `core/models/` + `core/services/`;
  there is no `core/`. Models and services live in `features/{table-plural}/`. Follow the code, not the skill.
- **`nchar(n)` columns need `RTRIM()`** in every SELECT (see the convention).
- **Read the constraints before assuming.** A column that *reads* like a natural key may have no UNIQUE
  index (`Partner.AppKey` — freely editable, no 409), and a two-FK table may carry payload columns and so
  not be a junction (`PartnerCourseGroup`). The same table can appear in several `.sql` files — diff them,
  and grep *every* file when hunting for a table's children/FKs.
- **PrimeNG major version tracks Angular's** (20 → 20); `primeng@latest` pulls v21 and fails peer resolution.
- **QR codes use the framework-agnostic `qrcode` package, not `angularx-qrcode`** — it has no Angular
  peer dep and returns a PNG data URL, which doubles as the `<img [src]>` and the download payload
  (build an anchor with `href=dataUrl`, `download={name}.png`, `.click()`). See the QR block in
  `course-detail` (title + image + download button in 基本資料). In tests, `qrcode.toDataURL` is
  overloaded — `spyOn(QRCode, 'toDataURL') as unknown as jasmine.Spy` to stub it.
- **In-place list editing is hand-rolled, not `pEditableColumn`.** The `course-list` cell editor is a
  component-managed edit-state (`editing` signal) driven by `(dblclick)`, because PrimeNG's
  `pEditableColumn` opens on **single** click and the requirement was double-click only. Don't reach for
  `pEditableColumn`/`p-cellEditor` here. See the Course row in `docs/reference-features.md`.
- **Overlay editors (`p-select`, `p-datepicker`) must NOT commit on blur.** Their panels are
  `appendTo="body"`, so a blur-to-save fires the instant you click an option / a date — tearing the editor
  down before the pick lands, so the control looks like it "won't edit". The `p-select` commits on
  `(onChange)` (+ `(onHide)` to close on click-away); the `p-datepicker` on `(onSelect)` + `(onClose)`.
  Only plain text/number inputs are safe to commit on `(blur)`. See the Course row in
  `docs/reference-features.md`.

### Deep reference — read the relevant file before working in that area

| Doc | Read it before… |
|-----|-----------------|
| `docs/pk-shapes.md` | adding a table — the four PK shapes (tinyint / smallint / plain-int / int+natural-key) and `AppUser`'s backend-only `PasswordHash` (SysConfig-seeded, reset-only) |
| `docs/delete-guards.md` | writing any DELETE — 409-not-FK guards, multi-child messages, no-FK orphans, and load-bearing `ON DELETE CASCADE` checks |
| `docs/relationships-and-nav.md` | touching FKs or N-N — `Course` multi-map nav objects, nullable-FK `LEFT JOIN`, `forkJoin` lookups, `date` ⇄ `p-datepicker`, why N-N editors are deferred |
| `docs/reference-features.md` | adding a feature — the *secondary* pattern each built feature is the reference for (multi-child & cascading delete guards, multi-map nav, write-only column, custom scheduler UI, lookup-only FK targets) |
| `docs/schema-and-testing.md` | reading the schema or writing tests — invented-constraint traps, multi-file `.sql`, `p-table` in-place sort, don't-assert-Chinese-sort-order |
| `docs/environment.md` | a Windows/PowerShell dev trap — Node PATH, `.ps1` execution policy, the `dotnet test` file lock, headless Chrome, UTF-8 curl |

## Adding a feature

Built: **AppRole**, **AppUser**, **PublishStatus**, **Partner**, **CourseGroup**, **Course**,
**FeaturedPromoItem** (routes are the kebab-case plural, e.g. `/api/app-roles`). They are the reference
implementations — the sample specs describe a richer system than exists. Pick one by PK shape, then read
its row in `docs/reference-features.md` for the secondary patterns it demonstrates:

| Reference | PK shape | Copy it when… |
|-----------|----------|---------------|
| `AppRole` | `int` IDENTITY + a separate immutable natural key | the table has a business key other tables FK to |
| `PublishStatus` | `tinyint`, **no** IDENTITY | the *user* supplies the key |
| `Partner` | `smallint` IDENTITY | the DB generates the key (the common case) |
| `Course` | `int` IDENTITY (plain) | the table has **outbound FKs** (nav objects via multi-map), or you want **in-place cell editing** on a list page |
| `FeaturedPromoItem` | `int` IDENTITY (plain) | the UI is **not** the list/detail/form triad, or you need a multi-column UNIQUE 409 / a positional swap |

1. Read the table in `database/*.sql`; write a spec from `spec/feature-spec.template.md` to
   `spec/{sub-system}/{Table}.md`. Confirm whether `pkid` is really an IDENTITY.
2. Backend: model trio → repository → controller → register in `Program.cs` (+ a `/api/lookups/{plural}`
   endpoint if the table is an FK target — a lookup-only target needs no full feature).
3. Frontend: model → service → list / detail / form → route (`/new` before `/:id`) → sidebar entry in
   `app.ts` (`app.html` renders nav groups generically). A custom UI can collapse this to one component
   on a single route (see `FeaturedPromoItem` in `docs/reference-features.md`).
4. Tests both sides: xUnit endpoints (list/filter, view, add, edit, delete-guard) + an in-memory fake
   swapped into `CmsApiFactory`; Karma for the components and the service.

The `/crud` skill automates steps 1–4, but it assumes a `core/` layout and a RowAudit subsystem this
repo lacks — follow the code and the always-true rules above.
