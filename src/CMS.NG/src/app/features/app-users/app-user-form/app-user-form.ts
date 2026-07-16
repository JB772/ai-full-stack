import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppUserRequest } from '../app-user.model';
import { AppUserService } from '../app-user.service';
import { AuthService } from '../../auth/auth.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-app-user-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, CheckboxModule, RowAuditBadgeComponent],
  templateUrl: './app-user-form.html',
  styleUrl: './app-user-form.scss'
})
export class AppUserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppUserService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly authService = inject(AuthService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly resetting = signal(false);

  // Reset-Password-to-Default is an Admin-only action; the button is hidden for non-Admins (and the
  // backend enforces the Admin role regardless — hiding the button is not the security control).
  protected readonly isAdmin = this.authService.isAdmin;

  /** Shown in the edit-mode toolbar (row-audit badge), so the template needs access. */
  protected pkid = 0;

  // No password control — the password is seeded from SysConfig on the backend and only changed
  // via the reset-password action. No pkid control — it is an int IDENTITY.
  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200)]],
    userName: ['', [Validators.required, Validators.maxLength(200)]],
    isActive: [true]
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam) {
      return;
    }

    this.isEdit.set(true);
    this.pkid = Number(idParam);
    // UserId is the natural key referenced by AppUserRole — immutable once created.
    this.form.controls.userId.disable();
    this.loading.set(true);

    this.service.getByPkid(this.pkid).subscribe({
      next: user => {
        this.form.patchValue({
          userId: user.userId,
          userName: user.userName,
          isActive: user.isActive
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該使用者。' });
        this.router.navigate(['/app-users']);
      }
    });
  }

  protected isInvalid(control: 'userId' | 'userName' | 'isActive'): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.dirty || c.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: AppUserRequest = {
      pkid: this.pkid,
      userId: value.userId,
      userName: value.userName,
      isActive: value.isActive
    };

    this.saving.set(true);

    if (this.isEdit()) {
      this.service.update(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({ severity: 'success', summary: '儲存成功', detail: `使用者「${request.userId}」已更新。` });
          this.router.navigate(['/app-users', this.pkid]);
        },
        error: err => this.handleError(err)
      });
      return;
    }

    this.service.create(request).subscribe({
      next: created => {
        this.saving.set(false);
        this.messageService.add({ severity: 'success', summary: '新增成功', detail: `使用者「${created.userId}」已建立 (密碼為系統預設值)。` });
        this.router.navigate(['/app-users', created.pkid]);
      },
      error: err => this.handleError(err)
    });
  }

  protected cancel(): void {
    this.router.navigate(this.isEdit() ? ['/app-users', this.pkid] : ['/app-users']);
  }

  /** Confirm, then reset the edited account's password to the system default (Admin only). */
  protected confirmResetPassword(): void {
    // UserId is the natural key; the disabled control still surfaces via getRawValue().
    const userId = this.form.getRawValue().userId;
    if (!this.isEdit() || !userId) {
      return;
    }

    this.confirmationService.confirm({
      header: '重設密碼確認',
      message: `確定要將使用者「${userId}」的密碼重設為系統預設值？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '重設',
      rejectLabel: '取消',
      accept: () => this.resetPassword(userId)
    });
  }

  private resetPassword(userId: string): void {
    // Only the target userId is sent — no password/hash ever crosses the API.
    this.resetting.set(true);
    this.authService.resetPasswordToDefault(userId).subscribe({
      next: () => {
        this.resetting.set(false);
        this.messageService.add({ severity: 'success', summary: '重設成功', detail: `使用者「${userId}」的密碼已重設為預設值。` });
      },
      error: err => {
        this.resetting.set(false);
        const detail = err?.error?.message ?? '重設密碼時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '重設失敗', detail });
      }
    });
  }

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存使用者時發生錯誤。'
    });
  }
}
