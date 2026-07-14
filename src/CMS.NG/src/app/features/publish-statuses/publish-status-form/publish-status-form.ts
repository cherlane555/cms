import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatusRequest } from '@core/models/publish-status.model';

@Component({
  selector: 'app-publish-status-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    InputNumberModule,
    InputTextModule,
    CheckboxModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss',
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PublishStatusService);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.group({
    // pkid is the user-assigned tinyint PK: required in new mode, disabled in edit mode.
    pkid: this.fb.control<number | null>(null, [Validators.required, Validators.min(1)]),
    description: this.fb.nonNullable.control('', [Validators.required]),
    isDraft: this.fb.nonNullable.control(false),
    isPublished: this.fb.nonNullable.control(false),
    isDiscontinued: this.fb.nonNullable.control(false),
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    if (idParam) {
      this.service.getById(Number(idParam)).subscribe({
        next: (status) => {
          this.form.patchValue({
            pkid: status.pkid,
            description: status.description,
            isDraft: status.isDraft,
            isPublished: status.isPublished,
            isDiscontinued: status.isDiscontinued,
          });
          this.form.controls.pkid.disable(); // primary key is not editable in edit mode
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
    const request: PublishStatusRequest = {
      pkid: raw.pkid!,
      description: raw.description,
      isDraft: raw.isDraft,
      isPublished: raw.isPublished,
      isDiscontinued: raw.isDiscontinued,
    };

    const onOk = () => {
      this.messages.add({ severity: 'success', summary: '儲存成功', detail: request.description });
      this.router.navigate(['/publish-statuses']);
    };

    const onError = (err: { status?: number }) => {
      this.saving.set(false);
      const detail = err?.status === 409 ? '主代碼已存在' : '儲存失敗，請稍後再試';
      this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
    };

    if (this.isEdit()) {
      this.service.update(request).subscribe({ next: () => onOk(), error: onError });
    } else {
      this.service.create(request).subscribe({ next: () => onOk(), error: onError });
    }
  }

  protected cancel(): void {
    this.router.navigate(['/publish-statuses']);
  }
}
