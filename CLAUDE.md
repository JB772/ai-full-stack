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
`DateOnly`/`TimeOnly` Dapper type handlers are registered in `Program.cs`. **All endpoints require a
JWT bearer token** (global `FallbackPolicy`); only `AuthController` is `[AllowAnonymous]` — see the auth
gotchas below.

**Frontend.** Per table: `features/{table-plural}/{table}-list|-detail|-form/` plus
`{table}.model.ts` and `{table}.service.ts`. Standalone components, lazy-loaded in `app.routes.ts`.
List pages use `p-table` (sortable, paginated) with a `p-drawer` filter, and persist state to
session storage under `{table}-list-filters` / `-sort` / `-page`. Forms use Reactive Forms.
Sidebar entries live in `app.ts` (`navGroups`) and render via `app.html`. Feature routes are children of
a token-guarded pathless parent (`authGuard`), and every request carries a bearer token via
`authInterceptor` — the auth plumbing lives in `features/auth/` (see the auth gotchas below).

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
- **Login / JWT auth is wired up (`POST /api/auth/login`).** `AuthController` + `AuthRepository`
  (`IAuthRepository`) + `Security/JwtTokenService` (`System.IdentityModel.Tokens.Jwt`). The credential
  check — `UserId` match **and** `IsActive = 1` **and** `PasswordHash = SHA256(password)` (uppercase hex
  via `PasswordHasher`) — runs entirely in the SQL `WHERE` clause, so `PasswordHash` is never SELECTed.
  Every failure (wrong password, unknown UserId, inactive, empty input) returns the *same* generic
  `401 {"message":"invalid credentials"}` — don't leak which check failed. On success the JWT is
  HMAC-SHA256-signed with `SysConfig['appConfig'].symmetricSecurityKey` **read at runtime** (never
  hard-coded — same JSON-blob path `AppUserRepository` uses for `defaultPassword`), carries `userId` /
  `userName` + one `role` claim per `AppUserRole.RoleId`, and expires 24h after issue. Response is
  `{ userId, userName, accessToken }` — never `PasswordHash`. The route is `/api/auth/login`, **not** the
  kebab-plural CRUD convention (auth is not a table, so there's no list/detail/form triad). Like the
  default-password path, the **live SysConfig key lookup is not covered by `dotnet test`** — the
  `InMemoryAuthRepository` fake (swapped into `CmsApiFactory`) supplies a known ≥32-char key; verify the
  real Dapper path via Swagger against a DB whose `appConfig` row has `symmetricSecurityKey`.
- **Self-service profile edit is `PUT /api/auth/profile` (authenticated) — identity comes from the JWT,
  never the body.** The signed-in user may change **only their own `UserName`**. `AuthController.UpdateProfile`
  reads `UserId` from `User.FindFirstValue(JwtTokenService.UserIdClaimType)` and passes it to
  `IAuthRepository.UpdateUserNameAsync(userId, userName)` (Dapper `UPDATE AppUser SET UserName WHERE UserId`,
  then reads the row + roles back). `UpdateProfileRequest` *has* a `UserId` field but it is **deliberately
  ignored** (present only so tests can prove a body `UserId` can't retarget another account); `UserName` is
  trimmed and required (empty/whitespace → **400**, not the login-style generic 401). Response is
  `ProfileResponse { userId, userName, roles }` (roles are display-only, unchangeable here). Frontend:
  `features/auth/profile/` (`Profile` component, route `/profile` under the token-guarded parent, "我的個人資料
  My Profile" link in the shell's `sidebar-user` block). `AuthService.updateProfile(userName)` PUTs `{ userName }`
  and, on success, writes the new name back into the `auth-profile` session blob **and** the `profileSignal`, so
  `App`'s `userName()` refreshes live — roles/token in the session are left intact. Like login, the real Dapper
  `UPDATE` isn't covered by `dotnet test` (the in-memory fake mutates its seed list); smoke-test via Swagger.
- **Self-service password change is `POST /api/auth/change-password` (authenticated) — identity comes from
  the JWT, never the body.** `AuthController.ChangePassword` (`[Authorize]`) reads `UserId` from
  `User.FindFirstValue(JwtTokenService.UserIdClaimType)`; `ChangePasswordRequest` carries only the three
  plaintext fields (`CurrentPassword` / `NewPassword` / `ConfirmNewPassword`) — no `UserId`. Order is fixed
  and each failure is a **400 with a descriptive `message`** (not the login-style generic 401, since the
  caller is already authenticated and the UI shows which check failed): **(1)** verify current password by
  *reusing* `IAuthRepository.AuthenticateAsync(userId, SHA256(current))` (hash compared in the SQL `WHERE`,
  never SELECTed) — wrong → 400, nothing changes; **(2)** complexity via `Security/PasswordPolicy.IsComplexEnough`
  (length ≥ 8 **and** ≥ 3 of 4 classes: upper/lower/digit/symbol, where "symbol" = not letter-or-digit) →
  400 with the exact bilingual `PasswordPolicy.ComplexityMessage`; **(3)** `NewPassword != ConfirmNewPassword`
  → 400; **(4)** success → `IAuthRepository.UpdatePasswordAsync(userId, SHA256(new))`
  (Dapper `UPDATE AppUser SET PasswordHash = @hash, PasswordUpdatedTime = SYSDATETIME() WHERE UserId` —
  `SYSDATETIME()` to match the existing reset-password path in `AppUserRepository`) and
  **204 No Content**. **No password hash ever crosses the wire in either direction** (success returns an empty
  body). `PasswordPolicy` is a static shared source of truth: the Angular `passwordComplexityValidator` and
  `COMPLEXITY_MESSAGE` in `features/auth/profile/profile.ts` mirror it verbatim. Frontend: a second `<form
  data-testid="change-password-form">` on the same `Profile` page (below 帳號資料), a group-level
  `passwordsMatchValidator`, and `AuthService.changePassword({ currentPassword, newPassword, confirmNewPassword })`
  which **touches no session state** (the token stays valid). Like login/profile, the real Dapper `UPDATE`
  isn't covered by `dotnet test`; the `InMemoryAuthRepository` fake stamps `PasswordUpdatedTime` and swaps the
  hash, so `ChangePasswordTests` asserts state via the singleton fake (`_factory.Services.GetRequiredService`)
  **and** end-to-end by re-logging-in with the new password — smoke-test the live path via Swagger.
- **Authorization is global: every endpoint requires a valid Bearer token except `AuthController.Login`.**
  `Program.cs` sets a `FallbackPolicy` (`RequireAuthenticatedUser`) + `AddJwtBearer`; `[AllowAnonymous]`
  is **action-scoped on `Login` only** (not the whole controller — `AuthController` also hosts the
  authenticated `PUT /api/auth/profile` and `POST /api/auth/change-password`, which carry `[Authorize]` and
  fall under the global policy). The bearer signing key is resolved at runtime by
  `JwtSigningKeyProvider` (a singleton that reads the same `symmetricSecurityKey` via `IAuthRepository`
  and caches it) and fed to `TokenValidationParameters.IssuerSigningKeyResolver`; validation uses
  `MapInboundClaims = false` + `RoleClaimType = "role"`, no issuer/audience. **Consequence for tests:**
  a plain `_factory.CreateClient()` now gets **401** on any feature endpoint — new controller tests must
  use **`_factory.CreateAuthenticatedClient()`** (or `CreateToken(...)`), the helpers on `CmsApiFactory`
  that mint a real signed token. Only `AuthControllerTests` / the unauthenticated-path cases in
  `AuthorizationTests` use a bare client on purpose.
- **Frontend auth lives in `features/auth/` (no `core/`).** `AuthService` stores the profile
  (`userId` / `userName` / `accessToken` / `roles`) in **session** storage under `auth-profile`; roles
  are **decoded from the JWT `role` claim**, never fetched separately. `authInterceptor` attaches
  `Authorization: Bearer <token>` and, on any **401**, clears the session and routes to `/login`.
  `authGuard` (a `CanActivateChildFn` on a pathless parent route wrapping every feature route) redirects
  to the public `/login` when there's no token. The `App` shell renders the sidebar only when
  authenticated (login is full-screen), shows the signed-in `userName` + a logout control, and **hides
  the `系統管理 Admin` nav group unless `roles` includes `Admin`** (via an `isAdmin` computed). When
  seeding a signed-in session in a spec, write the `auth-profile` JSON to `sessionStorage` before
  creating the component (see `app.spec.ts`).
- **PrimeNG major version tracks Angular's** (20 → 20); `primeng@latest` pulls v21 and fails peer resolution.
- **QR codes use the framework-agnostic `qrcode` package, not `angularx-qrcode`** — it has no Angular
  peer dep and returns a PNG data URL, which doubles as the `<img [src]>` and the download payload
  (build an anchor with `href=dataUrl`, `download={name}.png`, `.click()`). See the QR block in
  `course-detail` (title + image + download button in 基本資料). In tests, `qrcode.toDataURL` is
  overloaded — `spyOn(QRCode, 'toDataURL') as unknown as jasmine.Spy` to stub it.
- **The viewport is the scroll container — there is no fixed app header.** `.layout` is a flex row
  (sidebar + `.content`) with `min-height: 100vh`; the document itself scrolls. So a `.page-toolbar`
  can be pinned with plain `position: sticky; top: 0` (+ a `z-index` above the fields) — no scroll
  listener, no fixed positioning. It sticks to the viewport top within the content region without
  covering the sidebar (a separate flex column) or a header (there is none), and the toolbar's opaque
  `--p-content-background` keeps scrolling fields from showing through. `course-form` does this via a
  `sticky-toolbar` class on both New and Edit (one component serves both). Assert it in a headless
  Karma run with `getComputedStyle(el).position === 'sticky'`.
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
3. Frontend: model → service → list / detail / form → route (`/new` before `/:id`, added under the
   token-guarded pathless parent in `app.routes.ts`, **not** at the top level) → sidebar entry in
   `app.ts` (`app.html` renders nav groups generically). A custom UI can collapse this to one component
   on a single route (see `FeaturedPromoItem` in `docs/reference-features.md`).
4. Tests both sides: xUnit endpoints (list/filter, view, add, edit, delete-guard) driven through
   `CmsApiFactory.CreateAuthenticatedClient()` — a bare `CreateClient()` now 401s — plus an in-memory
   fake swapped into `CmsApiFactory`; Karma for the components and the service.

The `/crud` skill automates steps 1–4, but it assumes a `core/` layout and a RowAudit subsystem this
repo lacks — follow the code and the always-true rules above.
