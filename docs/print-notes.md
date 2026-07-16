# Print / PDF notes

How the 課程簡章 (`/courses/:pkid/brochure`) produces a PDF, and the non-obvious constraints
behind it. Read before building any print route or touching `course-brochure`.

The browser is the PDF renderer — no jsPDF, no QuestPDF, no html2canvas. HTML/CSS is the layout
language and Chromium's Skia backend embeds the fonts. Everything below is a consequence of that
choice, and each item was verified on the output rather than reasoned about.

## Rendering without the app shell

A route sets `data: { bare: true }` to render with no sidebar, header, or PrimeNG theme. The root
component (`app.ts`) walks to the deepest activated route on each `NavigationEnd` and reads it,
falling through to the same lone-outlet branch used for `/login`.

Route nesting cannot express this — the shell lives above the outlet in the root component, so it
renders regardless of tree position. **`bare` removes chrome, never the guard**: a bare route is
still a child of the guarded parent and still needs a JWT.

## The four things that each ruin a print page on their own

| Symptom | Cause | Fix |
|---|---|---|
| A blank page 2 on every PDF | UA default `body { margin: 8px }` overflows the page box (`1122.52px + 16px`). `@page { margin: 0 }` zeroes the *page* margin, not the body's. | `html, body { margin: 0; padding: 0 }` (already in `styles.scss`) |
| Every tint, rule and ink block vanishes | Chrome's print dialog defaults **Background graphics OFF** | `print-color-adjust: exact` on the sheet **and** tick the box in the dialog |
| The URL, date and a page number are stamped on the sheet | Dialog defaults **Headers and footers ON** | Only the dialog can turn it off. `page.pdf({ displayHeaderFooter: false })` is already the default on the Playwright path. |
| Everything is ~95% the size you designed | `margin: 0` full-bleed trips "Fit to printable area" — consumer printers have a ~4-6mm non-printable border | Keep ink inside a 10mm inset; set Margins: None / Scale: 100% when proofing |

**A4 is 297mm = 1122.519…px at 96dpi — non-integral, and irrelevant.** Chrome swallows ~0.5px of
overflow, so the fraction sits inside tolerance. `296.8mm` is a widely-repeated fix that does not
work; measured, it still produces two pages while the body margin is at default.

## Fonts: static instances, never the variable font

`public/fonts/noto-sans-tc/` holds Noto Sans TC at **static weights 400/700**, self-hosted.

**This is not stylistic.** Skia forces any font carrying `fvar` to **Type3** with instanced
outlines, on every platform, by design — not a bug. A Type3 brochure fails the "text is
selectable and searchable" bar, which is the entire reason we are not rasterising with
html2canvas.

**Request ONE weight per css2 call.** Any multi-weight request returns the variable font — a
list (`:wght@400;700`) does it just as much as a range (`:wght@100..900`). The tell is that
several `@font-face` blocks share one file, because the browser instances the weights:

```
:wght@400        faces=7   uniqueFiles=7   ← static, 1:1
:wght@700        faces=7   uniqueFiles=7   ← static, 1:1
:wght@400;700    faces=14  uniqueFiles=7   ← VARIABLE, 2 weights per file
```

So: **`faces == uniqueFiles` → static. `faces > uniqueFiles` → variable.** That comparison is the
cheap, reliable check. Do not try to detect `fvar` by scanning the woff2 bytes — woff2 encodes
known table tags as 5-bit indices rather than ASCII, so a byte search for `fvar` returns false on
every variable font and looks like a pass.

`public/fonts/noto-sans-tc/` was built with one request per weight (400, then 700), which is why
`pdffonts` shows CIDFontType2 rather than Type3. Writing the natural `:wght@400;700` instead
would have silently shipped a Type3 brochure — and a check that only inspects the output PDF
would still pass on the day it was built, because the regression arrives with the next font.

Self-hosted rather than linked because headless Chromium in CI has no CJK fonts at all — a
machine-font fallback renders tofu the moment a render leaves a Windows laptop. Windows headless
*does* inherit system fonts (DirectWrite, and 微軟正黑體 ships in the base Win10/11 set), so this
failure will not reproduce locally, which makes it worse rather than better.

The 210 woff2 slices are `unicode-range`-partitioned, so the browser fetches only what it draws.
Subsetting to the codepoints actually present in the course rows would cut ~5.1MB to ~200KB —
worth doing before this ships anywhere real.

**`text-spacing-trim` is deliberately not declared.** `normal` is the initial value and Chromium
already applies it; writing it changes nothing. To *see* it working, toggle `space-all`.
Chromium implements only `normal`, `trim-start`, `space-all`, `space-first` — `trim-both` and
`trim-all` are spec-only and unimplemented everywhere. It is gated on the font having `halt` or
`chws`; Noto Sans TC has `halt`.

**`font-feature-settings: 'palt'` is deliberately absent.** It half-widths punctuation regardless
of `text-spacing-trim`, making the CSS property moot — not double-applied. Pick one; this picks
the CSS mechanism.

## Verifying a PDF actually works

The failure modes here are silent — rasterised text looks identical to real text until you try to
select it. Check the bytes, not the picture:

```bash
node -e "const b=require('fs').readFileSync('out.pdf','latin1');
console.log('pages     ', [...b.matchAll(/\/Type\s*\/Page[^s]/g)].length,   '(want 1)');
console.log('Type3     ', [...b.matchAll(/\/Type3/g)].length,               '(want 0)');
console.log('ToUnicode ', [...b.matchAll(/\/ToUnicode/g)].length,           '(want >0 = selectable)');
console.log('images    ', [...b.matchAll(/\/Subtype\s*\/Image/g)].length,   '(want 0 = nothing rasterised)');
console.log('BaseFont  ', [...new Set([...b.matchAll(/\/BaseFont\s*\/([\w+\-,]+)/g)].map(m=>m[1]))].join(' '));"
```

A subset prefix (`AAAAAA+NotoSansTC…`) means the face was embedded and subsetted. `poppler`'s
`pdffonts` does the same job but ships with neither Windows nor this repo
(`winget install oschwartz10612.Poppler`).

## Course data facts the brochure depends on

Measured against all 1,084 rows, and the reason the code looks like it does:

- **`Outline` carries a line-anchored `N.` chapter convention in 93.5% of rows.** `N-N`
  sub-markers appear in 5.6% — too thin to parse a second level. Anchor to line-start: a loose
  match also catches `版本 1.0` and the 4.7% of rows holding decimals like `3.5`.
- **`Outline` is untrusted.** Nine rows hold a complete HTML document, `<style>` block included —
  one sets `body{font-family:...;line-height:1.6}`, which through `innerHTML` would silently
  override the brochure's own typography. Strip to text with `DOMParser` first. Strip, don't
  escape: escaping prints `<!DOCTYPE html>` on the page.
- **Overflow is a main path.** `Outline + Objective + Target` reaches 4,819 chars (pkid 1999, a
  real course); roughly one row in five exceeds the sheet. The fit loop drops trailing chapters
  until it fits and **always says so** — silent truncation is worse than overflow.
- **Public-facing artifacts are gated on `PublishStatus = 2` (上架中).** The QR points at
  `uuu.com.tw/Course/Show/{pkid}/…`, which resolves for 87.6% of published courses and 0.3% of
  已下架 ones. The route matches on `pkid` alone — the trailing slug is decorative.

## Not built yet

The `unicode-range` Latin/Han split and its `@font-face` metric overrides (`size-adjust`,
`ascent-override`, `descent-override`, `line-gap-override`) are the real craft in 混排 typography
and are **not** here — the Latin face is still unchosen, so the brochure sets everything in one
face. The strut comes from the block's *first available font*, not the first family listed, which
is exactly the trap a Han-first `unicode-range` walks into.

Full rationale, the measured data, and the open questions: the approved design doc in
`~/.gstack/projects/cherlane555-cms/`.
