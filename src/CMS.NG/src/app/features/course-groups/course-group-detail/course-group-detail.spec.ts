import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CourseGroupDetail } from './course-group-detail';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

const group: CourseGroup = { pkid: 1, description: '資料庫管理' };

describe('CourseGroupDetail', () => {
  let serviceSpy: jasmine.SpyObj<CourseGroupService>;

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getById']);
    serviceSpy.getById.and.returnValue(of(group));

    await TestBed.configureTestingModule({
      imports: [CourseGroupDetail],
      providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        { provide: CourseGroupService, useValue: serviceSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } },
        },
      ],
    }).compileComponents();
  });

  it('loads the course group by the route id and renders it', () => {
    const fixture = TestBed.createComponent(CourseGroupDetail);
    fixture.detectChanges();

    expect(serviceSpy.getById).toHaveBeenCalledWith(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('資料庫管理');
    expect(text).toContain('課程群組詳細');
  });
});
