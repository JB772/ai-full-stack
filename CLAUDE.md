# CLAUDE.md

Guidance for Claude Code when working in this repository.

> **How to read this file.** Everything above the **Deep reference** table is *always-needed* — the
> rules that apply to almost any change. The detail behind each rule lives in `docs/*.md`; the table
> tells you which file to open before working in an area. Read the doc, don't reconstruct it.

## What this is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth —
it already exists and is populated. Never recreate or migrate it; read `database/*.sql` to learn
a table's shape before writing code against it.

| Path | What |
|------|------|
| `database/*.sql` | The schema (auth, admin, course, promotion). Reference only — already deployed. |
| `spec/code-gen.convention.md` | **The** code-generation convention. Read it before adding any feature. |
| `spec/sample1.spec.md`, `sample2.spec.md` | Worked examples of a feature spec (Course, SkillTrain). **Aspirational** — `sample1` is richer than what shipped; the real, built spec is `spec/course/Course.md`. Don't conflate them. |
| `spec/feature-spec.template.md` | Template for new feature specs. |
| `spec/{sub-system}/{Table}.md` | Generated feature specs (e.g. `spec/admin/PublishStatus.md`, `spec/course/Course.md`). |
| `spec/ui-sample-*.png` | UI style reference only — not actual content. |
| `docs/*.md` | Deep-dive references. Read the relevant one before working in that area — see the **Deep reference** table. |
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
`Get-Process CMS.API | Stop-Process -Force`. Other Windows/PowerShell traps (`.ps1` execution
policy on `npm`/`ng`/`npx`, headless Chrome, UTF-8 curl) → **`docs/environment.md`**.

## Architecture

**Backend.** Per table: `Models/{Table}.cs` (response, with nav objects / subquery counts),
`{Table}Request.cs` (write DTO), `{Table}Query.cs` (search DTO); `Repositories/I{Table}Repository.cs`
+ Dapper implementation; `Controllers/{TablePlural}Controller.cs`. Routes are kebab-case
(`/api/app-roles`); `PUT` takes the pkid **from the body**, never a route param. Repositories take
`IDbConnectionFactory` (+ `RowAuditWriter` where there are writes), open a connection per call, Dapper
only — no EF. `DateOnly`/`TimeOnly` type handlers are registered in `Program.cs`. Write methods run
inside a transaction and audit on it; all endpoints require a JWT; unhandled errors become one generic
500 — see **Cross-Cutting Conventions**.

**Frontend.** Per table: `features/{table-plural}/{table}-list|-detail|-form/` plus
`{table}.model.ts` and `{table}.service.ts`. Standalone components, lazy-loaded in `app.routes.ts` as
children of a token-guarded pathless parent (`authGuard`) — **not** at the top level. List pages use
`p-table` (sortable, paginated) with a `p-drawer` filter, persisting state to session storage under
`{table}-list-filters` / `-sort` / `-page`. Forms use Reactive Forms. Sidebar entries live in `app.ts`
(`navGroups`) and render generically via `app.html`. Every request carries a bearer token via
`authInterceptor`, which is also the one central HTTP-error seam (see **Cross-Cutting Conventions**).
Auth plumbing lives in `features/auth/` (there is **no** `core/`).

**API base URL** comes from `environment.ts` / `environment.development.ts` (aliases `@env/*`,
`@app/*`). There is **no dev-server proxy** — the frontend calls `http://localhost:5000/api`
directly, and the API's CORS policy allows any localhost origin.

## Cross-Cutting Conventions

Every feature MUST follow these regardless of table shape. This is the checklist; mechanics and
testing seams are in `docs/row-audit.md` and `docs/exception-handling.md`.

**Row Audit — backend** (detail → `docs/row-audit.md`):

- Every repository's `Create` / `Update` / `Delete` MUST call the shared `RowAuditWriter`
  (`LogInsertAsync` / `LogUpdateAsync` / `LogDeleteAsync`). No write goes unaudited.
- Do it **on the same connection + transaction** as the change (open → `BeginTransaction()` → write →
  log → commit), so a failed/rolled-back change leaves no audit row.
- **Update** reloads the row before *and* after (accurate changed-column list); **Delete** reloads
  before (captures the first string column before it's gone).
- ActionDesc: Insert/Delete = row's **first string column value**; Update = **comma-separated changed
  column names**. `PrimaryKeyValues` = pkid as string. `UserName` = JWT `userName` claim, else
  `"system"`. Never insert `pkid` (IDENTITY).
- Exempt: lookup-only repos with no writes (`TrainingCenter`/`Promotion`) and positional swaps
  (`FeaturedPromoItem.MoveAsync`).

**Row Audit — frontend** (detail → `docs/row-audit.md`):

- Every **detail page and edit-form page** places `RowAuditBadgeComponent` (inputs `[tableName]` +
  `[pkid]`) in the toolbar `.page-actions` slot. It shows the latest change inline and opens the full
  trail (`GET /api/rowaudit?tableName=&pkid=`); it fetches only when `pkid > 0`, so create-mode renders
  nothing. Spec gotcha: the badge injects `RowAuditService`, so the host spec must stub
  `{ provide: RowAuditService, useValue: { getForRecord: () => of([]) } }`.

**Exception handling** (detail → `docs/exception-handling.md`):

- **Don't** add per-controller `try/catch` for unexpected errors — let them reach the global
  middleware, which logs full detail server-side and returns one safe 500 (no stack/SQL leak). Leave
  `401` / `403` / validation-`400` untouched.
- Frontend: the shared `authInterceptor` is the single error seam — 5xx → friendly toast, 401 →
  `/login`, everything else re-thrown for the form. Don't add a competing interceptor; specs wiring the
  real one must provide `MessageService`.

## Gotchas — always-true rules

Compressed to the rule + where the detail lives. Read the linked doc before working in that area.

- **The database is real.** Test writes land in the live DB. Exercise endpoints through `CMS.API.Tests`
  (swaps in an in-memory repo, no SQL Server) or a throwaway row you delete. Real-repo tests run against
  in-memory SQLite where the SQL is portable — worked example + traps in **`docs/schema-and-testing.md`**.
- **New repositories must be swapped in `CmsApiFactory`.** It removes each `I{Table}Repository` and
  registers an in-memory fake; miss one and those tests hit real SQL Server.
- **Auth is global.** `POST /api/auth/login` is the only `[AllowAnonymous]` action; every other endpoint
  needs a Bearer token. A bare `CreateClient()` 401s — use `CmsApiFactory.CreateAuthenticatedClient()` /
  `CreateToken(...)` (defaults to `Admin`; pass a role to test a restriction). Flows, JWT, `PasswordPolicy`,
  frontend plumbing, and the Admin-only reset → **`docs/auth.md`**.
- **Frontend auth** keeps the profile in **session** storage (`auth-profile`), guards every feature route,
  and hides the `系統管理 Admin` nav group unless roles include `Admin`. Seed a session by writing
  `auth-profile` before creating the component → **`docs/auth.md`**.
- **`nchar(n)` columns need `RTRIM()`** in every SELECT (see the convention).
- **Read the constraints before assuming.** A natural-looking key may have no UNIQUE index
  (`Partner.AppKey` — no 409); a two-FK table may carry payload and not be a junction. The same table
  spans several `.sql` files — diff them, grep every file. Delete guards → **`docs/delete-guards.md`**;
  FKs / nav objects → **`docs/relationships-and-nav.md`**; more traps → **`docs/schema-and-testing.md`**.
- **Load-bearing frontend UI patterns are in `docs/ui-patterns.md`** — sticky toolbar, QR download
  (`qrcode`, not `angularx-qrcode`), in-place cell editing on `(dblclick)` (not `pEditableColumn`), and the
  overlay-editor blur trap. Read it before reaching for any of them.
- **`/crud` skill layout is wrong for this repo** — it assumes `core/` + an `AuditHelper` that don't
  exist. Models/services live in `features/{table-plural}/`; follow the code, not the skill.
- **PrimeNG major version tracks Angular's** (20 → 20); `primeng@latest` pulls v21 and fails peer resolution.

### Deep reference — read the relevant file before working in that area

| Doc | Read it before… |
|-----|-----------------|
| `docs/auth.md` | login / JWT / authorization, self-service profile or password, Admin reset — per-endpoint flows, signing-key resolution, `PasswordPolicy`, the `CreateAuthenticatedClient` test seam, frontend auth plumbing |
| `docs/row-audit.md` | touching the row-audit writer or viewer — the reflection-based `RowAuditWriter`, its ActionDesc / UserName / pkid rules and pure-static test seam, the read path (`GET /api/rowaudit`), and the frontend `RowAuditBadgeComponent` |
| `docs/exception-handling.md` | touching error handling — the global 500 middleware (registration order, no-leak guarantee, test-only controller) and the `authInterceptor` 5xx/401 behavior + `MessageService` spec requirement |
| `docs/pk-shapes.md` | adding a table — the four PK shapes (tinyint / smallint / plain-int / int+natural-key) and `AppUser`'s backend-only `PasswordHash` (SysConfig-seeded, reset-only) |
| `docs/delete-guards.md` | writing any DELETE — 409-not-FK guards, multi-child messages, no-FK orphans, load-bearing `ON DELETE CASCADE` checks |
| `docs/relationships-and-nav.md` | touching FKs or N-N — `Course` multi-map nav objects, nullable-FK `LEFT JOIN`, `forkJoin` lookups, `date` ⇄ `p-datepicker`, and the `AppUserRole` membership editor (the reference for N-N assign/remove) |
| `docs/reference-features.md` | adding a feature — the *secondary* pattern each built feature demonstrates (multi-child & cascading delete guards, multi-map nav, write-only column, custom scheduler UI, lookup-only FK targets) |
| `docs/ui-patterns.md` | building a frontend page — sticky action toolbar, QR download, inline list-cell editing, overlay-editor (`p-select`/`p-datepicker`) blur trap |
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
| `AppUser` role editor | N-N junction (`AppUserRole`) | you need to **assign/remove membership** in a junction table |

**N-N membership editing** (add/remove a row in a junction table) is done by `AppUser`'s role editor:
Admin-only `GET`/`POST`/`DELETE /api/app-users/{id}/roles` (each write audited via `RowAuditWriter`;
`403` for non-Admins) plus a role picker on the AppUser **edit** form — **edit-mode only**, since membership
is keyed by the natural key and needs an existing row (create mode shows no picker, like the RowAudit badge).
Assign/remove are immediate API writes, independent of the form's Save. Copy it for any junction table;
detail → `docs/relationships-and-nav.md`.

1. Read the table in `database/*.sql`; write a spec from `spec/feature-spec.template.md` to
   `spec/{sub-system}/{Table}.md`. Confirm whether `pkid` is really an IDENTITY.
2. Backend: model trio → repository → controller → register in `Program.cs` (+ a `/api/lookups/{plural}`
   endpoint if the table is an FK target — a lookup-only target needs no full feature). Wire RowAudit and
   let errors reach the middleware (**Cross-Cutting Conventions**).
3. Frontend: model → service → list / detail / form → route (`/new` before `/:id`, under the token-guarded
   pathless parent in `app.routes.ts`) → sidebar entry in `app.ts`. Add the `RowAuditBadgeComponent` to the
   detail + form pages. A custom UI can collapse this to one component on a single route (see
   `FeaturedPromoItem`).
4. Tests both sides: xUnit endpoints (list/filter, view, add, edit, delete-guard) via
   `CmsApiFactory.CreateAuthenticatedClient()` + an in-memory fake swapped into `CmsApiFactory`; Karma for
   the components and the service (stub `RowAuditService` / `MessageService` per the conventions).

The `/crud` skill automates steps 1–4 but assumes a `core/` layout and an `AuditHelper` this repo lacks —
follow the code and the rules above.

## gstack

The [gstack](https://github.com/garrytan/gstack) skill suite is installed (`~/.claude/skills/gstack`).

- **Use the `/browse` skill for all web browsing.** It drives a fast headless Chromium for QA,
  dogfooding, and any page interaction.
- **Never use `mcp__claude-in-chrome__*` tools.** Route every browsing need through `/browse` instead.

Available skills: `/office-hours`, `/plan-ceo-review`, `/plan-eng-review`, `/plan-design-review`,
`/design-consultation`, `/design-shotgun`, `/design-html`, `/review`, `/ship`, `/land-and-deploy`,
`/canary`, `/benchmark`, `/browse`, `/connect-chrome`, `/qa`, `/qa-only`, `/design-review`,
`/setup-browser-cookies`, `/setup-deploy`, `/setup-gbrain`, `/retro`, `/investigate`,
`/document-release`, `/document-generate`, `/codex`, `/cso`, `/autoplan`, `/plan-devex-review`,
`/devex-review`, `/careful`, `/freeze`, `/guard`, `/unfreeze`, `/gstack-upgrade`, `/learn`.
