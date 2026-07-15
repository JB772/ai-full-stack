import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { AppUserRequest } from '../app-user.model';
import { AppUserService } from '../app-user.service';

@Component({
  selector: 'app-app-user-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, CheckboxModule],
  templateUrl: './app-user-form.html',
  styleUrl: './app-user-form.scss'
})
export class AppUserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppUserService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  private pkid = 0;

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

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存使用者時發生錯誤。'
    });
  }
}
