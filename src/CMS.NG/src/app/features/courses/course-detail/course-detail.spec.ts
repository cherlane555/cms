import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Tooltip } from 'primeng/tooltip';
import { of } from 'rxjs';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { PublishStatusService } from '@core/services/publish-status.service';
import { Course } from '@core/models/course.model';
import { PublishStatus } from '@core/models/publish-status.model';
import { CourseDetail } from './course-detail';

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
  objective: null,
  target: null,
  prerequisites: null,
  outline: null,
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

const PUBLISHED_7: PublishStatus = {
  pkid: 7,
  description: '上架中',
  isDraft: false,
  isPublished: true,
  isDiscontinued: false,
};

const UNPUBLISHED_2: PublishStatus = {
  pkid: 2,
  description: '已下架',
  isDraft: false,
  isPublished: false,
  isDiscontinued: true,
};

describe('CourseDetail', () => {
  let fixture: ComponentFixture<CourseDetail>;
  let host: HTMLElement;

  async function render(course: Partial<Course>, statuses: PublishStatus[]): Promise<CourseDetail> {
    await TestBed.configureTestingModule({
      imports: [CourseDetail],
      providers: [
        // MANDATORY: RowAuditBadge injects RowAuditService -> HttpClient. Without these the
        // whole suite goes red on a NullInjectorError that names neither.
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: CourseService,
          useValue: jasmine.createSpyObj('CourseService', { getById: of({ ...BASE, ...course }) }),
        },
        {
          provide: LookupService,
          useValue: jasmine.createSpyObj('LookupService', {
            getCertifications: of([]),
            getJobCategories: of([]),
          }),
        },
        {
          provide: PublishStatusService,
          useValue: jasmine.createSpyObj('PublishStatusService', { getAll: of(statuses) }),
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1999' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseDetail);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    host = fixture.nativeElement as HTMLElement;
    return fixture.componentInstance;
  }

  /** The button is always present now — an unexplained absence is worse than an explained lock. */
  function exportButton(): HTMLButtonElement {
    const button = Array.from(host.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('下載課程簡介 PDF'),
    );
    if (!button) {
      throw new Error('the 下載課程簡介 PDF button is not in the DOM');
    }
    return button;
  }

  it('enables the export when the status isPublished, whatever its pkid', async () => {
    // pkid 7, not 2. PublishStatus.Pkid is a user-assigned non-IDENTITY byte with a live admin
    // screen behind it, so the literal 2 was never a fact about the world.
    await render({ publishStatusPkid: 7 }, [PUBLISHED_7]);

    expect(host.textContent).toContain('下載課程簡介 PDF');
    expect(exportButton().disabled).toBeFalse();
  });

  it('disables the export when pkid is 2 but the status is not published', async () => {
    // The test that can only pass once the code stops trusting the literal.
    await render({ publishStatusPkid: 2 }, [UNPUBLISHED_2]);

    expect(exportButton().disabled).toBeTrue();
  });

  it('explains the lock rather than hiding the button', async () => {
    // 648 of 1,084 courses are not 上架中. An @if deletes the feature on 6 pages in 10 and tells
    // the reader nothing about why.
    await render({ publishStatusPkid: 2 }, [UNPUBLISHED_2]);

    // Asks the directive, not an ng-reflect attribute: those are a dev-mode debug artifact and
    // vanish under an optimized build, which would make this pass for the wrong reason.
    const tooltip = fixture.debugElement.query(By.directive(Tooltip)).injector.get(Tooltip);
    expect(tooltip.content).toBe('僅上架中課程可下載簡介');
  });

  it('navigates to the brochure route', async () => {
    const cmp = await render({ publishStatusPkid: 7 }, [PUBLISHED_7]);
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    (cmp as any).brochure();

    expect(navigate).toHaveBeenCalledWith(['/courses', 1999, 'brochure']);
  });

  it('leads the actions bar with the row-audit badge', async () => {
    // Lab 06 compliance fence.
    await render({}, [UNPUBLISHED_2]);

    const first = host.querySelector('.actions')!.firstElementChild!;
    expect(first.tagName.toLowerCase()).toBe('app-row-audit-badge');
  });

  it('renders an em dash for an empty officialTitle', async () => {
    // Correct HERE (a form-shaped read view, where an empty slot must be distinguishable from a
    // missing row) and wrong on the brochure, which is a composed document. See D6.
    await render({ officialTitle: null }, [UNPUBLISHED_2]);

    expect(host.textContent).toContain('—');
  });
});
