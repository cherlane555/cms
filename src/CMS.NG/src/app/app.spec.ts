import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { App } from './app';

@Component({ selector: 'app-stub', template: '' })
class StubPage {}

/** Unsigned JWT carrying the given roles, as the API emits them. */
function makeJwt(roles: string[]): string {
  const b64url = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const payload = {
    userName: 'Miles Sun',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':
      roles.length === 1 ? roles[0] : roles,
  };
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

function signIn(roles: string[], userName = 'Miles Sun'): void {
  sessionStorage.setItem('accessToken', makeJwt(roles));
  sessionStorage.setItem('userName', userName);
}

describe('App', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([{ path: 'stub', component: StubPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        MessageService, // the global <p-toast> in the template needs it
      ],
    }).compileComponents();
  });

  afterEach(() => sessionStorage.clear());

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders only the router-outlet (no sidebar) when signed out', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.sidebar')).toBeNull();
  });

  it('shows the shell, the signed-in user, and the Admin menu for an Admin user', () => {
    signIn(['Admin', 'User']);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.brand')?.textContent).toContain('UWA');
    expect(el.textContent).toContain('Miles Sun');
    expect(el.textContent).toContain('系統管理 Admin');
    expect(el.textContent).toContain('角色 AppRole');
  });

  it('hides the Admin menu for a non-Admin user but still shows the shell', () => {
    signIn(['User'], 'Reg User');
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('課程管理 Course');
    expect(el.textContent).not.toContain('系統管理 Admin');
  });

  describe('mobile sidebar drawer', () => {
    beforeEach(() => signIn(['Admin', 'User']));

    it('starts closed, and the menu-toggle button opens it', () => {
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;

      expect(el.querySelector('.sidebar')?.classList).not.toContain('open');
      expect(el.querySelector('.sidebar-backdrop')).toBeNull();

      el.querySelector<HTMLButtonElement>('.menu-toggle')!.click();
      fixture.detectChanges();

      expect(el.querySelector('.sidebar')?.classList).toContain('open');
      expect(el.querySelector('.sidebar-backdrop')).not.toBeNull();
      expect(el.querySelector('.menu-toggle')?.getAttribute('aria-expanded')).toBe('true');
    });

    it('clicking the backdrop closes the drawer', () => {
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;

      el.querySelector<HTMLButtonElement>('.menu-toggle')!.click();
      fixture.detectChanges();
      el.querySelector<HTMLElement>('.sidebar-backdrop')!.click();
      fixture.detectChanges();

      expect(el.querySelector('.sidebar')?.classList).not.toContain('open');
      expect(el.querySelector('.sidebar-backdrop')).toBeNull();
    });

    it('closes once a navigation lands, so picking a nav-item does not leave it open', async () => {
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      (fixture.componentInstance as any).toggleSidebar();
      fixture.detectChanges();
      expect((fixture.componentInstance as any).sidebarOpen()).toBe(true);

      await TestBed.inject(Router).navigateByUrl('/stub');
      fixture.detectChanges();

      expect((fixture.componentInstance as any).sidebarOpen()).toBe(false);
    });
  });
});
