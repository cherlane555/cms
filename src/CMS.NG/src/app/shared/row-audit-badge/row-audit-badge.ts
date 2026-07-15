import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

/**
 * Reusable toolbar badge showing a record's Row Audit history.
 * Inline it shows the latest change (action + user + time); clicking opens a
 * dialog with the record's full audit trail, newest first.
 * With no pkid yet (new, unsaved record) it shows a neutral "no history" state.
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [DatePipe, DialogModule, TagModule],
  templateUrl: './row-audit-badge.html',
  styleUrl: './row-audit-badge.scss',
})
export class RowAuditBadge {
  private readonly service = inject(RowAuditService);

  readonly tableName = input.required<string>();
  readonly pkid = input<number | string | null | undefined>(null);

  protected readonly entries = signal<RowAuditEntry[]>([]);
  protected readonly dialogVisible = signal(false);
  protected readonly latest = computed(() => this.entries()[0] ?? null);

  constructor() {
    effect(() => {
      const tableName = this.tableName();
      const pkid = this.pkid();
      if (pkid === null || pkid === undefined || pkid === 0 || pkid === '') {
        this.entries.set([]); // unsaved record: nothing to fetch
        return;
      }
      this.service.getForRecord(tableName, pkid).subscribe({
        next: (rows) => this.entries.set(rows),
        error: () => this.entries.set([]),
      });
    });
  }

  protected open(): void {
    this.dialogVisible.set(true);
  }
}
