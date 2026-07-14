import { Component, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  route?: string;
}

interface NavGroup {
  label: string;
  icon: string;
  items: NavItem[];
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly brand = 'UWA';

  // Sidebar nav. Only 角色 AppRole is wired to a route in this feature;
  // the other entries are visual placeholders for style reference.
  protected readonly navGroups: NavGroup[] = [
    { label: '首頁管理 Home', icon: 'pi pi-home', items: [] },
    { label: '課程管理 Course', icon: 'pi pi-folder', items: [] },
    { label: '說明會 Seminar', icon: 'pi pi-comments', items: [] },
    { label: '活動管理 Promotion', icon: 'pi pi-megaphone', items: [] },
    { label: '線上報名 Forms', icon: 'pi pi-pencil', items: [] },
    { label: '網站資訊 WebInfo', icon: 'pi pi-globe', items: [] },
    { label: '考試中心 TestingCenter', icon: 'pi pi-verified', items: [] },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      items: [
        { label: '角色 AppRole', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', route: '/publish-statuses' },
        { label: '使用者 AppUser' },
      ],
    },
  ];

  protected readonly expanded = signal<string>('系統管理 Admin');

  protected toggle(label: string): void {
    this.expanded.set(this.expanded() === label ? '' : label);
  }
}
