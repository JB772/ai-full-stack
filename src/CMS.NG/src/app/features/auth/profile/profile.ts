import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { AuthService } from '../auth.service';

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

  /** Read-only identity + roles pulled straight from the current session profile. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required, Validators.maxLength(200)]]
  });

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
}
