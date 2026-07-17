# Reading the schema & test/tooling traps

## Reading the schema

- **Don't invent constraints the schema doesn't have.** `Partner.AppKey` reads like a natural key, but
  there is no UNIQUE index on it — only `PK_Partner` on `pkid` — so the API adds **no** duplicate-AppKey
  409, and `AppKey` is freely editable. Contrast `AppRole.RoleId`, which really is a clustered PK and
  therefore really does get a 409 and an immutable field. Read the constraints before assuming.
- **A few tables are scripted into more than one `.sql` file.** `PublishStatus` appears identically in
  `admin.sql`, `course.sql`, and `promotion.sql`; `Partner` and `PartnerCourseGroup` appear in both
  `course.sql` and `promotion.sql`. Same table, one deployed copy — diff them before assuming which file
  is authoritative. The FK constraints may be declared in only *one* of the copies, so grep every file
  when hunting for a table's children.
- **`nchar(n)` columns need `RTRIM()`** in every SELECT (see the convention).

## Test & tooling traps

- **New repositories must be swapped in `CmsApiFactory`.** It removes each `I{Table}Repository` and
  registers an in-memory fake. Miss one and the test host resolves the real Dapper repository, and those
  tests will try to reach SQL Server.
- **`p-table` sorts its bound array *in place*.** If a list component's default sort is anything other
  than the order the mock data is already in, the table silently reorders the very array the spec holds a
  reference to — and `items[1]` stops being the row you wrote down. This bites any list not sorted by
  `pkid` (e.g. `CourseGroupList`, which defaults to `description`). In component specs return a fresh copy
  per call (`service.query.and.callFake(() => of([{ ...a }, { ...b }]))`) and assert against named consts,
  never `items[i]` or `tbody tr[i]` — locate rows by their text instead.
- **Don't assert an exact Chinese sort order.** SQL Server orders `nvarchar` by the database collation;
  an in-memory fake orders by whatever comparer you gave it (and the browser by its own locale). None of
  the three agree — e.g. code-point order puts 資料庫 before 資訊安全, and the DB may not. An exact-sequence
  assertion tests the fake, not the API. Assert the property that matters (the sort key is `Description`,
  not `pkid`) and pin the fake's order only with a comment saying why it may differ in production.
- **RowAudit IS wired up** (this section previously claimed the opposite — corrected 2026-07-16).
  `RowAuditWriter` (`src/CMS.API/Auditing/RowAuditWriter.cs`) exists and is injected by every write-bearing
  repository except `AuthRepository`; `RowAuditBadgeComponent` is on all 12 detail/form pages. Scaffolding a
  new table **must** log audit rows on the same connection + transaction as the write → `docs/row-audit.md`.
  (`AuditHelper` is still fiction — it appears only in the aspirational `sample1/2.spec.md` and the `/crud`
  skill. Follow `RowAuditWriter`.)
- **The `/crud` skill's file layout is wrong for this repo.** It says `core/models/` and `core/services/`;
  there is no `core/` directory. Models and services live in `features/{table-plural}/`, next to the
  components. Follow the existing code, not the skill text.
- **PrimeNG major version tracks Angular's.** Angular 20 → PrimeNG 20. Installing `primeng@latest` pulls
  v21 and fails peer resolution.
- **UTF-8 through the shell.** Chinese characters in a JSON body passed inline to `curl` via Bash get
  mangled and the API rejects the request. Write the payload to a file and use `--data-binary @file`, or
  use Swagger UI.
