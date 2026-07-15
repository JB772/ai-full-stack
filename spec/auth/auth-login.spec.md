# Build Spec for Auth (Login + JWT Authorization)
- database schema: `.\database\auth.sql` (AppUser, AppUserRole), `.\database\admin.sql` (SysConfig)

> **Not a CRUD table.** This feature is authentication + end-to-end authorization, not a
> list/detail/form triad. It reads from existing tables (no schema changes) and issues/validates JWTs.
> CRUD-only template sections below are marked **N/A**.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | N/A — no owned table; reads `AppUser` (PK `UserId`), `AppUserRole` (PK `UserId`+`RoleId`), `SysConfig` (PK `configKey`) |
| Foreign Keys | N/A (reads only) — `AppUserRole.UserId → AppUser.UserId`, `AppUserRole.RoleId → AppRole.RoleId` are consumed, not written |
| Required Fields | Request: `UserId`, `Password` (both non-empty; empty → generic 401) |
| N-N Relationships | Consumes `AppUserRole` (user ↔ role) read-only to build role claims; no editor |
| Primary-Foreign Links | N/A |
| Query Filters | N/A (no list/query endpoint) |
| Default Sort | Roles read `ORDER BY RoleId` for deterministic claim order |

**Deliverable surface**

| Layer | Artifacts |
|-------|-----------|
| Backend | `AuthController`, `IAuthRepository`/`AuthRepository`, `Security/JwtTokenService`, `Security/JwtSigningKeyProvider`, `Security/PasswordHasher` (existing), models `LoginRequest`/`LoginResponse`/`AuthenticatedUser` |
| Backend config | `Program.cs`: `AddJwtBearer` + global `FallbackPolicy`; `AuthController` is `[AllowAnonymous]` |
| Frontend | `features/auth/`: `auth.model.ts`, `auth.service.ts`, `auth.interceptor.ts`, `auth.guard.ts`, `login/` component; wiring in `app.config.ts`, `app.routes.ts`, `app.ts`/`app.html` |

---

## Localization

### Chinese Table Name

- Auth / Login: 登入 (系統登入與授權)
- Description: authenticates an `AppUser` by password and issues a JWT carrying the user's roles; the
  token then authorizes every other API call.

### Chinese Column Names (login surface)

- UserId: 帳號
- Password: 密碼 (明文，僅驗證用，不儲存/不回傳)
- UserName: 使用者名稱
- accessToken: 存取權杖
- role (claim): 角色

---

## Required Fields

Request (`LoginRequest`):
- **`userId`** — required (empty → generic 401, not 400).
- **`password`** — required (empty → generic 401).

Never accepted or returned: `PasswordHash` (backend-only; see `docs/pk-shapes.md`).

---

## Foreign Keys

**N/A** — the feature only reads `AppUserRole` to enumerate the user's `RoleId`s for claims. No FK is
written.

---

## Foreign-Primary Links / Primary-Foreign Links / N-N editor

**N/A** — no navigation UI. The `AppUserRole` N-N is consumed read-only (role claims) and its editor
remains deferred (see `docs/pk-shapes.md`).

---

## Query Filters

**N/A** — there is no `/query` endpoint.

---

## Credential Check (the core rule)

All three conditions are evaluated **in the SQL `WHERE` clause** so `PasswordHash` is never SELECTed
and never leaves the data layer:

1. `UserId` matches the supplied `userId` exactly.
2. `IsActive = 1`.
3. `PasswordHash = SHA256(password)` — uppercase hex via `PasswordHasher.Hash` (`Convert.ToHexString`).

If **any** check fails — wrong password, unknown `UserId`, inactive user, or empty input — return the
**same** generic `401 { "message": "invalid credentials" }`. Do **not** reveal which check failed.

---

## Token Issuance

On success, `JwtTokenService.CreateToken` builds an HMAC-SHA256 JWT:

- **Signing key**: `SysConfig['appConfig'].symmetricSecurityKey` (JSON blob → `symmetricSecurityKey`
  property), read at runtime — **never hard-coded**. Same JSON-blob path `AppUserRepository` uses for
  `defaultPassword`. Key must be ≥ 32 bytes (256-bit) for HMAC-SHA256.
- **Claims**: `sub` = UserId, `userId`, `userName`, and one **`role`** claim per `RoleId` in
  `AppUserRole` for this user.
- **Lifetime**: expires **24 hours** after issue (`notBefore = now`, `expires = now + 24h`).

Response (`LoginResponse`): `{ userId, userName, accessToken }` — **never** `PasswordHash`.

---

## Authorization (global)

- `Program.cs` registers `AddAuthentication(JwtBearerDefaults...).AddJwtBearer()` and an
  `AddAuthorization` **`FallbackPolicy = RequireAuthenticatedUser()`** → every endpoint requires a valid
  Bearer token by default.
- Only **`AuthController`** carries `[AllowAnonymous]`.
- Validation params: `ValidateIssuer=false`, `ValidateAudience=false`, `ValidateIssuerSigningKey=true`,
  `ValidateLifetime=true`, `ClockSkew=1min`, `MapInboundClaims=false`, `NameClaimType="userId"`,
  `RoleClaimType="role"`. The signing key is supplied by `JwtSigningKeyProvider.Get()` through
  `TokenValidationParameters.IssuerSigningKeyResolver` (validation and issuance share one key source).
- Pipeline order: `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers`.

`JwtSigningKeyProvider` is a singleton that lazily resolves the key from `IAuthRepository`
(`IServiceScopeFactory` scope) and caches it behind a `SemaphoreSlim` — one DB read, reused for all
validations.

---

## Lookup Endpoints Required

**N/A**.

---

## API Endpoints

Standard CRUD six: **N/A**. Only:

| Method | Route | Notes |
|--------|-------|-------|
| `POST` | `/api/auth/login` | Body `LoginRequest { userId, password }`. `200 → LoginResponse`; any failed check → `401 { message: "invalid credentials" }`. |

**Auth exceptions**: `AuthController` is `[AllowAnonymous]`; **all other controllers** require a valid
token via the global `FallbackPolicy`. Requests without/with an invalid Bearer token to any protected
endpoint return **401**. (No per-role `[Authorize(Roles=...)]` on endpoints yet — role gating is
currently frontend-only; see Frontend Notes.)

Route casing: attribute is `[Route("api/auth")]` + `[HttpPost("login")]`; reachable at
`/api/Auth/login` (routing is case-insensitive).

---

## Backend Notes

### Models

```csharp
public class LoginRequest        // write DTO (password never stored/returned)
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse       // response — no PasswordHash
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}

public class AuthenticatedUser   // internal domain (not serialized) — no PasswordHash
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public IReadOnlyList<string> RoleIds { get; set; } = [];
}
```

### SQL — credential check + roles (no INSERT/UPDATE/DELETE)

```sql
-- Only public columns; PasswordHash is compared, never selected.
SELECT UserId, UserName
FROM AppUser
WHERE UserId = @UserId AND IsActive = 1 AND PasswordHash = @PasswordHash;

-- If a row matched, enumerate roles for claims:
SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId;
```

### SQL — signing key lookup

```sql
SELECT configValue FROM SysConfig WHERE configKey = 'appConfig';  -- JSON → symmetricSecurityKey
```
Throw `InvalidOperationException` if the row is missing or the JSON lacks a non-empty
`symmetricSecurityKey`.

### Special Column Notes

- **`PasswordHash` is backend-only** — never in any model, request, response, or SELECT column list.
- **Hash format**: SHA-256, uppercase hex (matches how `AppUserRepository` seeds it). SQL string
  comparison is case-insensitive by default, but keep issuance/validation both uppercase.
- **`IsActive`** default is `1` (see `auth.sql`); an inactive user with the correct password still 401s.

### DI registration (`Program.cs`)

- `AddSingleton<IJwtTokenService, JwtTokenService>()`
- `AddSingleton<JwtSigningKeyProvider>()`
- `AddScoped<IAuthRepository, AuthRepository>()`
- Package: `Microsoft.AspNetCore.Authentication.JwtBearer` (9.0.x) for validation;
  `System.IdentityModel.Tokens.Jwt` (8.3.0) for issuance.

---

## Frontend Notes

All auth code lives under **`features/auth/`** (this repo has no `core/`).

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `auth-profile` | `{ userId, userName, accessToken, roles }` — **session** storage (cleared on tab close), **not** local storage |

`roles` are **decoded from the JWT `role` claim** at login time and persisted in the profile — role
checks never call a separate API. A single role serializes as a string, several as an array; handle both.

### AuthService (`auth.service.ts`)

- `login(request)` → `POST {apiBaseUrl}/Auth/login`, on success writes `auth-profile` and mirrors it into
  a signal.
- Signals/computed: `profile`, `userName`, `roles`, `isAuthenticated`, `isAdmin` (`roles().includes('Admin')`),
  and a `token` getter.
- `logout()` / `clearSession()` remove the key and reset the signal.

### HTTP Interceptor (`auth.interceptor.ts`, functional)

- Attaches `Authorization: Bearer <token>` when a token exists.
- On **any 401 response**: `clearSession()` then `router.navigate(['/login'])`; rethrows the error.
- Registered via `provideHttpClient(withFetch(), withInterceptors([authInterceptor]))` in `app.config.ts`.

### Route Guard (`auth.guard.ts`, `CanActivateChildFn`)

- Returns `true` when authenticated, else `router.createUrlTree(['/login'])`.
- Applied via a **pathless parent route** whose `children` are all feature routes; `/login` sits outside
  the guarded subtree so it stays public. New feature routes go **under this parent**, not top-level.

### App Shell (`app.ts` / `app.html`)

- Renders the sidebar layout only when `auth.isAuthenticated()`; otherwise renders the bare
  `<router-outlet />` so the login page is full-screen.
- Shows the signed-in `auth.userName()` and a **logout** control (calls `auth.logout()` +
  `router.navigate(['/login'])`).
- **Admin menu gating**: the `系統管理 Admin` nav group renders **only when `roles` include `Admin`**
  (`visibleNavGroups` computed filters it out otherwise). Read from the stored profile/token — no API call.

### Login Component (`login/`)

- Reactive form `{ userId, password }` (both `Validators.required`).
- On submit → `auth.login(...)`; success → `router.navigateByUrl('/')`; error → show inline error
  (`帳號或密碼錯誤 Invalid credentials`), no navigation.

### Date Handling / Sub-panels

**N/A**.

---

## Tests

### Backend (xUnit, `CMS.API.Tests`)

- `AuthControllerTests`: valid active user → token; wrong password / unknown UserId / inactive → 401;
  issued JWT carries role claims + ~24h expiry; `PasswordHash` never in the response.
- `AuthorizationTests`: protected endpoint → **401** without a Bearer token, **200** with a valid one,
  **401** with a garbage token; `AuthController` login reachable anonymously.
- **Fake**: `InMemoryAuthRepository` (swapped into `CmsApiFactory`) seeds admin/editor/disabled accounts
  and a known ≥32-char signing key. **New endpoint tests must use
  `CmsApiFactory.CreateAuthenticatedClient()`** — a bare `CreateClient()` now 401s.
- **Not covered by `dotnet test`**: the live `SysConfig` key lookup (fake supplies the key). Verify the
  real Dapper path via Swagger against a DB whose `appConfig` row has `symmetricSecurityKey`.

### Frontend (Karma/Jasmine)

- `auth.service.spec`: login stores profile + decodes roles; single-role string handling; logout clears.
- `auth.interceptor.spec`: attaches Bearer header; no header without a token; 401 clears session +
  redirects to `/login`.
- `auth.guard.spec`: UrlTree to `/login` with no token; `true` with a token.
- `app.spec`: Admin group shown only for `Admin` roles; username + logout rendered; no sidebar when
  signed out.
- `login.spec`: posts `{ userId, password }`, stores session, navigates home; shows error + no nav on 401.

Seed a signed-in session in specs by writing the `auth-profile` JSON to `sessionStorage` **before**
creating the component.
