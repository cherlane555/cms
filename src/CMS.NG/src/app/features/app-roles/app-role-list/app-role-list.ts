import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppRoleService } from '@core/services/app-role.service';
import { AppRole, AppRoleQuery } from '@core/models/app-role.model';

const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

@Component({
  selector: 'app-app-role-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    InputNumberModule,
    TooltipModule,
    ConfirmDialogModule,
    ToastModule,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss',
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterOpen = signal(false);

  protected filter: AppRoleQuery = { keyword: null, permissionLevel: null };

  // Restored from session storage.
  protected sortField = '';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  private restoreState(): void {
    const f = sessionStorage.getItem(FILTERS_KEY);
    if (f) {
      this.filter = { ...this.filter, ...JSON.parse(f) };
    }
    const s = sessionStorage.getItem(SORT_KEY);
    if (s) {
      const { sortField, sortOrder } = JSON.parse(s);
      this.sortField = sortField ?? '';
      this.sortOrder = sortOrder ?? 1;
    }
    const p = sessionStorage.getItem(PAGE_KEY);
    if (p) {
      const { first, rows } = JSON.parse(p);
      this.first = first ?? 0;
      this.rows = rows ?? 20;
    }
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.normalizedFilter()).subscribe({
      next: (data) => {
        this.roles.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得角色清單' });
      },
    });
  }

  private normalizedFilter(): AppRoleQuery {
    return {
      keyword: this.filter.keyword?.trim() || null,
      permissionLevel: this.filter.permissionLevel ?? null,
    };
  }

  protected applyFilter(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
    this.first = 0;
    this.filterOpen.set(false);
    this.load();
  }

  protected clearFilter(): void {
    this.filter = { keyword: null, permissionLevel: null };
    sessionStorage.removeItem(FILTERS_KEY);
    this.first = 0;
    this.load();
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? '';
    this.sortOrder = event.order ?? 1;
    sessionStorage.setItem(
      SORT_KEY,
      JSON.stringify({ sortField: this.sortField, sortOrder: this.sortOrder }),
    );
  }

  protected onPage(event: TableLazyLoadEvent): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
    sessionStorage.setItem(PAGE_KEY, JSON.stringify({ first: this.first, rows: this.rows }));
  }

  protected add(): void {
    this.router.navigate(['/app-roles/new']);
  }

  protected view(role: AppRole): void {
    this.router.navigate(['/app-roles', role.roleId]);
  }

  protected edit(role: AppRole): void {
    this.router.navigate(['/app-roles', role.roleId, 'edit']);
  }

  protected remove(role: AppRole): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${role.pkid}</b>「${role.roleId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.service.delete(role.roleId).subscribe({
          next: () => {
            this.messages.add({ severity: 'success', summary: '已刪除', detail: role.roleId });
            this.load();
          },
          error: () =>
            this.messages.add({ severity: 'error', summary: '刪除失敗', detail: role.roleId }),
        });
      },
    });
  }
}
