import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { FeaturedPromoItemForm } from './featured-promo-item-form';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  PromotionLookup,
} from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemForm', () => {
  let fixture: ComponentFixture<FeaturedPromoItemForm>;
  let serviceSpy: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookupSpy: jasmine.SpyObj<LookupService>;
  let messagesSpy: jasmine.SpyObj<MessageService>;

  const promo: PromotionLookup = {
    pkid: 100,
    promoCode: '20251215_n8n',
    topic: 'n8n自動化三部曲',
    description: '從自動化新手到企業級AI架構師學習路徑',
  };

  const existing: FeaturedPromoItem = {
    pkid: 10,
    scheduleOn: '2026-07-13T00:00:00',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 100,
    topic: '原本主題',
    description: '原本描述',
    promoCode: '20251215_n8n',
  };

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('FeaturedPromoItemService', ['create', 'update']);
    serviceSpy.create.and.returnValue(of(existing));
    serviceSpy.update.and.returnValue(of(void 0));

    lookupSpy = jasmine.createSpyObj('LookupService', ['getPromotionByCode']);
    lookupSpy.getPromotionByCode.and.returnValue(of(promo));

    messagesSpy = jasmine.createSpyObj('MessageService', ['add']);

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemForm],
      providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: FeaturedPromoItemService, useValue: serviceSpy },
        { provide: LookupService, useValue: lookupSpy },
        { provide: MessageService, useValue: messagesSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemForm);
    fixture.componentInstance.scheduleOn = '2026-07-13';
    fixture.componentInstance.trainingCenterPkid = 1;
    fixture.componentInstance.slot = 1;
  });

  function form() {
    return (fixture.componentInstance as any).form;
  }

  it('New mode starts with an empty form', () => {
    fixture.detectChanges();
    expect(form().getRawValue()).toEqual({ promoCode: '', topic: '', description: '' });
  });

  it('Edit mode patches the existing values', () => {
    fixture.componentInstance.item = existing;
    fixture.detectChanges();

    expect(form().getRawValue()).toEqual({
      promoCode: '20251215_n8n',
      topic: '原本主題',
      description: '原本描述',
    });
  });

  it('Paste mode prefills the copied values', () => {
    fixture.componentInstance.prefill = {
      promotionPkid: 100,
      promoCode: '20251215_n8n',
      topic: '複製主題',
      description: '複製描述',
    };
    fixture.detectChanges();

    expect(form().getRawValue()).toEqual({
      promoCode: '20251215_n8n',
      topic: '複製主題',
      description: '複製描述',
    });
  });

  it('lookup resolves the PromoCode and fills the empty Topic/Description', () => {
    fixture.detectChanges();
    form().patchValue({ promoCode: '20251215_n8n' });

    (fixture.componentInstance as any).lookup();

    expect(lookupSpy.getPromotionByCode).toHaveBeenCalledWith('20251215_n8n');
    expect(form().getRawValue().topic).toBe('n8n自動化三部曲');
    expect(form().getRawValue().description).toBe(promo.description);
  });

  it('lookup keeps user-entered Topic/Description', () => {
    fixture.detectChanges();
    form().patchValue({ promoCode: '20251215_n8n', topic: '自訂主題', description: '自訂描述' });

    (fixture.componentInstance as any).lookup();

    expect(form().getRawValue().topic).toBe('自訂主題');
    expect(form().getRawValue().description).toBe('自訂描述');
  });

  it('save in New mode resolves the code then creates with the resolved Promotion_pkid', () => {
    fixture.detectChanges();
    form().patchValue({ promoCode: '20251215_n8n', topic: '主題', description: '描述' });
    const saved = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(saved);

    (fixture.componentInstance as any).save();

    expect(lookupSpy.getPromotionByCode).toHaveBeenCalledWith('20251215_n8n');
    expect(serviceSpy.create).toHaveBeenCalledWith({
      pkid: 0,
      scheduleOn: '2026-07-13',
      trainingCenterPkid: 1,
      slot: 1,
      promotionPkid: 100,
      topic: '主題',
      description: '描述',
    });
    expect(serviceSpy.update).not.toHaveBeenCalled();
    expect(saved).toHaveBeenCalled();
  });

  it('save in Edit mode updates without re-looking-up an unchanged code', () => {
    fixture.componentInstance.item = existing;
    fixture.detectChanges();
    form().patchValue({ topic: '更新主題' });
    const saved = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(saved);

    (fixture.componentInstance as any).save();

    expect(lookupSpy.getPromotionByCode).not.toHaveBeenCalled();
    expect(serviceSpy.update).toHaveBeenCalledWith({
      pkid: 10,
      scheduleOn: '2026-07-13',
      trainingCenterPkid: 1,
      slot: 1,
      promotionPkid: 100,
      topic: '更新主題',
      description: '原本描述',
    });
    expect(saved).toHaveBeenCalled();
  });

  it('save blocks when the PromoCode cannot be resolved', () => {
    lookupSpy.getPromotionByCode.and.returnValue(throwError(() => ({ status: 404 })));
    fixture.detectChanges();
    form().patchValue({ promoCode: 'nope', topic: '主題' });

    (fixture.componentInstance as any).save();

    expect(serviceSpy.create).not.toHaveBeenCalled();
    expect(messagesSpy.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', summary: '查無促銷代碼' }),
    );
  });

  it('save does nothing while the form is invalid', () => {
    fixture.detectChanges();

    (fixture.componentInstance as any).save();

    expect(lookupSpy.getPromotionByCode).not.toHaveBeenCalled();
    expect(serviceSpy.create).not.toHaveBeenCalled();
  });

  it('cancel emits cancelled', () => {
    fixture.detectChanges();
    const cancelled = jasmine.createSpy('cancelled');
    fixture.componentInstance.cancelled.subscribe(cancelled);

    (fixture.componentInstance as any).cancel();

    expect(cancelled).toHaveBeenCalled();
  });
});
