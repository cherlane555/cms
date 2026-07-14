import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PublishStatusList } from './publish-status-list';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusList', () => {
  let fixture: ComponentFixture<PublishStatusList>;
  let serviceSpy: jasmine.SpyObj<PublishStatusService>;

  const statuses: PublishStatus[] = [
    {
      pkid: 1,
      description: '草稿',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    },
  ];

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('PublishStatusService', ['query', 'delete']);
    serviceSpy.query.and.returnValue(of(statuses));
    serviceSpy.delete.and.returnValue(of(void 0));
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PublishStatusService, useValue: serviceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    fixture.detectChanges();
  });

  it('creates and loads statuses on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(serviceSpy.query).toHaveBeenCalledTimes(1);
    expect((fixture.componentInstance as any).statuses().length).toBe(1);
  });

  it('renders the status row and the header', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('草稿');
    expect(text).toContain('發布狀態 PublishStatus');
  });

  it('applyFilter persists filters and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: '草', isDraft: true, isPublished: null, isDiscontinued: null };
    cmp.applyFilter();
    expect(sessionStorage.getItem('publish-status-list-filters')).toContain('草');
    expect(serviceSpy.query).toHaveBeenCalledTimes(2);
  });

  it('clearFilter resets the filter and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: '草', isDraft: true, isPublished: null, isDiscontinued: null };
    cmp.applyFilter();
    cmp.clearFilter();
    expect(sessionStorage.getItem('publish-status-list-filters')).toBeNull();
    expect(cmp.filter.keyword).toBeNull();
    expect(cmp.filter.isDraft).toBeNull();
  });
});
