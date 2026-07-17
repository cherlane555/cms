import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  PromoClipboard,
} from '@core/models/featured-promo-item.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

/**
 * Inline editor for one (day, slot) cell of the weekly grid.
 * Edit mode when `item` is set; New mode otherwise (optionally prefilled from the clipboard).
 * The PromoCode is resolved to Promotion_pkid via the lookup before saving.
 */
@Component({
  selector: 'app-featured-promo-item-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, RowAuditBadge],
  templateUrl: './featured-promo-item-form.html',
  styleUrl: './featured-promo-item-form.scss',
})
export class FeaturedPromoItemForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookups = inject(LookupService);
  private readonly messages = inject(MessageService);

  /** Target cell ('yyyy-MM-dd'). */
  @Input({ required: true }) scheduleOn = '';
  @Input({ required: true }) trainingCenterPkid = 0;
  @Input({ required: true }) slot = 0;
  /** Existing record (Edit) or null (New). */
  @Input() item: FeaturedPromoItem | null = null;
  /** Copied values to clone into a New form (Paste). */
  @Input() prefill: PromoClipboard | null = null;

  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  protected readonly form = this.fb.nonNullable.group({
    promoCode: ['', Validators.required],
    topic: ['', Validators.required],
    description: [''],
  });

  protected saving = false;

  // Promotion_pkid resolved for the current promoCode value.
  private promotionPkid = 0;
  private lookedUpCode = '';
  // Clicking the 查詢 button blurs the PromoCode input first, firing both the
  // (blur) and (onClick) handlers for the same click — guard against the duplicate call.
  private lookingUp = false;

  ngOnInit(): void {
    const source = this.item ?? this.prefill;
    if (source) {
      this.form.patchValue({
        promoCode: source.promoCode,
        topic: source.topic,
        description: source.description,
      });
      this.promotionPkid = source.promotionPkid;
      this.lookedUpCode = source.promoCode;
    }
  }

  /** Resolve PromoCode → Promotion_pkid; fill empty Topic/Description from the promotion. */
  protected lookup(): void {
    const code = this.form.getRawValue().promoCode.trim();
    if (!code || this.lookingUp) {
      return;
    }
    this.lookingUp = true;
    this.lookups.getPromotionByCode(code).subscribe({
      next: (promo) => {
        this.lookingUp = false;
        this.promotionPkid = promo.pkid;
        this.lookedUpCode = code;
        if (!this.form.getRawValue().topic) {
          this.form.patchValue({ topic: promo.topic });
        }
        if (!this.form.getRawValue().description) {
          this.form.patchValue({ description: promo.description });
        }
        this.messages.add({ severity: 'success', summary: '已找到促銷代碼', detail: code });
      },
      error: () => {
        this.lookingUp = false;
        this.promotionPkid = 0;
        this.messages.add({ severity: 'error', summary: '查無促銷代碼', detail: code });
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const code = this.form.getRawValue().promoCode.trim();
    this.saving = true;

    // Re-resolve the code unless the current value is the one already looked up.
    if (this.promotionPkid > 0 && code === this.lookedUpCode) {
      this.submit(this.promotionPkid);
      return;
    }
    this.lookups.getPromotionByCode(code).subscribe({
      next: (promo) => {
        this.promotionPkid = promo.pkid;
        this.lookedUpCode = code;
        this.submit(promo.pkid);
      },
      error: () => {
        this.saving = false;
        this.messages.add({ severity: 'error', summary: '查無促銷代碼', detail: code });
      },
    });
  }

  private submit(promotionPkid: number): void {
    const value = this.form.getRawValue();
    const request: FeaturedPromoItemRequest = {
      pkid: this.item?.pkid ?? 0,
      scheduleOn: this.scheduleOn,
      trainingCenterPkid: this.trainingCenterPkid,
      slot: this.slot,
      promotionPkid,
      topic: value.topic.trim(),
      description: value.description.trim(),
    };

    if (this.item) {
      this.service.update(request).subscribe({
        next: () => this.onSaveSuccess(),
        error: (err) => this.onSaveError(err),
      });
    } else {
      this.service.create(request).subscribe({
        next: () => this.onSaveSuccess(),
        error: (err) => this.onSaveError(err),
      });
    }
  }

  private onSaveSuccess(): void {
    this.saving = false;
    this.messages.add({ severity: 'success', summary: '已儲存', detail: this.form.getRawValue().topic });
    this.saved.emit();
  }

  private onSaveError(err: { status?: number }): void {
    this.saving = false;
    const detail = err.status === 409 ? '該日期時段已有資料' : '儲存失敗';
    this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}
