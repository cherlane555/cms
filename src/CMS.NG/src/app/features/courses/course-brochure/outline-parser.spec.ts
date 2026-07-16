import { parseOutline, stripToText } from './outline-parser';

/**
 * Pure functions — no TestBed, no fixture. Every case below is drawn from the measured shape of
 * the 1,084 production `Outline` rows, not from imagination; the percentages name how much of
 * the table each test speaks for.
 */
describe('outline-parser', () => {
  describe('stripToText', () => {
    it('returns the raw string when it contains no markup', () => {
      expect(stripToText('1. 課程介紹\n2. 實作')).toBe('1. 課程介紹\n2. 實作');
    });

    it('drops style/script and returns only body text (pkid=3233)', () => {
      const raw =
        '<!DOCTYPE html><html><head><style>body{line-height:1.6}</style></head>' +
        '<body>1. 課程介紹</body></html>';

      const text = stripToText(raw);

      expect(text).toContain('1. 課程介紹');
      // The 0.8% of rows carrying a full HTML document. Through innerHTML their <style> would
      // silently override the brochure's own typography.
      expect(text).not.toContain('line-height');
      expect(text).not.toContain('DOCTYPE');
    });
  });

  describe('parseOutline', () => {
    it('returns an empty array for null', () => {
      expect(parseOutline(null)).toEqual([]);
    });

    it('splits on line-anchored N. and attaches the following lines', () => {
      const chapters = parseOutline('1. 課程介紹\n介紹內容\n2. 實作\n實作內容');

      expect(chapters.length).toBe(2);
      expect(chapters[0].heading).toBe('課程介紹');
      expect(chapters[0].lines).toEqual(['介紹內容']);
      expect(chapters[1].heading).toBe('實作');
      expect(chapters[1].lines).toEqual(['實作內容']);
    });

    it('carries the authored marker rather than the render position', () => {
      // An author who writes 1., 2., 4. gets 1., 2., 4. Renumbering to 1., 2., 3. invents a
      // chapter the syllabus does not have.
      const chapters = parseOutline('1. 課程介紹\n2. 實作\n4. 進階');

      expect(chapters.map((c) => c.num)).toEqual([1, 2, 4]);
    });

    it('does not treat a mid-line decimal as a chapter', () => {
      // The 4.7% of rows containing decimals. Asserts the ^ anchor.
      const chapters = parseOutline('1. 課程介紹\n本課程 3.5 小時\n版本 1.0 更新');

      expect(chapters.length).toBe(1);
      expect(chapters[0].lines.length).toBe(2);
    });

    it('does not treat a line starting with a decimal as a chapter', () => {
      // Before the (?!\d) lookahead this yielded 2 chapters, the second headed '5 小時的實作' —
      // (\d{1,2})\. matched '3.' and (.*) swallowed the rest.
      const chapters = parseOutline('1. 課程介紹\n3.5 小時的實作');

      expect(chapters.length).toBe(1);
      expect(chapters[0].lines).toEqual(['3.5 小時的實作']);
    });

    it('collapses an outline with no N. convention into one prose chapter, blanks dropped', () => {
      // The 6.5%.
      expect(parseOutline('這是一段介紹\n\n第二段')).toEqual([
        { heading: '', lines: ['這是一段介紹', '第二段'] },
      ]);
    });

    it('keeps text before the first marker as an unheaded lead-in', () => {
      const chapters = parseOutline('前言\n1. 課程介紹');

      expect(chapters.length).toBe(2);
      expect(chapters[0].heading).toBe('');
      expect(chapters[0].lines).toEqual(['前言']);
      expect(chapters[1].heading).toBe('課程介紹');
    });
  });
});
