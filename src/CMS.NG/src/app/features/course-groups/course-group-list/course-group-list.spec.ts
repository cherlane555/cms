import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CourseGroupList } from './course-group-list';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

describe('CourseGroupList', () => {
  let fixture: ComponentFixture<CourseGroupList>;
  let serviceSpy: jasmine.SpyObj<CourseGroupService>;

  const groups: CourseGroup[] = [{ pkid: 1, description: '資料庫管理' }];

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('CourseGroupService', ['query', 'delete']);
    serviceSpy.query.and.returnValue(of(groups));
    serviceSpy.delete.and.returnValue(of(void 0));
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [CourseGroupList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: CourseGroupService, useValue: serviceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupList);
    fixture.detectChanges();
  });

  it('creates and loads course groups on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(serviceSpy.query).toHaveBeenCalledTimes(1);
    expect((fixture.componentInstance as any).groups().length).toBe(1);
  });

  it('renders the course group row and the header', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('資料庫管理');
    expect(text).toContain('課程群組 CourseGroup');
  });

  it('defaults the sort to description ascending', () => {
    const cmp = fixture.componentInstance as any;
    expect(cmp.sortField).toBe('description');
    expect(cmp.sortOrder).toBe(1);
  });

  it('applyFilter persists filters and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: '資料庫' };
    cmp.applyFilter();
    expect(sessionStorage.getItem('course-group-list-filters')).toContain('資料庫');
    expect(serviceSpy.query).toHaveBeenCalledTimes(2);
  });

  it('clearFilter resets the filter and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: '資料庫' };
    cmp.applyFilter();
    cmp.clearFilter();
    expect(sessionStorage.getItem('course-group-list-filters')).toBeNull();
    expect(cmp.filter.keyword).toBeNull();
  });
});
