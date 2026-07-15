import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ConfirmationService, Confirmation } from 'primeng/api';
import { FeaturedPromoItemList } from './featured-promo-item-list';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  PromoClipboard,
  TrainingCenterLookup,
} from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemList', () => {
  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let serviceSpy: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookupSpy: jasmine.SpyObj<LookupService>;
  let clipboardSig: ReturnType<typeof signal<PromoClipboard | null>>;

  const centers: TrainingCenterLookup[] = [
    { pkid: 1, name: '台北' },
    { pkid: 2, name: '新竹' },
  ];

  // Monday of the current week, so the sample lands inside the default window.
  const monday = FeaturedPromoItemList.mondayOf(new Date());
  const mondayStr = FeaturedPromoItemList.toDateStr(monday);
  const sundayStr = FeaturedPromoItemList.toDateStr(FeaturedPromoItemList.addDays(monday, 6));

  const item: FeaturedPromoItem = {
    pkid: 10,
    scheduleOn: `${mondayStr}T00:00:00`,
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 100,
    topic: 'n8n自動化三部曲',
    description: '從自動化新手到企業級AI架構師學習路徑',
    promoCode: '20251215_n8n',
  };

  beforeEach(async () => {
    clipboardSig = signal<PromoClipboard | null>(null);
    serviceSpy = jasmine.createSpyObj(
      'FeaturedPromoItemService',
      ['query', 'move', 'delete', 'copy'],
      { clipboard: clipboardSig.asReadonly() },
    );
    serviceSpy.query.and.returnValue(of([item]));
    serviceSpy.move.and.returnValue(of(void 0));
    serviceSpy.delete.and.returnValue(of(void 0));

    lookupSpy = jasmine.createSpyObj('LookupService', ['getTrainingCenters']);
    lookupSpy.getTrainingCenters.and.returnValue(of(centers));

    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: FeaturedPromoItemService, useValue: serviceSpy },
        { provide: LookupService, useValue: lookupSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemList);
    fixture.detectChanges();
  });

  it('loads training centers, activates the first tab and queries the current Mon–Sun week', () => {
    expect(lookupSpy.getTrainingCenters).toHaveBeenCalledTimes(1);
    expect((fixture.componentInstance as any).activeTc()).toBe(1);
    expect(serviceSpy.query).toHaveBeenCalledWith({
      trainingCenterPkid: 1,
      scheduleFrom: mondayStr,
      scheduleTo: sundayStr,
    });
  });

  it('renders the tabs, seven day groups with three slots each, and the item values', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('.tab').length).toBe(2);
    expect(el.textContent).toContain('台北');
    expect(el.querySelectorAll('.day-group').length).toBe(7);
    expect(el.querySelectorAll('.slot-row').length).toBe(21);
    expect(el.querySelector<HTMLInputElement>('.cell.code')?.value).toBe('20251215_n8n');
    expect(el.textContent).toContain('上稿作業 FeaturedPromoItem');
  });

  it('places the item in the slot matching its ScheduleOn date and Slot', () => {
    const days = (fixture.componentInstance as any).days();
    expect(days[0].cells[0].item?.pkid).toBe(10); // Monday, slot 1
    expect(days[0].cells[1].item).toBeNull();
    expect(days[1].cells[0].item).toBeNull();
  });

  it('clicking a tab re-queries with that TrainingCenter and persists the state', () => {
    (fixture.componentInstance as any).selectTab(2);

    expect(serviceSpy.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ trainingCenterPkid: 2 }),
    );
    expect(sessionStorage.getItem('featured-promo-item-list-filters')).toContain('2');
  });

  it('nextWeek / prevWeek shift the window by seven days', () => {
    const cmp = fixture.componentInstance as any;
    cmp.nextWeek();

    const nextMonday = FeaturedPromoItemList.toDateStr(FeaturedPromoItemList.addDays(monday, 7));
    expect(serviceSpy.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ scheduleFrom: nextMonday }),
    );

    cmp.prevWeek();
    expect(serviceSpy.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ scheduleFrom: mondayStr }),
    );
  });

  it('copy hands the item to the service clipboard', () => {
    (fixture.componentInstance as any).copy(item);
    expect(serviceSpy.copy).toHaveBeenCalledWith(item);
  });

  it('paste opens the editor prefilled from the clipboard on an empty cell', () => {
    const copied: PromoClipboard = {
      promotionPkid: 100,
      promoCode: '20251215_n8n',
      topic: 'n8n自動化三部曲',
      description: '描述',
    };
    clipboardSig.set(copied);

    const cmp = fixture.componentInstance as any;
    const day = cmp.days()[1]; // Tuesday: empty
    cmp.paste(day, day.cells[0]);

    expect(cmp.editing()).toEqual({
      dateStr: day.dateStr,
      slot: 1,
      item: null,
      prefill: copied,
    });
  });

  it('edit opens the editor with the existing item', () => {
    const cmp = fixture.componentInstance as any;
    const day = cmp.days()[0];
    cmp.edit(day, day.cells[0]);

    expect(cmp.editing()?.item?.pkid).toBe(10);
    expect(cmp.isEditing(day.dateStr, 1)).toBeTrue();
  });

  it('+ moves the item down a slot and reloads; boundary moves are no-ops', () => {
    const cmp = fixture.componentInstance as any;
    const day = cmp.days()[0];

    cmp.moveDown(day.cells[0]); // slot 1 -> 2
    expect(serviceSpy.move).toHaveBeenCalledWith(10, 'down');

    serviceSpy.move.calls.reset();
    cmp.moveUp(day.cells[0]); // slot 1 up: out of range
    expect(serviceSpy.move).not.toHaveBeenCalled();

    cmp.moveUp({ slot: 2, item: null }); // empty cell: no-op
    expect(serviceSpy.move).not.toHaveBeenCalled();
  });

  it('delete confirms then removes the item and reloads', () => {
    const confirm = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirm;
    });
    serviceSpy.query.calls.reset();

    (fixture.componentInstance as any).remove(item);

    expect(serviceSpy.delete).toHaveBeenCalledWith(10);
    expect(serviceSpy.query).toHaveBeenCalledTimes(1);
  });
});
