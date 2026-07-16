import { courseQrSvg, publicCourseUrl } from './course-qr-url';

describe('course-qr-url', () => {
  it('builds the public page URL from pkid and CourseId', () => {
    expect(publicCourseUrl(1999, 'AZ-104')).toBe('https://www.uuu.com.tw/Course/Show/1999/AZ-104');
  });

  it('percent-encodes a junk CourseId', () => {
    // CourseId is free-text varchar(50) and the table holds known junk ('string', '00'). This
    // URL is also fed to bypassSecurityTrustHtml via the QR, so the encoding is the fence.
    expect(publicCourseUrl(1, 'A B/C')).toBe('https://www.uuu.com.tw/Course/Show/1/A%20B%2FC');
  });

  it('resolves courseQrSvg to an SVG string', async () => {
    // Vector, not a data URL. If this ever returns a raster the brochure's QR silently stops
    // being printable at 22mm and nothing else in the suite would notice.
    const svg = await courseQrSvg(1999, 'AZ-104');

    expect(svg.startsWith('<svg')).toBeTrue();
  });
});
