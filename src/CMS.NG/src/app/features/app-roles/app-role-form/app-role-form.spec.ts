import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AppRoleForm } from './app-role-form';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppUserLookup } from '@core/models/app-role.model';

const users: AppUserLookup[] = [
  { userId: 'helen', userName: 'helen', label: 'helen (helen)' },
];

const role: AppRole = {
  pkid: 1,
  roleId: 'Admin',
  roleName: 'Administrator',
  permissionLevel: 1,
  description: '系統管理員',
  userCount: 1,
  userIds: ['helen'],
};

function setup(id: string | null) {
  const roleSpy = jasmine.createSpyObj<AppRoleService>('AppRoleService', [
    'getById',
    'create',
    'update',
  ]);
  roleSpy.getById.and.returnValue(of(role));
  roleSpy.create.and.returnValue(of(role));
  roleSpy.update.and.returnValue(of(void 0));

  const lookupSpy = jasmine.createSpyObj<LookupService>('LookupService', ['getAppUsers']);
  lookupSpy.getAppUsers.and.returnValue(of(users));

  TestBed.configureTestingModule({
    imports: [AppRoleForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: AppRoleService, useValue: roleSpy },
      { provide: LookupService, useValue: lookupSpy },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
      },
    ],
  });

  const fixture = TestBed.createComponent(AppRoleForm);
  fixture.detectChanges();
  return { fixture, roleSpy, lookupSpy };
}

describe('AppRoleForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  describe('new mode', () => {
    let fixture: ComponentFixture<AppRoleForm>;
    let roleSpy: jasmine.SpyObj<AppRoleService>;

    beforeEach(() => ({ fixture, roleSpy } = setup(null)));

    it('creates in add mode with defaults', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeFalse();
      expect(cmp.form.controls.permissionLevel.value).toBe(100);
      expect(roleSpy.getById).not.toHaveBeenCalled();
    });

    it('does not save an invalid (empty) form', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(roleSpy.create).not.toHaveBeenCalled();
    });

    it('calls create when the form is valid', () => {
      const cmp = fixture.componentInstance as any;
      cmp.form.setValue({
        roleId: 'Editor',
        roleName: 'Editor',
        permissionLevel: 50,
        description: '編輯',
        userIds: ['helen'],
      });
      cmp.save();
      expect(roleSpy.create).toHaveBeenCalledTimes(1);
      const arg = roleSpy.create.calls.mostRecent().args[0];
      expect(arg.roleId).toBe('Editor');
      expect(arg.userIds).toEqual(['helen']);
    });
  });

  describe('edit mode', () => {
    let fixture: ComponentFixture<AppRoleForm>;
    let roleSpy: jasmine.SpyObj<AppRoleService>;

    beforeEach(() => ({ fixture, roleSpy } = setup('Admin')));

    it('loads the role and disables the roleId control', () => {
      const cmp = fixture.componentInstance as any;
      expect(cmp.isEdit()).toBeTrue();
      expect(roleSpy.getById).toHaveBeenCalledWith('Admin');
      expect(cmp.form.controls.roleName.value).toBe('Administrator');
      expect(cmp.form.controls.roleId.disabled).toBeTrue();
    });

    it('calls update on save (including the disabled roleId)', () => {
      const cmp = fixture.componentInstance as any;
      cmp.save();
      expect(roleSpy.update).toHaveBeenCalledTimes(1);
      const arg = roleSpy.update.calls.mostRecent().args[0];
      expect(arg.roleId).toBe('Admin');
      expect(arg.pkid).toBe(1);
    });
  });
});
