/**
 * Parses the `Outline` blob into chapters for the 課程簡章.
 *
 * Measured against all 1,084 production rows (2026-07-16):
 * - 93.5% carry a line-anchored `N.` chapter convention — parsed here.
 * - 5.6% carry `N-N` sub-markers — NOT parsed; too rare to design a second level around.
 * - 0.8% (9 rows) hold a complete HTML document, `<style>` block included. Rendering those
 *   through innerHTML would leak their CSS over the brochure's own typography, so every
 *   blob is stripped to text first.
 */

export interface OutlineChapter {
  /** Heading text without the `N.` marker, e.g. "課程介紹". Empty for the prose fallback. */
  heading: string;
  /**
   * The number the author wrote, e.g. 4 from `4.` — NOT the render position. An outline that
   * skips (`1. 2. 4.`) prints its own numbering; renumbering it silently invents a chapter 3.
   * Absent on the prose fallback and the unheaded lead-in, which have no marker to carry.
   */
  num?: number;
  /** Body lines under the heading, blank lines dropped. */
  lines: string[];
}

/** Line-anchored chapter marker: `1.`, `10.` at line start. Mid-line decimals (`3.5`, */
/** `版本 1.0`) do not match — a loose `%1.%` probe over-counted this convention by 2.2pts. */
/** `(?!\d)` extends that to LINE-LEADING decimals: `3.5 小時的實作` is a body line, not */
/** chapter 3 headed "5 小時的實作". 4.7% of rows carry a decimal. */
const CHAPTER_RE = /^[ \t]*(\d{1,2})\.(?!\d)[ \t]*(.*)$/;

/**
 * Strips markup to plain text. `DOMParser` on a detached document neither executes scripts
 * nor applies `<style>`, so the 9 HTML-document rows collapse to their text safely.
 * Escaping instead of stripping would print `<!DOCTYPE html>` on the brochure, which is worse.
 */
export function stripToText(raw: string): string {
  if (!raw.includes('<')) {
    return raw;
  }
  const doc = new DOMParser().parseFromString(raw, 'text/html');
  doc.querySelectorAll('style, script, head').forEach((el) => el.remove());
  return doc.body?.textContent ?? '';
}

export function parseOutline(raw: string | null): OutlineChapter[] {
  if (!raw) {
    return [];
  }
  const lines = stripToText(raw)
    .split(/\r?\n/)
    .map((l) => l.replace(/\t/g, ' ').trim());

  const chapters: OutlineChapter[] = [];
  for (const line of lines) {
    if (!line) {
      continue;
    }
    const match = CHAPTER_RE.exec(line);
    if (match) {
      chapters.push({ heading: match[2].trim(), num: Number(match[1]), lines: [] });
    } else if (chapters.length) {
      chapters[chapters.length - 1].lines.push(line);
    } else {
      // Text before the first marker: keep it as an unheaded lead-in.
      chapters.push({ heading: '', lines: [line] });
    }
  }

  // The 6.5% with no convention: one prose block, paragraph breaks preserved.
  if (!chapters.some((c) => c.heading)) {
    return lines.filter(Boolean).length ? [{ heading: '', lines: lines.filter(Boolean) }] : [];
  }
  return chapters;
}
