# Feature Spec for Course 存成 PDF (course archive export)

- parent feature: `spec/course/Course.md` (the 檢視 page this button lives on)
- pattern reference: **PDF export** in `docs/ui-patterns.md`; entry in `docs/reference-features.md` (Course row)

> **Not a table spec.** This is a frontend-only feature on the existing `Course` detail page —
> no schema, no API change, no new endpoint. It ships the deferred **列印PDF** item from
> `spec/sample1.spec.md`.

---

## Summary

An internal admin archives a dated snapshot of a course's current state (before editing, or for
filing). One click on the 檢視 page downloads a real `.pdf` — complete, timestamped, with the
generating admin's name. Fidelity is low-stakes; **completeness + provenance** are the bar. It is
not a branded handout and not a stored/compliance record.

| Item | Detail |
|------|--------|
| Trigger | 存成 PDF button (`pi pi-file-pdf`) in the 檢視課程 toolbar, next to 返回/編輯 |
| Output | Direct download — **no print dialog**, no navigation, admin stays on the page |
| Filename | `{CourseId} {Title} 課程資料 {yyyyMMdd}.pdf` |
| Data source | The already-loaded `course()` signal (`CourseService.getByPkid`) — no extra request |
| Generator | `pdfmake` (client-side), lazy-loaded on first click |
| CJK font | Vendored **Noto Sans TC** OTF (Regular + Bold, OFL) in `public/fonts/` — PDF generators cannot render 繁體中文 without an embedded font |
| Backend impact | **None** |
| Auth | Inherits the detail page's guard; the 產生於 stamp reads `AuthService.userName()` |

### History (why this shape)

A `@media print` + browser-dialog variant was built first and rejected on first use — the job is
"save a file", and routing through the print dialog read as "print", not "save". The pivot to
pdfmake accepts the two costs that design had avoided: a dependency + ~11 MB font (mitigated:
lazy-loaded, session-cached, main bundle unaffected) and a second layout (mitigated: the
completeness spec below). The `@media print` CSS **remains** in `app.scss` / `course-detail.scss`
as a Ctrl+P courtesy only — it is not the export path.

---

## Localization

- Button: 存成 PDF
- Provenance line: 產生於 {yyyy-MM-dd HH:mm} · {userName}
- Error toast: PDF 產生失敗 / 無法產生課程資料 PDF。
- Document section headings: 基本資料 / 課程內容 / 關聯資料 (mirroring the 檢視 page)
- Field labels: identical to the 檢視 page (see `spec/course/Course.md` → Chinese Column Names)

---

## Document layout

Single A4 document, `NotoSansTC` 10.5pt, 1.5 cm margins, fields in 檢視-page order.

1. **Identity header** — a filed PDF must say what it is first: `Title` (16pt bold), then
   `{CourseId} · {ProdCourseId}`, then the 產生於 provenance line.
2. **基本資料** — borderless label/value table (label column 110pt, bold gray labels). Rows never
   split across pages (`dontBreakRows`). FK values show **nav labels** (原廠 = `partner.name`,
   課程群組 = `courseGroup.description`, 上架狀態 = `publishStatus.description`), never raw ids.
3. **課程內容** — the eight long `nvarchar` bodies, each as **one text node** (bold label +
   newline + body) so a label can never separate from the start of its value, while an
   arbitrarily long body still breaks across pages (expected).
4. **關聯資料** — the six child-row counts (point-in-time relational metadata is archival value).

Rules carried from the design review:

- **Null fields render as labeled `—` rows, never dropped** — an archive reader must see the
  field existed and was empty (matches the 檢視 detail-grid convention).
- **允許重聽 renders as plain 是/否 text** (no severity-colored widget look-alikes on paper).
- **The 引用/無法刪除 usage note is excluded** — it is an editing affordance, not course data.
- The QR code is **not** embedded (default pending the requester's answer; see Out of scope).

---

## Implementation map

| File | Role |
|------|------|
| `features/courses/course-pdf.def.ts` | **Pure** docDefinition builder + `coursePdfFilename` / stamp formatters. No pdfmake import, no DOM — fully unit-testable. |
| `features/courses/course-pdf.service.ts` | Lazy-loads `pdfmake/build/pdfmake` + both OTFs (fetch → base64 → `addVirtualFileSystem` / `setFonts`), caches the **promise** per session (drops it on failure so a transient fetch error doesn't poison the session), `createPdf(def).download(filename)`. |
| `course-detail.ts` / `.html` | `savePdf()` + `savingPdf` signal → button `[loading]`; guard on `!course()` and re-entry; failure → PDF 產生失敗 toast. |
| `public/fonts/NotoSansTC-{Regular,Bold}.otf` | Vendored font (OFL). Served as static assets, fetched once per session. |
| `angular.json` | `allowedCommonJsDependencies: ["pdfmake/build/pdfmake"]`. |

Timestamp formats (local components, never `toISOString` — the repo's UTC+8 rule):
`formatFileStamp` → `yyyyMMdd`; `formatGeneratedAt` → `yyyy-MM-dd HH:mm`.

---

## Error handling

- Font fetch / engine failure → the shared error path: `PDF 產生失敗` toast; `savingPdf` resets;
  the cached engine promise is dropped so the next click retries the load.
- No course loaded → button disabled; `savePdf()` also no-ops defensively.
- No backend codepath exists to fail.

---

## Tests

**`course-pdf.def.spec.ts`** (builder — runs without pdfmake):

- Filename format + zero-padding.
- 產生於 stamp format.
- **Completeness guard (F3):** iterates every `Course` model field and asserts it appears in
  `JSON.stringify(docDefinition)` — a new column added to the model but not the archive breaks
  the build. Nav-object/pkid/boolean fields are asserted via their rendered forms (labels, 是/否).
- Identity-header order; null fields → labeled `—` rows; 否 rendering; counts in / 無法刪除 out;
  `defaultStyle.font === 'NotoSansTC'`.

**`course-detail.spec.ts`** (component):

- Stub `CoursePdfService` (`jasmine.createSpyObj(..., ['download'])`) — **never** load the real
  engine or font in Karma.
- Button present in toolbar; disabled until the course loads.
- Click → `download(course, any Date, userName)` (userName from the seeded `auth-profile`).
- Second click → second download (no reload needed).
- Rejection → PDF 產生失敗 toast + `savingPdf` back to false.
- Load-error path asserts `download` never fires.
- Screen-view completeness test remains (the PDF twin lives in the builder spec).

---

## Out of scope

- **QR code embed** on the document — default off pending the requester's answer.
- **Server-side / stored immutable snapshot** (QuestPDF, `GET /api/courses/{id}/pdf`) — the
  documented upgrade path if "archive" ever becomes "compliance record". Revisit only on that
  trigger.
- **Print/export for other entities** — deferred until a second consumer asks (`TODOS.md`).
- **The print dialog flow** — superseded; the remaining `@media print` CSS is a courtesy, not a
  feature surface.
