import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService } from 'primeng/api';
import { PartnerRequest } from '../partner.model';
import { PartnerService } from '../partner.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

type ControlName =
  | 'name'
  | 'appKey'
  | 'nameOnPartnerMenu'
  | 'nameOnCourseDetailPage'
  | 'displayOrder'
  | 'imageFilename';

@Component({
  selector: 'app-partner-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, InputNumberModule, RowAuditBadgeComponent],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss'
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  /** Shown in the edit-mode toolbar, so the template needs access. */
  protected pkid = 0;

  // No pkid control: it is a smallint IDENTITY — unknown when adding, immutable when editing.
  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(50)]],
    appKey: ['', [Validators.required, Validators.maxLength(10)]],
    nameOnPartnerMenu: ['', [Validators.required, Validators.maxLength(200)]],
    nameOnCourseDetailPage: ['', [Validators.required, Validators.maxLength(50)]],
    displayOrder: [0, [Validators.required, Validators.min(0)]],
    imageFilename: ['', [Validators.maxLength(50)]]
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam) {
      return;
    }

    this.isEdit.set(true);
    this.pkid = Number(idParam);
    this.loading.set(true);

    this.service.getByPkid(this.pkid).subscribe({
      next: partner => {
        this.form.patchValue({
          name: partner.name,
          appKey: partner.appKey,
          nameOnPartnerMenu: partner.nameOnPartnerMenu,
          nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
          displayOrder: partner.displayOrder,
          imageFilename: partner.imageFilename ?? ''
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該合作夥伴。' });
        this.router.navigate(['/partners']);
      }
    });
  }

  protected isInvalid(control: ControlName): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.dirty || c.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const imageFilename = value.imageFilename.trim();

    const request: PartnerRequest = {
      // 0 on create — the database generates the real key.
      pkid: this.isEdit() ? this.pkid : 0,
      name: value.name.trim(),
      appKey: value.appKey.trim(),
      nameOnPartnerMenu: value.nameOnPartnerMenu.trim(),
      nameOnCourseDetailPage: value.nameOnCourseDetailPage.trim(),
      displayOrder: value.displayOrder,
      // The column is nullable — send null, not '', so it stays NULL.
      imageFilename: imageFilename === '' ? null : imageFilename
    };

    this.saving.set(true);

    if (this.isEdit()) {
      this.service.update(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: '儲存成功',
            detail: `合作夥伴「${request.name}」已更新。`
          });
          this.router.navigate(['/partners', this.pkid]);
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
          detail: `合作夥伴「${created.name}」已建立。`
        });
        this.router.navigate(['/partners', created.pkid]);
      },
      error: err => this.handleError(err)
    });
  }

  protected cancel(): void {
    this.router.navigate(this.isEdit() ? ['/partners', this.pkid] : ['/partners']);
  }

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存合作夥伴時發生錯誤。'
    });
  }
}
