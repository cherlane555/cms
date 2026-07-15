import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CourseForm } from './course-form';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';

const course: Course = {
  pkid: 1,
  title: 'SQL Server 資料庫管理',
  officialTitle: null,
  courseId: 'C001',
  prodCourseId: 'P001',
  friendlyUrl: 'sql-server-admin',
  displayOrder: 1,
  partnerPkid: 1,
  courseGroupPkid: null,
  publishStatusPkid: 1,
  scheduleOn: '2026-01-01T00:00:00',
  scheduleOff: '2026-12-31T00:00:00',
  hour: 21,
  listPrice: 30000,
  learningCredit: 0,
  material: null,
  objective: null,
  target: null,
  prerequisites: null,
  outline: null,
  towardCertOrExam: null,
  note: null,
  otherInfo: null,
  canRepeat: false,
  partnerName: 'Microsoft',
  courseGroupDescription: null,
  publishStatusDescription: '已發布',
  certificationPkids: [],
  jobCategoryPkids: [],
};

function setup(id: string | null) {
  const serviceSpy = jasmine.createSpyObj<CourseService>('CourseService', [
    'getById',
    'create',
    'update',
  ]);
  serviceSpy.getById.and.returnValue(of(course));
  serviceSpy.create.and.returnValue(of(course));
  serviceSpy.update.and.returnValue(of(void 0));

  const lookupSpy = jasmine.createSpyObj<LookupService>('LookupService', [
    'getPartners',
    'getCourseGroups',
    'getPublishStatuses',
    'getCertifications',
    'getJobCategories',
  ]);
  lookupSpy.getPartners.and.returnValue(of([{ pkid: 1, name: 'Microsoft' }]));
  lookupSpy.getCourseGroups.and.returnValue(of([]));
  lookupSpy.getPublishStatuses.and.returnValue(of([{ pkid: 1, description: '已發布' }]));
  lookupSpy.getCertifications.and.returnValue(of([]));
  lookupSpy.getJobCategories.and.returnValue(of([]));

  TestBed.configureTestingModule({
    imports: [CourseForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: CourseService, useValue: serviceSpy },
      { provide: LookupService, useValue: lookupSpy },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
      },
    ],
  });

  const fixture = TestBed.createComponent(CourseForm);
  fixture.detectChanges();
  return { fixture, serviceSpy };
}

// Shared toolbar assertions: pinned (sticky) styling + Save/Cancel present.
function expectStickyToolbar(fixture: ComponentFixture<CourseForm>) {
  const host: HTMLElement = fixture.nativeElement;
  const toolbar = host.querySelector<HTMLElement>('.page-header');
  expect(toolbar).withContext('action toolbar (.page-header) renders').not.toBeNull();

  const style = getComputedStyle(toolbar!);
  expect(style.position).withContext('toolbar is pinned').toBe('sticky');
  // -1.25rem (-20px): cancels the .content pane's top padding so the bar pins flush.
  expect(style.top).withContext('toolbar pins to the top edge').toBe('-20px');
  expect(Number(style.zIndex)).withContext('toolbar layers above the form').toBeGreaterThan(0);

  const labels = Array.from(toolbar!.querySelectorAll('p-button')).map(
    (b) => b.textContent?.trim() ?? '',
  );
  expect(labels.some((l) => l.includes('儲存'))).withContext('Save button present').toBeTrue();
  expect(labels.some((l) => l.includes('取消'))).withContext('Cancel button present').toBeTrue();
}

describe('CourseForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  describe('new mode', () => {
    let fixture: ComponentFixture<CourseForm>;
    let serviceSpy: jasmine.SpyObj<CourseService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup(null)));

    it('creates in add mode (no getById)', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeFalse();
      expect(serviceSpy.getById).not.toHaveBeenCalled();
    });

    it('renders a sticky action toolbar with Save and Cancel', () => {
      expectStickyToolbar(fixture);
    });
  });

  describe('edit mode', () => {
    let fixture: ComponentFixture<CourseForm>;
    let serviceSpy: jasmine.SpyObj<CourseService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup('1')));

    it('loads the course and patches the form', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeTrue();
      expect(serviceSpy.getById).toHaveBeenCalledWith(1);
      expect(cmp.form.controls.title.value).toBe('SQL Server 資料庫管理');
    });

    it('renders a sticky action toolbar with Save and Cancel', () => {
      expectStickyToolbar(fixture);
    });
  });
});
