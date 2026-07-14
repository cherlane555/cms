import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PartnerForm } from './partner-form';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

const partner: Partner = {
  pkid: 1,
  name: '恆逸資訊',
  appKey: 'uwa',
  nameOnPartnerMenu: '恆逸資訊教育訓練中心',
  nameOnCourseDetailPage: '恆逸',
  displayOrder: 10,
  imageFilename: 'uwa.png',
};

function setup(id: string | null) {
  const serviceSpy = jasmine.createSpyObj<PartnerService>('PartnerService', [
    'getById',
    'create',
    'update',
  ]);
  serviceSpy.getById.and.returnValue(of(partner));
  serviceSpy.create.and.returnValue(of(partner));
  serviceSpy.update.and.returnValue(of(void 0));

  TestBed.configureTestingModule({
    imports: [PartnerForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: PartnerService, useValue: serviceSpy },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
      },
    ],
  });

  const fixture = TestBed.createComponent(PartnerForm);
  fixture.detectChanges();
  return { fixture, serviceSpy };
}

describe('PartnerForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  describe('new mode', () => {
    let fixture: ComponentFixture<PartnerForm>;
    let serviceSpy: jasmine.SpyObj<PartnerService>;

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
      cmp.form.setValue({
        name: '新廠商',
        appKey: 'new',
        nameOnPartnerMenu: '新廠商選單名',
        nameOnCourseDetailPage: '新廠商',
        displayOrder: 30,
        imageFilename: null,
      });
      cmp.save();
      expect(serviceSpy.create).toHaveBeenCalledTimes(1);
      const arg = serviceSpy.create.calls.mostRecent().args[0];
      expect(arg.pkid).toBe(0);
      expect(arg.name).toBe('新廠商');
    });
  });

  describe('edit mode', () => {
    let fixture: ComponentFixture<PartnerForm>;
    let serviceSpy: jasmine.SpyObj<PartnerService>;

    beforeEach(() => ({ fixture, serviceSpy } = setup('1')));

    it('loads the partner and patches the form', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeTrue();
      expect(serviceSpy.getById).toHaveBeenCalledWith(1);
      expect(cmp.form.controls.name.value).toBe('恆逸資訊');
    });

    it('calls update on save, carrying the loaded pkid', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(serviceSpy.update).toHaveBeenCalledTimes(1);
      const arg = serviceSpy.update.calls.mostRecent().args[0];
      expect(arg.pkid).toBe(1);
      expect(arg.appKey).toBe('uwa');
    });
  });
});
