import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AppRoleList } from './app-role-list';
import { AppRoleService } from '@core/services/app-role.service';
import { AppRole } from '@core/models/app-role.model';

describe('AppRoleList', () => {
  let fixture: ComponentFixture<AppRoleList>;
  let serviceSpy: jasmine.SpyObj<AppRoleService>;

  const roles: AppRole[] = [
    {
      pkid: 1,
      roleId: 'Admin',
      roleName: 'Administrator',
      permissionLevel: 1,
      description: '系統管理員',
      userCount: 3,
      userIds: [],
    },
  ];

  beforeEach(async () => {
    serviceSpy = jasmine.createSpyObj('AppRoleService', ['query', 'delete']);
    serviceSpy.query.and.returnValue(of(roles));
    serviceSpy.delete.and.returnValue(of(void 0));
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AppRoleService, useValue: serviceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleList);
    fixture.detectChanges();
  });

  it('creates and loads roles on init', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(serviceSpy.query).toHaveBeenCalledTimes(1);
    expect((fixture.componentInstance as any).roles().length).toBe(1);
  });

  it('renders the role row and the user count', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Admin');
    expect(text).toContain('Administrator');
    expect(text).toContain('角色 AppRole');
  });

  it('applyFilter persists filters and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: 'adm', permissionLevel: 1 };
    cmp.applyFilter();
    expect(sessionStorage.getItem('app-role-list-filters')).toContain('adm');
    expect(serviceSpy.query).toHaveBeenCalledTimes(2);
  });

  it('clearFilter resets the filter and reloads', () => {
    const cmp = fixture.componentInstance as any;
    cmp.filter = { keyword: 'adm', permissionLevel: 1 };
    cmp.applyFilter();
    cmp.clearFilter();
    expect(sessionStorage.getItem('app-role-list-filters')).toBeNull();
    expect(cmp.filter.keyword).toBeNull();
  });
});
