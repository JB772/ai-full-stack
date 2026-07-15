import { Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { AuthService } from '../auth.service';

/**
 * New-password complexity, mirroring the backend `PasswordPolicy`: length >= 8 and at least 3 of the 4
 * character classes (uppercase / lowercase / digit / symbol). Empty is left to `Validators.required`.
 */
export function passwordComplexityValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '') as string;
  if (value === '') {
    return null;
  }
  const classes =
    (/[A-Z]/.test(value) ? 1 : 0) +
    (/[a-z]/.test(value) ? 1 : 0) +
    (/[0-9]/.test(value) ? 1 : 0) +
    (/[^A-Za-z0-9]/.test(value) ? 1 : 0);
  return value.length >= 8 && classes >= 3 ? null : { complexity: true };
}

/** Group-level validator: new password and its confirmation must match. */
export function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const next = group.get('newPassword')?.value;
  const confirm = group.get('confirmNewPassword')?.value;
  return next === confirm ? null : { mismatch: true };
}

/** Bilingual complexity message — identical wording to the backend's `PasswordPolicy.ComplexityMessage`. */
export const COMPLEXITY_MESSAGE =
  '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號' +
  ' (Password must be at least 8 characters and contain at least 3 of the 4 classes:' +
  ' uppercase / lowercase / digit / symbol.)';

/**
 * "My Profile" — lets the signed-in user view their account and edit only their own display name.
 * UserId and roles are read-only (the server derives identity from the JWT and never lets roles change
 * here). On a successful save the shell's user name refreshes via {@link AuthService.updateProfile}.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, TagModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss'
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  protected readonly saving = signal(false);
  protected readonly changingPassword = signal(false);

  /** Bilingual complexity hint/error text, shared verbatim with the backend policy. */
  protected readonly complexityMessage = COMPLEXITY_MESSAGE;

  /** Read-only identity + roles pulled straight from the current session profile. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required, Validators.maxLength(200)]]
  });

  /** Change-password form. New password is validated client-side to match the backend policy. */
  protected readonly pwForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordComplexityValidator]],
      confirmNewPassword: ['', [Validators.required]]
    },
    { validators: passwordsMatchValidator }
  );

  protected isInvalid(): boolean {
    const c = this.form.controls.userName;
    return c.invalid && (c.dirty || c.touched);
  }

  protected save(): void {
    const userName = this.form.controls.userName.value.trim();
    if (userName === '') {
      this.form.controls.userName.markAsTouched();
      this.form.controls.userName.setErrors({ required: true });
      return;
    }

    this.saving.set(true);
    this.auth.updateProfile(userName).subscribe({
      next: response => {
        this.saving.set(false);
        this.form.controls.userName.setValue(response.userName);
        this.form.markAsPristine();
        this.messageService.add({ severity: 'success', summary: '儲存成功', detail: '個人資料已更新。' });
      },
      error: err => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: err?.error?.message ?? '更新個人資料時發生錯誤。'
        });
      }
    });
  }

  /** True when the new-password field fails the complexity rule and the user has interacted with it. */
  protected showComplexityError(): boolean {
    const c = this.pwForm.controls.newPassword;
    return c.hasError('complexity') && (c.dirty || c.touched);
  }

  /** True when confirm doesn't match new (only once both are filled and the field was touched). */
  protected showMismatchError(): boolean {
    const c = this.pwForm.controls.confirmNewPassword;
    return this.pwForm.hasError('mismatch') && !c.hasError('required') && (c.dirty || c.touched);
  }

  protected changePassword(): void {
    if (this.pwForm.invalid) {
      this.pwForm.markAllAsTouched();
      return;
    }

    this.changingPassword.set(true);
    const { currentPassword, newPassword, confirmNewPassword } = this.pwForm.getRawValue();
    this.auth.changePassword({ currentPassword, newPassword, confirmNewPassword }).subscribe({
      next: () => {
        this.changingPassword.set(false);
        this.pwForm.reset({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
        this.messageService.add({ severity: 'success', summary: '密碼已更新', detail: '您的密碼已成功變更。' });
      },
      error: err => {
        this.changingPassword.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '變更失敗',
          detail: err?.error?.message ?? '變更密碼時發生錯誤。'
        });
      }
    });
  }
}
