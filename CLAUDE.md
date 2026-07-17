# CLAUDE.md

Guidance for Claude Code when working in this repository.

> **How to read this file.** Only always-needed rules and the doc index live here. Detail lives in
> `docs/*.md` — open the relevant file **before** working in an area. Read the doc, don't reconstruct it.

## What this is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth —
already deployed and populated. Never recreate or migrate it; read `database/*.sql` for a table's
shape before writing code against it.

| Path | What |
|------|------|
| `database/*.sql` | The schema. Reference only. |
| `spec/code-gen.convention.md` | **The** code-gen convention — read before adding any feature. |
| `spec/{sub-system}/{Table}.md` | Built feature specs. `sample1/2.spec.md` are **aspirational** — don't conflate. |
| `docs/*.md` | Deep-dive references — index below. |
| `TODOS.md` | Backlog **plus the settled decisions behind it** — read before re-raising any finding. |
| `src/CMS.API` / `src/CMS.API.Tests` | .NET 9 Web API, Dapper, Swagger, port 5000 / xUnit. |
| `src/CMS.NG` | Angular 20 standalone + PrimeNG 20, port 4200. |

## Commands

```powershell
dotnet run --project src\CMS.API      # API + Swagger at http://localhost:5000/swagger
dotnet test                           # xUnit — stop the API first: Get-Process CMS.API | Stop-Process -Force
cd src\CMS.NG; npm start              # http://localhost:4200
cd src\CMS.NG; npm test               # Karma + Jasmine (headless needs CHROME_BIN)
```

Windows traps (`node: command not found` in a stale shell, a running API breaking `dotnet test` with
`MSB3021`, `.ps1` policy, headless Chrome, UTF-8 curl) → **`docs/environment.md`**.

## Hard rules — never violate

- **The database is real** — test writes land in the live DB. Test via `CMS.API.Tests` (in-memory
  fakes) or portable-SQL SQLite → `docs/schema-and-testing.md`.
- **New repositories must be swapped in `CmsApiFactory`** — miss one and tests hit real SQL Server.
- **Every repository write is audited**; **every detail/edit page** carries `RowAuditBadgeComponent`
  → `docs/row-audit.md`.
- **No per-controller `try/catch`** — the global middleware owns unexpected errors; frontend errors
  flow only through `authInterceptor` → `docs/exception-handling.md`.
- **Auth is global** — only login is anonymous; tests use `CreateAuthenticatedClient()`; frontend
  specs seed `auth-profile` in session storage first → `docs/auth.md`.
- **The「系統管理 Admin」nav group is Admin-only, enforced server-side** — only the
  `[Authorize(Roles = "Admin")]` attribute is the boundary; route guard and nav hiding are
  usability/cosmetic. `PublishStatuses` gates writes only → `docs/auth.md` § "The Admin boundary".
- **Open security findings are deferred by decision, not oversight** — valid only while the app is
  localhost-only; if a re-open trigger goes true, re-score **before** deploying
  → `TODOS.md` § "Security findings — deferred by decision".
- **`nchar(n)` columns need `RTRIM()`** in every SELECT.
- **Design/a11y conventions are load-bearing** — red-as-text is `--p-red-600`, never `red-500`
  (fails WCAG AA); icon-only `p-button` needs the `ariaLabel` **input**, not `[attr.aria-label]`
  → `docs/ui-patterns.md` § "Design & a11y conventions".
- **`/crud` skill layout is wrong for this repo** (assumes `core/` + `AuditHelper`) — follow the code.
- **PrimeNG major tracks Angular's** (20 → 20); `primeng@latest` pulls v21 and fails peer resolution.

## Doc index — read the relevant file before working in that area

| Doc | Read it before… |
|-----|-----------------|
| `docs/architecture.md` | **any backend/frontend change**: per-table code shapes, routing/state conventions, the add-a-feature workflow + PK-shape reference table |
| `docs/reference-features.md` | adding a feature: the secondary pattern each built feature demonstrates |
| `docs/pk-shapes.md` | adding a table: the four PK shapes; `AppUser`'s backend-only `PasswordHash` |
| `docs/auth.md` | anything auth: login/JWT flows, `PasswordPolicy`, Admin reset, test seam, frontend plumbing |
| `docs/row-audit.md` | touching audit: `RowAuditWriter` rules + test seam, read path, `RowAuditBadgeComponent` |
| `docs/exception-handling.md` | touching error handling: global 500 middleware, `authInterceptor`, spec requirements |
| `docs/delete-guards.md` | writing any DELETE: 409 guards, multi-child messages, `ON DELETE CASCADE` checks |
| `docs/relationships-and-nav.md` | FKs or N-N: multi-map nav objects, nullable-FK `LEFT JOIN`, `forkJoin` lookups, date ⇄ `p-datepicker`, the `AppUserRole` N-N editor |
| `docs/ui-patterns.md` | building a frontend page: sticky toolbar, QR, PDF export, inline editing, overlay blur trap, design/a11y conventions — load-bearing, read before reaching for any |
| `docs/schema-and-testing.md` | reading schema / writing tests: constraint traps (keys may lack UNIQUE indexes, two-FK ≠ junction, multi-file `.sql`), `p-table` sort gotchas |
| `docs/environment.md` | any Windows/PowerShell dev trap |

## Skill routing

The [gstack](https://github.com/garrytan/gstack) skill suite is installed. **Use `/browse` for all
web browsing; never use `mcp__claude-in-chrome__*` tools.**

When a request matches a skill, invoke it via the Skill tool: bugs/errors → `/investigate` ·
QA site behavior → `/qa` (report-only: `/qa-only`) · code review → `/review` · visual polish →
`/design-review` · ship/deploy/PR → `/ship` · plan reviews → `/plan-eng-review`, `/plan-ceo-review`,
`/plan-design-review` (all: `/autoplan`) · spec/issue authoring → `/spec` · brainstorming →
`/office-hours` · save/resume context → `/context-save`, `/context-restore`.

Exception: `/crud` — its layout doesn't match this repo (see hard rule); follow the code.
