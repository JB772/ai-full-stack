# TODOS

## Security findings — deferred by decision, 2026-07-17

**Decision:** every still-open security finding from the 2026-07-16 /review audit, the 2026-07-17
whole-project audit and the 2026-07-17 /cso audit is **deferred**. None is being fixed now. They stay
filed below with evidence so the next session neither re-discovers nor re-litigates them.

**This deferral rests on one fact, and it is the only thing holding it up:**

> **The app runs on localhost only.** `environment.ts:3` sets the *production* `apiBaseUrl` to
> `http://localhost:5000/api`; the API binds `http://localhost:5000` (`launchSettings.json`); CORS admits
> loopback origins only (`Program.cs:55-61`); SQL Server is local SQLEXPRESS over Windows integrated auth
> with no password in the connection string (`appsettings.json`). The attacker who could use any of these
> findings must already be an authenticated operator on the machine.

**Re-open trigger — if any of these becomes true, this whole section is void and the findings must be
re-scored before the deploy, not after:**

1. The API binds anything other than loopback, or the frontend points at a non-localhost `apiBaseUrl`.
2. The DB moves off Windows integrated auth (a password enters the connection string).
3. Accounts are issued to anyone outside the trusted-operator group.

**What is deferred, and where each lives:**

| ID | Finding | Item |
|---|---|---|
| `SEC-01`/`SEC-06` | Unsalted SHA-256 + same default password on every account | P1 — *Migrate password hashing* |
| `SEC-07` | No rate limiting / lockout on login | P1 — same item (compounding factor) |
| `SEC-05` | Role claims baked into a 24h JWT; revocation lags | P1 — *Revoke role changes* |
| `SEC-04` | `UseSwagger()` unconditional | P2 — *Swagger exposed in every environment* |
| `SEC-08` | Signing key cached for process lifetime, no length validation | P3 — *Harden JWT signing key* |

**Not deferred, because it was never a finding:** `GET /api/lookups/app-roles` and `GET /api/rowaudit` were
both refused by independent verifiers (3/10, 2/10) as documented-intentional, read-only and
escalation-free. The Lookups one survives as a P3 **tidy-up** only. See that item before re-filing either.

## P1 — Migrate password hashing off unsalted SHA-256

- **What:** `src/CMS.API/Security/PasswordHasher.cs` hashes with a single unsalted round of SHA-256
  (`SHA256.HashData(...)` → hex). Move to a salted, slow KDF: ASP.NET Core's `PasswordHasher<T>` (PBKDF2)
  or Argon2id/bcrypt.
- **Why:** No salt means identical passwords produce identical hashes, so a stolen `AppUser` table falls to
  precomputed tables; a fast hash means GPU brute force. The DB is live and populated, so this is real
  stored-credential exposure. Verified 2026-07-16 by the /review audit and independently by Codex.
- **Compounding factors** (all verified, all argue for doing this sooner):
  - Every account created **or reset** gets the same system-wide default password
    (`AppUserRepository.cs:79` and `AuthRepository.cs:112` both hash `SysConfig.appConfig.defaultPassword`)
    — unsalted, so every such account shares one identical hash.
  - Login never reads `PasswordUpdatedTime`, so a first-login password change is **not** enforced.
  - No rate limiting, throttling, or account lockout anywhere (`grep` for `AddRateLimiter` → nothing), so
    the online guessing path is unthrottled too.
- **Migration shape (no lockout):** keep the legacy path for verification only — on a successful login
  against a 64-char-hex legacy hash, re-hash the supplied plaintext with the new KDF and overwrite.
  `AppUser.PasswordHash` is `nvarchar(800)`, so column width does not block the encoded hash.
- **Blast radius:** forces the credential comparison **out of the SQL `WHERE`**
  (`AuthRepository.cs:25` currently does `... AND PasswordHash = @PasswordHash`) and into application code
  using a constant-time verify. The data layer must SELECT the stored hash — it must never reach the wire.
- **Effort:** M (human) → M (CC). **Priority:** P1. Deferred by decision on 2026-07-16 (report only),
  **reaffirmed 2026-07-17** — see the deferral section at the top of this file and its re-open trigger.
  Re-confirmed still open by the 2026-07-17 /cso audit, which flagged it as the biggest real risk in the
  codebase; the injection specialist re-raised it independently.

## P1 — Revoke role changes without waiting out the 24h token (`SEC-05`)

- **What:** role claims are baked into the JWT at login (`JwtTokenService.cs:37` —
  `claims.AddRange(roleIds.Select(roleId => new Claim(RoleClaimType, roleId)))`) with a 24h lifetime
  (`JwtTokenService.cs:24` — `TokenLifetime = TimeSpan.FromHours(24)`). Validation checks signature and
  lifetime only (`Program.cs:93-104`) — there is no `OnTokenValidated` hook, so nothing re-reads the DB.
  **Revoke someone's Admin role and they stay Admin until their token expires — up to 24 hours.**
  Deactivating an account (`IsActive = 0`) and changing a password have the same lag: login checks
  `IsActive = 1` (`AuthRepository.cs:50`), but an already-issued token is never re-checked against it.
- **Why it's P1:** this is **the ceiling on the Admin boundary shipped 2026-07-17** (`84a5389`). That commit
  made roles a real boundary; this is the one way through it. It was Minor while roles weren't a boundary —
  the reversal promoted it, and the 2026-07-17 audit named it *"Recommended next"*. Bounded: it needs prior
  privilege, so it is **revocation latency, not escalation**. Found by Codex, missed by the Claude
  specialists. Re-confirmed still open by the 2026-07-17 /cso audit.
- **Fix shape:** add a security stamp — a column bumped on any role/password/IsActive change — and re-check
  it in an `OnTokenValidated` handler, failing the token when it disagrees. Costs one DB read per request.
  Cheaper alternative: shorten `TokenLifetime` and add refresh, which shrinks the window without closing it.
- **Effort:** M (human) → M (CC). **Priority:** P1. **Deferred 2026-07-17** — see the deferral section at
  the top of this file and its re-open trigger.

## P2 — Fixes found by the 2026-07-16 whole-project audit, not yet applied

All verified with quoted evidence; left unapplied pending a decision.

- **Timezone: password-changed time renders 8h in the future (UTC+8).** `app-user-detail.html:49` and
  `app-user-list.html:62` append `+ 'Z'` before the `DatePipe`, but `PasswordUpdatedTime` is written with
  `SYSDATETIME()` (server **local**, not `SYSUTCDATETIME()`). The `Z` makes Angular read local time as UTC
  and re-render it in the browser zone. These are the only two `+ 'Z'` in the codebase;
  `row-audit-badge.html:13` renders the same offset-less `DateTime` shape without `Z` and is correct.
  **Fix:** delete `+ 'Z'` from both lines.
- ~~**TOCTOU: silent cascade delete on CourseGroup and Course.**~~ **FIXED 2026-07-17.** Both repositories
  now re-check the child counts inside the delete transaction and return the new
  `DeleteResult` (`NotFound` / `Blocked` / `Deleted`, mirroring the existing `MoveResult` convention);
  both controllers map `Blocked` → 409. The controller pre-check stays as the fast path with the friendly
  per-child message. **Residual test gap:** endpoint tests run against the in-memory fakes, so the real
  Dapper guard is verified by reading, not by test — the fakes mirror it and carry a comment saying to keep
  them in step. See `docs/delete-guards.md`.
- **409 message names zero-count children.** `PublishStatusesController.cs:94` reports
  `仍有 3 筆課程、0 筆促銷活動`, against `docs/delete-guards.md` ("name only the ones that actually have
  rows"). Every sibling controller has a `DescribeBlockers` helper; add one mirroring `PartnersController`.
- **Swagger exposed in every environment** (`SEC-04`). `Program.cs:118` calls `app.UseSwagger()`
  unconditionally, and middleware runs before endpoint authorization, so the global `FallbackPolicy` does not
  cover it — the API inventory is anonymously readable wherever this deploys. Fine if it never leaves
  localhost/LAN (CLAUDE.md documents Swagger as the dev entry point); wrap in `IsDevelopment()` otherwise.
  **Deferred 2026-07-17** — this is the finding most directly voided by the re-open trigger at the top of
  this file: it is harmless on loopback and anonymous-read the moment it is not.
- **`AuthRepository` writes are unaudited.** See the P2 item below.

## ~~P2 — Audit the three AppUser writes in `AuthRepository`~~ — DONE 2026-07-17

`AuthRepository` was the only write-bearing repository not injecting `RowAuditWriter` (set difference: 8
repos write, 7 audited). Fixed: all three `AppUser` writes (`UpdateUserNameAsync`, `UpdatePasswordAsync`,
`ResetPasswordToDefaultAsync`) now run in a transaction with a before/after reload and `LogUpdateAsync` on the
same connection + transaction. That set difference is now empty.

Two things to preserve if you touch this (detail in `docs/row-audit.md`):
- It audits the **`AppUser` response model, which has no `PasswordHash` property** — deliberate, so ActionDesc
  (a list of changed *column names*) can only ever say `PasswordUpdatedTime`. The hash must never reach
  `dbo.RowAudit`, which any authenticated user can read via `GET /api/rowaudit`.
- `ResetPasswordToDefaultAsync` passes `tx` into the `SysConfig` read, because a non-transactional command on
  a connection with a pending transaction is rejected by SQL Server.

Covered by `AuthRepositoryAuditTests` — 8 tests against the **real** repository via in-memory SQLite
(`SYSDATETIME()` defined as a SQLite function; all other SQL unmodified), including a rollback test proving a
failed audit write rolls the password change back.

## P2 — Shrink the 11.5 MB Noto Sans TC payload without introducing tofu

- **What:** 存成 PDF ships two full Noto Sans TC faces (5.68 MB + 5.84 MB = 11.52 MB) from
  `src/CMS.NG/public/fonts/`, fetched before the first PDF can be generated. Find a way to cut that
  which does **not** silently blank characters.
- **Why:** It is ~77% of the production build. But **subsetting was investigated and rejected on
  2026-07-16** — don't re-run the same experiment. Measured, with harfbuzz (`subset-font@2.5.0`,
  output verified as valid `OTTO`/CFF and glyph-checked with pdfmake's own `@foliojs-fork/fontkit`):

  | Subset | Chars | Both faces | Verdict |
  |---|---|---|---|
  | Full Big5 (L1 常用字 + L2 次常用字) | 13,276 | **7.91 MB** (−31%) | drops 2,315 main-block CJK |
  | Big5 L1 only | 5,624 | 3.32 MB (−71%) | far more tofu |
  | Drop only CJK Ext A (rare/historical) | 20,170 | 11.10 MB (−3.6%) | savings vanish |

- **The trap:** the tempting rationale is "Big5 bounds the Traditional Chinese this CMS can hold."
  **It is false.** `Course.Title` etc. are `nvarchar` (UTF-16) and store any Unicode. Verified: the
  upstream face covers 20,745 codepoints / 15,958 CJK ideographs; a full-Big5 subset drops 2,315 of
  the main CJK block (U+4E00–9FFF). Most are simplified forms (东両丢两个丽义会) a TC CMS won't hold,
  **but 喆 (U+5586) and 堃 (U+5803) are ordinary Taiwanese name characters** and would render as blank
  boxes — silently, no error, no log — in the field the archive exists to preserve.
- **Therefore:** subsetting a CJK font only pays by dropping real characters. There is no free
  1–2 MB. A safe subset needs a *sourced* non-Big5 Traditional allowlist (教育部姓名用字表 or
  similar); guessing one recreates the same under-inclusive bug, just smaller.
- **Alternatives not yet costed:** (a) server-side generation in `CMS.API` — font stays on disk,
  client downloads only the finished PDF, and the 產生於 stamp becomes server-asserted instead of
  client clock + sessionStorage; (b) map `bold` → the Regular face, halving to 5.68 MB for the cost
  of unbolded labels (design already accepts a degraded `italics` mapping).
- **Also:** the fonts sit at an **unfingerprinted** URL. `outputHashing: "all"` does not touch
  `public/` assets (verified: the build emits `dist/CMS.NG/browser/fonts/NotoSansTC-Bold.otf`
  unhashed), so a long `immutable` cache header can't be busted and a short one re-downloads
  11.5 MB. A `fonts/v1/` path segment fixes that independently of any size work.
- **Effort:** M (human) → M (CC). **Priority:** P2.

## P3 — Harden the JWT signing key handling (`SEC-08`)

- **What:** `JwtSigningKeyProvider.Get()` (`JwtSigningKeyProvider.cs:15`) reads
  `SysConfig['appConfig'].symmetricSecurityKey` once and caches it for **process lifetime**
  (`:13` `private string? _cachedKey;` behind the `:12` `SemaphoreSlim _gate`, assigned at `:29` and never
  invalidated). Rotating the key in the DB therefore does nothing until the API restarts. The key is also
  never length-validated: it goes straight to `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(...))`
  (`Program.cs:103` for validation, `JwtTokenService.cs:28` for issuing), so a key shorter than 32 bytes
  fails at first *use* with an opaque error rather than at startup. Issuer/audience validation is off
  (`Program.cs:95-96`) — deliberate, since tokens carry neither.
- **Why it's only P3:** it matters if the symmetric key is ever shared with another service, or if a rotation
  is ever attempted and silently does nothing. Neither applies today — one process, one key, no rotation
  procedure. No exploit path on the current posture.
- **Fix shape:** validate `>= 32` bytes where the key is loaded so a weak or truncated key fails fast at
  startup instead of at first login. Rotation support only matters if a second consumer of the key appears.
- **Effort:** S (human) → S (CC). **Priority:** P3. **Deferred 2026-07-17** — see the deferral section at the
  top of this file.

## P3 — Delete or gate `GET /api/lookups/app-roles` (dead endpoint contradicting the Admin boundary)

- **Not a vulnerability — read this before you file it as one.** Two independent adversarial verifiers
  (2026-07-17 /cso) scored it 3/10 and refused it: the exposure is **documented as intentional**
  (`docs/auth.md:37` — "Everything else | any authenticated account — … **Lookups** / RowAudit / profile"),
  it is read-only with no write path, and the data is authorization *taxonomy* (`RoleId`, `RoleName`,
  `PermissionLevel`, `Description`, `UserCount`) — no credentials, no PII. Reading the role list confers no
  capability: every action that could weaponize knowing a `RoleId` stays gated (`AppRolesController.cs:18`,
  `AppUsersController.cs:18`). **This is a tidy-up, not a hole.** Don't let the next audit re-file it.
- **What:** `LookupsController.cs:22` (`=> Ok(await appRoleRepository.GetAllAsync())`) resolves to the same
  `IAppRoleRepository.GetAllAsync()` and the same `AppRole` projection as the Admin-gated
  `AppRolesController.cs:25` (`=> Ok(await repository.GetAllAsync())`) — identical payload, only the injected
  field name differs. But Lookups carries no `[Authorize]`, so it is fallback-only (any authenticated
  account). Delete the action, or add `[Authorize(Roles = "Admin")]` to it for consistency and
  defence-in-depth.
- **Why it's worth the two minutes:** it is **dead code with zero consumers repo-wide** — verified
  2026-07-17: nothing in `src/CMS.NG` or `src/CMS.API.Tests` calls it; the app-user form's role dropdown
  uses `AppRoleService` (`app-role.service.ts:10`), which points at `/api/app-roles`.
  `LookupsControllerTests` has no coverage for it, and `AdminAuthorizationTests.cs:148` enumerates
  `/api/lookups/publish-statuses` as intentionally open while never mentioning it. So it is the one surface
  that returns to a non-Admin exactly what `AdminAuthorizationTests.cs:47` + `:58` pin as **403** for that
  same caller.
- **The contrast that makes it worth closing:** `PublishStatusesController.cs:8-15` justifies its identical
  read/write asymmetry in seven lines — *the course form's FK dropdown needs the list, so gating reads would
  break course editing*, and `AdminAuthorizationTests` pins that read staying open. `lookups/app-roles` has
  **no such justification and no such consumer**. The open read there looks inherited from documenting
  "Lookups" wholesale, not decided.
- **While you're there:** `docs/auth.md` rows 33 and 37 are in tension for the same data — row 33 says
  `AppRolesController` is Admin "all verbs", row 37 says Lookups is any-authenticated. Whichever way this
  goes, make the two rows agree.
- **Effort:** S (human) → S (CC). **Priority:** P3.

## P3 — Three items the 2026-07-17 /cso audit raised that were never decided

Filed because they were surfaced and left unanswered, not because they are urgent. Each is independent.

### a) The published review report overstates a verified-clean claim (`9 ORDER BY` → actually 16)

- **What:** `review/2026-07-17-whole-project-audit.md:75` lists among its verified-clean negatives:
  *"every interpolation splices compile-time constants; all 9 `ORDER BY` clauses are hardcoded"*. **There are
  16, not 9** — counted 2026-07-17 across `src/CMS.API/Repositories/`: AppRole 2, AppUser 3, Auth 2,
  CourseGroup 1, Course 1, FeaturedPromoItem 1, Partner 1, Promotion 1, PublishStatus 2, RowAudit 1,
  TrainingCenter 1.
- **The conclusion is still true.** All 16 were re-checked by the /cso injection specialist and every one is
  hardcoded; there is no SQL injection. Only the **count** is wrong — which implies the original claim was
  reached by sampling roughly half the clauses and stated as if exhaustive.
- **Why bother:** `review/` is tracked deliberately and `JB772/ai-full-stack` is **public**. This is a
  negative result — the kind a reader trusts precisely because it is boring — and its number is wrong. It is
  the same species as `DOC-01`/`DOC-02`/`DOC-03`, which this project has already been bitten by three times:
  a confident false statement in a doc nobody re-checks. **Fix:** change `9` to `16`, or say "every
  `ORDER BY`" and drop the count.
- **Effort:** S (human) → S (CC).

### b) No commit-time secret scanning

- **What:** no `.gitleaks.toml`, no `.secretlintrc`, no `.pre-commit-config.yaml` anywhere in the repo.
- **Why it is worth something despite history being clean:** the /cso audit verified git history is clean of
  credential patterns and that `database/*.sql` is schema-only with **zero `INSERT` statements** — so the
  `SysConfig` signing key and default password were never committable. That is the current state, not a
  guarantee. The one rule that must never break is written in
  `review/2026-07-17-whole-project-audit.md:15` — *"Never commit an actual secret — a connection string with
  a password, a signing key, real user data — that is a different category and it is permanent once pushed"*
  — and on a **public** repo it is enforced today by nothing but attention.
- **Note:** the real credentials live in the `SysConfig` DB table and the DB uses Windows integrated auth, so
  there is genuinely little to leak. This is cheap insurance, not a response to a live problem.
- **Effort:** S (human) → S (CC).

### c) A stored learning contradicts the shipped Admin boundary and is still live

- **What:** the `authenticated-equals-trusted-operator` learning is stored at **confidence 10 / user-stated**
  and asserts *"authentication is the security boundary; roles are NOT… Do not add role gates without
  revisiting docs/auth.md"*. Commit `84a5389` (2026-07-17) **reversed exactly that**, and `docs/auth.md:28`
  now opens with *"Roles are a real boundary, enforced server-side."* The learning is now false, and it
  argues against the boundary the repo actually has.
- **Why it matters:** that reasoning — deciding authorization in the abstract instead of checking what a
  non-Admin sees — is what the audit's own retrospective named as the mistake that produced `SEC-09`. A
  confidence-10 learning telling the next session not to add role gates is that mistake, cached.
- **Mitigation already in place:** a superseding learning
  (`admin-boundary-is-write-side-and-documented-open-reads`, confidence 9) was logged 2026-07-17 and states
  the reversal explicitly. Both load together, so the correction is visible — the stale one is not silently
  winning.
- **⚠ This item is NOT actionable from a fresh clone.** The learnings store is machine-local
  (`~/.gstack/projects/…/learnings.jsonl`), so nothing in the repo can fix it and a new machine will never
  see it. It is recorded here only so the contradiction is not invisible — deliberately **not** given a
  `~/.gstack` path to chase, because `DOC-06` was filed for exactly that. **Decide on the machine that has
  it:** delete the stale entry, or leave it and rely on the superseding one.
- **Effort:** S (human) → S (CC).

## P3 — Generalize the course archive pattern to other entities

- **What:** Extract the Course archive shape — the pure `buildCourseDocDefinition` builder, the
  lazy-loading `CoursePdfService`, and the completeness-guard spec — into a reusable form for other
  detail pages (Partner, CourseGroup, AppUser…).
- **Why:** The 2026-07-16 course-PDF review predicted the "do this for X too" ask arrives
  once admins see the Course archive. A Course-hardcoded builder doesn't generalize.
- **Pros:** Second consumer becomes a copy of a proven pattern (or a shared field-descriptor
  component); the docDefinition scaffolding is written once.
- **Cons:** Speculative until a second requester actually asks; a premature abstraction over
  n=1 is worse than a copy.
- **Carry forward:** the completeness guard only works with **synthetic 6-digit fixture values**.
  With realistic small integers it silently passes against layout literals (`margin: [0, 4, 0, 12]`),
  and with mixed-length values one fixture can be a substring of another (`hour: 731` inside
  `pkid: 90731`). Both holes were live on this branch until 2026-07-16. See the rationale comment in
  `course-pdf.def.spec.ts`.
- **Context:** Deferred by /autoplan (2026-07-16, branch feature-course-pdf). The design decisions
  are recorded in-repo at `spec/course/CoursePDF.md` (the ~/.gstack design doc referenced earlier is
  machine-local and unreachable from a fresh clone).
- **Effort:** M (human) → S (CC). **Priority:** P3.
- **Depends on:** the Course print feature shipping first; a second entity actually needing it.
