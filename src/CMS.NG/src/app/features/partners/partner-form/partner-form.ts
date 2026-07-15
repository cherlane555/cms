import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PartnerService } from '@core/services/partner.service';
import { PartnerRequest } from '@core/models/partner.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-partner-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    InputNumberModule,
    InputTextModule,
    ToastModule,
    RowAuditBadge,
  ],
  providers: [MessageService],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss',
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PartnerService);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  // pkid is IDENTITY (DB-assigned) — not an editable form field; exposed for the audit badge.
  protected readonly pkid = signal(0);

  protected readonly form = this.fb.group({
    name: this.fb.nonNullable.control('', [Validators.required]),
    appKey: this.fb.nonNullable.control('', [Validators.required]),
    nameOnPartnerMenu: this.fb.nonNullable.control('', [Validators.required]),
    nameOnCourseDetailPage: this.fb.nonNullable.control('', [Validators.required]),
    displayOrder: this.fb.nonNullable.control(0, [Validators.required]),
    imageFilename: this.fb.control<string | null>(null),
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    if (idParam) {
      this.service.getById(Number(idParam)).subscribe({
        next: (partner) => {
          this.pkid.set(partner.pkid);
          this.form.patchValue({
            name: partner.name,
            appKey: partner.appKey,
            nameOnPartnerMenu: partner.nameOnPartnerMenu,
            nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
            displayOrder: partner.displayOrder,
            imageFilename: partner.imageFilename,
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入資料' });
        },
      });
    } else {
      this.loading.set(false);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const request: PartnerRequest = {
      pkid: this.pkid(),
      name: raw.name,
      appKey: raw.appKey,
      nameOnPartnerMenu: raw.nameOnPartnerMenu,
      nameOnCourseDetailPage: raw.nameOnCourseDetailPage,
      displayOrder: raw.displayOrder,
      imageFilename: raw.imageFilename?.trim() || null,
    };

    const onOk = () => {
      this.messages.add({ severity: 'success', summary: '儲存成功', detail: request.name });
      this.router.navigate(['/partners']);
    };

    const onError = () => {
      this.saving.set(false);
      this.messages.add({ severity: 'error', summary: '儲存失敗', detail: '儲存失敗，請稍後再試' });
    };

    if (this.isEdit()) {
      this.service.update(request).subscribe({ next: () => onOk(), error: onError });
    } else {
      this.service.create(request).subscribe({ next: () => onOk(), error: onError });
    }
  }

  protected cancel(): void {
    this.router.navigate(['/partners']);
  }
}
