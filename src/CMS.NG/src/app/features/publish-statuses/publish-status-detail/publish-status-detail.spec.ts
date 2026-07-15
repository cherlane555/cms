import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PublishStatusDetail } from './publish-status-detail';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusDetail', () => {
  let fixture: ComponentFixture<PublishStatusDetail>;
  let serviceSpy: jasmine.SpyObj<PublishStatusService>;

  const status: PublishStatus = {
    pkid: 2,
    description: '已發布',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false,
  };

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('PublishStatusService', ['getById']);
    serviceSpy.getById.and.returnValue(of(status));

    await TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
      providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PublishStatusService, useValue: serviceSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '2' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusDetail);
    fixture.detectChanges();
  });

  it('loads the status by numeric pkid', () => {
    expect(serviceSpy.getById).toHaveBeenCalledWith(2);
    const cmp = fixture.componentInstance as any;
    expect(cmp.status().pkid).toBe(2);
  });

  it('renders status fields', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('已發布');
  });
});
