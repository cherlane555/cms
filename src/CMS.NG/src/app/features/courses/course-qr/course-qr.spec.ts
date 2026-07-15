import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { CourseQr } from './course-qr';

describe('CourseQr', () => {
  let fixture: ComponentFixture<CourseQr>;
  let component: CourseQr;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CourseQr],
      providers: [provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseQr);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('pkid', 42);
    fixture.componentRef.setInput('courseId', 'AZ-104');
    fixture.detectChanges();
  });

  /** QR generation is async (qrcode toDataURL); poll until the image renders. */
  async function qrImage(): Promise<HTMLImageElement> {
    for (let i = 0; i < 100; i++) {
      fixture.detectChanges();
      const img = (fixture.nativeElement as HTMLElement).querySelector('img.qr-image');
      if (img) {
        return img as HTMLImageElement;
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    throw new Error('QR image was not rendered');
  }

  it('encodes the public course URL from pkid and CourseId', async () => {
    expect(component.url).toBe('https://www.uuu.com.tw/Course/Show/42/AZ-104');

    const img = await qrImage();
    expect(img.getAttribute('alt')).toBe('https://www.uuu.com.tw/Course/Show/42/AZ-104');
    expect(img.src).toMatch(/^data:image\/png/);
  });

  it('shows the CourseId as the title', () => {
    const title = (fixture.nativeElement as HTMLElement).querySelector('.qr-title');
    expect(title?.textContent?.trim()).toBe('AZ-104');
  });

  it('download saves the generated QR image as a PNG named after the CourseId', async () => {
    await qrImage();

    const clicked: HTMLAnchorElement[] = [];
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
      clicked.push(this);
    });

    component.download();

    expect(clicked.length).toBe(1);
    expect(clicked[0].download).toBe('AZ-104.png');
    expect(clicked[0].href).toMatch(/^data:image\/png/);
  });
});
