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
| `docs/*.md` | Deep-dive gotcha references (PK shapes, delete guards, nav objects, schema/test traps). Read the relevant one before working in that area — see the pointer table under **Gotchas**. |
| `src/CMS.API` | .NET 9 Web API, Dapper, Swagger. Port 5000. |
| `src/CMS.API.Tests` | xUnit. |
| `src/CMS.NG` | Angular 20 standalone + PrimeNG 20. Port 4200. |

## Commands

Node is installed but **not on PATH** — prepend it in every shell:
`$env:Path = "C:\Program Files\nodejs;$env:Path"`

```powershell
dotnet run --project src\CMS.API      # API + Swagger UI at http://localhost:5000/swagger
dotnet test                           # xUnit
cd src\CMS.NG; npm start              # http://localhost:4200
cd src\CMS.NG; npm test               # Karma + Jasmine (needs CHROME_BIN for headless)
```

`ng test` headless: `$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"`
then `npx ng test --watch=false --browsers=ChromeHeadless`.

**Stop the API before `dotnet test`.** A running `dotnet run` holds a lock on
`src\CMS.API\bin\Debug\net9.0\CMS.API.dll`, and the test project rebuilds the API into that same
folder — so the build dies with `MSB3021 ... being used by another process` and the failure looks
nothing like a compile error. `Get-Process CMS.API | Stop-Process -Force` first. To type-check the
API *without* stopping it, build to a throwaway folder: `dotnet build src\CMS.API --output <tmp>`.

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
- **UTF-8 through the shell.** Inline Chinese JSON to `curl` gets mangled — use `--data-binary @file` or Swagger UI.

### Deep reference — read the relevant file before working in that area

| Doc | Read it before… |
|-----|-----------------|
| `docs/pk-shapes.md` | adding a table — the four PK shapes (tinyint / smallint / plain-int / int+natural-key) and `AppUser`'s backend-only `PasswordHash` (SysConfig-seeded, reset-only) |
| `docs/delete-guards.md` | writing any DELETE — 409-not-FK guards, multi-child messages, no-FK orphans, and load-bearing `ON DELETE CASCADE` checks |
| `docs/relationships-and-nav.md` | touching FKs or N-N — `Course` multi-map nav objects, nullable-FK `LEFT JOIN`, `forkJoin` lookups, `date` ⇄ `p-datepicker`, why N-N editors are deferred |
| `docs/schema-and-testing.md` | reading the schema or writing tests — invented-constraint traps, multi-file `.sql`, `p-table` in-place sort, don't-assert-Chinese-sort-order |

## Adding a feature

Built so far: **AppRole** (`/api/app-roles`), **AppUser** (`/api/app-users`),
**PublishStatus** (`/api/publish-statuses`), **Partner** (`/api/partners`),
**CourseGroup** (`/api/course-groups`) and **Course** (`/api/courses`).
Use those as the reference implementation — the sample specs describe a richer system than what exists.
Pick the one whose PK matches the table you're adding:

| Reference | PK shape | Copy it when… |
|-----------|----------|---------------|
| `AppRole` | `int` IDENTITY + a separate immutable natural key | the table has a business key other tables FK to |
| `PublishStatus` | `tinyint`, **no** IDENTITY | the *user* supplies the key |
| `Partner` | `smallint` IDENTITY | the DB generates the key (the common case) |
| `Course` | `int` IDENTITY (plain) | the table has **outbound FKs** (nav objects via multi-map) |

`Partner` is also the reference for a multi-child delete guard and for a table with no outbound FKs.
`CourseGroup` is the reference for a **cascading** FK — the case where the delete guard is the only thing
preventing data loss — and for a single-column table (one form field, keyword-only filter; don't pad it).
`Course` is the reference for **outbound FKs**: Dapper multi-map nav objects, a nullable-FK `LEFT JOIN`,
FK dropdowns fed by `forkJoin` of existing feature services, and `date` ⇄ `p-datepicker` conversion.
`AppUser` (same PK shape as `AppRole`) is the reference for a **server-managed, write-only column** — a
secret (`PasswordHash`) that never appears in any DTO/model, is seeded from `SysConfig` on create, and
is changed only through a dedicated bodyless reset endpoint (details in `docs/pk-shapes.md`).

Per-shape and per-pattern depth for all of the above lives in `docs/*.md` — see the pointer table under
**Gotchas**.

1. Read the table in `database/*.sql` and write a spec from `spec/feature-spec.template.md`,
   saved to `spec/{sub-system}/{Table}.md`. Check whether `pkid` is really an IDENTITY.
2. Backend: model trio → repository → controller → register in `Program.cs` (+ a
   `/api/lookups/{plural}` endpoint if the table is an FK target).
3. Frontend: model → service → list / detail / form → route (`/new` before `/:id`) → sidebar entry
   in `app.ts`. `app.html` renders nav groups generically and needs no edit.
4. Tests both sides: xUnit for the endpoints (list/filter, view, add, edit, delete-guard), plus an
   in-memory fake swapped into `CmsApiFactory`; Karma for the components and the data service.

The `/crud` skill automates steps 1–4, but read the gotchas above — it assumes a `core/` layout and a
RowAudit subsystem that this repo does not have.
