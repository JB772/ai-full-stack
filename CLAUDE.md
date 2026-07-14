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
| `spec/sample1.spec.md`, `sample2.spec.md` | Worked examples of a feature spec (Course, SkillTrain). Aspirational — see the RowAudit gotcha. |
| `spec/feature-spec.template.md` | Template for new feature specs. |
| `spec/{sub-system}/{Table}.md` | Generated feature specs (e.g. `spec/admin/PublishStatus.md`). |
| `spec/ui-sample-*.png` | UI style reference only — not actual content. |
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

- **`AppRole.RoleId` is immutable.** The table has a `pkid` IDENTITY column *and* a clustered PK on
  `RoleId`, which `AppUserRole` foreign-keys to. `pkid` is the API identifier; `RoleId` is never
  written by the UPDATE statement and the edit form disables the field. Expect this shape on other
  auth tables (`AppUser.UserId`).
- **Not every PK is an IDENTITY.** `PublishStatus.pkid` is a `tinyint` with no IDENTITY, so the *user*
  supplies the key. Consequences, all of which `PublishStatusRepository` / `PublishStatusesController`
  already handle — copy them for any other table shaped this way:
  - INSERT writes `pkid` explicitly; there is no `SELECT CAST(SCOPE_IDENTITY() AS int)`, and
    `CreateAsync` echoes the caller's pkid back.
  - Create must check for a duplicate pkid and return **409** — otherwise SQL Server throws a raw PK
    violation.
  - **No `pkid <= 0` → 400 guard on PUT** (the one `AppRole` uses). `0` is a legal `tinyint` key and a
    C# `byte` defaults to `0`, so "absent" and "zero" are indistinguishable. An unknown pkid is a 404.
  - Route is `{id:int}` — ASP.NET has no `:byte` constraint — and the controller range-checks 0–255
    before casting, so `/api/publish-statuses/999` is a clean 404 rather than a 500.
  - The form makes `pkid` editable on add and `disable()`s it on edit (same as `AppRole.RoleId`).
- **Deletes that would orphan children return 409**, not a FK exception — check the child count first.
  Prefer a correlated-subquery count on the response model (`PublishStatus.CourseCount`) and read the
  guard off the entity the controller already loaded for its 404 — no second round trip.
- **`nchar(n)` columns need `RTRIM()`** in every SELECT (see the convention).
- **RowAudit is not wired up.** The `RowAudit` table exists in `admin.sql`, and the sample specs and
  the `/crud` skill both reference a `RowAuditWriter` / `RowAuditBadgeComponent` / `AuditHelper` —
  **none of these exist in the code.** No feature logs audit rows. Don't invent the subsystem while
  scaffolding a table; adding it is a separate cross-cutting task.
- **The `/crud` skill's file layout is wrong for this repo.** It says `core/models/` and
  `core/services/`; there is no `core/` directory. Models and services live in
  `features/{table-plural}/`, next to the components. Follow the existing code, not the skill text.
- **A few tables are scripted into more than one `.sql` file.** `PublishStatus` appears identically in
  `admin.sql`, `course.sql`, and `promotion.sql`. Same table, one deployed copy — diff them before
  assuming which file is authoritative.
- **New repositories must be swapped in `CmsApiFactory`.** It removes each `I{Table}Repository` and
  registers an in-memory fake. Miss one and the test host resolves the real Dapper repository, and
  those tests will try to reach SQL Server.
- **PrimeNG major version tracks Angular's.** Angular 20 → PrimeNG 20. Installing `primeng@latest`
  pulls v21 and fails peer resolution.
- **UTF-8 through the shell.** Chinese characters in a JSON body passed inline to `curl` via Bash get
  mangled and the API rejects the request. Write the payload to a file and use `--data-binary @file`,
  or use Swagger UI.
- **The database is real.** Test writes land in the live CMS database. Use a throwaway row and delete
  it, or exercise the endpoints through `CMS.API.Tests`, which swaps in an in-memory repository via
  `WebApplicationFactory` and needs no SQL Server.

## Adding a feature

Built so far: **AppRole** (`/api/app-roles`) and **PublishStatus** (`/api/publish-statuses`). Use
those as the reference implementation — the sample specs describe a richer system than what exists.

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
