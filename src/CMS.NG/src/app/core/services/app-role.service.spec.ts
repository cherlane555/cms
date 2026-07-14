import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { AppRoleService } from './app-role.service';
import { AppRole, AppRoleQuery, AppRoleRequest } from '@core/models/app-role.model';

describe('AppRoleService', () => {
  let service: AppRoleService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/app-roles`;

  const sample: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3,
    userIds: ['helen', 'miles@uuu.com.tw'],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppRoleService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppRoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET and returns roles', () => {
    let result: AppRole[] | undefined;
    service.getAll().subscribe((r) => (result = r));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result?.length).toBe(1);
    expect(result?.[0].roleId).toBe('Admin');
  });

  it('query POSTs the filter to /query', () => {
    const query: AppRoleQuery = { keyword: 'adm', permissionLevel: 1 };
    service.query(query).subscribe();

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs a single role and encodes the id', () => {
    service.getById('a/b').subscribe();
    const req = httpMock.expectOne(`${base}/a%2Fb`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create POSTs the request', () => {
    const request: AppRoleRequest = {
      pkid: 0,
      roleId: 'Editor',
      roleName: 'Editor',
      permissionLevel: 50,
      description: '編輯',
      userIds: ['helen'],
    };
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 3, roleId: 'Editor' });
  });

  it('update PUTs the request', () => {
    const request: AppRoleRequest = {
      pkid: 1,
      roleId: 'Admin',
      roleName: 'Administrator',
      permissionLevel: 1,
      description: '系統管理員',
      userIds: [],
    };
    service.update(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(null);
  });

  it('delete DELETEs by encoded id', () => {
    service.delete('Admin').subscribe();
    const req = httpMock.expectOne(`${base}/Admin`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
