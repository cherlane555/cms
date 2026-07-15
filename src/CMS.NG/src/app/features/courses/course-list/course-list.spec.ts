import { ComponentFixture, TestBed, fakeAsync, flush, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { CourseList } from './course-list';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';

describe('CourseList', () => {
  let fixture: ComponentFixture<CourseList>;
  let serviceSpy: jasmine.SpyObj<CourseService>;
  let lookupSpy: jasmine.SpyObj<LookupService>;

  const course: Course = {
    pkid: 1,
    title: 'Azure 基礎課程',
    officialTitle: null,
    courseId: 'AZ-900',
    prodCourseId: 'P-AZ900',
    friendlyUrl: 'az-900',
    displayOrder: 1,
    partnerPkid: 10,
    courseGroupPkid: 20,
    publishStatusPkid: 30,
    scheduleOn: '2026-01-01T00:00:00',
    scheduleOff: '2026-12-31T00:00:00',
    hour: 8,
    listPrice: 3000,
    learningCredit: 5,
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
    courseGroupDescription: '雲端基礎',
    publishStatusDescription: '上架',
    certificationPkids: [],
    jobCategoryPkids: [],
  };

  // getById returns the n-n associations the list rows lack; an inline save must
  // carry these through to the update request unchanged.
  const fullCourse: Course = { ...course, certificationPkids: [7], jobCategoryPkids: [9] };

  // Body cell order: 0 主代碼, 1 顯示順序, 2 簡介代碼, 3 科目代碼, 4 課程名稱,
  // 5 原廠, 6 課程群組, 7 上架狀態, 8 上架日期, 9 下架日期, 10 時數, 11 定價,
  // 12 點數, 13 允許重聽, 14 操作.
  const TITLE = 4;
  const READ_ONLY = [0, 5, 6];

  function bodyCells(): HTMLTableCellElement[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr td'),
    ) as HTMLTableCellElement[];
  }

  function dblclick(cell: HTMLElement): void {
    cell.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    fixture.detectChanges();
  }

  function typeInto(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('CourseService', ['query', 'getById', 'update', 'delete']);
    serviceSpy.query.and.returnValue(of([course]));
    serviceSpy.getById.and.returnValue(of(fullCourse));
    serviceSpy.update.and.returnValue(of(void 0));
    serviceSpy.delete.and.returnValue(of(void 0));

    lookupSpy = jasmine.createSpyObj('LookupService', [
      'getPartners',
      'getCourseGroups',
      'getPublishStatuses',
    ]);
    lookupSpy.getPartners.and.returnValue(of([{ pkid: 10, name: 'Microsoft' }]));
    lookupSpy.getCourseGroups.and.returnValue(of([{ pkid: 20, description: '雲端基礎' }]));
    lookupSpy.getPublishStatuses.and.returnValue(
      of([
        { pkid: 30, description: '上架' },
        { pkid: 31, description: '下架' },
      ]),
    );
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [CourseList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: CourseService, useValue: serviceSpy },
        { provide: LookupService, useValue: lookupSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseList);
    fixture.detectChanges();
  });

  it('creates and loads courses on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(serviceSpy.query).toHaveBeenCalledTimes(1);
    expect((fixture.componentInstance as any).courses().length).toBe(1);
  });

  describe('inline editing', () => {
    it('enters edit mode on double-click but not on single click', fakeAsync(() => {
      const cell = bodyCells()[TITLE];

      cell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      fixture.detectChanges();
      expect(cell.querySelector('input')).toBeNull();

      dblclick(cell);
      const input = cell.querySelector('input') as HTMLInputElement;
      expect(input).not.toBeNull();
      tick(); // NgModel writes the initial value on a microtask
      expect(input.value).toBe('Azure 基礎課程');
      flush();
    }));

    it('keeps 主代碼, 原廠 and 課程群組 read-only', fakeAsync(() => {
      const cells = bodyCells();
      for (const index of READ_ONLY) {
        dblclick(cells[index]);
        expect(cells[index].querySelector('input, p-select, p-datepicker, p-checkbox'))
          .withContext(`cell ${index} must not become editable`)
          .toBeNull();
      }
      expect((fixture.componentInstance as any).editingKey()).toBeNull();
      flush();
    }));

    it('persists the edited value on blur via the update endpoint', fakeAsync(() => {
      const cell = bodyCells()[TITLE];
      dblclick(cell);
      const input = cell.querySelector('input') as HTMLInputElement;

      typeInto(input, '更新後課程名稱');
      input.dispatchEvent(new Event('blur'));

      expect(serviceSpy.getById).toHaveBeenCalledWith(1);
      expect(serviceSpy.update).toHaveBeenCalledWith(
        jasmine.objectContaining({
          pkid: 1,
          title: '更新後課程名稱',
          certificationPkids: [7],
          jobCategoryPkids: [9],
        }),
      );
      fixture.detectChanges();
      expect((fixture.componentInstance as any).courses()[0].title).toBe('更新後課程名稱');
      flush();
    }));

    it('does not call the endpoint when the value is unchanged', fakeAsync(() => {
      const cell = bodyCells()[TITLE];
      dblclick(cell);
      const input = cell.querySelector('input') as HTMLInputElement;

      input.dispatchEvent(new Event('blur'));

      expect(serviceSpy.update).not.toHaveBeenCalled();
      expect((fixture.componentInstance as any).editingKey()).toBeNull();
      flush();
    }));

    it('blocks clearing a required field and stays in edit mode', fakeAsync(() => {
      const cell = bodyCells()[TITLE];
      dblclick(cell);
      const input = cell.querySelector('input') as HTMLInputElement;

      typeInto(input, '');
      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      expect(serviceSpy.update).not.toHaveBeenCalled();
      expect((fixture.componentInstance as any).editError()).toBe('此欄位為必填');
      expect(cell.querySelector('input')).not.toBeNull();
      flush();
    }));

    it('rejects negative numbers for 時數/定價/點數', () => {
      const cmp = fixture.componentInstance as any;
      const row = cmp.courses()[0];
      for (const field of ['hour', 'listPrice', 'learningCredit']) {
        cmp.editingKey.set(`1:${field}`);
        cmp.editValue = -3;
        cmp.editOriginal = row[field];
        cmp.commit(row, field);
        expect(cmp.editError()).withContext(field).toBe('不可為負數');
      }
      expect(serviceSpy.update).not.toHaveBeenCalled();
    });

    it('rejects an invalid date', () => {
      const cmp = fixture.componentInstance as any;
      const row = cmp.courses()[0];
      cmp.editingKey.set('1:scheduleOn');
      cmp.editValue = new Date('not-a-date');
      cmp.editOriginal = new Date(2026, 0, 1);
      cmp.commit(row, 'scheduleOn');
      expect(cmp.editError()).toBe('請輸入有效日期');
      expect(serviceSpy.update).not.toHaveBeenCalled();
    });

    it('rejects 上架日期 later than 下架日期', () => {
      const cmp = fixture.componentInstance as any;
      const row = cmp.courses()[0];
      cmp.editingKey.set('1:scheduleOn');
      cmp.editValue = new Date(2027, 0, 1); // scheduleOff is 2026-12-31
      cmp.editOriginal = new Date(2026, 0, 1);
      cmp.commit(row, 'scheduleOn');
      expect(cmp.editError()).toBe('上架日期不可晚於下架日期');
      expect(serviceSpy.update).not.toHaveBeenCalled();
    });

    it('reverts the cell when the save fails', fakeAsync(() => {
      serviceSpy.update.and.returnValue(throwError(() => new Error('boom')));
      const cell = bodyCells()[TITLE];
      dblclick(cell);
      const input = cell.querySelector('input') as HTMLInputElement;

      typeInto(input, '不會被儲存的名稱');
      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      expect(serviceSpy.update).toHaveBeenCalled();
      const cmp = fixture.componentInstance as any;
      expect(cmp.courses()[0].title).toBe('Azure 基礎課程');
      expect(cmp.editingKey()).toBeNull();
      flush();
    }));
  });
});
