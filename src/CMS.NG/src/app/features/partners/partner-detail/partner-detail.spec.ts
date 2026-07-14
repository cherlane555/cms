import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PartnerDetail } from './partner-detail';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

describe('PartnerDetail', () => {
  let fixture: ComponentFixture<PartnerDetail>;
  let serviceSpy: jasmine.SpyObj<PartnerService>;

  const partner: Partner = {
    pkid: 2,
    name: '碁峰',
    appKey: 'gp',
    nameOnPartnerMenu: '碁峰資訊',
    nameOnCourseDetailPage: '碁峰',
    displayOrder: 20,
    imageFilename: null,
  };

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('PartnerService', ['getById']);
    serviceSpy.getById.and.returnValue(of(partner));

    await TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PartnerService, useValue: serviceSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '2' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerDetail);
    fixture.detectChanges();
  });

  it('loads the partner by numeric pkid', () => {
    expect(serviceSpy.getById).toHaveBeenCalledWith(2);
    const cmp = fixture.componentInstance as any;
    expect(cmp.partner().pkid).toBe(2);
  });

  it('renders partner fields', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('碁峰');
  });
});
