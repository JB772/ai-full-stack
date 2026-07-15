# Auth — login, JWT, self-service profile/password, admin reset

All auth lives in `AuthController` (`/api/auth/*`, **not** the kebab-plural CRUD convention — auth is not
a table, so there's no list/detail/form triad) + `AuthRepository` (`IAuthRepository`) +
`Security/JwtTokenService` + `Security/PasswordPolicy` + `Security/PasswordHasher`; the frontend lives in
`features/auth/`.

Two load-bearing invariants across everything here:

- **`PasswordHash` is never SELECTed or returned.** Every credential check compares it inside the SQL
  `WHERE` clause; no endpoint puts a password or hash on the wire in either direction.
- **The live `SysConfig['appConfig']` JSON-blob lookups (signing key + default password) are NOT covered
  by `dotnet test`.** The `InMemoryAuthRepository` fake (swapped into `CmsApiFactory`) supplies known
  values, so endpoint tests never exercise the real Dapper lookup. Smoke-test the live paths via Swagger
  against a DB whose `appConfig` row has `symmetricSecurityKey` + `defaultPassword`.

## Global authorization & the test seam

Every endpoint requires a valid Bearer token except `AuthController.Login`. `Program.cs` sets a
`FallbackPolicy` (`RequireAuthenticatedUser`) + `AddJwtBearer`; `[AllowAnonymous]` is **action-scoped on
`Login` only** (not the whole controller — `AuthController` also hosts the authenticated
`PUT /api/auth/profile`, `POST /api/auth/change-password`, and the Admin-only
`POST /api/auth/reset-password` (`[Authorize(Roles = "Admin")]` → 403 for non-Admins), all of which carry
`[Authorize]` and fall under the global policy).

**Consequence for tests:** a plain `_factory.CreateClient()` gets **401** on any feature endpoint. New
controller tests must use **`_factory.CreateAuthenticatedClient()`** (or `CreateToken(...)`), the helpers
on `CmsApiFactory` that mint a real signed token — **a bare token defaults to the `Admin` role**, so to
test a role restriction pass an explicit non-Admin role (e.g. `CreateAuthenticatedClient(..., "Editor")`).
Only `AuthControllerTests` / the unauthenticated-path cases in `AuthorizationTests` use a bare client on
purpose.

## Login — `POST /api/auth/login` (anonymous)

`AuthController` + `AuthRepository` + `JwtTokenService` (`System.IdentityModel.Tokens.Jwt`). The
credential check — `UserId` match **and** `IsActive = 1` **and** `PasswordHash = SHA256(password)`
(uppercase hex via `PasswordHasher`) — runs entirely in the SQL `WHERE` clause. Every failure (wrong
password, unknown UserId, inactive, empty input) returns the *same* generic
`401 {"message":"invalid credentials"}` — don't leak which check failed. On success the JWT carries
`userId` / `userName` + one `role` claim per `AppUserRole.RoleId`, and expires 24h after issue. Response
is `{ userId, userName, accessToken }` — never `PasswordHash`.

## Signing key & JWT validation

The JWT is HMAC-SHA256-signed with `SysConfig['appConfig'].symmetricSecurityKey` **read at runtime**
(never hard-coded — same JSON-blob path `AppUserRepository` uses for `defaultPassword`). On the validation
side the bearer signing key is resolved at runtime by `JwtSigningKeyProvider` (a singleton that reads the
same `symmetricSecurityKey` via `IAuthRepository` and caches it) and fed to
`TokenValidationParameters.IssuerSigningKeyResolver`; validation uses `MapInboundClaims = false` +
`RoleClaimType = "role"`, no issuer/audience. The `InMemoryAuthRepository` fake supplies a known ≥32-char
key so HMAC-SHA256 works in tests.

## Self-service profile edit — `PUT /api/auth/profile` (authenticated)

Identity comes from the JWT, **never** the body. The signed-in user may change **only their own
`UserName`**. `AuthController.UpdateProfile` reads `UserId` from
`User.FindFirstValue(JwtTokenService.UserIdClaimType)` and passes it to
`IAuthRepository.UpdateUserNameAsync(userId, userName)` (Dapper `UPDATE AppUser SET UserName WHERE UserId`,
then reads the row + roles back). `UpdateProfileRequest` *has* a `UserId` field but it is **deliberately
ignored** (present only so tests can prove a body `UserId` can't retarget another account); `UserName` is
trimmed and required (empty/whitespace → **400**, not the login-style generic 401). Response is
`ProfileResponse { userId, userName, roles }` (roles are display-only, unchangeable here).

Frontend: `features/auth/profile/` (`Profile` component, route `/profile` under the token-guarded parent,
"我的個人資料 My Profile" link in the shell's `sidebar-user` block). `AuthService.updateProfile(userName)`
PUTs `{ userName }` and, on success, writes the new name back into the `auth-profile` session blob **and**
the `profileSignal`, so `App`'s `userName()` refreshes live — roles/token in the session are left intact.
The real Dapper `UPDATE` isn't covered by `dotnet test` (the in-memory fake mutates its seed list).

## Self-service password change — `POST /api/auth/change-password` (authenticated)

Identity comes from the JWT, **never** the body. `AuthController.ChangePassword` (`[Authorize]`) reads
`UserId` from `User.FindFirstValue(JwtTokenService.UserIdClaimType)`; `ChangePasswordRequest` carries only
the three plaintext fields (`CurrentPassword` / `NewPassword` / `ConfirmNewPassword`) — no `UserId`. Order
is fixed and each failure is a **400 with a descriptive `message`** (not the login-style generic 401,
since the caller is already authenticated and the UI shows which check failed):

1. verify current password by *reusing* `IAuthRepository.AuthenticateAsync(userId, SHA256(current))` (hash
   compared in the SQL `WHERE`, never SELECTed) — wrong → 400, nothing changes;
2. complexity via `Security/PasswordPolicy.IsComplexEnough` (length ≥ 8 **and** ≥ 3 of 4 classes:
   upper/lower/digit/symbol, where "symbol" = not letter-or-digit) → 400 with the exact bilingual
   `PasswordPolicy.ComplexityMessage`;
3. `NewPassword != ConfirmNewPassword` → 400;
4. success → `IAuthRepository.UpdatePasswordAsync(userId, SHA256(new))` (Dapper `UPDATE AppUser SET
   PasswordHash = @hash, PasswordUpdatedTime = SYSDATETIME() WHERE UserId`) and **204 No Content**.

`PasswordPolicy` is a static shared source of truth: the Angular `passwordComplexityValidator` and
`COMPLEXITY_MESSAGE` in `features/auth/profile/profile.ts` mirror it verbatim. Frontend: a second `<form
data-testid="change-password-form">` on the same `Profile` page (below 帳號資料), a group-level
`passwordsMatchValidator`, and `AuthService.changePassword({ currentPassword, newPassword,
confirmNewPassword })` which **touches no session state** (the token stays valid). The real Dapper `UPDATE`
isn't covered by `dotnet test`; the `InMemoryAuthRepository` fake stamps `PasswordUpdatedTime` and swaps
the hash, so `ChangePasswordTests` asserts state via the singleton fake
(`_factory.Services.GetRequiredService`) **and** end-to-end by re-logging-in with the new password.

## Admin reset-to-default — `POST /api/auth/reset-password` (`[Authorize(Roles = "Admin")]`)

A non-Admin gets **403** (enforced server-side, not just a hidden button). Unlike profile/change-password,
the target identity comes from the **body** (`ResetPasswordRequest { userId }`), because an admin is
resetting *someone else's* password — the JWT identifies the *caller* (for the role check), not the
target. `AuthController.ResetPassword` trims `userId` (empty/whitespace → **400**), then
`IAuthRepository.ResetPasswordToDefaultAsync(userId)` reads `defaultPassword` from `SysConfig['appConfig']`
**at runtime** (same JSON-blob path as `AppUserRepository`, its own private `GetDefaultPasswordAsync`),
writes `PasswordHash = SHA256(default)` + `PasswordUpdatedTime = SYSDATETIME()` (`UPDATE … WHERE UserId`),
and returns **204** (unknown user → **404**). Request is just `{ userId }`; success is an empty body.

This **replaced** an earlier unguarded `POST /api/app-users/{id}/reset-password` (keyed by pkid) — that
endpoint, `IAppUserRepository.ResetPasswordAsync`, and its fake/tests were **removed**; don't reintroduce a
second reset path. Frontend: `AuthService.resetPasswordToDefault(userId)` (POSTs `{ userId }`, touches no
session state); an **Admin-only** "重設密碼" button (gated on `AuthService.isAdmin`) on the AppUser **edit
form** (`app-user-form`, edit mode only) **and** the detail page (`app-user-detail`), each with a
`ConfirmationService.confirm` dialog sending only the account's natural-key `UserId`. The live SysConfig
default-password lookup isn't covered by `dotnet test` — the fake supplies a known `DefaultPassword`, so
`ResetPasswordTests` asserts `SHA256(default)` + stamped time via the singleton fake (and end-to-end by
logging in with the default).

## Frontend auth plumbing

Lives in `features/auth/` (**no `core/`**). `AuthService` stores the profile (`userId` / `userName` /
`accessToken` / `roles`) in **session** storage under `auth-profile`; roles are **decoded from the JWT
`role` claim**, never fetched separately. `authInterceptor` attaches `Authorization: Bearer <token>` and,
on any **401**, clears the session and routes to `/login`. `authGuard` (a `CanActivateChildFn` on a
pathless parent route wrapping every feature route) redirects to the public `/login` when there's no token.
The `App` shell renders the sidebar only when authenticated (login is full-screen), shows the signed-in
`userName` + a logout control, and **hides the `系統管理 Admin` nav group unless `roles` includes `Admin`**
(via an `isAdmin` computed).

When seeding a signed-in session in a spec, write the `auth-profile` JSON to `sessionStorage` before
creating the component (see `app.spec.ts`).
