import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { AuthService } from '@core/auth/auth.service';
import {
  PASSWORD_COMPLEXITY_MESSAGE,
  passwordComplexityValidator,
  passwordsMatchValidator,
} from '@core/auth/password.validators';

@Component({
  selector: 'app-profile',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    InputTextModule,
    PasswordModule,
    TagModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messages = inject(MessageService);

  protected readonly complexityMessage = PASSWORD_COMPLEXITY_MESSAGE;

  // Read-only identity — shown, never editable here.
  protected readonly userId = this.auth.userId;
  protected readonly roles = this.auth.roles;

  protected readonly saving = signal(false);
  protected readonly changingPassword = signal(false);

  protected readonly form = this.fb.group({
    userName: this.fb.nonNullable.control(this.auth.userName(), [Validators.required]),
  });

  protected readonly passwordForm = this.fb.group(
    {
      currentPassword: this.fb.nonNullable.control('', [Validators.required]),
      newPassword: this.fb.nonNullable.control('', [
        Validators.required,
        passwordComplexityValidator,
      ]),
      confirmPassword: this.fb.nonNullable.control('', [Validators.required]),
    },
    { validators: passwordsMatchValidator },
  );

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const userName = this.form.getRawValue().userName.trim();
    if (!userName) {
      this.form.controls.userName.setErrors({ required: true });
      return;
    }

    this.saving.set(true);
    this.auth.updateUserName(userName).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.form.controls.userName.setValue(res.userName);
        this.messages.add({
          severity: 'success',
          summary: '已儲存 Saved',
          detail: '顯示名稱已更新 Your display name was updated.',
        });
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.messages.add({
          severity: 'error',
          summary: '儲存失敗 Save failed',
          detail:
            err.status === 400
              ? '請輸入顯示名稱 UserName is required.'
              : '請稍後再試 Please try again later.',
        });
      },
    });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.getRawValue();
    this.changingPassword.set(true);

    this.auth.changePassword(currentPassword, newPassword, confirmPassword).subscribe({
      next: () => {
        this.changingPassword.set(false);
        this.passwordForm.reset({ currentPassword: '', newPassword: '', confirmPassword: '' });
        this.messages.add({
          severity: 'success',
          summary: '已更新 Updated',
          detail: '密碼已變更，請下次以新密碼登入 Your password was changed.',
        });
      },
      error: (err: HttpErrorResponse) => {
        this.changingPassword.set(false);
        // Surface the server's message (e.g. wrong current password / complexity) when present.
        const serverMessage = err.error?.message as string | undefined;
        this.messages.add({
          severity: 'error',
          summary: '變更失敗 Change failed',
          detail: serverMessage ?? '請稍後再試 Please try again later.',
        });
      },
    });
  }
}
