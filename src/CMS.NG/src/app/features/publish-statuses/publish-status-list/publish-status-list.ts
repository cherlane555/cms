import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusQuery } from '@core/models/publish-status.model';

const FILTERS_KEY = 'publish-status-list-filters';
const SORT_KEY = 'publish-status-list-sort';
const PAGE_KEY = 'publish-status-list-page';

@Component({
  selector: 'app-publish-status-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TagModule,
    TooltipModule,
    ConfirmDialogModule,
    ToastModule,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './publish-status-list.html',
  styleUrl: './publish-status-list.scss',
})
export class PublishStatusList implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly statuses = signal<PublishStatus[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterOpen = signal(false);

  // Tri-state bool filter options (null = 不限).
  protected readonly triStateOptions = [
    { label: '不限', value: null },
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  protected filter: PublishStatusQuery = {
    keyword: null,
    isDraft: null,
    isPublished: null,
    isDiscontinued: null,
  };

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
        this.statuses.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得發布狀態清單' });
      },
    });
  }

  private normalizedFilter(): PublishStatusQuery {
    return {
      keyword: this.filter.keyword?.trim() || null,
      isDraft: this.filter.isDraft ?? null,
      isPublished: this.filter.isPublished ?? null,
      isDiscontinued: this.filter.isDiscontinued ?? null,
    };
  }

  protected applyFilter(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
    this.first = 0;
    this.filterOpen.set(false);
    this.load();
  }

  protected clearFilter(): void {
    this.filter = { keyword: null, isDraft: null, isPublished: null, isDiscontinued: null };
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
    this.router.navigate(['/publish-statuses/new']);
  }

  protected view(status: PublishStatus): void {
    this.router.navigate(['/publish-statuses', status.pkid]);
  }

  protected edit(status: PublishStatus): void {
    this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
  }

  protected remove(status: PublishStatus): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${status.pkid}</b>「${status.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.service.delete(status.pkid).subscribe({
          next: () => {
            this.messages.add({ severity: 'success', summary: '已刪除', detail: status.description });
            this.load();
          },
          error: () =>
            this.messages.add({ severity: 'error', summary: '刪除失敗', detail: status.description }),
        });
      },
    });
  }
}
