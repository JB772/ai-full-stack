import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService } from 'primeng/api';
import { PublishStatusRequest } from '../publish-status.model';
import { PublishStatusService } from '../publish-status.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-publish-status-form',
  imports: [ReactiveFormsModule, ButtonModule, CheckboxModule, InputTextModule, InputNumberModule, RowAuditBadgeComponent],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss'
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  /** Shown in the edit-mode toolbar (row-audit badge), so the template needs access. */
  protected pkid = 0;

  protected readonly form = this.fb.nonNullable.group({
    // pkid is a caller-supplied tinyint, so on create it is a required, user-entered field.
    pkid: [0, [Validators.required, Validators.min(0), Validators.max(255)]],
    description: ['', [Validators.required, Validators.maxLength(50)]],
    isDraft: [false],
    isPublished: [false],
    isDiscontinued: [false]
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam) {
      return;
    }

    this.isEdit.set(true);
    this.pkid = Number(idParam);
    // pkid is the primary key — immutable once created.
    this.form.controls.pkid.disable();
    this.loading.set(true);

    this.service.getByPkid(this.pkid).subscribe({
      next: status => {
        this.form.patchValue({
          pkid: status.pkid,
          description: status.description,
          isDraft: status.isDraft,
          isPublished: status.isPublished,
          isDiscontinued: status.isDiscontinued
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該發布狀態。' });
        this.router.navigate(['/publish-statuses']);
      }
    });
  }

  protected isInvalid(control: 'pkid' | 'description'): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.dirty || c.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the pkid control even while it is disabled in edit mode.
    const value = this.form.getRawValue();
    const request: PublishStatusRequest = {
      pkid: this.isEdit() ? this.pkid : value.pkid,
      description: value.description.trim(),
      isDraft: value.isDraft,
      isPublished: value.isPublished,
      isDiscontinued: value.isDiscontinued
    };

    this.saving.set(true);

    if (this.isEdit()) {
      this.service.update(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: '儲存成功',
            detail: `發布狀態「${request.description}」已更新。`
          });
          this.router.navigate(['/publish-statuses', this.pkid]);
        },
        error: err => this.handleError(err)
      });
      return;
    }

    this.service.create(request).subscribe({
      next: created => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '新增成功',
          detail: `發布狀態「${created.description}」已建立。`
        });
        this.router.navigate(['/publish-statuses', created.pkid]);
      },
      error: err => this.handleError(err)
    });
  }

  protected cancel(): void {
    this.router.navigate(this.isEdit() ? ['/publish-statuses', this.pkid] : ['/publish-statuses']);
  }

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存發布狀態時發生錯誤。'
    });
  }
}
