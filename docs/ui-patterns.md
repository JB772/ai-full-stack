# Frontend UI patterns

Cross-cutting Angular/PrimeNG gotchas for building a page. Read the entry for the widget you're adding.
Inline cell editing and overlay-editor blur are documented in depth in the **Course** row of
`reference-features.md`; this file is the quick index plus the two patterns that live nowhere else.

## Sticky action toolbar

The viewport is the scroll container — **there is no fixed app header**. `.layout` is a flex row
(sidebar + `.content`) with `min-height: 100vh`; the document itself scrolls. So a `.page-toolbar` can be
pinned with plain `position: sticky; top: 0` (+ a `z-index` above the fields) — no scroll listener, no
fixed positioning. It sticks to the viewport top within the content region without covering the sidebar
(a separate flex column) or a header (there is none), and the toolbar's opaque `--p-content-background`
keeps scrolling fields from showing through.

Both `course-form` (New and Edit — one component serves both) and `course-detail` (檢視課程) do this via a
`sticky-toolbar` class on their `.page-toolbar`; the class name is **per-component** (defined in each
component's `.scss`), not a shared style. When adding a list/detail/form triad, apply it to the detail and
form toolbars too. Assert it in a headless Karma run with `getComputedStyle(el).position === 'sticky'`.

## QR codes

Use the framework-agnostic **`qrcode`** package, **not `angularx-qrcode`** — it has no Angular peer dep
and returns a PNG data URL, which doubles as the `<img [src]>` and the download payload (build an anchor
with `href=dataUrl`, `download={name}.png`, `.click()`). See the QR block in `course-detail` (title +
image + download button in 基本資料). In tests, `qrcode.toDataURL` is overloaded — stub it with
`spyOn(QRCode, 'toDataURL') as unknown as jasmine.Spy`.

## PDF export (存成 PDF)

One-click PDF download uses **`pdfmake`** with a vendored **Noto Sans TC** font (browsers have no
silent save-as-PDF API, and PDF generators can't render 繁體中文 without an embedded font). The
reference is `course-detail`'s 存成 PDF button:

- `course-pdf.def.ts` — a **pure** docDefinition builder (no pdfmake import, no DOM), so
  completeness/content specs run without the engine. Filename: `{courseId} {title} 課程資料 {yyyyMMdd}.pdf`.
- `course-pdf.service.ts` — lazy-loads `pdfmake/build/pdfmake` + the OTFs from `public/fonts/`
  (~13 MB total) on first use and caches the promise per session; the main bundle pays nothing.
  `pdfmake/build/pdfmake` is in `allowedCommonJsDependencies` (angular.json).
- Component: `savingPdf` signal drives `[loading]`; failure → error toast. In specs, stub the
  service (`jasmine.createSpyObj('CoursePdfService', ['download'])`) — never load the real engine.
- A completeness spec iterates the model's fields against `JSON.stringify(docDefinition)` so a new
  column can't silently vanish from the archive (`course-pdf.def.spec.ts`).
- `@media print` rules (in `app.scss` + the component's scss) remain as a Ctrl+P courtesy only —
  they are not the export path.

## In-place (inline) list-cell editing

Hand-rolled, **not `pEditableColumn`.** The `course-list` cell editor is a component-managed edit-state
(`editing` signal) driven by `(dblclick)`, because `pEditableColumn` opens on **single** click and the
requirement was double-click only. Don't reach for `pEditableColumn` / `p-cellEditor`. Full pattern:
Course row in `reference-features.md`.

## Overlay editors must not commit on blur

`p-select` and `p-datepicker` panels are `appendTo="body"`, so a blur-to-save fires the instant you click
an option / a date — tearing the editor down before the pick lands, so the control looks like it "won't
edit". The `p-select` commits on `(onChange)` (+ `(onHide)` to close on click-away); the `p-datepicker` on
`(onSelect)` + `(onClose)`. Only plain text/number inputs are safe to commit on `(blur)`. Full pattern:
Course row in `reference-features.md`.
