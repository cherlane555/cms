import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroupRequest } from '@core/models/course-group.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-group-form',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, InputTextModule, ToastModule, RowAuditBadge],
  providers: [MessageService],
  templateUrl: './course-group-form.html',
  styleUrl: './course-group-form.scss',
})
export class CourseGroupForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseGroupService);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  // pkid is IDENTITY (DB-assigned) — not an editable form field; exposed for the audit badge.
  protected readonly pkid = signal(0);

  protected readonly form = this.fb.group({
    description: this.fb.nonNullable.control('', [Validators.required]),
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    if (idParam) {
      this.service.getById(Number(idParam)).subscribe({
        next: (group) => {
          this.pkid.set(group.pkid);
          this.form.patchValue({ description: group.description });
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
    const request: CourseGroupRequest = {
      pkid: this.pkid(),
      description: raw.description,
    };

    const onOk = () => {
      this.messages.add({ severity: 'success', summary: '儲存成功', detail: request.description });
      this.router.navigate(['/course-groups']);
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
    this.router.navigate(['/course-groups']);
  }
}
