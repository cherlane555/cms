import { Component, computed, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { AuthService, ADMIN_ROLE } from '@core/auth/auth.service';

interface NavItem {
  label: string;
  route?: string;
}

interface NavGroup {
  label: string;
  icon: string;
  items: NavItem[];
  /** When set, the group is shown only if the signed-in user has this role. */
  requiresRole?: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, ToastModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly brand = 'UWA';

  protected readonly userName = this.auth.userName;
  protected readonly isAuthenticated = this.auth.isAuthenticated;

  // Sidebar nav. The 系統管理 Admin group is role-gated; the other entries are visual placeholders.
  private readonly allNavGroups: NavGroup[] = [
    {
      label: '首頁 Home',
      icon: 'pi pi-home',
      items: [{ label: '上稿作業 FeaturedPromoItem', route: '/featured-promo-items' }],
    },
    {
      label: '課程管理 Course',
      icon: 'pi pi-folder',
      items: [
        { label: '課程 Course', route: '/courses' },
        { label: '合作廠商 Partner', route: '/partners' },
        { label: '課程群組 CourseGroup', route: '/course-groups' },
      ],
    },
    { label: '說明會 Seminar', icon: 'pi pi-comments', items: [] },
    { label: '活動管理 Promotion', icon: 'pi pi-megaphone', items: [] },
    { label: '線上報名 Forms', icon: 'pi pi-pencil', items: [] },
    { label: '網站資訊 WebInfo', icon: 'pi pi-globe', items: [] },
    { label: '考試中心 TestingCenter', icon: 'pi pi-verified', items: [] },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      requiresRole: ADMIN_ROLE,
      items: [
        { label: '角色 AppRole', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', route: '/publish-statuses' },
        { label: '使用者 AppUser', route: '/app-users' },
      ],
    },
  ];

  // Hide role-gated groups the user can't access (Admin shows only for Admin-role users).
  protected readonly navGroups = computed(() =>
    this.allNavGroups.filter((g) => !g.requiresRole || this.auth.hasRole(g.requiresRole)),
  );

  protected readonly expanded = signal<string>('系統管理 Admin');

  protected toggle(label: string): void {
    this.expanded.set(this.expanded() === label ? '' : label);
  }

  protected logout(): void {
    this.auth.clear();
    void this.router.navigate(['/login']);
  }
}
