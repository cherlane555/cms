import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  PromoClipboard,
  TrainingCenterLookup,
} from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemForm } from '../featured-promo-item-form/featured-promo-item-form';

const FILTERS_KEY = 'featured-promo-item-list-filters';
const SLOTS = [1, 2, 3] as const;
const WEEKDAY_LABELS = ['一', '二', '三', '四', '五', '六', '日'];

interface SlotCell {
  slot: number;
  item: FeaturedPromoItem | null;
}

interface DayGroup {
  dateStr: string; // 'yyyy-MM-dd'
  label: string; // 'M/D (一)'
  cells: SlotCell[];
}

interface EditingCell {
  dateStr: string;
  slot: number;
  item: FeaturedPromoItem | null;
  prefill: PromoClipboard | null;
}

/**
 * Weekly (Mon–Sun) publishing grid: one tab per TrainingCenter, one section per day,
 * three slots per day, edited inline. Custom-spec feature — not the standard p-table list.
 */
@Component({
  selector: 'app-featured-promo-item-list',
  imports: [ButtonModule, ConfirmDialogModule, ToastModule, TooltipModule, FeaturedPromoItemForm],
  providers: [ConfirmationService, MessageService],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss',
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookups = inject(LookupService);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly trainingCenters = signal<TrainingCenterLookup[]>([]);
  protected readonly activeTc = signal<number | null>(null);
  protected readonly weekStart = signal<Date>(FeaturedPromoItemList.mondayOf(new Date()));
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly editing = signal<EditingCell | null>(null);

  protected readonly clipboard = this.service.clipboard;

  protected readonly weekLabel = computed(() => {
    const start = this.weekStart();
    const end = FeaturedPromoItemList.addDays(start, 6);
    return `${start.getMonth() + 1}/${start.getDate()} -- ${end.getMonth() + 1}/${end.getDate()}`;
  });

  protected readonly days = computed<DayGroup[]>(() => {
    const start = this.weekStart();
    const items = this.items();
    return Array.from({ length: 7 }, (_, i) => {
      const date = FeaturedPromoItemList.addDays(start, i);
      const dateStr = FeaturedPromoItemList.toDateStr(date);
      return {
        dateStr,
        label: `${date.getMonth() + 1}/${date.getDate()} (${WEEKDAY_LABELS[i]})`,
        cells: SLOTS.map((slot) => ({
          slot,
          item:
            items.find((it) => it.scheduleOn.slice(0, 10) === dateStr && it.slot === slot) ?? null,
        })),
      };
    });
  });

  ngOnInit(): void {
    this.restoreState();
    this.lookups.getTrainingCenters().subscribe({
      next: (centers) => {
        this.trainingCenters.set(centers);
        if (this.activeTc() === null || !centers.some((c) => c.pkid === this.activeTc())) {
          this.activeTc.set(centers[0]?.pkid ?? null);
        }
        this.load();
      },
      error: () =>
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得訓練中心' }),
    });
  }

  private restoreState(): void {
    const raw = sessionStorage.getItem(FILTERS_KEY);
    if (!raw) {
      return;
    }
    const state = JSON.parse(raw) as { trainingCenterPkid?: number; weekStart?: string };
    if (state.trainingCenterPkid) {
      this.activeTc.set(state.trainingCenterPkid);
    }
    if (state.weekStart) {
      const [y, m, d] = state.weekStart.split('-').map(Number);
      this.weekStart.set(FeaturedPromoItemList.mondayOf(new Date(y, m - 1, d)));
    }
  }

  private persistState(): void {
    sessionStorage.setItem(
      FILTERS_KEY,
      JSON.stringify({
        trainingCenterPkid: this.activeTc(),
        weekStart: FeaturedPromoItemList.toDateStr(this.weekStart()),
      }),
    );
  }

  protected load(): void {
    const tc = this.activeTc();
    if (tc === null) {
      return;
    }
    this.loading.set(true);
    this.service
      .query({
        trainingCenterPkid: tc,
        scheduleFrom: FeaturedPromoItemList.toDateStr(this.weekStart()),
        scheduleTo: FeaturedPromoItemList.toDateStr(FeaturedPromoItemList.addDays(this.weekStart(), 6)),
      })
      .subscribe({
        next: (data) => {
          this.items.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得上稿清單' });
        },
      });
  }

  protected selectTab(pkid: number): void {
    if (this.activeTc() === pkid) {
      return;
    }
    this.activeTc.set(pkid);
    this.editing.set(null);
    this.persistState();
    this.load();
  }

  protected prevWeek(): void {
    this.shiftWeek(-7);
  }

  protected nextWeek(): void {
    this.shiftWeek(7);
  }

  private shiftWeek(days: number): void {
    this.weekStart.set(FeaturedPromoItemList.addDays(this.weekStart(), days));
    this.editing.set(null);
    this.persistState();
    this.load();
  }

  protected isEditing(dateStr: string, slot: number): boolean {
    const e = this.editing();
    return e !== null && e.dateStr === dateStr && e.slot === slot;
  }

  protected edit(day: DayGroup, cell: SlotCell): void {
    this.editing.set({ dateStr: day.dateStr, slot: cell.slot, item: cell.item, prefill: null });
  }

  protected paste(day: DayGroup, cell: SlotCell): void {
    const copied = this.clipboard();
    if (!copied || cell.item) {
      return;
    }
    this.editing.set({ dateStr: day.dateStr, slot: cell.slot, item: null, prefill: copied });
  }

  protected copy(item: FeaturedPromoItem): void {
    this.service.copy(item);
    this.messages.add({ severity: 'success', summary: '已複製', detail: item.promoCode });
  }

  protected onSaved(): void {
    this.editing.set(null);
    this.load();
  }

  protected onCancelled(): void {
    this.editing.set(null);
  }

  /** '+' — move the item one slot down (1→2), swapping with the occupant. */
  protected moveDown(cell: SlotCell): void {
    this.move(cell, 'down');
  }

  /** '-' — move the item one slot up (2→1), swapping with the occupant. */
  protected moveUp(cell: SlotCell): void {
    this.move(cell, 'up');
  }

  private move(cell: SlotCell, direction: 'up' | 'down'): void {
    if (!cell.item) {
      return;
    }
    const target = direction === 'down' ? cell.slot + 1 : cell.slot - 1;
    if (target < 1 || target > 3) {
      return;
    }
    this.service.move(cell.item.pkid, direction).subscribe({
      next: () => this.load(),
      error: () =>
        this.messages.add({ severity: 'error', summary: '移動失敗', detail: cell.item!.promoCode }),
    });
  }

  protected remove(item: FeaturedPromoItem): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.promoCode}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.service.delete(item.pkid).subscribe({
          next: () => {
            this.messages.add({ severity: 'success', summary: '已刪除', detail: item.promoCode });
            this.load();
          },
          error: () =>
            this.messages.add({ severity: 'error', summary: '刪除失敗', detail: item.promoCode }),
        });
      },
    });
  }

  // ---- date helpers (local time; avoid toISOString's UTC shift) ----

  static mondayOf(date: Date): Date {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const offset = (d.getDay() + 6) % 7; // Mon=0 … Sun=6
    d.setDate(d.getDate() - offset);
    return d;
  }

  static addDays(date: Date, days: number): Date {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    d.setDate(d.getDate() + days);
    return d;
  }

  static toDateStr(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }
}
