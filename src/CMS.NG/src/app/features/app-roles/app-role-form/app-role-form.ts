import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRoleRequest, AppUserLookup } from '@core/models/app-role.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-app-role-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    InputTextModule,
    InputNumberModule,
    MultiSelectModule,
    ToastModule,
    RowAuditBadge,
  ],
  providers: [MessageService],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss',
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AppRoleService);
  private readonly lookups = inject(LookupService);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly users = signal<AppUserLookup[]>([]);

  // pkid is the IDENTITY surrogate (RoleId is the PK) — exposed for the audit badge.
  protected readonly pkid = signal(0);

  protected readonly form = this.fb.group({
    roleId: this.fb.nonNullable.control('', [Validators.required]),
    roleName: this.fb.nonNullable.control('', [Validators.required]),
    permissionLevel: this.fb.nonNullable.control(100, [Validators.required]),
    description: this.fb.control<string | null>('', [Validators.required]),
    userIds: this.fb.nonNullable.control<string[]>([]),
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!id);

    forkJoin({
      users: this.lookups.getAppUsers(),
      role: id ? this.service.getById(id) : of(null),
    }).subscribe({
      next: ({ users, role }) => {
        this.users.set(users);
        if (role) {
          this.pkid.set(role.pkid);
          this.form.patchValue({
            roleId: role.roleId,
            roleName: role.roleName,
            permissionLevel: role.permissionLevel,
            description: role.description,
            userIds: role.userIds,
          });
          this.form.controls.roleId.disable(); // primary key is not editable
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入資料' });
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const request: AppRoleRequest = {
      pkid: this.pkid(),
      roleId: raw.roleId,
      roleName: raw.roleName,
      permissionLevel: raw.permissionLevel,
      description: raw.description,
      userIds: raw.userIds,
    };

    const onOk = () => {
      this.messages.add({ severity: 'success', summary: '儲存成功', detail: request.roleId });
      this.router.navigate(['/app-roles']);
    };

    const onError = (err: { status?: number }) => {
      this.saving.set(false);
      const detail = err?.status === 409 ? '角色代碼已存在' : '儲存失敗，請稍後再試';
      this.messages.add({ severity: 'error', summary: '儲存失敗', detail });
    };

    if (this.isEdit()) {
      this.service.update(request).subscribe({ next: () => onOk(), error: onError });
    } else {
      this.service.create(request).subscribe({ next: () => onOk(), error: onError });
    }
  }

  protected cancel(): void {
    this.router.navigate(['/app-roles']);
  }
}
