import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PartnerList } from './partner-list';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

describe('PartnerList', () => {
  let fixture: ComponentFixture<PartnerList>;
  let serviceSpy: jasmine.SpyObj<PartnerService>;

  const partners: Partner[] = [
    {
      pkid: 1,
      name: '恆逸資訊',
      appKey: 'uwa',
      nameOnPartnerMenu: '恆逸資訊教育訓練中心',
      nameOnCourseDetailPage: '恆逸',
      displayOrder: 10,
      imageFilename: 'uwa.png',
    },
  ];

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('PartnerService', ['query', 'delete']);
    serviceSpy.query.and.returnValue(of(partners));
    serviceSpy.delete.and.returnValue(of(void 0));
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PartnerService, useValue: serviceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    fixture.detectChanges();
  });

  it('creates and loads partners on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(serviceSpy.query).toHaveBeenCalledTimes(1);
    expect((fixture.componentInstance as any).partners().length).toBe(1);
  });

  it('renders the partner row and the header', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('恆逸資訊');
    expect(text).toContain('合作廠商 Partner');
  });

  it('applyFilter persists filters and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: '恆逸' };
    cmp.applyFilter();
    expect(sessionStorage.getItem('partner-list-filters')).toContain('恆逸');
    expect(serviceSpy.query).toHaveBeenCalledTimes(2);
  });

  it('clearFilter resets the filter and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: '恆逸' };
    cmp.applyFilter();
    cmp.clearFilter();
    expect(sessionStorage.getItem('partner-list-filters')).toBeNull();
    expect(cmp.filter.keyword).toBeNull();
  });
});
