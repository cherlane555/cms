import { toString } from 'qrcode';

/**
 * The public course page on the legacy site. Verified 2026-07-16: the route matches on
 * `pkid` alone — the trailing segment is decorative (a garbage slug resolves identically),
 * so `courseId` is kept only for URL readability.
 *
 * Resolves for 87.6% of 上架中 courses. 草稿 and 已下架 courses correctly 404, which is why
 * the brochure button is gated on PublishStatus 2.
 */
export function publicCourseUrl(pkid: number, courseId: string): string {
  return `https://www.uuu.com.tw/Course/Show/${pkid}/${encodeURIComponent(courseId)}`;
}

/**
 * QR as inline SVG, not the PNG data URL `course-qr` renders on screen.
 * Print needs vector: a 200px raster scaled to ~22mm lands well under printer resolution and
 * its module edges won't align to printer dots. `margin: 4` is the spec quiet zone (the
 * screen component's `margin: 1` is too tight to scan off paper).
 */
export function courseQrSvg(pkid: number, courseId: string): Promise<string> {
  return toString(publicCourseUrl(pkid, courseId), {
    type: 'svg',
    margin: 4,
    errorCorrectionLevel: 'M',
  });
}
