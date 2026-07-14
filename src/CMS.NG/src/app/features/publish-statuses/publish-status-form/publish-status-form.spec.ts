import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PublishStatusForm } from './publish-status-form';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

const status: PublishStatus = {
  pkid: 1,
  description: '草稿',
  isDraft: true,
  isPublished: false,
  isDiscontinued: false,
};

function setup(id: string | null) {
  const serviceSpy = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', [
    'getById',
    'create',
    'update',
  ]);
  serviceSpy.getById.and.returnValue(of(status));
  serviceSpy.create.and.returnValue(of(status));
  serviceSpy.update.and.returnValue(of(void 0));

  TestBed.configureTestingModule({
    imports: [PublishStatusForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: PublishStatusService, useValue: serviceSpy },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
      },
    ],
  });

  const fixture = TestBed.createComponent(PublishStatusForm);
  fixture.detectChanges();
  return { fixture, serviceSpy };
}

describe('PublishStatusForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  describe('new mode', () => {
    let fixture: ComponentFixture<PublishStatusForm>;
    let serviceSpy: jasmine.SpyObj<PublishStatusService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup(null)));

    it('creates in add mode with the pkid control enabled', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeFalse();
      expect(cmp.form.controls.pkid.enabled).toBeTrue();
      expect(serviceSpy.getById).not.toHaveBeenCalled();
    });

    it('does not save an invalid (empty) form', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(serviceSpy.create).not.toHaveBeenCalled();
    });

    it('calls create when the form is valid', () => {
      const cmp = fixture.componentInstance as any;
      cmp.form.setValue({
        pkid: 3,
        description: '已發布',
        isDraft: false,
        isPublished: true,
        isDiscontinued: false,
      });
      cmp.save();
      expect(serviceSpy.create).toHaveBeenCalledTimes(1);
      const arg = serviceSpy.create.calls.mostRecent().args[0];
      expect(arg.pkid).toBe(3);
      expect(arg.isPublished).toBeTrue();
    });
  });

  describe('edit mode', () => {
    let fixture: ComponentFixture<PublishStatusForm>;
    let serviceSpy: jasmine.SpyObj<PublishStatusService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup('1')));

    it('loads the status and disables the pkid control', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeTrue();
      expect(serviceSpy.getById).toHaveBeenCalledWith(1);
      expect(cmp.form.controls.description.value).toBe('草稿');
      expect(cmp.form.controls.pkid.disabled).toBeTrue();
    });

    it('calls update on save (including the disabled pkid)', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(serviceSpy.update).toHaveBeenCalledTimes(1);
      const arg = serviceSpy.update.calls.mostRecent().args[0];
      expect(arg.pkid).toBe(1);
      expect(arg.description).toBe('草稿');
    });
  });
});
