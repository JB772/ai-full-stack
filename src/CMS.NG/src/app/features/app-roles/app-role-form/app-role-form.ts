import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService } from 'primeng/api';
import { AppRoleRequest } from '../app-role.model';
import { AppRoleService } from '../app-role.service';

@Component({
  selector: 'app-app-role-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, InputNumberModule],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss'
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppRoleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  private pkid = 0;

  protected readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required, Validators.maxLength(200)]],
    roleName: ['', [Validators.required, Validators.maxLength(200)]],
    permissionLevel: [100, [Validators.required, Validators.min(0)]],
    description: ['', [Validators.maxLength(400)]]
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam) {
      return;
    }

    this.isEdit.set(true);
    this.pkid = Number(idParam);
    // RoleId is the natural key referenced by AppUserRole — immutable once created.
    this.form.controls.roleId.disable();
    this.loading.set(true);

    this.service.getByPkid(this.pkid).subscribe({
      next: role => {
        this.form.patchValue({
          roleId: role.roleId,
          roleName: role.roleName,
          permissionLevel: role.permissionLevel,
          description: role.description ?? ''
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該角色。' });
        this.router.navigate(['/app-roles']);
      }
    });
  }

  protected isInvalid(control: 'roleId' | 'roleName' | 'permissionLevel' | 'description'): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.dirty || c.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: AppRoleRequest = {
      pkid: this.pkid,
      roleId: value.roleId,
      roleName: value.roleName,
      permissionLevel: value.permissionLevel,
      description: value.description.trim() === '' ? null : value.description
    };

    this.saving.set(true);

    if (this.isEdit()) {
      this.service.update(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({ severity: 'success', summary: '儲存成功', detail: `角色「${request.roleId}」已更新。` });
          this.router.navigate(['/app-roles', this.pkid]);
        },
        error: err => this.handleError(err)
      });
      return;
    }

    this.service.create(request).subscribe({
      next: created => {
        this.saving.set(false);
        this.messageService.add({ severity: 'success', summary: '新增成功', detail: `角色「${created.roleId}」已建立。` });
        this.router.navigate(['/app-roles', created.pkid]);
      },
      error: err => this.handleError(err)
    });
  }

  protected cancel(): void {
    this.router.navigate(this.isEdit() ? ['/app-roles', this.pkid] : ['/app-roles']);
  }

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存角色時發生錯誤。'
    });
  }
}
