import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppUserLookup } from '@core/models/app-role.model';

@Component({
  selector: 'app-app-role-detail',
  imports: [ButtonModule, CardModule, TagModule, ToastModule],
  providers: [MessageService],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss',
})
export class AppRoleDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AppRoleService);
  private readonly lookups = inject(LookupService);
  private readonly messages = inject(MessageService);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly userLabels = signal<string[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      role: this.service.getById(id),
      users: this.lookups.getAppUsers(),
    }).subscribe({
      next: ({ role, users }) => {
        this.role.set(role);
        const map = new Map<string, AppUserLookup>(users.map((u) => [u.userId, u]));
        this.userLabels.set(
          role.userIds.map((uid) => map.get(uid)?.label ?? uid),
        );
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '找不到角色' });
      },
    });
  }

  protected back(): void {
    this.router.navigate(['/app-roles']);
  }

  protected edit(): void {
    const role = this.role();
    if (role) {
      this.router.navigate(['/app-roles', role.roleId, 'edit']);
    }
  }
}
