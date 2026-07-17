import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CourseService } from '@core/services/course.service';
import { Course } from '@core/models/course.model';
import { CourseBrochure } from './course-brochure';

const BASE: Course = {
  pkid: 1999,
  title: 'AZ-104 Microsoft Azure 系統管理員',
  officialTitle: 'Microsoft Azure Administrator',
  courseId: 'AZ-104',
  prodCourseId: 'P-AZ-104',
  friendlyUrl: 'az-104',
  displayOrder: 1,
  partnerPkid: 1,
  courseGroupPkid: null,
  publishStatusPkid: 2,
  scheduleOn: '2026-01-01T00:00:00',
  scheduleOff: '2026-12-31T00:00:00',
  hour: 24,
  listPrice: 26000,
  learningCredit: 0,
  material: null,
  objective: '學會管理 Azure 訂用帳戶。',
  target: 'IT 系統管理員',
  prerequisites: null,
  outline: '1. 課程介紹\n2. 實作',
  towardCertOrExam: null,
  note: null,
  otherInfo: null,
  canRepeat: true,
  partnerName: 'Microsoft',
  courseGroupDescription: null,
  publishStatusDescription: '上架中',
  certificationPkids: [],
  jobCategoryPkids: [],
};

describe('CourseBrochure', () => {
  let fixture: ComponentFixture<CourseBrochure>;
  let host: HTMLElement;

  /**
   * `fit()` awaits document.fonts.load and drives a rAF measure loop against real layout.
   * Karma has neither the face nor a settled layout, so proving the loop here would lie in both
   * directions — it is stubbed for every test that only cares what renders. The one test that
   * cares about the loop's DECISION stubs the measurement instead and runs the real loop.
   */
  async function render(course: Partial<Course>, stubFit = true): Promise<CourseBrochure> {
    if (stubFit) {
      spyOn<any>(CourseBrochure.prototype, 'fit').and.resolveTo();
    }

    await TestBed.configureTestingModule({
      imports: [CourseBrochure],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: CourseService,
          useValue: jasmine.createSpyObj('CourseService', {
            getById: of({ ...BASE, ...course }),
          }),
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1999' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseBrochure);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    host = fixture.nativeElement as HTMLElement;
    return fixture.componentInstance;
  }

  it('announces the clip when the body overflows with no chapters to drop', async () => {
    // The record the old chapter-count test could not see: null Outline, huge Objective. Nothing
    // to drop, so `shownCount < allChapters.length` compared 0 < 0 and the footer cheerfully
    // said 課程詳細資訊請掃描 QR 碼 over content that had been cut off the page.
    spyOn<any>(CourseBrochure.prototype, 'overflows').and.returnValue(true);

    const cmp = await render({ outline: null, objective: 'x'.repeat(4000) }, false);
    // Awaited explicitly: whenStable() does not wait for this chain. `document.fonts.ready` is
    // a browser-created promise, and an unresolved promise is not a task the zone tracks — the
    // zone goes stable while the loop is still measuring.
    await (cmp as any).fit();
    fixture.detectChanges();

    expect((cmp as any).truncated()).toBeTrue();
    expect(host.textContent).toContain('節錄');
  });

  it('never renders 課程代碼 as a fact', async () => {
    // The Field Map marks CourseId "Never shown"; it was rendering at 11pt bold in the reader's
    // second-most-attended slot. Scoped to .facts because the public URL in the footer embeds
    // the CourseId by construction — that one is the QR contract, and deliberate.
    await render({});

    expect(host.textContent).not.toContain('課程代碼');
    expect(host.querySelector('.facts')?.textContent).not.toContain('AZ-104');
  });

  it('numbers chapters as the author wrote them, not by position', async () => {
    await render({ outline: '1. 課程介紹\n2. 實作\n4. 進階' });

    const nums = Array.from(host.querySelectorAll('.num')).map((n) => n.textContent?.trim());
    expect(nums).toEqual(['1', '2', '4']);
  });

  it('prints the public URL and never promises a QR that is absent', async () => {
    const cmp = await render({});

    (cmp as any).qrSvg.set(null);
    fixture.detectChanges();

    expect(host.querySelector('.url')?.textContent).toContain(
      'https://www.uuu.com.tw/Course/Show/1999/AZ-104',
    );
    expect(host.textContent).toContain('請見下方網址');
    expect(host.textContent).not.toContain('請掃描 QR 碼');
  });

  it('renders Hour=0 and ListPrice=0 rather than hiding them', async () => {
    // 9 rows and 1 row. A regression fence against "improving" these into @if (c.hour).
    await render({ hour: 0, listPrice: 0 });

    expect(host.textContent).toContain('0 小時');
    expect(host.textContent).toContain('NT$ 0');
  });

  it('renders no .official element when officialTitle is null', async () => {
    // D6: on a composed document absence is absence — an em dash reads as a printing error.
    await render({ officialTitle: null });

    expect(host.querySelector('.official')).toBeNull();
  });

  it('names the document after the course, so Save-as-PDF suggests that filename', async () => {
    // Chrome reads the print filename off document.title. Left alone it is CMSNG.pdf.
    await render({});

    expect(TestBed.inject(Title).getTitle()).toBe('AZ-104-課程簡介');
  });

  it('strips a junk CourseId out of the document title', async () => {
    // CourseId is free-text varchar(50); the table really holds values like 'string' and '00',
    // and a slash would land in a filename.
    await render({ courseId: 'A B/C' });

    expect(TestBed.inject(Title).getTitle()).toBe('ABC-課程簡介');
  });

  it('restores the app title on leave', async () => {
    // Set through the DOM, not the Title service: injecting before configureTestingModule()
    // would instantiate the testing module and render() could no longer configure it.
    document.title = 'CMSNG';

    await render({});
    expect(document.title).toBe('AZ-104-課程簡介');

    fixture.destroy();

    expect(document.title).toBe('CMSNG');
  });

  it('steps the title class down at the documented thresholds', async () => {
    const cmp = await render({ title: 'x'.repeat(78) }); // the longest real title
    expect((cmp as any).titleClass()).toBe('title-sm');

    (cmp as any).course.set({ ...BASE, title: 'x'.repeat(10) });
    expect((cmp as any).titleClass()).toBe('title-lg');
  });
});
