import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { CourseGroupRequest } from '../course-group.model';
import { CourseGroupService } from '../course-group.service';

@Component({
  selector: 'app-course-group-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './course-group-form.html',
  styleUrl: './course-group-form.scss'
})
export class CourseGroupForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  private pkid = 0;

  /** Read-only echo of the IDENTITY key for the edit template — there is no pkid form control. */
  protected get pkidDisplay(): number {
    return this.pkid;
  }

  // pkid has no control: it is a smallint IDENTITY, so it is unknown on create and immutable on edit.
  // (Contrast PublishStatusForm, whose pkid IS caller-supplied and therefore does get a field.)
  // One field is the whole form — CourseGroup has exactly one writable column.
  protected readonly form = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(100)]]
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
      next: group => {
        this.form.patchValue({ description: group.description });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該課程群組。' });
        this.router.navigate(['/course-groups']);
      }
    });
  }

  protected isInvalid(control: 'description'): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.dirty || c.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const request: CourseGroupRequest = {
      // 0 on create — the DB generates the real key; on edit it identifies the row.
      pkid: this.isEdit() ? this.pkid : 0,
      description: this.form.getRawValue().description.trim()
    };

    this.saving.set(true);

    if (this.isEdit()) {
      this.service.update(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: '儲存成功',
            detail: `課程群組「${request.description}」已更新。`
          });
          this.router.navigate(['/course-groups', this.pkid]);
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
          detail: `課程群組「${created.description}」已建立。`
        });
        this.router.navigate(['/course-groups', created.pkid]);
      },
      error: err => this.handleError(err)
    });
  }

  protected cancel(): void {
    this.router.navigate(this.isEdit() ? ['/course-groups', this.pkid] : ['/course-groups']);
  }

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存課程群組時發生錯誤。'
    });
  }
}
