import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { Course, CourseRequest } from '../course.model';
import { CourseService } from '../course.service';
import { fromIsoDate, toIsoDate } from '../date.util';
import { PartnerService } from '../../partners/partner.service';
import { CourseGroupService } from '../../course-groups/course-group.service';
import { PublishStatusService } from '../../publish-statuses/publish-status.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

type ControlName =
  | 'title'
  | 'officialTitle'
  | 'courseId'
  | 'prodCourseId'
  | 'friendlyUrl'
  | 'displayOrder'
  | 'partnerPkid'
  | 'courseGroupPkid'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'material'
  | 'objective'
  | 'target'
  | 'prerequisites'
  | 'outline'
  | 'towardCertOrExam'
  | 'note'
  | 'otherInfo'
  | 'canRepeat';

interface Option {
  label: string;
  value: number;
}

@Component({
  selector: 'app-course-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    TextareaModule,
    DatePickerModule,
    SelectModule,
    CheckboxModule,
    RowAuditBadgeComponent
  ],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss'
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseService);
  private readonly partnerService = inject(PartnerService);
  private readonly courseGroupService = inject(CourseGroupService);
  private readonly publishStatusService = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly partnerOptions = signal<Option[]>([]);
  protected readonly courseGroupOptions = signal<Option[]>([]);
  protected readonly publishStatusOptions = signal<Option[]>([]);

  /** Shown in the edit-mode toolbar, so the template needs access. */
  protected pkid = 0;

  // No pkid control: it is an int IDENTITY — unknown when adding, immutable when editing.
  protected readonly form = this.fb.group({
    title: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    officialTitle: this.fb.control<string | null>(null, [Validators.maxLength(300)]),
    courseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    prodCourseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    friendlyUrl: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    displayOrder: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    partnerPkid: this.fb.control<number | null>(null, [Validators.required]),
    courseGroupPkid: this.fb.control<number | null>(null),
    publishStatusPkid: this.fb.control<number | null>(null, [Validators.required]),
    scheduleOn: this.fb.control<Date | null>(null, [Validators.required]),
    scheduleOff: this.fb.control<Date | null>(null, [Validators.required]),
    hour: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    listPrice: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    learningCredit: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    material: this.fb.control<string | null>(null, [Validators.maxLength(500)]),
    objective: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    target: this.fb.control<string | null>(null, [Validators.maxLength(500)]),
    prerequisites: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    outline: this.fb.control<string | null>(null),
    towardCertOrExam: this.fb.control<string | null>(null),
    note: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    otherInfo: this.fb.control<string | null>(null, [Validators.maxLength(4000)]),
    canRepeat: this.fb.nonNullable.control(false)
  });

  ngOnInit(): void {
    this.loading.set(true);

    forkJoin({
      partners: this.partnerService.getAll(),
      courseGroups: this.courseGroupService.getAll(),
      publishStatuses: this.publishStatusService.getAll()
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses }) => {
        this.partnerOptions.set(partners.map(p => ({ label: p.name, value: p.pkid })));
        this.courseGroupOptions.set(courseGroups.map(g => ({ label: g.description, value: g.pkid })));
        this.publishStatusOptions.set(publishStatuses.map(s => ({ label: s.description, value: s.pkid })));

        const idParam = this.route.snapshot.paramMap.get('id');
        if (idParam) {
          this.isEdit.set(true);
          this.pkid = Number(idParam);
          this.loadCourse();
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '無法取得下拉選項。' });
      }
    });
  }

  private loadCourse(): void {
    this.service.getByPkid(this.pkid).subscribe({
      next: course => {
        this.patch(course);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該課程。' });
        this.router.navigate(['/courses']);
      }
    });
  }

  private patch(course: Course): void {
    this.form.patchValue({
      title: course.title,
      officialTitle: course.officialTitle,
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      friendlyUrl: course.friendlyUrl,
      displayOrder: course.displayOrder,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid,
      publishStatusPkid: course.publishStatusPkid,
      scheduleOn: fromIsoDate(course.scheduleOn),
      scheduleOff: fromIsoDate(course.scheduleOff),
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      material: course.material,
      objective: course.objective,
      target: course.target,
      prerequisites: course.prerequisites,
      outline: course.outline,
      towardCertOrExam: course.towardCertOrExam,
      note: course.note,
      otherInfo: course.otherInfo,
      canRepeat: course.canRepeat
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

    const request: CourseRequest = {
      // 0 on create — the database generates the real key.
      pkid: this.isEdit() ? this.pkid : 0,
      title: value.title.trim(),
      officialTitle: this.nullIfBlank(value.officialTitle),
      courseId: value.courseId.trim(),
      prodCourseId: value.prodCourseId.trim(),
      friendlyUrl: value.friendlyUrl.trim(),
      displayOrder: value.displayOrder,
      partnerPkid: value.partnerPkid!,
      courseGroupPkid: value.courseGroupPkid ?? null,
      publishStatusPkid: value.publishStatusPkid!,
      // Required — the form guarantees a Date here.
      scheduleOn: toIsoDate(value.scheduleOn)!,
      scheduleOff: toIsoDate(value.scheduleOff)!,
      hour: value.hour,
      listPrice: value.listPrice,
      learningCredit: value.learningCredit,
      material: this.nullIfBlank(value.material),
      objective: this.nullIfBlank(value.objective),
      target: this.nullIfBlank(value.target),
      prerequisites: this.nullIfBlank(value.prerequisites),
      outline: this.nullIfBlank(value.outline),
      towardCertOrExam: this.nullIfBlank(value.towardCertOrExam),
      note: this.nullIfBlank(value.note),
      otherInfo: this.nullIfBlank(value.otherInfo),
      canRepeat: value.canRepeat
    };

    this.saving.set(true);

    if (this.isEdit()) {
      this.service.update(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: '儲存成功',
            detail: `課程「${request.title}」已更新。`
          });
          this.router.navigate(['/courses', this.pkid]);
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
          detail: `課程「${created.title}」已建立。`
        });
        this.router.navigate(['/courses', created.pkid]);
      },
      error: err => this.handleError(err)
    });
  }

  protected cancel(): void {
    this.router.navigate(this.isEdit() ? ['/courses', this.pkid] : ['/courses']);
  }

  /** Nullable columns must stay NULL — send null, not an empty/whitespace string. */
  private nullIfBlank(value: string | null): string | null {
    const trimmed = value?.trim() ?? '';
    return trimmed === '' ? null : trimmed;
  }

  private handleError(err: { error?: { message?: string } }): void {
    this.saving.set(false);
    this.messageService.add({
      severity: 'error',
      summary: '儲存失敗',
      detail: err?.error?.message ?? '儲存課程時發生錯誤。'
    });
  }
}
