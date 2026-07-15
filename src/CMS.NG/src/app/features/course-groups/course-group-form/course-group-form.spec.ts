import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CourseGroupForm } from './course-group-form';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

const group: CourseGroup = { pkid: 1, description: '資料庫管理' };

function setup(id: string | null) {
  const serviceSpy = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', [
    'getById',
    'create',
    'update',
  ]);
  serviceSpy.getById.and.returnValue(of(group));
  serviceSpy.create.and.returnValue(of(group));
  serviceSpy.update.and.returnValue(of(void 0));

  TestBed.configureTestingModule({
    imports: [CourseGroupForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: CourseGroupService, useValue: serviceSpy },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
      },
    ],
  });

  const fixture = TestBed.createComponent(CourseGroupForm);
  fixture.detectChanges();
  return { fixture, serviceSpy };
}

describe('CourseGroupForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  describe('new mode', () => {
    let fixture: ComponentFixture<CourseGroupForm>;
    let serviceSpy: jasmine.SpyObj<CourseGroupService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup(null)));

    it('creates in add mode (no getById)', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeFalse();
      expect(serviceSpy.getById).not.toHaveBeenCalled();
    });

    it('does not save an invalid (empty) form', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(serviceSpy.create).not.toHaveBeenCalled();
    });

    it('calls create with pkid 0 when the form is valid', () => {
      const cmp = fixture.componentInstance as any;
      cmp.form.setValue({ description: '雲端技術' });
      cmp.save();
      expect(serviceSpy.create).toHaveBeenCalledTimes(1);
      const arg = serviceSpy.create.calls.mostRecent().args[0];
      expect(arg.pkid).toBe(0);
      expect(arg.description).toBe('雲端技術');
    });
  });

  describe('edit mode', () => {
    let fixture: ComponentFixture<CourseGroupForm>;
    let serviceSpy: jasmine.SpyObj<CourseGroupService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup('1')));

    it('loads the course group and patches the form', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeTrue();
      expect(serviceSpy.getById).toHaveBeenCalledWith(1);
      expect(cmp.form.controls.description.value).toBe('資料庫管理');
    });

    it('calls update on save, carrying the loaded pkid', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(serviceSpy.update).toHaveBeenCalledTimes(1);
      const arg = serviceSpy.update.calls.mostRecent().args[0];
      expect(arg.pkid).toBe(1);
      expect(arg.description).toBe('資料庫管理');
    });
  });
});
