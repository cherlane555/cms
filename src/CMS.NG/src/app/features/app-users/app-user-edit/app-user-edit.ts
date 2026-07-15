import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { LookupService } from '@core/services/lookup.service';
import { AuthService } from '@core/auth/auth.service';

@Component({
  selector: 'app-app-user-edit',
  imports: [ButtonModule, CardModule, InputTextModule, ConfirmDialogModule, ToastModule],
  providers: [ConfirmationService, MessageService],
  templateUrl: './app-user-edit.html',
})
export class AppUserEdit implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly lookup = inject(LookupService);
  private readonly auth = inject(AuthService);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  // Only Admins may reset another user's password (also enforced by the backend).
  protected readonly isAdmin = this.auth.isAdmin;

  protected readonly userId = signal('');
  protected readonly userName = signal('');
  protected readonly resetting = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.userId.set(id);
    // No single-user GET exists; resolve the display name from the AppUser lookup.
    this.lookup.getAppUsers().subscribe((users) => {
      this.userName.set(users.find((u) => u.userId === id)?.userName ?? '');
    });
  }

  protected confirmReset(): void {
    this.confirm.confirm({
      header: '重設密碼 Reset Password',
      message: `確定要將使用者 ${this.userId()} 的密碼重設為系統預設值嗎？
Reset this user's password to the system default?`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '確定 Reset',
      rejectLabel: '取消 Cancel',
      accept: () => this.reset(),
    });
  }

  private reset(): void {
    this.resetting.set(true);
    this.auth.resetPassword(this.userId()).subscribe({
      next: () => {
        this.resetting.set(false);
        this.messages.add({
          severity: 'success',
          summary: '已重設 Reset',
          detail: '密碼已重設為系統預設值 Password reset to the system default.',
        });
      },
      error: (err: HttpErrorResponse) => {
        this.resetting.set(false);
        this.messages.add({
          severity: 'error',
          summary: '重設失敗 Reset failed',
          detail:
            err.status === 403
              ? '需要管理員權限 Admin role required.'
              : '請稍後再試 Please try again later.',
        });
      },
    });
  }
}
