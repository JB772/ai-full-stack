# TODOS

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
- **Effort:** M (human) → M (CC). **Priority:** P1. Deferred by decision on 2026-07-16 (report only).

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
- **Swagger exposed in every environment.** `Program.cs:118` calls `app.UseSwagger()` unconditionally, and
  middleware runs before endpoint authorization, so the global `FallbackPolicy` does not cover it — the API
  inventory is anonymously readable wherever this deploys. Fine if it never leaves localhost/LAN (CLAUDE.md
  documents Swagger as the dev entry point); wrap in `IsDevelopment()` otherwise.
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
