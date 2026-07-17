# Whole-Project Review — CMS (ai-full-stack)

**Date:** 2026-07-17 · **Branch:** `worktree-feature-course-pdf` → base `develop` (merge-base `9348a18`)
**Scope:** entire project (302 files, ~24k lines) — requested because previously-built features had never been reviewed.
**Method:** 7 Claude specialists in parallel over the whole codebase + independent OpenAI Codex (`gpt-5.6-sol`,
reasoning=high, read-only) verification. Every finding below was re-verified by the primary reviewer against
the cited code. Nothing is taken on a subagent's word.

> **`review/` is tracked deliberately** (decision, 2026-07-17). `JB772/ai-full-stack` is public, and the
> reasoning is that review history belongs with the code: it survives machine changes, shows up in PRs, and
> reaches collaborators — whereas a gitignored report rots on one laptop, which is the exact defect this audit
> filed as `DOC-06`. Every finding here is `file:line` plus prose, all of it derivable from source that is
> already public; the report contains no credentials, keys, tokens, or exploit code, and that is the bar.
>
> **Two conditions would change that.** (1) Never commit an actual secret — a connection string with a
> password, a signing key, real user data — that is a different category and it is permanent once pushed.
> (2) If this app is ever deployed somewhere publicly reachable, reconsider publishing *unfixed* findings:
> "here is an open hole in a live system" is not the same claim as "here is an open hole on localhost".
> Today it runs against a local SQLEXPRESS with Windows auth and localhost-only CORS.

---

## Findings summary

**53 findings — 15 Critical, 38 Minor. 18 fixed, 30 open, 5 closed by decision.**

| | Critical | Minor | Total |
|---|---|---|---|
| **Fixed** | 11 | 7 | **18** |
| **Closed by decision** (deferred / accepted / by design / no action) | 3 | 2 | **5** |
| **Open** | 1 | 29 | **30** |
| **Total** | **15** | **38** | **53** |

**Still-open Critical: `SEC-06` only** — already folded into the deferred `SEC-01` password migration.

> **`SEC-02` was reversed on 2026-07-17.** It was recorded here as "by design — authenticated == trusted
> operator" and committed to `docs/auth.md` + `CLAUDE.md`. A real report then landed: a newly created account
> with no Admin role could open the 角色 AppRole page. That surfaced `SEC-09` — `/` and `**` both redirected
> *everyone* to `/app-roles`, so the nav hiding was decoration around a page users were sent to by default.
> `SEC-02`, `SEC-03` and `SEC-09` are now fixed and the docs are reversed. The lesson worth keeping: the
> abstract decision ("roles aren't a boundary") did not survive contact with the concrete behaviour, and the
> three pre-existing selective Admin gates were evidence of the real intent all along.

**The headline: the documentation was lying about the thing it most needed to be right about.**
`docs/schema-and-testing.md` stated *"RowAudit is not wired up… none of these exist in the code."* In reality
`RowAuditWriter` exists, 7 repositories inject it, 12 pages carry the badge, and CLAUDE.md lists auditing as a
hard rule. A future session scaffolding a table would have read that and correctly skipped audit. **Fixed.**

**Cross-model value:** Codex verified 3 of my 4 top claims, **corrected 1** (the admin-lockout is recoverable —
issued JWTs stay valid 24h and are never re-checked against `IsActive`), and found 4 issues the Claude
specialists missed entirely (`SEC-04`, `SEC-05`, `SEC-06`, `SEC-07`).

**Biggest remaining risk:** `SEC-01` (unsalted SHA-256 password hashing) — deferred to a P1 TODO by decision.
**Cheapest remaining fix:** `API-01` (409 message names zero-count children) or `SEC-04` (gate Swagger).

**Verified clean** (negative results worth trusting): the `nchar` RTRIM hard rule is fully satisfied
(`Certification.Title` is the schema's only `nchar` column and no repository selects it); no SQL injection
(every interpolation splices compile-time constants; all 9 `ORDER BY` clauses are hardcoded); no N+1;
`CmsApiFactory` swaps 11/11 repositories — no live-DB-write risk; no committed secrets; `PasswordHash` never
leaks; no IDOR; no XSS; no memory leaks; no per-controller `try/catch`.

---

## Issue table

Severity: **Critical** = correctness/security/data-loss · **Minor** = quality, consistency, coverage.
Conf = confidence /10 (10 = demonstrated; 7-8 = verified pattern match).

| ID | Finding | Priority | Severity | Conf | Area | Fix status | Fix notes |
|---|---|---|---|---|---|---|---|
| SEC-01 | `PasswordHasher.cs:14` — passwords hashed with one unsalted round of SHA-256; compared inside a SQL `WHERE` | P1 | Critical | 10 | Security | **Deferred** | Decision 2026-07-17: report only. P1 TODO with opportunistic re-hash-on-login migration. Forces comparison out of SQL into constant-time app code. |
| SEC-02 | `Program.cs:111` — `FallbackPolicy` requires only an authenticated user; `[Authorize(Roles)]` on just 3 endpoints, so any account can call user/role/content mutations | P1 | Critical | 9 | Security | **✅ Fixed** | **Decision reversed 2026-07-17** (was "by design") after a real report: a new non-Admin could open 角色 AppRole. Class-level `[Authorize(Roles="Admin")]` on AppRoles + AppUsers; PublishStatus **writes only** (its list feeds course-form's FK dropdown). Scope taken from the app's own「系統管理 Admin」nav group. +24 endpoint tests; negative-controlled. Docs reversed. |
| SEC-03 | `AppUserRequest.IsActive` writable via ungated `PUT /api/app-users`; login requires `IsActive=1`, so any operator can deactivate any Admin | P1 | Critical | 9 | Security | **✅ Fixed** | Closed by SEC-02's class-level gate. Pinned by a test asserting a non-Admin gets 403 and the Admin stays `IsActive`. |
| SEC-09 | `app.routes.ts` — `/` **and** `**` both redirected to `app-roles`, so every account landed on the Admin page on login (`login.ts:37` → `navigateByUrl('/')`); no admin route guard existed anywhere | P1 | Critical | 10 | Frontend | **✅ Fixed** | The actual reported symptom, and the reason nav hiding never helped — users were *sent* to the page the sidebar hid. Landing → `featured-promo-items` (the 首頁 Home item); new `adminGuard` as a pathless `canActivateChild` over the admin subtree, so a 13th admin route can't miss it. +4 guard specs. |
| SEC-04 | `Program.cs:118` — `app.UseSwagger()` unconditional; middleware runs before endpoint authz, so API inventory is anonymously readable wherever deployed | P2 | Minor | 9 | Security | **Open** | *(Codex)* Wrap in `IsDevelopment()`. Moot if localhost/LAN-only — CLAUDE.md documents Swagger as the dev entry point. |
| SEC-05 | Deactivation / password change / role removal do **not** revoke live JWTs (24h, roles baked in at `JwtTokenService.cs:31`, no re-check) | P2 | Minor | 8 | Security | **Open** | *(Codex)* Shorten lifetime + refresh token, or re-check a security stamp in `OnTokenValidated`. |
| SEC-06 | Every account created **or** reset gets the same default password (`AppUserRepository.cs:79`, `AuthRepository.cs:112`); login never reads `PasswordUpdatedTime`, so no forced first change | P1 | Critical | 9 | Security | **Open** | *(Codex)* Compounds SEC-01 — unsalted means all such accounts share one identical hash. Folded into the SEC-01 P1 TODO. |
| SEC-07 | No rate limiting, throttling, or account lockout on login (`AddRateLimiter` → no hits) | P2 | Minor | 9 | Security | **Open** | *(Codex)* Compounds SEC-01: the online guessing path is unthrottled too. |
| SEC-08 | `JwtSigningKeyProvider` caches the signing key for process lifetime (no rotation, no length validation); issuer/audience validation disabled | P3 | Minor | 7 | Security | **Open** | Matters only if the symmetric key is shared with another service. Validate ≥32 bytes at load so a weak key fails fast. |
| DATA-01 | `CourseGroupsController.cs` — delete guard read on one connection, `DeleteAsync` deletes on another without re-check; `FK_Course_CourseGroup` is `ON DELETE CASCADE` so SQL Server does **not** reject → silent destruction | P1 | Critical | 9 | Data | **✅ Fixed** | New `DeleteResult` enum (mirrors existing `MoveResult`); repo re-checks inside its transaction; controller maps `Blocked`→409. Counts were already loaded and discarded. |
| DATA-02 | `CoursesController.cs` — same TOCTOU against `FK_CourseInCertification_Course` + `FK_CourseJobCategories_Course` (both `ON DELETE CASCADE`) | P1 | Critical | 9 | Data | **✅ Fixed** | Same fix. 2 new tests; negative-controlled. |
| DATA-03 | `AppUsersController.cs:42` / `FeaturedPromoItemsController.cs:41` — check-then-write on separate connections | P3 | Minor | 7 | API | **Open** | Data integrity is safe (real unique constraints); only the HTTP status degrades 409→500. Catch SqlException 2601/2627 and map. |
| DATA-04 | `FeaturedPromoItemRepository.MoveAsync` reads the slot outside its transaction | P3 | Minor | 6 | API | **Open** | Move `BeginTransaction()` above the SELECT; add `UPDLOCK, HOLDLOCK`. |
| AUD-01 | `AuthRepository.cs:14` — the **only** write-bearing repository without `RowAuditWriter` (8 repos write, 7 audit). Its 3 `AppUser` UPDATEs run with no transaction and no audit row | P2 | Critical | 9 | Audit | **✅ Fixed** | Injected `RowAuditWriter`; all 3 writes now run in a transaction with before/after reload + `LogUpdateAsync` on the same conn+tx. Audits the `AppUser` projection (**no** `PasswordHash` property) so a password change logs ActionDesc `PasswordUpdatedTime` and the hash can never reach `dbo.RowAudit`. **8 new tests against the REAL repository via in-memory SQLite** (`AuthRepositoryAuditTests`), incl. a rollback test; negative-controlled. `docs/row-audit.md` updated. |
| AUD-02 | `FeaturedPromoItemRepository.MoveAsync:198` — 3 slot UPDATEs, no audit | P3 | Minor | 9 | Audit | **By design** | Documented exception in `docs/row-audit.md` ("a two-row positional swap"). Confirmed still intended. |
| AUD-03 | `AppUserRepository.AssignRoleAsync:219` — `INSERT…SELECT` can affect 0 rows, then `row!` is null-forgiven into `LogInsertAsync` → `ArgumentNullException` (500) instead of 404 | P3 | Minor | 6 | API | **Open** | Use `ExecuteScalarAsync<int?>` and return early when null. |
| API-01 | `PublishStatusesController.cs:94` — 409 message names children with zero rows (`仍有 3 筆課程、0 筆促銷活動`) | P2 | Minor | 9 | API | **Open** | Against `docs/delete-guards.md`. Add `DescribeBlockers` mirroring `PartnersController`. |
| API-02 | `AppRolesController.cs:86` / `AppUsersController.cs:87` — second round trip for a guard count the loaded entity already carries | P3 | Minor | 8 | API | **Open** | Against `docs/delete-guards.md` ("no second round trip"); also widens the TOCTOU window. |
| API-03 | `UpdateProfileRequest.UserName` has no `[StringLength(200)]` though `AppUser.UserName` is `nvarchar(200)`; >200 chars → truncation → generic 500 where siblings return 400 | P2 | Minor | 7 | API | **Open** | Add `[Required]` + `[StringLength(200)]` to match `AppUserRequest`. |
| API-04 | `AuthController.cs:73` — returns 404 but declares only 200/400/401 in `ProducesResponseType` | P3 | Minor | 8 | API | **Open** | Add `[ProducesResponseType(404)]`. Swagger contract omits a status it emits. |
| API-05 | `RowAuditController.cs:12` — route `api/rowaudit`, not kebab-case; the lone violator (9 siblings comply) | P3 | Minor | 7 | API | **Open** | Rename to `api/row-audit` + update the Angular service, or document the opt-out. |
| API-06 | `LookupsController` — 4 of 6 endpoints return full entities (incl. delete-guard counts), 2 return slim models | P3 | Minor | 7 | API | **Open** | Two contradictory precedents in one file. Pick one or document why. |
| API-07 | `AuthController.cs:23` — the uniform-401 guard is unreachable; `[ApiController]` implicit-required returns 400 first | P3 | Minor | 6 | API | **Open** | Make `LoginRequest` fields nullable so the 401 runs, or drop the guard and document 400. |
| FE-01 | `app-user-detail.html:49` + `app-user-list.html:62` — append `+ 'Z'` before `DatePipe`, but `PasswordUpdatedTime` is written with `SYSDATETIME()` (**local**) → renders **+8h in the future** for UTC+8 | P2 | Critical | 9 | Frontend | **✅ Fixed** | Deleted `+ 'Z'` from both (now zero in the repo), matching `row-audit-badge.html:13`. +2 regression tests. Negative-controlled: restoring the bug rendered `密碼更新時間 2026-07-16 22:30` for a 14:30 input. Caveat in the test doc: only bites on a non-UTC runner (UTC+8 here). |
| FE-02 | `featured-promo-item-list.html` — every action is a bare `<a>` with no `href`/`role`/`tabindex`: the whole scheduler is keyboard-unreachable and unnamed to screen readers | P2 | Minor | 8 | Frontend | **Open** | The lone outlier; six sibling features use `p-button` + `[attr.aria-label]`. Use `<button type="button">`. |
| FE-03 | `featured-promo-item-list.html:46,61,65` — `<label>`s with no `for` and not wrapping their control | P3 | Minor | 8 | Frontend | **Open** | Give each control an `id` derived from the day+slot key. |
| FE-04 | `auth.interceptor.ts:39` — toasts 5xx **and** re-throws into component handlers that toast again; every 500 stacks two toasts across all 9 features | P3 | Minor | 7 | Frontend | **Open** | Not a convention breach (nothing swallows). Gate component toasts on `err.status < 500`. |
| FE-05 | `courses/date.util.ts` vs `featured-promo-items/week.util.ts` — `toIsoDate`/`fromIsoDate` duplicated, both carrying the same load-bearing "Never `toISOString()`" warning | P3 | Minor | 9 | Frontend | **Open** | Two copies of a rule documented as easy to get wrong. Extract to a shared module. |
| FE-06 | `app-user-detail.ts:92` — `reload()` subscribes with no `error` callback; the only such subscribe in the repo | P3 | Minor | 6 | Frontend | **Open** | A 4xx fails silently leaving a stale record on screen. Add an `error` arm. |
| FE-07 | `featured-promo-item-list.ts:163` — `itemAt()` does a linear `find()` per cell × 21 cells per CD tick | P3 | Minor | 6 | Frontend | **Open** | Bounded (≤441 comparisons) — cleanliness, not a live problem. Precompute a `computed` Map index. |
| FE-08 | PrimeNG `ConfirmDialog` renders `message` via `[innerHTML]`; 6 handlers interpolate DB strings into it | P3 | Minor | 6 | Frontend | **No action** | **Not XSS** — Angular's default sanitizer strips scripts (verified in PrimeNG 20.4.0 source). Cosmetic markup injection only. Recorded as a latent sink. |
| FE-09 | `course-pdf.service.ts` — `pdfmake`'s `download()` is callback-based and returns `undefined`, so the awaited promise resolves before the PDF exists; the spinner is decorative once warm and pdfkit throws escape as unhandled rejections | P3 | Critical | 9 | Frontend | **Accepted** | Decision 2026-07-16: ship as-is. Font-load failures *do* toast. Test renamed + comment added so the scope isn't widened back out. |
| FE-10 | `public/fonts/*.otf` — 11.5 MB (77% of the production build) at an **unfingerprinted** URL (`outputHashing` doesn't touch `public/`) | P2 | Critical | 8 | Frontend | **Deferred** | Subsetting investigated and **rejected** — see TODOS P2. Big5 subset drops 2,315 CJK chars incl. 喆/堃 (Taiwanese name chars). No free lunch. `fonts/v1/` path fixes cache-busting independently. |
| FE-11 | `course-pdf.def.ts:33` — `coursePdfFilename` interpolates unsanitized `course.title` (`nvarchar(200)`) into the download name | P3 | Minor | 7 | Frontend | **Open** | Not exploitable (browsers sanitize the `download` attribute) but breaks on `/ \ :` and can exceed 255 chars. |
| FE-12 | `course-pdf.def.ts` — no page-number footer on a multi-page archive; `產生於` appears only on page 1 | P3 | Minor | 8 | Frontend | **Open** | Eight `nvarchar(max)` bodies routinely paginate. A filed archive can lose a page with no trace. |
| FE-13 | `course-detail.ts:93` — bindingless `catch {}` discarded the error; a font 404, CORS failure and builder bug all rendered as the same toast | P2 | Minor | 9 | Frontend | **✅ Fixed** | Added `console.error('PDF generation failed', error)`. Verified firing in the failure test. |
| FE-14 | Leftover `@media print` from the rejected print-dialog design rendered `允許重聽` as **是 是** (p-tag not hidden alongside its `.print-only` twin) | P3 | Minor | 8 | Frontend | **✅ Fixed** | Added `p-tag { display: none }` to the print block. Kept as a documented Ctrl+P courtesy per `spec/course/CoursePDF.md:36`. |
| FE-15 | `course-pdf.def.ts` — `產生於` line rendered a dangling `·` with no attribution when the session profile is gone (`userName` → `''`) | P3 | Minor | 8 | Frontend | **✅ Fixed** | Extracted `formatProvenance()`; drops the separator when empty. +2 tests. |
| TEST-01 | `course-pdf.def.spec.ts` + `course-detail.spec.ts` — the completeness guard (sold by `spec/course/CoursePDF.md:105` as the safety net) was **vacuous for all 11 numeric fields**: `toContain(String(value))` matched layout literals (`margin: [0,4,0,12]`) | P1 | Critical | 10 | Tests | **✅ Fixed** | Proved by deleting `時數` from the builder — suite stayed green. Fixed with uniform 6-digit synthetic fixtures. Negative-controlled across all 11 fields. My first fix (`hour: 731`) was itself broken — a substring of `pkid: 90731`. |
| TEST-02 | `CoursePdfService` has no spec — the only file with real runtime logic (engine promise-cache, cache-drop-on-failure, CommonJS interop, base64) is untested; the pure builder has 138 spec lines | P2 | Minor | 9 | Tests | **Open** | Fetch is stubbable without loading 11 MB. |
| TEST-03 | `course-pdf.def.spec.ts:147` — global `toContain('—')` passes if 9 of 10 nulled fields lose their placeholder | P3 | Minor | 8 | Tests | **Open** | Same substring-incidence family as TEST-01. Count occurrences or assert per field. |
| TEST-04 | `week.util.ts:32` — `mondayOf()`'s Sunday branch (`dow === 0 ? -6`), the off-by-one the comment exists for, is never exercised; no spec file | P3 | Minor | 8 | Tests | **Open** | Both indirect tests pin Mondays. |
| TEST-05 | `RowAuditService` has no spec — unlike every other feature service; the `tableName`/`pkid` query contract the whole audit read path depends on is unasserted | P3 | Minor | 7 | Tests | **Open** | Both consumers stub it, so a wrong param name ships green. |
| TEST-06 | `CmsApiFactory` — 11/11 correctly swapped, but nothing **enforces** it; CLAUDE.md's "miss one and tests hit real SQL Server" rests on reviewer diligence | P2 | Minor | 7 | Tests | **Open** | Add a reflection guard test asserting every `I*Repository` resolves to an `InMemory*` fake. |
| TEST-07 | `course-detail.spec.ts` — `setup()` seeded `sessionStorage` with no `afterEach` cleanup, leaking the auth profile into later specs | P3 | Minor | 8 | Tests | **✅ Fixed** | Added `afterEach(() => sessionStorage.clear())`, matching `course-list.spec.ts`. |
| TEST-08 | `course-detail.spec.ts` — `'is disabled until the course loads'` actually stubbed a **failure**, not an in-flight load | P3 | Minor | 6 | Tests | **✅ Fixed** | Renamed to `'is disabled when the course fails to load'`. |
| DOC-01 | `docs/schema-and-testing.md:32` — **"RowAudit is not wired up… none of these exist in the code"**. False: `RowAuditWriter` exists, 7 repos inject it, 12 pages carry the badge, CLAUDE.md calls it a hard rule | P1 | Critical | 10 | Docs | **✅ Fixed** | The most dangerous finding of the audit: it tells a future session to skip audit on new tables. |
| DOC-02 | `CLAUDE.md:26` — "Node is **not on PATH** — prepend it". False: `node v24.18.0` resolves in a plain shell | P2 | Critical | 10 | Docs | **✅ Fixed** | The first instruction every session reads; prescribed a pointless ritual all session. |
| DOC-03 | `docs/environment.md:6` — repeats the same stale Node claim as its lead section | P2 | Critical | 10 | Docs | **✅ Fixed** | Rewritten + explained the stale-session-env root cause (which also hid `codex`). |
| DOC-04 | `docs/reference-features.md:37` vs `docs/ui-patterns.md` — sticky-toolbar documented twice and already drifted; code agrees with `ui-patterns.md` | P3 | Minor | 9 | Docs | **Open** | The concrete cost of documenting one pattern in two places. |
| DOC-05 | `docs/ui-patterns.md:5` — self-describes as "the two patterns that live nowhere else"; all three of its sections are also in `reference-features.md` | P3 | Minor | 7 | Docs | **Open** | The understatement is what let DOC-04 drift unnoticed. |
| DOC-06 | `TODOS.md` P3 pointed at a machine-local `~/.gstack/...` design doc, unreachable from a fresh clone | P3 | Minor | 8 | Docs | **✅ Fixed** | Now points at the in-repo `spec/course/CoursePDF.md`. |
| DOC-07 | `docs/auth.md` never documented the authorization posture; `grep -i audit docs/auth.md` → 0 hits despite 3 unaudited `AppUser` writes | P2 | Minor | 8 | Docs | **✅ Fixed** | Added § "Authenticated == trusted operator" + a CLAUDE.md hard-rule line, with the AUD-01 gap noted. |

---

## Fixes applied this session

**Backend — 260 xUnit passing** (258 + 2 new) · **Frontend — 338 Karma passing** · production build clean.

| Area | Change |
|---|---|
| `DATA-01/02` | New `src/CMS.API/Models/DeleteResult.cs` (`NotFound`/`Blocked`/`Deleted`) mirroring the existing `MoveResult` convention; `CourseGroupRepository` + `CourseRepository` re-check counts inside their delete transaction; both controllers map `Blocked`→409; both in-memory fakes updated to mirror the contract; 2 race-simulating tests via a new `OnBeforeDeleteGuard` seam. |
| `TEST-01` | Uniform 6-digit synthetic fixtures + rationale comment; negative-controlled across all 11 numeric fields. |
| `FE-13/14/15` | `console.error` on PDF failure; `p-tag { display: none }` in print; `formatProvenance()` guard. |
| `TEST-07/08` | `afterEach` sessionStorage cleanup; misleading test renamed. |
| `DOC-01/02/03/06/07` | The three doc lies corrected; `TODOS.md` pointer fixed; auth posture documented. |

### Test-coverage reality — which fixes are actually proven

`CmsApiFactory` swaps **every** repository for an in-memory fake, so **no endpoint test can execute real
Dapper code**. Proved by negative control: neutralising the *real* delete guards left all 260 tests green;
neutralising the *fake's* guard, or the controller's `Blocked` arm, turned exactly one test red.

- **`DATA-01/02` (delete guards)** — endpoint tests cover (a) the fake mirrors the real contract and (b) the
  controller maps `Blocked`→409. The real SQL guard is verified **by reading**. Both fakes carry a comment to
  keep them in step. *Could* be promoted to a real SQLite test as below; not done.
- **`AUD-01` (auth audit)** — covered **for real**. `AuthRepositoryAuditTests` drives the actual
  `AuthRepository` against in-memory SQLite (the `PublishStatusRepositoryAuditTests` pattern), so removing the
  audit call turns tests red. The only concession: `SYSDATETIME()` is defined as a SQLite function in the test
  factory, since SQLite has no equivalent. All other SQL runs unmodified.

The SQLite route exists and works — the "real repositories are untestable here" framing was too pessimistic.
It is available to any repository whose SQL is portable.

---

## Open decisions

1. **`SEC-06`** (the last open Critical) — shared default password + no forced first-login change; already
   folded into the deferred `SEC-01` P1 migration, so this is really "when do you do `SEC-01`".
2. **`SEC-04`** — gate Swagger, or confirm localhost/LAN-only.
3. **`DATA-01/02` coverage** — now that `AuthRepositoryAuditTests` proves the SQLite route works, the delete
   guards could be promoted from "verified by reading" to real tests the same way.
4. **`TODOS.md` detail level** — same publishing question as this file, resolved the same way: the entries are
   engineering content, not secrets, and they stay as written.

## Status

**DONE_WITH_CONCERNS.** The audit found materially more than the branch diff did, confirming the instinct that
earlier features had gone unreviewed. One Critical remains open — `SEC-06` — and it is already folded into the
deferred `SEC-01` password migration, so every remaining Critical sits behind a recorded decision rather than
an oversight. The 29 open Minors are quality/consistency/coverage work.
