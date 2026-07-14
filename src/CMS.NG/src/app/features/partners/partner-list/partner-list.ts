import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerQuery } from '@core/models/partner.model';

const FILTERS_KEY = 'partner-list-filters';
const SORT_KEY = 'partner-list-sort';
const PAGE_KEY = 'partner-list-page';

@Component({
  selector: 'app-partner-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    TooltipModule,
    ConfirmDialogModule,
    ToastModule,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './partner-list.html',
  styleUrl: './partner-list.scss',
})
export class PartnerList implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterOpen = signal(false);

  protected filter: PartnerQuery = { keyword: null };

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
        this.partners.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得廠商清單' });
      },
    });
  }

  private normalizedFilter(): PartnerQuery {
    return { keyword: this.filter.keyword?.trim() || null };
  }

  protected applyFilter(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
    this.first = 0;
    this.filterOpen.set(false);
    this.load();
  }

  protected clearFilter(): void {
    this.filter = { keyword: null };
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
    this.router.navigate(['/partners/new']);
  }

  protected view(partner: Partner): void {
    this.router.navigate(['/partners', partner.pkid]);
  }

  protected edit(partner: Partner): void {
    this.router.navigate(['/partners', partner.pkid, 'edit']);
  }

  protected remove(partner: Partner): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${partner.pkid}</b>「${partner.name}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.service.delete(partner.pkid).subscribe({
          next: () => {
            this.messages.add({ severity: 'success', summary: '已刪除', detail: partner.name });
            this.load();
          },
          error: () =>
            this.messages.add({ severity: 'error', summary: '刪除失敗', detail: partner.name }),
        });
      },
    });
  }
}
