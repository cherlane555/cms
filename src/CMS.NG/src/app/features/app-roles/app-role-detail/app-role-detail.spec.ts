import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AppRoleDetail } from './app-role-detail';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppUserLookup } from '@core/models/app-role.model';

describe('AppRoleDetail', () => {
  let fixture: ComponentFixture<AppRoleDetail>;
  let roleSpy: jasmine.SpyObj<AppRoleService>;
  let lookupSpy: jasmine.SpyObj<LookupService>;

  const role: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    userIds: ['helen', 'miles@uuu.com.tw'],
  };

  const users: AppUserLookup[] = [
    { userId: 'helen', userName: 'helen', label: 'helen (helen)' },
    { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', label: 'Miles Sun (miles@uuu.com.tw)' },
  ];

  beforeEach(async () => {
    roleSpy = jasmine.createSpyObj('AppRoleService', ['getById']);
    roleSpy.getById.and.returnValue(of(role));
    lookupSpy = jasmine.createSpyObj('LookupService', ['getAppUsers']);
    lookupSpy.getAppUsers.and.returnValue(of(users));

    await TestBed.configureTestingModule({
      imports: [AppRoleDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AppRoleService, useValue: roleSpy },
        { provide: LookupService, useValue: lookupSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'Admin' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleDetail);
    fixture.detectChanges();
  });

  it('loads the role and maps user ids to labels', () => {
    expect(roleSpy.getById).toHaveBeenCalledWith('Admin');
    const cmp = fixture.componentInstance as any;
    expect(cmp.role().roleId).toBe('Admin');
    expect(cmp.userLabels()).toEqual(['helen (helen)', 'Miles Sun (miles@uuu.com.tw)']);
  });

  it('renders role fields', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
  });
});
